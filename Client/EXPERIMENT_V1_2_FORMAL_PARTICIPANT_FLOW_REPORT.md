# Experiment v1.2 Formal Participant Flow Report

The standard path no longer depends on Team Showcase Control:

```text
Operator arms identity -> Play -> Start -> choose NE/NR/SE/SR
-> preassigned task and fixed resources -> participant turns
-> automatic goals -> questionnaire -> completed mode
-> repeat remaining modes -> final ranking -> completion
```

New sessions use `flowMode=formal`, `runQualification=collection`, `dataOrigin=participant_collection`, `collectionEligible=true`, `developerTestAssignment=false`, `demoMode=false`, `synthetic=false`, and `deploymentProfile=editor_collection`. Eligibility is derived from `ExperimentRuntimeContext`, never an ID prefix.

Assignment is a stable randomized bijection persisted in `formal_assignment.json`. The observed QA run mapped `NE=tourist_assistance`, `NR=furniture_shopping`, `SE=gym_membership`, `SR=hotel_check_in`, with selection order `SR,NE,NR,SE`; all four completed exactly once and ranking opened. A separate resume run preserved `NE=gym_membership,NR=tourist_assistance,SE=hotel_check_in,SR=furniture_shopping`, the same condition run ID, and one confirmed Gym goal across Play Mode restart.

QA/recovery actions call `MarkQaAutomationUsed`, set assignment `collectionEligible=false`, and make bundle export fail with `collection_bundle_identity_invalid`. This deliberately prevents automated evidence from masquerading as participant collection.
