# LLM Pipeline Manipulation Check Report
Date: 2026/7/14 22:02
Total Test Cases: 5
Total Executed Variations: 20

## Summary Metrics
- **Pass Rate**: 100.0% (20/20 passed)
- **JSON Parse Success Rate**: 100.0% (20/20 parsed)
- **Assistant Dialogue Leakage Count**: 0
- **Recast Purity Violation Count**: 0

## Detailed Test Results

| Case ID | Condition | Input | Result | Details |
|---|---|---|---|---|
| T001 | dialogue_avatar_explicit | I want reserve a table. | ✅ PASS |  |
| T001 | dialogue_avatar_recast | I want reserve a table. | ✅ PASS |  |
| T001 | assistant_agent_explicit | I want reserve a table. | ✅ PASS |  |
| T001 | assistant_agent_recast | I want reserve a table. | ✅ PASS |  |
| T002 | dialogue_avatar_explicit | Table for two, please. | ✅ PASS |  |
| T002 | dialogue_avatar_recast | Table for two, please. | ✅ PASS |  |
| T002 | assistant_agent_explicit | Table for two, please. | ✅ PASS |  |
| T002 | assistant_agent_recast | Table for two, please. | ✅ PASS |  |
| T003 | dialogue_avatar_explicit | For tomorrow at seven. | ✅ PASS |  |
| T003 | dialogue_avatar_recast | For tomorrow at seven. | ✅ PASS |  |
| T003 | assistant_agent_explicit | For tomorrow at seven. | ✅ PASS |  |
| T003 | assistant_agent_recast | For tomorrow at seven. | ✅ PASS |  |
| T004 | dialogue_avatar_explicit | Do you have table by window? | ✅ PASS |  |
| T004 | dialogue_avatar_recast | Do you have table by window? | ✅ PASS |  |
| T004 | assistant_agent_explicit | Do you have table by window? | ✅ PASS |  |
| T004 | assistant_agent_recast | Do you have table by window? | ✅ PASS |  |
| T005 | dialogue_avatar_explicit | I very love reservation window. | ✅ PASS |  |
| T005 | dialogue_avatar_recast | I very love reservation window. | ✅ PASS |  |
| T005 | assistant_agent_explicit | I very love reservation window. | ✅ PASS |  |
| T005 | assistant_agent_recast | I very love reservation window. | ✅ PASS |  |
