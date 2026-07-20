using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class LearningMemoryStoreTests
    {
        private string databasePath;
        private string historyRootPath;
        private GameObject serviceHost;
        private IDisposable unownedStore;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(
                Path.GetTempPath(),
                $"scenetalk-history-{Guid.NewGuid():N}.sqlite3");
            historyRootPath = Path.Combine(
                Path.GetTempPath(),
                $"scenetalk-history-assets-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (serviceHost != null)
            {
                serviceHost.GetComponent<LearningMemoryService>()?.Dispose();
                UnityEngine.Object.DestroyImmediate(serviceHost);
            }

            unownedStore?.Dispose();
            unownedStore = null;

            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
            if (Directory.Exists(historyRootPath))
            {
                Directory.Delete(historyRootPath, true);
            }
        }

        [Test]
        public void StorePersistsPagesTurnsAndStatisticsAcrossConnections()
        {
            var older = CreateSession("older", 1000);
            var newer = CreateSession("newer", 2000);

            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                store.CreateSession(older);
                store.CreateSession(newer);
                store.AppendTurn("newer", CreateLearnerTurn(1, 3000, true));

                Assert.That(store.CountSessions(), Is.EqualTo(2));
                Assert.That(store.ListSessions(0, 1).Single().sessionId, Is.EqualTo("newer"));

                var detail = store.GetSession("newer");
                Assert.That(detail.turns.Select(turn => turn.sequenceIndex), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(detail.summary.turnCount, Is.EqualTo(1));
                Assert.That(detail.summary.correctionCount, Is.EqualTo(1));
                detail.summary.title = "Updated Reservation";
                store.UpdateSession(detail);
            }

            using (var reopened = new SqliteLearningMemoryStore(databasePath))
            {
                reopened.Initialize();
                var restored = reopened.GetSession("newer");
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.summary.title, Is.EqualTo("Updated Reservation"));
                Assert.That(restored.turns[1].userText, Is.EqualTo("I is ready."));
                Assert.That(restored.turns[1].payload.correctionFeedback.correctedText, Is.EqualTo("I am ready."));
            }
        }

        [Test]
        public void DeleteSessionCascadesTurnsWithoutTouchingOtherSessions()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateSession(CreateSession("first", 1000));
            store.CreateSession(CreateSession("second", 2000));
            store.AppendTurn("first", CreateLearnerTurn(1, 3000, false));

            Assert.That(store.DeleteSession("first"), Is.True);
            Assert.That(store.GetSession("first"), Is.Null);
            Assert.That(store.GetSession("second"), Is.Not.Null);
            Assert.That(store.CountSessions(), Is.EqualTo(1));
        }

        [Test]
        public void SameScenarioCreatesIndependentHistoryRows()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateSession(CreateSession("session-a", 1000));
            store.CreateSession(CreateSession("session-b", 2000));

            var summaries = store.ListSessions(0, 5);
            Assert.That(summaries.Count, Is.EqualTo(2));
            Assert.That(summaries.Select(item => item.scenarioId).Distinct().Single(), Is.EqualTo("restaurant_reservation"));
            Assert.That(summaries.Select(item => item.sessionId), Is.EquivalentTo(new[] { "session-a", "session-b" }));
        }

        [Test]
        public void MemoryServiceCleansOrphansAndDeletesSessionAssets()
        {
            var store = new SqliteLearningMemoryStore(databasePath);
            unownedStore = store;
            store.Initialize();
            store.CreateSession(CreateSession("known-session", 1000));

            var knownAssets = Path.Combine(historyRootPath, "Assets", "known-session");
            var orphanAssets = Path.Combine(historyRootPath, "Assets", "orphan-session");
            Directory.CreateDirectory(knownAssets);
            Directory.CreateDirectory(orphanAssets);
            File.WriteAllText(Path.Combine(knownAssets, "panorama.png"), "known");
            File.WriteAllText(Path.Combine(orphanAssets, "panorama.png"), "orphan");

            serviceHost = new GameObject("Learning Memory Test Host");
            var memory = serviceHost.AddComponent<LearningMemoryService>();
            memory.ConfigureStoreForTests(store, historyRootPath);
            unownedStore = null;

            Assert.That(Directory.Exists(knownAssets), Is.True);
            Assert.That(Directory.Exists(orphanAssets), Is.False);

            Assert.That(memory.DeleteSession("known-session"), Is.True);
            Assert.That(Directory.Exists(knownAssets), Is.False);
            Assert.That(memory.GetSession("known-session"), Is.Null);
        }

        private static LearningSessionDetail CreateSession(string id, long timestamp)
        {
            var payload = CreatePayload(false);
            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary
                {
                    sessionId = id,
                    title = "Restaurant Reservation",
                    scenarioId = "restaurant_reservation",
                    taskType = "restaurant_reservation",
                    environmentType = "restaurant",
                    correctionProvider = "assistant_agent",
                    correctionStyle = "explicit",
                    createdAtUnixMs = timestamp,
                    updatedAtUnixMs = timestamp
                },
                settings = new ConversationSettingsSnapshot
                {
                    brainMode = "DirectRealLlm",
                    feedbackSensitivity = "moderate",
                    condition = new CorrectionExperimentCondition
                    {
                        sessionId = id,
                        scenarioId = "restaurant_reservation",
                        provider = "assistant_agent",
                        style = "explicit",
                        task = new SceneTalkExperimentTask { scenarioId = "restaurant_reservation" }
                    }
                },
                sceneSnapshot = payload,
                turns = new[]
                {
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 0,
                        isOpening = true,
                        createdAtUnixMs = timestamp,
                        assistantText = payload.dialogueReply,
                        payload = payload
                    }
                }
            };
        }

        private static DialogueTurnRecord CreateLearnerTurn(int index, long timestamp, bool corrected)
        {
            var payload = CreatePayload(corrected);
            payload.dialogueReply = "Thanks. What time would you like?";
            return new DialogueTurnRecord
            {
                sequenceIndex = index,
                createdAtUnixMs = timestamp,
                userText = "I is ready.",
                assistantText = payload.dialogueReply,
                payload = payload
            };
        }

        private static SpringScenePayload CreatePayload(bool corrected)
        {
            return new SpringScenePayload
            {
                taskType = "restaurant_reservation",
                environmentType = "restaurant",
                dialogueReply = "Good evening. How can I help?",
                avatarRole = new AvatarRoleData { role = "barista" },
                scene = new ScenePayload { mode = "skybox", skyboxUrl = "demo://restaurant-360" },
                correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = corrected,
                    provider = "assistant_agent",
                    style = "explicit",
                    errorType = corrected ? "grammar" : "none",
                    originalText = corrected ? "I is ready." : string.Empty,
                    correctedText = corrected ? "I am ready." : string.Empty,
                    feedbackText = corrected ? "Try: I am ready." : string.Empty
                }
            };
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
