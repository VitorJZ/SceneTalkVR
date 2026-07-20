from __future__ import annotations

from typing import Any


FORMAL = {
    "NE": ("Non-Split / Dialogue Avatar", "Explicit"),
    "NR": ("Non-Split / Dialogue Avatar", "Recast"),
    "SE": ("Split / Assistant Agent", "Explicit"),
    "SR": ("Split / Assistant Agent", "Recast"),
}


def parse_assignments(manifest: dict[str, Any], assignment: dict[str, Any]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    pilot = "pilotProtocolVersion" in assignment
    for item in assignment.get("conditions", []):
        task = item.get("task") or {}
        code = item.get("formalConditionLabel", "")
        embodiment = item.get("embodimentConditionLabel", "")
        provider, style = FORMAL.get(code, ("", assignment.get("feedbackStyleLabel", "")))
        rows.append({
            "participantId": manifest.get("participantId", ""), "sessionId": manifest.get("sessionId", ""),
            "sequenceId": assignment.get("sequenceId", ""), "conditionPosition": item.get("conditionPosition", ""),
            "formalConditionCode": code, "provider": provider, "style": style, "embodimentCondition": embodiment,
            "taskId": task.get("taskId", ""), "taskAssignmentId": task.get("taskAssignmentId", ""),
            "runAttempt": item.get("runAttempt", 0), "conditionRunId": "" if pilot else item.get("latestConditionRunId", ""),
            "pilotRunId": item.get("latestPilotRunId", "") if pilot else "", "conditionStatus": str(item.get("status", "")),
            "technicalValidity": "TechnicalInvalid" if str(item.get("status")) == "7" else "Valid",
        })
    return rows
