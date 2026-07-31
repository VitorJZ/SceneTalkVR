# SceneTalkVR Editor Collection Operator Guide

1. Start the local voice gateway and verify `http://127.0.0.1:8787/health`.
2. Open Unity 6000.3.16f1 and `Assets/Scenes/SampleScene.unity`.
3. Run `SceneTalkVR > Diagnostics > Run Preflight Check`. Require `Editor Formal Collection: READY`; PICO may remain `NOT VALIDATED`.
4. Open `SceneTalkVR > Experiment > Operator Control`.
5. Enter a new participant ID and session ID, then click **Arm New Session**. Use **Resume Session** only for the same stored identity.
6. Enter Play Mode. Give control to the participant. They click Start and see four feedback modes, never task names.
7. Do not use Team Showcase, QA Auto-Fill, transcript injection or recovery shortcuts during collection. Any QA operation must mark the assignment non-collection.
8. After each task, confirm that all six goals automatically open the questionnaire. The participant answers every required 1–7 item and presses Submit, then Confirm.
9. After four conditions, the participant completes unique 1–4 rankings and the reason field.
10. In Operator Control, export the completed bundle and run Audit Last Bundle. Keep the raw bundle immutable.

For a technical failure, record the reason and mark the condition TechnicalInvalid. Retry reuses its assigned task. Experimenter Goal Confirm/Reject/Undo is available with operator identity and evidence; participant UI remains read-only.

Current fixed resources: Hotel `barista_humanoid_v1`; Furniture `teacher_humanoid_v1`; Gym `barista_male_humanoid_v1`; Tourist `teacher_female_humanoid_v1`; voice `101050`, `en-US`, 24000 Hz. PICO is a separate future deployment profile.
