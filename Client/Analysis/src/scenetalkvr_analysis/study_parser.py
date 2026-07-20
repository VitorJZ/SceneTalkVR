from __future__ import annotations

from collections import defaultdict
from typing import Any


def parse_goals(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for event in events:
        if event.get("eventType") not in {"GoalCandidateSubmitted", "GoalConfirmed", "GoalRejected"}:
            continue
        rows.append({
            "participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"conditionRunId":event.get("conditionRunId",""),
            "goalId":event.get("goalId",""),"goalText":event.get("goalText",""),"state":event.get("eventType",""),"candidateSource":event.get("candidateSource","synthetic_or_runtime"),
            "evidenceTurnId":event.get("turnId",""),"candidateAtUtc":event.get("timestampUtc","") if event.get("eventType")=="GoalCandidateSubmitted" else "",
            "confirmedAtUtc":event.get("timestampUtc","") if event.get("eventType")=="GoalConfirmed" else "","confirmedBy":event.get("actor",""),"rejectionReason":event.get("rejectionReason","")
        })
    return rows


def parse_attempts(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    retries = {x.get("conditionRunId", "") for x in events if x.get("eventType") == "RetryAuthorized"}
    invalid = {x.get("conditionRunId", "") for x in events if x.get("eventType") in {"ConditionTechnicalInvalid", "PilotConditionTechnicalInvalid"}}
    completed = {x.get("conditionRunId", "") for x in events if x.get("eventType") in {"ConditionCompleted", "PilotConditionCompleted"}}
    for event in events:
        if event.get("eventType") not in {"ConditionStarted", "PilotConditionStarted"}:
            continue
        run = event.get("conditionRunId", "")
        rows.append({"participantId":event.get("participantId",""),"sessionId":event.get("sessionId",""),"conditionRunId":run,"pilotRunId":run if event.get("conditionLabel") in {"voice_only","floating_orb","humanoid_agent"} else "","runAttempt":event.get("runAttempt",1),"isTechnicalInvalid":run in invalid or event.get("technicalValidity")=="TechnicalInvalid","isRetry":event.get("runAttempt",1)>1 or run in retries,"supersedesRunId":event.get("supersedesRunId",""),"isValidCompletedAttempt":run in completed and run not in invalid})
    return rows


def condition_summaries(study: list[dict[str, Any]], turns: list[dict[str, Any]], goals: list[dict[str, Any]], attempts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    runs = sorted({x.get("conditionRunId", "") for x in study if x.get("conditionRunId")})
    result: list[dict[str, Any]] = []
    for run in runs:
        run_turns = [x for x in turns if x.get("conditionRunId") == run]
        run_goals = [x for x in goals if x.get("conditionRunId") == run]
        feedback = [x for x in run_turns if x.get("hasFeedback")]
        gaps = [x["feedbackToDialogueGapMs"] for x in feedback if x.get("feedbackToDialogueGapMs") is not None]
        latencies = [x["userEndToFeedbackAudioMs"] for x in feedback if x.get("userEndToFeedbackAudioMs") is not None]
        clocks = [int(x.get("monotonicElapsedMs", 0)) for x in study if x.get("conditionRunId") == run]
        confirmed = {x.get("goalId") for x in run_goals if x.get("state") == "GoalConfirmed"}
        all_goals = {x.get("goalId") for x in run_goals if x.get("goalId")}
        result.append({"conditionRunId":run,"turnCount":len(run_turns),"validTurnCount":sum(x.get("technicalValidity")=="Valid" for x in run_turns),"technicalInvalidTurnCount":sum(x.get("technicalValidity")!="Valid" for x in run_turns),"feedbackTurnCount":len(feedback),"noFeedbackTurnCount":len(run_turns)-len(feedback),"meanUserEndToFeedbackAudioMs":_mean(latencies),"medianUserEndToFeedbackAudioMs":_median(latencies),"meanFeedbackToDialogueGapMs":_mean(gaps),"taskCompletionRate":1 if any(x.get("conditionRunId")==run and x.get("eventType") in {"ConditionCompleted","PilotConditionCompleted"} for x in study) else 0,"completedGoalCount":len(confirmed),"totalGoalCount":len(all_goals),"turnsToCompletion":len(run_turns),"conditionDurationMs":max(clocks)-min(clocks) if clocks else None,"completionReason":"completed" if run in {x.get('conditionRunId') for x in study if x.get('eventType') in {'ConditionCompleted','PilotConditionCompleted'}} else "incomplete","retryCount":sum(x.get("conditionRunId")==run and x.get("isRetry") for x in attempts)})
    return result


def _mean(values: list[int]) -> float | None: return sum(values)/len(values) if values else None
def _median(values: list[int]) -> float | None:
    if not values: return None
    ordered=sorted(values);middle=len(ordered)//2
    return ordered[middle] if len(ordered)%2 else (ordered[middle-1]+ordered[middle])/2
