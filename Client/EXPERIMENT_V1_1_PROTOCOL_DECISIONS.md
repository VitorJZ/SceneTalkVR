# Experiment v1.1 Protocol Decisions

Protocol asset: `Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset`
Protocol version: `1.1.0-stage0`

## Frozen in Stage 0

- Formal condition codes are `NE`, `NR`, `SE`, and `SR`.
- Their semantic labels are respectively Non-Split Explicit, Non-Split Recast, Split Explicit, and Split Recast.
- Feedback timing policy is `feedback_first_then_dialogue`; dialogue text/TTS may be prepared before the playback gate opens.
- The protocol asset is the unique runtime source for protocol/build/Git metadata and formal condition codes.
- Formal Mode is locked. It cannot start until every required decision below is marked `Confirmed` in the protocol asset.

## Required but Unconfirmed Decisions

| Decision ID | Decision required | Stage 0 status | Runtime effect |
|---|---|---|---|
| `condition_letter_mapping` | Map a/b/c/d to NE/NR/SE/SR. | Unconfirmed | Formal Mode blocks. |
| `pilot_feedback_style` | Fix Explicit/Recast policy for pilot. | Unconfirmed | Formal Mode blocks; no pilot behavior is inferred. |
| `voice_only_spatial_audio` | Define Voice Only spatial/non-spatial routing and source position. | Unconfirmed | Formal Mode blocks; no audio fallback is reclassified as Voice Only. |
| `formal_social_comfort` | Decide whether Social Comfort enters formal questionnaire. | Unconfirmed | Formal Mode blocks; no questionnaire feature is added. |
| `formal_task_no_replacement` | Decide whether formal task assignment is strictly without replacement. | Unconfirmed | Formal Mode blocks; no assignment policy is inferred. |

## Confirmation Procedure

1. Obtain a research-team decision with an unambiguous value.
2. Set the corresponding `requiredDecisions[].status` to `Confirmed` and fill `confirmedValue` in `ExperimentV11Protocol.asset`.
3. Add only approved sequence definitions to `conditionSequenceDefinitions`.
4. Run `SceneTalkVR/Diagnostics/Run Preflight Check` and confirm Formal Mode validation passes.
5. Commit the asset change with the research decision reference. Do not silently introduce a fallback default.
