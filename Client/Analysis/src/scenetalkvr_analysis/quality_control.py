from __future__ import annotations

from collections import Counter
from typing import Any


def qc_summary(sessions: list[dict[str, Any]], exclusions: list[dict[str, Any]], conditions: list[dict[str, Any]], attempts: list[dict[str, Any]], turns: list[dict[str, Any]]) -> dict[str, Any]:
    return {"inputBundleCount":len(sessions),"collectionEligibleCount":sum(bool(x.get("collectionEligible")) for x in sessions),"syntheticCount":sum(str(x.get("dataOrigin","")).startswith("synthetic") for x in sessions),"integrityStatus":dict(Counter(x.get("integrityStatus","") for x in sessions)),"includedSessions":sum(x.get("inclusionStatus")=="included" for x in sessions),"excludedSessions":sum(x.get("inclusionStatus")!="included" for x in sessions),"conditionCount":len(conditions),"technicalInvalidCount":sum(bool(x.get("isTechnicalInvalid")) for x in attempts),"retryCount":sum(bool(x.get("isRetry")) for x in attempts),"feedbackFirstViolations":sum(x.get("ruleId")=="feedback_first_violation" for x in exclusions),"timingMismatchCount":sum(x.get("ruleId")=="summary_recompute_mismatch" for x in exclusions),"exclusionReasons":dict(Counter(x.get("ruleId","") for x in exclusions)),"turnCount":len(turns)}
