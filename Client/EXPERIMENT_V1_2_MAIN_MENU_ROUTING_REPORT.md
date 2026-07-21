# Experiment v1.2 Main Menu Routing Report

## Root cause

The original `SceneTalkFlowUiController` Start binding called `SceneTalkOrchestrator.StartPractice()`. In fixed mode that set `SceneTalkState.AwaitingTaskSelection`, so `BuildTaskButtons()` exposed the four formal scenes. The only code that prepared a condition assignment and mode panel was the Team Showcase/Rehearsal coordinator; therefore the participant path depended on an operator QA surface.

## Current routing

```text
Operator Control: Arm New/Resume Session
  -> SceneTalkOperatorControlWindow stores participant/session identity
  -> EnteredPlayMode configures EditorCollectionSessionCoordinator
  -> ArmParticipantSession creates or restores one stable assignment

Game View StartButton
  -> SceneTalkFlowUiController.HandleParticipantStart
  -> EditorCollectionSessionCoordinator.BeginParticipantFlow
  -> FormalModeSelectionPanel (NE/NR/SE/SR)
  -> participant mode button
  -> EditorCollectionSessionCoordinator.SelectFormalCondition
  -> ExperimentLifecycleCoordinator.PrepareCondition
  -> SceneTalkOrchestrator.LoadAssignedTask(preassigned taskId)
```

If the coordinator is not armed, Start shows `SessionNotPreparedPanel` with the required participant-safe message. It never opens `TaskSelectionPanel`. `ShowDeveloperTaskSelectionForQa()` remains an explicit QA-only entry and is not bound to standard Start.

The mapping is created once by `ExperimentAssignmentAllocator.TryCreateEditorCollection`, persisted immediately, and loaded by `ArmParticipantSession(..., resume:true)`. Mode selection only looks up the stored mapping. Completed conditions are rejected, double selection is guarded, and retries reuse the stored task.

Evidence: `formal-main-menu.png`, `formal-mode-selection.png`, and `formal-mode-selection-one-completed.png` in `Client/EXPERIMENT_V1_2_EVIDENCE`.
