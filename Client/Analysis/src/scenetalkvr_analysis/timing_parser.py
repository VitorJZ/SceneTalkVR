from __future__ import annotations

from collections import defaultdict
from typing import Any

from .assignment_parser import FORMAL


EVENT_PAIRS = {
    "userEndToFeedbackAudioMs": ("UserSpeechEnded", "CorrectionPlaybackStarted"),
    "userEndToDialogueAudioMs": ("UserSpeechEnded", "DialoguePlaybackStarted"),
    "feedbackToDialogueGapMs": ("CorrectionPlaybackEnded", "DialoguePlaybackStarted"),
    "correctionGenerationMs": ("CorrectionRequestStarted", "CorrectionTextReady"),
    "dialogueFirstSentenceGenerationMs": ("DialogueRequestStarted", "DialogueFirstSentenceReady"),
    "correctionTtsMs": ("CorrectionTtsStarted", "CorrectionTtsReady"),
    "dialogueFirstTtsMs": ("DialogueTtsStarted", "DialogueFirstTtsReady"),
}


def parse_turns(events: list[dict[str, Any]], tolerance_ms: int = 0) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for event in events:
        if event.get("turnId"):
            grouped[event["turnId"]].append(event)
    rows: list[dict[str, Any]] = []
    exclusions: list[dict[str, Any]] = []
    for turn_index, (turn_id, raw) in enumerate(sorted(grouped.items()), 1):
        source_order = [int(x.get("monotonicElapsedMs", -1)) for x in raw]
        if any(b < a for a, b in zip(source_order, source_order[1:])):
            exclusions.append(_exclusion(raw[0], turn_id, "timing_non_monotonic", "FAIL", "Monotonic event order regressed."))
        ordered = sorted(raw, key=lambda x: int(x.get("monotonicElapsedMs", -1)))
        by_type = {x.get("eventType"): x for x in ordered}
        summary = by_type.get("TurnSummary", {})
        has_feedback = bool(summary.get("hasFeedback", any(x.get("eventType") == "CorrectionPlaybackStarted" for x in ordered)))
        values: dict[str, int | None] = {}
        for name, (left, right) in EVENT_PAIRS.items():
            values[name] = _difference(by_type, left, right)
        if has_feedback and (values["feedbackToDialogueGapMs"] is None or values["feedbackToDialogueGapMs"] < 0):
            exclusions.append(_exclusion(raw[0], turn_id, "feedback_first_violation", "FAIL", "Feedback did not end before dialogue playback."))
        if not has_feedback and any(x.get("eventType", "").startswith("CorrectionPlayback") for x in ordered):
            exclusions.append(_exclusion(raw[0], turn_id, "feedback_first_violation", "FAIL", "No-feedback turn contains correction playback."))
        for metric in ("userEndToFeedbackAudioMs", "userEndToDialogueAudioMs", "feedbackToDialogueGapMs"):
            observed = summary.get(metric, -1)
            expected = values[metric] if values[metric] is not None else -1
            if observed != expected and abs(int(observed) - int(expected)) > tolerance_ms:
                exclusions.append(_exclusion(raw[0], turn_id, "summary_recompute_mismatch", "FAIL", f"{metric}: expected={expected}, observed={observed}, difference={int(observed)-int(expected)}"))
        condition = summary.get("conditionLabel", raw[0].get("conditionLabel", ""))
        provider, style = FORMAL.get(condition, ("", ""))
        row = {
            "participantId": summary.get("participantId", raw[0].get("participantId", "")), "sessionId": summary.get("sessionId", raw[0].get("sessionId", "")),
            "conditionRunId": summary.get("conditionRunId", ""), "pilotRunId": summary.get("conditionRunId", "") if condition in {"voice_only","floating_orb","humanoid_agent"} else "",
            "turnId": turn_id, "turnIndex": turn_index, "conditionCode": condition if condition in FORMAL else "", "provider": provider, "style": style,
            "embodimentCondition": condition if condition not in FORMAL else "", "taskId": summary.get("taskId", ""), "hasFeedback": has_feedback,
            "feedbackTextHash": summary.get("feedbackTextHash", ""), "actualPlaybackActor": summary.get("actualPlaybackActor", ""),
            "technicalValidity": summary.get("technicalValidity", "Valid"), "failureStage": summary.get("failureStage", ""), "failureReason": summary.get("failureReason", ""),
            "fallbackUsed": summary.get("fallbackUsed", False), **values,
        }
        rows.append(row)
    return rows, exclusions


def _difference(by_type: dict[str, dict[str, Any]], left: str, right: str) -> int | None:
    if left not in by_type or right not in by_type:
        return None
    return int(by_type[right].get("monotonicElapsedMs", 0)) - int(by_type[left].get("monotonicElapsedMs", 0))


def _exclusion(event: dict[str, Any], turn_id: str, rule: str, severity: str, reason: str) -> dict[str, Any]:
    return {"scope":"turn","participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"conditionRunId":event.get("conditionRunId",""),"turnId":turn_id,"ruleId":rule,"severity":severity,"reason":reason,"sourceEvidence":f"{event.get('_sourceFile','')}:{event.get('_sourceLine','')}"}
