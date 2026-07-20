# Experiment v1.1 Editor Demo Pilot Run

Validated in Unity 6000.3.16f1. This run is an Editor demonstration and is never participant data.

## Run identity

- Runtime mode: `EditorDemoPilot`
- Participant: `DEMO-PILOT-VALIDATION`
- Session: `DEMO-PILOT-SESSION-20260720-112152`
- Stable sequence: `b-c-a` = Floating Orb → Humanoid Agent → Voice Only
- Tasks: `pilot_restaurant_walk_in`, `pilot_restaurant_ordering`, `pilot_restaurant_wrong_dish`, without repetition
- Shared style/voice: Explicit + `editor_demo_feedback_voice`

## Executed lifecycle

Each assignment used `PilotWorkflowCoordinator.Prepare`, `ExperimentConditionManager.ApplyPilotAssignment`, Task Catalog loading, GoalProgressTracker, task completion, `pilot_condition_v1`, condition completion, and reset. After three conditions, the existing ranking exporter produced the Pilot ranking, then the Demo Bundle exporter and integrity auditor ran.

Runtime checks:

- Voice Only: `VisualEntityType=none`, `HasVisualEntity=false`, non-spatial head-locked audio, `spatialBlend=0`.
- Floating Orb: existing `generated_orb_v1` appeared during feedback and hid after it.
- Humanoid: `teacher_female_humanoid_v1` instantiated at the explicit Pilot position and was visible only for feedback.
- Cross-condition visual reset: PASS. The generic Assistant Orb is suppressed while Pilot presentation owns visibility, so Humanoid and Voice Only do not inherit an Orb.
- Pilot task phase: fixed so the assigned restaurant task is used instead of the formal Hotel fallback.

## Evidence

- `Assets/Screenshots/editor-demo-pilot-voice-only.png`
- `Assets/Screenshots/editor-demo-pilot-orb.png`
- `Assets/Screenshots/editor-demo-pilot-humanoid.png`
- `Assets/Screenshots/editor-demo-pilot-ranking.png`
- Bundle: `Client/Library/SceneTalkVR/EditorDemoSessions/DEMO-PILOT-VALIDATION_DEMO-PILOT-SESSION-20260720-112152/bundle`
- Integrity: PASS; source hash unchanged before/after analysis: `5d97534738b1b479712cfb6852c07983601c5b83aae865603c2425a57edc2793`

The humanoid remains a Demo placeholder and is not collection-approved. Locked Pilot remains blocked by the official decisions and approved Humanoid requirement.
