# Pilot Editor Collection Operator Guide

1. Run `SceneTalkVR -> Experiment -> Pilot Editor Collection Preflight`; proceed only when READY.
2. Enter Play Mode and choose `Pilot Experiment`.
3. Enter a unique Participant ID; generate or enter Session ID; select Create. For an existing session use Resume—never overwrite.
4. Let the participant follow Instructions, Task Introduction, Dialogue, Questionnaire, Transition and Ranking screens. Do not use Team Showcase.
5. Monitor `SceneTalkVR -> Experiment -> Operator Control` for participant/session, sequence, position, embodiment, task, run, Goals, questionnaire, validity, gateway and data path.
6. For a genuine technical failure, mark TechnicalInvalid and Retry. Retry must keep the assignment and create a new attempt/run ID.
7. At Completion, Export Bundle and Run Integrity Audit. Require checksum and integrity PASS before copying data.
8. QA auto-fill/auto-complete tools are Advanced QA only and set `qaAutomationUsed=true`, actor `qa_operator`; do not use them for participant collection.
9. Before the first participant each day, perform a real microphone/STT/TTS/LLM smoke run and verify the selected audio input/output and network gateway.

Data root is shown in Operator Control. Never treat Developer, Demo, Synthetic or QA-automated runs as eligible collection data.
