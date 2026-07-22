# Pilot Participant Flow

`Main Menu -> Pilot Experiment -> Session Setup -> Instructions -> Task Introduction -> Dialogue -> Goal 4/4 -> Condition Questionnaire -> Neutral Transition -> next condition -> final Questionnaire -> Embodiment Ranking -> Completion`.

Session Setup validates trimmed Participant ID, rejects invalid path characters and prevents silent overwrite. Assignment is generated and persisted at Create. Participants never choose embodiment, task, sequence, Avatar or Voice.

Condition Continue calls `ResetPilotConditionBoundary`, applies the assignment, loads the restaurant panorama/dialogue Avatar/task, initializes four Goals and starts the approved Opening Question. Formal and Pilot routes are independent, and Pilot never calls Team Showcase or Developer Task Selection.
