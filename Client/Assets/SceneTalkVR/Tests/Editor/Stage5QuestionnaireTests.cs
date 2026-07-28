using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage5QuestionnaireTests
    {
        private QuestionnaireCatalog catalog;
        private ExperimentV11ProtocolConfig protocol;

        [SetUp] public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset");
            protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
            Assert.That(catalog, Is.Not.Null); Assert.That(protocol, Is.Not.Null);
        }

        [Test] public void FormalQuestionnaire_HasExpectedSectionsAndCounts()
        {
            var items = catalog.GetEnabledItems("formal_condition_v1", protocol);
            Assert.That(items.Count, Is.EqualTo(16));
            Assert.That(items.Count(x => x.sectionId == "role_clarity"), Is.EqualTo(2));
            Assert.That(items.Count(x => x.sectionId == "conversation_continuity"), Is.EqualTo(3));
            Assert.That(items.Count(x => x.sectionId == "interest_enjoyment"), Is.EqualTo(5));
            Assert.That(items.Count(x => x.sectionId == "pressure_tension"), Is.EqualTo(2));
            Assert.That(items.Count(x => x.sectionId == "learning_support"), Is.EqualTo(4));
        }

        [Test] public void ItemIds_AreUniqueAndPromptsAreBilingual()
        {
            var items = catalog.Questionnaires.SelectMany(x => x.Items).ToArray();
            Assert.That(items.Select(x => x.itemId).Distinct().Count(), Is.EqualTo(items.Length));
            Assert.That(items.All(x => !string.IsNullOrWhiteSpace(x.itemVersion) && !string.IsNullOrWhiteSpace(x.promptEnglish) && !string.IsNullOrWhiteSpace(x.promptChinese)), Is.True);
        }

        [Test] public void MissingLikertRange_FailsFormalValidation()
        {
            var bad = ScriptableObject.CreateInstance<QuestionnaireCatalog>();
            bad.EditorSet("test", new[] { new QuestionnaireDefinition { questionnaireId = "formal_condition_v1", sections = new[] { new QuestionnaireSection { sectionId = "role_clarity", items = new[] { new QuestionnaireItem { itemId = "bad", itemVersion = "1", promptEnglish = "e", promptChinese = "中", itemType = QuestionnaireItemType.Likert } } } } } });
            Assert.That(bad.ValidateFormal(protocol, out var error), Is.False); StringAssert.Contains("scale_range_invalid:bad", error);
            UnityEngine.Object.DestroyImmediate(bad);
        }

        [Test] public void UnconfirmedSocialComfort_IsExcluded()
            => Assert.That(catalog.GetEnabledItems("formal_condition_v1", protocol).Any(x => x.sectionId == "social_comfort"), Is.False);

        [Test] public void ConfirmedSocialComfort_TestProtocolEnablesItems()
        {
            var test = ScriptableObject.Instantiate(protocol); ConfirmDecision(test, "formal_social_comfort", "include");
            Assert.That(catalog.GetEnabledItems("formal_condition_v1", test).Count(x => x.sectionId == "social_comfort"), Is.EqualTo(3));
            UnityEngine.Object.DestroyImmediate(test);
        }

        [Test] public void ReverseScore_UsesScaleBounds_AndPreservesRaw()
        {
            var service = BeginService(); Assert.That(service.SetResponse("formal_ie_02", "2", out var error), Is.True, error);
            var response = service.ActiveSession.responses.Single(); Assert.That(response.rawValue, Is.EqualTo("2")); Assert.That(response.scoredValue, Is.EqualTo(6));
        }

        [Test] public void RequiredItems_BlockSubmission()
        {
            var service = BeginService(); Assert.That(service.CanSubmit(out var error), Is.False); StringAssert.StartsWith("required_item_missing", error);
        }

        [Test] public void EmptyQuestionnaire_CanSkipAndWritesAuditableTerminalRecord()
        {
            var folder = TempFolder(); try
            {
                var service = BeginService();
                Assert.That(service.CanSkip(out var error), Is.True, error);
                Assert.That(service.Skip(folder, out error), Is.True, error);
                Assert.That(service.ActiveSession.completionStatus, Is.EqualTo(QuestionnaireCompletionStatus.Skipped));
                Assert.That(service.ActiveSession.conditionStatus, Is.EqualTo(ConditionRunStatus.Completed));
                Assert.That(service.ActiveSession.completionRate, Is.Zero);
                Assert.That(service.ActiveSession.hasMissing, Is.True);
                Assert.That(service.ActiveSession.skippedAtUtc, Is.Not.Empty);
                Assert.That(service.ActiveSession.completionReason, Is.EqualTo("participant_skipped"));
                var events = File.ReadAllText(Directory.GetFiles(folder, "*questionnaire_events_v1.jsonl").Single());
                StringAssert.Contains("\"eventType\":\"QuestionnaireSkipped\"", events);
                StringAssert.Contains("\"questionnaireLinkageKey\":\"ql-test\"", events);
                Assert.That(service.Skip(folder, out error), Is.False);
                Assert.That(error, Is.EqualTo("questionnaire_already_skipped"));
                Assert.That(service.SetResponse("formal_rc_01", "4", out error), Is.False);
                Assert.That(error, Is.EqualTo("questionnaire_not_editable"));
            }
            finally { DeleteFolder(folder); }
        }

        [Test] public void PartiallyAnsweredQuestionnaire_SkipPreservesResponsesWithoutSubmissionMetadata()
        {
            var folder = TempFolder(); try
            {
                var service = BeginService(); Assert.That(service.SetResponse("formal_rc_01", "5", out _), Is.True);
                Assert.That(service.Skip(folder, out var error), Is.True, error);
                var response = service.ActiveSession.responses.Single();
                Assert.That(response.rawValue, Is.EqualTo("5"));
                Assert.That(response.questionnaireStatus, Is.EqualTo("Skipped"));
                Assert.That(response.submittedAtUtc, Is.Empty);
                Assert.That(response.questionnaireSubmittedAtUtc, Is.Empty);
                Assert.That(response.conditionStatus, Is.EqualTo("Completed"));
                var json = File.ReadAllText(Directory.GetFiles(folder, "*questionnaire_responses_v1.jsonl").Single());
                StringAssert.Contains("\"questionnaireStatus\":\"Skipped\"", json);
            }
            finally { DeleteFolder(folder); }
        }

        [Test] public void LinkageMismatch_RejectsRestore()
        {
            var service = BeginService(); service.SetResponse("formal_rc_01", "4", out _);
            var restored = NewService(); Assert.That(restored.Restore(service.DraftPath, "wrong", protocol.ProtocolVersion, catalog.CatalogVersion, out var error), Is.False);
            Assert.That(error, Is.EqualTo("questionnaire_linkage_mismatch"));
        }

        [Test] public void InterruptedQuestionnaire_RestoresAnswers()
        {
            var service = BeginService(); service.SetResponse("formal_rc_01", "5", out _);
            var restored = NewService(); Assert.That(restored.Restore(service.DraftPath, "ql-test", protocol.ProtocolVersion, catalog.CatalogVersion, out var error), Is.True, error);
            Assert.That(restored.ActiveSession.responses.Single().rawValue, Is.EqualTo("5"));
        }

        [Test] public void SubmitCannotRepeat_AndReopenCreatesRevision()
        {
            var folder = TempFolder(); try
            {
                var service = BeginService(); FillRequired(service); Assert.That(service.Submit(folder, out var error), Is.True, error);
                Assert.That(service.Submit(folder, out error), Is.False); Assert.That(error, Is.EqualTo("questionnaire_already_submitted"));
                Assert.That(service.Reopen("exp-1", out error), Is.True, error); Assert.That(service.ActiveSession.revision, Is.EqualTo(2));
                Assert.That(service.ActiveSession.previousRevisionId, Is.EqualTo("ql-test-r1"));
            }
            finally { DeleteFolder(folder); }
        }

        [Test] public void FormalRanking_RejectsDuplicateRanks_AndKeepsStringLabels()
        {
            var ranking = FormalRanking(); ranking.rankings[1].rank = 1;
            Assert.That(ranking.ValidateUnique(new[] { "NE", "NR", "SE", "SR" }, out _), Is.False);
            ranking = FormalRanking(); Assert.That(ranking.ValidateUnique(new[] { "NE", "NR", "SE", "SR" }, out var error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "NE", "NR", "SE", "SR" }, ranking.rankings.Select(x => x.conditionCode));
        }

        [Test] public void PilotRanking_RejectsDuplicateEmbodiments()
        {
            var values = new[] { "voice_only", "floating_orb", "floating_orb" };
            var ranking = new PreferenceRankingResponse { rankings = values.Select((x, i) => new PreferenceRankEntry { rank = i + 1, embodimentCondition = x }).ToArray() };
            Assert.That(ranking.ValidateUnique(new[] { "voice_only", "floating_orb", "humanoid_agent" }, out _), Is.False);
        }

        [Test] public void JsonlAndCsv_ContainHumanReadableCondition()
        {
            var folder = TempFolder(); try
            {
                var service = BeginService(); FillRequired(service); service.Submit(folder, out _);
                var json = File.ReadAllText(Directory.GetFiles(folder, "*questionnaire_responses_v1.jsonl").Single()); var csv = File.ReadAllText(Directory.GetFiles(folder, "*.csv").Single());
                StringAssert.Contains("\"formalConditionCode\":\"NE\"", json); StringAssert.Contains("formalConditionCode", csv); StringAssert.Contains("\"NE\"", csv);
            }
            finally { DeleteFolder(folder); }
        }

        [Test] public void Lifecycle_AwaitingToInProgressToCompleted_UsesLinkage()
        {
            using var fixture = new LifecycleFixture("stage5-flow", protocol, catalog);
            fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done");
            Assert.That(fixture.Controller.StartCurrentConditionQuestionnaire(out var error), Is.True, error);
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.status, Is.EqualTo(ConditionRunStatus.QuestionnaireInProgress));
            FillRequired(fixture.Controller.Service); Assert.That(fixture.Controller.Submit(out error), Is.True, error);
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.status, Is.EqualTo(ConditionRunStatus.Completed));
        }

        [Test] public void Lifecycle_SkipWithoutAnswers_CompletesConditionWithSkippedOutcome()
        {
            using var fixture = new LifecycleFixture("stage5-skip", protocol, catalog);
            fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done");
            Assert.That(fixture.Controller.StartCurrentConditionQuestionnaire(out var error), Is.True, error);
            Assert.That(fixture.Controller.Skip(out error), Is.True, error);
            Assert.That(fixture.Controller.ActiveSession.completionStatus, Is.EqualTo(QuestionnaireCompletionStatus.Skipped));
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.status, Is.EqualTo(ConditionRunStatus.Completed));
            Assert.That(fixture.Coordinator.TechnicalValidity, Is.EqualTo(ExperimentTechnicalValidity.Valid));
        }

        [Test] public void Lifecycle_WrongRunOrLinkageCannotComplete()
        {
            using var fixture = new LifecycleFixture("stage5-link", protocol, catalog); fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done");
            fixture.Controller.StartCurrentConditionQuestionnaire(out _);
            Assert.That(fixture.Coordinator.CompleteQuestionnaireSubmission("wrong", fixture.Coordinator.QuestionnaireLinkageKey, out var error), Is.False);
            Assert.That(error, Is.EqualTo("questionnaire_linkage_mismatch"));
        }

        [Test] public void UnsubmittedCondition_RemainsInQuestionnaireState()
        {
            using var fixture = new LifecycleFixture("stage5-pending", protocol, catalog); fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done"); fixture.Controller.StartCurrentConditionQuestionnaire(out _);
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.status, Is.EqualTo(ConditionRunStatus.QuestionnaireInProgress));
        }

        [Test] public void TechnicalInvalidCondition_CannotStartQuestionnaire()
        {
            using var fixture = new LifecycleFixture("stage5-invalid", protocol, catalog); fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done"); fixture.Coordinator.MarkTechnicalInvalid("failure");
            Assert.That(fixture.Controller.StartCurrentConditionQuestionnaire(out _), Is.False);
        }

        [Test] public void QuestionnaireStudyEvents_AreAppendedWithoutTimingSchemaChanges()
        {
            var participant = "stage5-events-" + Guid.NewGuid().ToString("N"); using var fixture = new LifecycleFixture(participant, protocol, catalog);
            fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done"); fixture.Controller.StartCurrentConditionQuestionnaire(out _); fixture.Controller.CompletePage(0, out _); FillRequired(fixture.Controller.Service); fixture.Controller.Submit(out _);
            var path = Path.Combine(Application.persistentDataPath, "SceneTalkVR", "ExperimentLogs", participant + "_session_study_events_v1.jsonl");
            var text = File.ReadAllText(path); StringAssert.Contains("QuestionnaireStarted", text); StringAssert.Contains("QuestionnairePageCompleted", text); StringAssert.Contains("QuestionnaireSubmitted", text);
        }

        [Test] public void CatalogContainsFormalPilotFinalAndInterviewDefinitions()
        {
            CollectionAssert.IsSubsetOf(new[] { "formal_condition_v1", "pilot_condition_v1", "formal_final_v1", "pilot_final_v1", "formal_interview_v1" }, catalog.Questionnaires.Select(x => x.questionnaireId).ToArray());
        }

        private QuestionnaireSessionService BeginService()
        {
            var service = NewService(); var context = Context(); Assert.That(service.Begin(catalog.Find("formal_condition_v1"), context, out var error), Is.True, error); return service;
        }
        private QuestionnaireSessionService NewService() { var value = new QuestionnaireSessionService(); value.Configure(catalog, protocol); return value; }
        private QuestionnaireSession Context() => new QuestionnaireSession { participantId = "p", sessionId = "s", sequenceId = "seq", conditionRunId = "cr-test", questionnaireLinkageKey = "ql-test", conditionPosition = 0, formalCondition = FormalConditionCode.NE, taskId = "hotel_check_in", taskAssignmentId = "ta", technicalValidity = ExperimentTechnicalValidity.Valid, protocolVersion = protocol.ProtocolVersion };
        private void FillRequired(QuestionnaireSessionService service) { foreach (var item in catalog.GetEnabledItems("formal_condition_v1", protocol).Where(x => x.required)) service.SetResponse(item.itemId, item.itemType == QuestionnaireItemType.Likert ? "4" : "answer", out _); }
        private static PreferenceRankingResponse FormalRanking() => new PreferenceRankingResponse { rankings = new[] { "NE", "NR", "SE", "SR" }.Select((x, i) => new PreferenceRankEntry { rank = i + 1, conditionCode = x }).ToArray() };
        private static string TempFolder() { var path = Path.Combine(Path.GetTempPath(), "SceneTalkVR-stage5-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
        private static void DeleteFolder(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        private static void ConfirmDecision(ExperimentV11ProtocolConfig target, string id, string value)
        {
            var decisions = (ExperimentProtocolDecision[])typeof(ExperimentV11ProtocolConfig).GetField("requiredDecisions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
            var decision = decisions.Single(x => x.decisionId == id); decision.status = ProtocolDecisionStatus.Confirmed; decision.confirmedValue = value;
        }

        private sealed class LifecycleFixture : IDisposable
        {
            private readonly GameObject go = new GameObject("stage5-fixture");
            public ExperimentConditionManager Manager { get; }
            public ExperimentLifecycleCoordinator Coordinator { get; }
            public QuestionnaireRuntimeController Controller { get; }
            public LifecycleFixture(string participant, ExperimentV11ProtocolConfig protocol, QuestionnaireCatalog catalog)
            {
                Manager = go.AddComponent<ExperimentConditionManager>(); Coordinator = Manager.LifecycleCoordinator ?? go.AddComponent<ExperimentLifecycleCoordinator>();
                var taskCatalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
                Set(Manager, "experimentProtocol", protocol); Set(Manager, "taskCatalog", taskCatalog); Set(Manager, "questionnaireCatalog", catalog);
                Controller = go.GetComponent<QuestionnaireRuntimeController>() ?? go.AddComponent<QuestionnaireRuntimeController>(); Controller.Configure(Manager, Coordinator); Coordinator.Configure(Manager);
                var sequences = new[]
                {
                    new AssignmentSequence { sequenceId = "test-1", conditions = new[] { FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR } },
                    new AssignmentSequence { sequenceId = "test-2", conditions = new[] { FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE } },
                    new AssignmentSequence { sequenceId = "test-3", conditions = new[] { FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR } },
                    new AssignmentSequence { sequenceId = "test-4", conditions = new[] { FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE } }
                };
                Assert.That(new ExperimentAssignmentAllocator().TryCreateForTesting(participant, "session", protocol.ProtocolVersion, taskCatalog.CatalogVersion, sequences,
                    new[] { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" }, AssignmentPolicy.StrictWithoutReplacement, out var assignment, out var createError), Is.True, createError);
                Assert.That(Coordinator.LoadAssignment(assignment, out var error), Is.True, error);
            }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
