using System;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class RehearsalOperatorEvent
    {
        public string schemaVersion = "1.1";
        public string timestampUtc;
        public string flowMode;
        public string runQualification = "Rehearsal";
        public string dataOrigin = "rehearsal";
        public bool collectionEligible;
        public bool developerTestAssignment;
        public bool synthetic;
        public bool demoMode;
        public string protocolVersion;
        public string protocolSnapshotId;
        public string resourceSnapshotId;
        public string participantId;
        public string sessionId;
        public string actor = "rehearsal_operator";
        public string action;
        public bool qaAutomationUsed;
        public string detail;
    }

    [DisallowMultipleComponent]
    public sealed class RehearsalSessionCoordinator : MonoBehaviour
    {
        public static RehearsalSessionCoordinator Active { get; private set; }
        [SerializeField] private ExperimentV11RehearsalProtocol protocol;
        [SerializeField] private ExperimentV11RehearsalResourceCatalog resources;
        [SerializeField] private ExperimentVoiceProfileCatalog voiceCatalog;
        [SerializeField] private ExperimentDeploymentCatalog deploymentCatalog;
        private ExperimentConditionManager conditionManager;
        private ExperimentLifecycleCoordinator formalLifecycle;
        private PilotWorkflowCoordinator pilotWorkflow;
        private QuestionnaireRuntimeController questionnaire;
        private SceneTalkOrchestrator orchestrator;
        private int currentPosition = -1;
        private bool rankingSubmitted;
        private bool interviewSaved;
        private bool resetInProgress;
        private string lastBundlePath;

        public ExperimentRuntimeContext RuntimeContext { get; private set; }
        public bool IsActive => RuntimeContext != null && RuntimeContext.IsRehearsal;
        public bool IsFormal => IsActive && RuntimeContext.flowMode == ExperimentFlowMode.Formal;
        public bool IsPilot => IsActive && RuntimeContext.flowMode == ExperimentFlowMode.Pilot;
        public ExperimentAssignment FormalAssignment => formalLifecycle?.Assignment;
        public PilotAssignment PilotAssignment => pilotWorkflow?.Assignment;
        public ExperimentV11RehearsalProtocol Protocol => protocol;
        public ExperimentV11RehearsalResourceCatalog Resources => resources;
        public ExperimentVoiceProfileCatalog VoiceCatalog => voiceCatalog;
        public ExperimentDeploymentCatalog DeploymentCatalog => deploymentCatalog;
        public int CurrentPosition => currentPosition;
        public int TotalConditions => IsFormal ? 4 : IsPilot ? 3 : 0;
        public string ParticipantId => RuntimeContext?.participantId ?? string.Empty;
        public string SessionId => RuntimeContext?.sessionId ?? string.Empty;
        public string CurrentRunId => IsFormal ? formalLifecycle?.ConditionRunId ?? string.Empty : pilotWorkflow?.PilotRunId ?? string.Empty;
        public string CurrentTaskId => IsFormal ? formalLifecycle?.CurrentConditionAssignment?.task?.taskId ?? string.Empty : pilotWorkflow?.Current?.task?.taskId ?? string.Empty;
        public string LastBundlePath => lastBundlePath ?? string.Empty;
        public bool RankingSubmitted => rankingSubmitted;
        public bool InterviewSaved => interviewSaved;
        public static string RehearsalRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "SceneTalkVR", "RehearsalSessions");
        public string CurrentDataFolder => string.IsNullOrWhiteSpace(ParticipantId) || string.IsNullOrWhiteSpace(SessionId)
            ? RehearsalRoot : Path.Combine(RehearsalRoot, Safe(ParticipantId) + "_" + Safe(SessionId), "raw");

        private void Awake() { Active = this; ResolveDependencies(); }
        private void OnDestroy() { if (Active == this) Active = null; }

        public void Configure(ExperimentV11RehearsalProtocol rehearsalProtocol, ExperimentV11RehearsalResourceCatalog rehearsalResources,
            ExperimentVoiceProfileCatalog voices, ExperimentDeploymentCatalog deployments)
        {
            protocol = rehearsalProtocol; resources = rehearsalResources; voiceCatalog = voices; deploymentCatalog = deployments;
            ResolveDependencies(); ApplyProtocolLimits(); RefreshUi();
        }

        public bool CreateFormalSession(string participantId, string sessionId, out string error) =>
            CreateSession(ExperimentFlowMode.Formal, participantId, sessionId, out error);

        public bool CreatePilotSession(string participantId, string sessionId, out string error) =>
            CreateSession(ExperimentFlowMode.Pilot, participantId, sessionId, out error);

        public bool CreateSession(ExperimentFlowMode flow, string participantId, string sessionId, out string error)
        {
            if (!ValidateCommon(flow, out error)) return false;
            if (string.IsNullOrWhiteSpace(participantId)) { error = "participant_id_missing"; return false; }
            if (string.IsNullOrWhiteSpace(sessionId)) { error = "session_id_missing"; return false; }
            ResetSession();
            RuntimeContext = ExperimentRuntimeContext.CreateRehearsal(flow, participantId, sessionId, protocol.ProtocolSnapshotId, resources.ResourceSnapshotId);
            if (flow == ExperimentFlowMode.Formal)
            {
                var allocator = new ExperimentAssignmentAllocator();
                if (!allocator.TryCreateRehearsal(ParticipantId, SessionId, protocol, conditionManager.TaskCatalog,
                    resources.ResourceSnapshotId, out var assignment, out error) || !formalLifecycle.LoadAssignment(assignment, out error)) return FailStart(error);
            }
            else
            {
                var allocator = new PilotAssignmentAllocator();
                if (!allocator.TryCreateRehearsal(ParticipantId, SessionId, protocol, conditionManager.TaskCatalog,
                    resources.ResourceSnapshotId, out var assignment, out error) || !pilotWorkflow.LoadAssignment(assignment, out error)) return FailStart(error);
            }
            currentPosition = -1; rankingSubmitted = false; interviewSaved = false;
            PersistAssignments(); WriteOperator("CreateSession"); RefreshUi(); return true;
        }

        public bool LoadSession(ExperimentFlowMode flow, string participantId, string sessionId, out string error)
        {
            if (!ValidateCommon(flow, out error)) return false;
            RuntimeContext = ExperimentRuntimeContext.CreateRehearsal(flow, participantId, sessionId, protocol.ProtocolSnapshotId, resources.ResourceSnapshotId);
            var raw = CurrentDataFolder;
            if (flow == ExperimentFlowMode.Formal)
            {
                var value = ExperimentAssignmentAllocator.Load(Path.Combine(raw, "formal_assignment.json"));
                if (!ValidateFormalRehearsal(value, out error) || !formalLifecycle.LoadAssignment(value, out error)) return FailStart(error);
                currentPosition = Array.FindIndex(value.conditions, x => x.status == ConditionRunStatus.Running || x.status == ConditionRunStatus.AwaitingQuestionnaire || x.status == ConditionRunStatus.QuestionnaireInProgress);
            }
            else
            {
                var value = PilotAssignmentAllocator.Load(Path.Combine(raw, "pilot_assignment.json"));
                if (!ValidatePilotRehearsal(value, out error) || !pilotWorkflow.LoadAssignment(value, out error)) return FailStart(error);
                currentPosition = Array.FindIndex(value.conditions, x => x.status == PilotRunStatus.Running || x.status == PilotRunStatus.AwaitingPilotQuestionnaire || x.status == PilotRunStatus.PilotQuestionnaireInProgress);
            }
            WriteOperator("LoadSession"); RefreshUi(); return true;
        }

        public bool PrepareCurrentCondition(out string error)
        {
            error = string.Empty;
            if (!IsActive) { error = "rehearsal_session_not_active"; return false; }
            if (IsFormal)
            {
                var items = FormalAssignment?.conditions;
                var next = items == null ? -1 : Array.FindIndex(items, x => x.status == ConditionRunStatus.Assigned || x.status == ConditionRunStatus.TechnicalInvalid);
                if (next < 0) { error = "formal_rehearsal_no_remaining_condition"; return false; }
                currentPosition = next;
                if (!formalLifecycle.PrepareCondition(next, items[next].status == ConditionRunStatus.TechnicalInvalid, out error)) return false;
            }
            else
            {
                var items = PilotAssignment?.conditions;
                var next = items == null ? -1 : Array.FindIndex(items, x => x.status == PilotRunStatus.Assigned || x.status == PilotRunStatus.TechnicalInvalid);
                if (next < 0) { error = "pilot_rehearsal_no_remaining_condition"; return false; }
                currentPosition = next;
                if (!pilotWorkflow.Prepare(next, items[next].status == PilotRunStatus.TechnicalInvalid, out error)) return false;
                orchestrator.LoadAssignedTask(items[next].task.taskId);
            }
            PersistAssignments(); WriteOperator("PrepareCurrentCondition"); RefreshUi(); return true;
        }

        public bool IsTaskPrepared(string taskId) => IsActive && string.Equals(CurrentTaskId, taskId, StringComparison.OrdinalIgnoreCase)
            && (IsFormal ? formalLifecycle?.CurrentConditionAssignment?.status == ConditionRunStatus.Running : pilotWorkflow?.Current?.status == PilotRunStatus.Running);

        public bool CompleteTask(out string error)
        {
            error = string.Empty;
            if (IsFormal) formalLifecycle.CompleteTask("experimenter_completed", "rehearsal_operator");
            else if (IsPilot) pilotWorkflow.CompleteTask();
            else { error = "rehearsal_session_not_active"; return false; }
            PersistAssignments(); WriteOperator("CompleteTask"); RefreshUi(); return true;
        }

        public bool OpenQuestionnaire(out string error)
        {
            var ok = IsFormal ? questionnaire.StartCurrentConditionQuestionnaire(out error)
                : IsPilot ? pilotWorkflow.BeginQuestionnaire(out error) : Fail(out error, "rehearsal_session_not_active");
            if (ok) WriteOperator("OpenQuestionnaire"); RefreshUi(); return ok;
        }

        public bool SubmitQuestionnaire(out string error)
        {
            var ok = IsFormal ? questionnaire.Submit(out error)
                : IsPilot ? pilotWorkflow.SubmitQuestionnaire(out error) : Fail(out error, "rehearsal_session_not_active");
            if (ok) { PersistAssignments(); WriteOperator("CompleteQuestionnaireBoundary"); }
            RefreshUi(); return ok;
        }

        public void MarkTechnicalInvalid(string reason)
        {
            if (IsFormal) formalLifecycle.MarkTechnicalInvalid(reason);
            else if (IsPilot) pilotWorkflow.MarkTechnicalInvalid("rehearsal_operator", reason);
            PersistAssignments(); WriteOperator("MarkTechnicalInvalid", detail: reason); RefreshUi();
        }

        public bool Retry(out string error)
        {
            var ok = IsFormal ? formalLifecycle.PrepareCondition(currentPosition, true, out error)
                : IsPilot ? pilotWorkflow.RetryCurrent(out error) : Fail(out error, "rehearsal_session_not_active");
            if (ok) { PersistAssignments(); WriteOperator("Retry"); } return ok;
        }

        public bool OpenFinalRanking(out string error)
        {
            var ui = FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include);
            if (!IsActive || ui == null) { error = !IsActive ? "rehearsal_session_not_active" : "scene_talk_flow_ui_missing"; return false; }
            ui.ShowRehearsalRanking(IsPilot); WriteOperator("OpenFinalRanking"); error = string.Empty; return true;
        }

        public bool SubmitRanking(PreferenceRankingResponse response, out string error)
        {
            var ok = IsFormal ? questionnaire.SubmitFormalRanking(response, out error)
                : IsPilot ? pilotWorkflow.SubmitFinalRanking(response, out error) : Fail(out error, "rehearsal_session_not_active");
            if (ok) { rankingSubmitted = true; WriteOperator("SubmitFinalRanking"); } return ok;
        }

        public bool AutoFillRankingForQa(out string error)
        {
            if (!IsActive) { error = "rehearsal_session_not_active"; return false; }
            var labels = IsFormal ? new[] { "NE", "NR", "SE", "SR" } : new[] { "voice_only", "floating_orb", "humanoid_agent" };
            var response = new PreferenceRankingResponse { protocolVersion = protocol.ProtocolVersion,
                questionnaireCatalogVersion = conditionManager.QuestionnaireCatalog.CatalogVersion,
                participantId = ParticipantId, sessionId = SessionId,
                sequenceId = IsFormal ? FormalAssignment.sequenceId : PilotAssignment.sequenceId,
                questionnaireId = IsFormal ? "formal_final_v1" : "pilot_final_v1",
                rankings = labels.Select((x, i) => new PreferenceRankEntry { rank = i + 1, conditionCode = IsFormal ? x : string.Empty, embodimentCondition = IsFormal ? string.Empty : x }).ToArray(),
                preferredConditionCode = IsFormal ? labels[0] : string.Empty,
                preferredEmbodimentCondition = IsFormal ? string.Empty : labels[0], reason = "Rehearsal QA automation", submittedAtUtc = DateTime.UtcNow.ToString("o") };
            if (!SubmitRanking(response, out error)) return false; WriteOperator("AutoFillRanking", true); return true;
        }

        public bool SaveInterview(string note, out string error)
        {
            if (!IsFormal) { error = "formal_rehearsal_required"; return false; }
            var value = new InterviewNote { protocolVersion = protocol.ProtocolVersion,
                questionnaireCatalogVersion = conditionManager.QuestionnaireCatalog.CatalogVersion,
                participantId = ParticipantId, sessionId = SessionId, sequenceId = FormalAssignment.sequenceId,
                interviewerId = "rehearsal_operator", interviewStartedAtUtc = DateTime.UtcNow.ToString("o"),
                interviewCompletedAtUtc = DateTime.UtcNow.ToString("o"), questionId = "formal_rehearsal_interview",
                responseText = note ?? string.Empty, notes = "runQualification=rehearsal; collectionEligible=false" };
            if (!questionnaire.SaveInterview(value, out error)) return false;
            interviewSaved = true; WriteOperator("SaveInterview"); return true;
        }

        public bool CompleteCurrentGoalsForQa(out string error)
        {
            error = string.Empty; if (!IsActive) { error = "rehearsal_session_not_active"; return false; }
            var tracker = IsFormal ? formalLifecycle.GoalTracker : pilotWorkflow.Goals;
            foreach (var goal in tracker.Goals.ToArray())
            {
                if ((goal.state == GoalProgressState.NotStarted || goal.state == GoalProgressState.Rejected)
                    && !tracker.SubmitGoalCandidate(goal.goalId, "rehearsal_qa_operator", new GoalEvidence { turnId = "qa", transcript = "qaAutomationUsed=true" }, out error)) return false;
                if (goal.state == GoalProgressState.Candidate && !tracker.ConfirmGoal(goal.goalId, "rehearsal_qa_operator", "qaAutomationUsed=true", out error)) return false;
            }
            WriteOperator("AutoCompleteGoals", true); RefreshUi(); return true;
        }

        public bool AutoFillQuestionnaireForQa(out string error)
        {
            error = string.Empty; var service = IsFormal ? questionnaire.Service : pilotWorkflow.Questionnaire;
            if (!IsActive || service?.ActiveSession == null || service.Definition == null) { error = "questionnaire_not_started"; return false; }
            foreach (var item in conditionManager.QuestionnaireCatalog.GetEnabledItems(service.Definition.questionnaireId, conditionManager.ExperimentProtocol))
            {
                var raw = item.itemType == QuestionnaireItemType.Likert ? Mathf.Clamp(5, item.scaleMin, item.scaleMax).ToString()
                    : item.choiceValues != null && item.choiceValues.Length > 0 ? item.choiceValues[0] : "Rehearsal QA response";
                if (!service.SetResponse(item.itemId, raw, out error)) return false;
            }
            WriteOperator("AutoFillQuestionnaire", true); return true;
        }

        public string ResolveFormalAvatarKey(string taskId) => resources?.FindAvatar(taskId)?.avatarPresetKey ?? string.Empty;
        public string ResolveVoiceId(string profileKey) => voiceCatalog != null && voiceCatalog.TryGet(profileKey, out var profile)
            ? profile.voiceId : string.Empty;
        public PilotPresentationProfile ResolvePilotProfile(PilotEmbodimentCondition condition)
        {
            var common = new PilotPresentationProfile { embodimentCondition = condition, feedbackActor = "assistant_agent",
                voiceProfileKey = "rehearsal_feedback_voice", volume = 1f, speakingSpeed = 1f, subtitlePolicy = "feedback_only",
                approvedForCollection = false, evidenceReference = "scenetalkvr-rehearsal-baseline-v1", assetVersion = "rehearsal-1" };
            if (condition == PilotEmbodimentCondition.VoiceOnly) { common.visualMode = PilotVisualMode.None; common.audioSourcePolicy = PilotAudioSourcePolicy.NonSpatialHeadLocked; common.spatialBlend = 0f; common.visualPrefabKey = "none"; return common; }
            common.audioSourcePolicy = PilotAudioSourcePolicy.SpatialFixedSource; common.spatialBlend = 1f; common.sourcePosition = new Vector3(.9f, condition == PilotEmbodimentCondition.FloatingOrb ? 1.45f : 0f, 1.8f);
            if (condition == PilotEmbodimentCondition.FloatingOrb) { common.visualMode = PilotVisualMode.FloatingOrb; common.visualPrefabKey = "generated_orb_v1"; return common; }
            common.visualMode = PilotVisualMode.Humanoid; common.visualPrefabKey = resources?.PilotHumanoidPresetKey ?? string.Empty;
            common.visualPrefab = resources?.PilotHumanoidPrefab; common.developerPlaceholder = false; common.scale = Vector3.one;
            common.spawnRotation = new Vector3(0, 180, 0); common.idleParameterOrState = "Idle"; common.speakingParameterOrState = "Talking"; return common;
        }

        public bool ExportBundle(out string error)
        {
            var ok = RehearsalBundleExporter.Export(Path.GetDirectoryName(CurrentDataFolder), FormalAssignment, PilotAssignment,
                protocol, resources, rankingSubmitted, interviewSaved, out lastBundlePath, out error);
            if (ok) WriteOperator("ExportBundle", detail: lastBundlePath); return ok;
        }

        public bool AuditBundle(out string error)
        {
            if (string.IsNullOrWhiteSpace(lastBundlePath) || !Directory.Exists(lastBundlePath)) { error = "rehearsal_bundle_missing"; return false; }
            var report = SessionDataIntegrityAuditor.Audit(lastBundlePath, ParticipantId, SessionId);
            SessionDataIntegrityAuditor.WriteReport(report, lastBundlePath + "-manual-audit.json");
            error = report.result.ToString().ToUpperInvariant(); WriteOperator("RunIntegrityAudit", detail: error); return report.result != DataIntegritySeverity.Fail;
        }

        public void ResetSession()
        {
            if (resetInProgress) return; resetInProgress = true;
            try
            {
                if (IsActive) WriteOperator("EndSession");
                if (formalLifecycle?.CurrentConditionAssignment != null) formalLifecycle.Abort("rehearsal_reset");
                orchestrator?.ReturnToInitialMenu(); formalLifecycle?.ClearAssignmentForRuntimeMode(); pilotWorkflow?.ClearAssignmentForRuntimeMode();
                questionnaire?.Service.Reset(); conditionManager?.ResetConditionSessionBoundary(); currentPosition = -1;
                rankingSubmitted = interviewSaved = false; RuntimeContext = null; RefreshUi();
            }
            finally { resetInProgress = false; }
        }

        private bool ValidateCommon(ExperimentFlowMode flow, out string error)
        {
            ResolveDependencies();
            if (!Application.isEditor) { error = "rehearsal_requires_unity_editor"; return false; }
            if (flow != ExperimentFlowMode.Formal && flow != ExperimentFlowMode.Pilot) { error = "rehearsal_flow_invalid"; return false; }
            if (conditionManager == null || formalLifecycle == null || pilotWorkflow == null || questionnaire == null || orchestrator == null) { error = "rehearsal_scene_bindings_missing"; return false; }
            if (protocol == null) { error = "rehearsal_protocol_missing"; return false; }
            if (!protocol.Validate(out error)) return false;
            if (resources == null || string.IsNullOrWhiteSpace(resources.ResourceSnapshotId) || voiceCatalog == null || deploymentCatalog == null) { error = "rehearsal_resources_missing"; return false; }
            if (!voiceCatalog.ValidateForRehearsal(out error)) return false;
            if (!deploymentCatalog.TryGet(ExperimentDeploymentProfileId.RehearsalEditor, out var deployment)
                || !deployment.approvedForRehearsal || !deployment.loopbackAllowedForRehearsal || deployment.collectionAllowed) { error = "rehearsal_deployment_invalid"; return false; }
            error = string.Empty; return true;
        }

        private void ResolveDependencies()
        {
            conditionManager ??= GetComponent<ExperimentConditionManager>() ?? FindFirstObjectByType<ExperimentConditionManager>();
            formalLifecycle ??= GetComponent<ExperimentLifecycleCoordinator>() ?? FindFirstObjectByType<ExperimentLifecycleCoordinator>();
            pilotWorkflow ??= GetComponent<PilotWorkflowCoordinator>() ?? FindFirstObjectByType<PilotWorkflowCoordinator>();
            questionnaire ??= GetComponent<QuestionnaireRuntimeController>() ?? FindFirstObjectByType<QuestionnaireRuntimeController>();
            orchestrator ??= GetComponent<SceneTalkOrchestrator>() ?? FindFirstObjectByType<SceneTalkOrchestrator>();
        }

        private void ApplyProtocolLimits()
        {
            if (protocol == null) return;
            formalLifecycle?.ConfigureRunLimits(protocol.FormalMaxTurns, protocol.FormalMaxDurationMinutes);
            pilotWorkflow?.ConfigureRunLimits(protocol.PilotMaxTurns, protocol.PilotMaxDurationMinutes);
        }

        private void PersistAssignments()
        {
            if (!IsActive) return; Directory.CreateDirectory(CurrentDataFolder);
            if (IsFormal && FormalAssignment != null) ExperimentAssignmentAllocator.Save(FormalAssignment, Path.Combine(CurrentDataFolder, "formal_assignment.json"));
            if (IsPilot && PilotAssignment != null) PilotAssignmentAllocator.Save(PilotAssignment, Path.Combine(CurrentDataFolder, "pilot_assignment.json"));
        }

        private void WriteOperator(string action, bool qa = false, string detail = "")
        {
            if (!IsActive) return; Directory.CreateDirectory(CurrentDataFolder);
            var value = new RehearsalOperatorEvent { timestampUtc = DateTime.UtcNow.ToString("o"), flowMode = RuntimeContext.flowMode.ToString(),
                protocolVersion = protocol.ProtocolVersion, protocolSnapshotId = RuntimeContext.protocolSnapshotId,
                resourceSnapshotId = RuntimeContext.resourceSnapshotId, participantId = ParticipantId, sessionId = SessionId,
                action = action, qaAutomationUsed = qa, actor = qa ? "rehearsal_qa_operator" : "rehearsal_operator", detail = detail ?? string.Empty };
            File.AppendAllText(Path.Combine(CurrentDataFolder, "rehearsal_operator_events.jsonl"), JsonUtility.ToJson(value) + Environment.NewLine, Encoding.UTF8);
        }

        private static bool ValidateFormalRehearsal(ExperimentAssignment value, out string error)
        {
            if (value == null || value.flowMode != ExperimentFlowMode.Formal || value.runQualification != ExperimentRunQualification.Rehearsal
                || value.dataOrigin != "rehearsal" || value.collectionEligible || value.developerTestAssignment || value.demoMode)
            { error = "formal_rehearsal_assignment_invalid"; return false; }
            error = string.Empty; return true;
        }

        private static bool ValidatePilotRehearsal(PilotAssignment value, out string error)
        {
            if (value == null || value.flowMode != ExperimentFlowMode.Pilot || value.runQualification != ExperimentRunQualification.Rehearsal
                || value.dataOrigin != "rehearsal" || value.collectionEligible || value.developerTestAssignment || value.demoMode)
            { error = "pilot_rehearsal_assignment_invalid"; return false; }
            error = string.Empty; return true;
        }

        private bool FailStart(string error) { RuntimeContext = null; RefreshUi(); return false; }
        private static bool Fail(out string error, string value) { error = value; return false; }
        private static string Safe(string value) => new string((value ?? string.Empty).Select(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' ? x : '_').ToArray());
        private void RefreshUi() => FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include)?.RefreshExternalState();
    }
}
