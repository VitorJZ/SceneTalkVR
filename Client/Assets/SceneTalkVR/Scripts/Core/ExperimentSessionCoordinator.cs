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

        public ExperimentRecordDetail CurrentExperiment { get; private set; }
        public ExperimentRecordPage CurrentHistoryPage { get; private set; }
        public ExperimentRecordDetail SelectedExperiment { get; private set; }
        public LearningSessionDetail SelectedConversation { get; private set; }
        public ExperimentQuestionnaireRecord SelectedQuestionnaire { get; private set; }
        public string ErrorMessage { get; private set; }

        public bool HasActiveExperiment => CurrentExperiment?.summary != null;
        public bool IsComplete => CurrentExperiment?.summary?.status == ExperimentRecordStatus.Completed;
        public bool CanEnterPilot => HasActiveExperiment
            && CurrentExperiment.summary.pilotStatus != ExperimentPhaseStatus.Completed;
        public bool CanEnterFormal => HasActiveExperiment
            && CurrentExperiment.summary.pilotStatus == ExperimentPhaseStatus.Completed
            && CurrentExperiment.summary.formalStatus != ExperimentPhaseStatus.Completed
            && !string.IsNullOrWhiteSpace(ResolvePreferredAssistantEmbodiment(
                CurrentExperiment.summary.preferredEmbodiment));

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

        private void OnDisable()
        {
            Unsubscribe();
        }

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

        public bool StartNewExperiment(out string error)
        {
            ResolveDependencies();
            if (HasAnyPhaseRuntime())
            {
                error = "another_experiment_runtime_is_active";
                return false;
            }

            var token = Guid.NewGuid().ToString("N");
            var participantId = $"EXP-P-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{token.Substring(0, 6).ToUpperInvariant()}";
            try
            {
                CurrentExperiment = history.CreateExperiment(participantId);
                SelectedExperiment = null;
                ErrorMessage = string.Empty;
                orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentMenu);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                EnterError(error);
                return false;
            }
        }

        public bool ContinueExperiment(string experimentId, out string error)
        {
            ResolveDependencies();
            if (HasAnyPhaseRuntime())
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
                error = "experiment_already_completed";
                return false;
            }
            history.ActivateExperiment(experimentId);
            history.SuspendInterruptedRuntime();
            CurrentExperiment = history.GetExperiment(experimentId);
            SelectedExperiment = null;
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentMenu);
            error = string.Empty;
            return true;
        }

        public bool EnterPilot(out string error)
        {
            RefreshCurrentExperiment();
            if (!CanEnterPilot)
            {
                error = "pilot_phase_unavailable";
                return false;
            }
            conditionManager.ClearExperimentAssistantEmbodiment();
            var phase = GetPhase(ExperimentPhaseKind.Pilot);
            var resume = phase.status != ExperimentPhaseStatus.NotStarted;
            if (!(resume
                    ? pilot.ResumeSession(CurrentExperiment.summary.participantId, phase.sessionId, out error)
                    : pilot.CreateSession(CurrentExperiment.summary.participantId, phase.sessionId, out error)))
                return false;

            history.ActivateExperiment(CurrentExperiment.summary.experimentId);
            history.SetPhaseStatus(ExperimentPhaseKind.Pilot, ExperimentPhaseStatus.InProgress, ResolveSessionRoot(pilot.CurrentDataFolder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentPhase);
            error = string.Empty;
            return true;
        }

        public bool EnterFormal(out string error)
        {
            RefreshCurrentExperiment();
            if (!CanEnterFormal)
            {
                error = "formal_phase_locked_until_pilot_completion";
                return false;
            }
            if (!ApplyPilotEmbodiment(CurrentExperiment.summary.preferredEmbodiment, out error)) return false;

            var phase = GetPhase(ExperimentPhaseKind.Formal);
            var resume = phase.status != ExperimentPhaseStatus.NotStarted;
            bool armed;
            string dataFolder;
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
                    ? rehearsal.LoadSession(ExperimentFlowMode.Formal, CurrentExperiment.summary.participantId, phase.sessionId, out error)
                    : rehearsal.CreateFormalSession(CurrentExperiment.summary.participantId, phase.sessionId, out error);
                dataFolder = rehearsal?.CurrentDataFolder;
            }
            else
            {
                armed = formalCollection.ArmParticipantSession(
                    CurrentExperiment.summary.participantId, phase.sessionId, resume, out error);
                if (armed) armed = formalCollection.BeginParticipantFlow(out error);
                dataFolder = formalCollection?.CurrentDataFolder;
            }
            if (!armed)
            {
                if (formalCollection?.IsArmed == true) formalCollection.EndRuntimeSession();
                if (rehearsal?.IsActive == true) rehearsal.EndRuntimeSession();
                conditionManager.ClearExperimentAssistantEmbodiment();
                return false;
            }

            history.ActivateExperiment(CurrentExperiment.summary.experimentId);
            history.SetPhaseStatus(ExperimentPhaseKind.Formal, ExperimentPhaseStatus.InProgress, ResolveSessionRoot(dataFolder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentPhase);
            error = string.Empty;
            return true;
        }

        public void NotifyAttemptStarted(
            ExperimentPhaseKind phase,
            string conditionKey,
            string taskId,
            string runId,
            int attemptIndex)
        {
            if (!HasActiveExperiment) return;
            history.ActivateExperiment(CurrentExperiment.summary.experimentId);
            var activeLink = history.CurrentConversationLink;
            if (activeLink?.IsValid == true
                && activeLink.phase == phase
                && string.Equals(activeLink.runId, runId, StringComparison.Ordinal))
            {
                return;
            }
            history.BeginAttempt(phase, conditionKey, taskId, runId, attemptIndex);
            RefreshCurrentExperiment();
        }

        public void NotifyAttemptCompleted(string reason)
        {
            if (!HasActiveExperiment) return;
            history.CompleteAttempt(ExperimentAttemptStatus.Completed, reason);
            RefreshCurrentExperiment();
        }

        public void NotifyAttemptTechnicalInvalid(string reason)
        {
            if (!HasActiveExperiment) return;
            history.CompleteAttempt(ExperimentAttemptStatus.TechnicalInvalid, reason);
            RefreshCurrentExperiment();
        }

        public void ContinueAfterPhaseCompletion()
        {
            if (pilot?.Stage == PilotParticipantStage.Completion) pilot.EndSession();
            if (formalCollection?.IsArmed == true) formalCollection.EndRuntimeSession();
            if (rehearsal?.IsActive == true) rehearsal.EndRuntimeSession();
            orchestrator.ResetForConditionSelection();
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentMenu);
        }

        public void ExitCurrentPhaseToMenu()
        {
            if (!HasActiveExperiment) return;
            var phase = pilot != null && pilot.Stage != PilotParticipantStage.None
                ? ExperimentPhaseKind.Pilot
                : rehearsal?.IsPilot == true
                    ? ExperimentPhaseKind.Pilot
                    : ExperimentPhaseKind.Formal;
            if (pilot != null && pilot.Stage != PilotParticipantStage.None)
                pilot.SuspendAndEndSession("participant_exit_checkpoint");
            else if (formalCollection?.IsArmed == true)
                formalCollection.SuspendAndEndRuntimeSession("participant_exit_checkpoint");
            else if (rehearsal?.IsActive == true)
                rehearsal.SuspendSession("participant_exit_checkpoint");
            history.CompleteAttempt(ExperimentAttemptStatus.Suspended, "participant_exit_checkpoint");
            if (GetPhase(phase).status != ExperimentPhaseStatus.Completed)
                history.SetPhaseStatus(phase, ExperimentPhaseStatus.Suspended);
            RefreshCurrentExperiment();
            orchestrator.ResetForConditionSelection();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentMenu);
        }

        public void RequestLeaveExperiment()
        {
            if (!HasActiveExperiment) return;
            if (IsComplete) LeaveExperiment(false);
            else orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentExitConfirm);
        }

        public void CancelLeaveExperiment()
        {
            if (HasActiveExperiment) orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentMenu);
        }

        public void ConfirmLeaveExperiment()
        {
            LeaveExperiment(true);
        }

        private void LeaveExperiment(bool suspendActivePhase)
        {
            if (suspendActivePhase && HasAnyPhaseRuntime()) ExitCurrentPhaseToMenu();
            conditionManager?.ClearExperimentAssistantEmbodiment();
            history?.ClearRuntimeContext();
            CurrentExperiment = null;
            SelectedExperiment = null;
            orchestrator.ResetForConditionSelection();
        }

        public void OpenExperimentHistory()
        {
            LoadExperimentHistoryPage(0);
        }

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

        public void OpenPreviousExperimentHistoryPage()
        {
            LoadExperimentHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) - 1);
        }

        public void OpenNextExperimentHistoryPage()
        {
            LoadExperimentHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) + 1);
        }

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
            if (!ContinueExperiment(SelectedExperiment.summary.experimentId, out var error)) EnterError(error);
        }

        public void SelectExperimentConversation(string sessionId)
        {
            SelectedConversation = learningMemory.GetSession(sessionId);
            if (SelectedConversation == null) { EnterError("The selected conversation no longer exists."); return; }
            if (!string.Equals(
                    SelectedConversation.summary?.experimentId,
                    SelectedExperiment?.summary?.experimentId,
                    StringComparison.Ordinal))
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
                if (HasAnyPhaseRuntime())
                    throw new InvalidOperationException("An experiment with an active run cannot be deleted.");
                var page = CurrentHistoryPage?.pageIndex ?? 0;
                var roots = new[]
                {
                    PilotCollectionSessionCoordinator.CollectionRoot,
                    EditorCollectionSessionCoordinator.CollectionRoot,
                    RehearsalSessionCoordinator.RehearsalRoot,
                    HistoryStoragePaths.RootPath
                };
                history.DeleteExperiment(SelectedExperiment.summary.experimentId, roots);
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
                        orchestrator.ResetForConditionSelection();
                    break;
                default:
                    CurrentHistoryPage = null;
                    SelectedExperiment = null;
                    orchestrator.ResetForConditionSelection();
                    break;
            }
        }

        public bool ApplyPilotEmbodiment(string preferred, out string error)
        {
            var mapped = ResolvePreferredAssistantEmbodiment(preferred);
            if (string.IsNullOrWhiteSpace(mapped))
            {
                error = "pilot_preferred_embodiment_invalid";
                return false;
            }
            if (!conditionManager.SetExperimentAssistantEmbodiment(mapped))
            {
                error = "formal_assistant_embodiment_unavailable";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static string ResolvePreferredAssistantEmbodiment(string preferred) => preferred switch
        {
            "voice_only" => ExperimentConditionManager.AudioOnlyAssistantEmbodiment,
            "floating_orb" => ExperimentConditionManager.OrbAssistantEmbodiment,
            "humanoid_agent" => ExperimentConditionManager.HumanoidAssistantEmbodiment,
            _ => string.Empty
        };

        private void OnPilotCompleted(PreferenceRankingResponse response)
        {
            if (!HasActiveExperiment) return;
            history.RecordRanking(ExperimentPhaseKind.Pilot, response);
            history.SetPhaseStatus(ExperimentPhaseKind.Pilot, ExperimentPhaseStatus.Completed, ResolveSessionRoot(pilot.CurrentDataFolder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentPhaseCompleted);
        }

        private void OnFormalCompleted(PreferenceRankingResponse response)
        {
            if (!HasActiveExperiment) return;
            history.RecordRanking(ExperimentPhaseKind.Formal, response);
            var folder = formalCollection?.IsArmed == true ? formalCollection.CurrentDataFolder : rehearsal?.CurrentDataFolder;
            history.SetPhaseStatus(ExperimentPhaseKind.Formal, ExperimentPhaseStatus.Completed, ResolveSessionRoot(folder));
            RefreshCurrentExperiment();
            orchestrator.SetExperimentNavigationState(SceneTalkState.ExperimentPhaseCompleted);
        }

        private void OnPilotQuestionnaireChanged(QuestionnaireSession session)
        {
            RecordQuestionnaire(ExperimentPhaseKind.Pilot, session);
        }

        private void OnFormalQuestionnaireChanged(QuestionnaireSession session)
        {
            RecordQuestionnaire(ExperimentPhaseKind.Formal, session);
        }

        private void RecordQuestionnaire(ExperimentPhaseKind phase, QuestionnaireSession session)
        {
            if (!HasActiveExperiment || session == null) return;
            history.RecordQuestionnaire(
                phase,
                history.CurrentConversationLink?.attemptId,
                session,
                conditionManager.QuestionnaireCatalog,
                conditionManager.ExperimentProtocol);
            RefreshCurrentExperiment();
        }

        private ExperimentPhaseRecord GetPhase(ExperimentPhaseKind phase) =>
            CurrentExperiment.phases.First(x => x.phase == phase);

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

        private bool HasAnyPhaseRuntime() => pilot?.Stage != PilotParticipantStage.None
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
