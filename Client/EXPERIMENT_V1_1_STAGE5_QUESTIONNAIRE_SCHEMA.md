# Stage 5 Questionnaire Schema

Catalog authority: `ExperimentQuestionnaireCatalog.asset`, version `1.1-stage5.1`.

`QuestionnaireItem` freezes questionnaire/section/item IDs, item version, order, bilingual prompts, type, required/reverse flags, explicit scale bounds, enabled state, decision dependency and choice labels.

`QuestionnaireSession` links protocol/Catalog/questionnaire versions to participant, session, sequence, condition run, linkage key, assignment policy, condition, task, technical validity and revision. Status is `NotStarted`, `InProgress`, `Submitted`, `Reopened`, `Incompatible` or `Rejected`.

`QuestionnaireResponse` is an append-only research row. Numeric enums are accompanied by `assignmentPolicy`, `formalConditionCode`, `conditionStatus` and `technicalValidity` labels. `rawValue` is never replaced by `scoredValue`.

Reverse score: `scaleMax + scaleMin - rawValue`.

Ranking requires ranks `1..N`, unique ranks, unique labels and exact label-set equality. Formal labels are `NE/NR/SE/SR`; Pilot labels are `voice_only/floating_orb/humanoid_agent`.

Draft compatibility key: `questionnaireLinkageKey + protocolVersion + questionnaireCatalogVersion`. Reopen creates a new revision and retains earlier appended response rows.
