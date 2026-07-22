using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public static class EditorDemoBundleExporter
    {
        private readonly struct DemoCondition
        {
            public readonly string label, task, run, link, assignment;
            public DemoCondition(string label, string task, string run, string link, string assignment) { this.label = label; this.task = task; this.run = run; this.link = link; this.assignment = assignment; }
        }

        public static bool Export(string root, ExperimentAssignment formal, PilotAssignment pilot, string demoProtocol,
            string officialProtocol, bool rankingSubmitted, bool interviewSaved, out string bundle, out string error)
        {
            bundle = string.Empty; error = string.Empty; var isFormal = formal != null;
            var p = isFormal ? formal.participantId : pilot?.participantId; var s = isFormal ? formal.experimentSessionId : pilot?.sessionId;
            if (string.IsNullOrWhiteSpace(p) || string.IsNullOrWhiteSpace(s)) { error = "editor_demo_assignment_missing"; return false; }
            if (!rankingSubmitted || isFormal && !interviewSaved) { error = "editor_demo_final_ranking_or_interview_incomplete"; return false; }
            if (isFormal && (formal.dataOrigin != "editor_demo" || formal.collectionEligible || formal.conditions.Any(x => x.status != ConditionRunStatus.Completed))) { error = "editor_demo_formal_incomplete_or_not_isolated"; return false; }
            if (!isFormal && (pilot.dataOrigin != "editor_demo" || pilot.collectionEligible || pilot.conditions.Any(x => x.status != PilotRunStatus.Completed))) { error = "editor_demo_pilot_incomplete_or_not_isolated"; return false; }
            var source = Path.Combine(root, "bundle-source"); bundle = Path.Combine(root, "bundle");
            if (Directory.Exists(source)) Directory.Delete(source, true); if (Directory.Exists(bundle)) Directory.Delete(bundle, true);
            foreach (var folder in new[] { "assignment", "timing", "study", "questionnaire", "ranking", "interview" }) Directory.CreateDirectory(Path.Combine(source, folder));
            File.WriteAllText(Path.Combine(source, "assignment", "assignment.json"), isFormal ? JsonUtility.ToJson(formal, true) : JsonUtility.ToJson(pilot, true), Encoding.UTF8);
            var taskVersion = isFormal ? formal.taskCatalogVersion : pilot.taskCatalogVersion;
            var assignmentVersion = isFormal ? formal.assignmentVersion : pilot.pilotAssignmentVersion;
            var conditions = isFormal
                ? formal.conditions.Select((x, i) => new DemoCondition(x.formalConditionLabel, x.task.taskId, x.latestConditionRunId, $"editor-demo-formal-q-{i + 1}", x.task.taskAssignmentId)).ToArray()
                : pilot.conditions.Select((x, i) => new DemoCondition(x.embodimentConditionLabel, x.task.taskId, x.latestPilotRunId, $"editor-demo-pilot-q-{i + 1}", x.task.taskAssignmentId)).ToArray();
            var timing = new List<SyntheticDryRunEvent>(); var study = new List<SyntheticDryRunEvent>(); var question = new List<SyntheticDryRunEvent>(); var ranking = new List<SyntheticDryRunEvent>(); var interview = new List<SyntheticDryRunEvent>(); long clock = 0;
            var pilotHash = ExperimentEventTimeline.HashText("Editor Demo shared Explicit feedback");
            SyntheticDryRunEvent Event(string type, DemoCondition c, long time, string turn = "") => new SyntheticDryRunEvent
            {
                schemaVersion = "1.1-editor-demo", dataOrigin = "editor_demo", collectionEligible = false, developerTestAssignment = true, demoMode = true,
                runtimeMode = isFormal ? ExperimentRuntimeMode.EditorDemoFormal.ToString() : ExperimentRuntimeMode.EditorDemoPilot.ToString(), demoProtocolVersion = demoProtocol,
                participantId = p, sessionId = s, protocolVersion = demoProtocol, taskCatalogVersion = taskVersion,
                questionnaireCatalogVersion = "1.1-stage5.1", assignmentVersion = assignmentVersion, eventType = type,
                conditionRunId = c.run ?? string.Empty, questionnaireLinkageKey = c.link ?? string.Empty,
                taskAssignmentId = c.assignment ?? string.Empty, conditionLabel = c.label ?? string.Empty,
                taskId = c.task ?? string.Empty, turnId = turn, monotonicElapsedMs = time, technicalValidity = "Valid"
            };
            void Add(List<SyntheticDryRunEvent> list, string type, DemoCondition c, string goal = "") { var e = Event(type, c, clock++); e.goalId = goal; list.Add(e); }

            for (var i = 0; i < conditions.Length; i++)
            {
                var c = conditions[i]; Add(study, "ConditionPrepared", c); Add(study, "ConditionStarted", c); Add(study, "GoalCandidateSubmitted", c, "goal-1"); Add(study, "GoalConfirmed", c, "goal-1");
                var turn = "editor-demo-turn-" + (i + 1); var start = clock; var hash = isFormal ? ExperimentEventTimeline.HashText("Editor Demo correction " + c.label) : pilotHash;
                void Timing(string type) { var e = Event(type, c, clock++, turn); e.hasFeedback = true; e.feedbackTextHash = hash; timing.Add(e); }
                Timing("UserSpeechEnded"); Timing("DialogueGateClosed"); Timing("CorrectionPlaybackStarted"); Timing("CorrectionPlaybackEnded"); Timing("DialogueGateOpened"); Timing("DialoguePlaybackStarted"); Timing("DialoguePlaybackEnded"); Timing("TurnCompleted");
                var summary = Event("TurnSummary", c, clock++, turn); summary.hasFeedback = true; summary.feedbackTextHash = hash; summary.userEndToFeedbackAudioMs = 2; summary.userEndToDialogueAudioMs = 5; summary.feedbackToDialogueGapMs = 2; timing.Add(summary);
                var item = Event("QuestionnaireItem", c, clock++); item.itemId = "editor_demo_item"; item.rawValue = "5"; item.scoredValue = 5; item.revision = 1; question.Add(item); Add(question, "QuestionnaireSubmitted", c);
                Add(study, "ConditionCompleted", c); Add(study, "ConditionBoundaryReset", c);
            }
            for (var i = 0; i < conditions.Length; i++) { var e = Event(isFormal ? "FinalRankingEntry" : "PilotFinalRankingEntry", conditions[i], clock++); e.rank = i + 1; ranking.Add(e); }
            Add(study, isFormal ? "FinalRankingSubmitted" : "PilotFinalRankingSubmitted", default);
            if (isFormal) { var e = Event("InterviewSaved", default, clock++); e.interviewLinkageKey = "editor-demo-interview"; interview.Add(e); Add(study, "InterviewCompleted", default); Add(study, "ExperimentCompleted", default); }
            Write(Path.Combine(source, "timing", "timing.jsonl"), timing); Write(Path.Combine(source, "study", "study.jsonl"), study); Write(Path.Combine(source, "questionnaire", "questionnaire.jsonl"), question); Write(Path.Combine(source, "ranking", "ranking.jsonl"), ranking); Write(Path.Combine(source, "interview", "interview.jsonl"), interview);
            var manifest = new SessionBundleManifest { bundleSchemaVersion = "1.1-editor-demo", dataOrigin = "editor_demo", collectionEligible = false, developerTestAssignment = true, demoMode = true,
                demoProtocolVersion = demoProtocol, officialProtocolVersion = officialProtocol, runtimeMode = isFormal ? ExperimentRuntimeMode.EditorDemoFormal.ToString() : ExperimentRuntimeMode.EditorDemoPilot.ToString(), sessionMode = isFormal ? "editor_demo_formal" : "editor_demo_pilot",
                participantId = p, sessionId = s, gitCommit = "editor-working-tree", protocolVersion = demoProtocol, taskCatalogVersion = taskVersion, questionnaireCatalogVersion = "1.1-stage5.1", assignmentVersion = assignmentVersion, createdAtUtc = DateTime.UtcNow.ToString("o") };
            if (!SessionBundleExporter.Export(source, bundle, manifest, out error)) return false;
            var audit = SessionDataIntegrityAuditor.Audit(bundle, p, s); manifest.integrityStatus = audit.result.ToString().ToUpperInvariant(); SessionBundleExporter.UpdateIntegrity(bundle, manifest, audit); return true;
        }

        private static void Write(string path, IEnumerable<SyntheticDryRunEvent> values) => File.WriteAllLines(path, values.Select(JsonUtility.ToJson), Encoding.UTF8);
    }
}
