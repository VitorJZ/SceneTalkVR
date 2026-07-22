using System;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class EditorDemoOperatorEvent
    {
        public string schemaVersion = "1.0";
        public string timestampUtc;
        public string runtimeMode;
        public string dataOrigin = "editor_demo";
        public bool collectionEligible;
        public bool developerTestAssignment = true;
        public bool demoMode = true;
        public string demoProtocolVersion;
        public string officialProtocolVersion;
        public string participantId;
        public string sessionId;
        public string actor = "demo_operator";
        public string action;
        public bool autoFilledForDemo;
        public string detail;
    }

    [DisallowMultipleComponent]
    public sealed class EditorDemoSessionCoordinator : MonoBehaviour
    {
        public static EditorDemoSessionCoordinator Active { get; private set; }
        [SerializeField] private ExperimentV11EditorDemoProtocol demoProtocol;
        [SerializeField] private EditorDemoAvatarMappingCatalog avatarMapping;
        [SerializeField] private ExperimentVoiceProfileCatalog demoVoiceCatalog;
        [SerializeField] private ExperimentDeploymentCatalog demoDeploymentCatalog;
        private ExperimentConditionManager conditionManager;
        private ExperimentLifecycleCoordinator lifecycle;
        private PilotWorkflowCoordinator pilot;
        private QuestionnaireRuntimeController questionnaire;
        private SceneTalkOrchestrator orchestrator;
        private int formalPosition = -1;
        private int pilotPosition = -1;
        private bool rankingSubmitted;
        private bool interviewSaved;
        private string lastBundlePath;
        private bool resetInProgress;

        public ExperimentRuntimeMode RuntimeMode { get; private set; } = ExperimentRuntimeMode.DeveloperManual;
        public bool IsDemoMode => RuntimeMode == ExperimentRuntimeMode.EditorDemoFormal || RuntimeMode == ExperimentRuntimeMode.EditorDemoPilot;
        public bool IsFormalDemo => RuntimeMode == ExperimentRuntimeMode.EditorDemoFormal;
        public bool IsPilotDemo => RuntimeMode == ExperimentRuntimeMode.EditorDemoPilot;
        public ExperimentAssignment FormalAssignment => lifecycle?.Assignment;
        public PilotAssignment PilotAssignment => pilot?.Assignment;
        public ExperimentV11EditorDemoProtocol DemoProtocol => demoProtocol;
        public EditorDemoAvatarMappingCatalog AvatarMapping => avatarMapping;
        public ExperimentVoiceProfileCatalog DemoVoiceCatalog => demoVoiceCatalog;
        public ExperimentDeploymentCatalog DemoDeploymentCatalog => demoDeploymentCatalog;
        public string ParticipantId => IsFormalDemo ? FormalAssignment?.participantId ?? string.Empty : PilotAssignment?.participantId ?? string.Empty;
        public string SessionId => IsFormalDemo ? FormalAssignment?.experimentSessionId ?? string.Empty : PilotAssignment?.sessionId ?? string.Empty;
        public string CurrentRunId => IsFormalDemo ? lifecycle?.ConditionRunId ?? string.Empty : pilot?.PilotRunId ?? string.Empty;
        public string CurrentTaskId => IsFormalDemo ? lifecycle?.CurrentConditionAssignment?.task?.taskId ?? string.Empty : pilot?.Current?.task?.taskId ?? string.Empty;
        public int CurrentPosition => IsFormalDemo ? formalPosition : pilotPosition;
        public int TotalConditions => IsFormalDemo ? 4 : IsPilotDemo ? 3 : 0;
        public bool RankingSubmitted => rankingSubmitted;
        public bool InterviewSaved => interviewSaved;
        public string LastBundlePath => lastBundlePath ?? string.Empty;
        public string CurrentDataFolder => string.IsNullOrWhiteSpace(ParticipantId) || string.IsNullOrWhiteSpace(SessionId)
            ? DemoRoot
            : Path.Combine(DemoRoot, Safe(ParticipantId) + "_" + Safe(SessionId), "raw");
        public static string DemoRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "SceneTalkVR", "EditorDemoSessions");

        private void Awake()
        {
            Active = this;
            ResolveSceneDependencies();
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        public void Configure(ExperimentV11EditorDemoProtocol protocol, EditorDemoAvatarMappingCatalog mapping,
            ExperimentVoiceProfileCatalog voices, ExperimentDeploymentCatalog deployment)
        {
            demoProtocol = protocol; avatarMapping = mapping; demoVoiceCatalog = voices; demoDeploymentCatalog = deployment;
            ResolveSceneDependencies(); RefreshUi();
        }

        [Obsolete("Legacy Editor Demo creation is disabled. Use RehearsalSessionCoordinator.CreateFormalSession.")]
        public bool StartFormalDemo(string participantId, out string error)
        {
            error = "legacy_editor_demo_creation_disabled_create_rehearsal_session";
            return false;
        }

        [Obsolete("Legacy Editor Demo creation is disabled. Use RehearsalSessionCoordinator.CreatePilotSession.")]
        public bool StartPilotDemo(string participantId, out string error)
        {
            error = "legacy_editor_demo_creation_disabled_create_rehearsal_session";
            return false;
        }

        public bool ResumeLatest(bool formal, out string error)
        {
            error = string.Empty;
            if (!ValidateCommon(out error) || !Directory.Exists(DemoRoot)) return false;
            var fileName = formal ? "formal_assignment.json" : "pilot_assignment.json";
            var path = Directory.GetFiles(DemoRoot, fileName, SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(path)) { error = "editor_demo_assignment_not_found"; return false; }
            ResetDemoSession();
            if (formal)
            {
                var value = ExperimentAssignmentAllocator.Load(path);
                if (value == null || value.runtimeMode != ExperimentRuntimeMode.EditorDemoFormal || value.dataOrigin != "editor_demo" || value.collectionEligible) { error = "editor_demo_formal_assignment_isolation_invalid"; return false; }
                RuntimeMode = ExperimentRuntimeMode.EditorDemoFormal;
                if (!lifecycle.LoadAssignment(value, out error)) return FailStart(error);
                formalPosition = Array.FindIndex(value.conditions, x => x.status == ConditionRunStatus.Running);
            }
            else
            {
                var value = PilotAssignmentAllocator.Load(path);
                if (value == null || value.runtimeMode != ExperimentRuntimeMode.EditorDemoPilot || value.dataOrigin != "editor_demo" || value.collectionEligible) { error = "editor_demo_pilot_assignment_isolation_invalid"; return false; }
                RuntimeMode = ExperimentRuntimeMode.EditorDemoPilot;
                if (!pilot.LoadAssignment(value, out error)) return FailStart(error);
                pilotPosition = Array.FindIndex(value.conditions, x => x.status == PilotRunStatus.Running);
            }
            WriteOperator(formal ? "ResumeFormalDemo" : "ResumePilotDemo"); RefreshUi(); return true;
        }

        public bool PrepareNextCondition(out string error)
        {
            error = string.Empty;
            if (IsFormalDemo)
            {
                var next = Array.FindIndex(FormalAssignment.conditions, x => x.status == ConditionRunStatus.Assigned || x.status == ConditionRunStatus.TechnicalInvalid);
                if (next < 0) { error = "formal_demo_no_remaining_condition"; return false; }
                formalPosition = next;
                var retry = FormalAssignment.conditions[next].status == ConditionRunStatus.TechnicalInvalid;
                if (!lifecycle.PrepareCondition(next, retry, out error)) return false;
            }
            else if (IsPilotDemo)
            {
                var next = Array.FindIndex(PilotAssignment.conditions, x => x.status == PilotRunStatus.Assigned || x.status == PilotRunStatus.TechnicalInvalid);
                if (next < 0) { error = "pilot_demo_no_remaining_condition"; return false; }
                pilotPosition = next;
                var retry = PilotAssignment.conditions[next].status == PilotRunStatus.TechnicalInvalid;
                if (!pilot.Prepare(next, retry, out error)) return false;
                orchestrator.LoadAssignedTask(PilotAssignment.conditions[next].task.taskId);
            }
            else { error = "editor_demo_not_active"; return false; }
            PersistAssignments(); WriteOperator("PrepareCurrentCondition"); RefreshUi(); return true;
        }

        public bool IsTaskPrepared(string taskId)
        {
            if (!IsDemoMode || string.IsNullOrWhiteSpace(taskId)) return false;
            return string.Equals(CurrentTaskId, taskId, StringComparison.OrdinalIgnoreCase)
                && (IsFormalDemo ? lifecycle?.CurrentConditionAssignment?.status == ConditionRunStatus.Running
                    : pilot?.Current?.status == PilotRunStatus.Running);
        }

        public bool CompleteCurrentGoals(out string error)
        {
            error = string.Empty;
            if (!IsDemoMode) { error = "demo_autofill_forbidden_outside_demo"; return false; }
            var tracker = IsFormalDemo ? lifecycle.GoalTracker : pilot.Goals;
            foreach (var goal in tracker.Goals.ToArray())
            {
                if (goal.state == GoalProgressState.NotStarted || goal.state == GoalProgressState.Rejected)
                    if (!tracker.SubmitGoalCandidate(goal.goalId, "demo_operator", new GoalEvidence { turnId = "demo-auto", transcript = "autoFilledForDemo=true" }, out error)) return false;
                if (goal.state == GoalProgressState.Candidate)
                    if (!tracker.ConfirmGoal(goal.goalId, "demo_operator", "autoFilledForDemo=true", out error)) return false;
            }
            WriteOperator("CompleteCurrentGoals", true); RefreshUi(); return true;
        }

        public bool CompleteCurrentTask(out string error)
        {
            error = string.Empty;
            if (IsFormalDemo) lifecycle.CompleteTask("demo_operator_completed", "demo_operator");
            else if (IsPilotDemo) pilot.CompleteTask();
            else { error = "editor_demo_not_active"; return false; }
            WriteOperator("CompleteCurrentTask"); RefreshUi(); return true;
        }

        public bool OpenQuestionnaire(out string error)
        {
            var result = IsFormalDemo ? questionnaire.StartCurrentConditionQuestionnaire(out error)
                : IsPilotDemo ? pilot.BeginQuestionnaire(out error) : Fail(out error, "editor_demo_not_active");
            if (result) { WriteOperator("OpenQuestionnaire"); RefreshUi(); } return result;
        }

        public bool AutoFillQuestionnaire(out string error)
        {
            error = string.Empty;
            if (!IsDemoMode) { error = "demo_autofill_forbidden_outside_demo"; return false; }
            var service = IsFormalDemo ? questionnaire.Service : pilot.Questionnaire;
            if (service.ActiveSession == null || service.Definition == null) { error = "questionnaire_not_started"; return false; }
            foreach (var item in conditionManager.QuestionnaireCatalog.GetEnabledItems(service.Definition.questionnaireId, conditionManager.ExperimentProtocol))
            {
                var value = item.itemType == QuestionnaireItemType.Likert ? Mathf.Clamp(5, item.scaleMin, item.scaleMax).ToString()
                    : item.choiceValues != null && item.choiceValues.Length > 0 ? item.choiceValues[0] : "Editor demo response";
                if (!service.SetResponse(item.itemId, value, out error)) return false;
            }
            service.ActiveSession.autoFilledForDemo = true;
            WriteOperator("DemoAutoFillQuestionnaire", true); RefreshUi(); return true;
        }

        public bool SubmitQuestionnaire(out string error)
        {
            var result = IsFormalDemo ? questionnaire.Submit(out error)
                : IsPilotDemo ? pilot.SubmitQuestionnaire(out error) : Fail(out error, "editor_demo_not_active");
            if (result) { if (IsFormalDemo) lifecycle.RecordStudyEvent(StudyEventType.QuestionnaireSubmitted, "demo_operator", "autoFilledForDemo=true"); WriteOperator("SubmitQuestionnaire", true); PersistAssignments(); RefreshUi(); }
            return result;
        }

        public bool AutoFillFinalRanking(out string error)
        {
            error = string.Empty;
            if (!IsDemoMode) { error = "demo_autofill_forbidden_outside_demo"; return false; }
            if (IsFormalDemo)
            {
                var labels = new[] { "NE", "NR", "SE", "SR" };
                var response = Ranking(labels, true);
                if (!questionnaire.SubmitFormalRanking(response, out error)) return false;
            }
            else
            {
                var labels = new[] { "voice_only", "floating_orb", "humanoid_agent" };
                var response = Ranking(labels, false);
                if (!pilot.SubmitFinalRanking(response, out error)) return false;
            }
            rankingSubmitted = true; WriteOperator("DemoAutoFillRanking", true); RefreshUi(); return true;
        }

        public bool OpenFinalRanking(out string error)
        {
            error = string.Empty;
            if (!IsDemoMode) { error = "editor_demo_not_active"; return false; }
            var ui = FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include);
            if (ui == null) { error = "scene_talk_flow_ui_missing"; return false; }
            ui.ShowDemoRankingPreview(IsPilotDemo);
            WriteOperator("OpenFinalRanking"); RefreshUi(); return true;
        }

        public bool ShowPilotFeedbackVisual(out string error)
        {
            error = string.Empty;
            if (!IsPilotDemo) { error = "pilot_demo_not_active"; return false; }
            var presenter = PilotEmbodimentPresenter.Active ?? FindFirstObjectByType<PilotEmbodimentPresenter>(FindObjectsInactive.Include);
            if (presenter == null || presenter.Profile == null) { error = "pilot_presenter_not_prepared"; return false; }
            presenter.BeginFeedback();
            WriteOperator("ShowPilotFeedbackVisual", detail: presenter.VisualEntityType); RefreshUi(); return true;
        }

        public bool HidePilotFeedbackVisual(out string error)
        {
            error = string.Empty;
            if (!IsPilotDemo) { error = "pilot_demo_not_active"; return false; }
            var presenter = PilotEmbodimentPresenter.Active ?? FindFirstObjectByType<PilotEmbodimentPresenter>(FindObjectsInactive.Include);
            if (presenter == null) { error = "pilot_presenter_missing"; return false; }
            presenter.EndFeedback();
            WriteOperator("HidePilotFeedbackVisual", detail: presenter.VisualEntityType); RefreshUi(); return true;
        }

        public bool SaveDemoInterviewNote(string note, out string error)
        {
            if (!IsFormalDemo) { error = "formal_demo_required"; return false; }
            var value = new InterviewNote { protocolVersion = demoProtocol.DemoProtocolVersion,
                questionnaireCatalogVersion = conditionManager.QuestionnaireCatalog.CatalogVersion,
                participantId = ParticipantId, sessionId = SessionId, sequenceId = FormalAssignment.sequenceId,
                interviewerId = "demo_operator", interviewStartedAtUtc = DateTime.UtcNow.ToString("o"),
                interviewCompletedAtUtc = DateTime.UtcNow.ToString("o"), questionId = "editor_demo_interview",
                responseText = string.IsNullOrWhiteSpace(note) ? "Editor demonstration interview note." : note,
                notes = "dataOrigin=editor_demo; autoFilledForDemo=true" };
            if (!questionnaire.SaveInterview(value, out error)) return false;
            interviewSaved = true; WriteOperator("SaveDemoInterviewNote", true); RefreshUi(); return true;
        }

        public void MarkTechnicalInvalid(string reason)
        {
            if (IsFormalDemo) lifecycle.MarkTechnicalInvalid(reason);
            else if (IsPilotDemo) pilot.MarkTechnicalInvalid("demo_operator", reason);
            WriteOperator("MarkTechnicalInvalid", false, reason); RefreshUi();
        }

        public bool ExportSessionBundle(out string error)
        {
            var ok = EditorDemoBundleExporter.Export(Path.GetDirectoryName(CurrentDataFolder), FormalAssignment, PilotAssignment,
                demoProtocol?.DemoProtocolVersion ?? string.Empty, conditionManager?.ExperimentProtocol?.ProtocolVersion ?? string.Empty,
                rankingSubmitted, interviewSaved, out lastBundlePath, out error);
            if (ok) WriteOperator("ExportSessionBundle", false, lastBundlePath); return ok;
        }

        public bool AuditLastBundle(out string error)
        {
            if (string.IsNullOrWhiteSpace(lastBundlePath) || !Directory.Exists(lastBundlePath)) { error = "editor_demo_bundle_missing"; return false; }
            var report = SessionDataIntegrityAuditor.Audit(lastBundlePath, ParticipantId, SessionId);
            SessionDataIntegrityAuditor.WriteReport(report, lastBundlePath + "-manual-audit.json");
            error = report.result.ToString().ToUpperInvariant(); WriteOperator("RunIntegrityAudit", false, error); return report.result != DataIntegritySeverity.Fail;
        }

        public bool Retry(out string error)
        {
            var result = IsFormalDemo ? lifecycle.PrepareCondition(formalPosition, true, out error)
                : IsPilotDemo ? pilot.RetryCurrent(out error) : Fail(out error, "editor_demo_not_active");
            if (result) WriteOperator("Retry"); return result;
        }

        public string ResolveFormalAvatarKey(string taskId) => avatarMapping?.Find(taskId)?.demoAvatarKey ?? string.Empty;

        public PilotPresentationProfile ResolvePilotProfile(PilotEmbodimentCondition condition)
        {
            var common = new PilotPresentationProfile { embodimentCondition = condition, feedbackActor = "assistant_agent",
                voiceProfileKey = "editor_demo_feedback_voice", volume = 1f, speakingSpeed = 1f,
                subtitlePolicy = "feedback_only", minDistance = .2f, maxDistance = 4f,
                approvedForCollection = false, evidenceReference = "editor-demo-protocol-v1", assetVersion = "editor-demo-v1" };
            if (condition == PilotEmbodimentCondition.VoiceOnly)
            { common.visualMode = PilotVisualMode.None; common.audioSourcePolicy = PilotAudioSourcePolicy.NonSpatialHeadLocked; common.spatialBlend = 0f; common.sourcePosition = Vector3.zero; common.visualPrefabKey = "none"; return common; }
            common.audioSourcePolicy = PilotAudioSourcePolicy.SpatialFixedSource; common.spatialBlend = 1f; common.sourcePosition = new Vector3(.9f, condition == PilotEmbodimentCondition.FloatingOrb ? 1.45f : 0f, 1.8f);
            if (condition == PilotEmbodimentCondition.FloatingOrb)
            { common.visualMode = PilotVisualMode.FloatingOrb; common.visualPrefabKey = "generated_orb_v1"; common.developerPlaceholder = false; return common; }
            common.visualMode = PilotVisualMode.Humanoid; common.visualPrefabKey = avatarMapping?.PilotHumanoidPrefabKey ?? string.Empty;
            common.visualPrefab = avatarMapping?.PilotHumanoidPrefab; common.developerPlaceholder = true; common.scale = Vector3.one; common.spawnRotation = new Vector3(0, 180, 0); common.idleParameterOrState = "Idle"; common.speakingParameterOrState = "Talking"; return common;
        }

        public void ResetDemoSession()
        {
            if (resetInProgress) return;
            resetInProgress = true;
            try
            {
                if (IsDemoMode) WriteOperator("ResetDemoSession");
                if (lifecycle?.CurrentConditionAssignment != null) lifecycle.Abort("editor_demo_reset");
                orchestrator?.ReturnToInitialMenu();
                lifecycle?.ClearAssignmentForRuntimeMode(); pilot?.ClearAssignmentForRuntimeMode(); questionnaire?.Service.Reset();
                conditionManager?.ResetConditionSessionBoundary();
                formalPosition = pilotPosition = -1; rankingSubmitted = interviewSaved = false;
                RuntimeMode = ExperimentRuntimeMode.DeveloperManual; RefreshUi();
            }
            finally { resetInProgress = false; }
        }

        public void ResetSession() => ResetDemoSession();

        private bool ValidateCommon(out string error)
        {
            ResolveSceneDependencies();
            if (Application.isEditor == false) { error = "editor_demo_requires_unity_editor"; return false; }
            if (conditionManager == null || lifecycle == null || pilot == null || questionnaire == null || orchestrator == null) { error = "demo_scene_bindings_missing"; return false; }
            if (conditionManager.IsFormalExperiment) { error = "locked_collection_rejects_editor_demo"; return false; }
            if (demoProtocol == null) { error = "demo_protocol_missing"; return false; }
            if (!demoProtocol.Validate(out error)) return false;
            if (avatarMapping == null || demoVoiceCatalog == null || demoDeploymentCatalog == null) { error = "demo_assets_missing"; return false; }
            if (!demoDeploymentCatalog.TryGet(ExperimentDeploymentProfileId.EditorDemo, out var deployment)
                || !deployment.approvedForEditorDemo || deployment.approvedForCollection || deployment.collectionAllowed) { error = "editor_demo_deployment_invalid"; return false; }
            error = string.Empty; return true;
        }

        private void ResolveSceneDependencies()
        {
            conditionManager ??= GetComponent<ExperimentConditionManager>() ?? FindFirstObjectByType<ExperimentConditionManager>();
            lifecycle ??= GetComponent<ExperimentLifecycleCoordinator>() ?? FindFirstObjectByType<ExperimentLifecycleCoordinator>();
            pilot ??= GetComponent<PilotWorkflowCoordinator>() ?? FindFirstObjectByType<PilotWorkflowCoordinator>();
            questionnaire ??= GetComponent<QuestionnaireRuntimeController>() ?? FindFirstObjectByType<QuestionnaireRuntimeController>();
            orchestrator ??= GetComponent<SceneTalkOrchestrator>() ?? FindFirstObjectByType<SceneTalkOrchestrator>();
        }

        private void PersistAssignments()
        {
            Directory.CreateDirectory(CurrentDataFolder);
            if (IsFormalDemo && FormalAssignment != null) ExperimentAssignmentAllocator.Save(FormalAssignment, Path.Combine(CurrentDataFolder, "formal_assignment.json"));
            if (IsPilotDemo && PilotAssignment != null) PilotAssignmentAllocator.Save(PilotAssignment, Path.Combine(CurrentDataFolder, "pilot_assignment.json"));
        }

        private void WriteOperator(string action, bool autoFilled = false, string detail = "")
        {
            if (!IsDemoMode && string.IsNullOrWhiteSpace(ParticipantId)) return;
            Directory.CreateDirectory(CurrentDataFolder);
            var value = new EditorDemoOperatorEvent { timestampUtc = DateTime.UtcNow.ToString("o"), runtimeMode = RuntimeMode.ToString(),
                demoProtocolVersion = demoProtocol?.DemoProtocolVersion ?? string.Empty,
                officialProtocolVersion = conditionManager?.ExperimentProtocol?.ProtocolVersion ?? string.Empty,
                participantId = ParticipantId, sessionId = SessionId, action = action, autoFilledForDemo = autoFilled, detail = detail ?? string.Empty };
            File.AppendAllText(Path.Combine(CurrentDataFolder, "editor_demo_operator_events.jsonl"), JsonUtility.ToJson(value) + Environment.NewLine, Encoding.UTF8);
        }

        private PreferenceRankingResponse Ranking(string[] labels, bool formal)
        {
            var response = new PreferenceRankingResponse { protocolVersion = demoProtocol.DemoProtocolVersion,
                questionnaireCatalogVersion = conditionManager.QuestionnaireCatalog.CatalogVersion,
                participantId = ParticipantId, sessionId = SessionId,
                sequenceId = formal ? FormalAssignment.sequenceId : PilotAssignment.sequenceId,
                questionnaireId = formal ? "formal_final_v1" : "pilot_final_v1",
                rankings = labels.Select((x, i) => new PreferenceRankEntry { rank = i + 1, conditionCode = formal ? x : string.Empty, embodimentCondition = formal ? string.Empty : x }).ToArray(),
                preferredConditionCode = formal ? labels[0] : string.Empty, preferredEmbodimentCondition = formal ? string.Empty : labels[0],
                reason = "Editor demonstration auto-filled ranking; not participant data.", submittedAtUtc = DateTime.UtcNow.ToString("o") };
            return response;
        }

        private bool FailStart(string error) { RuntimeMode = ExperimentRuntimeMode.DeveloperManual; RefreshUi(); return false; }
        private static bool Fail(out string error, string value) { error = value; return false; }
        private static string Prefix(string value, string prefix, string fallback) => !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Trim() : prefix + (string.IsNullOrWhiteSpace(value) ? fallback : value.Trim());
        private static string Safe(string value) => new string((value ?? string.Empty).Select(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' ? x : '_').ToArray());
        private void RefreshUi() => FindFirstObjectByType<SceneTalkFlowUiController>()?.RefreshExternalState();
    }
}
