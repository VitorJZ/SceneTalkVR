# Experiment v1.2 Questionnaire Fix Report

## Root causes

1. `QuestionnaireVrPanel` rebuilt pages whenever a response changed. The clicked Button was destroyed during its own callback, which made selection state and subsequent input unreliable.
2. The panel did not provide a persistent selected style or a complete `CanvasGroup` raycast contract.
3. Likert controls were 112 px wide but only 44 px apart. They overlapped, so the rightmost “7” visually covered the other values even though callbacks existed.
4. Submit is intentionally two-step (`Submit`, then `Confirm`). The pre-fix flow never reliably reached the second callback because of page recreation; this looked like a stuck Submit rather than a lifecycle exception. No reproducible exception remained after the callback repair.

## Fix

Pages are rebuilt only when the questionnaire linkage changes. Responses update button color in place; `CanvasGroup.interactable` and `blocksRaycasts` stay enabled. Likert buttons are now 40 px wide on 44 px centers and the prompt column was narrowed so all 1–7 targets are independent. A PlayMode assertion checks their geometry.

Missing required items keep the questionnaire open, jump to the first missing page, and show a readable error. Successful Confirm executes persistence, scoring, `QuestionnaireSubmitted`, condition completion, panel close, input reset, runtime boundary reset, and return to mode selection. A duplicate submission is rejected. The fourth condition opens ranking instead of returning to ordinary selection.

Game View verification selected visible values 1 and 7, traversed five pages, confirmed Submit, observed `SR:Completed`, and returned to the mode panel with no Console error.
