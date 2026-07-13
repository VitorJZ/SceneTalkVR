# LLM Pipeline Manipulation Check Report

This report serves as the official validation record for the 2x2 experimental conditions (Dialogue Avatar vs. Assistant Agent, Explicit vs. Recast) under the Manipulation Validity Sprint.

## Run Status
- **Status**: Ready to Run
- **Test Runner Location**: Unity Editor Menu -> `SceneTalkVR/Diagnostics/Run LLM Manipulation Check`
- **Total Test Cases**: 40 Cases (covering Restaurant Reservation, Furniture Shopping, Gym Membership, Hotel Check-in)
- **Total Executed Variations**: 160 Variations (4 conditions per case)

## Instructions for Execution
1. Open the SceneTalkVR main practice scene in the Unity Editor.
2. Select `SceneTalkVR` -> `Diagnostics` -> `Run LLM Manipulation Check` from the top menu bar.
3. Click the **Run Test Suite & Generate Report** button in the test runner window.
4. The test runner will automatically:
   - Cycle through all 40 test cases.
   - Run each case under the 4 experimental conditions.
   - Perform STT suppression checks (<500ms duration or <0.5 confidence).
   - Guard against dialogue reply corrective leakage under `assistant_agent` provider.
   - Guard against recast purity violations under `recast` style.
   - Cleanly overwrite this report file with the exact run results, metrics, and details.

## Anticipated Metrics
- **Pass Rate Target**: ≥ 90%
- **JSON Parse Success Rate Target**: ≥ 99%
- **Assistant Dialogue Leakage Count Target**: 0
- **Recast Purity Violation Count Target**: 0
- **STT Suppression Pass Rate**: 100%
- **Enriched Logging Fields Compliance**: 100%
