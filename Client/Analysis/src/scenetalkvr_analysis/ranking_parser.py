from __future__ import annotations

from typing import Any


def parse_rankings(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    rows=[]
    for event in events:
        if event.get("eventType") not in {"FinalRankingEntry","PilotFinalRankingEntry"}: continue
        condition=event.get("conditionLabel","")
        rows.append({"participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"rankingType":"formal" if event.get("eventType")=="FinalRankingEntry" else "pilot","rank":event.get("rank"),"formalConditionCode":condition if condition in {"NE","NR","SE","SR"} else "","embodimentCondition":condition if condition not in {"NE","NR","SE","SR"} else "","preferredCondition":condition if event.get("rank")==1 else "","reason":event.get("reason","")})
    return rows
