# Dialogue feedback UI changes and correction-mode Settings plan

Date: 2026-07-16

## 1. Context

This note records the recent dialogue-feedback UI and flow changes, then proposes how to expose the four embodied corrective-feedback modes in the in-app `Settings` page for fast switching during PICO real-device testing.

The four existing correction modes are already represented by `ExperimentConditionManager.ExperimentConditionPreset`:

| Mode ID | Provider | Style | Runtime meaning |
| --- | --- | --- | --- |
| `dialogue_avatar_explicit` | `dialogue_avatar` | `explicit` | The roleplay Avatar gives a direct correction. |
| `dialogue_avatar_recast` | `dialogue_avatar` | `recast` | The roleplay Avatar naturally reformulates the learner's utterance. |
| `assistant_agent_explicit` | `assistant_agent` | `explicit` | A separate assistant agent gives a direct teaching correction. |
| `assistant_agent_recast` | `assistant_agent` | `recast` | A separate assistant agent gives a softer reformulation hint. |

## 2. Recent completed changes

### 2.1 Subtitle/dialogue panel overflow fix

Main file:

- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

The bottom dialogue panel previously used fixed-position text plus an auto-height subtitle container. In longer dialogue turns, the player line, Avatar line, correction text, status text, and buttons could visually overlap or spill outside the panel.

The UI was adjusted to:

- use a slightly taller subtitle panel;
- add `RectMask2D` to clip any remaining overflow inside the panel bounds;
- replace the old auto-height subtitle area with fixed text regions;
- enable best-fit scaling for dialogue/status text;
- reserve the right side for a single `Speak` / `End` button;
- keep correction feedback and status text in the left text column.

Expected effect:

- long Avatar replies are constrained inside the panel;
- correction/status text no longer collides with buttons;
- subtitles are more stable on PICO real-device display.

### 2.2 Removed `Try Again` and `Continue`

Main files:

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

Previous behavior after a correction:

```text
User speaks -> Avatar/correction plays -> Try Again / Continue choice
```

Current behavior:

```text
User speaks -> Avatar/correction plays -> Speak is available immediately
```

Implementation intent:

- no `Try Again` / `Continue` buttons are created in the dialogue panel;
- `EnterTurnReviewState()` no longer waits for `IsAwaitingTurnReviewAction`;
- after correction playback, the UI returns to the ready state and shows `Speak`.

Rationale:

- PICO testing needs a faster loop;
- `Try Again` semantics were ambiguous because the previous incorrect turn remained in LLM dialogue history;
- a single `Speak` action is simpler for live demo flow.

### 2.3 Related local project changes observed

These were also present in the working tree during the same test window:

- `Client/Assets/Resources/PXR_PicoDebuggerSO.asset`
  - `maxInfoCount` changed from `0` to `500`.
  - This affects PICO debugger log capacity, not dialogue behavior.
- `Client/ProjectSettings/EditorBuildSettings.asset`
  - removed a disabled `Assets/first_save.unity` entry.
  - `Assets/Scenes/SampleScene.unity` remains enabled.
- `Client/Assets/Settings/Mobile_RPAsset.asset`
  - no meaningful content diff was observed; likely Unity/line-ending touch.
- `Client/Packages/com.unity.xr.picoxr/Runtime/windows/x86_64/applogrs.pdb.meta`
  - generated untracked meta for a Windows PDB; should generally not be committed for the PICO Android path.

## 3. Validation performed

Code validation:

```powershell
dotnet build Client\Assembly-CSharp.csproj --no-restore -v:minimal
```

Result:

- build succeeded;
- `0` compile errors;
- existing Unity/.NET/PICO warnings remain.

Backend validation after restarting gateways:

- `voice-gateway`: `0.0.0.0:8787`
- `llm-gateway`: `0.0.0.0:8788`
- hotspot URL: `http://192.168.137.1`
- PICO candidate device observed on hotspot: `192.168.137.131`
- health checks passed:
  - `http://192.168.137.1:8787/health`
  - `http://192.168.137.1:8788/health`
- LLM smoke test returned HTTP `200`;
- Tencent TTS smoke test returned HTTP `200` with `fallbackLevel: none`.

## 4. Goal: expose four correction modes in Settings

The new requirement is to make the four correction modes switchable from the runtime `Settings` page, so testers can change condition quickly without opening the Unity Inspector or rebuilding.

Important constraint for implementation:

- Settings switching should be a debug/test convenience.
- It should not break formal experiment mode or ordered-condition logic.
- If `formalExperiment` or `useConditionOrder` is enabled, manual switching should either be hidden or clearly disabled to avoid contaminating experiment logs.

## 5. Recommended UX

Add a new row to the existing `Settings` panel:

```text
Correction Mode    [Dialogue + Explicit]    [Change]
```

Pressing `Change` cycles through:

1. `Dialogue + Explicit`
2. `Dialogue + Recast`
3. `Assistant + Explicit`
4. `Assistant + Recast`

For PICO controller use, this is better than a dropdown:

- one large button is easier to hit with a ray;
- no extra scroll/list interaction;
- matches the current Settings style (`Dialogue Subtitles` already uses a `Change` button).

Optional debug label under the row:

```text
Applies from the next turn.
```

## 6. Data model proposal

Extend `SceneTalkUserSettings` with one persisted field:

```csharp
public string correctionConditionId = "assistant_agent_explicit";
```

Add helpers to `SceneTalkUserSettingsStore`:

```csharp
public static void SetCorrectionConditionId(string conditionId);
public static void CycleCorrectionCondition();
```

Normalization should accept the same IDs as `ExperimentConditionManager`:

- `dialogue_avatar_explicit`
- `dialogue_avatar_recast`
- `assistant_agent_explicit`
- `assistant_agent_recast`

Recommended default:

- use the current scene default: `assistant_agent_explicit`

Persistence:

- use the existing `PlayerPrefs` settings JSON path;
- preserve backward compatibility: when loading old settings without this field, default to `assistant_agent_explicit`.

## 7. Runtime application proposal

Add a small public API to `ExperimentConditionManager`, for example:

```csharp
public bool CanUseManualRuntimeConditionOverride { get; }
public void SetManualConditionById(string conditionId);
public string CurrentConditionId { get; }
```

Behavior:

1. If `formalExperiment == true`, do not allow Settings override.
2. If `useConditionOrder == true`, do not allow Settings override unless a separate debug flag explicitly permits it.
3. Otherwise:
   - set `manualCondition`;
   - refresh current condition;
   - keep the current scenario/task unchanged;
   - apply provider/style to `RealLLMService` and `AvatarPresentationVoiceModule` before the next turn.

Implementation detail:

- `SceneTalkOrchestrator.ApplyExperimentConditionToModules()` already injects the current condition into the brain and Avatar voice module.
- Therefore the cleanest path is to update `ExperimentConditionManager` state, then let the orchestrator refresh modules when opening a new turn.

## 8. UI integration proposal

In `SceneTalkFlowUiController`:

Add fields:

```csharp
private Text correctionModeValueText;
private Button correctionModeChangeButton;
```

In `Build()` Settings panel:

- insert one row under `Dialogue Subtitles`, or slightly increase `settingsPanel` height if needed;
- label: `Correction Mode`;
- value text: short human-readable mode;
- button: `Change`.

Suggested display strings:

| Condition ID | Display |
| --- | --- |
| `dialogue_avatar_explicit` | `Avatar / Explicit` |
| `dialogue_avatar_recast` | `Avatar / Recast` |
| `assistant_agent_explicit` | `Assistant / Explicit` |
| `assistant_agent_recast` | `Assistant / Recast` |

On click:

```csharp
SceneTalkUserSettingsStore.CycleCorrectionCondition();
```

Then bridge the new setting to `ExperimentConditionManager`:

- either from `SceneTalkFlowUiController.OnUserSettingsChanged`;
- or from a small runtime applier method in `SceneTalkOrchestrator`;
- preferred: `SceneTalkOrchestrator` owns applying gameplay condition changes, UI only changes settings.

## 9. Turn-boundary behavior

Recommended rule:

```text
Changing mode in Settings takes effect from the next recorded turn.
```

Do not mutate a turn that is currently:

- recording;
- transcribing;
- generating LLM reply;
- playing Avatar/correction audio.

If Settings can only be opened from idle/non-running states, the current UI already avoids most race conditions. Still, the condition manager should be robust and only apply mode changes before `BeginTurn()` or before the next `ApplyExperimentConditionToModules()` call.

## 10. Logging impact

When a mode is changed through Settings:

- subsequent turn logs should use the selected `conditionId`;
- `provider` and `style` should match that selected condition;
- no special user action is required unless we want to log a debug event like `settings_condition_change`.

For formal experiments:

- hide or disable the correction-mode switch;
- preserve `conditionOrder` behavior;
- avoid accidental condition contamination.

## 11. Recommended implementation order

1. Extend `SceneTalkUserSettings` with persisted `correctionConditionId`.
2. Add condition ID normalization/cycling helpers.
3. Add manual runtime setter to `ExperimentConditionManager`.
4. Let `SceneTalkOrchestrator` apply the current settings condition before each turn when manual override is allowed.
5. Add Settings UI row and `Change` button.
6. Validate in Editor:
   - each mode changes debug label;
   - LLM prompt receives the correct provider/style;
   - Avatar/assistant feedback presenter follows provider.
7. Validate on PICO:
   - ray-click Settings mode switch;
   - run one turn per mode;
   - confirm logs show the selected condition.

## 12. Risks and decisions

Open decision:

- Should Settings mode persist across app restarts?

Recommendation:

- yes for demo/testing convenience, using `PlayerPrefs`;
- but disable or ignore it during formal experiment mode.

Risk:

- changing condition mid-session may make experiment logs less clean.

Mitigation:

- show the active condition label in debug mode;
- apply mode only from next turn;
- disable switching in formal experiment mode.

## 13. Implementation completed

Implemented on 2026-07-16.

Changed files:

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkUserSettings.cs`
  - added persisted `correctionConditionId`;
  - added condition normalization, display-name, and cycle helpers;
  - default is `assistant_agent_explicit`.
- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs`
  - added runtime manual condition override API;
  - Settings override is allowed only when `formalExperiment == false` and `useConditionOrder == false`.
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
  - applies the Settings-selected correction mode before starting a new turn;
  - does not mutate an already active turn condition;
  - re-injects provider/style into Avatar voice and LLM modules after Settings changes.
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
  - added `Correction Mode` row to Settings;
  - `Change` cycles through the four modes;
  - button shows `Locked` when formal experiment or condition-order mode prevents manual switching.

Validation:

```powershell
dotnet build Client\Assembly-CSharp.csproj --no-restore -v:minimal
```

Result:

- build succeeded;
- `0` compile errors;
- existing project warnings remain unchanged.

## 14. Follow-up fix: make mode switching affect real generation

Implemented after PICO/Unity testing showed that the Settings value could change while the actual generated dialogue still behaved like the previous mode.

Root causes:

1. The request-recording step creates an active experiment turn before scene generation. If the user changed Settings after recording but before confirming, the previous guard refused to apply the new mode because an active turn already existed.
2. `RealLLMService` creates the roleplay `chatHistory` system prompt only once at the beginning of a session. Later Settings changes updated payload metadata, but the LLM still saw the old `feedbackProvider` / `feedbackStyle` instructions in the existing system prompt.

Fixes:

- `ExperimentConditionManager.SetManualConditionById(..., updateActiveTurnLog: true)` can now update the current active turn log when the change is still before generation.
- `SceneTalkOrchestrator.ApplyCorrectionModeSetting()` and the initial confirm path now apply the Settings-selected mode to that pre-generation active turn.
- `RealLLMService` stores the active roleplay prompt context and rewrites the first `chatHistory` system message whenever the experiment condition changes, so multi-turn dialogue generation receives the new provider/style.
- `RealLLMService` logs the applied condition in Unity logs, for example:

```text
[RealLLMService] Experiment condition applied: assistant_agent_recast provider=assistant_agent, style=recast
```

Validation:

```powershell
dotnet build Client\Assembly-CSharp.csproj --no-restore -v:minimal
```

Result:

- build succeeded;
- `0` compile errors;
- existing project warnings remain unchanged.
