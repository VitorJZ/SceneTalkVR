# SceneTalkVR v1.2 Editor Collection Fix Report

## Outcome

The Editor participant flow is implemented and regression-clean: Compile PASS, Console 0 errors, EditMode 247/247, PlayMode 36/36, Python 40/40. Preflight reports `Editor Formal Collection: READY`, `Pilot Collection: READY`, and independently `PICO Deployment: NOT VALIDATED`.

Baseline branch and commit: `experiment-v1.1-integration` at `a7e482c87458b4c93a3300d41728e6b0e9b6b14e`. The implementation commit is the commit titled `fix(experiment): complete formal editor collection participant flow` (recorded by Git after this report is committed).

## Main fixes

- Added strong Editor Collection deployment/runtime identity and official `1.2.0-editor-collection` protocol metadata.
- Confirmed all 11 protocol decisions plus participant-choice, stable bijection, automatic goal, questionnaire-return and final-ranking policies.
- Added `EditorCollectionSessionCoordinator`, Operator Control, persisted assignment/resume, ranking, exporter and resource catalog.
- Rerouted Start from Developer task selection to armed formal mode selection.
- Added the 16-goal hybrid evaluator and connected it to final participant transcripts.
- Made the read-only Goal panel event-driven and run-scoped.
- Made all-goals completion open the questionnaire once and reset speech/audio/runtime safely.
- Repaired questionnaire page lifetime, selection visuals, required validation, submission lifecycle and overlapping Likert geometry.
- Added official Avatar/Agent/voice/deployment/panorama metadata and preserved Pilot behavior; fixed Pilot task loading so Formal phase validation cannot reject Pilot restaurant tasks.
- Changed analysis primary-attempt policy to `latest_valid_completed_attempt`.

## Requested answers

1. Start showed scenes because its click handler called `StartPractice`, which entered fixed task selection without an assignment.
2. It now calls `HandleParticipantStart -> BeginParticipantFlow -> FormalModeSelectionPanel`.
3. The standard path does not use Team Showcase; Showcase remains QA/recovery only.
4. Goal failure was at transcript-to-evaluator routing: the only detector was Rehearsal-gated, so the tracker never changed.
5. The Hotel name intent recognizes `My name is ...` and submits typed evidence for `reservation_name`.
6. The panel subscribes to tracker reset/progress/state/all-confirmed events and filters the current run.
7. Questionnaire responses recreated the page objects; additionally 112px Likert buttons overlapped at 44px spacing.
8. No stable Submit exception was reproducible. The apparent failure was destroyed response controls plus an uncompleted two-step Submit/Confirm lifecycle.
9. Confirm persists and scores responses, marks the condition Completed, closes the panel, resets condition presentation, and returns to modes.
10. A seeded stable bijection binds the four conditions to the four unique tasks once.
11. The serialized assignment and goal snapshot are loaded on Resume; observed mapping, run ID and progress were unchanged.
12. Protocol is `1.2.0-editor-collection`; all listed decisions are Confirmed by ProjectLead with the directive evidence.
13. Formal Avatar keys, Voice 101050, local 2048x1024 panoramas and Editor deployment are listed in the resource manifest.
14. A real new session is Formal/Collection/participant_collection/eligible, non-demo, non-synthetic and non-developer.
15. No participant bundle was generated during automated QA; integrity correctly blocked the QA-marked run.
16. The Editor flow completed four conditions, four questionnaires, ranking, completion and Resume at the final-transcript boundary; physical microphone speech still needs a human smoke run.
17. Unity 247 EditMode + 36 PlayMode and Python 40 tests passed.
18. Final Git SHA is reported by `git rev-parse HEAD` after commit and push; the functional commit has the exact requested message.

## Files and evidence

Core classes are under `Assets/SceneTalkVR/Scripts/Core`; UI is under `Scripts/Runtime`; Editor configuration is under `Scripts/Editor`; protocol assets are under `Assets/SceneTalkVR/ExperimentProtocol`. Ten required screenshots plus before-state captures are in `Client/EXPERIMENT_V1_2_EVIDENCE`.

Known external boundary: no claim of PICO validation is made. Before participant enrollment, an operator should perform one human microphone/STT run and export/audit the first genuinely collection-eligible bundle.

Accordingly, configuration Preflight is `READY`, but the release booleans remain false in the validation artifact until that human run and real bundle audit pass. This follows the directive's “only when all conditions pass” rule.
