using System;
using System.Collections;
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
    public sealed class PilotCollectionParticipantFlowPlayModeTests
    {
        private GameObject transportHost;
        private object previousTransportRouter;
        [UnitySetUp]public IEnumerator SetUp(){ForcePicoDeviceValidation(false);ForcePicoCollection(false);ResetUserSettings();if(SceneManager.GetActiveScene().name!="SampleScene"){SceneManager.LoadScene("SampleScene");yield return null;}yield return null;CallActive("SceneTalkVR.Core.EditorCollectionSessionCoordinator, Assembly-CSharp","EndRuntimeSession");CallActive("SceneTalkVR.Core.RehearsalSessionCoordinator, Assembly-CSharp","ResetSession");transportHost=InstallReadyUsbTransport(out previousTransportRouter);}
        [UnityTearDown]public IEnumerator TearDown(){CallActive("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp","ResetPilotSessionForQa");CallActive("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp","ConfirmLeaveExperiment");RestoreGatewayTransport(transportHost,previousTransportRouter);transportHost=null;previousTransportRouter=null;ForcePicoDeviceValidation(false);ForcePicoCollection(false);ResetUserSettings();yield return null;}
        [UnityTest]public IEnumerator MainMenu_HasIndependentFormalAndPilotRoutes()
        {Assert.That(Label("PilotExperimentButton"),Is.EqualTo("预实验"));Assert.That(Label("FormalExperimentButton"),Is.EqualTo("正式实验"));Assert.That(Label("ExperimentHistoryButton"),Is.EqualTo("实验历史"));Assert.That(Label("ExportHistoryButton"),Is.EqualTo("导出历史数据"));Assert.That(Resources.FindObjectsOfTypeAll<Button>().Any(x=>x.name=="NewExperimentButton"),Is.False);AssertHomeNavigation();Click("PilotExperimentButton");yield return null;Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);Assert.That(Active("PilotSessionSetupPanel"),Is.False);Assert.That(Active("ExperimentMenuPanel"),Is.False);Assert.That(Active("FormalModeSelectionPanel"),Is.False);Assert.That(Active("TaskSelectionPanel"),Is.False);AssertOverlayText("PilotAppearanceSelectionPanel");AssertExitOverlay();}
        [UnityTest]public IEnumerator SettingsLanguageToggle_RebuildsBilingualUiWithoutLeavingSettings()
        {
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            Click("SettingsButton");yield return null;
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("Settings"));
            Assert.That(Text("SettingsPanel"),Does.Contain("语言"));
            Assert.That(Text("SettingsPanel"),Does.Contain("状态显示"));
            Assert.That(Text("StatusDisplayValue"),Does.Contain("显示"));
            Assert.That(Label("LanguageChangeButton"),Is.EqualTo("English"));

            Click("LanguageChangeButton");yield return null;yield return null;
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("Settings"));
            Assert.That(Text("SettingsPanel"),Does.Contain("Language"));
            Assert.That(Text("SettingsPanel"),Does.Contain("Display, feedback, and connection"));
            Assert.That(Text("SettingsPanel"),Does.Contain("Status display"));
            Assert.That(Text("StatusDisplayValue"),Does.Contain("Shown"));
            Assert.That(Label("StatusDisplayChangeButton"),Is.EqualTo("Switch"));
            Assert.That(Label("LanguageChangeButton"),Is.EqualTo("Chinese"));
            Assert.That(Label("PilotExperimentButton"),Is.EqualTo("Pilot experiment"));
            Assert.That(Label("FormalExperimentButton"),Is.EqualTo("Formal experiment"));
            Assert.That(Resources.FindObjectsOfTypeAll<TMP_Text>().Where(x=>x.gameObject.scene.IsValid()).Select(x=>x.text),Has.None.EqualTo("Translation unavailable"));
            AssertNoChineseSystemText();
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.scene.IsValid()&&x.name=="SceneTalkVR Flow UI"),Is.EqualTo(1));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.scene.IsValid()&&x.name=="SettingsPanel"),Is.EqualTo(1));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.scene.IsValid()&&x.name=="ExitButton"),Is.EqualTo(1));

            Click("StatusDisplayChangeButton");yield return null;
            Assert.That(Text("StatusDisplayValue"),Does.Contain("Hidden"));

            Click("LanguageChangeButton");yield return null;yield return null;
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("Settings"));
            Assert.That(Text("SettingsPanel"),Does.Contain("显示、纠错与连接"));
            Assert.That(Text("StatusDisplayValue"),Does.Contain("隐藏"));
            Assert.That(Label("LanguageChangeButton"),Is.EqualTo("English"));
        }

        [UnityTest]public IEnumerator DialogueStatusVisibilitySetting_HidesOnlyStatusesWithoutChangingDialogueLayout()
        {
            Create();var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");var selected=Conditions(coordinator).First();
            Click(Get(selected,"embodimentCondition")+"AppearanceButton");yield return null;Click("PilotTaskContinueButton");yield return null;
            for(var i=0;i<300&&!Active("SubtitlePanel");i++)yield return null;
            Assert.That(Active("SubtitlePanel"),Is.True,"Dialogue panel did not become visible before the status-display assertions.");
            var panel=Rect("SubtitlePanel");var button=Rect("DialogueListenButton");
            var panelPosition=panel.anchoredPosition;var panelSize=panel.sizeDelta;var buttonPosition=button.anchoredPosition;var buttonSize=button.sizeDelta;
            var feedback=SceneObject("CorrectionFeedback");var feedbackActive=feedback.activeSelf;
            Assert.That(SceneObject("AgentSubtitle"),Is.Not.Null);Assert.That(feedback,Is.Not.Null);

            Assert.That(Active("CorrectionStatus"),Is.True);Assert.That(Active("DialogueStatus"),Is.True);
            Assert.That(Active("TextContainer"),Is.True);Assert.That(Active("DialogueListenButton"),Is.True);
            SetHideDialogueStatuses(true);yield return null;
            Assert.That(Active("CorrectionStatus"),Is.False);Assert.That(Active("DialogueStatus"),Is.False);
            Assert.That(Active("TextContainer"),Is.True);Assert.That(Active("DialogueListenButton"),Is.True);
            Assert.That(feedback.activeSelf,Is.EqualTo(feedbackActive));
            Assert.That(panel.anchoredPosition,Is.EqualTo(panelPosition));Assert.That(panel.sizeDelta,Is.EqualTo(panelSize));
            Assert.That(button.anchoredPosition,Is.EqualTo(buttonPosition));Assert.That(button.sizeDelta,Is.EqualTo(buttonSize));

            SetHideDialogueSubtitles(true);yield return null;
            Assert.That(Active("TextContainer"),Is.False);Assert.That(Active("CorrectionStatus"),Is.False);Assert.That(Active("DialogueStatus"),Is.False);
            SetHideDialogueStatuses(false);yield return null;
            Assert.That(Active("TextContainer"),Is.False);Assert.That(Active("CorrectionStatus"),Is.True);Assert.That(Active("DialogueStatus"),Is.True);
        }
        [UnityTest]public IEnumerator RecoverableDialogueError_KeepsDialogueOpenAndShowsRetryButton()
        {
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            var stateType=Type.GetType("SceneTalkVR.Core.SceneTalkState, Assembly-CSharp",true);
            SetProperty(orchestrator,"IsDialogueActive",true);
            SetProperty(orchestrator,"LastError","Please try again.");
            SetProperty(orchestrator,"CurrentState",Enum.Parse(stateType,"Error"));yield return null;
            Assert.That(Active("SubtitlePanel"),Is.True);
            Assert.That(Label("DialogueListenButton"),Is.EqualTo("重试"));
            Assert.That(Button("DialogueListenButton").interactable,Is.True);
        }
        [UnityTest]public IEnumerator CorrectionSubtitles_CoexistWithIndependentFeedbackAndFollowProvider()
        {
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            var stateType=Type.GetType("SceneTalkVR.Core.SceneTalkState, Assembly-CSharp",true);
            SetProperty(orchestrator,"CurrentState",Enum.Parse(stateType,"TurnReview"));
            SetProperty(orchestrator,"IsDialogueActive",true);
            yield return null;
            Assert.That(Active("SubtitlePanel"),Is.True,"Dialogue panel did not become visible before the subtitle assertions.");

            SetProperty(orchestrator,"CurrentDialogueSubtitleText","Here is the role reply.");
            SetProperty(orchestrator,"LastCorrectionHasFeedback",true);
            SetProperty(orchestrator,"LastCorrectionDisplayText","I would like a table.");
            yield return null;
            Assert.That(Active("AgentSubtitle"),Is.False);
            Assert.That(Active("AvatarSubtitle"),Is.False,"Reply text must wait for its correction subtitle.");
            InvokeCorrectionSubtitleCue(orchestrator,"assistant_agent","Try saying: I would like a table.");yield return null;

            Assert.That(Active("AgentSubtitle"),Is.True);
            Assert.That(SceneObject("AgentSubtitle").GetComponent<TMP_Text>().text,Is.EqualTo("助手：Try saying: I would like a table."));
            Assert.That(SceneObject("AvatarSubtitle").GetComponent<TMP_Text>().text,Is.EqualTo("角色：Here is the role reply."));
            Assert.That(Active("CorrectionFeedback"),Is.True);
            Assert.That(SceneObject("CorrectionFeedback").GetComponent<TMP_Text>().text,Is.EqualTo("纠错：I would like a table."));

            InvokeCorrectionSubtitleCue(orchestrator,"dialogue_avatar","Use 'would like' instead.");yield return null;
            Assert.That(Active("AgentSubtitle"),Is.False);
            Assert.That(SceneObject("AvatarSubtitle").GetComponent<TMP_Text>().text,Is.EqualTo("角色：Use 'would like' instead.\nHere is the role reply."));
            Assert.That(SceneObject("CorrectionFeedback").GetComponent<TMP_Text>().text,Is.EqualTo("纠错：I would like a table."));
        }
        [UnityTest]public IEnumerator LongDialogueSubtitles_WrapExpandAndRemainFullyVisible()
        {
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            var stateType=Type.GetType("SceneTalkVR.Core.SceneTalkState, Assembly-CSharp",true);
            SetProperty(orchestrator,"CurrentState",Enum.Parse(stateType,"TurnReview"));
            SetProperty(orchestrator,"IsDialogueActive",true);
            yield return null;

            var panel=Rect("SubtitlePanel");var button=Rect("DialogueListenButton");
            var originalBottom=panel.anchoredPosition.y+panel.rect.yMin;
            var originalHeight=panel.rect.height;var originalButtonPosition=button.anchoredPosition;
            SetFontScale(1.4f);yield return null;

            var userText=string.Join(" ",Enumerable.Repeat("I would like to explain the reservation details and confirm every requirement before we continue.",8));
            var assistantText=string.Join(" ",Enumerable.Repeat("Use a complete polite request and include the important detail clearly.",7));
            var replyText=string.Join(" ",Enumerable.Repeat("Thank you for explaining that; I will check the available options and confirm the details for you.",8));
            var feedbackText=string.Join(" ",Enumerable.Repeat("I would like to confirm every requirement before we continue.",6));
            SetProperty(orchestrator,"LastTranscript",userText);
            SetProperty(orchestrator,"CurrentDialogueSubtitleText",replyText);
            SetProperty(orchestrator,"LastCorrectionHasFeedback",true);
            SetProperty(orchestrator,"LastCorrectionDisplayText",feedbackText);
            InvokeCorrectionSubtitleCue(orchestrator,"assistant_agent",assistantText);
            yield return null;Canvas.ForceUpdateCanvases();yield return null;

            Assert.That(SceneObject("PlayerSubtitle").GetComponent<TMP_Text>().text,Does.EndWith(userText));
            Assert.That(SceneObject("AgentSubtitle").GetComponent<TMP_Text>().text,Does.EndWith(assistantText));
            Assert.That(SceneObject("AvatarSubtitle").GetComponent<TMP_Text>().text,Does.EndWith(replyText));
            Assert.That(SceneObject("CorrectionFeedback").GetComponent<TMP_Text>().text,Does.EndWith(feedbackText));
            foreach(var name in new[]{"PlayerSubtitle","AgentSubtitle","AvatarSubtitle","CorrectionFeedback"})AssertCompleteWrappedText(name);
            AssertVerticalOrder("PlayerSubtitle","AgentSubtitle","AvatarSubtitle","CorrectionFeedback","CorrectionStatus","DialogueStatus");
            AssertChildrenInsidePanel(panel,"PlayerSubtitle","AgentSubtitle","AvatarSubtitle","CorrectionFeedback","CorrectionStatus","DialogueStatus");

            var chineseUserText=string.Concat(Enumerable.Repeat("我想完整说明预订信息，并在继续之前确认所有重要要求。",18));
            var chineseCorrectionText=string.Concat(Enumerable.Repeat("请使用完整而礼貌的表达方式，并清楚说明重要细节。",14));
            var chineseReplyText=string.Concat(Enumerable.Repeat("感谢你的说明，我会检查可用选项并逐项确认相关信息。",18));
            var chineseFeedbackText=string.Concat(Enumerable.Repeat("我想在继续之前确认所有重要要求。",14));
            SetProperty(orchestrator,"LastTranscript",chineseUserText);
            SetProperty(orchestrator,"CurrentDialogueSubtitleText",chineseReplyText);
            SetProperty(orchestrator,"LastCorrectionDisplayText",chineseFeedbackText);
            InvokeCorrectionSubtitleCue(orchestrator,"dialogue_avatar",chineseCorrectionText);
            yield return null;Canvas.ForceUpdateCanvases();yield return null;

            Assert.That(Active("AgentSubtitle"),Is.False);
            Assert.That(SceneObject("AvatarSubtitle").GetComponent<TMP_Text>().text,Does.Contain(chineseCorrectionText+"\n"+chineseReplyText));
            foreach(var name in new[]{"PlayerSubtitle","AvatarSubtitle","CorrectionFeedback"})AssertCompleteWrappedText(name);
            AssertVerticalOrder("PlayerSubtitle","AvatarSubtitle","CorrectionFeedback","CorrectionStatus","DialogueStatus");
            AssertChildrenInsidePanel(panel,"PlayerSubtitle","AvatarSubtitle","CorrectionFeedback","CorrectionStatus","DialogueStatus");
            Assert.That(panel.rect.height,Is.GreaterThan(originalHeight));
            Assert.That(panel.anchoredPosition.y+panel.rect.yMin,Is.EqualTo(originalBottom).Within(.01f));
            Assert.That(button.anchoredPosition,Is.EqualTo(originalButtonPosition));
        }
        [UnityTest]public IEnumerator PilotCreateSession_PersistsLockedMappingAndShowsAppearanceSelection()
        {Create();yield return null;var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");Assert.That((bool)Get(coordinator,"IsArmed"),Is.True);Assert.That(Get(coordinator,"Stage").ToString(),Is.EqualTo("AppearanceSelection"));Assert.That((string)Get(coordinator,"ParticipantId"),Does.StartWith("PILOT-P-"));Assert.That((string)Get(coordinator,"SessionId"),Does.StartWith("PILOT-S-"));var assignment=Get(coordinator,"Assignment");Assert.That((bool)Get(assignment,"collectionEligible"),Is.True);Assert.That((bool)Get(assignment,"developerTestAssignment"),Is.False);var conditions=((IEnumerable)Get(assignment,"conditions")).Cast<object>().ToArray();Assert.That(conditions.All(x=>(string)Get(Get(x,"task"),"taskId")=="pilot_restaurant_walk_in"),Is.True);Assert.That(conditions.Select(x=>Get(x,"embodimentCondition")).Distinct().Count(),Is.EqualTo(3));Assert.That(System.IO.File.Exists(System.IO.Path.Combine((string)Get(coordinator,"CurrentDataFolder"),"pilot_assignment.json")),Is.True);Assert.That(Active("PilotAppearanceSelectionPanel"),Is.True);}
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
        {Create();var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");var selected=Conditions(coordinator).First();var expectedTask=(string)Get(Get(selected,"task"),"taskId");Click(Get(selected,"embodimentCondition")+"AppearanceButton");yield return null;Assert.That(Active("PilotConditionTaskIntroductionPanel"),Is.True);var text=Text("PilotConditionTaskIntroductionPanel");Assert.That(text,Does.Contain("沟通目标："));Assert.That(text,Does.Not.Contain("voice_only"));Assert.That(text,Does.Not.Contain("floating_orb"));Assert.That(text,Does.Not.Contain("humanoid_agent"));Click("PilotTaskContinueButton");yield return new WaitForSecondsRealtime(1f);Assert.That(Active("ReadOnlyTaskGoalPanel"),Is.True);AssertTaskAboveFullWidthDialogue();SetHideDialogueSubtitles(true);yield return null;AssertTaskAboveFullWidthDialogue();SetHideDialogueSubtitles(false);yield return null;var task=Get(coordinator,"CurrentTask");var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");var payload=Get(orchestrator,"LastScenePayload");Assert.That(Get(task,"taskId"),Is.EqualTo(expectedTask));Assert.That(Get(payload,"taskType"),Is.EqualTo(expectedTask));}
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
            var conditionIndex=0;
            foreach(var condition in Conditions(coordinator).Reverse())
            {
                Click(Get(condition,"embodimentCondition")+"AppearanceButton");yield return null;
                Assert.That(Active("PilotConditionTaskIntroductionPanel"),Is.True);
                Click("PilotTaskContinueButton");yield return null;AssertExitOverlay();
                foreach(var phrase in PhrasesForTask((string)Get(Get(condition,"task"),"taskId")))EvaluatePilot(phrase);
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(Active("PilotQuestionnairePanel"),Is.True);AssertExitOverlay();
                if(conditionIndex++==0)
                {
                    var skip=Button("PilotQuestionnaireSkipButton");var submit=Button("PilotQuestionnaireSubmitButton");
                    Assert.That(skip.GetComponent<RectTransform>().anchoredPosition.x,Is.LessThan(submit.GetComponent<RectTransform>().anchoredPosition.x));
                    Click("PilotQuestionnaireSkipButton");yield return null;
                    Assert.That(Active("PilotQuestionnairePanel"),Is.True);
                    Assert.That(skip.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("确认跳过"));
                    Click("PilotQuestionnaireSkipButton");yield return null;
                    Assert.That(Get(condition,"status").ToString(),Is.EqualTo("Completed"));
                }
                else
                {
                    foreach(var item in new[]{"pilot_rc_01","pilot_sc_01","pilot_accept_01"})Click(item+"_4");
                    Click("PilotQuestionnaireSubmitButton");yield return null;
                }
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
            var deviceConditionIndex=0;
            foreach(var condition in chosenOrder)
            {
                var embodiment=Get(condition,"embodimentCondition");
                var expectedTask=Get(Get(condition,"task"),"taskId");
                Click(embodiment+"AppearanceButton");yield return null;
                Assert.That(Get(Get(coordinator,"CurrentTask"),"taskId"),Is.EqualTo(expectedTask));
                Click("PilotTaskContinueButton");yield return null;CompletePilotGoalsForQa(coordinator);yield return new WaitForSecondsRealtime(1f);
                if(deviceConditionIndex++==0){Click("PilotQuestionnaireSkipButton");Click("PilotQuestionnaireSkipButton");yield return null;}
                else{foreach(var item in new[]{"pilot_rc_01","pilot_sc_01","pilot_accept_01"})Click(item+"_4");Click("PilotQuestionnaireSubmitButton");yield return null;}
            }
            var recordedOrder=((IEnumerable)Get(Get(coordinator,"Assignment"),"participantSelectionOrder")).Cast<object>().Select(x=>x.ToString()).ToArray();
            Assert.That(recordedOrder,Is.EqualTo(chosenOrder.Select(x=>Get(x,"embodimentCondition").ToString()).ToArray()));
            Click("VoiceOnlyRank1");Click("FloatingOrbRank2");Click("HumanoidAgentRank3");Click("VoiceOnlyPreferred");Input("PilotRankingReason").text="Device validation ranking.";Click("PilotRankingSubmitButton");yield return null;
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            Assert.That(Get(orchestrator,"CurrentState").ToString(),Is.EqualTo("ExperimentCompleted"));
            Assert.That(Active("PilotExperimentCompletionPanel"),Is.True);Assert.That((bool)Get(Get(coordinator,"Assignment"),"collectionEligible"),Is.False);
            var rehearsal=ActiveObject("SceneTalkVR.Core.RehearsalSessionCoordinator, Assembly-CSharp");Assert.That((bool)Get(rehearsal,"ExperimentCompleted"),Is.True);
        }
        [UnityTest]public IEnumerator PicoDeviceValidation_StartsTaskWithSingleGatewayAuthorization()
        {
            ForcePicoDeviceValidation(true);Create();yield return null;
            var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            var condition=Conditions(coordinator).First();
            Click(Get(condition,"embodimentCondition")+"AppearanceButton");yield return null;
            var transportHost=InstallReadyUsbTransport(out var previousRouter);
            try
            {
                Click("PilotTaskContinueButton");yield return null;
                Assert.That(Get(coordinator,"Stage").ToString(),Is.EqualTo("Dialogue"));
            }
            finally
            {
                RestoreGatewayTransport(transportHost,previousRouter);
            }
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
        [UnityTest]public IEnumerator InterruptedPilotConversationOffersResumeOrRestartAndAppliesGoalPolicy()
        {
            Create();yield return null;
            var pilot=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            var experiment=ActiveObject("SceneTalkVR.Core.ExperimentSessionCoordinator, Assembly-CSharp");
            var orchestrator=ActiveObject("SceneTalkVR.Runtime.SceneTalkOrchestrator, Assembly-CSharp");
            var memory=ActiveObject("SceneTalkVR.History.LearningMemoryService, Assembly-CSharp");
            var condition=Conditions(pilot).First();
            var taskId=(string)Get(Get(condition,"task"),"taskId");
            Click(Get(condition,"embodimentCondition")+"AppearanceButton");yield return null;
            Click("PilotTaskContinueButton");
            for(var i=0;i<120&&string.IsNullOrWhiteSpace((string)Get(memory,"ActiveSessionId"));i++)yield return null;

            var originalConversationId=(string)Get(memory,"ActiveSessionId");
            var workflow=Get(pilot,"Workflow");
            var originalRunId=(string)Get(workflow,"PilotRunId");
            var firstGoal=((IEnumerable)Get(Get(pilot,"CurrentTask"),"goals")).Cast<object>().First();
            var firstGoalId=(string)Get(firstGoal,"goalId");
            Assert.That(originalConversationId,Is.Not.Empty);
            const string restoredUserText="Please keep this restaurant conversation in context.";
            memory.GetType().GetMethod("AppendTurn").Invoke(memory,new[]{
                restoredUserText,
                Get(orchestrator,"LastScenePayload")
            });
            Assert.That(EvaluatePilot(PhrasesForTask(taskId)[0]),Is.EqualTo(1));
            Assert.That(PilotGoalState(workflow,firstGoalId),Is.EqualTo("Confirmed"));

            var experimentId=(string)Get(Get(Get(experiment,"CurrentExperiment"),"summary"),"experimentId");
            experiment.GetType().GetMethod("ConfirmLeaveExperiment").Invoke(experiment,null);yield return null;
            var continueArgs=new object[]{experimentId,null};
            Assert.That((bool)experiment.GetType().GetMethod("ContinueExperiment").Invoke(experiment,continueArgs),Is.True,continueArgs[1] as string);
            yield return null;
            Assert.That(Active("ExperimentConversationResumePanel"),Is.True);
            Assert.That((string)Get(experiment,"SelectedConversationResumeSessionId"),Is.EqualTo(originalConversationId));

            Click("ContinueSelectedConversationButton");
            for(var i=0;i<120&&Get(orchestrator,"CurrentState").ToString()!="TurnReview";i++)yield return null;
            workflow=Get(pilot,"Workflow");
            Assert.That((string)Get(memory,"ActiveSessionId"),Is.EqualTo(originalConversationId));
            Assert.That((string)Get(workflow,"PilotRunId"),Is.EqualTo(originalRunId));
            Assert.That((string)Get(orchestrator,"LastTranscript"),Is.EqualTo(restoredUserText));
            Assert.That(((IEnumerable)Get(memory.GetType().GetMethod("GetSession").Invoke(
                memory,new object[]{originalConversationId}),"turns")).Cast<object>().Count(),Is.EqualTo(2));
            Assert.That(PilotGoalState(workflow,firstGoalId),Is.EqualTo("Confirmed"));

            experiment.GetType().GetMethod("ConfirmLeaveExperiment").Invoke(experiment,null);yield return null;
            continueArgs=new object[]{experimentId,null};
            Assert.That((bool)experiment.GetType().GetMethod("ContinueExperiment").Invoke(experiment,continueArgs),Is.True,continueArgs[1] as string);
            yield return null;
            Assert.That(Active("ExperimentConversationResumePanel"),Is.True);
            Click("StartNewExperimentConversationButton");
            for(var i=0;i<120;i++)
            {
                var activeSessionId=(string)Get(memory,"ActiveSessionId");
                if(!string.IsNullOrWhiteSpace(activeSessionId)
                    && !string.Equals(activeSessionId,originalConversationId,StringComparison.Ordinal))break;
                yield return null;
            }

            workflow=Get(pilot,"Workflow");
            Assert.That((string)Get(memory,"ActiveSessionId"),Is.Not.Empty.And.Not.EqualTo(originalConversationId));
            Assert.That((string)Get(workflow,"PilotRunId"),Is.Not.EqualTo(originalRunId));
            Assert.That(PilotGoalState(workflow,firstGoalId),Is.EqualTo("NotStarted"));
            Assert.That(memory.GetType().GetMethod("GetSession").Invoke(memory,new object[]{originalConversationId}),Is.Not.Null);
            var attempts=((IEnumerable)Get(Get(experiment,"CurrentExperiment"),"attempts")).Cast<object>().ToArray();
            Assert.That(attempts,Has.Length.EqualTo(2));
            Assert.That(attempts.Count(x=>Get(x,"status").ToString()=="Suspended"),Is.EqualTo(1));
            Assert.That(attempts.Count(x=>Get(x,"status").ToString()=="Running"),Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator PicoProductionPilotUsesCollectionQualificationAndPicoDeployment()
        {
            ForcePicoCollection(true);Create();yield return null;
            var coordinator=ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp");
            Assert.That((bool)Get(coordinator,"IsDeviceValidation"),Is.False);
            var context=Get(coordinator,"RuntimeContext");
            Assert.That(Get(context,"qualification").ToString(),Is.EqualTo("Collection"));
            Assert.That(Get(context,"dataOrigin"),Is.EqualTo("participant_collection"));
            Assert.That((bool)Get(context,"collectionEligible"),Is.True);
            Assert.That(Get(context,"deploymentTarget").ToString(),Is.EqualTo("Pico"));
            Assert.That(Get(context,"deploymentProfile"),Is.EqualTo("pico_lab"));
            var assignment=Get(coordinator,"Assignment");
            Assert.That(Get(assignment,"runtimeMode").ToString(),Is.EqualTo("PicoCollectionPilot"));
            Assert.That(Get(assignment,"deploymentProfile"),Is.EqualTo("pico_lab"));
        }
        private static void Create(){Click("PilotExperimentButton");}
        private static object[] Conditions(object coordinator)=>((IEnumerable)Get(Get(coordinator,"Assignment"),"conditions")).Cast<object>().ToArray();
        private static string[] PhrasesForTask(string taskId)=>taskId switch
        {
            "pilot_restaurant_walk_in"=>new[]{"No, I don't have a reservation.","There are four of us.","Do you have any tables by the window?","May I have a menu, please?"},
            "pilot_restaurant_ordering"=>new[]{"What do you recommend?","I'd like the grilled chicken.","How much does the grilled chicken cost?","I'd like a glass of water."},
            _=>new[]{"This isn't what I ordered.","I am allergic to peanuts.","Will I be charged extra?","How long will the new dish take to prepare?"}
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
        private static int EvaluatePilot(string transcript){var type=Type.GetType("SceneTalkVR.Core.GoalEvaluationOrchestrator, Assembly-CSharp");var pilot=Get(ActiveObject("SceneTalkVR.Core.PilotCollectionSessionCoordinator, Assembly-CSharp"),"Workflow");var turnId=Guid.NewGuid().ToString("N");type.GetMethod("NotifyParticipantTurnSubmitted").Invoke(null,new[]{null,pilot,turnId,transcript,"participant"});var count=(int)type.GetMethod("EvaluatePilotUserTranscript").Invoke(null,new[]{pilot,turnId,transcript,"participant"});if(count>0){var goals=Get(pilot,"Goals");var sequenceState=Get(goals,"SequenceState").ToString();if(sequenceState=="AwaitingParticipantTurn"){Assert.That((bool)goals.GetType().GetMethod("NotifyDialogueTurnCompleted").Invoke(goals,new object[]{turnId}),Is.False);var unlockTurnId=turnId+"-unlock";Assert.That((bool)goals.GetType().GetMethod("NotifyParticipantTurnSubmitted").Invoke(goals,new object[]{unlockTurnId}),Is.True);Assert.That((bool)goals.GetType().GetMethod("NotifyDialogueTurnCompleted").Invoke(goals,new object[]{unlockTurnId}),Is.True);}else if(sequenceState=="AwaitingAvatarReply"){Assert.That((bool)goals.GetType().GetMethod("NotifyDialogueTurnCompleted").Invoke(goals,new object[]{turnId}),Is.True);}}return count;}
        private static string PilotGoalState(object workflow,string goalId)=>Get(((IEnumerable)Get(Get(workflow,"Goals"),"Goals")).Cast<object>().Single(x=>(string)Get(x,"goalId")==goalId),"state").ToString();
        private static object Get(object value,string name){var type=value.GetType();return type.GetProperty(name)?.GetValue(value)??type.GetField(name)?.GetValue(value);}
        private static object ActiveObject(string typeName){var type=Type.GetType(typeName);return Resources.FindObjectsOfTypeAll(type).FirstOrDefault();}
        private static void CallActive(string typeName,string method){var value=ActiveObject(typeName);value?.GetType().GetMethod(method)?.Invoke(value,null);}
        private static void CompletePilotGoalsForQa(object coordinator){var args=new object[]{null};Assert.That((bool)coordinator.GetType().GetMethod("CompleteCurrentPilotGoalsForQa").Invoke(coordinator,args),Is.True,args[0] as string);}
        private static void ForcePicoDeviceValidation(bool value){var type=Type.GetType("SceneTalkVR.Core.ExperimentRuntimePlatform, Assembly-CSharp");type.GetProperty("ForcePicoDeviceValidationForTests").SetValue(null,value);}
        private static void ForcePicoCollection(bool value){var type=Type.GetType("SceneTalkVR.Core.ExperimentRuntimePlatform, Assembly-CSharp");type.GetProperty("ForcePicoCollectionForTests").SetValue(null,value);}
        private static void ResetUserSettings(){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("ResetAll").Invoke(null,null);}
        private static void SetFontScale(float value){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("SetFontScale").Invoke(null,new object[]{value});}
        private static void SetHideDialogueSubtitles(bool hidden){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("SetHideDialogueSubtitles").Invoke(null,new object[]{hidden});}
        private static void SetHideDialogueStatuses(bool hidden){var type=Type.GetType("SceneTalkVR.Core.SceneTalkUserSettingsStore, Assembly-CSharp");type.GetMethod("SetHideDialogueStatuses").Invoke(null,new object[]{hidden});}
        private static void SetProperty(object target,string name,object value)=>target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target,new[]{value});
        private static void InvokeCorrectionSubtitleCue(object orchestrator,string provider,string spokenText){var cueType=Type.GetType("SceneTalkVR.Core.CorrectionSubtitleCue, Assembly-CSharp",true);var cue=Activator.CreateInstance(cueType,new object[]{provider,spokenText});orchestrator.GetType().GetMethod("OnCorrectionSubtitleStarted",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(orchestrator,new[]{cue});}
        private static GameObject SceneObject(string name)=>Resources.FindObjectsOfTypeAll<GameObject>().First(x=>x.name==name&&x.scene.IsValid());
        private static GameObject InstallReadyUsbTransport(out object previousRouter)
        {
            var routerType=Type.GetType("SceneTalkVR.Core.GatewayTransportRouter, Assembly-CSharp",true);
            var activeField=routerType.GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            previousRouter=activeField.GetValue(null);
            var host=new GameObject("ReadyUsbTransportForPilotTest");
            var router=host.AddComponent(routerType);
            ((Behaviour)router).enabled=false;
            var configurationType=Type.GetType("SceneTalkVR.Core.GatewayTransportConfiguration, Assembly-CSharp",true);
            var preferenceType=Type.GetType("SceneTalkVR.Core.GatewayTransportPreference, Assembly-CSharp",true);
            var configuration=Activator.CreateInstance(configurationType);
            SetField(configuration,"preference",Enum.Parse(preferenceType,"UsbOnly"));
            SetField(configuration,"usbVoiceBaseUrl","http://127.0.0.1:8787");
            SetField(configuration,"usbLlmApiUrl","http://127.0.0.1:8788/api/llm/chat/completions");
            SetField(configuration,"lanVoiceBaseUrl","http://192.168.137.1:8787");
            SetField(configuration,"lanLlmApiUrl","http://192.168.137.1:8788/api/llm/chat/completions");
            SetField(configuration,"requireLiveTransport",true);
            routerType.GetMethod("Configure").Invoke(router,new[]{configuration});
            var stateMachine=routerType.GetField("stateMachine",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
            var routeType=Type.GetType("SceneTalkVR.Core.GatewayRouteSnapshot, Assembly-CSharp",true);
            var transportType=Type.GetType("SceneTalkVR.Core.GatewayTransportKind, Assembly-CSharp",true);
            var route=Activator.CreateInstance(routeType);
            SetField(route,"transport",Enum.Parse(transportType,"Usb"));
            SetField(route,"voiceBaseUrl","http://127.0.0.1:8787");
            SetField(route,"llmApiUrl","http://127.0.0.1:8788/api/llm/chat/completions");
            SetField(route,"selectedAtUtc","2026-07-28T00:00:00.0000000Z");
            stateMachine.GetType().GetMethod("MarkReady").Invoke(stateMachine,new[]{route});
            return host;
        }
        private static void RestoreGatewayTransport(GameObject host,object previousRouter)
        {
            var routerType=Type.GetType("SceneTalkVR.Core.GatewayTransportRouter, Assembly-CSharp",true);
            routerType.GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousRouter);
            UnityEngine.Object.Destroy(host);
        }
        private static void SetField(object target,string name,object value)=>target.GetType().GetField(name).SetValue(target,value);
        private static void AssertCompleteWrappedText(string name)
        {
            var text=SceneObject(name).GetComponent<TMP_Text>();text.ForceMeshUpdate(true,true);
            var preferred=text.GetPreferredValues(text.text,text.rectTransform.rect.width,0f);
            Assert.That(text.textWrappingMode,Is.EqualTo(TextWrappingModes.Normal),name+" must wrap normally.");
            Assert.That(text.overflowMode,Is.EqualTo(TextOverflowModes.Overflow),name+" must not truncate text.");
            Assert.That(text.enableAutoSizing,Is.False,name+" must preserve the selected font size.");
            Assert.That(text.textInfo.lineCount,Is.GreaterThan(1),name+" should wrap the long test content.");
            Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(preferred.y),name+" must allocate its full preferred height.");
            Assert.That(text.isTextOverflowing,Is.False,name+" must not report hidden overflow.");
        }
        private static void AssertVerticalOrder(params string[] names)
        {
            var panel=Rect("SubtitlePanel");var previousBottom=float.PositiveInfinity;
            foreach(var name in names)
            {
                var rect=Rect(name);var corners=new Vector3[4];rect.GetWorldCorners(corners);
                var values=corners.Select(x=>panel.InverseTransformPoint(x).y).ToArray();var top=values.Max();var bottom=values.Min();
                Assert.That(top,Is.LessThanOrEqualTo(previousBottom+.1f),name+" overlaps the row above it.");previousBottom=bottom;
            }
        }
        private static void AssertChildrenInsidePanel(RectTransform panel,params string[] names)
        {
            foreach(var name in names)
            {
                var rect=Rect(name);var corners=new Vector3[4];rect.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var local=panel.InverseTransformPoint(corner);
                    Assert.That(local.x,Is.InRange(panel.rect.xMin-.1f,panel.rect.xMax+.1f),name+" extends outside the panel horizontally.");
                    Assert.That(local.y,Is.InRange(panel.rect.yMin-.1f,panel.rect.yMax+.1f),name+" extends outside the panel vertically.");
                }
            }
        }
        private static void AssertTaskAboveFullWidthDialogue()
        {
            var task = Rect("ReadOnlyTaskGoalPanel");
            var dialogue = Rect("SubtitlePanel");
            var canvas = Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.gameObject.scene.IsValid() && x.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal));
            var canvasRect = (RectTransform)canvas.transform;
            var taskCanvas = task.GetComponentInParent<Canvas>();
            var taskCanvasRect = (RectTransform)taskCanvas.transform;
            var taskCorners = new Vector3[4];
            task.GetWorldCorners(taskCorners);
            var taskCornersInMainCanvas = taskCorners.Select(canvasRect.InverseTransformPoint).ToArray();
            var taskRight = taskCornersInMainCanvas.Max(x => x.x);
            var taskTop = taskCornersInMainCanvas.Max(x => x.y);
            var taskBottom = taskCornersInMainCanvas.Min(x => x.y);
            var dialogueLeft = dialogue.anchoredPosition.x + dialogue.rect.xMin;
            var dialogueRight = dialogue.anchoredPosition.x + dialogue.rect.xMax;
            var dialogueTop = dialogue.anchoredPosition.y + dialogue.rect.yMax;
            var dialogueBottom = dialogue.anchoredPosition.y + dialogue.rect.yMin;
            var goalText = task.GetComponentsInChildren<TMP_Text>(true).First(x => x.name == "GoalStateText");
            var interactionCamera = taskCanvas.worldCamera;
            Assert.That(interactionCamera, Is.Not.Null, "Task canvas must use the interaction camera.");

            Assert.That(taskBottom - dialogueTop, Is.GreaterThanOrEqualTo(20f), "Task goals must remain above the dialogue panel.");
            Assert.That(taskCanvas, Is.Not.SameAs(canvas), "Task goals must use a dedicated canvas.");
            Assert.That(taskCanvas.transform.parent, Is.EqualTo(canvas.transform), "The task canvas must follow the main world canvas.");
            Assert.That(taskCanvasRect.anchoredPosition.x, Is.LessThanOrEqualTo(-550f));
            Assert.That(taskRight, Is.LessThanOrEqualTo(canvasRect.rect.xMin + 20.1f), "Task goals must remain at the left perimeter of the main canvas.");
            Assert.That(taskTop, Is.LessThanOrEqualTo(canvasRect.rect.yMax));
            Assert.That(task.rect.width * task.rect.height, Is.GreaterThanOrEqualTo(95000f), "Task goals must remain enlarged.");
            Assert.That(goalText.fontSizeMax, Is.GreaterThanOrEqualTo(20f), "Task goal text must remain readable.");
            Assert.That(Quaternion.Angle(taskCanvas.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f), "Task canvas local rotation must remain zero.");
            Assert.That(task.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(dialogueLeft, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin));
            Assert.That(dialogueRight, Is.LessThanOrEqualTo(canvasRect.rect.xMax));
            Assert.That(dialogueTop, Is.LessThanOrEqualTo(-75f), "Dialogue must stay at the bottom perimeter and keep the center clear.");
            Assert.That(dialogueBottom, Is.GreaterThanOrEqualTo(canvasRect.rect.yMin));
            Assert.That(dialogue.rect.width, Is.GreaterThanOrEqualTo(800f), "Dialogue must keep the existing full-width layout.");
            Assert.That(dialogue.anchoredPosition.y, Is.LessThan(0f));
        }
        private static RectTransform Rect(string name)=>Resources.FindObjectsOfTypeAll<GameObject>().First(x=>x.name==name&&x.scene.IsValid()).GetComponent<RectTransform>();
        private static void AssertHomeNavigation(){Assert.That(Active("QuitButton"),Is.True);Assert.That(Label("QuitButton"),Is.EqualTo("退出"));Assert.That(Button("QuitButton").transform.parent.gameObject.name,Is.EqualTo("InitialPanel"));Assert.That(Active("ExitButton"),Is.False,"The home page must use Quit instead of the global Exit button.");}
        private static void AssertExitOverlay(){var button=Button("ExitButton");var rect=button.GetComponent<RectTransform>();var canvas=Resources.FindObjectsOfTypeAll<Canvas>().First(x=>x.gameObject.scene.IsValid()&&x.gameObject.name.StartsWith("SceneTalkVR World UI",StringComparison.Ordinal));Assert.That(button.gameObject.activeInHierarchy,Is.True);Assert.That(button.transform.parent,Is.EqualTo(canvas.transform));Assert.That(button.transform.GetSiblingIndex(),Is.EqualTo(canvas.transform.childCount-1));Assert.That(rect.anchorMin,Is.EqualTo(Vector2.one));Assert.That(rect.anchorMax,Is.EqualTo(Vector2.one));Assert.That(rect.anchoredPosition.x,Is.LessThan(0f));Assert.That(rect.anchoredPosition.y,Is.LessThan(0f));Assert.That(Active("QuitButton"),Is.False);if(Active("PilotQuestionnairePanel"))AssertOverlayText("PilotQuestionnairePanel");}
        private static void AssertNoChineseSystemText(){foreach(var text in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(x=>x.gameObject.scene.IsValid()))Assert.That(text.text,Does.Not.Match("[\\u3400-\\u9fff]"),text.gameObject.name+" should use the selected English UI language.");}
    }
}
