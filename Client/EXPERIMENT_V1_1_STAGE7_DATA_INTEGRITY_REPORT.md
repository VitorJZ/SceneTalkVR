# Experiment v1.1 Stage 7 — Data Integrity Audit

Status: `WARNING` — audit tooling implemented; no genuine complete formal or pilot session dataset was supplied for acceptance.

## Tool

- Core reader: `Assets/SceneTalkVR/Scripts/Core/SessionDataIntegrityAuditor.cs`
- Editor entry: `SceneTalkVR > Diagnostics > Session Data Integrity Audit`
- UI: `Assets/SceneTalkVR/Scripts/Editor/SessionDataIntegrityAuditWindow.cs`
- Output values: `PASS`, `WARNING`, `FAIL`

The auditor reads Assignment JSON plus JSON/JSONL records recursively. It checks participant/session consistency, Assignment presence, unique condition starts, linkage-to-task consistency, duplicate valid questionnaire submission, TechnicalInvalid completion misuse, monotonic turn events, and feedback-before-dialogue ordering. It writes only a separate report chosen by the operator; raw input files are never changed.

## Automated evidence

`Stage7ReleaseReadinessTests.IntegrityAuditor_DetectsDialogueBeforeFeedback_AndDoesNotModifyInput` creates controlled temporary data, verifies a dialogue-before-feedback record produces `FAIL`, and verifies the source bytes remain unchanged. Project-only test execution is pending UnitySkills Bypass availability in this run.

## Still required on real data

- Formal four-condition completed session and Pilot three-embodiment completed/retry session.
- Goal trace, ranking completeness/uniqueness and closed-condition evidence across their actual export files.
- Cross-file reconciliation of Timing summary metrics against raw Stage 3 events.
- Assignment snapshot, Timing JSONL, Study JSONL and Questionnaire JSONL joined by actual run/linkage keys.

No production data was altered and no real-session `PASS` is claimed.
