# Pilot Embodiment Validation

| Condition | Resource | Visual lifecycle | Audio |
|---|---|---|---|
| Voice Only | none | no visual object created | head-locked, `spatialBlend=0` |
| Floating Orb | `generated_orb_v1` | show on feedback start; hide on feedback end/reset | shared collection feedback voice |
| Humanoid | `teacher_female_humanoid_v1` | show on feedback start; hide on feedback end/reset; never coexists with Orb | shared collection feedback voice |

The restaurant dialogue Avatar is constant across all conditions. Tests verify Explicit style, identical feedback hash/voice profile/voiceId/speed/volume and Feedback First ordering. Humanoid failure is TechnicalInvalid and does not fall back to Orb. Voice Only is a genuine assigned condition, not a fallback.

Manual Sequence A validation confirmed no Voice Only visual, Orb lifecycle cleanup, and Humanoid with Orb inactive. Current approved visual quality is not a Preflight blocker.
