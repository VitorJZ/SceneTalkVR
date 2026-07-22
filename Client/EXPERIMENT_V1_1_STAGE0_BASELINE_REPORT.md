# SceneTalkVR Experiment v1.1 - Stage 0 Baseline Report

## Baseline identity

- Start branch/commit: `spring-dev` / `26217dfee11b4cfad263a3618f78f934c505edfd`.
- Integration branch: `experiment-v1.1-integration`, created directly from that audited `spring-dev` commit.
- `origin/edwin-dev` points to `60c5328`; it is already merged into the start commit through `c0e9a1b`.
- `main` is intentionally untouched. No force push, squash, or merge to `main` was performed.
- Protocol source: `Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset`, version `1.1.0-stage0`.

## Dirty-worktree review and treatment

| Path/category at start | Decision | Treatment |
|---|---|---|
| `Client/Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset` | Formally commit | Preserve `useDeveloperTextConsole: 0` as reproducible non-console baseline. |
| `Client/Assets/Scenes/SampleScene.unity` | Formally commit | Preserve `manualCondition: 0` (dialogue-avatar explicit) and bind the protocol asset. |
| `Client/Assets/_Recovery/*.unity(.meta)` | Ignore | Retained on disk, added `Client/Assets/_Recovery/` gitignore rule; excluded from Build Settings. |
| `Client/EXPERIMENT_V1_1_IMPLEMENTATION_AUDIT.md` | Formally commit | Prior audit is a Stage 0 evidence input. |
| root docs/reports, `Holodeck/*`, `docs/*`, root `Packages/`, root `ProjectSettings/`, Chinese request markdown | Need human confirmation / local ignore | Retained unchanged and excluded locally from this integration commit; they are outside the Unity project root or unrelated documentation, so Stage 0 does not adopt or delete them. |

## Protocol and runtime changes

`ExperimentV11ProtocolConfig` is the unified protocol entry point. It contains:

- `protocolVersion`, `buildVersion`, `gitCommit`, `activeBranch`, `experimentPhase`, `formalModeLocked`;
- `formalConditionCodes` (`NE`, `NR`, `SE`, `SR`) and `conditionSequenceDefinitions`;
- `pilotEmbodimentOptions`, `formalTaskIds`, `pilotTaskIds`, `feedbackTimingPolicy`;
- an explicit required-decision list.

`ExperimentConditionManager` references the asset, copies protocol/build/Git/branch/phase/lock values into every JSONL and CSV turn record, and validates it before a formal run. `SceneTalkOrchestrator.ValidateGenerationModules` turns a formal validation failure into a visible configuration error rather than selecting a default.

## Formal-mode decision state

Formal Mode is intentionally **blocked** at Stage 0. The five undecided research decisions are recorded in [EXPERIMENT_V1_1_PROTOCOL_DECISIONS.md](EXPERIMENT_V1_1_PROTOCOL_DECISIONS.md). No a/b/c/d mapping, pilot style, Voice Only spatial definition, Social Comfort inclusion, or task no-replacement rule was inferred.

## Configuration single-source review

| Concern | Authoritative source at Stage 0 | Duplicate source | Runtime precedence | Drift risk | Stage 1 migration |
|---|---|---|---|---|---|
| Protocol/version/conditions | `ExperimentV11Protocol.asset` | `ExperimentConditionManager` still has legacy condition strings | Protocol for metadata/codes; manager for current provider/style | High until manager consumes code mapping | Make manager consume protocol conditions/sequences only. |
| Formal tasks | Protocol `formalTaskIds` | `CreateDefaultTasks`, `SampleScene` serialized `taskDefinitions`, task-selection UI | Manager/Scene taskDefinitions | High; Tourist exists only in protocol now | One task ScriptableObject catalog, then remove duplicate defaults/YAML. |
| Runtime configuration | `SceneTalkRuntimeConfig.asset` | `SampleScene` component values and `SceneTalkRuntimeConfig` code defaults | Runtime config applier | Medium | Keep asset only; remove behavior-critical scene fallbacks. |
| Avatar mapping | `AvatarCatalog.asset` | task fallback role strings and payload appearance | `AvatarPresetResolver.FindByScenarioId` | Medium/high | Move task-to-avatar/voice/placement into task/preset catalog. |
| Scene tasks | `SampleScene` serialized `ExperimentConditionManager.taskDefinitions` in current run | `CreateDefaultTasks` | Scene serialization overrides code default because array is non-empty | High | Delete Scene task serialization after migration to catalog. |

## Validation baseline

- Build Settings contains only `Assets/Scenes/SampleScene.unity`; no Recovery scene is included.
- Preflight now checks protocol binding/version, formal lock, RuntimeConfig binding/dirty state, Avatar catalog reference, missing scripts, required scene references, Recovery Build Settings exclusion, and active Scene dirty state.
- Expected result before research confirmation: general baseline checks can pass; the Formal Mode decision check reports blocked. This is intentional and prevents silent defaults.
- `git diff --check` passed before staging.
- Unity batch preflight was attempted with Unity `6000.3.16f1` and `SceneTalkPreflightMenu.RunPreflightCheck`, but it returned code 1 immediately because this project was already open in another Unity Editor process. It therefore did **not** compile scripts or run preflight.
- Unity C# compile and minimal Play Mode are **BLOCKED, not passed**. No PICO result is implied.

## Known risks

- Current protocol formal task list contains `tourist_assistance`, but Stage 0 deliberately does not implement that task.
- Existing `ExperimentConditionManager` uses legacy provider/style IDs and retains task defaults; protocol codes are frozen but not yet an allocator.
- The development RuntimeConfig has a loopback voice URL and is not a PICO LAN deployment configuration.
- The protocol `gitCommit` records the audited source commit, not a device-time Git invocation.

## Stage 1 entry conditions

1. Research team confirms all five protocol decisions and commits them in the protocol asset.
2. After the currently open Unity Editor is released, Stage 0 Unity compile, preflight baseline, and minimal Play Mode evidence are attached.
3. Team chooses the single authoritative task catalog migration plan.
4. Only then implement task allocation/reset, Tourist, goal tracking, questionnaires, and pilot embodiment execution in separate scoped work.
