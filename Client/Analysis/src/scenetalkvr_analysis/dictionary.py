from __future__ import annotations

TABLES = {
 "sessions.csv":["participantId","sessionId","sessionMode","dataOrigin","collectionEligible","gitCommit","protocolVersion","taskCatalogVersion","questionnaireCatalogVersion","assignmentVersion","formalConditionOrderPolicy","taskAssignmentPolicy","goalConfirmationPolicy","questionnaireReturnPolicy","assignmentAlgorithmVersion","randomSeedHash","integrityStatus","inclusionStatus","exclusionReasons"],
 "assignments_long.csv":["participantId","sessionId","sequenceId","conditionPosition","formalConditionCode","provider","style","embodimentCondition","taskId","taskAssignmentId","runAttempt","conditionRunId","pilotRunId","conditionStatus","technicalValidity","formalConditionOrderPolicy","taskAssignmentPolicy","goalConfirmationPolicy","questionnaireReturnPolicy","participantSelectionPosition","selectedAtUtc"],
 "turns_long.csv":["participantId","sessionId","conditionRunId","pilotRunId","turnId","turnIndex","conditionCode","provider","style","embodimentCondition","taskId","hasFeedback","feedbackTextHash","actualPlaybackActor","technicalValidity","failureStage","failureReason","fallbackUsed","userEndToFeedbackAudioMs","userEndToDialogueAudioMs","feedbackToDialogueGapMs","correctionGenerationMs","dialogueFirstSentenceGenerationMs","correctionTtsMs","dialogueFirstTtsMs"],
 "condition_summary.csv":["conditionRunId","isPrimaryAttempt","turnCount","validTurnCount","technicalInvalidTurnCount","feedbackTurnCount","noFeedbackTurnCount","meanUserEndToFeedbackAudioMs","medianUserEndToFeedbackAudioMs","meanFeedbackToDialogueGapMs","taskCompletionRate","completedGoalCount","totalGoalCount","turnsToCompletion","conditionDurationMs","completionReason","retryCount"],
 "goals_long.csv":["goalId","goalText","state","candidateSource","evidenceTurnId","candidateAtUtc","confirmedAtUtc","confirmedBy","rejectionReason"],
 "questionnaire_items_long.csv":["questionnaireId","sectionId","itemId","itemVersion","rawValue","scoredValue","reverseScored","scaleMin","scaleMax","missing","revision","questionnaireStatus","conditionStatus","submittedAtUtc"],
 "scale_scores.csv":["scale","itemCountExpected","itemCountAnswered","missingItemCount","scaleMean","scaleSum","scorable"],
 "rankings_long.csv":["rankingType","rank","formalConditionCode","embodimentCondition","preferredCondition","reason"],
 "interviews_long.csv":["interviewLinkageKey","containsFreeText","restrictedAccess","text"],
 "all_attempts.csv":["conditionRunId","pilotRunId","conditionCode","runAttempt","isTechnicalInvalid","isRetry","supersedesRunId","isValidCompletedAttempt","isPrimaryAttempt"],
 "exclusions.csv":["scope","participantId","sessionId","conditionRunId","turnId","ruleId","severity","reason","sourceEvidence"]
}

def rows():
    return [{"table":table,"field":field,"description":field,"dataType":"derived_or_source","containsFreeText":field in {"reason","text","goalText"},"restrictedByDefault":field=="text"} for table,fields in TABLES.items() for field in fields]
