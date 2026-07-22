# Experiment v1.1 Stage 1 — Core Model and Formal Mode Lock

Date: 2026-07-17

## Baseline

- Requested baseline commit: `6e4ab20`.
- Actual starting HEAD: `4330897`, the one-commit Stage 0 compile-regression repair on top of `6e4ab20`.
- Branch: `experiment-v1.1-integration`; tracking `origin/experiment-v1.1-integration` and already pushed before Stage 1 work. `main` was not merged.
- Unity Editor: `6000.3.16f1`; Unity Skills instance was available at the start of this stage.

## Implemented model

`Assets/SceneTalkVR/Scripts/Core/ExperimentCoreModel.cs` establishes the serializable core vocabulary:

- `ExperimentPhase`, `FormalConditionCode` (`NE`, `NR`, `SE`, `SR`), `FeedbackProvider`, `FeedbackStyle`, `EmbodimentCondition`, and `ExperimentTechnicalValidity`;
- `ExperimentRunContext`, `ExperimentAssignment`, and `ExperimentTaskReference`;
- `ExperimentBuildInfo`, a player-safe ScriptableObject.

`FormalConditionResolver` is the sole v1.1 mapping implementation: NE → DialogueAvatar/Explicit, NR → DialogueAvatar/Recast, SE → AssistantAgent/Explicit, SR → AssistantAgent/Recast. Legacy string IDs remain only at the `ExperimentConditionManager` compatibility boundary.

## Protocol, BuildInfo, and logging

- `ExperimentV11Protocol.asset` now identifies `1.1.0-stage1`, a typed Formal phase, its four codes, timing policy, and the unconfirmed research-decision list.
- `ExperimentBuildInfo.asset` is deliberately blank in source control and holds build commit, branch, version, timestamp, Unity version, and protocol version only after generation.
- `ExperimentBuildInfoGenerator` refreshes that asset in the Unity Editor and on `IPreprocessBuildWithReport`; no player code executes Git.
- `ExperimentConditionManager.CreateTurnLog` now writes commit/branch/build data from BuildInfo and protocol metadata from the protocol asset.

## Formal/Developer boundary

- Formal condition selection resolves through `formalCondition`; legacy Inspector order/manual strings are rejected by `AdvanceCondition`.
- Formal mode rejects manual task/scene changes through `AdvanceScenario` and `SelectTask`.
- Formal startup requires protocol plus BuildInfo and still blocks on every unconfirmed research decision.
- `SceneTalkRuntimeConfig` has a runtime Formal lock: it forces panorama-only/local fallback and disables Holodeck.
- `SceneTalkRuntimeConfigApplier` disables developer text console in Formal mode and blocks initialization for invalid protocol configuration.
- `HolodeckSceneService` rejects generation under the Formal lock rather than silently falling back to generated/mock layouts.
- Existing `CorrectionFeedbackPresenter.SetExperimentLocked` continues to clear `debugForceFeedback`; the orchestrator broadcasts this lock through `ISceneTalkExperimentLockReceiver`.

## Reset boundary

`ExperimentConditionManager.ResetConditionSessionBoundary()` resets turn/log transient state and invokes `ISceneTalkSessionReset` on scene modules. `RealLLMService` already implements that interface. This stage additionally makes Avatar voice, correction feedback, and correction agent presenters implement it, thereby clearing queued speech/audio, avatar state, correction UI, and agent visibility at the same boundary.

## Automated coverage

`Assets/SceneTalkVR/Tests/Editor/FormalConditionResolverTests.cs` covers all four provider/style mappings.

## Validation status

Before modifications, Unity Skills reported compilation success (0 errors, 0 warnings), Console errors `0`, and no missing scene references. Preflight ran and confirmed the protocol asset, SceneTalkRuntimeConfig asset, SampleScene, and Recovery build exclusion. It correctly reported Formal Mode blocked by the five unconfirmed protocol decisions.

After switching Unity Skills to Bypass, the server was restored. A malformed first BuildInfo script layout was corrected by moving `ExperimentBuildInfo` into its own Unity script file; the asset then resolved and the SampleScene reference was assigned through the Unity Editor. Post-change Unity compilation completed with Console error count `0`; missing-reference validation returned `0`; `FormalConditionResolverTests` passed `4/4` in EditMode; and an 8-second Developer Mode Play Mode observation completed with no Console errors.

A separate 5-second Formal Mode observation was run after temporarily setting `formalExperiment=true`. It produced the expected explicit startup error listing all five unconfirmed decisions. The scene was restored to `formalExperiment=false` and saved afterward. This is an expected validation error, not a residual Developer Mode failure.

## Still-unconfirmed research decisions

- a/b/c/d mapping to NE/NR/SE/SR;
- Pilot Explicit/Recast style;
- Voice Only spatial-audio definition;
- Social Comfort inclusion;
- strict without-replacement formal task allocation.

## Stage 2 inputs

1. Confirm the five protocol decisions before enabling Formal Mode.
2. Add allocator/task-catalog ownership decisions before task migration or assignment automation.
3. Run device-specific PICO validation separately; this stage includes no PICO claim.
