using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum PilotParticipantStage { None, Setup, Instructions, TaskIntroduction, Dialogue, Questionnaire, Transition, FinalRanking, Completion }

    [Serializable]
    public sealed class PilotCollectionOperatorEvent
    {
        public string schemaVersion="1.0"; public string timestampUtc; public string eventType;
        public string participantId; public string sessionId; public string pilotRunId; public string sequenceId;
        public int conditionPosition=-1; public string embodiment; public string taskId; public string detail;
        public string flowMode="pilot"; public string runQualification="collection";
        public string dataOrigin="participant_collection"; public bool collectionEligible=true;
        public bool developerTestAssignment; public bool synthetic; public bool demoMode;
        public string deploymentProfile="editor_collection"; public bool qaAutomationUsed; public string actor="experiment_operator";
    }

    [DisallowMultipleComponent]
    public sealed class PilotCollectionSessionCoordinator : MonoBehaviour
    {
        public static PilotCollectionSessionCoordinator Active { get; private set; }
        private ExperimentConditionManager manager; private PilotWorkflowCoordinator workflow;
        private SceneTalkOrchestrator orchestrator; private int currentPosition=-1; private bool subscribed;
        private bool rankingSubmitted; private string lastBundlePath;
        private bool questionnaireTransitionPending;
        private PreferenceRankingResponse qaRankingDraft;
        private const string LastParticipantKey="SceneTalkVR.Pilot.LastParticipantId";
        private const string LastSessionKey="SceneTalkVR.Pilot.LastSessionId";

        public ExperimentRuntimeContext RuntimeContext { get; private set; }
        public PilotParticipantStage Stage { get; private set; }
        public bool IsArmed => RuntimeContext?.flowMode==ExperimentFlowMode.Pilot && RuntimeContext.IsCollection;
        public PilotAssignment Assignment => workflow?.Assignment;
        public PilotWorkflowCoordinator Workflow => workflow;
        public string ParticipantId => RuntimeContext?.participantId??string.Empty;
        public string SessionId => RuntimeContext?.sessionId??string.Empty;
        public int CurrentPosition => currentPosition;
        public ExperimentTaskDefinition CurrentTask { get { var taskId=workflow?.Current?.task?.taskId;if(string.IsNullOrWhiteSpace(taskId)&&Assignment?.conditions!=null&&currentPosition>=0&&currentPosition<Assignment.conditions.Length)taskId=Assignment.conditions[currentPosition].task?.taskId;return manager?.TaskCatalog?.Find(taskId); } }
        public string LastBundlePath => lastBundlePath??string.Empty;
        public static string CollectionRoot => Path.Combine(Application.persistentDataPath,"SceneTalkVR","PilotCollectionSessions");
        public string CurrentDataFolder => string.IsNullOrWhiteSpace(ParticipantId)||string.IsNullOrWhiteSpace(SessionId)
            ?CollectionRoot:Path.Combine(CollectionRoot,Safe(ParticipantId)+"_"+Safe(SessionId),"raw");

        private void Awake(){Active=this;Resolve();Subscribe();}
        private void OnDestroy(){Unsubscribe();if(Active==this)Active=null;}
        public void Configure(ExperimentConditionManager value,SceneTalkOrchestrator sceneOrchestrator){manager=value;orchestrator=sceneOrchestrator;Resolve();Subscribe();}

        public void OpenSetup(){if(IsArmed)EndSession();Stage=PilotParticipantStage.Setup;RefreshUi();}
        public bool OpenOrCreateAutomaticParticipantSession(out string error)
        {
            if(IsArmed){if(Stage==PilotParticipantStage.None||Stage==PilotParticipantStage.Setup)Stage=PilotParticipantStage.Instructions;RefreshUi();error="";return true;}
            var previousParticipant=PlayerPrefs.GetString(LastParticipantKey,string.Empty);
            var previousSession=PlayerPrefs.GetString(LastSessionKey,string.Empty);
            if(!string.IsNullOrWhiteSpace(previousParticipant)&&!string.IsNullOrWhiteSpace(previousSession)
                && File.Exists(Path.Combine(CollectionRoot,Safe(previousParticipant)+"_"+Safe(previousSession),"raw","pilot_assignment.json"))
                && ResumeSession(previousParticipant,previousSession,out error))return true;
            var timestamp=DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");var suffix=Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();
            var participantId=$"PILOT-P-{timestamp}-{suffix}";var sessionId=$"PILOT-S-{timestamp}-{suffix}";
            if(!CreateSession(participantId,sessionId,out error))return false;
            PlayerPrefs.SetString(LastParticipantKey,participantId);PlayerPrefs.SetString(LastSessionKey,sessionId);PlayerPrefs.Save();return true;
        }
        public bool CreateSession(string participantId,string sessionId,out string error)
            => Arm(participantId,sessionId,false,out error);
        public bool ResumeSession(string participantId,string sessionId,out string error)
            => Arm(participantId,sessionId,true,out error);

        private bool Arm(string participantId,string sessionId,bool resume,out string error)
        {
            Resolve();Subscribe();participantId=participantId?.Trim();sessionId=sessionId?.Trim();
            if(!Application.isEditor){error="pilot_collection_requires_unity_editor";return false;}
            if(!ValidIdentity(participantId,out error))return false;
            if(string.IsNullOrWhiteSpace(sessionId))sessionId=$"PILOT-{participantId}-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
            if(!ValidIdentity(sessionId,out error))return false;
            if(EditorCollectionSessionCoordinator.Active?.IsArmed==true||RehearsalSessionCoordinator.Active?.IsActive==true||EditorDemoSessionCoordinator.Active?.IsDemoMode==true){error="another_experiment_runtime_is_active";return false;}
            if(manager==null||workflow==null||orchestrator==null){error="pilot_collection_scene_bindings_missing";return false;}
            var protocol=manager.ExperimentProtocol;var tasks=manager.TaskCatalog;var presentations=manager.PilotPresentationCatalog;
            if(protocol==null||!protocol.ValidateForFormalMode(out error))return false;
            if(!protocol.TryResolvePilotDecisions(out var style,out var audio,out error)||style!=PilotFeedbackStyleChoice.Explicit||audio!=PilotAudioSourcePolicy.NonSpatialHeadLocked)return false;
            if(presentations==null||!presentations.ValidateLocked(protocol,out error))return false;
            if(!ExperimentTaskCatalog.ValidatePilotTasks(tasks?.GetTasks(ExperimentTaskPhase.Pilot).ToArray(),out error))return false;
            ResetRuntime();RuntimeContext=ExperimentRuntimeContext.CreatePilotEditorCollection(participantId,sessionId,protocol.ProtocolSnapshotId,"pilot-resources-"+presentations.CatalogVersion);
            Directory.CreateDirectory(CurrentDataFolder);
            var stored=PilotAssignmentAllocator.Load(AssignmentPath);
            if(resume)
            {
                if(stored==null){error="pilot_assignment_missing";return FailArm();}
                if(!ValidateAssignment(stored,out error)||!workflow.LoadAssignment(stored,out error))return FailArm();
                currentPosition=Array.FindIndex(stored.conditions,x=>x.status==PilotRunStatus.Running||x.status==PilotRunStatus.AwaitingPilotQuestionnaire||x.status==PilotRunStatus.PilotQuestionnaireInProgress||x.status==PilotRunStatus.TechnicalInvalid);
                if(currentPosition>=0&&stored.conditions[currentPosition].status!=PilotRunStatus.TechnicalInvalid){stored.conditions[currentPosition].status=PilotRunStatus.TechnicalInvalid;Persist();Write("PilotIncompleteAttemptPreservedForResumeRetry");}
                rankingSubmitted=File.Exists(RankingSubmittedMarkerPath);
                Stage=stored.conditions.All(x=>x.status==PilotRunStatus.Completed)
                    ?(rankingSubmitted?PilotParticipantStage.Completion:PilotParticipantStage.FinalRanking)
                    :currentPosition>=0?PilotParticipantStage.TaskIntroduction:PilotParticipantStage.Instructions;
            }
            else
            {
                if(stored!=null){error="session_already_exists_use_resume";return FailArm();}
                var allocator=new PilotAssignmentAllocator();
                if(!allocator.TryCreateCollection(participantId,sessionId,protocol,tasks,presentations,RuntimeContext.resourceSnapshotId,out var created,out error)||!workflow.LoadAssignment(created,out error))return FailArm();
                PilotAssignmentAllocator.Save(created,AssignmentPath);currentPosition=-1;Stage=PilotParticipantStage.Instructions;
            }
            workflow.ConfigureRunLimits(5,8f);Write("PilotSessionArmed","resume="+resume);RefreshUi();error="";return true;
        }

        public void BeginPilot(){if(!IsArmed||Stage!=PilotParticipantStage.Instructions)return;currentPosition=NextAssigned();Stage=currentPosition>=0?PilotParticipantStage.TaskIntroduction:PilotParticipantStage.FinalRanking;Write("PilotInstructionsCompleted");RefreshUi();}
        public bool BeginCurrentTask(out string error)
        {
            if(!IsArmed||Stage!=PilotParticipantStage.TaskIntroduction||currentPosition<0){error="pilot_task_introduction_not_active";return false;}
            var retry=Assignment.conditions[currentPosition].status==PilotRunStatus.TechnicalInvalid;
            if(!workflow.Prepare(currentPosition,retry,out error))return false;
            Persist();Stage=PilotParticipantStage.Dialogue;Write(retry?"PilotConditionRetryStarted":"PilotConditionStarted");orchestrator.LoadAssignedTask(workflow.Current.task.taskId);RefreshUi();return true;
        }
        public bool IsTaskPrepared(string taskId)=>IsArmed&&Stage==PilotParticipantStage.Dialogue&&workflow?.Current?.status==PilotRunStatus.Running&&string.Equals(workflow.Current.task?.taskId,taskId,StringComparison.OrdinalIgnoreCase);
        public bool SubmitQuestionnaire(out string error)
        {
            if(Stage!=PilotParticipantStage.Questionnaire){error="pilot_questionnaire_not_visible";return false;}
            if(!workflow.SubmitQuestionnaire(out error))return false;Persist();
            if(Assignment.conditions.All(x=>x.status==PilotRunStatus.Completed)){workflow.ResetPilotConditionBoundary();Stage=PilotParticipantStage.FinalRanking;}
            else Stage=PilotParticipantStage.Transition;
            Write("PilotQuestionnaireSubmitted");RefreshUi();return true;
        }
        public void ContinueAfterTransition(){if(Stage!=PilotParticipantStage.Transition)return;workflow.ResetPilotConditionBoundary();currentPosition=NextAssigned();Stage=currentPosition>=0?PilotParticipantStage.TaskIntroduction:PilotParticipantStage.FinalRanking;Write("PilotNeutralTransitionCompleted");RefreshUi();}
        public bool SubmitFinalRanking(PreferenceRankingResponse value,out string error)
        {
            if(Stage!=PilotParticipantStage.FinalRanking){error="pilot_final_ranking_not_visible";return false;}
            value.protocolVersion=Assignment.pilotProtocolVersion;value.questionnaireCatalogVersion=manager.QuestionnaireCatalog.CatalogVersion;
            value.participantId=ParticipantId;value.sessionId=SessionId;value.sequenceId=Assignment.sequenceId;value.questionnaireId="pilot_final_v1";value.submittedAtUtc=DateTime.UtcNow.ToString("o");
            if(!workflow.SubmitFinalRanking(value,out error))return false;rankingSubmitted=true;File.WriteAllText(RankingSubmittedMarkerPath,value.submittedAtUtc,Encoding.UTF8);Stage=PilotParticipantStage.Completion;Write("PilotExperimentCompleted");RefreshUi();return true;
        }
        public bool ExportBundle(out string error){var ok=PilotCollectionBundleExporter.Export(Path.GetDirectoryName(CurrentDataFolder),Assignment,manager.ExperimentProtocol,manager.QuestionnaireCatalog,rankingSubmitted,out lastBundlePath,out error);if(ok)Write("PilotBundleExported",lastBundlePath);return ok;}
        public bool AuditBundle(out string error){if(string.IsNullOrWhiteSpace(lastBundlePath)||!Directory.Exists(lastBundlePath)){error="pilot_bundle_missing";return false;}var report=SessionDataIntegrityAuditor.Audit(lastBundlePath,ParticipantId,SessionId);SessionDataIntegrityAuditor.WriteReport(report,lastBundlePath+"-manual-audit.json");error=report.result.ToString().ToUpperInvariant();return report.result!=DataIntegritySeverity.Fail;}
        public void MarkTechnicalInvalid(string reason){if(!IsArmed)return;workflow.MarkTechnicalInvalid("experiment_operator",reason);Persist();Stage=PilotParticipantStage.TaskIntroduction;Write("PilotTechnicalInvalid",reason);RefreshUi();}
        public bool Retry(out string error){if(workflow?.Current?.status!=PilotRunStatus.TechnicalInvalid){error="pilot_retry_not_available";return false;}Stage=PilotParticipantStage.TaskIntroduction;return BeginCurrentTask(out error);}
        public void EndSession(){Write("PilotSessionEnded");ResetRuntime();RuntimeContext=null;Stage=PilotParticipantStage.None;RefreshUi();}

        public bool PrepareCurrentPilotConditionForQa(out string error)
        { if(Stage==PilotParticipantStage.Instructions)BeginPilot();if(Stage==PilotParticipantStage.Transition)ContinueAfterTransition();var ok=BeginCurrentTask(out error);if(ok)Write("QaPreparePilotCondition","",true);return ok; }
        public bool CompleteCurrentPilotGoalsForQa(out string error)
        {
            error="";if(!IsArmed||Stage!=PilotParticipantStage.Dialogue){error="pilot_dialogue_not_active";return false;}
            foreach(var goal in workflow.Goals.Goals.ToArray())
            { if((goal.state==GoalProgressState.NotStarted||goal.state==GoalProgressState.Rejected)&&!workflow.Goals.SubmitGoalCandidate(goal.goalId,"qa_operator",new GoalEvidence{turnId="qa",transcript="qaAutomationUsed=true"},out error))return false;if(goal.state==GoalProgressState.Candidate&&!workflow.Goals.ConfirmGoal(goal.goalId,"qa_operator","qaAutomationUsed=true",out error))return false; }
            Write("QaCompletePilotGoals","",true);return true;
        }
        public bool OpenPilotQuestionnaireForQa(out string error)
        { if(Stage==PilotParticipantStage.Dialogue)workflow.CompleteTask();if(workflow.Current?.status!=PilotRunStatus.AwaitingPilotQuestionnaire){error="pilot_not_awaiting_questionnaire";return false;}if(!workflow.BeginQuestionnaire(out error))return false;Stage=PilotParticipantStage.Questionnaire;Persist();Write("QaOpenPilotQuestionnaire","",true);RefreshUi();return true; }
        public bool AutoFillPilotQuestionnaireForQa(out string error)
        { error="";var service=workflow?.Questionnaire;if(Stage!=PilotParticipantStage.Questionnaire||service?.ActiveSession==null||service.Definition==null){error="pilot_questionnaire_not_visible";return false;}foreach(var item in manager.QuestionnaireCatalog.GetEnabledItems(service.Definition.questionnaireId,manager.ExperimentProtocol)){var raw=item.itemType==QuestionnaireItemType.Likert?Mathf.Clamp(5,item.scaleMin,item.scaleMax).ToString():item.choiceValues!=null&&item.choiceValues.Length>0?item.choiceValues[0]:"QA response";if(!service.SetResponse(item.itemId,raw,out error))return false;}Write("QaAutoFillPilotQuestionnaire","",true);return true; }
        public bool SubmitPilotQuestionnaireForQa(out string error){var ok=SubmitQuestionnaire(out error);if(ok)Write("QaSubmitPilotQuestionnaire","",true);return ok;}
        public bool PrepareNextPilotConditionForQa(out string error){if(Stage==PilotParticipantStage.Transition)ContinueAfterTransition();return PrepareCurrentPilotConditionForQa(out error);}
        public void MarkPilotTechnicalInvalidForQa(){MarkTechnicalInvalid("qa_operator_injected");Write("QaMarkPilotTechnicalInvalid","",true);}
        public bool RetryPilotConditionForQa(out string error){var ok=Retry(out error);if(ok)Write("QaRetryPilotCondition","",true);return ok;}
        public bool OpenPilotFinalRankingForQa(out string error){if(Assignment?.conditions==null||Assignment.conditions.Any(x=>x.status!=PilotRunStatus.Completed)){error="pilot_final_ranking_requires_three_valid_conditions";return false;}Stage=PilotParticipantStage.FinalRanking;Write("QaOpenPilotFinalRanking","",true);RefreshUi();error="";return true;}
        public bool AutoFillPilotRankingForQa(out string error){if(Stage!=PilotParticipantStage.FinalRanking){error="pilot_final_ranking_not_visible";return false;}qaRankingDraft=new PreferenceRankingResponse{rankings=new[]{new PreferenceRankEntry{embodimentCondition="voice_only",rank=1},new PreferenceRankEntry{embodimentCondition="floating_orb",rank=2},new PreferenceRankEntry{embodimentCondition="humanoid_agent",rank=3}},preferredEmbodimentCondition="voice_only",reason="QA operator ranking"};Write("QaAutoFillPilotRanking","",true);error="";return true;}
        public bool SubmitPilotRankingForQa(out string error){if(qaRankingDraft==null){error="pilot_qa_ranking_not_filled";return false;}var ok=SubmitFinalRanking(qaRankingDraft,out error);if(ok){Write("QaSubmitPilotRanking","",true);qaRankingDraft=null;}return ok;}
        public void ResetPilotSessionForQa(){Write("QaResetPilotSession","",true);EndSession();PlayerPrefs.DeleteKey(LastParticipantKey);PlayerPrefs.DeleteKey(LastSessionKey);PlayerPrefs.Save();}

        private void OnAllGoalsConfirmed(GoalProgressChangedEvent value)
        {
            if(!IsArmed||Stage!=PilotParticipantStage.Dialogue||questionnaireTransitionPending||value?.conditionRunId!=workflow.PilotRunId)return;
            questionnaireTransitionPending=true;orchestrator?.PauseForQuestionnaireBoundary();workflow.CompleteTask();Persist();Write("PilotAllGoalsConfirmed");StartCoroutine(OpenQuestionnaireAfterGoalDisplay());
        }
        private IEnumerator OpenQuestionnaireAfterGoalDisplay(){yield return new WaitForSecondsRealtime(.75f);questionnaireTransitionPending=false;if(!IsArmed||workflow?.Current?.status!=PilotRunStatus.AwaitingPilotQuestionnaire)yield break;if(!workflow.BeginQuestionnaire(out var error)){MarkTechnicalInvalid("questionnaire_start_failed:"+error);yield break;}Persist();Stage=PilotParticipantStage.Questionnaire;Write("PilotQuestionnaireOpened");RefreshUi();}
        private void OnGoalChanged(GoalProgressChangedEvent value){if(IsArmed&&value?.conditionRunId==workflow.PilotRunId){PersistGoals();RefreshUi();}}
        private void Resolve(){manager??=GetComponent<ExperimentConditionManager>()??FindFirstObjectByType<ExperimentConditionManager>();workflow??=GetComponent<PilotWorkflowCoordinator>()??FindFirstObjectByType<PilotWorkflowCoordinator>();orchestrator??=GetComponent<SceneTalkOrchestrator>()??FindFirstObjectByType<SceneTalkOrchestrator>();}
        private void Subscribe(){if(subscribed||workflow==null)return;workflow.Goals.OnAllGoalsConfirmed+=OnAllGoalsConfirmed;workflow.Goals.OnGoalProgressChanged+=OnGoalChanged;subscribed=true;}
        private void Unsubscribe(){if(!subscribed||workflow==null)return;workflow.Goals.OnAllGoalsConfirmed-=OnAllGoalsConfirmed;workflow.Goals.OnGoalProgressChanged-=OnGoalChanged;subscribed=false;}
        private int NextAssigned()=>Assignment?.conditions==null?-1:Array.FindIndex(Assignment.conditions,x=>x.status==PilotRunStatus.Assigned||x.status==PilotRunStatus.TechnicalInvalid);
        private string AssignmentPath=>Path.Combine(CurrentDataFolder,"pilot_assignment.json");
        private string RankingSubmittedMarkerPath=>Path.Combine(CurrentDataFolder,"pilot_ranking_submitted.marker");
        private void Persist(){if(IsArmed&&Assignment!=null)PilotAssignmentAllocator.Save(Assignment,AssignmentPath);}
        private void PersistGoals(){if(!IsArmed||string.IsNullOrWhiteSpace(workflow.PilotRunId)||workflow.Current==null)return;Directory.CreateDirectory(CurrentDataFolder);var file=$"pilot_goals_{workflow.Current.conditionPosition}_{workflow.Current.runAttempt}.json";File.WriteAllText(Path.Combine(CurrentDataFolder,file),JsonUtility.ToJson(new PilotGoalSnapshot{participantId=ParticipantId,sessionId=SessionId,pilotRunId=workflow.PilotRunId,taskId=workflow.Current.task.taskId,savedAtUtc=DateTime.UtcNow.ToString("o"),goals=workflow.Goals.Goals.ToArray()},true),Encoding.UTF8);}
        private bool ValidateAssignment(PilotAssignment value,out string error){if(value==null||value.flowMode!=ExperimentFlowMode.Pilot||value.runQualification!=ExperimentRunQualification.Collection||value.dataOrigin!="participant_collection"||!value.collectionEligible||value.developerTestAssignment||value.demoMode||value.conditions?.Length!=3){error="pilot_collection_assignment_invalid";return false;}return PilotAssignmentAllocator.IsCompatible(value,manager.ExperimentProtocol.ProtocolVersion,manager.TaskCatalog.CatalogVersion,out error);}
        private bool FailArm(){RuntimeContext=null;Stage=PilotParticipantStage.Setup;return false;}
        private void ResetRuntime(){questionnaireTransitionPending=false;qaRankingDraft=null;StopAllCoroutines();workflow?.ResetPilotConditionBoundary();workflow?.ClearAssignmentForRuntimeMode();orchestrator?.ResetForConditionSelection();currentPosition=-1;rankingSubmitted=false;}
        private void Write(string type,string detail="",bool qa=false){if(!IsArmed)return;Directory.CreateDirectory(CurrentDataFolder);var item=workflow?.Current;var value=new PilotCollectionOperatorEvent{timestampUtc=DateTime.UtcNow.ToString("o"),eventType=type,participantId=ParticipantId,sessionId=SessionId,pilotRunId=workflow?.PilotRunId??"",sequenceId=Assignment?.sequenceId??"",conditionPosition=item?.conditionPosition??-1,embodiment=item==null?"":PilotProtocolValues.Label(item.embodimentCondition),taskId=item?.task?.taskId??"",detail=detail,qaAutomationUsed=qa,actor=qa?"qa_operator":"experiment_operator"};File.AppendAllText(Path.Combine(CurrentDataFolder,"pilot_collection_operator_events.jsonl"),JsonUtility.ToJson(value)+Environment.NewLine,Encoding.UTF8);}
        private static bool ValidIdentity(string value,out string error){if(string.IsNullOrWhiteSpace(value)){error="participant_or_session_required";return false;}if(value.IndexOfAny(Path.GetInvalidFileNameChars())>=0||value.Contains("/")||value.Contains("\\")){error="participant_or_session_contains_invalid_path_character";return false;}error="";return true;}
        private static string Safe(string value)=>new string((value??"").Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
        private static void RefreshUi()=>FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include)?.RefreshExternalState();
    }

    [Serializable] public sealed class PilotGoalSnapshot{public string participantId;public string sessionId;public string pilotRunId;public string taskId;public string savedAtUtc;public GoalProgressRecord[] goals=Array.Empty<GoalProgressRecord>();}
}
