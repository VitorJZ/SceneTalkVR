using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class ConversationContextRestoreTests
    {
        [Test]
        public void RealLlmRestoresDialogueMessagesConditionAndErrorHistory()
        {
            var host = new GameObject("LLM History Restore Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                var detail = CreateDetail();

                service.RestoreConversationContext(detail);

                var messages = GetPrivateList(service, "chatHistory");
                var errors = GetPrivateList(service, "sessionErrorHistory");
                Assert.That(messages.Count, Is.EqualTo(4), "system + opening + user + assistant");
                Assert.That(errors.Count, Is.EqualTo(1));
                Assert.That(service.CurrentCondition.sessionId, Is.EqualTo("resume-session"));
                Assert.That(service.CurrentCondition.provider, Is.EqualTo("dialogue_avatar"));
                Assert.That(service.FeedbackSensitivity, Is.EqualTo("active"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static LearningSessionDetail CreateDetail()
        {
            var scene = new SpringScenePayload
            {
                taskType = "hotel_check_in",
                environmentType = "hotel_lobby",
                dialogueReply = "Welcome. May I have your name?",
                avatarRole = new AvatarRoleData { role = "clerk" },
                scene = new ScenePayload { mode = "skybox", skyboxUrl = "demo://hotel-lobby-360" }
            };
            var response = new SpringScenePayload
            {
                dialogueReply = "Thank you. Let me check your booking.",
                correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = true,
                    provider = "dialogue_avatar",
                    style = "recast",
                    errorType = "grammar"
                }
            };

            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary { sessionId = "resume-session", turnCount = 1 },
                settings = new ConversationSettingsSnapshot
                {
                    feedbackSensitivity = "active",
                    condition = new CorrectionExperimentCondition
                    {
                        sessionId = "resume-session",
                        scenarioId = "hotel_check_in",
                        provider = "dialogue_avatar",
                        style = "recast",
                        task = new SceneTalkExperimentTask { scenarioId = "hotel_check_in" }
                    }
                },
                sceneSnapshot = scene,
                turns = new[]
                {
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 0,
                        isOpening = true,
                        assistantText = scene.dialogueReply,
                        payload = scene
                    },
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 1,
                        userText = "I have booking.",
                        assistantText = response.dialogueReply,
                        payload = response
                    }
                }
            };
        }

        private static IList GetPrivateList(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(target);
        }
    }
}
