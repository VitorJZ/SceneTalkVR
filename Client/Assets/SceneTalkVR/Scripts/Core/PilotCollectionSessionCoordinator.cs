using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.History;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum PilotParticipantStage { None, Setup, AppearanceSelection, TaskIntroduction, Dialogue, Questionnaire, FinalRanking, Completion }

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
        private RehearsalSessionCoordinator rehearsal;
        private bool rankingSubmitted; private string lastBundlePath;
        private bool questionnaireTransitionPending;
        private PreferenceRankingResponse qaRankingDraft;
        private const string LastParticipantKey="SceneTalkVR.Pilot.LastParticipantId";
        private const string LastSessionKey="SceneTalkVR.Pilot.LastSessionId";

        public event Action<PreferenceRankingResponse> ExperimentCompleted;

        public ExperimentRuntimeContext RuntimeContext { get; private set; }
        public PilotParticipantStage Stage { get; private set; }
        public bool IsDeviceValidation => RuntimeContext?.flowMode==ExperimentFlowMode.Pilot
            && RuntimeContext.IsRehearsal && RuntimeContext.deploymentTarget==ExperimentDeploymentTarget.Pico
            && string.Equals(RuntimeContext.deploymentProfile,"pico_device_validation",StringComparison.Ordinal);
        public bool IsArmed => RuntimeContext?.flowMode==ExperimentFlowMode.Pilot
            && (RuntimeContext.IsCollection||IsDeviceValidation);
        public PilotAssignment Assignment => workflow?.Assignment;
        public PilotWorkflowCoordinator Workflow => workflow;
        public string ParticipantId => RuntimeContext?.participantId??string.Empty;
        public string SessionId => RuntimeContext?.sessionId??string.Empty;
        public int CurrentPosition => currentPosition;
        public bool HasActiveDialogueCondition => IsArmed&&Stage==PilotParticipantStage.Dialogue
            &&currentPosition>=0&&workflow?.Current?.status==PilotRunStatus.Running;
        public bool HasPendingConversationResume => HasActiveDialogueCondition
            && orchestrator?.IsDialogueActive != true;
        public ExperimentTaskDefinition CurrentTask { get { var taskId=workflow?.Current?.task?.taskId;if(string.IsNullOrWhiteSpace(taskId)&&Assignment?.conditions!=null&&currentPosition>=0&&currentPosition<Assignment.conditions.Length)taskId=Assignment.conditions[currentPosition].task?.taskId;return manager?.TaskCatalog?.Find(taskId); } }
        public string LastBundlePath => lastBundlePath??string.Empty;
        public static string CollectionRoot => Path.Combine(Application.persistentDataPath,"SceneTalkVR","PilotCollectionSessions");
        public string CurrentDataFolder
        {
            get
            {
                if(IsDeviceValidation&&rehearsal!=null)return rehearsal.CurrentDataFolder;
                return string.IsNullOrWhiteSpace(ParticipantId)||string.IsNullOrWhiteSpace(SessionId)
                    ?CollectionRoot:Path.Combine(CollectionRoot,Safe(ParticipantId)+"_"+Safe(SessionId),"raw");
            }
        }

        private void Awake(){Active=this;Resolve();Subscribe();}
        private void OnDestroy(){Unsubscribe();if(Active==this)Active=null;}
        public void Configure(ExperimentConditionManager value,SceneTalkOrchestrator sceneOrchestrator){manager=value;orchestrator=sceneOrchestrator;Resolve();Subscribe();}

        public void OpenSetup(){if(IsArmed)EndSession();Stage=PilotParticipantStage.Setup;RefreshUi();}
        public bool OpenOrCreateAutomaticParticipantSession(out string error)
        {
            if(IsArmed){if(Stage==PilotParticipantStage.None||Stage==PilotParticipantStage.Setup)Stage=PilotParticipantStage.AppearanceSelection;RefreshUi();error="";return true;}
            var previousParticipant=PlayerPrefs.GetString(LastParticipantKey,string.Empty);
            var previousSession=PlayerPrefs.GetString(LastSessionKey,string.Empty);
            var sessionRoot=ExperimentRuntimePlatform.IsPicoDeviceValidation?RehearsalSessionCoordinator.RehearsalRoot:CollectionRoot;
            if(!string.IsNullOrWhiteSpace(previousParticipant)&&!string.IsNullOrWhiteSpace(previousSession)
                && File.Exists(Path.Combine(sessionRoot,Safe(previousParticipant)+"_"+Safe(previousSession),"raw","pilot_assignment.json"))
                && ResumeSession(previousParticipant,previousSession,out error))return true;
            var timestamp=DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");var suffix=Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();
            var deviceValidation=ExperimentRuntimePlatform.IsPicoDeviceValidation;
            var participantId=deviceValidation?$"PICO-VAL-{suffix}":$"PILOT-P-{timestamp}-{suffix}";
            var sessionId=deviceValidation?$"PV-{timestamp}-{suffix}":$"PILOT-S-{timestamp}-{suffix}";
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
            if(!ValidIdentity(participantId,out error))return false;
            if(string.IsNullOrWhiteSpace(sessionId))sessionId=$"PILOT-{participantId}-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
            if(!ValidIdentity(sessionId,out error))return false;
            if(ExperimentRuntimePlatform.IsPicoDeviceValidation)return ArmDeviceValidation(participantId,sessionId,resume,out error);
            var picoCollection=ExperimentRuntimePlatform.IsPicoCollection;
            if(!Application.isEditor&&!picoCollection){error="pilot_collection_runtime_not_supported";return false;}
            if(EditorCollectionSessionCoordinator.Active?.IsArmed==true||RehearsalSessionCoordinator.Active?.IsActive==true||EditorDemoSessionCoordinator.Active?.IsDemoMode==true){error="another_experiment_runtime_is_active";return false;}
            if(manager==null||workflow==null||orchestrator==null){error="pilot_collection_scene_bindings_missing";return false;}
            var protocol=manager.ExperimentProtocol;var tasks=manager.TaskCatalog;var presentations=manager.PilotPresentationCatalog;
            var deploymentProfile=picoCollection?ExperimentDeploymentProfileId.PicoLab:ExperimentDeploymentProfileId.EditorCollection;
            manager.EnterCollectionMode(protocol,tasks,manager.QuestionnaireCatalog,manager.VoiceProfileCatalog,
                manager.DeploymentCatalog,deploymentProfile);
            if(protocol==null||!protocol.ValidateForFormalMode(out error))return FailArm();
            if(manager.EditorCollectionResources==null||!manager.EditorCollectionResources.Validate(tasks,
                manager.VoiceProfileCatalog,manager.DeploymentCatalog,deploymentProfile,out error))return FailArm();
            if(!manager.ValidateFormalProtocol(out error))return FailArm();
            if(!protocol.TryResolvePilotDecisions(out var style,out var audio,out error)||style!=PilotFeedbackStyleChoice.Explicit||audio!=PilotAudioSourcePolicy.NonSpatialHeadLocked)return FailArm();
            if(presentations==null||!presentations.ValidateLocked(protocol,out error))return FailArm();
            if(!ExperimentTaskCatalog.ValidatePilotTasks(tasks?.GetTasks(ExperimentTaskPhase.Pilot).ToArray(),out error))return FailArm();
            ResetRuntime();RuntimeContext=picoCollection
                ?ExperimentRuntimeContext.CreatePicoCollection(ExperimentFlowMode.Pilot,participantId,sessionId,protocol.ProtocolSnapshotId,"pilot-resources-"+presentations.CatalogVersion)
                :ExperimentRuntimeContext.CreatePilotEditorCollection(participantId,sessionId,protocol.ProtocolSnapshotId,"pilot-resources-"+presentations.CatalogVersion);
            Directory.CreateDirectory(CurrentDataFolder);
            var stored=PilotAssignmentAllocator.Load(AssignmentPath);
            if(resume)
            {
                if(stored==null){error="pilot_assignment_missing";return FailArm();}
                if(!ValidateAssignment(stored,out error)||!workflow.LoadAssignment(stored,out error))return FailArm();
                currentPosition=Array.FindIndex(stored.conditions,x=>x.status==PilotRunStatus.Running||x.status==PilotRunStatus.AwaitingPilotQuestionnaire||x.status==PilotRunStatus.PilotQuestionnaireInProgress||x.status==PilotRunStatus.TechnicalInvalid);
                rankingSubmitted=File.Exists(RankingSubmittedMarkerPath);
                if(currentPosition>=0&&stored.conditions[currentPosition].status!=PilotRunStatus.TechnicalInvalid)
                {
                    if(!ResumeStoredCondition(out error))return FailArm();
                }
                else
                {
                    currentPosition=-1;
                    Stage=stored.conditions.All(x=>x.status==PilotRunStatus.Completed)
                        ?(rankingSubmitted?PilotParticipantStage.Completion:PilotParticipantStage.FinalRanking)
                        :PilotParticipantStage.AppearanceSelection;
                }
            }
            else
            {
                if(stored!=null){error="session_already_exists_use_resume";return FailArm();}
                var allocator=new PilotAssignmentAllocator();
                if(!allocator.TryCreateCollection(participantId,sessionId,protocol,tasks,presentations,RuntimeContext.resourceSnapshotId,out var created,out error))return FailArm();
                created.runtimeMode=picoCollection?ExperimentRuntimeMode.PicoCollectionPilot:ExperimentRuntimeMode.EditorCollectionPilot;
                created.deploymentProfile=RuntimeContext.deploymentProfile;
                if(!workflow.LoadAssignment(created,out error))return FailArm();
                PilotAssignmentAllocator.Save(created,AssignmentPath);currentPosition=-1;Stage=PilotParticipantStage.AppearanceSelection;
            }
            if(!TryApplyConfirmedRunLimits(protocol,out error))return FailArm();if(!resume)ResetFinalRankingUi();Write("PilotSessionArmed","resume="+resume);RefreshUi();error="";return true;
        }

        private bool ArmDeviceValidation(string participantId,string sessionId,bool resume,out string error)
        {
            if(EditorCollectionSessionCoordinator.Active?.IsArmed==true||EditorDemoSessionCoordinator.Active?.IsDemoMode==true)
            {error="another_experiment_runtime_is_active";return false;}
            rehearsal=RehearsalSessionCoordinator.Active??SceneTalkFlowUiController.EnsureRuntimeRehearsalCoordinator();
            if(rehearsal==null){error="device_validation_rehearsal_coordinator_missing";return false;}
            var ok=resume?rehearsal.LoadSession(ExperimentFlowMode.Pilot,participantId,sessionId,out error)
                :rehearsal.CreatePilotSession(participantId,sessionId,out error);
            if(!ok)return false;
            RuntimeContext=rehearsal.RuntimeContext;
            currentPosition=resume&&Assignment?.conditions!=null
                ?Array.FindIndex(Assignment.conditions,x=>x.status==PilotRunStatus.Running||x.status==PilotRunStatus.AwaitingPilotQuestionnaire||x.status==PilotRunStatus.PilotQuestionnaireInProgress||x.status==PilotRunStatus.TechnicalInvalid)
                :-1;
            if(resume&&currentPosition>=0&&Assignment.conditions[currentPosition].status!=PilotRunStatus.TechnicalInvalid)
            {Assignment.conditions[currentPosition].status=PilotRunStatus.TechnicalInvalid;Persist();Write("PilotIncompleteAttemptPreservedForResumeRetry");}
            rankingSubmitted=File.Exists(RankingSubmittedMarkerPath);
            if(resume&&currentPosition>=0)currentPosition=-1;
            Stage=resume&&Assignment?.conditions?.All(x=>x.status==PilotRunStatus.Completed)==true
                ?(rankingSubmitted?PilotParticipantStage.Completion:PilotParticipantStage.FinalRanking)
                :PilotParticipantStage.AppearanceSelection;
            if(!resume)ResetFinalRankingUi();
            Write("PilotDeviceValidationArmed","resume="+resume);
            Debug.Log($"[ExperimentRuntime] Pilot device validation armed. qualification=Rehearsal; "
                + $"dataOrigin=rehearsal; collectionEligible=false; profile=pico_device_validation; sessionId={SessionId}",this);
            RefreshUi();error="";return true;
        }

        public void BeginPilot(){if(IsArmed)Stage=PilotParticipantStage.AppearanceSelection;RefreshUi();}
        public bool SelectEmbodiment(PilotEmbodimentCondition embodiment,out string error)
        {
            if(!IsArmed||Stage!=PilotParticipantStage.AppearanceSelection){error="pilot_appearance_selection_not_available";return false;}
            var items=Assignment?.conditions;var position=items==null?-1:Array.FindIndex(items,x=>x.embodimentCondition==embodiment);
            if(position<0){error="pilot_embodiment_not_assigned";return false;}var selected=items[position];
            if(selected.status!=PilotRunStatus.Assigned&&selected.status!=PilotRunStatus.TechnicalInvalid){error="pilot_embodiment_not_selectable:"+selected.status;return false;}
            currentPosition=position;var order=Assignment.participantSelectionOrder?.ToList()??new System.Collections.Generic.List<PilotEmbodimentCondition>();
            if(!order.Contains(embodiment))order.Add(embodiment);Assignment.participantSelectionOrder=order.ToArray();selected.participantSelectionPosition=order.IndexOf(embodiment);selected.selectedAtUtc=DateTime.UtcNow.ToString("o");
            Persist();Stage=PilotParticipantStage.TaskIntroduction;Write("PilotEmbodimentSelected","embodiment="+PilotProtocolValues.Label(embodiment));RefreshUi();error="";return true;
        }
        public bool BeginCurrentTask(out string error)
        {
            if(!IsArmed||Stage!=PilotParticipantStage.TaskIntroduction||currentPosition<0){error="pilot_task_introduction_not_active";return false;}
            var retry=Assignment.conditions[currentPosition].status==PilotRunStatus.TechnicalInvalid;
            if(IsDeviceValidation)
            {
                if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}
                if(!rehearsal.PreparePilotCondition(Assignment.conditions[currentPosition].embodimentCondition,out error))return false;
            }
            else
            {
                if(!GatewayTransportRouter.CanStartLiveAttempt(out error))return false;
                if(!workflow.Prepare(currentPosition,retry,out error))return false;
            }
            ExperimentSessionCoordinator.Active?.NotifyAttemptStarted(
                PilotProtocolValues.Label(workflow.Current.embodimentCondition),
                workflow.Current.task.taskId,
                workflow.PilotRunId,
                workflow.Current.runAttempt);
            Persist();Stage=PilotParticipantStage.Dialogue;Write(retry?"PilotConditionRetryStarted":"PilotConditionStarted");if(!IsDeviceValidation)orchestrator.LoadAssignedTask(workflow.Current.task.taskId);RefreshUi();return true;
        }
        public bool IsTaskPrepared(string taskId)=>IsArmed&&Stage==PilotParticipantStage.Dialogue&&workflow?.Current?.status==PilotRunStatus.Running&&string.Equals(workflow.Current.task?.taskId,taskId,StringComparison.OrdinalIgnoreCase);
        public bool StartNewConversationForResumedCondition(out string error)
        {
            if(!HasPendingConversationResume||currentPosition<0){error="pilot_conversation_resume_not_pending";return false;}
            if(IsDeviceValidation){error="pilot_device_validation_conversation_restart_not_supported";return false;}
            if(!GatewayTransportRouter.CanStartLiveAttempt(out error))return false;
            var selected=workflow.Current;
            PersistGoals();
            if(!workflow.Prepare(currentPosition,false,out error))return false;
            ExperimentSessionCoordinator.Active?.NotifyAttemptStarted(
                PilotProtocolValues.Label(selected.embodimentCondition),
                selected.task.taskId,
                workflow.PilotRunId,
                selected.runAttempt);
            PersistGoals();Persist();Stage=PilotParticipantStage.Dialogue;
            Write("PilotConversationRestarted",$"sessionRun={workflow.PilotRunId}",false,"participant");
            orchestrator.LoadAssignedTask(selected.task.taskId);RefreshUi();error="";return true;
        }
        public void RecordConversationResumed(string conversationSessionId)
        {
            if(!HasActiveDialogueCondition)return;
            Persist();Write("PilotConversationResumed",$"sessionId={conversationSessionId};runId={workflow.PilotRunId}",false,"participant");RefreshUi();
        }
        public bool SubmitQuestionnaire(out string error)
            => CompleteQuestionnaire(false, out error);
        public bool SkipQuestionnaire(out string error)
            => CompleteQuestionnaire(true, out error);
        private bool CompleteQuestionnaire(bool skip, out string error)
        {
            if(Stage!=PilotParticipantStage.Questionnaire){error="pilot_questionnaire_not_visible";return false;}
            if(IsDeviceValidation)
            {
                if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}
                var completed=skip?rehearsal.SkipQuestionnaire(out error):rehearsal.SubmitQuestionnaire(out error);
                if(!completed)return false;
            }
            else
            {
                var completed=skip?workflow.SkipQuestionnaire(out error):workflow.SubmitQuestionnaire(out error);
                if(!completed)return false;
            }
            ExperimentSessionCoordinator.Active?.NotifyAttemptCompleted(skip?"questionnaire_skipped":"questionnaire_submitted");
            Persist();
            workflow.ResetPilotConditionBoundary();currentPosition=-1;
            if(Assignment.conditions.All(x=>x.status==PilotRunStatus.Completed)){Stage=PilotParticipantStage.FinalRanking;ExperimentSessionCoordinator.Active?.NotifyRankingOpened();}
            else Stage=PilotParticipantStage.AppearanceSelection;
            Write(skip?"PilotQuestionnaireSkipped":"PilotQuestionnaireSubmitted");RefreshUi();return true;
        }
        public void ContinueAfterTransition(){if(IsArmed)Stage=PilotParticipantStage.AppearanceSelection;RefreshUi();}
        public bool SubmitFinalRanking(PreferenceRankingResponse value,out string error)
        {
            if(Stage!=PilotParticipantStage.FinalRanking){error="pilot_final_ranking_not_visible";return false;}
            value.protocolVersion=Assignment.pilotProtocolVersion;value.questionnaireCatalogVersion=manager.QuestionnaireCatalog.CatalogVersion;
            value.participantId=ParticipantId;value.sessionId=SessionId;value.sequenceId=Assignment.sequenceId;value.questionnaireId="pilot_final_v1";value.submittedAtUtc=DateTime.UtcNow.ToString("o");
            bool ok;
            if(IsDeviceValidation)
            {
                if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}
                ok=rehearsal.SubmitRanking(value,out error);
            }
            else ok=workflow.SubmitFinalRanking(value,out error);
            if(!ok)return false;rankingSubmitted=true;File.WriteAllText(RankingSubmittedMarkerPath,value.submittedAtUtc,Encoding.UTF8);Stage=PilotParticipantStage.Completion;Write("PilotExperimentCompleted");ExperimentCompleted?.Invoke(value);RefreshUi();return true;
        }
        public bool ExportBundle(out string error){if(IsDeviceValidation){if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}var ok=rehearsal.ExportBundle(out error);lastBundlePath=rehearsal.LastBundlePath;return ok;}var result=PilotCollectionBundleExporter.Export(Path.GetDirectoryName(CurrentDataFolder),Assignment,manager.ExperimentProtocol,manager.QuestionnaireCatalog,rankingSubmitted,out lastBundlePath,out error);if(result)Write("PilotBundleExported",lastBundlePath);return result;}
        public bool AuditBundle(out string error){if(IsDeviceValidation){if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}return rehearsal.AuditBundle(out error);}if(string.IsNullOrWhiteSpace(lastBundlePath)||!Directory.Exists(lastBundlePath)){error="pilot_bundle_missing";return false;}var report=SessionDataIntegrityAuditor.Audit(lastBundlePath,ParticipantId,SessionId);SessionDataIntegrityAuditor.WriteReport(report,lastBundlePath+"-manual-audit.json");error=report.result.ToString().ToUpperInvariant();return report.result!=DataIntegritySeverity.Fail;}
        public bool ReturnToAppearanceSelectionFromDialogue(string reason,out string error)
        {
            if(!HasActiveDialogueCondition){error="pilot_dialogue_not_active";return false;}
            reason=string.IsNullOrWhiteSpace(reason)?"participant_return_to_selection":reason.Trim();
            if(IsDeviceValidation)
            {
                if(rehearsal==null){error="device_validation_rehearsal_session_missing";return false;}
                if(!rehearsal.ReturnToConditionSelectionFromDialogue(reason,out error))return false;
            }
            else
            {
                workflow.MarkTechnicalInvalid("ParticipantNavigation",reason);
                Persist();
                orchestrator?.ResetForConditionSelection();
                workflow.ResetPilotConditionBoundary();
            }
            currentPosition=-1;Stage=PilotParticipantStage.AppearanceSelection;
            Write("PilotReturnedToAppearanceSelection","reason="+reason,false,"participant");
            RefreshUi();error="";return true;
        }
        public void MarkTechnicalInvalid(string reason){if(!IsArmed)return;workflow.MarkTechnicalInvalid("experiment_operator",reason);ExperimentSessionCoordinator.Active?.NotifyAttemptTechnicalInvalid(reason);Persist();currentPosition=-1;Stage=PilotParticipantStage.AppearanceSelection;Write("PilotTechnicalInvalid",reason);RefreshUi();}
        public bool Retry(out string error){if(workflow?.Current?.status!=PilotRunStatus.TechnicalInvalid){error="pilot_retry_not_available";return false;}currentPosition=workflow.Current.conditionPosition;Stage=PilotParticipantStage.TaskIntroduction;return BeginCurrentTask(out error);}
        public void EndSession(){Write("PilotSessionEnded");if(IsDeviceValidation)rehearsal?.EndRuntimeSession();else manager?.ExitEditorCollectionMode();ResetRuntime();RuntimeContext=null;rehearsal=null;Stage=PilotParticipantStage.None;RefreshUi();}
        public void ExitAndEndSession(string reason)
        {
            reason=string.IsNullOrWhiteSpace(reason)?"participant_exit":reason.Trim();
            if(IsArmed){workflow?.AbortCurrent(reason);Persist();Write("PilotSessionExited","reason="+reason,false,"participant");}
            EndSession();
        }

        public void SuspendAndEndSession(string reason)
        {
            reason=string.IsNullOrWhiteSpace(reason)?"participant_exit_checkpoint":reason.Trim();
            if(IsArmed)
            {
                if(workflow?.Current!=null&&workflow.Current.status!=PilotRunStatus.Completed
                    &&workflow.Current.status!=PilotRunStatus.TechnicalInvalid)
                {
                    PersistGoals();workflow.SuspendForCheckpoint(reason);Persist();
                }
                Write("PilotSessionSuspended","reason="+reason,false,"participant");
            }
            EndSession();
        }

        public bool PrepareCurrentPilotConditionForQa(out string error)
        { if(Stage==PilotParticipantStage.AppearanceSelection){var next=NextAssigned();if(next<0){error="pilot_no_remaining_condition";return false;}if(!SelectEmbodiment(Assignment.conditions[next].embodimentCondition,out error))return false;}var ok=BeginCurrentTask(out error);if(ok)Write("QaPreparePilotCondition","",true);return ok; }
        public bool CompleteCurrentPilotGoalsForQa(out string error)
        {
            error="";if(!IsArmed||Stage!=PilotParticipantStage.Dialogue){error="pilot_dialogue_not_active";return false;}
            var guard=workflow.Goals.Goals.Count;
            while(workflow.Goals.ActiveGoal!=null&&guard-->0)
            {var goal=workflow.Goals.ActiveGoal;var turnId=$"pilot-qa-{workflow.Goals.ActiveGoalIndex+1}";if((goal.state==GoalProgressState.NotStarted||goal.state==GoalProgressState.Rejected)&&!workflow.Goals.SubmitGoalCandidate(goal.goalId,"qa_operator",new GoalEvidence{turnId=turnId,transcript="qaAutomationUsed=true"},out error))return false;if(goal.state==GoalProgressState.Candidate&&!workflow.Goals.ConfirmGoal(goal.goalId,"qa_operator","qaAutomationUsed=true",out error))return false;if(!AdvanceAutomatedGoalTurn(workflow.Goals,turnId)){error="pilot_goal_sequence_did_not_advance";return false;}}
            if(!workflow.Goals.IsSequenceCompleted){error="pilot_goal_sequence_incomplete";return false;}
            Write("QaCompletePilotGoals","",true);return true;
        }
        private static bool AdvanceAutomatedGoalTurn(GoalProgressTracker tracker,string evidenceTurnId)
        {if(tracker.SequenceState==GoalSequenceState.ActiveGoal||tracker.SequenceState==GoalSequenceState.Completed)return true;if(tracker.SequenceState==GoalSequenceState.AwaitingParticipantTurn){var unlockTurnId=evidenceTurnId+"-unlock";return tracker.NotifyParticipantTurnSubmitted(unlockTurnId)&&tracker.NotifyDialogueTurnCompleted(unlockTurnId);}return tracker.SequenceState==GoalSequenceState.AwaitingAvatarReply&&tracker.NotifyDialogueTurnCompleted(evidenceTurnId);}
        public bool OpenPilotQuestionnaireForQa(out string error)
        { if(Stage==PilotParticipantStage.Dialogue)workflow.CompleteTask();if(workflow.Current?.status!=PilotRunStatus.AwaitingPilotQuestionnaire){error="pilot_not_awaiting_questionnaire";return false;}if(!workflow.BeginQuestionnaire(out error))return false;Stage=PilotParticipantStage.Questionnaire;Persist();Write("QaOpenPilotQuestionnaire","",true);RefreshUi();return true; }
        public bool AutoFillPilotQuestionnaireForQa(out string error)
        { error="";var service=workflow?.Questionnaire;if(Stage!=PilotParticipantStage.Questionnaire||service?.ActiveSession==null||service.Definition==null){error="pilot_questionnaire_not_visible";return false;}foreach(var item in manager.QuestionnaireCatalog.GetEnabledItems(service.Definition.questionnaireId,manager.ExperimentProtocol)){var raw=item.itemType==QuestionnaireItemType.Likert?Mathf.Clamp(5,item.scaleMin,item.scaleMax).ToString():item.choiceValues!=null&&item.choiceValues.Length>0?item.choiceValues[0]:"QA response";if(!service.SetResponse(item.itemId,raw,out error))return false;}Write("QaAutoFillPilotQuestionnaire","",true);return true; }
        public bool SubmitPilotQuestionnaireForQa(out string error){var ok=SubmitQuestionnaire(out error);if(ok)Write("QaSubmitPilotQuestionnaire","",true);return ok;}
        public bool PrepareNextPilotConditionForQa(out string error)=>PrepareCurrentPilotConditionForQa(out error);
        public void MarkPilotTechnicalInvalidForQa(){MarkTechnicalInvalid("qa_operator_injected");Write("QaMarkPilotTechnicalInvalid","",true);}
        public bool RetryPilotConditionForQa(out string error){var ok=Retry(out error);if(ok)Write("QaRetryPilotCondition","",true);return ok;}
        public bool OpenPilotFinalRankingForQa(out string error){if(Assignment?.conditions==null||Assignment.conditions.Any(x=>x.status!=PilotRunStatus.Completed)){error="pilot_final_ranking_requires_three_valid_conditions";return false;}Stage=PilotParticipantStage.FinalRanking;ExperimentSessionCoordinator.Active?.NotifyRankingOpened();Write("QaOpenPilotFinalRanking","",true);RefreshUi();error="";return true;}
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
        private void PersistGoals(){if(!IsArmed||string.IsNullOrWhiteSpace(workflow.PilotRunId)||workflow.Current==null)return;Directory.CreateDirectory(CurrentDataFolder);var file=$"pilot_goals_{workflow.Current.conditionPosition}_{workflow.Current.runAttempt}.json";File.WriteAllText(Path.Combine(CurrentDataFolder,file),JsonUtility.ToJson(new PilotGoalSnapshot{participantId=ParticipantId,sessionId=SessionId,pilotRunId=workflow.PilotRunId,taskId=workflow.Current.task.taskId,savedAtUtc=DateTime.UtcNow.ToString("o"),goals=workflow.Goals.Goals.ToArray(),sequence=workflow.Goals.CaptureSequenceSnapshot()},true),Encoding.UTF8);}
        private PilotGoalSnapshot LoadGoalSnapshot(PilotConditionAssignment condition)
        {
            if(condition==null)return null;
            var path=Path.Combine(CurrentDataFolder,$"pilot_goals_{condition.conditionPosition}_{condition.runAttempt}.json");
            if(!File.Exists(path))return null;
            try
            {
                var value=JsonUtility.FromJson<PilotGoalSnapshot>(File.ReadAllText(path,Encoding.UTF8));
                return value!=null
                    &&string.Equals(value.pilotRunId,condition.latestPilotRunId,StringComparison.Ordinal)
                    &&string.Equals(value.taskId,condition.task?.taskId,StringComparison.OrdinalIgnoreCase)
                    ?value:null;
            }
            catch(Exception exception)
            {
                Debug.LogWarning("[PilotCollection] Failed to load goal snapshot: "+exception.Message,this);
                return null;
            }
        }
        private bool ResumeStoredCondition(out string error)
        {
            var condition=Assignment?.conditions?[currentPosition];
            var snapshot=LoadGoalSnapshot(condition);
            if(!workflow.Resume(currentPosition,snapshot?.goals,snapshot?.sequence,out error))return false;
            if(condition.status==PilotRunStatus.Running&&workflow.Goals.IsSequenceCompleted)
                workflow.CompleteTask();
            if(condition.status==PilotRunStatus.AwaitingPilotQuestionnaire)
            {
                if(!workflow.BeginQuestionnaire(out error))return false;
                Persist();Stage=PilotParticipantStage.Questionnaire;return true;
            }
            if(condition.status==PilotRunStatus.PilotQuestionnaireInProgress)
            {
                if(!workflow.RestoreQuestionnaireDraft(CurrentDataFolder,out error))
                {
                    var legacyFolder=Path.Combine(Application.persistentDataPath,"SceneTalkVR","ExperimentLogs");
                    if(!workflow.RestoreQuestionnaireDraft(legacyFolder,out error))return false;
                }
                Stage=PilotParticipantStage.Questionnaire;return true;
            }
            Stage=PilotParticipantStage.Dialogue;return true;
        }
        private bool TryApplyConfirmedRunLimits(ExperimentV11ProtocolConfig protocol,out string error){if(protocol==null||!protocol.TryGetConfirmedDecision("pilot_max_turns",out var turnsValue)||!int.TryParse(turnsValue,out var turns)||turns<=0){error="pilot_max_turns_invalid";return false;}if(!protocol.TryGetConfirmedDecision("pilot_max_duration",out var durationValue)){error="pilot_max_duration_invalid";return false;}var token=new string(durationValue.TakeWhile(c=>char.IsDigit(c)||c=='.').ToArray());if(!float.TryParse(token,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var minutes)||minutes<=0f){error="pilot_max_duration_invalid";return false;}workflow.ConfigureRunLimits(turns,minutes);error="";return true;}
        private bool ValidateAssignment(PilotAssignment value,out string error){var expectedMode=RuntimeContext?.deploymentTarget==ExperimentDeploymentTarget.Pico?ExperimentRuntimeMode.PicoCollectionPilot:ExperimentRuntimeMode.EditorCollectionPilot;if(value==null||value.flowMode!=ExperimentFlowMode.Pilot||value.runQualification!=ExperimentRunQualification.Collection||value.dataOrigin!="participant_collection"||!value.collectionEligible||value.developerTestAssignment||value.demoMode||value.conditions?.Length!=3||value.runtimeMode!=expectedMode||value.deploymentProfile!=RuntimeContext?.deploymentProfile){error="pilot_collection_assignment_invalid";return false;}return PilotAssignmentAllocator.IsCompatible(value,manager.ExperimentProtocol.ProtocolVersion,manager.TaskCatalog.CatalogVersion,out error);}
        private bool FailArm(){manager?.ExitEditorCollectionMode();ResetRuntime();RuntimeContext=null;Stage=PilotParticipantStage.Setup;return false;}
        private void ResetRuntime(){questionnaireTransitionPending=false;qaRankingDraft=null;StopAllCoroutines();workflow?.ResetPilotConditionBoundary();workflow?.ClearAssignmentForRuntimeMode();orchestrator?.ResetForConditionSelection();currentPosition=-1;rankingSubmitted=false;}
        private void ResetFinalRankingUi()=>(GetComponent<PilotCollectionParticipantUi>()??FindFirstObjectByType<PilotCollectionParticipantUi>(FindObjectsInactive.Include))?.ResetFinalRankingDraft();
        private void Write(string type,string detail="",bool qa=false,string actor=""){if(!IsArmed)return;Directory.CreateDirectory(CurrentDataFolder);var item=workflow?.Current;var value=new PilotCollectionOperatorEvent{timestampUtc=DateTime.UtcNow.ToString("o"),eventType=type,participantId=ParticipantId,sessionId=SessionId,pilotRunId=workflow?.PilotRunId??"",sequenceId=Assignment?.sequenceId??"",conditionPosition=item?.conditionPosition??-1,embodiment=item==null?"":PilotProtocolValues.Label(item.embodimentCondition),taskId=item?.task?.taskId??"",detail=detail,runQualification=RuntimeContext?.qualification.ToString().ToLowerInvariant()??"",dataOrigin=RuntimeContext?.dataOrigin??"",collectionEligible=RuntimeContext?.collectionEligible==true,deploymentProfile=RuntimeContext?.deploymentProfile??"",qaAutomationUsed=qa,actor=!string.IsNullOrWhiteSpace(actor)?actor:qa?"qa_operator":IsDeviceValidation?"device_validation_participant":"experiment_operator"};var file=IsDeviceValidation?"pilot_device_validation_operator_events.jsonl":"pilot_collection_operator_events.jsonl";File.AppendAllText(Path.Combine(CurrentDataFolder,file),JsonUtility.ToJson(value)+Environment.NewLine,Encoding.UTF8);}
        private static bool ValidIdentity(string value,out string error){if(string.IsNullOrWhiteSpace(value)){error="participant_or_session_required";return false;}if(value.IndexOfAny(Path.GetInvalidFileNameChars())>=0||value.Contains("/")||value.Contains("\\")){error="participant_or_session_contains_invalid_path_character";return false;}error="";return true;}
        private static string Safe(string value)=>new string((value??"").Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
        private static void RefreshUi()=>FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include)?.RefreshExternalState();
    }

    [Serializable] public sealed class PilotGoalSnapshot{public string schemaVersion=GoalSequenceSnapshot.CurrentSchemaVersion;public string participantId;public string sessionId;public string pilotRunId;public string taskId;public string savedAtUtc;public GoalProgressRecord[] goals=Array.Empty<GoalProgressRecord>();public GoalSequenceSnapshot sequence;}
}
