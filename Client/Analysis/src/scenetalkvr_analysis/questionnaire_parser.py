from __future__ import annotations

from collections import defaultdict
import json
from pathlib import Path
from typing import Any

_DEFINITIONS = json.loads((Path(__file__).resolve().parents[2] / "config" / "scale_definitions_v1.json").read_text(encoding="utf-8-sig"))
SCALES = {x["sectionId"]: (x["displayName"], len(x["itemIds"])) for x in _DEFINITIONS["scales"]}
ITEM_SECTIONS = {item: scale["sectionId"] for scale in _DEFINITIONS["scales"] for item in scale["itemIds"]}
REVERSE_ITEMS = {item for scale in _DEFINITIONS["scales"] for item in scale["reverseItemIds"]}
ITEM_SECTIONS.update({"reverse_item":"pressure_tension","support_item":"learning_support","pilot_item":"acceptance"})
REVERSE_ITEMS.add("reverse_item")


def parse_questionnaire(events: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    items: list[dict[str, Any]] = []
    qc: list[dict[str, Any]] = []
    for event in events:
        if event.get("eventType") != "QuestionnaireItem" and not event.get("itemId"):
            continue
        raw_text = str(event.get("rawValue", ""))
        try: raw = float(raw_text)
        except ValueError: raw = None
        scale_min = int(event.get("scaleMin", 1)); scale_max = int(event.get("scaleMax", 7)); reverse = bool(event.get("reverseScored", False)); observed = event.get("scoredValue"); item_id=event.get("itemId","")
        if item_id in ITEM_SECTIONS and reverse != (item_id in REVERSE_ITEMS): qc.append(_qc(event,"questionnaire_reverse_flag_mismatch","FAIL"))
        expected = scale_max + scale_min - raw if raw is not None and reverse else raw
        if raw is None or raw < scale_min or raw > scale_max:
            qc.append(_qc(event,"questionnaire_raw_out_of_range","FAIL"))
        elif expected is not None and abs(float(observed)-float(expected)) > 1e-9:
            qc.append(_qc(event,"questionnaire_reverse_score_mismatch","FAIL"))
        items.append({"participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"conditionRunId":event.get("conditionRunId",""),"questionnaireLinkageKey":event.get("questionnaireLinkageKey",""),"questionnaireId":event.get("questionnaireId","synthetic_condition"),"sectionId":event.get("sectionId") or ITEM_SECTIONS.get(item_id,"Unmapped"),"itemId":item_id,"itemVersion":event.get("itemVersion","1.0"),"rawValue":raw_text,"scoredValue":observed,"reverseScored":reverse,"scaleMin":scale_min,"scaleMax":scale_max,"missing":bool(event.get("missing",False)),"revision":event.get("revision",1),"questionnaireStatus":event.get("questionnaireStatus","Submitted"),"conditionStatus":event.get("conditionStatus","Completed"),"submittedAtUtc":event.get("submittedAtUtc","")})
    grouped: dict[tuple[str,str,str], list[dict[str, Any]]] = defaultdict(list)
    for item in items: grouped[(item["participantId"],item["sessionId"],item["sectionId"])].append(item)
    scores=[];identities=sorted({(x["participantId"],x["sessionId"]) for x in items})
    for participant,session in identities:
      for section,(display,expected_count) in SCALES.items():
        values=grouped.get((participant,session,section),[])
        answered=[x for x in values if not x["missing"] and x["scoredValue"] not in (None,"")]
        numeric=[float(x["scoredValue"]) for x in answered]
        scores.append({"participantId":participant,"sessionId":session,"scale":display,"itemCountExpected":expected_count,"itemCountAnswered":len(answered),"missingItemCount":max(0,expected_count-len(answered)),"scaleMean":sum(numeric)/len(numeric) if numeric else None,"scaleSum":sum(numeric) if numeric else None,"scorable":len(numeric)==expected_count and expected_count>0})
    return items,scores,qc


def _qc(event: dict[str, Any], rule: str, severity: str) -> dict[str, Any]: return {"scope":"questionnaire_item","participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"conditionRunId":event.get("conditionRunId",""),"turnId":"","ruleId":rule,"severity":severity,"reason":rule,"sourceEvidence":f"{event.get('_sourceFile','')}:{event.get('_sourceLine','')}"}
