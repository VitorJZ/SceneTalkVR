using System;
using System.Globalization;
using System.IO;
using System.Text;
using SceneTalkVR.AvatarSystem;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class PilotEventRecord
    {
        public string schemaVersion="1.0";public string timestampUtc;public string eventType;public string pilotProtocolVersion;public string pilotAssignmentVersion;public string pilotRunId;public string participantId;public string sessionId;public string sequenceId;public int conditionPosition;public string embodimentCondition;public string pilotFeedbackStyle;public string taskId;public string taskAssignmentId;public string feedbackTextHash;public string actualPlaybackActor;public string visualEntityType;public string visualPrefabKey;public string voiceProfileKey;public string audioSourcePolicy;public float spatialBlend;public Vector3 sourcePosition;public string feedbackPlaybackStartedAt;public string feedbackPlaybackEndedAt;public long userEndToFeedbackAudioMs=-1;public long feedbackToDialogueGapMs=-1;public string technicalValidity;public string failureStage;public string failureReason;public string questionnaireLinkageKey;
    }

    [DisallowMultipleComponent]
    public sealed class PilotWorkflowCoordinator : MonoBehaviour, ISceneTalkSessionReset
    {
        public static PilotWorkflowCoordinator Active {get;private set;}
        [SerializeField] private ExperimentConditionManager conditionManager; [SerializeField] private PilotEmbodimentPresenter presenter;
        private readonly GoalProgressTracker goals=new GoalProgressTracker(); private readonly QuestionnaireSessionService questionnaire=new QuestionnaireSessionService();
        private PilotAssignment assignment;private PilotConditionAssignment current;
        private long userSpeechEndedMs=-1,feedbackStartedMs=-1,feedbackEndedMs=-1,dialogueStartedMs=-1; private string feedbackStartedAt="",feedbackEndedAt="";
        public PilotAssignment Assignment=>assignment;public PilotConditionAssignment Current=>current;public GoalProgressTracker Goals=>goals;public QuestionnaireSessionService Questionnaire=>questionnaire;
        public bool HasActivePilotRun => assignment != null && current != null && !string.IsNullOrWhiteSpace(PilotRunId);
        public string PilotRunId{get;private set;}public string QuestionnaireLinkageKey{get;private set;}public PilotEmbodimentCondition CurrentEmbodiment=>current?.embodimentCondition??PilotEmbodimentCondition.VoiceOnly;
        private void Awake(){if(conditionManager==null)conditionManager=GetComponent<ExperimentConditionManager>();if(presenter==null)presenter=GetComponent<PilotEmbodimentPresenter>()??gameObject.AddComponent<PilotEmbodimentPresenter>();Active=this;}
        private void OnDestroy(){if(Active==this)Active=null;}
        public void Configure(ExperimentConditionManager manager,PilotEmbodimentPresenter target=null){conditionManager=manager;if(target!=null)presenter=target;else if(presenter==null)presenter=GetComponent<PilotEmbodimentPresenter>()??gameObject.AddComponent<PilotEmbodimentPresenter>();Active=this;}
        public bool LoadAssignment(PilotAssignment value,out string error){if(value==null){error="pilot_assignment_missing";return false;}if(conditionManager==null){error="condition_manager_missing";return false;}if(!PilotAssignmentAllocator.IsCompatible(value,conditionManager.ExperimentProtocol?.ProtocolVersion??value.pilotProtocolVersion,conditionManager.TaskCatalog?.CatalogVersion??value.taskCatalogVersion,out error))return false;assignment=value;Write("PilotAssignmentLoaded");return true;}
        public bool CreateLocked(string participant,string session,out string error){var allocator=new PilotAssignmentAllocator();if(!allocator.TryCreateLocked(participant,session,conditionManager.ExperimentProtocol,conditionManager.TaskCatalog,conditionManager.PilotPresentationCatalog,out var value,out error))return false;return LoadAssignment(value,out error);}
        public bool Prepare(int position,bool retry,out string error)
        {
            error="";if(assignment?.conditions==null||position<0||position>=assignment.conditions.Length){error="pilot_condition_missing";return false;}var next=assignment.conditions[position];if(next.status==PilotRunStatus.Completed){error="pilot_condition_completed";return false;}if(next.status==PilotRunStatus.TechnicalInvalid&&!retry){error="pilot_retry_requires_authorization";return false;}
            conditionManager.ResetConditionSessionBoundary();presenter.ResetSession();questionnaire.Reset();goals.ResetGoals(null);userSpeechEndedMs=feedbackStartedMs=feedbackEndedMs=dialogueStartedMs=-1;feedbackStartedAt=feedbackEndedAt="";current=next;current.runAttempt++;PilotRunId=$"pr-{assignment.assignmentSeed}-{position}-{current.runAttempt}-{Guid.NewGuid():N}";QuestionnaireLinkageKey=$"pql-{PilotRunId}";current.latestPilotRunId=PilotRunId;current.status=PilotRunStatus.Preparing;
            var profile=conditionManager.PilotPresentationCatalog?.Find(current.embodimentCondition);if(profile==null){error="pilot_profile_missing";return Invalid("Presentation",error);}
            var locked=!assignment.developerTestAssignment;if(!presenter.Configure(profile,assignment.voiceOnlyAudioPolicy,locked,out error))return Invalid("Presentation",error);
            if(!conditionManager.ApplyPilotAssignment(assignment.feedbackStyle,current.task.taskId,assignment.participantId,assignment.sessionId,out error))return Invalid("Task",error);
            goals.ResetGoals(conditionManager.TaskCatalog.Find(current.task.taskId));current.status=PilotRunStatus.Running;Write("PilotConditionStarted");return true;
        }
        public void CompleteTask(){if(current==null||current.status!=PilotRunStatus.Running)return;current.status=PilotRunStatus.TaskCompleted;Write("PilotTaskCompleted");current.status=PilotRunStatus.AwaitingPilotQuestionnaire;Write("PilotAwaitingQuestionnaire");}
        public bool BeginQuestionnaire(out string error)
        {
            if(current==null||current.status!=PilotRunStatus.AwaitingPilotQuestionnaire){error="pilot_not_awaiting_questionnaire";return false;}var def=conditionManager.QuestionnaireCatalog?.Find("pilot_condition_v1");var context=new QuestionnaireSession{protocolVersion=assignment.pilotProtocolVersion,questionnaireCatalogVersion=conditionManager.QuestionnaireCatalog.CatalogVersion,participantId=assignment.participantId,sessionId=assignment.sessionId,sequenceId=assignment.sequenceId,conditionRunId=PilotRunId,questionnaireLinkageKey=QuestionnaireLinkageKey,conditionPosition=current.conditionPosition,embodimentCondition=PilotProtocolValues.Label(current.embodimentCondition),taskId=current.task.taskId,taskAssignmentId=current.task.taskAssignmentId,technicalValidity=ExperimentTechnicalValidity.Valid,conditionStatus=ConditionRunStatus.QuestionnaireInProgress};questionnaire.Configure(conditionManager.QuestionnaireCatalog,conditionManager.ExperimentProtocol);if(!questionnaire.Begin(def,context,out error))return false;current.status=PilotRunStatus.PilotQuestionnaireInProgress;Write("PilotQuestionnaireStarted");return true;
        }
        public bool SubmitQuestionnaire(out string error){if(current==null||current.status!=PilotRunStatus.PilotQuestionnaireInProgress){error="pilot_questionnaire_not_in_progress";return false;}if(!questionnaire.Submit(QuestionnaireSessionService.DefaultFolder,out error))return false;current.status=PilotRunStatus.PilotQuestionnaireSubmitted;Write("PilotQuestionnaireSubmitted");current.status=PilotRunStatus.Completed;presenter.ResetSession();Write("PilotConditionCompleted");return true;}
        public bool SubmitFinalRanking(PreferenceRankingResponse ranking,out string error)
        {
            if(assignment?.conditions==null||Array.Exists(assignment.conditions,x=>x.status!=PilotRunStatus.Completed)){error="pilot_final_ranking_requires_three_valid_conditions";return false;}
            if(ranking==null){error="pilot_ranking_missing";return false;}
            var labels=new[]{"voice_only","floating_orb","humanoid_agent"};
            if(!ranking.ValidateUnique(labels,out error))return false;
            if(!Array.Exists(labels,x=>x==ranking.preferredEmbodimentCondition)){error="pilot_preferred_embodiment_missing_or_invalid";return false;}
            if(string.IsNullOrWhiteSpace(ranking.reason)){error="pilot_ranking_reason_required";return false;}
            QuestionnaireResearchExporter.AppendRanking(QuestionnaireSessionService.DefaultFolder,ranking);Write("PilotFinalRankingSubmitted");error="";return true;
        }
        public void MarkTechnicalInvalid(string stage,string reason){Invalid(stage,reason);}
        public bool PrepareNext(out string error)
        {
            error = "";
            if (assignment?.conditions == null) { error = "pilot_assignment_missing"; return false; }
            for (var i = 0; i < assignment.conditions.Length; i++)
                if (assignment.conditions[i].status == PilotRunStatus.Assigned) return Prepare(i, false, out error);
            error = "pilot_no_remaining_condition"; return false;
        }
        public bool RetryCurrent(out string error)
        {
            if (current == null) { error = "pilot_condition_missing"; return false; }
            return Prepare(current.conditionPosition, true, out error);
        }
        public void ResetSession()
        {
            presenter?.ResetSession(); questionnaire.Reset(); goals.ResetGoals(null);
            current=null; PilotRunId=""; QuestionnaireLinkageKey="";
            userSpeechEndedMs=feedbackStartedMs=feedbackEndedMs=dialogueStartedMs=-1; feedbackStartedAt=feedbackEndedAt="";
        }
        private bool Invalid(string stage,string reason){if(current!=null)current.status=PilotRunStatus.TechnicalInvalid;presenter?.ResetSession();Write("PilotTechnicalInvalid",stage,reason,ExperimentTechnicalValidity.TechnicalInvalid);return false;}
        public static string BuildCorrectionPlannerContext(PilotFeedbackStyleChoice style)=>"feedback_style="+PilotProtocolValues.Label(style)+"; use the shared correction planner";
        public void RecordFeedback(string text,bool started){Write(started?"PilotFeedbackPlaybackStarted":"PilotFeedbackPlaybackEnded",feedbackHash:ExperimentEventTimeline.HashText(text));}
        public void ObserveTimingEvent(ExperimentTimingEvent timingEvent)
        {
            if (!HasActivePilotRun || timingEvent == null) return;
            if (timingEvent.eventType == ExperimentTimingEventType.UserSpeechEnded.ToString()) userSpeechEndedMs = timingEvent.monotonicElapsedMs;
            else if (timingEvent.eventType == ExperimentTimingEventType.CorrectionPlaybackStarted.ToString()) { feedbackStartedMs = timingEvent.monotonicElapsedMs; feedbackStartedAt = timingEvent.timestampUtc; }
            else if (timingEvent.eventType == ExperimentTimingEventType.CorrectionPlaybackEnded.ToString()) { feedbackEndedMs = timingEvent.monotonicElapsedMs; feedbackEndedAt = timingEvent.timestampUtc; }
            else if (timingEvent.eventType == ExperimentTimingEventType.DialoguePlaybackStarted.ToString()) dialogueStartedMs = timingEvent.monotonicElapsedMs;
            if (timingEvent.eventType == ExperimentTimingEventType.UserSpeechEnded.ToString() || timingEvent.eventType == ExperimentTimingEventType.CorrectionPlaybackStarted.ToString() || timingEvent.eventType == ExperimentTimingEventType.CorrectionPlaybackEnded.ToString() || timingEvent.eventType == ExperimentTimingEventType.DialoguePlaybackStarted.ToString())
                Write("PilotTimingLinked:" + timingEvent.eventType, feedbackHash:timingEvent.feedbackTextHash);
        }
        private void Write(string type,string stage="",string reason="",ExperimentTechnicalValidity validity=ExperimentTechnicalValidity.Valid,string feedbackHash="")
        {
            if(assignment==null)return;var p=current==null?null:conditionManager.PilotPresentationCatalog?.Find(current.embodimentCondition);var now=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);var r=new PilotEventRecord{timestampUtc=now,eventType=type,pilotProtocolVersion=assignment.pilotProtocolVersion,pilotAssignmentVersion=assignment.pilotAssignmentVersion,pilotRunId=PilotRunId??"",participantId=assignment.participantId,sessionId=assignment.sessionId,sequenceId=assignment.sequenceId,conditionPosition=current?.conditionPosition??-1,embodimentCondition=current==null?"":PilotProtocolValues.Label(current.embodimentCondition),pilotFeedbackStyle=PilotProtocolValues.Label(assignment.feedbackStyle),taskId=current?.task?.taskId??"",taskAssignmentId=current?.task?.taskAssignmentId??"",feedbackTextHash=feedbackHash,actualPlaybackActor=p?.feedbackActor??"",visualEntityType=p==null?"":PilotProtocolValues.Label(current.embodimentCondition),visualPrefabKey=p?.visualPrefabKey??"",voiceProfileKey=p?.voiceProfileKey??"",audioSourcePolicy=PilotProtocolValues.Label(current?.embodimentCondition==PilotEmbodimentCondition.VoiceOnly?assignment.voiceOnlyAudioPolicy:p?.audioSourcePolicy??PilotAudioSourcePolicy.Undefined),spatialBlend=p?.spatialBlend??0,sourcePosition=p?.sourcePosition??Vector3.zero,feedbackPlaybackStartedAt=feedbackStartedAt,feedbackPlaybackEndedAt=feedbackEndedAt,userEndToFeedbackAudioMs=userSpeechEndedMs>=0&&feedbackStartedMs>=0?feedbackStartedMs-userSpeechEndedMs:-1,feedbackToDialogueGapMs=feedbackEndedMs>=0&&dialogueStartedMs>=0?Math.Max(0,dialogueStartedMs-feedbackEndedMs):-1,technicalValidity=validity.ToString(),failureStage=stage,failureReason=reason,questionnaireLinkageKey=QuestionnaireLinkageKey??""};var folder=Path.Combine(Application.persistentDataPath,"SceneTalkVR","ExperimentLogs");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,$"{assignment.participantId}_{assignment.sessionId}_pilot_events_v1.jsonl"),JsonUtility.ToJson(r)+Environment.NewLine,Encoding.UTF8);
        }
    }
}
