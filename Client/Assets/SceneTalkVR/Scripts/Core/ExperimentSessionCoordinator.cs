using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneTalkVR.History;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [DisallowMultipleComponent]
    public sealed class ExperimentSessionCoordinator : MonoBehaviour
    {
        public static ExperimentSessionCoordinator Active { get; private set; }

        private ExperimentConditionManager conditionManager;
        private SceneTalkOrchestrator orchestrator;
        private LearningMemoryService learningMemory;
        private ExperimentHistoryService history;
        private PilotCollectionSessionCoordinator pilot;
        private EditorCollectionSessionCoordinator formalCollection;
        private RehearsalSessionCoordinator rehearsal;
        private QuestionnaireRuntimeController formalQuestionnaire;
        private bool subscribed;
        private SceneTalkState exitReturnState = SceneTalkState.ExperimentSelection;

        public ExperimentRecordDetail CurrentExperiment { get; private set; }
        public ExperimentRecordPage CurrentHistoryPage { get; private set; }
        public ExperimentRecordDetail SelectedExperiment { get; private set; }
        public LearningSessionDetail SelectedConversation { get; private set; }
        public ExperimentQuestionnaireRecord SelectedQuestionnaire { get; private set; }
        public string ErrorMessage { get; private set; }

        public bool HasActiveExperiment => CurrentExperiment?.summary != null;
        public bool IsComplete => CurrentExperiment?.summary?.status == ExperimentRecordStatus.Completed;
        public ExperimentKind? ActiveKind => CurrentExperiment?.summary == null
            ? null
            : CurrentExperiment.summary.kind;
        public bool HasActiveConversation => HasActiveExperiment && (ActiveKind == ExperimentKind.Pilot
            ? pilot?.HasActiveDialogueCondition == true
            : formalCollection?.HasActiveDialogueCondition == true
                || rehearsal?.IsFormal == true && rehearsal.HasActiveDialogueCondition);

        private void Awake()
        {
            Active = this;
            ResolveDependencies();
            Subscribe();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (Active == this) Active = null;
        }

        public void Configure(ExperimentConditionManager manager, SceneTalkOrchestrator sceneOrchestrator)
        {
            Unsubscribe();
            conditionManager = manager;
            orchestrator = sceneOrchestrator;
            ResolveDependencies();
            Subscribe();
        }

        public bool StartNewExperiment(ExperimentKind kind, out string error)
        {
            ResolveDependencies();
            RebindSubscriptionsAfterDependencyResolution();
            if (HasAnyExperimentRuntime() || HasActiveExperiment)
            {
                error = "another_experiment_runtime_is_active";
                return false;
            }

            var token = Guid.NewGuid().ToString("N");
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var prefix = kind == ExperimentKind.Pilot ? "PILOT" : "FORMAL";
            var suffix = token.Substring(0, 6).ToUpperInvariant();
            var participantId = $"{prefix}-P-{timestamp}-{suffix}";
            var sessionId = $"{prefix}-S-{timestamp}-{suffix}";
            var assistantSnapshot = kind == ExperimentKind.Formal
                ? SceneTalkUserSettingsStore.Current.assistantEmbodiment
                : string.Empty;

            try
            {
                CurrentExperiment = history.CreateExperiment(
                    kind,
                    participantId,
                    sessionId,
                    assistantSnapshot);
                SelectedExperiment = null;
                ErrorMessage = string.Empty;
                if (StartRuntime(CurrentExperiment, false, out error)) return true;
                ClearFailedStartContext(deleteRecord: true);
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                ClearFailedStartContext(deleteRecord: true);
                Debug.LogError("[Experiment] Failed to start a new experiment: " + error, this);
                return false;
            }
        }

        public bool ContinueExperiment(string experimentId, out string error)
        {
            ResolveDependencies();
            RebindSubscriptionsAfterDependencyResolution();
            if (HasAnyExperimentRuntime() || HasActiveExperiment)
            {
                error = "another_experiment_runtime_is_active";
                return false;
            }

            CurrentExperiment = history.GetExperiment(experimentId);
            if (CurrentExperiment?.summary == null)
            {
                error = "experiment_record_missing";
                return false;
            }
            if (!CurrentExperiment.summary.CanContinue)
            {
                CurrentExperiment = null;
                error = "experiment_already_completed";
                return false;
            }

            history.ActivateExperiment(experimentId);
            history.SuspendInterruptedRuntime();
            CurrentExperiment = history.GetExperiment(experimentId);
            SelectedExperiment = null;
            if (StartRuntime(CurrentExperiment, true, out error)) return true;
            ClearFailedStartContext(deleteRecord: false);
            return false;
        }

        private bool StartRuntime(ExperimentRecordDetail experiment, bool resume, out string error)
        {
            if (experiment?.summary == null)
            {
                error = "experiment_record_missing";
                return false;
            }

            history.ActivateExperiment(experiment.summary.experimentId);
            var started = experiment.summary.kind == ExperimentKind.Pilot
                ? StartPilot(experiment.summary, resume, out error)
                : StartFormal(experiment.summary, resume, out error);
            if (!started) return false;

            history.SetStatus(
                ExperimentRecordStatus.InProgress,
                ResolveSessionRoot(experiment.summary.kind == ExperimentKind.Pilot
                    ? pilot?.CurrentDataFolder
                    : formalCollection?.IsArmed == true
                        ? formalCollection.CurrentDataFolder
                        : rehearsal?.CurrentDataFolder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(ResolveRuntimeNavigationState(experiment.summary.kind));
            error = string.Empty;
            return true;
        }

        private SceneTalkState ResolveRuntimeNavigationState(ExperimentKind kind)
        {
            if (kind == ExperimentKind.Pilot)
            {
                return pilot?.Stage == PilotParticipantStage.FinalRanking
                    ? SceneTalkState.ExperimentRanking
                    : pilot?.Stage == PilotParticipantStage.Completion
                        ? SceneTalkState.ExperimentCompleted
                        : SceneTalkState.ExperimentSelection;
            }

            var rankingVisible = formalCollection?.FinalRankingVisible == true
                || rehearsal?.FinalRankingVisible == true;
            var completed = formalCollection?.ExperimentCompleted == true
                || rehearsal?.ExperimentCompleted == true;
            return completed
                ? SceneTalkState.ExperimentCompleted
                : rankingVisible
                    ? SceneTalkState.ExperimentRanking
                    : SceneTalkState.ExperimentSelection;
        }

        private bool StartPilot(ExperimentRecordSummary record, bool resume, out string error)
        {
            conditionManager.ClearExperimentAssistantEmbodiment();
            return resume
                ? pilot.ResumeSession(record.participantId, record.sessionId, out error)
                : pilot.CreateSession(record.participantId, record.sessionId, out error);
        }

        private bool StartFormal(ExperimentRecordSummary record, bool resume, out string error)
        {
            if (!conditionManager.SetExperimentAssistantEmbodiment(record.assistantEmbodimentSnapshot))
            {
                error = "formal_assistant_embodiment_unavailable";
                return false;
            }

            bool armed;
            if (ExperimentRuntimePlatform.IsPicoDeviceValidation)
            {
                var runtimeRehearsal = rehearsal ?? SceneTalkFlowUiController.EnsureRuntimeRehearsalCoordinator();
                if (!ReferenceEquals(rehearsal, runtimeRehearsal))
                {
                    Unsubscribe();
                    rehearsal = runtimeRehearsal;
                    Subscribe();
                }
                armed = resume
                    ? rehearsal.LoadSession(ExperimentFlowMode.Formal, record.participantId, record.sessionId, out error)
                    : rehearsal.CreateFormalSession(record.participantId, record.sessionId, out error);
                if (armed && !string.Equals(
                        rehearsal.FormalAssignment?.assistantEmbodimentSnapshot,
                        record.assistantEmbodimentSnapshot,
                        StringComparison.Ordinal))
                {
                    error = "formal_assistant_embodiment_snapshot_changed";
                    armed = false;
                }
            }
            else
            {
                formalCollection.SetAssistantEmbodimentSnapshot(record.assistantEmbodimentSnapshot);
                armed = formalCollection.ArmParticipantSession(record.participantId, record.sessionId, resume, out error);
                if (armed) armed = formalCollection.BeginParticipantFlow(out error);
            }

            if (armed) return true;
            if (formalCollection?.IsArmed == true) formalCollection.EndRuntimeSession();
            if (rehearsal?.IsActive == true) rehearsal.EndRuntimeSession();
            conditionManager.ClearExperimentAssistantEmbodiment();
            return false;
        }

        public void NotifyAttemptStarted(string conditionKey, string taskId, string runId, int attemptIndex)
        {
            if (!HasActiveExperiment) return;
            history.ActivateExperiment(CurrentExperiment.summary.experimentId);
            var activeLink = history.CurrentConversationLink;
            if (activeLink?.IsValid == true && string.Equals(activeLink.runId, runId, StringComparison.Ordinal)) return;
            history.BeginAttempt(conditionKey, taskId, runId, attemptIndex);
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentPhase);
        }

        public void NotifyAttemptCompleted(string reason)
        {
            if (!HasActiveExperiment) return;
            history.CompleteAttempt(ExperimentAttemptStatus.Completed, reason);
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentSelection);
        }

        public void NotifyAttemptTechnicalInvalid(string reason)
        {
            if (!HasActiveExperiment) return;
            history.CompleteAttempt(ExperimentAttemptStatus.TechnicalInvalid, reason);
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentSelection);
        }

        public bool ReturnFromCurrentConversationToSelection(out string error)
        {
            ResolveDependencies();
            if (!HasActiveExperiment)
            {
                error = "experiment_not_active";
                return false;
            }
            if (!HasActiveConversation)
            {
                error = "experiment_conversation_not_active";
                return false;
            }

            const string reason = "participant_return_to_selection";
            bool returned;
            if (ActiveKind == ExperimentKind.Pilot)
                returned = pilot.ReturnToAppearanceSelectionFromDialogue(reason, out error);
            else if (formalCollection?.HasActiveDialogueCondition == true)
                returned = formalCollection.ReturnToModeSelectionFromDialogue(reason, out error);
            else if (rehearsal != null)
                returned = rehearsal.ReturnToConditionSelectionFromDialogue(reason, out error);
            else
            {
                error = "experiment_runtime_missing";
                return false;
            }

            if (!returned) return false;
            history.CompleteAttempt(ExperimentAttemptStatus.Suspended, reason);
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentSelection);
            error = string.Empty;
            return true;
        }

        public void NotifyRankingOpened()
        {
            if (HasActiveExperiment)
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentRanking);
        }

        public void ContinueAfterExperimentCompletion()
        {
            if (!IsComplete) return;
            EndAllRuntimes();
            ReturnHomeAndClearContext();
        }

        public void RequestLeaveExperiment()
        {
            if (!HasActiveExperiment) return;
            if (IsComplete)
            {
                ContinueAfterExperimentCompletion();
                return;
            }

            var currentState = orchestrator.CurrentState;
            exitReturnState = currentState == SceneTalkState.Idle
                || currentState == SceneTalkState.Finished
                || currentState == SceneTalkState.ExperimentExitConfirm
                    ? ResolveRuntimeNavigationState(ActiveKind.Value)
                    : currentState;
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentExitConfirm);
        }

        public void CancelLeaveExperiment()
        {
            if (HasActiveExperiment)
                orchestrator.RestoreAfterExperimentExit(exitReturnState);
        }

        public void ConfirmLeaveExperiment()
        {
            if (!HasActiveExperiment) return;
            SuspendAndEndCurrentRuntime();
            history.CompleteAttempt(ExperimentAttemptStatus.Suspended, "participant_exit_checkpoint");
            history.SetStatus(ExperimentRecordStatus.Suspended);
            ReturnHomeAndClearContext();
        }

        private void SuspendAndEndCurrentRuntime()
        {
            if (HasPilotRuntime)
                pilot.SuspendAndEndSession("participant_exit_checkpoint");
            else if (formalCollection?.IsArmed == true)
                formalCollection.SuspendAndEndRuntimeSession("participant_exit_checkpoint");
            else if (rehearsal?.IsActive == true)
                rehearsal.SuspendSession("participant_exit_checkpoint");
            orchestrator.ResetForConditionSelection();
        }

        private void EndAllRuntimes()
        {
            if (HasPilotRuntime) pilot.EndSession();
            if (formalCollection?.IsArmed == true) formalCollection.EndRuntimeSession();
            if (rehearsal?.IsActive == true) rehearsal.EndRuntimeSession();
            orchestrator.ResetForConditionSelection();
        }

        private void ClearFailedStartContext(bool deleteRecord)
        {
            var failedExperimentId = CurrentExperiment?.summary?.experimentId;
            var dataRoot = ResolveSessionRoot(ResolveActiveDataFolder());
            try
            {
                if (HasActiveExperiment && !IsComplete)
                {
                    history.ActivateExperiment(CurrentExperiment.summary.experimentId);
                    history.SetStatus(ExperimentRecordStatus.Suspended, dataRoot);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ExperimentHistory] Failed to mark an experiment start as suspended: "
                    + exception.Message, this);
            }
            EndAllRuntimes();

            if (deleteRecord && !string.IsNullOrWhiteSpace(failedExperimentId))
            {
                try
                {
                    history.ClearRuntimeContext();
                    history.DeleteExperiment(failedExperimentId, ExperimentDataRoots());
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ExperimentHistory] Failed to remove an experiment that never started: "
                        + exception.Message, this);
                }
            }

            ReturnHomeAndClearContext();
        }

        private string ResolveActiveDataFolder()
        {
            if (HasPilotRuntime) return pilot.CurrentDataFolder;
            if (formalCollection?.IsArmed == true) return formalCollection.CurrentDataFolder;
            return rehearsal?.IsActive == true ? rehearsal.CurrentDataFolder : string.Empty;
        }

        private static string[] ExperimentDataRoots() => new[]
        {
            PilotCollectionSessionCoordinator.CollectionRoot,
            EditorCollectionSessionCoordinator.CollectionRoot,
            RehearsalSessionCoordinator.RehearsalRoot,
            HistoryStoragePaths.RootPath
        };

        private void ReturnHomeAndClearContext()
        {
            conditionManager?.ExitEditorCollectionMode();
            conditionManager?.ClearExperimentAssistantEmbodiment();
            history?.ClearRuntimeContext();
            CurrentExperiment = null;
            SelectedExperiment = null;
            orchestrator.ReturnToInitialMenu();
        }

        public void OpenExperimentHistory() => LoadExperimentHistoryPage(0);

        public void LoadExperimentHistoryPage(int pageIndex)
        {
            try
            {
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryLoading);
                CurrentHistoryPage = history.GetPage(pageIndex);
                SelectedExperiment = null;
                SelectedConversation = null;
                SelectedQuestionnaire = null;
                ErrorMessage = string.Empty;
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryList);
            }
            catch (Exception exception) { EnterError(exception.Message); }
        }

        public void OpenPreviousExperimentHistoryPage() =>
            LoadExperimentHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) - 1);

        public void OpenNextExperimentHistoryPage() =>
            LoadExperimentHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) + 1);

        public void SelectExperiment(string experimentId)
        {
            try
            {
                SelectedExperiment = history.GetExperiment(experimentId);
                if (SelectedExperiment == null) throw new InvalidOperationException("The selected experiment no longer exists.");
                SelectedConversation = null;
                SelectedQuestionnaire = null;
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryActions);
            }
            catch (Exception exception) { EnterError(exception.Message); }
        }

        public void ViewSelectedExperiment()
        {
            if (SelectedExperiment != null)
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryRecord);
        }

        public void ContinueSelectedExperiment()
        {
            if (SelectedExperiment?.summary == null) return;
            var id = SelectedExperiment.summary.experimentId;
            SelectedExperiment = null;
            if (!ContinueExperiment(id, out var error)) EnterError(error);
        }

        public void SelectExperimentConversation(string sessionId)
        {
            SelectedConversation = learningMemory.GetSession(sessionId);
            if (SelectedConversation == null) { EnterError("The selected conversation no longer exists."); return; }
            if (!string.Equals(SelectedConversation.summary?.experimentId, SelectedExperiment?.summary?.experimentId, StringComparison.Ordinal))
            {
                SelectedConversation = null;
                EnterError("The selected conversation does not belong to this experiment.");
                return;
            }
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryConversationDetail);
        }

        public void SelectExperimentQuestionnaire(string questionnaireRecordId)
        {
            SelectedQuestionnaire = SelectedExperiment?.questionnaires?.FirstOrDefault(x =>
                string.Equals(x.questionnaireRecordId, questionnaireRecordId, StringComparison.Ordinal));
            if (SelectedQuestionnaire == null) { EnterError("The selected questionnaire no longer exists."); return; }
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryQuestionnaireDetail);
        }

        public void RequestDeleteSelectedExperiment()
        {
            if (SelectedExperiment != null)
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryDeleteConfirm);
        }

        public void ConfirmDeleteSelectedExperiment()
        {
            if (SelectedExperiment?.summary == null) return;
            try
            {
                if (HasAnyExperimentRuntime())
                    throw new InvalidOperationException("An experiment with an active run cannot be deleted.");
                var page = CurrentHistoryPage?.pageIndex ?? 0;
                history.DeleteExperiment(SelectedExperiment.summary.experimentId, ExperimentDataRoots());
                SelectedExperiment = null;
                LoadExperimentHistoryPage(page);
            }
            catch (Exception exception) { EnterError(exception.Message); }
        }

        public void BackFromExperimentHistory()
        {
            switch (orchestrator.CurrentState)
            {
                case SceneTalkState.ExperimentHistoryConversationDetail:
                case SceneTalkState.ExperimentHistoryQuestionnaireDetail:
                    orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryRecord);
                    break;
                case SceneTalkState.ExperimentHistoryRecord:
                case SceneTalkState.ExperimentHistoryDeleteConfirm:
                    orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryActions);
                    break;
                case SceneTalkState.ExperimentHistoryActions:
                    SelectedExperiment = null;
                    orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryList);
                    break;
                case SceneTalkState.ExperimentHistoryError:
                    ErrorMessage = string.Empty;
                    if (SelectedExperiment != null)
                        orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryActions);
                    else if (CurrentHistoryPage != null)
                        orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryList);
                    else
                        orchestrator.ReturnToInitialMenu();
                    break;
                default:
                    CurrentHistoryPage = null;
                    SelectedExperiment = null;
                    orchestrator.ReturnToInitialMenu();
                    break;
            }
        }

        private void OnPilotCompleted(PreferenceRankingResponse response)
        {
            if (!HasActiveExperiment || ActiveKind != ExperimentKind.Pilot) return;
            history.RecordRanking(response);
            history.SetStatus(ExperimentRecordStatus.Completed, ResolveSessionRoot(pilot.CurrentDataFolder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentCompleted);
        }

        private void OnFormalCompleted(PreferenceRankingResponse response)
        {
            if (!HasActiveExperiment || ActiveKind != ExperimentKind.Formal) return;
            history.RecordRanking(response);
            var folder = formalCollection?.IsArmed == true ? formalCollection.CurrentDataFolder : rehearsal?.CurrentDataFolder;
            history.SetStatus(ExperimentRecordStatus.Completed, ResolveSessionRoot(folder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentCompleted);
        }

        private void OnPilotQuestionnaireChanged(QuestionnaireSession session) => RecordQuestionnaire(session);
        private void OnFormalQuestionnaireChanged(QuestionnaireSession session) => RecordQuestionnaire(session);

        private void RecordQuestionnaire(QuestionnaireSession session)
        {
            if (!HasActiveExperiment || session == null) return;
            history.RecordQuestionnaire(
                history.CurrentConversationLink?.attemptId,
                session,
                conditionManager.QuestionnaireCatalog,
                conditionManager.ExperimentProtocol);
            RefreshCurrentExperiment();
        }

        private void RefreshCurrentExperiment()
        {
            if (CurrentExperiment?.summary == null) return;
            CurrentExperiment = history.GetExperiment(CurrentExperiment.summary.experimentId);
        }

        private void EnterError(string message)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Experiment history operation failed." : message;
            orchestrator?.SetExperimentNavigationState(SceneTalkState.ExperimentHistoryError);
            Debug.LogError("[ExperimentHistory] " + ErrorMessage, this);
        }

        private bool HasPilotRuntime => pilot != null && pilot.Stage != PilotParticipantStage.None;

        private bool HasAnyExperimentRuntime() => HasPilotRuntime
            || formalCollection?.IsArmed == true
            || rehearsal?.IsActive == true;

        private static string ResolveSessionRoot(string rawFolder)
        {
            if (string.IsNullOrWhiteSpace(rawFolder)) return string.Empty;
            var full = Path.GetFullPath(rawFolder);
            return string.Equals(Path.GetFileName(full), "raw", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(full)?.FullName ?? full
                : full;
        }

        private void ResolveDependencies()
        {
            conditionManager ??= GetComponent<ExperimentConditionManager>()
                ?? FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            orchestrator ??= GetComponent<SceneTalkOrchestrator>()
                ?? FindFirstObjectByType<SceneTalkOrchestrator>(FindObjectsInactive.Include);
            learningMemory ??= GetComponent<LearningMemoryService>()
                ?? (conditionManager == null ? null : conditionManager.gameObject.GetComponent<LearningMemoryService>()
                    ?? conditionManager.gameObject.AddComponent<LearningMemoryService>());
            history ??= GetComponent<ExperimentHistoryService>()
                ?? (conditionManager == null ? null : conditionManager.gameObject.GetComponent<ExperimentHistoryService>()
                    ?? conditionManager.gameObject.AddComponent<ExperimentHistoryService>());
            pilot ??= PilotCollectionSessionCoordinator.Active
                ?? GetComponent<PilotCollectionSessionCoordinator>()
                ?? FindFirstObjectByType<PilotCollectionSessionCoordinator>(FindObjectsInactive.Include);
            formalCollection ??= EditorCollectionSessionCoordinator.Active
                ?? GetComponent<EditorCollectionSessionCoordinator>()
                ?? FindFirstObjectByType<EditorCollectionSessionCoordinator>(FindObjectsInactive.Include);
            rehearsal ??= RehearsalSessionCoordinator.Active
                ?? GetComponent<RehearsalSessionCoordinator>()
                ?? FindFirstObjectByType<RehearsalSessionCoordinator>(FindObjectsInactive.Include);
            formalQuestionnaire ??= GetComponent<QuestionnaireRuntimeController>()
                ?? FindFirstObjectByType<QuestionnaireRuntimeController>(FindObjectsInactive.Include);
        }

        private void RebindSubscriptionsAfterDependencyResolution()
        {
            if (!subscribed) return;
            Unsubscribe();
            Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            ResolveDependencies();
            if (pilot != null) pilot.ExperimentCompleted += OnPilotCompleted;
            if (formalCollection != null) formalCollection.ExperimentCompletedWithRanking += OnFormalCompleted;
            if (rehearsal != null) rehearsal.ExperimentCompletedWithRanking += OnFormalCompleted;
            if (pilot?.Workflow?.Questionnaire != null)
                pilot.Workflow.Questionnaire.SessionChanged += OnPilotQuestionnaireChanged;
            if (formalQuestionnaire != null)
                formalQuestionnaire.QuestionnaireChanged += OnFormalQuestionnaireChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (pilot != null) pilot.ExperimentCompleted -= OnPilotCompleted;
            if (formalCollection != null) formalCollection.ExperimentCompletedWithRanking -= OnFormalCompleted;
            if (rehearsal != null) rehearsal.ExperimentCompletedWithRanking -= OnFormalCompleted;
            if (pilot?.Workflow?.Questionnaire != null)
                pilot.Workflow.Questionnaire.SessionChanged -= OnPilotQuestionnaireChanged;
            if (formalQuestionnaire != null)
                formalQuestionnaire.QuestionnaireChanged -= OnFormalQuestionnaireChanged;
            subscribed = false;
        }
    }
}
