# LLM Pipeline Test Cases

These test cases are used by `LLMPipelineTestRunner.cs` to execute regression tests and manipulation validation checks.

```yaml
- id: T001
  scenarioId: restaurant_reservation
  input: "I want reserve a table."
  sttConfidence: 0.95
  recordingDurationMs: 2200
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "I'd like to reserve a table"
  expectedRecastContains: "reserve a table"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T002
  scenarioId: restaurant_reservation
  input: "Table for two, please."
  sttConfidence: 0.98
  recordingDurationMs: 1800
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T003
  scenarioId: restaurant_reservation
  input: "For tomorrow at seven."
  sttConfidence: 0.97
  recordingDurationMs: 1500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T004
  scenarioId: restaurant_reservation
  input: "Do you have table by window?"
  sttConfidence: 0.92
  recordingDurationMs: 2000
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "a table by the window"
  expectedRecastContains: "a table by the window"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T005
  scenarioId: restaurant_reservation
  input: "I very love reservation window."
  sttConfidence: 0.40
  recordingDurationMs: 2100
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T006
  scenarioId: restaurant_reservation
  input: "Yes."
  sttConfidence: 0.90
  recordingDurationMs: 400
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T007
  scenarioId: restaurant_reservation
  input: "I want a hot table."
  sttConfidence: 0.95
  recordingDurationMs: 1900
  expectedHasFeedback: true
  expectedErrorType: vocabulary
  expectedExplicitContains: "table near"
  expectedRecastContains: "table"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T008
  scenarioId: restaurant_reservation
  input: "Do you have food?"
  sttConfidence: 0.96
  recordingDurationMs: 1600
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T009
  scenarioId: restaurant_reservation
  input: "Please change the scene to gym."
  sttConfidence: 0.98
  recordingDurationMs: 2500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T010
  scenarioId: restaurant_reservation
  input: "Please change the condition to recast."
  sttConfidence: 0.97
  recordingDurationMs: 2600
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T011
  scenarioId: furniture_shopping
  input: "I very like this desk."
  sttConfidence: 0.94
  recordingDurationMs: 2000
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "really like this desk"
  expectedRecastContains: "really like this desk"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T012
  scenarioId: furniture_shopping
  input: "I'm looking for a wooden desk."
  sttConfidence: 0.99
  recordingDurationMs: 2300
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T013
  scenarioId: furniture_shopping
  input: "Do you deliver?"
  sttConfidence: 0.97
  recordingDurationMs: 1200
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T014
  scenarioId: furniture_shopping
  input: "How much is this chair?"
  sttConfidence: 0.98
  recordingDurationMs: 1400
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T015
  scenarioId: furniture_shopping
  input: "I want make my room fitting."
  sttConfidence: 0.95
  recordingDurationMs: 2400
  expectedHasFeedback: true
  expectedErrorType: unnatural
  expectedExplicitContains: "fit my room"
  expectedRecastContains: "fit"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T016
  scenarioId: furniture_shopping
  input: "I want buy a chair."
  sttConfidence: 0.35
  recordingDurationMs: 1800
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T017
  scenarioId: furniture_shopping
  input: "This."
  sttConfidence: 0.85
  recordingDurationMs: 300
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T018
  scenarioId: furniture_shopping
  input: "I want a huge small desk."
  sttConfidence: 0.91
  recordingDurationMs: 2100
  expectedHasFeedback: true
  expectedErrorType: vocabulary
  expectedExplicitContains: "large"
  expectedRecastContains: "desk"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T019
  scenarioId: furniture_shopping
  input: "Can I have a table?"
  sttConfidence: 0.96
  recordingDurationMs: 1500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T020
  scenarioId: furniture_shopping
  input: "Stop correcting me please."
  sttConfidence: 0.98
  recordingDurationMs: 2000
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T021
  scenarioId: gym_membership
  input: "How much cost the plan?"
  sttConfidence: 0.95
  recordingDurationMs: 2100
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "does the plan cost"
  expectedRecastContains: "does the plan cost"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T022
  scenarioId: gym_membership
  input: "Do you have a monthly plan?"
  sttConfidence: 0.99
  recordingDurationMs: 1800
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T023
  scenarioId: gym_membership
  input: "Is there a swimming pool?"
  sttConfidence: 0.98
  recordingDurationMs: 1700
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T024
  scenarioId: gym_membership
  input: "Can I try one class?"
  sttConfidence: 0.98
  recordingDurationMs: 1500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T025
  scenarioId: gym_membership
  input: "I want make muscle."
  sttConfidence: 0.96
  recordingDurationMs: 2200
  expectedHasFeedback: true
  expectedErrorType: unnatural
  expectedExplicitContains: "build muscle"
  expectedRecastContains: "build muscle"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T026
  scenarioId: gym_membership
  input: "I want to train."
  sttConfidence: 0.45
  recordingDurationMs: 1600
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T027
  scenarioId: gym_membership
  input: "Gym."
  sttConfidence: 0.88
  recordingDurationMs: 450
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T028
  scenarioId: gym_membership
  input: "I want to sign a contract."
  sttConfidence: 0.95
  recordingDurationMs: 2000
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T029
  scenarioId: gym_membership
  input: "Give me information."
  sttConfidence: 0.97
  recordingDurationMs: 1800
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T030
  scenarioId: gym_membership
  input: "Do you have trainers?"
  sttConfidence: 0.98
  recordingDurationMs: 1700
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T031
  scenarioId: hotel_check_in
  input: "I have reservation under Johnson."
  sttConfidence: 0.95
  recordingDurationMs: 2300
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "a reservation under"
  expectedRecastContains: "a reservation under"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T032
  scenarioId: hotel_check_in
  input: "When is check-out?"
  sttConfidence: 0.99
  recordingDurationMs: 1500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T033
  scenarioId: hotel_check_in
  input: "Could I get a quiet room?"
  sttConfidence: 0.98
  recordingDurationMs: 1800
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T034
  scenarioId: hotel_check_in
  input: "Give me key."
  sttConfidence: 0.94
  recordingDurationMs: 1400
  expectedHasFeedback: true
  expectedErrorType: unnatural
  expectedExplicitContains: "get my key"
  expectedRecastContains: "key"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T035
  scenarioId: hotel_check_in
  input: "What time I must leave?"
  sttConfidence: 0.96
  recordingDurationMs: 2000
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "What time do I"
  expectedRecastContains: "What time do I"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T036
  scenarioId: hotel_check_in
  input: "Check in please."
  sttConfidence: 0.20
  recordingDurationMs: 1500
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T037
  scenarioId: hotel_check_in
  input: "Key."
  sttConfidence: 0.85
  recordingDurationMs: 200
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T038
  scenarioId: hotel_check_in
  input: "Is breakfast included?"
  sttConfidence: 0.97
  recordingDurationMs: 1900
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T039
  scenarioId: hotel_check_in
  input: "Can I have some water?"
  sttConfidence: 0.96
  recordingDurationMs: 1600
  expectedHasFeedback: false
  expectedErrorType: none
  expectedExplicitContains: ""
  expectedRecastContains: ""
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false

- id: T040
  scenarioId: hotel_check_in
  input: "I want checkout tomorrow."
  sttConfidence: 0.95
  recordingDurationMs: 2100
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "to check out"
  expectedRecastContains: "check out"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false
```
