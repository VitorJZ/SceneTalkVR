from __future__ import annotations

from typing import Any


def parse_interviews(events: list[dict[str, Any]], include_text: bool = False) -> list[dict[str, Any]]:
    return [{"participantId":x.get("participantId",""),"sessionId":x.get("sessionId",""),"interviewLinkageKey":x.get("interviewLinkageKey",""),"containsFreeText":bool(x.get("text")),"restrictedAccess":bool(x.get("text")),"text":x.get("text","") if include_text else ""} for x in events if x.get("eventType")=="InterviewSaved"]
