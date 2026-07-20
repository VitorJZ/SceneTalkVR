# Formal participant-choice flow schema

Protocol `1.1-rehearsal-2` separates task assignment from execution order.

```text
Create session
  -> stable hash(participantId + protocolVersion + algorithmVersion)
  -> Fisher-Yates task permutation
  -> fixed NE/NR/SE/SR -> task bijection (hidden from participant)
  -> participant chooses any Available feedback-mode card
  -> PrepareCondition(selected code)
  -> task dialogue + validated goal detection
  -> all four goals confirmed exactly once
  -> AwaitingQuestionnaire + automatic questionnaire open
  -> submit -> return to remaining mode cards
  -> after fourth submit -> final ranking
```

Condition card states are derived from `ConditionRunStatus`: `Assigned=Available`, active boundary states=`InProgress`, `Completed=Completed`, and `TechnicalInvalid=RetryAvailable`. A second selection while a run is active is rejected. A completed condition cannot reopen. A technical retry retains its task and creates a new `conditionRunId`.

Persisted assignment fields: `formalConditionOrderPolicy`, `taskAssignmentPolicy`, `goalConfirmationPolicy`, `questionnaireReturnPolicy`, `assignmentAlgorithmVersion`, `randomSeedHash`, `participantSelectionOrder`, and per-condition `participantSelectionPosition`/`selectedAtUtc`.

Participant UI never renders `taskId`, task display name, or the condition-to-task mapping. The operator window does render the mapping, selection order, status, current run, and captured goal evidence.
