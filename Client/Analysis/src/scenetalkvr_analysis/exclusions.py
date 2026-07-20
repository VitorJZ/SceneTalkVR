from __future__ import annotations

from typing import Any


def session_exclusions(manifest: dict[str, Any], assignment: dict[str, Any], config: dict[str, Any]) -> list[dict[str, Any]]:
    rows=[]
    def add(rule: str, reason: str): rows.append({"scope":"session","participantId":manifest.get("participantId",""),"sessionId":manifest.get("sessionId",""),"conditionRunId":"","turnId":"","ruleId":rule,"severity":"FAIL","reason":reason,"sourceEvidence":"manifest.json"})
    synthetic=manifest.get("dataOrigin") in {"synthetic_dry_run","synthetic_matrix","developer_placeholder_matrix"}
    demo=manifest.get("dataOrigin")=="editor_demo"
    testing_allowed=(synthetic and config.get("includeSyntheticForTesting",False)) or (demo and config.get("includeDemoForTesting",False))
    if config.get("requireCollectionEligible",True) and not manifest.get("collectionEligible",False) and not testing_allowed: add("collection_ineligible","collectionEligible=false")
    if assignment.get("developerTestAssignment",False) and not testing_allowed: add("developer_test_assignment","developerTestAssignment=true")
    if config.get("requireIntegrityPass",True) and manifest.get("integrityStatus")!="PASS": add("integrity_fail",f"integrityStatus={manifest.get('integrityStatus')}")
    allowed=config.get("allowedProtocolVersions") or []
    if allowed and manifest.get("protocolVersion") not in allowed: add("protocol_version_mismatch",manifest.get("protocolVersion",""))
    return rows
