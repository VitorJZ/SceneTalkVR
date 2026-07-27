using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class PilotCollectionParticipantFlowPlayModeTests
    {
        [UnitySetUp]public IEnumerator SetUp(){ForcePicoDeviceValidation(false);ResetUserSettings();if(SceneManager.GetActiveScene().name!="SampleScene"){SceneManager.LoadScene("SampleScene");yield return null;}yield return null;CallActive("SceneTalkVR.Core.EditorCollectionSessionCoordinator, Assembly-CSharp","EndRuntimeSession");CallActive("SceneTalkVR.Core.RehearsalSessionCoordinator, Assembly-CSharp","ResetSession");}
        [UnityTearDown]public IEnumerator TearDown(){CallActive("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp","ResetPilotSessionForQa");CallActive("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp","ConfirmLeaveExperiment");ForcePicoDeviceValidation(false);ResetUserSettings();yield return null;}
        [UnityTest]public IEnumerator MainMenu_HasIndependentFormalAndPilotRoutes()
        {Assert.That(Label("PilotExperimentButton"),Is.EqualTo("Pilot Experiment"));Assert.That(Label("FormalExperimentButton"),Is.EqualTo("Formal Experiment"));Assert.That(Label("ExperimentHistoryButton"),Is.EqualTo("Experiment History"));Assert.That(Resources.FindObjectsOfTypeAll<Button>().Any(x=>x.name=="NewExperimentButton"),Is.False);AssertHomeNavigation();Click("PilotExperimentButton");yield return null;Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Assert.That(Active("PilotSessionSetupPanel"),Is.False);Assert.That(Active("ExperimentMenuPanel"),Is.False);Assert.That(Active("FormalModeSelectionPanel"),Is.False);Assert.That(Active("TaskSelectionPanel"),Is.False);AssertOverlayText("PilotAppearanceSelectionPanel");AssertExitOverlay();}
        [UnityTest]public IEnumerator PilotCreateSession_PersistsLockedMappingAndShowsAppearanceSelection()
        {Create();yield return null;var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");Assert.That((bool)Get(coordinator,"IsArmed"),Is.True);Assert.That(Get(coordinator,"Stage").ToString(),Is.EqualTo("AppearanceSelection"));Assert.That((string)Get(coordinator,"ParticipantId"),Does.StartWith("PILOT-P-"));Assert.That((string)Get(coordinator,"SessionId"),Does.StartWith("PILOT-S-"));var assignment=Get(coordinator,"Assignment");Assert.That((bool)Get(assignment,"collectionEligible"),Is.True);Assert.That((bool)Get(assignment,"developerTestAssignment"),Is.False);var conditions=((IEnumerable)Get(assignment,"conditions")).Cast<object>().ToArray();Assert.That(conditions.Select(x=>Get(Get(x,"task"),"taskId")).Distinct().Count(),Is.EqualTo(3));Assert.That(conditions.Select(x=>Get(x,"embodimentCondition")).Distinct().Count(),Is.EqualTo(3));Assert.That(System.IO.File.Exists(System.IO.Path.Combine((string)Get(coordinator,"CurrentDataFolder"),"pilot_assignment.json")),Is.True);Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);}
        [UnityTest]public IEnumerator NewPilotExperiment_ClearsFinalRankingDraft()
        {
            Create();yield return null;
            Input("PilotRankingReason").text="Previous participant feedback.";
            Click("VoiceOnlyRank1");Click("FloatingOrbRank2");Click("HumanoidAgentRank3");Click("VoiceOnlyPreferred");
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            experiment.GetType().GetMethod("ConfirmLeaveExperiment").Invoke(experiment,null);yield return null;
            Click("PilotExperimentButton");yield return null;
            Assert.That(Input("PilotRankingReason").text,Is.Empty);
            foreach(var button in new[]{"VoiceOnlyRank1","FloatingOrbRank2","HumanoidAgentRank3","VoiceOnlyPreferred","FloatingOrbPreferred","HumanoidAgentPreferred"})AssertRankNotSelected(button);
        }
        [UnityTest]public IEnumerator ExperimentHistoryResume_PreservesPilotRankingDraft()
        {
            Create();yield return null;
            Input("PilotRankingReason").text="Keep this draft when resuming.";
            Click("VoiceOnlyRank1");Click("FloatingOrbRank2");Click("HumanoidAgentRank3");Click("VoiceOnlyPreferred");
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var experimentId=(string)Get(Get(Get(experiment,"CurrentExperiment"),"summary"),"experimentId");
            experiment.GetType().GetMethod("ConfirmLeaveExperiment").Invoke(experiment,null);yield return null;
            var continueArgs=new object[]{experimentId,null};
            Assert.That((bool)experiment.GetType().GetMethod("ContinueExperiment").Invoke(experiment,continueArgs),Is.True,continueArgs[1] as string);
            yield return null;
            Assert.That(Input("PilotRankingReason").text,Is.EqualTo("Keep this draft when resuming."));
            foreach(var button in new[]{"VoiceOnlyRank1","FloatingOrbRank2","HumanoidAgentRank3","VoiceOnlyPreferred"})AssertRankSelected(button);
        }
        [UnityTest]public IEnumerator IncompleteExperimentExitRequiresConfirmationAndKeepsRecord()
        {Create();yield return null;var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");var id=(string)Get(Get(Get(experiment,"CurrentExperiment"),"summary"),"experimentId");Click("ExitButton");yield return null;Assert.That(Active("ExperimentExitConfirmPanel"),Is.True);Assert.That(Active("PilotAppearanceSelectionPanel"),Is.False);Click("ContinueExperimentButton");yield return null;Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Click("ExitButton");yield return null;Click("ConfirmExitExperimentButton");yield return null;AssertHomeNavigation();Click("ExperimentHistoryButton");yield return null;Assert.That(Active("ExperimentHistoryListPanel"),Is.True);var items=((IEnumerable)Get(Get(experiment,"CurrentHistoryPage"),"items")).Cast<object>();Assert.That(items.Any(x=>(string)Get(x,"experimentId")==id),Is.True);}
        [UnityTest]public IEnumerator BeginPilot_ShowsAssignedTaskWithoutEmbodimentMetadata()
        {Create();var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");var selected=Conditions(coordinator).First();var expectedTask=(string)Get(Get(selected,"task"),"taskId");Click(Get(selected,"embodimentCondition")+"AppearanceButton");yield return null;Assert.That(Active("PilotConditionTaskIntroductionPanel"),Is.True);var text=Text("PilotConditionTaskIntroductionPanel");Assert.That(text,Does.Contain("Communication goals:"));Assert.That(text,Does.Not.Contain("voice_only"));Assert.That(text,Does.Not.Contain("floating_orb"));Assert.That(text,Does.Not.Contain("humanoid_agent"));Click("PilotTaskContinueButton");yield return new WaitForSecondsRealtime(1f);Assert.That(Active("ReadOnlyTaskGoalPanel"),Is.True);AssertTaskAboveFullWidthDialogue();SetHideDialogueSubtitles(true);yield return null;AssertTaskAboveFullWidthDialogue();SetHideDialogueSubtitles(false);yield return null;var task=Get(coordinator,"CurrentTask");var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");var payload=Get(orchestrator,"LastScenePayload");Assert.That(Get(task,"taskId"),Is.EqualTo(expectedTask));Assert.That(Get(payload,"taskType"),Is.EqualTo(expectedTask));}
        [UnityTest]public IEnumerator SelectedPilotAppearanceSurvivesConditionBroadcast_AndDialogueExitReturnsToSelection()
        {
            Create();var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            var voiceCondition=Conditions(coordinator).Single(x=>Get(x,"embodimentCondition").ToString()=="VoiceOnly");
            var manager=(Component)ActiveObject("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            var agentType=Type.GetType("SceneTalkVR.AvatarSystem.CorrectionAgentPresenter, Assembly-CSharp");
            var presenterType=Type.GetType("SceneTalkVR.AvatarSystem.PilotEmbodimentPresenter, Assembly-CSharp");
            var agent=manager.GetComponent(agentType);var presenter=manager.GetComponent(presenterType);

            Click("VoiceOnlyAppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            Assert.That(Get(agent,"CurrentVisualMode").ToString(),Is.EqualTo("AudioOnly"));
            Assert.That(Get(manager,"CurrentAssistantEmbodiment"),Is.EqualTo("audio_only"));
            Assert.That(Get(Get(presenter,"Profile"),"embodimentCondition").ToString(),Is.EqualTo("VoiceOnly"));

            Click("ExitButton");yield return null;
            Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);
            Assert.That(Active("ExperimentExitConfirmPanel"),Is.False);
            Assert.That((bool)Get(coordinator,"IsArmed"),Is.True);
            Assert.That(Get(voiceCondition,"status").ToString(),Is.EqualTo("TechnicalInvalid"));
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var attempts=((IEnumerable)Get(Get(experiment,"CurrentExperiment"),"attempts")).Cast<object>().ToArray();
            Assert.That(attempts,Has.Length.EqualTo(1));Assert.That(Get(attempts[0],"status").ToString(),Is.EqualTo("Suspended"));

            Click("FloatingOrbAppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            Assert.That(Get(agent,"CurrentVisualMode").ToString(),Is.EqualTo("GeneratedAgent"));
            Assert.That(Get(manager,"CurrentAssistantEmbodiment"),Is.EqualTo("orb"));
            Assert.That(Get(Get(presenter,"Profile"),"embodimentCondition").ToString(),Is.EqualTo("FloatingOrb"));

            Click("ExitButton");yield return null;
            Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);
            Assert.That(Active("ExperimentExitConfirmPanel"),Is.False);

            Click("HumanoidAgentAppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            Assert.That(Get(agent,"CurrentVisualMode").ToString(),Is.EqualTo("HumanoidAvatar"));
            Assert.That(Get(manager,"CurrentAssistantEmbodiment"),Is.EqualTo("humanoid"));
            Assert.That(Get(Get(presenter,"Profile"),"embodimentCondition").ToString(),Is.EqualTo("HumanoidAgent"));
        }
        [UnityTest]public IEnumerator VoiceOnlyPresenter_HasNoVisualAndResetDoesNotLeak()
        {var manager=(Component)ActiveObject("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");var presenterType=Type.GetType("SceneTalkVR.AvatarSystem.PilotEmbodimentPresenter, Assembly-CSharp");var presenter=manager.GetComponent(presenterType)??manager.gameObject.AddComponent(presenterType);var catalog=Get(manager,"PilotPresentationCatalog");var embodiment=Enum.Parse(Type.GetType("SceneTalkVR.Core.PilotEmbodimentCondition, Assembly-CSharp"),"VoiceOnly");var profile=catalog.GetType().GetMethod("Find").Invoke(catalog,new[]{embodiment});var policy=Enum.Parse(Type.GetType("SceneTalkVR.Core.PilotAudioSourcePolicy, Assembly-CSharp"),"NonSpatialHeadLocked");var args=new[]{profile,policy,(object)true,null};Assert.That((bool)presenterType.GetMethod("Configure").Invoke(presenter,args),Is.True,args[3] as string);presenterType.GetMethod("BeginFeedback").Invoke(presenter,null);Assert.That((bool)Get(presenter,"HasVisualEntity"),Is.False);Assert.That(((AudioSource)Get(presenter,"AudioSource")).spatialBlend,Is.Zero);presenterType.GetMethod("ResetSession").Invoke(presenter,null);Assert.That((bool)Get(presenter,"HasVisualEntity"),Is.False);yield return null;}
        [UnityTest]public IEnumerator CompleteControlledParticipantFlow_ReachesRankingAndCompletion()
        {
            Create();AssertExitOverlay();
            var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            foreach(var condition in Conditions(coordinator).Reverse())
            {
                Click(Get(condition,"embodimentCondition")+"AppearanceButton");yield return null;
                Assert.That(Active("PilotConditionTaskIntroductionPanel"),Is.True);
                Click("PilotTaskContinueButton");yield return null;AssertExitOverlay();
                foreach(var phrase in PhrasesForTask((string)Get(Get(condition,"task"),"taskId")))EvaluatePilot(phrase);
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(Active("PilotQuestionnairePanel"),Is.True);AssertExitOverlay();
                foreach(var item in new[]{"pilot_rc_01","pilot_sc_01","pilot_accept_01"})Click(item+"_4");
                Click("PilotQuestionnaireSubmitButton");yield return null;
            }
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("ExperimentRanking"));
            Assert.That(Active("PilotFinalRankingPanel"),Is.True);AssertExitOverlay();
            Click("VoiceOnlyRank1");Click("FloatingOrbRank2");Click("HumanoidAgentRank3");
            AssertRankSelected("VoiceOnlyRank1");AssertRankSelected("FloatingOrbRank2");AssertRankSelected("HumanoidAgentRank3");
            Click("FloatingOrbRank1");AssertRankSelected("VoiceOnlyRank2");AssertRankSelected("FloatingOrbRank1");AssertRankSelected("HumanoidAgentRank3");AssertRankNotSelected("VoiceOnlyRank1");AssertRankNotSelected("FloatingOrbRank2");
            Click("VoiceOnlyRank1");Click("VoiceOnlyPreferred");Input("PilotRankingReason").text="The feedback was easiest to follow.";Click("PilotRankingSubmitButton");yield return null;
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("ExperimentCompleted"));
            Assert.That(Active("PilotExperimentCompletionPanel"),Is.True);AssertExitOverlay();
            var exportArgs=new object[]{null};Assert.That((bool)coordinator.GetType().GetMethod("ExportBundle").Invoke(coordinator,exportArgs),Is.True,exportArgs[0] as string);
            var auditArgs=new object[]{null};Assert.That((bool)coordinator.GetType().GetMethod("AuditBundle").Invoke(coordinator,auditArgs),Is.True,auditArgs[0] as string);
            Click("PilotCompletionContinueButton");yield return null;Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("Idle"));AssertHomeNavigation();
            Assert.That(Active("PilotExperimentButton"),Is.True);Assert.That(Active("FormalExperimentButton"),Is.True);
            Click("FormalExperimentButton");yield return null;
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var formalSummary=Get(Get(experiment,"CurrentExperiment"),"summary");
            Assert.That(Get(formalSummary,"kind").ToString(),Is.EqualTo("Formal"));
            Assert.That(Get(formalSummary,"assistantEmbodimentSnapshot"),Is.EqualTo("orb"),"Pilot ranking must not change the Formal appearance snapshot.");
        }
        [UnityTest]public IEnumerator GlobalExit_SuspendsAndPersistsPilotExperimentSession()
        {
            Create();Click("VoiceOnlyAppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            var workflow=Get(coordinator,"Workflow");var current=Get(workflow,"Current");var dataFolder=(string)Get(coordinator,"CurrentDataFolder");
            AssertExitOverlay();Click("ExitButton");yield return null;
            Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Assert.That(Active("ExperimentExitConfirmPanel"),Is.False);Assert.That((bool)Get(coordinator,"IsArmed"),Is.True);
            Assert.That(Get(current,"status").ToString(),Is.EqualTo("TechnicalInvalid"));
            var returnEvents=System.IO.File.ReadAllText(System.IO.Path.Combine(dataFolder,"pilot_collection_operator_events.jsonl"));
            Assert.That(returnEvents,Does.Contain("PilotReturnedToAppearanceSelection"));
            Click("ExitButton");yield return null;Assert.That(Active("ExperimentExitConfirmPanel"),Is.True);Click("ConfirmExitExperimentButton");yield return null;
            AssertHomeNavigation();Assert.That((bool)Get(coordinator,"IsArmed"),Is.False);Assert.That(Get(coordinator,"Stage").ToString(),Is.EqualTo("None"));
            var events=System.IO.File.ReadAllText(System.IO.Path.Combine(dataFolder,"pilot_collection_operator_events.jsonl"));
            Assert.That(events,Does.Contain("PilotSessionSuspended"));Assert.That(events,Does.Contain("\"actor\":\"participant\""));
        }
        [UnityTest]public IEnumerator PicoDeviceValidation_CompletesPilotWithoutCollectionEligibility()
        {
            ForcePicoDeviceValidation(true);Create();yield return null;
            var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            Assert.That((bool)Get(coordinator,"IsDeviceValidation"),Is.True);Assert.That((bool)Get(coordinator,"IsArmed"),Is.True);
            var context=Get(coordinator,"RuntimeContext");Assert.That((bool)Get(context,"collectionEligible"),Is.False);Assert.That(Get(context,"dataOrigin"),Is.EqualTo("rehearsal"));Assert.That(Get(context,"deploymentProfile"),Is.EqualTo("pico_device_validation"));
            var chosenOrder=Conditions(coordinator).Reverse().ToArray();
            foreach(var condition in chosenOrder)
            {
                var embodiment=Get(condition,"embodimentCondition");
                var expectedTask=Get(Get(condition,"task"),"taskId");
                Click(embodiment+"AppearanceButton");yield return null;
                Assert.That(Get(Get(coordinator,"CurrentTask"),"taskId"),Is.EqualTo(expectedTask));
                Click("PilotTaskContinueButton");yield return null;CompletePilotGoalsForQa(coordinator);yield return new WaitForSecondsRealtime(1f);
                foreach(var item in new[]{"pilot_rc_01","pilot_sc_01","pilot_accept_01"})Click(item+"_4");
                Click("PilotQuestionnaireSubmitButton");yield return null;
            }
            var recordedOrder=((IEnumerable)Get(Get(coordinator,"Assignment"),"participantSelectionOrder")).Cast<object>().Select(x=>x.ToString()).ToArray();
            Assert.That(recordedOrder,Is.EqualTo(chosenOrder.Select(x=>Get(x,"embodimentCondition").ToString()).ToArray()));
            Click("VoiceOnlyRank1");Click("FloatingOrbRank2");Click("HumanoidAgentRank3");Click("VoiceOnlyPreferred");Input("PilotRankingReason").text="Device validation ranking.";Click("PilotRankingSubmitButton");yield return null;
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("ExperimentCompleted"));
            Assert.That(Active("PilotExperimentCompletionPanel"),Is.True);Assert.That((bool)Get(Get(coordinator,"Assignment"),"collectionEligible"),Is.False);
            var rehearsal=ActiveObject("SceneTalkVR.Core.RehearsalSessionCoordinator, Assembly-CSharp");Assert.That((bool)Get(rehearsal,"ExperimentCompleted"),Is.True);
        }
        [UnityTest]public IEnumerator SuspendedPilotResumesAtIntroductionAndCreatesNewAttempt()
        {
            Create();Click("VoiceOnlyAppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            var pilot=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");var first=(int)Get(Get(Get(pilot,"Workflow"),"Current"),"runAttempt");
            Click("ExitButton");yield return null;Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Click("ExitButton");yield return null;Click("ConfirmExitExperimentButton");yield return null;AssertHomeNavigation();Click("ExperimentHistoryButton");yield return null;Click("ExperimentHistoryRow1");yield return null;Click("ContinueExperimentRecordButton");yield return null;
            Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Click("VoiceOnlyAppearanceButton");yield return null;Assert.That(Active("PilotConditionTaskIntroductionPanel"),Is.True);Click("PilotTaskContinueButton");yield return null;
            var second=(int)Get(Get(Get(pilot,"Workflow"),"Current"),"runAttempt");Assert.That(second,Is.EqualTo(first+1));
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");var attempts=((IEnumerable)Get(Get(experiment,"CurrentExperiment"),"attempts")).Cast<object>().ToArray();
            Assert.That(attempts,Has.Length.EqualTo(2));Assert.That(Get(attempts[0],"status").ToString(),Is.EqualTo("Suspended"));Assert.That(Get(attempts[1],"status").ToString(),Is.EqualTo("Running"));
        }
        private static void Create(){Click("PilotExperimentButton");}
        private static object[] Conditions(object coordinator)=>((IEnumerable)Get(Get(coordinator,"Assignment"),"conditions")).Cast<object>().ToArray();
        private static string[] PhrasesForTask(string taskId)=>taskId switch
        {
            "pilot_restaurant_walk_in"=>new[]{"No, I don't have a reservation.","There are four of us.","Do you have a table available?","How long is the wait?"},
            "pilot_restaurant_ordering"=>new[]{"What do you recommend?","I'd like the grilled chicken.","I don't eat seafood.","I'd like a glass of water."},
            _=>new[]{"This isn't what I ordered.","I ordered the beef pasta.","Could you replace it, please?","How long will the replacement take?"}
        };
        private static TMP_InputField Input(string name)=>Resources.FindObjectsOfTypeAll<TMP_InputField>().First(x=>x.name==name&&x.gameObject.scene.IsValid());
        private static Button Button(string name)=>Resources.FindObjectsOfTypeAll<Button>().First(x=>x.name==name&&x.gameObject.scene.IsValid());
        private static void Click(string name)=>Button(name).onClick.Invoke();
        private static void AssertRankSelected(string name)=>Assert.That(Button(name).GetComponent<Image>().color.g,Is.EqualTo(.68f).Within(.001f),name+" should remain selected.");
        private static void AssertRankNotSelected(string name)=>Assert.That(Button(name).GetComponent<Image>().color.g,Is.EqualTo(.38f).Within(.001f),name+" should not remain selected.");
        private static bool Active(string name)=>Resources.FindObjectsOfTypeAll<GameObject>().Any(x=>x.name==name&&x.gameObject.scene.IsValid()&&x.activeInHierarchy);
        private static string Label(string button)=>Button(button).GetComponentInChildren<TMP_Text>(true).text;
        private static string Text(string panel){var go=Resources.FindObjectsOfTypeAll<GameObject>().First(x=>x.name==panel&&x.scene.IsValid());return string.Join("\n",go.GetComponentsInChildren<TMP_Text>(true).Select(x=>x.text));}
        private static void AssertOverlayText(string panel){var go=Resources.FindObjectsOfTypeAll<GameObject>().First(x=>x.name==panel&&x.scene.IsValid());foreach(var text in go.GetComponentsInChildren<TMP_Text>(true)){Assert.That(text.fontSharedMaterial,Is.Not.Null,text.name+" should have an initialized font material.");Assert.That(text.fontSharedMaterial.shader.name,Is.EqualTo("TextMeshPro/Distance Field Overlay"),text.name+" should render in the same overlay queue as its panel.");}foreach(var subMesh in go.GetComponentsInChildren<TMP_SubMeshUI>(true)){Assert.That(subMesh.sharedMaterial,Is.Not.Null,subMesh.name+" should have an initialized fallback material.");Assert.That(subMesh.sharedMaterial.shader.name,Is.EqualTo("TextMeshPro/Distance Field Overlay"),subMesh.name+" fallback glyphs should render in the overlay queue.");}}
        private static int EvaluatePilot(string transcript){var type=Type.GetType("SceneTalkVR.Core.GoalEvaluationOrchestrator, Assembly-CSharp");var pilot=Get(ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp"),"Workflow");return(int)type.GetMethod("EvaluatePilotUserTranscript").Invoke(null,new[]{pilot,Guid.NewGuid().ToString("N"),transcript,"participant"});}
        private static object Get(object value,string name){var type=value.GetType();return type.GetProperty(name)?.GetValue(value)??type.GetField(name)?.GetValue(value);}
        private static object ActiveObject(string typeName){var type=Type.GetType(typeName);return Resources.FindObjectsOfTypeAll(type).FirstOrDefault();}
        private static void CallActive(string typeName,string method){var value=ActiveObject(typeName);value?.GetType().GetMethod(method)?.Invoke(value,null);}
        private static void CompletePilotGoalsForQa(object coordinator){var args=new object[]{null};Assert.That((bool)coordinator.GetType().GetMethod("CompleteCurrentPilotGoalsForQa").Invoke(coordinator,args),Is.True,args[0] as string);}
        private static void ForcePicoDeviceValidation(bool value){var type=Type.GetType("SceneTalkVR.Core.ExperimentRuntimePlatform, Assembly-CSharp");type.GetProperty("ForcePicoDeviceValidationForTests").SetValue(null,value);}
        private static void ResetUserSettings(){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("ResetAll").Invoke(null,null);}
        private static void SetHideDialogueSubtitles(bool hidden){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("SetHideDialogueSubtitles").Invoke(null,new object[]{hidden});}
        private static void AssertTaskAboveFullWidthDialogue(){var task=Rect("ReadOnlyTaskGoalPanel");var dialogue=Rect("SubtitlePanel");var canvas=Resources.FindObjectsOfTypeAll<Canvas>().First(x=>x.gameObject.scene.IsValid()&&x.gameObject.name.StartsWith("SceneTalkVR World UI",StringComparison.Ordinal));var canvasRect=(RectTransform)canvas.transform;var taskLeft=task.anchoredPosition.x+task.rect.xMin;var taskTop=task.anchoredPosition.y+task.rect.yMax;var taskBottom=task.anchoredPosition.y+task.rect.yMin;var dialogueLeft=dialogue.anchoredPosition.x+dialogue.rect.xMin;var dialogueRight=dialogue.anchoredPosition.x+dialogue.rect.xMax;var dialogueTop=dialogue.anchoredPosition.y+dialogue.rect.yMax;var dialogueBottom=dialogue.anchoredPosition.y+dialogue.rect.yMin;Assert.That(taskBottom-dialogueTop,Is.GreaterThanOrEqualTo(20f),"Task goals must remain above the dialogue panel.");Assert.That(taskLeft,Is.GreaterThanOrEqualTo(canvasRect.rect.xMin));Assert.That(taskTop,Is.LessThanOrEqualTo(canvasRect.rect.yMax));Assert.That(task.anchoredPosition.x,Is.LessThan(0f));Assert.That(task.anchoredPosition.y,Is.GreaterThan(0f));Assert.That(dialogueLeft,Is.GreaterThanOrEqualTo(canvasRect.rect.xMin));Assert.That(dialogueRight,Is.LessThanOrEqualTo(canvasRect.rect.xMax));Assert.That(dialogueBottom,Is.GreaterThanOrEqualTo(canvasRect.rect.yMin));Assert.That(dialogue.rect.width,Is.GreaterThanOrEqualTo(800f),"Dialogue must use the full lower width.");Assert.That(dialogue.anchoredPosition.y,Is.LessThan(0f));}
        private static RectTransform Rect(string name)=>Resources.FindObjectsOfTypeAll<GameObject>().First(x=>x.name==name&&x.scene.IsValid()).GetComponent<RectTransform>();
        private static void AssertHomeNavigation(){Assert.That(Active("QuitButton"),Is.True);Assert.That(Label("QuitButton"),Is.EqualTo("Quit"));Assert.That(Button("QuitButton").transform.parent.gameObject.name,Is.EqualTo("InitialPanel"));Assert.That(Active("ExitButton"),Is.False,"The home page must use Quit instead of the global Exit button.");}
        private static void AssertExitOverlay(){var button=Button("ExitButton");var rect=button.GetComponent<RectTransform>();var canvas=Resources.FindObjectsOfTypeAll<Canvas>().First(x=>x.gameObject.scene.IsValid()&&x.gameObject.name.StartsWith("SceneTalkVR World UI",StringComparison.Ordinal));Assert.That(button.gameObject.activeInHierarchy,Is.True);Assert.That(button.transform.parent,Is.EqualTo(canvas.transform));Assert.That(button.transform.GetSiblingIndex(),Is.EqualTo(canvas.transform.childCount-1));Assert.That(rect.anchorMin,Is.EqualTo(Vector2.one));Assert.That(rect.anchorMax,Is.EqualTo(Vector2.one));Assert.That(rect.anchoredPosition.x,Is.LessThan(0f));Assert.That(rect.anchoredPosition.y,Is.LessThan(0f));Assert.That(Active("QuitButton"),Is.False);if(Active("PilotQuestionnairePanel"))AssertOverlayText("PilotQuestionnairePanel");}
    }
}
