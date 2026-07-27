using System;
using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PilotCollectionParticipantUi : MonoBehaviour
    {
        private Canvas canvas; private PilotCollectionSessionCoordinator coordinator; private SceneTalkOrchestrator orchestrator;
        private GameObject setup,selection,introduction,questionnaire,ranking,completion;
        private TMP_InputField participantInput,sessionInput,reasonInput; private TMP_Text setupError,introductionText,questionnaireError,rankingError;
        private readonly Dictionary<string,Button> answerButtons=new(); private readonly Dictionary<PilotEmbodimentCondition,int> ranks=new();
        private readonly Dictionary<string,Button> rankButtons=new(); private PilotEmbodimentCondition? preferred;
        private readonly Dictionary<PilotEmbodimentCondition,Button> appearanceButtons=new(); private readonly Dictionary<PilotEmbodimentCondition,TMP_Text> appearanceStatuses=new();
        private string questionnaireLinkage; private int builtTaskPosition=-2;

        public void Configure(Canvas targetCanvas,SceneTalkOrchestrator targetOrchestrator)
        {
            canvas=targetCanvas;orchestrator=targetOrchestrator;EnsureCoordinator();Build();
        }
        public void ResetForCanvasRebuild(){setup=selection=introduction=questionnaire=ranking=completion=null;participantInput=sessionInput=reasonInput=null;setupError=introductionText=questionnaireError=rankingError=null;answerButtons.Clear();rankButtons.Clear();appearanceButtons.Clear();appearanceStatuses.Clear();ranks.Clear();questionnaireLinkage="";builtTaskPosition=-2;}
        public void ResetFinalRankingDraft()
        {
            foreach(var condition in ranks.Keys.ToArray())ranks[condition]=0;
            preferred=null;
            if(reasonInput!=null)reasonInput.text=string.Empty;
            if(rankingError!=null)rankingError.text=string.Empty;
            RefreshRankButtons();
            RefreshPreferredButtons();
        }
        public void OpenSetup(){EnsureCoordinator();coordinator.OpenSetup();Refresh();}
        public void OpenAutomaticParticipantFlow(){EnsureCoordinator();if(coordinator==null)return;if(!coordinator.OpenOrCreateAutomaticParticipantSession(out var error))Debug.LogError("[PilotCollection] "+error,this);Refresh();}
        private void Awake(){EnsureCoordinator();}
        private void Update(){Refresh();}
        private void EnsureCoordinator()
        {
            var manager=FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            coordinator=PilotCollectionSessionCoordinator.Active??FindFirstObjectByType<PilotCollectionSessionCoordinator>(FindObjectsInactive.Include);
            if(coordinator==null&&manager!=null)coordinator=manager.gameObject.AddComponent<PilotCollectionSessionCoordinator>();
            coordinator?.Configure(manager,orchestrator??FindFirstObjectByType<SceneTalkOrchestrator>(FindObjectsInactive.Include));
        }
        private void Build()
        {
            if(canvas==null||setup!=null)return;
            setup=Panel("PilotSessionSetupPanel",new Vector2(760,500));
            Label(setup.transform,"Title","Pilot Session Setup",new Vector2(0,205),new Vector2(650,48),30);
            Label(setup.transform,"ParticipantLabel","Participant ID",new Vector2(-235,112),new Vector2(190,38),19,TextAnchor.MiddleLeft);
            participantInput=Input(setup.transform,"PilotParticipantIdInput","Required",new Vector2(100,112),new Vector2(400,48));
            Label(setup.transform,"SessionLabel","Session ID",new Vector2(-235,48),new Vector2(190,38),19,TextAnchor.MiddleLeft);
            sessionInput=Input(setup.transform,"PilotSessionIdInput","Optional — generated automatically",new Vector2(100,48),new Vector2(400,48));
            setupError=Label(setup.transform,"Validation","",new Vector2(0,-22),new Vector2(650,54),17);setupError.color=new Color(1,.55f,.42f,1);
            Button(setup.transform,"CreatePilotSessionButton","Create Pilot Session",new Vector2(-175,-105),()=>Arm(false),new Vector2(260,48));
            Button(setup.transform,"ResumePilotSessionButton","Resume Pilot Session",new Vector2(175,-105),()=>Arm(true),new Vector2(260,48));
            Button(setup.transform,"PilotSetupBackButton","Back",new Vector2(0,-180),()=>coordinator.EndSession(),new Vector2(180,44));

            selection=Panel("PilotAppearanceSelectionPanel",new Vector2(820,520));
            Label(selection.transform,"Title","Choose an Agent Appearance",new Vector2(0,210),new Vector2(720,46),29);
            Label(selection.transform,"Body","Complete all three appearances in any order. Completed appearances cannot be selected again.",new Vector2(0,166),new Vector2(700,50),17);
            var appearanceValues=new[]{PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent};
            for(var i=0;i<appearanceValues.Length;i++){var value=appearanceValues[i];var y=92-i*112;appearanceButtons[value]=Button(selection.transform,value+"AppearanceButton",Friendly(value),new Vector2(0,y),()=>SelectAppearance(value),new Vector2(420,58));appearanceStatuses[value]=Label(selection.transform,value+"AppearanceStatus","Available",new Vector2(0,y-46),new Vector2(420,28),15);}

            introduction=Panel("PilotConditionTaskIntroductionPanel",new Vector2(790,530));
            Label(introduction.transform,"Title","Restaurant Speaking Task",new Vector2(0,220),new Vector2(690,44),28);
            introductionText=Label(introduction.transform,"TaskContent","",new Vector2(0,20),new Vector2(680,350),19,TextAnchor.UpperLeft);
            Button(introduction.transform,"PilotTaskContinueButton","Continue",new Vector2(0,-224),BeginTask,new Vector2(210,48));

            questionnaire=Panel("PilotQuestionnairePanel",new Vector2(940,570));
            Label(questionnaire.transform,"Title","Questionnaire / 问卷",new Vector2(0,238),new Vector2(820,42),28);
            Label(questionnaire.transform,"Anchors","1 = Strongly disagree / 非常不同意     7 = Strongly agree / 非常同意",new Vector2(0,198),new Vector2(820,34),17);
            questionnaireError=Label(questionnaire.transform,"Validation","",new Vector2(0,-214),new Vector2(800,34),17);
            Button(questionnaire.transform,"PilotQuestionnaireSubmitButton","Submit",new Vector2(0,-258),SubmitQuestionnaire,new Vector2(210,44));

            ranking=Panel("PilotFinalRankingPanel",new Vector2(900,570));
            Label(ranking.transform,"Title","Final Feedback Ranking",new Vector2(0,238),new Vector2(790,44),28);
            Label(ranking.transform,"Instruction","Give each feedback experience a unique rank (1 = most preferred).",new Vector2(0,198),new Vector2(790,34),18);
            var values=new[]{PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent};
            for(var i=0;i<values.Length;i++){var condition=values[i];var y=130-i*72;Label(ranking.transform,condition+"Label",Friendly(condition),new Vector2(-245,y),new Vector2(360,42),19,TextAnchor.MiddleLeft);ranks[condition]=0;for(var rankValue=1;rankValue<=3;rankValue++){var captured=rankValue;var button=Button(ranking.transform,condition+"Rank"+rankValue,rankValue.ToString(),new Vector2(25+rankValue*58,y),()=>SelectRank(condition,captured),new Vector2(46,42));rankButtons[condition+":"+rankValue]=button;}Button(ranking.transform,condition+"Preferred","Preferred",new Vector2(315,y),()=>SelectPreferred(condition),new Vector2(126,42));}
            reasonInput=Input(ranking.transform,"PilotRankingReason","Why did you prefer this feedback experience?",new Vector2(0,-125),new Vector2(720,60));
            rankingError=Label(ranking.transform,"Validation","",new Vector2(0,-188),new Vector2(760,32),17);
            Button(ranking.transform,"PilotRankingSubmitButton","Submit Ranking",new Vector2(0,-242),SubmitRanking,new Vector2(220,46));

            completion=Panel("PilotExperimentCompletionPanel",new Vector2(700,300));
            Label(completion.transform,"Title","Pilot Experiment Completed",new Vector2(0,65),new Vector2(620,50),30);
            Label(completion.transform,"Message","Thank you. You have completed the pilot study.",new Vector2(0,-20),new Vector2(600,80),22);
            Button(completion.transform,"PilotCompletionContinueButton","Continue",new Vector2(0,-105),ContinueAfterCompletion,new Vector2(210,48));

            // Build under an active hierarchy so TMP initializes its font material
            // before SceneTalkWorldUiRenderer applies the overlay shader.
            Refresh();
        }
        private void Arm(bool resume){var ok=resume?coordinator.ResumeSession(participantInput.text,sessionInput.text,out var error):coordinator.CreateSession(participantInput.text,sessionInput.text,out error);if(ok)sessionInput.text=coordinator.SessionId;setupError.text=ok?"":Humanize(error);}
        private void BeginTask(){if(!coordinator.BeginCurrentTask(out var error))Debug.LogError("[PilotCollection] "+error,this);}
        private void SelectAppearance(PilotEmbodimentCondition value){if(!coordinator.SelectEmbodiment(value,out var error))Debug.LogWarning("[PilotCollection] "+error,this);Refresh();}
        private void BuildQuestionnaire()
        {
            var service=coordinator?.Workflow?.Questionnaire;var session=service?.ActiveSession;if(session==null||questionnaireLinkage==session.questionnaireLinkageKey)return;
            questionnaireLinkage=session.questionnaireLinkageKey;foreach(var item in answerButtons.Values.Select(x=>x.gameObject).ToArray())Destroy(item.transform.parent.gameObject);answerButtons.Clear();
            var definition=service.Definition;var manager=FindFirstObjectByType<ExperimentConditionManager>();var enabled=manager.QuestionnaireCatalog.GetEnabledItems(definition.questionnaireId,manager.ExperimentProtocol).ToArray();
            for(var i=0;i<enabled.Length;i++){var item=enabled[i];var y=128-i*105;var row=new GameObject("PilotQuestion_"+item.itemId,typeof(RectTransform));row.transform.SetParent(questionnaire.transform,false);Label(row.transform,"Prompt",item.promptChinese+"\n"+item.promptEnglish,new Vector2(-175,y),new Vector2(470,68),17,TextAnchor.MiddleLeft);for(var value=1;value<=7;value++){var captured=value;var button=Button(row.transform,item.itemId+"_"+value,value.ToString(),new Vector2(92+(value-1)*48,y),()=>SetAnswer(item.itemId,captured),new Vector2(42,42));answerButtons[item.itemId+":"+value]=button;}}
        }
        private void SetAnswer(string item,int value){coordinator.Workflow.Questionnaire.SetResponse(item,value.ToString(),out var error);questionnaireError.text=Humanize(error);RefreshAnswers();}
        private void RefreshAnswers(){var responses=coordinator?.Workflow?.Questionnaire?.ActiveSession?.responses??Array.Empty<QuestionnaireResponse>();foreach(var pair in answerButtons){var split=pair.Key.LastIndexOf(':');var item=pair.Key[..split];var value=pair.Key[(split+1)..];var selected=responses.Any(x=>x.itemId==item&&x.rawValue==value);pair.Value.GetComponent<Image>().color=selected?new Color(.12f,.68f,.34f,1):new Color(.12f,.38f,.62f,1);}}
        private void SubmitQuestionnaire(){if(!coordinator.SubmitQuestionnaire(out var error)){questionnaireError.text=Humanize(error);return;}questionnaireError.text="";}
        private void SelectRank(PilotEmbodimentCondition condition,int rank)
        {
            var previousRank=ranks[condition];
            var occupied=ranks.FirstOrDefault(x=>x.Key!=condition&&x.Value==rank);
            if(occupied.Value==rank)ranks[occupied.Key]=previousRank;
            ranks[condition]=rank;
            RefreshRankButtons();
            rankingError.text="";
        }
        private void RefreshRankButtons(){foreach(var pair in rankButtons)pair.Value.GetComponent<Image>().color=ranks.Any(x=>pair.Key==x.Key+":"+x.Value)?new Color(.12f,.68f,.34f,1):new Color(.12f,.38f,.62f,1);}
        private void SelectPreferred(PilotEmbodimentCondition condition){preferred=condition;RefreshPreferredButtons();}
        private void RefreshPreferredButtons(){if(ranking==null)return;foreach(var button in ranking.GetComponentsInChildren<Button>(true).Where(x=>x.name.EndsWith("Preferred")))button.GetComponent<Image>().color=preferred.HasValue&&button.name.StartsWith(preferred.Value.ToString())?new Color(.12f,.68f,.34f,1):new Color(.12f,.38f,.62f,1);}
        private void SubmitRanking(){if(ranks.Values.Any(x=>x<1||x>3)||ranks.Values.Distinct().Count()!=3){rankingError.text="Use each rank exactly once.";return;}if(!preferred.HasValue){rankingError.text="Select the overall preferred feedback experience.";return;}if(string.IsNullOrWhiteSpace(reasonInput.text)){rankingError.text="Please provide a reason.";return;}var entries=ranks.Select(x=>new PreferenceRankEntry{embodimentCondition=PilotProtocolValues.Label(x.Key),rank=x.Value}).OrderBy(x=>x.rank).ToArray();var response=new PreferenceRankingResponse{rankings=entries,preferredEmbodimentCondition=PilotProtocolValues.Label(preferred.Value),reason=reasonInput.text.Trim()};if(!coordinator.SubmitFinalRanking(response,out var error))rankingError.text=Humanize(error);}
        private void ContinueAfterCompletion(){var experiment=ExperimentSessionCoordinator.Active;if(experiment?.HasActiveExperiment==true)experiment.ContinueAfterExperimentCompletion();else coordinator?.EndSession();}
        private void Refresh()
        {
            if(setup==null)return;var stage=coordinator?.Stage??PilotParticipantStage.None;
            var exitConfirmationVisible=orchestrator?.CurrentState==SceneTalkState.ExperimentExitConfirm;
            Set(setup,!exitConfirmationVisible&&stage==PilotParticipantStage.Setup);Set(selection,!exitConfirmationVisible&&stage==PilotParticipantStage.AppearanceSelection);Set(introduction,!exitConfirmationVisible&&stage==PilotParticipantStage.TaskIntroduction);Set(questionnaire,!exitConfirmationVisible&&stage==PilotParticipantStage.Questionnaire);Set(ranking,!exitConfirmationVisible&&stage==PilotParticipantStage.FinalRanking);Set(completion,!exitConfirmationVisible&&stage==PilotParticipantStage.Completion);
            if(stage==PilotParticipantStage.AppearanceSelection)RefreshAppearanceSelection();
            if(stage==PilotParticipantStage.TaskIntroduction&&builtTaskPosition!=coordinator.CurrentPosition){builtTaskPosition=coordinator.CurrentPosition;var task=coordinator.CurrentTask;if(task!=null)introductionText.text=task.displayName+"\n\n"+task.context+"\n\nCommunication goals:\n"+string.Join("\n",task.goals.Select(x=>"• "+x.text));}
            if(stage==PilotParticipantStage.Questionnaire){BuildQuestionnaire();RefreshAnswers();questionnaire.transform.SetAsLastSibling();}
            if(stage==PilotParticipantStage.FinalRanking)ranking.transform.SetAsLastSibling();if(stage==PilotParticipantStage.Completion)completion.transform.SetAsLastSibling();
            if(stage!=PilotParticipantStage.None)GetComponent<SceneTalkFlowUiController>()?.BringExitButtonToFront();
        }
        private void RefreshAppearanceSelection(){foreach(var pair in appearanceButtons){var item=coordinator?.Assignment?.conditions?.FirstOrDefault(x=>x.embodimentCondition==pair.Key);var selectable=item!=null&&(item.status==PilotRunStatus.Assigned||item.status==PilotRunStatus.TechnicalInvalid);pair.Value.interactable=selectable;if(appearanceStatuses.TryGetValue(pair.Key,out var label))label.text=item==null?"Unavailable":item.status==PilotRunStatus.Completed?"Completed":item.status==PilotRunStatus.TechnicalInvalid?"Retry available":selectable?"Available":"In progress";}}
        private GameObject Panel(string name,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(canvas.transform,false);go.AddComponent<Image>().color=new Color(.035f,.05f,.08f,.98f);go.GetComponent<RectTransform>().sizeDelta=size;return go;}
        private static GameObject Node(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go;}
        private static TMP_Text Label(Transform parent,string name,string value,Vector2 pos,Vector2 size,int font,TextAnchor anchor=TextAnchor.MiddleCenter){var go=Node(parent,name);var text=go.AddComponent<TextMeshProUGUI>();text.text=value;text.color=Color.white;text.fontSize=font;text.alignment=ToTmpAlignment(anchor);text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;text.rectTransform.anchoredPosition=pos;text.rectTransform.sizeDelta=size;return text;}
        private static Button Button(Transform parent,string name,string label,Vector2 pos,UnityEngine.Events.UnityAction action,Vector2 size){var go=Node(parent,name);go.AddComponent<Image>().color=new Color(.12f,.38f,.62f,1);var button=go.AddComponent<Button>();button.onClick.AddListener(action);go.GetComponent<RectTransform>().anchoredPosition=pos;go.GetComponent<RectTransform>().sizeDelta=size;var text=Label(go.transform,"Label",label,Vector2.zero,size,18);text.raycastTarget=false;return button;}
        private static TMP_InputField Input(Transform parent,string name,string placeholder,Vector2 pos,Vector2 size){var go=Node(parent,name);go.AddComponent<Image>().color=new Color(.12f,.16f,.22f,1);var input=go.AddComponent<TMP_InputField>();var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=pos;rect.sizeDelta=size;var text=Label(go.transform,"Text","",Vector2.zero,size-new Vector2(20,8),18,TextAnchor.MiddleLeft);var hint=Label(go.transform,"Placeholder",placeholder,Vector2.zero,size-new Vector2(20,8),17,TextAnchor.MiddleLeft);hint.color=new Color(.65f,.7f,.75f,1);input.textViewport=rect;input.textComponent=text;input.placeholder=hint;return input;}
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)=>anchor switch{TextAnchor.UpperLeft=>TextAlignmentOptions.TopLeft,TextAnchor.UpperCenter=>TextAlignmentOptions.Top,TextAnchor.UpperRight=>TextAlignmentOptions.TopRight,TextAnchor.MiddleLeft=>TextAlignmentOptions.Left,TextAnchor.MiddleRight=>TextAlignmentOptions.Right,TextAnchor.LowerLeft=>TextAlignmentOptions.BottomLeft,TextAnchor.LowerCenter=>TextAlignmentOptions.Bottom,TextAnchor.LowerRight=>TextAlignmentOptions.BottomRight,_=>TextAlignmentOptions.Center};
        private static string Friendly(PilotEmbodimentCondition value)=>value==PilotEmbodimentCondition.VoiceOnly?"Voice Feedback Only":value==PilotEmbodimentCondition.FloatingOrb?"Floating Orb Feedback":"Humanoid Assistant Feedback";
        private static string Humanize(string error)=>string.IsNullOrWhiteSpace(error)?"":error.StartsWith("required_item_missing:")?"Please answer every required question.":error.Replace('_',' ');
        private static void Set(GameObject value,bool active){if(value!=null&&value.activeSelf!=active)value.SetActive(active);}
    }
}
