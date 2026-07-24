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
        public string schemaVersion="1.1";public string timestampUtc;public string eventType;public string pilotProtocolVersion;public string pilotAssignmentVersion;public string pilotRunId;public string participantId;public string sessionId;public string sequenceId;public int conditionPosition;public string embodimentCondition;public string pilotFeedbackStyle;public string taskId;public string taskAssignmentId;public string feedbackTextHash;public string actualPlaybackActor;public string visualEntityType;public string visualPrefabKey;public string voiceProfileKey;public string audioSourcePolicy;public float spatialBlend;public Vector3 sourcePosition;public string feedbackPlaybackStartedAt;public string feedbackPlaybackEndedAt;public long userEndToFeedbackAudioMs=-1;public long feedbackToDialogueGapMs=-1;public string technicalValidity;public string failureStage;public string failureReason;public string questionnaireLinkageKey;public string runtimeMode;public string dataOrigin;public bool collectionEligible;public bool developerTestAssignment;public bool demoMode;public string demoProtocolVersion;public bool autoFilledForDemo;public string flowMode;public string runQualification;public string protocolSnapshotId;public string resourceSnapshotId;public string goalId;public string turnId;public bool achieved;public float confidence;public string evaluatorSource;public long evaluatorLatencyMs;public string evaluatorVersion;public string evaluationReason;
    }

    [DisallowMultipleComponent]
    public sealed class PilotWorkflowCoordinator : MonoBehaviour, ISceneTalkSessionReset
    {
        public static PilotWorkflowCoordinator Active {get;private set;}
        [SerializeField] private ExperimentConditionManager conditionManager; [SerializeField] private PilotEmbodimentPresenter presenter;
        private readonly GoalProgressTracker goals=new GoalProgressTracker(); private readonly QuestionnaireSessionService questionnaire=new QuestionnaireSessionService();
        private PilotAssignment assignment;private PilotConditionAssignment current;
        private int maxTurns; private float maxDurationMinutes; private DateTime conditionStartedUtc; private int conditionStartTurn;
        private long userSpeechEndedMs=-1,feedbackStartedMs=-1,feedbackEndedMs=-1,dialogueStartedMs=-1; private string feedbackStartedAt="",feedbackEndedAt="";
        public PilotAssignment Assignment=>assignment;public PilotConditionAssignment Current=>current;public GoalProgressTracker Goals=>goals;public QuestionnaireSessionService Questionnaire=>questionnaire;
        public bool HasActivePilotRun => assignment != null && current != null && !string.IsNullOrWhiteSpace(PilotRunId);
        public string PilotRunId{get;private set;}public string QuestionnaireLinkageKey{get;private set;}public PilotEmbodimentCondition CurrentEmbodiment=>current?.embodimentCondition??PilotEmbodimentCondition.VoiceOnly;
        public int MaximumTurns=>maxTurns; public float MaximumDurationMinutes=>maxDurationMinutes;
        public event Action<PilotRunStatus> RunStatusChanged;
        public bool ShouldEndCurrentTask(out string reason)
        {
            if(current==null||current.status!=PilotRunStatus.Running){reason="";return false;}
            var turns=conditionManager==null?0:Mathf.Max(0,conditionManager.CurrentTurnIndex-conditionStartTurn);
            if(maxTurns>0&&turns>=maxTurns){reason="max_turns";return true;}
            if(maxDurationMinutes>0f&&conditionStartedUtc!=default&&(DateTime.UtcNow-conditionStartedUtc).TotalMinutes>=maxDurationMinutes){reason="max_duration";return true;}
            reason="";return false;
        }
        private void Awake(){if(conditionManager==null)conditionManager=GetComponent<ExperimentConditionManager>();if(presenter==null)presenter=GetComponent<PilotEmbodimentPresenter>()??gameObject.AddComponent<PilotEmbodimentPresenter>();Active=this;}
        private void OnDestroy(){if(Active==this)Active=null;}
        public void Configure(ExperimentConditionManager manager,PilotEmbodimentPresenter target=null){conditionManager=manager;if(target!=null)presenter=target;else if(presenter==null)presenter=GetComponent<PilotEmbodimentPresenter>()??gameObject.AddComponent<PilotEmbodimentPresenter>();Active=this;}
        public void ConfigureRunLimits(int turns,float durationMinutes){maxTurns=Mathf.Max(0,turns);maxDurationMinutes=Mathf.Max(0f,durationMinutes);}
        public bool LoadAssignment(PilotAssignment value,out string error){if(value==null){error="pilot_assignment_missing";return false;}if(conditionManager==null){error="condition_manager_missing";return false;}var rehearsal=value.runQualification==ExperimentRunQualification.Rehearsal;if(!ExperimentRuntimeContext.IsAllowed(value.flowMode,value.runQualification)){error="runtime_context_combination_invalid";return false;}if(rehearsal&&(value.flowMode!=ExperimentFlowMode.Pilot||value.dataOrigin!="rehearsal"||value.collectionEligible||value.developerTestAssignment||value.demoMode)){error="pilot_rehearsal_isolation_invalid";return false;}if(value.demoMode&&(value.runtimeMode!=ExperimentRuntimeMode.EditorDemoPilot||value.dataOrigin!="editor_demo"||value.collectionEligible||!value.developerTestAssignment)){error="editor_demo_pilot_isolation_invalid";return false;}if(value.runQualification==ExperimentRunQualification.Collection&&(value.flowMode!=ExperimentFlowMode.Pilot||value.runtimeMode!=ExperimentRuntimeMode.EditorCollectionPilot||value.dataOrigin!="participant_collection"||!value.collectionEligible||value.developerTestAssignment||value.demoMode)){error="pilot_collection_isolation_invalid";return false;}var expected=rehearsal||value.demoMode?value.pilotProtocolVersion:conditionManager.ExperimentProtocol?.ProtocolVersion??value.pilotProtocolVersion;if(!PilotAssignmentAllocator.IsCompatible(value,expected,conditionManager.TaskCatalog?.CatalogVersion??value.taskCatalogVersion,out error))return false;assignment=value;Write("PilotAssignmentLoaded");return true;}
        public bool CreateLocked(string participant,string session,out string error){var allocator=new PilotAssignmentAllocator();if(!allocator.TryCreateLocked(participant,session,conditionManager.ExperimentProtocol,conditionManager.TaskCatalog,conditionManager.PilotPresentationCatalog,out var value,out error))return false;return LoadAssignment(value,out error);}
        public bool Prepare(int position,bool retry,out string error)
        {
            error="";if(assignment?.conditions==null||position<0||position>=assignment.conditions.Length){error="pilot_condition_missing";return false;}var next=assignment.conditions[position];if(next.status==PilotRunStatus.Completed){error="pilot_condition_completed";return false;}if(next.status==PilotRunStatus.TechnicalInvalid&&!retry){error="pilot_retry_requires_authorization";return false;}
            conditionManager.ResetConditionSessionBoundary();presenter.ResetSession();questionnaire.Reset();goals.ResetGoals(null);userSpeechEndedMs=feedbackStartedMs=feedbackEndedMs=dialogueStartedMs=-1;feedbackStartedAt=feedbackEndedAt="";current=next;current.runAttempt++;PilotRunId=$"pr-{assignment.assignmentSeed}-{position}-{current.runAttempt}-{Guid.NewGuid():N}";QuestionnaireLinkageKey=$"pql-{PilotRunId}";current.latestPilotRunId=PilotRunId;current.status=PilotRunStatus.Preparing;Write("PilotConditionPrepared");
            var profile=assignment.runQualification==ExperimentRunQualification.Rehearsal&&RehearsalSessionCoordinator.Active!=null?RehearsalSessionCoordinator.Active.ResolvePilotProfile(current.embodimentCondition):assignment.demoMode&&EditorDemoSessionCoordinator.Active!=null?EditorDemoSessionCoordinator.Active.ResolvePilotProfile(current.embodimentCondition):conditionManager.PilotPresentationCatalog?.Find(current.embodimentCondition);if(profile==null){error="pilot_profile_missing";return Invalid("Presentation",error);}
            var locked=!assignment.developerTestAssignment;if(!presenter.Configure(profile,assignment.voiceOnlyAudioPolicy,locked,out error))return Invalid("Presentation",error);
            if(!conditionManager.ApplyPilotAssignment(assignment.feedbackStyle,current.task.taskId,assignment.participantId,assignment.sessionId,out error))return Invalid("Task",error);
            if(!conditionManager.SetExperimentAssistantEmbodiment(PilotEmbodimentPresenter.AppearanceIdFor(current.embodimentCondition)))
            {error="pilot_assistant_embodiment_override_invalid";return Invalid("Presentation",error);}
            goals.ResetGoals(conditionManager.TaskCatalog.Find(current.task.taskId),new GoalTrackingContext{participantId=assignment.participantId,sessionId=assignment.sessionId,conditionRunId=PilotRunId,taskAssignmentId=current.task.taskAssignmentId,taskId=current.task.taskId,confirmationPolicy=GoalConfirmationPolicy.AutomaticOnValidatedDetection});conditionStartedUtc=DateTime.UtcNow;conditionStartTurn=conditionManager.CurrentTurnIndex;current.status=PilotRunStatus.Running;Write("PilotConditionStarted");RunStatusChanged?.Invoke(current.status);return true;
        }
        public void CompleteTask(){if(current==null||current.status!=PilotRunStatus.Running)return;current.status=PilotRunStatus.TaskCompleted;Write("PilotTaskCompleted");current.status=PilotRunStatus.AwaitingPilotQuestionnaire;Write("PilotAwaitingQuestionnaire");RunStatusChanged?.Invoke(current.status);}
        public bool BeginQuestionnaire(out string error)
        {
            if(current==null||current.status!=PilotRunStatus.AwaitingPilotQuestionnaire){error="pilot_not_awaiting_questionnaire";return false;}var def=conditionManager.QuestionnaireCatalog?.Find("pilot_condition_v1");var context=new QuestionnaireSession{protocolVersion=assignment.pilotProtocolVersion,questionnaireCatalogVersion=conditionManager.QuestionnaireCatalog.CatalogVersion,participantId=assignment.participantId,sessionId=assignment.sessionId,sequenceId=assignment.sequenceId,conditionRunId=PilotRunId,questionnaireLinkageKey=QuestionnaireLinkageKey,conditionPosition=current.conditionPosition,embodimentCondition=PilotProtocolValues.Label(current.embodimentCondition),taskId=current.task.taskId,taskAssignmentId=current.task.taskAssignmentId,technicalValidity=ExperimentTechnicalValidity.Valid,conditionStatus=ConditionRunStatus.QuestionnaireInProgress,runtimeMode=assignment.runtimeMode.ToString(),dataOrigin=assignment.dataOrigin,collectionEligible=assignment.collectionEligible,developerTestAssignment=assignment.developerTestAssignment,demoMode=assignment.demoMode,demoProtocolVersion=assignment.demoProtocolVersion};questionnaire.Configure(conditionManager.QuestionnaireCatalog,conditionManager.ExperimentProtocol);if(!questionnaire.Begin(def,context,out error))return false;current.status=PilotRunStatus.PilotQuestionnaireInProgress;Write("PilotQuestionnaireStarted");RunStatusChanged?.Invoke(current.status);return true;
        }
        public bool SubmitQuestionnaire(out string error){if(current==null||current.status!=PilotRunStatus.PilotQuestionnaireInProgress){error="pilot_questionnaire_not_in_progress";return false;}if(!questionnaire.Submit(PilotCollectionSessionCoordinator.Active?.CurrentDataFolder??QuestionnaireSessionService.DefaultFolder,out error))return false;current.status=PilotRunStatus.PilotQuestionnaireSubmitted;Write("PilotQuestionnaireSubmitted");current.status=PilotRunStatus.Completed;presenter.ResetSession();Write("PilotConditionCompleted");RunStatusChanged?.Invoke(current.status);return true;}
        public bool SubmitFinalRanking(PreferenceRankingResponse ranking,out string error)
        {
            if(assignment?.conditions==null||Array.Exists(assignment.conditions,x=>x.status!=PilotRunStatus.Completed)){error="pilot_final_ranking_requires_three_valid_conditions";return false;}
            if(ranking==null){error="pilot_ranking_missing";return false;}
            var labels=new[]{"voice_only","floating_orb","humanoid_agent"};
            if(!ranking.ValidateUnique(labels,out error))return false;
            if(!Array.Exists(labels,x=>x==ranking.preferredEmbodimentCondition)){error="pilot_preferred_embodiment_missing_or_invalid";return false;}
            if(string.IsNullOrWhiteSpace(ranking.reason)){error="pilot_ranking_reason_required";return false;}
            QuestionnaireResearchExporter.AppendRanking(PilotCollectionSessionCoordinator.Active?.CurrentDataFolder??QuestionnaireSessionService.DefaultFolder,ranking);Write("PilotFinalRankingSubmitted");error="";return true;
        }
        public void MarkTechnicalInvalid(string stage,string reason){Invalid(stage,reason);RunStatusChanged?.Invoke(PilotRunStatus.TechnicalInvalid);}
        public void AbortCurrent(string reason)
        {
            if(current==null||current.status==PilotRunStatus.Completed||current.status==PilotRunStatus.TechnicalInvalid||current.status==PilotRunStatus.Aborted)return;
            current.status=PilotRunStatus.Aborted;presenter?.ResetSession();questionnaire.Reset();
            Write("PilotConditionAborted","ParticipantExit",string.IsNullOrWhiteSpace(reason)?"participant_exit":reason);
            RunStatusChanged?.Invoke(current.status);
        }
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
            current=null; PilotRunId=""; QuestionnaireLinkageKey=""; conditionStartedUtc=default; conditionStartTurn=0;
            userSpeechEndedMs=feedbackStartedMs=feedbackEndedMs=dialogueStartedMs=-1; feedbackStartedAt=feedbackEndedAt="";
        }
        public void ClearAssignmentForRuntimeMode(){ResetSession();assignment=null;}
        public void ResetPilotConditionBoundary(){if(current!=null)Write("PilotConditionBoundaryReset");conditionManager?.ResetConditionSessionBoundary();presenter?.ResetSession();questionnaire.Reset();goals.ResetGoals(null);userSpeechEndedMs=feedbackStartedMs=feedbackEndedMs=dialogueStartedMs=-1;feedbackStartedAt=feedbackEndedAt="";}
        private bool Invalid(string stage,string reason){if(current!=null)current.status=PilotRunStatus.TechnicalInvalid;presenter?.ResetSession();Write("PilotTechnicalInvalid",stage,reason,ExperimentTechnicalValidity.TechnicalInvalid);return false;}
        public static string BuildCorrectionPlannerContext(PilotFeedbackStyleChoice style)=>"feedback_style="+PilotProtocolValues.Label(style)+"; use the shared correction planner";
        public void RecordFeedback(string text,bool started){Write(started?"PilotFeedbackPlaybackStarted":"PilotFeedbackPlaybackEnded",feedbackHash:ExperimentEventTimeline.HashText(text));}
        public void RecordGoalEvaluationAudit(string turnId,GoalEvaluationAudit audit)
        {
            if(audit==null)return;
            Write(audit.eventType,reason:audit.error,goalId:audit.goalId,turnId:turnId,achieved:audit.achieved,
                confidence:audit.confidence,evaluatorSource:GoalEvaluationOrchestrator.SourceLabel(audit.source),
                evaluatorLatencyMs:audit.latencyMs,evaluatorVersion:audit.evaluatorVersion,evaluationReason:audit.reason);
        }
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
        private void Write(string type,string stage="",string reason="",ExperimentTechnicalValidity validity=ExperimentTechnicalValidity.Valid,string feedbackHash="",
            string goalId="",string turnId="",bool achieved=false,float confidence=0f,string evaluatorSource="",long evaluatorLatencyMs=0,
            string evaluatorVersion="",string evaluationReason="")
        {
            if (assignment == null) return;
            var p = current == null ? null : assignment.runQualification == ExperimentRunQualification.Rehearsal && RehearsalSessionCoordinator.Active != null
                ? RehearsalSessionCoordinator.Active.ResolvePilotProfile(current.embodimentCondition)
                : assignment.demoMode && EditorDemoSessionCoordinator.Active != null
                ? EditorDemoSessionCoordinator.Active.ResolvePilotProfile(current.embodimentCondition)
                : conditionManager.PilotPresentationCatalog?.Find(current.embodimentCondition);
            var r = new PilotEventRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), eventType = type,
                pilotProtocolVersion = assignment.pilotProtocolVersion, pilotAssignmentVersion = assignment.pilotAssignmentVersion,
                pilotRunId = PilotRunId ?? "", participantId = assignment.participantId, sessionId = assignment.sessionId,
                sequenceId = assignment.sequenceId, conditionPosition = current?.conditionPosition ?? -1,
                embodimentCondition = current == null ? "" : PilotProtocolValues.Label(current.embodimentCondition),
                pilotFeedbackStyle = PilotProtocolValues.Label(assignment.feedbackStyle), taskId = current?.task?.taskId ?? "",
                taskAssignmentId = current?.task?.taskAssignmentId ?? "", feedbackTextHash = feedbackHash,
                actualPlaybackActor = p?.feedbackActor ?? "", visualEntityType = p == null ? "" : PilotProtocolValues.Label(current.embodimentCondition),
                visualPrefabKey = p?.visualPrefabKey ?? "", voiceProfileKey = p?.voiceProfileKey ?? "",
                audioSourcePolicy = PilotProtocolValues.Label(current?.embodimentCondition == PilotEmbodimentCondition.VoiceOnly ? assignment.voiceOnlyAudioPolicy : p?.audioSourcePolicy ?? PilotAudioSourcePolicy.Undefined),
                spatialBlend = p?.spatialBlend ?? 0, sourcePosition = p?.sourcePosition ?? Vector3.zero,
                feedbackPlaybackStartedAt = feedbackStartedAt, feedbackPlaybackEndedAt = feedbackEndedAt,
                userEndToFeedbackAudioMs = userSpeechEndedMs >= 0 && feedbackStartedMs >= 0 ? feedbackStartedMs - userSpeechEndedMs : -1,
                feedbackToDialogueGapMs = feedbackEndedMs >= 0 && dialogueStartedMs >= 0 ? Math.Max(0, dialogueStartedMs - feedbackEndedMs) : -1,
                technicalValidity = validity.ToString(), failureStage = stage, failureReason = reason,
                questionnaireLinkageKey = QuestionnaireLinkageKey ?? "", runtimeMode = assignment.runtimeMode.ToString(),
                dataOrigin = assignment.dataOrigin, collectionEligible = assignment.collectionEligible,
                developerTestAssignment = assignment.developerTestAssignment, demoMode = assignment.demoMode,
                demoProtocolVersion = assignment.demoProtocolVersion,
                flowMode = assignment.flowMode.ToString(), runQualification = assignment.runQualification.ToString(),
                protocolSnapshotId = assignment.protocolSnapshotId, resourceSnapshotId = assignment.resourceSnapshotId,
                goalId=goalId??"",turnId=turnId??"",achieved=achieved,confidence=confidence,evaluatorSource=evaluatorSource??"",
                evaluatorLatencyMs=evaluatorLatencyMs,evaluatorVersion=evaluatorVersion??"",evaluationReason=evaluationReason??"",
                autoFilledForDemo = assignment.demoMode && (reason??string.Empty).IndexOf("autoFilledForDemo=true", StringComparison.Ordinal) >= 0
            };
            var folder = assignment.runQualification == ExperimentRunQualification.Rehearsal && RehearsalSessionCoordinator.Active != null
                ? RehearsalSessionCoordinator.Active.CurrentDataFolder
                : assignment.demoMode && EditorDemoSessionCoordinator.Active != null
                ? EditorDemoSessionCoordinator.Active.CurrentDataFolder
                : PilotCollectionSessionCoordinator.Active?.IsArmed==true?PilotCollectionSessionCoordinator.Active.CurrentDataFolder
                : Path.Combine(Application.persistentDataPath, "SceneTalkVR", "ExperimentLogs");
            Directory.CreateDirectory(folder);
            var fileName = assignment.runQualification == ExperimentRunQualification.Rehearsal
                || PilotCollectionSessionCoordinator.Active?.IsArmed == true ? "pilot_events_v1.jsonl"
                : $"{assignment.participantId}_{assignment.sessionId}_pilot_events_v1.jsonl";
            File.AppendAllText(Path.Combine(folder, fileName), JsonUtility.ToJson(r) + Environment.NewLine, Encoding.UTF8);
        }
    }
}
