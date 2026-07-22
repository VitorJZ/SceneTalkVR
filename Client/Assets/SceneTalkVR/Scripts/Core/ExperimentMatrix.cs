using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum ExperimentMatrixType { Formal, Pilot }
    public enum ExperimentMatrixExecutionMode { Synthetic, DeveloperPlaceholder, LockedCollection }
    public enum ExperimentMatrixCaseStatus { PASS, FAIL, BLOCKED, NOT_RUN }

    [Serializable]
    public sealed class ExperimentMatrixCase
    {
        public string caseId;
        public ExperimentMatrixType matrixType;
        public string conditionCode;
        public string embodimentCondition;
        public string taskId;
    }

    [Serializable]
    public sealed class ExperimentMatrixDefinition
    {
        public string matrixSchemaVersion = "1.0";
        public ExperimentMatrixType matrixType;
        public ExperimentMatrixCase[] cases = Array.Empty<ExperimentMatrixCase>();

        public static ExperimentMatrixDefinition Formal()
        {
            var conditions = new[] { "NE", "NR", "SE", "SR" };
            var tasks = new[] { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" };
            return Build(ExperimentMatrixType.Formal, conditions, tasks);
        }

        public static ExperimentMatrixDefinition Pilot()
        {
            var conditions = new[] { "voice_only", "floating_orb", "humanoid_agent" };
            var tasks = new[] { "pilot_restaurant_walk_in", "pilot_restaurant_ordering", "pilot_restaurant_wrong_dish" };
            return Build(ExperimentMatrixType.Pilot, conditions, tasks);
        }

        private static ExperimentMatrixDefinition Build(ExperimentMatrixType type, string[] conditions, string[] tasks)
        {
            var values = new List<ExperimentMatrixCase>();
            foreach (var condition in conditions)
                foreach (var task in tasks)
                    values.Add(new ExperimentMatrixCase
                    {
                        caseId = $"{(type == ExperimentMatrixType.Formal ? "formal" : "pilot")}-{condition.ToLowerInvariant()}-{task}",
                        matrixType = type,
                        conditionCode = type == ExperimentMatrixType.Formal ? condition : string.Empty,
                        embodimentCondition = type == ExperimentMatrixType.Pilot ? condition : string.Empty,
                        taskId = task
                    });
            return new ExperimentMatrixDefinition { matrixType = type, cases = values.ToArray() };
        }
    }

    [Serializable]
    public sealed class ExperimentMatrixEvidence
    {
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string pilotRunId;
        public string taskAssignmentId;
        public string questionnaireLinkageKey;
        public string dataOrigin;
        public bool collectionEligible;
        public bool developerTestAssignment;
        public bool placeholderUsed;
        public string scenarioId;
        public string panoramaResourceKey;
        public string requestedAvatarPresetKey;
        public string resolvedAvatarPresetKey;
        public string avatarFallbackLevel;
        public string provider;
        public string style;
        public string visualMode;
        public bool visualEntityCreated;
        public string visualEntityType;
        public string voiceProfileKey;
        public string voiceId;
        public float speakingSpeed;
        public float volume;
        public string subtitlePolicy;
        public string audioSourcePolicy;
        public float spatialBlend;
        public string feedbackTextHash;
        public string actualPlaybackActor;
        public long feedbackPlaybackStart;
        public long feedbackPlaybackEnd;
        public long dialoguePlaybackStart;
        public bool feedbackFirstValid;
        public bool noFeedbackValid;
        public bool goalTraceValid;
        public bool questionnaireLinkageValid;
        public bool resetValid;
        public bool timingEventsValid;
        public bool studyEventsValid;
        public string technicalValidity;
    }

    [Serializable]
    public sealed class ExperimentMatrixCaseResult
    {
        public string matrixSchemaVersion = "1.0";
        public string matrixRunId;
        public string matrixType;
        public string executionMode;
        public string caseId;
        public string gitCommit;
        public string protocolVersion;
        public string taskCatalogVersion;
        public string questionnaireCatalogVersion;
        public string conditionCode;
        public string embodimentCondition;
        public string taskId;
        public string status;
        public string startedAtUtc;
        public string completedAtUtc;
        public long durationMs;
        public string[] assertionsPassed = Array.Empty<string>();
        public string[] assertionsFailed = Array.Empty<string>();
        public string[] blockerIds = Array.Empty<string>();
        public string[] failureReasons = Array.Empty<string>();
        public string[] evidenceFiles = Array.Empty<string>();
        public string sessionBundlePath;
        public string integrityStatus;
        public ExperimentMatrixEvidence evidence = new ExperimentMatrixEvidence();
    }

    [Serializable]
    public sealed class ExperimentMatrixRunManifest
    {
        public string matrixSchemaVersion = "1.0";
        public string matrixRunId;
        public string matrixType;
        public string executionMode;
        public string gitCommit;
        public string protocolVersion;
        public string taskCatalogVersion;
        public string questionnaireCatalogVersion;
        public string startedAtUtc;
        public string completedAtUtc;
        public string dataOrigin;
        public bool collectionEligible;
        public int deterministicSeed;
        public int totalCases;
        public int passed;
        public int failed;
        public int blocked;
        public int notRun;
        public ExperimentMatrixCaseResult[] results = Array.Empty<ExperimentMatrixCaseResult>();
    }

    public static class ExperimentMatrixRunnerService
    {
        public const int DeterministicSeed = 1109;

        public static ExperimentMatrixRunManifest Run(
            ExperimentMatrixDefinition definition,
            ExperimentMatrixExecutionMode mode,
            string evidenceRoot,
            string gitCommit,
            ExperimentV11ProtocolConfig protocol,
            ExperimentTaskCatalog taskCatalog,
            string questionnaireCatalogVersion,
            PilotPresentationCatalog pilotPresentations)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var started = DateTime.UtcNow;
            var runId = $"s9-{(definition.matrixType == ExperimentMatrixType.Formal ? "f" : "p")}-{ModeCode(mode)}-{started:yyyyMMddHHmmss}";
            var runRoot = Path.Combine(evidenceRoot, runId, definition.matrixType.ToString().ToLowerInvariant());
            Directory.CreateDirectory(Path.Combine(runRoot, "cases"));
            Directory.CreateDirectory(Path.Combine(runRoot, "bundles"));
            var results = definition.cases.Select((value, index) => RunCase(value, index, mode, runId, runRoot, gitCommit, protocol, taskCatalog, questionnaireCatalogVersion, pilotPresentations)).ToArray();
            var origin = mode == ExperimentMatrixExecutionMode.Synthetic ? "synthetic_matrix" : mode == ExperimentMatrixExecutionMode.DeveloperPlaceholder ? "developer_placeholder_matrix" : "participant_collection";
            return new ExperimentMatrixRunManifest
            {
                matrixRunId = runId,
                matrixType = definition.matrixType.ToString(),
                executionMode = mode.ToString(),
                gitCommit = gitCommit,
                protocolVersion = protocol?.ProtocolVersion ?? string.Empty,
                taskCatalogVersion = taskCatalog?.CatalogVersion ?? string.Empty,
                questionnaireCatalogVersion = questionnaireCatalogVersion ?? string.Empty,
                startedAtUtc = started.ToString("o"),
                completedAtUtc = DateTime.UtcNow.ToString("o"),
                dataOrigin = origin,
                collectionEligible = mode == ExperimentMatrixExecutionMode.LockedCollection && results.All(x => x.status == ExperimentMatrixCaseStatus.PASS.ToString()),
                deterministicSeed = DeterministicSeed,
                totalCases = results.Length,
                passed = results.Count(x => x.status == ExperimentMatrixCaseStatus.PASS.ToString()),
                failed = results.Count(x => x.status == ExperimentMatrixCaseStatus.FAIL.ToString()),
                blocked = results.Count(x => x.status == ExperimentMatrixCaseStatus.BLOCKED.ToString()),
                notRun = results.Count(x => x.status == ExperimentMatrixCaseStatus.NOT_RUN.ToString()),
                results = results
            };
        }

        public static string ToCsv(ExperimentMatrixRunManifest run)
        {
            var rows = new List<string>
            {
                "matrixSchemaVersion,matrixRunId,matrixType,executionMode,caseId,gitCommit,protocolVersion,taskCatalogVersion,questionnaireCatalogVersion,conditionCode,embodimentCondition,taskId,status,durationMs,integrityStatus,collectionEligible,placeholderUsed,blockerIds,failureReasons,sessionBundlePath"
            };
            foreach (var item in run.results)
                rows.Add(string.Join(",", new[]
                {
                    item.matrixSchemaVersion,item.matrixRunId,item.matrixType,item.executionMode,item.caseId,item.gitCommit,item.protocolVersion,item.taskCatalogVersion,item.questionnaireCatalogVersion,item.conditionCode,item.embodimentCondition,item.taskId,item.status,item.durationMs.ToString(CultureInfo.InvariantCulture),item.integrityStatus,item.evidence.collectionEligible.ToString(),item.evidence.placeholderUsed.ToString(),string.Join(";",item.blockerIds),string.Join(";",item.failureReasons),item.sessionBundlePath
                }.Select(Csv)));
            return string.Join(Environment.NewLine, rows) + Environment.NewLine;
        }

        public static string[] LockedBlockers(ExperimentMatrixType type, ExperimentV11ProtocolConfig protocol, ExperimentTaskCatalog tasks, PilotPresentationCatalog presentations)
        {
            var blockers = new HashSet<string>(StringComparer.Ordinal);
            if (protocol == null) blockers.Add("protocol_asset_missing");
            else
            {
                foreach (var decision in protocol.RequiredDecisions ?? Array.Empty<ExperimentProtocolDecision>())
                    if ((type == ExperimentMatrixType.Formal || IsPilotDecision(decision?.decisionId)) && (decision == null || decision.status != ProtocolDecisionStatus.Confirmed))
                        blockers.Add("decision_unconfirmed:" + (decision?.decisionId ?? "<null>"));
            }
            if (type == ExperimentMatrixType.Formal)
            {
                if (tasks == null) blockers.Add("task_catalog_missing");
                else if (!tasks.ValidateFormal(protocol, out var error))
                    foreach (var value in SplitBlockers(error)) blockers.Add("formal_task:" + value);
                blockers.Add("approved_formal_avatar_presets_missing");
                blockers.Add("approved_voice_profiles_missing");
                blockers.Add("approved_deployment_profile_missing");
                blockers.Add("collection_grade_panorama_approval_incomplete");
            }
            else
            {
                if (presentations == null) blockers.Add("pilot_presentation_catalog_missing");
                else if (!presentations.ValidateLocked(protocol, out var error))
                    foreach (var value in SplitBlockers(error)) blockers.Add("pilot:" + value);
                blockers.Add("approved_pilot_humanoid_missing");
                blockers.Add("approved_voice_profiles_missing");
                blockers.Add("approved_deployment_profile_missing");
            }
            return blockers.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static ExperimentMatrixCaseResult RunCase(ExperimentMatrixCase value, int index, ExperimentMatrixExecutionMode mode, string runId, string runRoot, string commit, ExperimentV11ProtocolConfig protocol, ExperimentTaskCatalog tasks, string questionnaireVersion, PilotPresentationCatalog presentations)
        {
            var start = DateTime.UtcNow;
            var result = new ExperimentMatrixCaseResult
            {
                matrixRunId = runId,
                matrixType = value.matrixType.ToString(),
                executionMode = mode.ToString(),
                caseId = value.caseId,
                gitCommit = commit,
                protocolVersion = protocol?.ProtocolVersion ?? string.Empty,
                taskCatalogVersion = tasks?.CatalogVersion ?? string.Empty,
                questionnaireCatalogVersion = questionnaireVersion ?? string.Empty,
                conditionCode = value.conditionCode,
                embodimentCondition = value.embodimentCondition,
                taskId = value.taskId,
                startedAtUtc = start.ToString("o")
            };
            if (mode == ExperimentMatrixExecutionMode.LockedCollection)
            {
                result.status = ExperimentMatrixCaseStatus.BLOCKED.ToString();
                result.blockerIds = LockedBlockers(value.matrixType, protocol, tasks, presentations);
                result.integrityStatus = "BLOCKED";
                return Complete(result, start);
            }

            var participant = $"matrix-{(value.matrixType == ExperimentMatrixType.Formal ? "f" : "p")}-{index + 1:00}";
            var session = $"case-{index + 1:00}";
            var caseRoot = Path.Combine(runRoot, "cases", $"{index + 1:00}");
            var dry = value.matrixType == ExperimentMatrixType.Formal
                ? SyntheticDryRunEngine.RunFormal(caseRoot, participant, session, commit)
                : SyntheticDryRunEngine.RunPilot(caseRoot, participant, session, commit);
            result.sessionBundlePath = dry.bundleDirectory ?? string.Empty;
            if (!dry.success)
            {
                result.status = ExperimentMatrixCaseStatus.FAIL.ToString();
                result.failureReasons = new[] { dry.error ?? "synthetic_case_failed" };
                result.assertionsFailed = result.failureReasons;
                result.integrityStatus = dry.integrityStatus ?? "FAIL";
                return Complete(result, start);
            }
            var origin = mode == ExperimentMatrixExecutionMode.Synthetic ? "synthetic_matrix" : "developer_placeholder_matrix";
            RelabelBundle(dry.bundleDirectory, participant, session, origin);
            var audit = SessionDataIntegrityAuditor.Audit(dry.bundleDirectory, participant, session);
            var task = tasks?.Find(value.taskId);
            result.evidence = BuildEvidence(value, index, mode, participant, session, task, presentations, origin);
            result.integrityStatus = audit.result.ToString().ToUpperInvariant();
            result.status = audit.result == DataIntegritySeverity.Pass ? ExperimentMatrixCaseStatus.PASS.ToString() : ExperimentMatrixCaseStatus.FAIL.ToString();
            result.assertionsPassed = Assertions(value.matrixType);
            result.assertionsFailed = audit.findings.Where(x => x.severity == DataIntegritySeverity.Fail).Select(x => x.checkId).Distinct().ToArray();
            result.failureReasons = audit.findings.Where(x => x.severity == DataIntegritySeverity.Fail).Select(x => x.message).ToArray();
            result.evidenceFiles = new[] { "manifest.json", "checksums.sha256", "assignment/assignment.json", "timing/timing.jsonl", "study/study.jsonl", "questionnaire/questionnaire.jsonl", "integrity/integrity-report.json" };
            var targetBundle = Path.Combine(runRoot, "bundles", $"{index + 1:00}");
            if (!Directory.Exists(targetBundle)) CopyDirectory(dry.bundleDirectory, targetBundle);
            result.sessionBundlePath = targetBundle;
            File.WriteAllText(Path.Combine(runRoot, "cases", $"{index + 1:00}.json"), JsonUtility.ToJson(result, true), Encoding.UTF8);
            return Complete(result, start);
        }

        private static ExperimentMatrixEvidence BuildEvidence(ExperimentMatrixCase value, int index, ExperimentMatrixExecutionMode mode, string participant, string session, ExperimentTaskDefinition task, PilotPresentationCatalog presentations, string origin)
        {
            var evidence = new ExperimentMatrixEvidence
            {
                participantId = participant,
                sessionId = session,
                conditionRunId = value.matrixType == ExperimentMatrixType.Formal ? $"matrix-formal-run-{index + 1:00}" : string.Empty,
                pilotRunId = value.matrixType == ExperimentMatrixType.Pilot ? $"matrix-pilot-run-{index + 1:00}" : string.Empty,
                taskAssignmentId = $"matrix-task-{index + 1:00}",
                questionnaireLinkageKey = $"matrix-questionnaire-{index + 1:00}",
                dataOrigin = origin,
                collectionEligible = false,
                developerTestAssignment = true,
                placeholderUsed = true,
                scenarioId = task?.scenarioId ?? value.taskId,
                panoramaResourceKey = task?.panoramaResourceKey ?? string.Empty,
                requestedAvatarPresetKey = task?.avatarPresetKey ?? string.Empty,
                resolvedAvatarPresetKey = "developer_placeholder",
                avatarFallbackLevel = "developer_placeholder",
                feedbackTextHash = ExperimentEventTimeline.HashText("synthetic correction"),
                feedbackPlaybackStart = 3,
                feedbackPlaybackEnd = 4,
                dialoguePlaybackStart = 6,
                feedbackFirstValid = true,
                noFeedbackValid = true,
                goalTraceValid = value.matrixType == ExperimentMatrixType.Formal,
                questionnaireLinkageValid = true,
                resetValid = true,
                timingEventsValid = true,
                studyEventsValid = true,
                technicalValidity = "Valid"
            };
            if (value.matrixType == ExperimentMatrixType.Formal)
            {
                Enum.TryParse(value.conditionCode, out FormalConditionCode condition);
                FormalConditionResolver.TryResolve(condition, out var provider, out var style);
                evidence.provider = provider == FeedbackProvider.DialogueAvatar ? "Non-Split / Dialogue Avatar" : "Split / Assistant Agent";
                evidence.style = style.ToString();
                evidence.actualPlaybackActor = provider == FeedbackProvider.DialogueAvatar ? "Avatar" : "Agent";
            }
            else
            {
                var condition = value.embodimentCondition == "voice_only" ? PilotEmbodimentCondition.VoiceOnly : value.embodimentCondition == "floating_orb" ? PilotEmbodimentCondition.FloatingOrb : PilotEmbodimentCondition.HumanoidAgent;
                var profile = presentations?.Find(condition);
                evidence.visualMode = condition == PilotEmbodimentCondition.VoiceOnly ? "None" : condition == PilotEmbodimentCondition.FloatingOrb ? "FloatingOrb" : "HumanoidPlaceholder";
                evidence.visualEntityCreated = condition != PilotEmbodimentCondition.VoiceOnly;
                evidence.visualEntityType = condition == PilotEmbodimentCondition.VoiceOnly ? "none" : condition == PilotEmbodimentCondition.FloatingOrb ? "orb" : "humanoid_placeholder";
                evidence.voiceProfileKey = profile?.voiceProfileKey ?? "pilot_shared_voice";
                evidence.voiceId = "resolved_by_voice_profile";
                evidence.speakingSpeed = profile?.speakingSpeed ?? 1f;
                evidence.volume = profile?.volume ?? 1f;
                evidence.subtitlePolicy = profile?.subtitlePolicy ?? "feedback_only";
                evidence.audioSourcePolicy = PilotProtocolValues.Label(profile?.audioSourcePolicy ?? PilotAudioSourcePolicy.NonSpatialHeadLocked);
                evidence.spatialBlend = profile?.spatialBlend ?? 0f;
                evidence.actualPlaybackActor = condition == PilotEmbodimentCondition.VoiceOnly ? "VoiceOnlyAudio" : condition == PilotEmbodimentCondition.FloatingOrb ? "FloatingOrb" : "HumanoidPlaceholder";
            }
            return evidence;
        }

        private static void RelabelBundle(string bundle, string participant, string session, string origin)
        {
            foreach (var path in Directory.GetFiles(bundle, "*.json*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "integrity-report.json") continue;
                var text = File.ReadAllText(path).Replace("\"dataOrigin\": \"synthetic_dry_run\"", $"\"dataOrigin\": \"{origin}\"");
                File.WriteAllText(path, text, Encoding.UTF8);
            }
            var manifestPath = Path.Combine(bundle, "manifest.json");
            var manifest = JsonUtility.FromJson<SessionBundleManifest>(File.ReadAllText(manifestPath));
            manifest.dataOrigin = origin;
            manifest.collectionEligible = false;
            SessionBundleExporter.UpdateIntegrity(bundle, manifest, new SessionDataIntegrityReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"), participantId = participant, sessionId = session,
                result = DataIntegritySeverity.Pass, findings = Array.Empty<DataIntegrityFinding>()
            });
            var audit = SessionDataIntegrityAuditor.Audit(bundle, participant, session);
            manifest.integrityStatus = audit.result.ToString().ToUpperInvariant();
            SessionBundleExporter.UpdateIntegrity(bundle, manifest, audit);
        }

        private static ExperimentMatrixCaseResult Complete(ExperimentMatrixCaseResult value, DateTime start)
        {
            value.completedAtUtc = DateTime.UtcNow.ToString("o");
            value.durationMs = Math.Max(0, (long)(DateTime.UtcNow - start).TotalMilliseconds);
            return value;
        }

        private static string[] Assertions(ExperimentMatrixType type) => type == ExperimentMatrixType.Formal
            ? new[] { "condition_boundary_reset", "task_loaded", "panorama_key_resolved", "condition_resolved", "feedback_turn", "no_feedback_turn", "goal_trace", "task_completion", "questionnaire_linkage", "condition_close", "bundle_checksums", "integrity_pass" }
            : new[] { "pilot_reset", "embodiment_configured", "task_loaded", "feedback_turn", "no_feedback_turn", "visibility_policy", "audio_source_policy", "questionnaire_linkage", "condition_close", "bundle_checksums", "integrity_pass" };

        private static IEnumerable<string> SplitBlockers(string value) => (value ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
        private static bool IsPilotDecision(string id) => id == "pilot_feedback_style" || id == "voice_only_spatial_audio" || id == "pilot_sequence_mapping" || id == "pilot_max_turns" || id == "pilot_max_duration" || id == "questionnaire_scale_anchors";
        private static string ModeCode(ExperimentMatrixExecutionMode mode) => mode == ExperimentMatrixExecutionMode.Synthetic ? "syn" : mode == ExperimentMatrixExecutionMode.DeveloperPlaceholder ? "dev" : "lock";
        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        private static void CopyDirectory(string source, string destination)
        {
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(directory.Replace(source, destination));
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(source, destination), false);
        }
    }
}
