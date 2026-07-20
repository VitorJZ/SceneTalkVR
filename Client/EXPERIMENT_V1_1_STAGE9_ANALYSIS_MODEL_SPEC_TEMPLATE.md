# Stage 9 Analysis Model Specification Template

Status: research-team review required. This template does not select a final statistical method and must not be used to infer results from Synthetic data.

## Formal 2 x 2 within-subject design

- Repeated unit: `participantId`.
- Fixed factors: `provider`, `style`, and `provider × style`.
- Candidate controls: `conditionPosition`, `taskId`, `sequenceId`, `technicalValidity`.
- Candidate outcomes: questionnaire scale score, task/goal completion, turns to completion, and event-derived latency.
- Candidate methods for approval: repeated-measures ANOVA, linear mixed-effects model, or a justified non-parametric alternative.

## Pilot three-condition within-subject design

- Repeated unit: `participantId`.
- Factor: `embodimentCondition`.
- Candidate controls: `conditionPosition`, `taskId`, `sequenceId`.
- Candidate methods for approval: repeated-measures ANOVA, mixed-effects model, Friedman test, and ordinal/ranking analysis.

## Decisions required before primary analysis

- Select the primary valid attempt policy; current value remains `UNCONFIRMED`.
- Confirm missing questionnaire handling and any scale-specific minimum answered-item rule.
- Confirm outcome hierarchy, multiplicity handling, distributional checks and planned contrasts.
- Confirm treatment of TechnicalInvalid conditions and retries.
- Confirm whether Social Comfort is enabled.

No p-value, significance statement, effect estimate, or scientific conclusion is produced by Stage 9 Synthetic validation.
