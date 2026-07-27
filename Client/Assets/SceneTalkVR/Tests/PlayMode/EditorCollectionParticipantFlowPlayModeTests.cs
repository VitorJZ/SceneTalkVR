using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class EditorCollectionParticipantFlowPlayModeTests
    {
        private Component collection;
        private Component lifecycle;
        private Component questionnaire;
        private Component manager;
        private Component rehearsal;
        private string lastEvaluatedTurnId;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ForcePicoDeviceValidation(false);
            ResetUserSettings();
            if (SceneManager.GetActiveScene().name != "SampleScene") { SceneManager.LoadScene("SampleScene"); yield return null; }
            yield return null;
            manager = Find("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            lifecycle = Find("SceneTalkVR.Core.ExperimentLifecycleCoordinator, Assembly-CSharp");
            questionnaire = Find("SceneTalkVR.Runtime.QuestionnaireRuntimeController, Assembly-CSharp");
            rehearsal = Find("SceneTalkVR.Core.RehearsalSessionCoordinator, Assembly-CSharp");
            var goalOrchestrator = Type.GetType("SceneTalkVR.Core.GoalEvaluationOrchestrator, Assembly-CSharp");
            goalOrchestrator?.GetProperty("AsyncStructuredFallback", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, null);
            var type = Type.GetType("SceneTalkVR.Core.EditorCollectionSessionCoordinator, Assembly-CSharp");
            collection = manager.GetComponent(type) ?? (Component)manager.gameObject.AddComponent(type);
            Configure();
        }

        [UnityTearDown] public IEnumerator TearDown() { CallVoid(collection, "EndRuntimeSession"); CallVoid(rehearsal, "ResetSession"); CallVoid(Find("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp"), "ConfirmLeaveExperiment"); ForcePicoDeviceValidation(false); ResetUserSettings(); yield return null; }

        [UnityTest] public IEnumerator T01_HomeShowsIndependentPilotAndFormalRoutesWithoutIntermediateMenu()
        {
            Assert.That(Active("PilotExperimentButton"), Is.True);
            Assert.That(Active("FormalExperimentButton"), Is.True);
            Assert.That(Resources.FindObjectsOfTypeAll<Button>().Any(x => x.name == "NewExperimentButton"), Is.False);
            Assert.That(Active("SessionNotPreparedPanel"), Is.False);
            Assert.That(Active("TaskSelectionPanel"), Is.False);
            Assert.That(Active("ExperimentMenuPanel"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator T02_FormalHomeRouteCreatesIndependentRecordAndLocksAppearanceSnapshot()
        {
            SetAssistantEmbodiment("humanoid");
            Assert.That(Active("InitialPanel"), Is.True); AssertHomeNavigation();
            Click("FormalExperimentButton"); yield return null;
            Assert.That(Active("FormalModeSelectionPanel"), Is.True);
            Assert.That(Active("TaskSelectionPanel"), Is.False);
            Assert.That(Get(Get(collection, "RuntimeContext"), "qualification").ToString(), Is.EqualTo("Collection"));
            var experiment = Find("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var summary = Get(Get(experiment, "CurrentExperiment"), "summary");
            Assert.That(Get(summary, "kind").ToString(), Is.EqualTo("Formal"));
            Assert.That(Get(summary, "assistantEmbodimentSnapshot"), Is.EqualTo("humanoid"));
            Assert.That(Get(summary, "assistantEmbodimentSnapshot"), Is.EqualTo(Get(Get(collection, "Assignment"), "assistantEmbodimentSnapshot")));
            SetAssistantEmbodiment("audio_only");
            Assert.That(Get(summary, "assistantEmbodimentSnapshot"), Is.EqualTo("humanoid"));
            Assert.That(Get(Get(collection, "Assignment"), "assistantEmbodimentSnapshot"), Is.EqualTo("humanoid"));
            Assert.That(Get(manager, "ConfiguredAssistantEmbodiment"), Is.EqualTo("humanoid"));
            AssertExitOverlay();
            Click("ExitButton"); yield return null;
            Assert.That(Active("ExperimentExitConfirmPanel"), Is.True);
            Assert.That(Active("FormalModeSelectionPanel"), Is.False);
            Click("ContinueExperimentButton"); yield return null;
            Assert.That(Active("FormalModeSelectionPanel"), Is.True);
        }

        [UnityTest] public IEnumerator T03_ActualModeButtonLoadsPreassignedTaskAndReadOnlyGoals()
        {
            Arm(out var assignment); StartFormalFlow(); yield return null; var selected = ConditionForTask(assignment, "hotel_check_in");
            Click(Get(selected, "formalConditionCode") + "ModeButton"); yield return null;
            Assert.That(Get(collection, "CurrentTaskId"), Is.EqualTo("hotel_check_in")); Assert.That(Count(Get(Get(lifecycle, "GoalTracker"), "Goals")), Is.EqualTo(4));
            Assert.That(Active("ReadOnlyTaskGoalPanel"), Is.True); Assert.That(ButtonsUnder("ReadOnlyTaskGoalPanel"), Is.Empty);
            AssertTaskAboveFullWidthDialogue();
            SetHideDialogueSubtitles(true); yield return null;
            AssertTaskAboveFullWidthDialogue();
            SetHideDialogueSubtitles(false); yield return null;
        }

        [UnityTest] public IEnumerator T04_HotelSpeechUpdatesGoalPanelAndOpensQuestionnaireExactlyOnce()
        {
            Arm(out var assignment); StartFormalFlow(); yield return null; var selected = ConditionForTask(assignment, "hotel_check_in"); Click(Get(selected, "formalConditionCode") + "ModeButton"); yield return null;
            Assert.That(Evaluate("My name is Harry Potter."), Is.EqualTo(1)); yield return null;
            var reservationTurnId = lastEvaluatedTurnId; var tracker = Get(lifecycle, "GoalTracker"); Assert.That(GoalState(tracker, "reservation_name"), Is.EqualTo("Confirmed")); Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Contain("1 / 4 completed")); Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Not.Contain("breakfast"));
            CompleteEvaluatedTurn(reservationTurnId, false); yield return null;
            Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Contain("reservation name")); Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Not.Contain("breakfast"));
            Assert.That(Evaluate("Is breakfast included?"), Is.Zero, "The first post-goal participant turn is the unlock dialogue, not evidence for the hidden next goal."); CompleteEvaluatedTurn(); yield return null;
            Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Contain("breakfast")); Assert.That(TextOf("ReadOnlyTaskGoalPanel"), Does.Contain("reservation name"));
            Assert.That(Evaluate("Is breakfast included?"), Is.EqualTo(1)); CompleteEvaluatedTurn(expectedAdvance: false);
            Assert.That(Evaluate("Could I have a room on a higher floor?"), Is.Zero); CompleteEvaluatedTurn();
            Assert.That(Evaluate("Could I have a room on a higher floor?"), Is.EqualTo(1)); CompleteEvaluatedTurn(expectedAdvance: false);
            Assert.That(Evaluate("What time is checkout?"), Is.Zero); CompleteEvaluatedTurn();
            Assert.That(Evaluate("What time is checkout?"), Is.EqualTo(1));
            Assert.That(Get(questionnaire, "ActiveSession"), Is.Null, "The questionnaire must wait for the final Avatar reply."); CompleteEvaluatedTurn(); yield return null;
            Assert.That((int)Get(tracker, "ConfirmedCount"), Is.EqualTo(4)); Assert.That(Get(questionnaire, "ActiveSession"), Is.Not.Null); Assert.That(Get(Get(questionnaire, "ActiveSession"), "completionStatus").ToString(), Is.EqualTo("InProgress")); Assert.That(Active("QuestionnairePanel"), Is.True);
            AssertExitOverlay();
        }

        [UnityTest] public IEnumerator T05_AvatarAndUnrelatedSpeechCannotAdvanceGoals()
        {
            Arm(out _); StartFormalFlow(); yield return null; Select("NE", true); var tracker = Get(lifecycle, "GoalTracker"); var before = (int)Get(tracker, "ConfirmedCount");
            Assert.That(Evaluate("My name is Harry Potter.", "avatar"), Is.Zero); Assert.That(Evaluate("The weather is pleasant."), Is.Zero); Assert.That((int)Get(tracker, "ConfirmedCount"), Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator T06_QuestionnaireLikertSelectionPersistsAndSubmitReturnsToModes()
        {
            Arm(out var assignment); StartFormalFlow(); yield return null; var selected = ConditionForTask(assignment, "hotel_check_in"); Click(Get(selected, "formalConditionCode") + "ModeButton"); yield return null; CompleteTask("hotel_check_in"); yield return null;
            var visibleLikert = Resources.FindObjectsOfTypeAll<Button>().Where(x => x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy && x.name.StartsWith("formal_", StringComparison.Ordinal)).ToArray();
            Assert.That(visibleLikert, Is.Not.Empty);
            Assert.That(visibleLikert.All(x => x.GetComponent<RectTransform>().sizeDelta.x <= 40.1f), Is.True, "Likert hit targets must not overlap.");
            foreach (var row in visibleLikert.GroupBy(x => Mathf.RoundToInt(x.GetComponent<RectTransform>().anchoredPosition.y)))
            {
                var positions = row.Select(x => x.GetComponent<RectTransform>().anchoredPosition.x).OrderBy(x => x).ToArray();
                for (var i = 1; i < positions.Length; i++) Assert.That(positions[i] - positions[i - 1], Is.GreaterThanOrEqualTo(40f));
            }
            var service = Get(questionnaire, "Service"); var definition = Get(service, "Definition"); var catalog = Get(manager, "QuestionnaireCatalog"); var protocol = Get(manager, "ExperimentProtocol");
            var enabled = ((IEnumerable)catalog.GetType().GetMethod("GetEnabledItems").Invoke(catalog, new[] { Get(definition, "questionnaireId"), protocol })).Cast<object>().ToArray();
            foreach (var item in enabled.Where(x => (bool)Get(x, "required"))) { Click(Get(item, "itemId") + "_7"); yield return null; }
            var first = enabled.First(x => (bool)Get(x, "required")); Assert.That(Button(Get(first, "itemId") + "_7").GetComponent<Image>().color.g, Is.GreaterThan(.6f));
            Click("SubmitButton"); Click("SubmitButton"); yield return null;
            Assert.That(Get(Get(questionnaire, "ActiveSession"), "completionStatus").ToString(), Is.EqualTo("Submitted")); Assert.That(Active("QuestionnairePanel"), Is.False); Assert.That(Active("FormalModeSelectionPanel"), Is.True); Assert.That(Get(selected, "status").ToString(), Is.EqualTo("Completed"));
        }

        [UnityTest] public IEnumerator T07_ResumeKeepsMappingAndGoalSnapshot()
        {
            Arm(out var assignment); StartFormalFlow(); yield return null; var selected = ConditionForTask(assignment, "hotel_check_in"); Click(Get(selected, "formalConditionCode") + "ModeButton"); yield return null; Evaluate("My name is Harry Potter."); CompleteEvaluatedTurn(expectedAdvance: false); yield return null;
            var mapping = Conditions(assignment).Select(x => Get(x, "formalConditionCode") + "=" + Get(Get(x, "task"), "taskId")).ToArray(); var participant = (string)Get(assignment, "participantId"); var session = (string)Get(assignment, "experimentSessionId");
            CallVoid(collection, "EndRuntimeSession"); Configure(); var args = new object[] { participant, session, true, null }; Assert.That((bool)collection.GetType().GetMethod("ArmParticipantSession").Invoke(collection, args), Is.True, args[3] as string); Assert.That((bool)Get(manager, "IsFormalExperiment"), Is.True); Assert.That((bool)Get(manager, "CanUseManualRuntimeCondition"), Is.False); OutCall(collection, "BeginParticipantFlow"); yield return null;
            Assert.That(Conditions(Get(collection, "Assignment")).Select(x => Get(x, "formalConditionCode") + "=" + Get(Get(x, "task"), "taskId")), Is.EqualTo(mapping)); Assert.That(GoalState(Get(lifecycle, "GoalTracker"), "reservation_name"), Is.EqualTo("Confirmed"));
        }

        [UnityTest] public IEnumerator T08_DoubleConditionSelectionCreatesOnlyOneRun()
        { Arm(out _); StartFormalFlow(); yield return null; Select("NE", true); var run = Get(collection, "CurrentRunId"); Select("NR", false); Assert.That(Get(collection, "CurrentRunId"), Is.EqualTo(run)); }

        [UnityTest] public IEnumerator T09_PicoDeviceValidation_FormalReachesInteractiveRankingAndCompletion()
        {
            ForcePicoDeviceValidation(true);
            var flowUiType = Type.GetType("SceneTalkVR.Runtime.SceneTalkFlowUiController, Assembly-CSharp");
            rehearsal = (Component)flowUiType.GetMethod("EnsureRuntimeRehearsalCoordinator", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Click("FormalExperimentButton");
            yield return null;
            var experiment = Find("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var orchestrator = Find("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            Assert.That(Get(Get(Get(experiment, "CurrentExperiment"), "summary"), "kind").ToString(), Is.EqualTo("Formal"));
            var context = Get(rehearsal, "RuntimeContext"); Assert.That(Get(context, "deploymentProfile"), Is.EqualTo("pico_device_validation")); Assert.That((bool)Get(context, "collectionEligible"), Is.False);
            var assignment = Get(rehearsal, "FormalAssignment");
            foreach (var item in Conditions(assignment))
            {
                var select = rehearsal.GetType().GetMethod("SelectFormalCondition"); var selectArgs = new object[] { Get(item, "formalConditionCode"), null };
                Assert.That((bool)select.Invoke(rehearsal, selectArgs), Is.True, selectArgs[1] as string);
                Set(orchestrator, "LastTranscript", "Previous scene user utterance.");
                OutCall(rehearsal, "CompleteCurrentGoalsForQa"); yield return null;
                var service = Get(questionnaire, "Service"); var definition = Get(service, "Definition"); var catalog = Get(manager, "QuestionnaireCatalog"); var protocol = Get(manager, "ExperimentProtocol");
                var enabled = ((IEnumerable)catalog.GetType().GetMethod("GetEnabledItems").Invoke(catalog, new[] { Get(definition, "questionnaireId"), protocol })).Cast<object>().ToArray();
                foreach (var question in enabled.Where(x => (bool)Get(x, "required")))
                { var setArgs = new object[] { Get(question, "itemId"), "4", null }; Assert.That((bool)service.GetType().GetMethod("SetResponse").Invoke(service, setArgs), Is.True, setArgs[2] as string); }
                OutCall(questionnaire, "Submit"); yield return null;
                Assert.That(Get(orchestrator, "LastTranscript"), Is.EqualTo(string.Empty));
            }
            yield return null; Assert.That(Get(orchestrator, "CurrentState").ToString(), Is.EqualTo("ExperimentRanking")); Assert.That(Active("FormalFinalRankingPanel"), Is.True);
            AssertExitOverlay();
            AssertSelectedRanks();
            Click("NERank1"); AssertSelectedRanks("NERank1");
            Click("NRRank2"); AssertSelectedRanks("NERank1", "NRRank2");
            Click("NRRank1"); AssertSelectedRanks("NERank2", "NRRank1");
            Click("NERank1"); Click("SERank3"); Click("SRRank4");
            AssertSelectedRanks("NERank1", "NRRank2", "SERank3", "SRRank4");
            Click("NEPreferred"); Input("RankingReason").text = "Device validation ranking."; Click("RankingSubmitButton"); yield return null;
            Assert.That(Get(orchestrator, "CurrentState").ToString(), Is.EqualTo("ExperimentCompleted"));
            Assert.That(Get(Get(Get(experiment, "CurrentExperiment"), "summary"), "status").ToString(), Is.EqualTo("Completed"));
            Assert.That(Active("FormalExperimentCompletionPanel"), Is.True); AssertExitOverlay(); Assert.That((bool)Get(rehearsal, "ExperimentCompleted"), Is.True); Assert.That((bool)Get(Get(rehearsal, "FormalAssignment"), "collectionEligible"), Is.False);
            Click("FormalCompletionContinueButton"); yield return null;
            Assert.That(Get(orchestrator, "CurrentState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Get(experiment, "CurrentExperiment"), Is.Null);
            Assert.That(Conditions(assignment).All(item => Get(item, "status").ToString() == "Completed"), Is.True);
            Assert.That((bool)Get(rehearsal, "IsActive"), Is.False);
            AssertHomeNavigation();
        }

        [UnityTest] public IEnumerator T10_DialogueExitReturnsToModes_AndSelectionExitSuspendsFormalExperiment()
        {
            Click("FormalExperimentButton"); yield return null;
            var assignment = Get(collection, "Assignment");
            var selected = ConditionForTask(assignment, "hotel_check_in");
            Click(Get(selected, "formalConditionCode") + "ModeButton"); yield return null;
            var activeCondition = Get(lifecycle, "CurrentConditionAssignment");
            var dataFolder = (string)Get(collection, "CurrentDataFolder");
            var experiment = Find("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            AssertExitOverlay();

            Click("ExitButton"); yield return null;

            Assert.That(Active("FormalModeSelectionPanel"), Is.True);
            Assert.That(Active("ExperimentExitConfirmPanel"), Is.False);
            Assert.That((bool)Get(collection, "IsArmed"), Is.True);
            Assert.That(Get(assignment, "status").ToString(), Is.EqualTo("Active"));
            Assert.That(Get(activeCondition, "status").ToString(), Is.EqualTo("TechnicalInvalid"));
            var attempts = ((IEnumerable)Get(Get(experiment, "CurrentExperiment"), "attempts")).Cast<object>().ToArray();
            Assert.That(attempts, Has.Length.EqualTo(1));
            Assert.That(Get(attempts[0], "status").ToString(), Is.EqualTo("Suspended"));
            Click("ExitButton"); yield return null;
            Assert.That(Active("ExperimentExitConfirmPanel"), Is.True);
            Click("ConfirmExitExperimentButton"); yield return null;
            Assert.That(Active("InitialPanel"), Is.True); AssertHomeNavigation();
            Assert.That((bool)Get(collection, "IsArmed"), Is.False);
            Assert.That((bool)Get(manager, "IsFormalExperiment"), Is.False);
            Assert.That((bool)Get(manager, "CanUseManualRuntimeCondition"), Is.True);
            var persisted = JsonUtility.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(dataFolder, "formal_assignment.json")), assignment.GetType());
            Assert.That(Get(persisted, "status").ToString(), Is.EqualTo("Active"));
            Assert.That(Get(Conditions(persisted).Single(x => (string)Get(Get(x, "task"), "taskId") == "hotel_check_in"), "status").ToString(), Is.EqualTo("TechnicalInvalid"));
            var events = System.IO.File.ReadAllText(System.IO.Path.Combine(dataFolder, "editor_collection_operator_events.jsonl"));
            Assert.That(events, Does.Contain("ParticipantReturnedToModeSelection"));
            Assert.That(events, Does.Contain("ParticipantSessionSuspended"));
            Assert.That(events, Does.Contain("\"actor\":\"participant\""));
        }

        private void Arm(out object assignment)
        { var token = Guid.NewGuid().ToString("N"); var args = new object[] { "PLAY-" + token, "SESSION-" + token, false, null }; Assert.That((bool)collection.GetType().GetMethod("ArmParticipantSession").Invoke(collection, args), Is.True, args[3] as string); assignment = Get(collection, "Assignment"); Assert.That((bool)Get(assignment, "collectionEligible"), Is.True); Assert.That((bool)Get(assignment, "developerTestAssignment"), Is.False); }
        private int Evaluate(string transcript, string speaker = "participant") { lastEvaluatedTurnId = Guid.NewGuid().ToString("N"); var type = Type.GetType("SceneTalkVR.Core.GoalEvaluationOrchestrator, Assembly-CSharp"); type.GetMethod("NotifyParticipantTurnSubmitted").Invoke(null, new object[] { lifecycle, null, lastEvaluatedTurnId, transcript, speaker }); return (int)type.GetMethod("EvaluateUserTranscript").Invoke(null, new object[] { lifecycle, lastEvaluatedTurnId, transcript, speaker }); }
        private void CompleteEvaluatedTurn(string turnId = null, bool expectedAdvance = true) { var tracker = Get(lifecycle, "GoalTracker"); Assert.That((bool)tracker.GetType().GetMethod("NotifyDialogueTurnCompleted").Invoke(tracker, new object[] { turnId ?? lastEvaluatedTurnId }), Is.EqualTo(expectedAdvance)); }
        private void StartFormalFlow() { OutCall(collection, "BeginParticipantFlow"); }
        private void CompleteTask(string task) { var phrases = new[] { "My name is Harry Potter.", "Is breakfast included?", "Could I have a room on a higher floor?", "What time is checkout?" }; for (var i = 0; i < phrases.Length; i++) { Assert.That(Evaluate(phrases[i]), Is.EqualTo(1)); if (i == phrases.Length - 1) { CompleteEvaluatedTurn(); continue; } CompleteEvaluatedTurn(expectedAdvance: false); Assert.That(Evaluate(phrases[i + 1]), Is.Zero); CompleteEvaluatedTurn(); } }
        private void Select(string code, bool expected) { var method = collection.GetType().GetMethod("SelectFormalCondition"); var args = new object[] { Enum.Parse(method.GetParameters()[0].ParameterType, code), null }; Assert.That((bool)method.Invoke(collection, args), Is.EqualTo(expected), args[1] as string); }
        private void Configure() { var m = collection.GetType().GetMethod("Configure"); var p = m.GetParameters(); m.Invoke(collection, new[] { Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset", p[0].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentEditorCollectionResources.asset", p[1].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentVoiceProfileCatalog.asset", p[2].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentDeploymentCatalog.asset", p[3].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset", p[4].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset", p[5].ParameterType) }); }
        private static object ConditionForTask(object assignment, string task) => Conditions(assignment).Single(x => (string)Get(Get(x, "task"), "taskId") == task);
        private static object[] Conditions(object assignment) => ((IEnumerable)Get(assignment, "conditions")).Cast<object>().ToArray();
        private static string GoalState(object tracker, string goal) => Get(((IEnumerable)Get(tracker, "Goals")).Cast<object>().Single(x => (string)Get(x, "goalId") == goal), "state").ToString();
        private static void OutCall(Component value, string method) { var args = new object[] { null }; Assert.That((bool)value.GetType().GetMethod(method).Invoke(value, args), Is.True, args[0] as string); }
        private static void CallVoid(Component value, string method) { value?.GetType().GetMethod(method)?.Invoke(value, null); }
        private static object Get(object value, string name) { if (value == null) return null; var t = value.GetType(); return t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) ?? t.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value); }
        private static void Set(object value, string name, object propertyValue) { value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(value, propertyValue); }
        private static int Count(object value) { if (value is ICollection c) return c.Count; return (int)(value?.GetType().GetProperty("Count")?.GetValue(value) ?? 0); }
        private static Component Find(string type) => (Component)Resources.FindObjectsOfTypeAll(Type.GetType(type)).FirstOrDefault();
        private static object Asset(string path, Type type) { var adb = Type.GetType("UnityEditor.AssetDatabase, UnityEditor"); return adb.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, type }); }
        private static Button Button(string name) => Resources.FindObjectsOfTypeAll<Button>().First(x => x.gameObject.name == name);
        private static TMP_InputField Input(string name) => Resources.FindObjectsOfTypeAll<TMP_InputField>().First(x => x.gameObject.name == name);
        private static void ForcePicoDeviceValidation(bool value) { var type = Type.GetType("SceneTalkVR.Core.ExperimentRuntimePlatform, Assembly-CSharp"); type.GetProperty("ForcePicoDeviceValidationForTests").SetValue(null, value); }
        private static void Click(string name) => Button(name).onClick.Invoke();
        private static void AssertSelectedRanks(params string[] selectedNames)
        {
            var selected = new HashSet<string>(selectedNames, StringComparer.Ordinal);
            var buttons = Resources.FindObjectsOfTypeAll<Button>()
                .Where(x => x.gameObject.scene.IsValid() && x.transform.parent != null && x.transform.parent.name == "FormalFinalRankingPanel")
                .Where(x => x.name.Contains("Rank", StringComparison.Ordinal) && x.name != "RankingSubmitButton")
                .ToArray();
            Assert.That(buttons, Has.Length.EqualTo(16));
            foreach (var button in buttons)
            {
                var color = button.GetComponent<Image>().color;
                Assert.That(color.g > color.b, Is.EqualTo(selected.Contains(button.name)), button.name);
            }
        }
        private static bool Active(string name) { var go = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == name && x.scene.IsValid()); return go != null && go.activeInHierarchy; }
        private static string TextOf(string name) { var go = Resources.FindObjectsOfTypeAll<GameObject>().First(x => x.name == name && x.scene.IsValid()); return string.Join("\n", go.GetComponentsInChildren<TMP_Text>(true).Select(x => x.text)); }
        private static Button[] ButtonsUnder(string name) { var go = Resources.FindObjectsOfTypeAll<GameObject>().First(x => x.name == name && x.scene.IsValid()); return go.GetComponentsInChildren<Button>(true); }
        private static Type UserSettingsStoreType => Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");
        private static void ResetUserSettings() => UserSettingsStoreType.GetMethod("ResetAll").Invoke(null, null);
        private static void SetAssistantEmbodiment(string value) => UserSettingsStoreType.GetMethod("SetAssistantEmbodiment").Invoke(null, new object[] { value });
        private static void SetHideDialogueSubtitles(bool hidden) => UserSettingsStoreType.GetMethod("SetHideDialogueSubtitles").Invoke(null, new object[] { hidden });
        private static void AssertTaskAboveFullWidthDialogue()
        {
            var task = Rect("ReadOnlyTaskGoalPanel");
            var dialogue = Rect("SubtitlePanel");
            var canvas = Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.gameObject.scene.IsValid() && x.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal));
            var canvasRect = (RectTransform)canvas.transform;
            var taskLeft = task.anchoredPosition.x + task.rect.xMin;
            var taskTop = task.anchoredPosition.y + task.rect.yMax;
            var taskBottom = task.anchoredPosition.y + task.rect.yMin;
            var dialogueLeft = dialogue.anchoredPosition.x + dialogue.rect.xMin;
            var dialogueRight = dialogue.anchoredPosition.x + dialogue.rect.xMax;
            var dialogueTop = dialogue.anchoredPosition.y + dialogue.rect.yMax;
            var dialogueBottom = dialogue.anchoredPosition.y + dialogue.rect.yMin;
            Assert.That(taskBottom - dialogueTop, Is.GreaterThanOrEqualTo(20f), "Task goals must remain above the dialogue panel.");
            Assert.That(taskLeft, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin), "Task goals must remain inside the canvas.");
            Assert.That(taskTop, Is.LessThanOrEqualTo(canvasRect.rect.yMax), "Task goals must remain inside the canvas.");
            Assert.That(task.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(task.anchoredPosition.y, Is.GreaterThan(0f));
            Assert.That(dialogueLeft, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin), "Dialogue must remain inside the canvas.");
            Assert.That(dialogueRight, Is.LessThanOrEqualTo(canvasRect.rect.xMax), "Dialogue must remain inside the canvas.");
            Assert.That(dialogueBottom, Is.GreaterThanOrEqualTo(canvasRect.rect.yMin), "Dialogue must remain inside the canvas.");
            Assert.That(dialogue.rect.width, Is.GreaterThanOrEqualTo(800f), "Dialogue must use the full lower width.");
            Assert.That(dialogue.anchoredPosition.y, Is.LessThan(0f));
        }
        private static RectTransform Rect(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(x => x.name == name && x.scene.IsValid()).GetComponent<RectTransform>();
        private static void AssertHomeNavigation()
        {
            Assert.That(Active("QuitButton"), Is.True);
            Assert.That(Button("QuitButton").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Quit"));
            Assert.That(Button("QuitButton").transform.parent.gameObject.name, Is.EqualTo("InitialPanel"));
            Assert.That(Active("ExitButton"), Is.False, "The home page must use Quit instead of the global Exit button.");
        }
        private static void AssertExitOverlay()
        {
            var button = Button("ExitButton");
            var rect = button.GetComponent<RectTransform>();
            var canvas = Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.gameObject.scene.IsValid() && x.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal));
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.transform.parent, Is.EqualTo(canvas.transform));
            Assert.That(button.transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(rect.anchoredPosition.y, Is.LessThan(0f));
            Assert.That(Active("QuitButton"), Is.False);
        }
    }
}
