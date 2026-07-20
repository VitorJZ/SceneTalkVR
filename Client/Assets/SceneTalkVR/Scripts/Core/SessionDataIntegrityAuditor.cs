using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum DataIntegritySeverity { Pass, Warning, Fail }
    [Serializable] public sealed class DataIntegrityFinding { public DataIntegritySeverity severity; public string checkId; public string message; public string sourceFile; public int sourceLine; }
    [Serializable] public sealed class SessionDataIntegrityReport { public string schemaVersion="2.0";public string generatedAtUtc;public string participantId;public string sessionId;public DataIntegritySeverity result;public DataIntegrityFinding[] findings=Array.Empty<DataIntegrityFinding>(); }

    public static class SessionDataIntegrityAuditor
    {
        [Serializable] private sealed class Envelope
        {
            public string dataOrigin;public bool collectionEligible;public bool developerTestAssignment;public bool demoMode;public string runtimeMode;public string demoProtocolVersion;public string participantId;public string sessionId;public string experimentSessionId;
            public string protocolVersion;public string pilotProtocolVersion;public string taskCatalogVersion;public string questionnaireCatalogVersion;public string assignmentVersion;public string pilotAssignmentVersion;
            public string eventType;public string conditionRunId;public string questionnaireLinkageKey;public string taskAssignmentId;public string technicalValidity;public string turnId;public long monotonicElapsedMs;
            public string conditionLabel;public string formalConditionCode;public string embodimentCondition;public string taskId;public string goalId;public string feedbackTextHash;public string itemId;public string rawValue;public float scoredValue;public bool reverseScored;public int revision;public int rank;public string interviewLinkageKey;public int runAttempt;public bool hasFeedback;
            public long userEndToFeedbackAudioMs=-1;public long userEndToDialogueAudioMs=-1;public long feedbackToDialogueGapMs=-1;
        }

        public static SessionDataIntegrityReport Audit(string directory,string participantId,string sessionId)
        {
            var findings=new List<DataIntegrityFinding>();
            if(!Directory.Exists(directory))return Build(participantId,sessionId,findings,DataIntegritySeverity.Fail,"session_directory_missing",directory);
            var before=Snapshot(directory);var files=Directory.GetFiles(directory,"*",SearchOption.AllDirectories);var dataFiles=files.Where(IsJsonData).ToArray();
            var manifestPath=Path.Combine(directory,"manifest.json");SessionBundleManifest manifest=null;if(File.Exists(manifestPath)){try{manifest=JsonUtility.FromJson<SessionBundleManifest>(File.ReadAllText(manifestPath));}catch{Add(findings,DataIntegritySeverity.Fail,"manifest_parse","Manifest JSON is invalid.",manifestPath);}}
            if(manifest!=null)ValidateManifest(directory,manifest,participantId,sessionId,findings);
            var assignmentPath=dataFiles.FirstOrDefault(x=>x.IndexOf("assignment",StringComparison.OrdinalIgnoreCase)>=0);
            Add(findings,assignmentPath==null?DataIntegritySeverity.Fail:DataIntegritySeverity.Pass,"assignment_exists",assignmentPath==null?"Assignment snapshot not found.":"Assignment snapshot found.",assignmentPath);
            Envelope assignment=null;if(assignmentPath!=null)assignment=Parse(File.ReadAllText(assignmentPath),assignmentPath,1,findings);
            if(assignment!=null)ValidateAssignment(assignment,manifest,participantId,sessionId,findings,assignmentPath);
            var records=new List<(Envelope item,string file,int line)>();
            foreach(var file in dataFiles.Where(x=>x.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)))
            {var line=0;foreach(var json in File.ReadLines(file)){line++;if(string.IsNullOrWhiteSpace(json))continue;var item=Parse(json,file,line,findings);if(item!=null)records.Add((item,file,line));}}
            foreach(var record in records)ValidateIdentityAndVersions(record.item,manifest,participantId,sessionId,findings,record.file,record.line);
            ValidateTiming(records,findings);ValidateStudy(records,manifest,findings);ValidateQuestionnaire(records,manifest,findings);ValidateRankingInterview(records,manifest,findings);
            if(dataFiles.Length==0)Add(findings,DataIntegritySeverity.Fail,"session_files","No session JSON/JSONL files matched.",directory);
            var after=Snapshot(directory);if(!before.SequenceEqual(after))Add(findings,DataIntegritySeverity.Fail,"read_only","Audit changed source file metadata.",directory);else Add(findings,DataIntegritySeverity.Pass,"read_only","Audit completed without modifying source data.");
            var result=findings.Any(x=>x.severity==DataIntegritySeverity.Fail)?DataIntegritySeverity.Fail:findings.Any(x=>x.severity==DataIntegritySeverity.Warning)?DataIntegritySeverity.Warning:DataIntegritySeverity.Pass;
            return new SessionDataIntegrityReport{generatedAtUtc=DateTime.UtcNow.ToString("o"),participantId=participantId,sessionId=sessionId,result=result,findings=findings.ToArray()};
        }

        public static void WriteReport(SessionDataIntegrityReport report,string outputPath){var parent=Path.GetDirectoryName(outputPath);if(!string.IsNullOrWhiteSpace(parent))Directory.CreateDirectory(parent);File.WriteAllText(outputPath,JsonUtility.ToJson(report,true),Encoding.UTF8);}

        private static void ValidateManifest(string root,SessionBundleManifest m,string p,string s,List<DataIntegrityFinding> f)
        {
            if(m.participantId!=p||m.sessionId!=s)Add(f,DataIntegritySeverity.Fail,"manifest_identity","Manifest participant/session mismatch.");
            if(m.dataOrigin=="synthetic_dry_run"&&(m.collectionEligible||m.dataOrigin!="synthetic_dry_run"))Add(f,DataIntegritySeverity.Fail,"synthetic_collection_isolation","Synthetic bundle is collection eligible.");
            if(m.dataOrigin=="editor_demo"&&(m.collectionEligible||!m.developerTestAssignment||!m.demoMode))Add(f,DataIntegritySeverity.Fail,"editor_demo_collection_isolation","Editor Demo bundle isolation metadata is invalid.");
            foreach(var required in new[]{"assignment/","timing/","study/","questionnaire/","ranking/"})if(m.files==null||!m.files.Any(x=>x.relativePath.StartsWith(required,StringComparison.OrdinalIgnoreCase)))Add(f,DataIntegritySeverity.Fail,"bundle_required_file_missing",required);
            foreach(var record in m.files??Array.Empty<SessionBundleFileRecord>()){var path=Path.Combine(root,record.relativePath.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path))Add(f,DataIntegritySeverity.Fail,"bundle_manifest_file_missing",record.relativePath);else if(SessionBundleExporter.Sha256File(path)!=record.sha256)Add(f,DataIntegritySeverity.Fail,"bundle_checksum_mismatch",record.relativePath);}
            var checksum=Path.Combine(root,"checksums.sha256");if(!File.Exists(checksum))Add(f,DataIntegritySeverity.Fail,"bundle_checksum_file_missing","checksums.sha256 missing.");else foreach(var line in File.ReadAllLines(checksum).Where(x=>!string.IsNullOrWhiteSpace(x))){var split=line.Split(new[]{"  "},2,StringSplitOptions.None);if(split.Length!=2)Add(f,DataIntegritySeverity.Fail,"checksum_format","Invalid checksum line.");else{var path=Path.Combine(root,split[1].Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path)||SessionBundleExporter.Sha256File(path)!=split[0])Add(f,DataIntegritySeverity.Fail,"checksum_invalid",split[1]);}}
        }

        private static void ValidateAssignment(Envelope a,SessionBundleManifest m,string p,string s,List<DataIntegrityFinding> f,string file)
        {
            var actualSession=string.IsNullOrWhiteSpace(a.sessionId)?a.experimentSessionId:a.sessionId;if(a.participantId!=p||actualSession!=s)Add(f,DataIntegritySeverity.Fail,"assignment_identity","Assignment participant/session mismatch.",file);
            if(a.dataOrigin=="synthetic_dry_run"&&(a.collectionEligible||m!=null&&m.collectionEligible))Add(f,DataIntegritySeverity.Fail,"synthetic_assignment_collection_eligible","Synthetic Assignment cannot be collection eligible.",file);
            if(a.dataOrigin=="editor_demo"&&(a.collectionEligible||m!=null&&m.collectionEligible))Add(f,DataIntegritySeverity.Fail,"editor_demo_assignment_collection_eligible","Editor Demo Assignment cannot be collection eligible.",file);
            if(m!=null){var protocol=string.IsNullOrWhiteSpace(a.protocolVersion)?a.pilotProtocolVersion:a.protocolVersion;var version=string.IsNullOrWhiteSpace(a.assignmentVersion)?a.pilotAssignmentVersion:a.assignmentVersion;if(protocol!=m.protocolVersion||a.taskCatalogVersion!=m.taskCatalogVersion||version!=m.assignmentVersion)Add(f,DataIntegritySeverity.Fail,"assignment_version_mismatch","Assignment and bundle versions differ.",file);}
        }

        private static void ValidateIdentityAndVersions(Envelope e,SessionBundleManifest m,string p,string s,List<DataIntegrityFinding> f,string file,int line)
        {if((!string.IsNullOrWhiteSpace(e.participantId)&&e.participantId!=p)||(!string.IsNullOrWhiteSpace(e.sessionId)&&e.sessionId!=s))Add(f,DataIntegritySeverity.Fail,"participant_session_consistency","Record belongs to another participant/session.",file,line);if(e.dataOrigin=="editor_demo"&&(e.collectionEligible||!e.developerTestAssignment||!e.demoMode))Add(f,DataIntegritySeverity.Fail,"editor_demo_event_isolation","Editor Demo event isolation metadata is invalid.",file,line);if(m!=null&&(!string.IsNullOrWhiteSpace(e.protocolVersion)&&e.protocolVersion!=m.protocolVersion||!string.IsNullOrWhiteSpace(e.taskCatalogVersion)&&e.taskCatalogVersion!=m.taskCatalogVersion||!string.IsNullOrWhiteSpace(e.questionnaireCatalogVersion)&&e.questionnaireCatalogVersion!=m.questionnaireCatalogVersion||!string.IsNullOrWhiteSpace(e.assignmentVersion)&&e.assignmentVersion!=m.assignmentVersion))Add(f,DataIntegritySeverity.Fail,"record_version_mismatch","Record version differs from bundle manifest.",file,line);}

        private static void ValidateTiming(List<(Envelope item,string file,int line)> records,List<DataIntegrityFinding> f)
        {
            foreach(var turn in records.Where(x=>!string.IsNullOrWhiteSpace(x.item.turnId)).GroupBy(x=>x.item.turnId))
            {var raw=turn.ToArray();for(var i=1;i<raw.Length;i++)if(raw[i].item.monotonicElapsedMs<raw[i-1].item.monotonicElapsedMs)Add(f,DataIntegritySeverity.Fail,"timing_monotonic",turn.Key,raw[i].file,raw[i].line);var events=raw.OrderBy(x=>x.item.monotonicElapsedMs).ToArray();int At(string name)=>Array.FindIndex(events,x=>x.item.eventType==name);var gate=At("DialogueGateClosed");var dialogue=At("DialoguePlaybackStarted");var feedbackStart=At("CorrectionPlaybackStarted");var feedbackEnd=At("CorrectionPlaybackEnded");var summary=events.FirstOrDefault(x=>x.item.eventType=="TurnSummary").item;var hasFeedback=summary?.hasFeedback??feedbackStart>=0;if(gate<0||dialogue<0||gate>dialogue)Add(f,DataIntegritySeverity.Fail,"dialogue_gate_order",turn.Key);if(hasFeedback&&(feedbackStart<0||feedbackEnd<0||feedbackEnd>dialogue))Add(f,DataIntegritySeverity.Fail,"feedback_first",turn.Key);if(!hasFeedback&&(feedbackStart>=0||feedbackEnd>=0))Add(f,DataIntegritySeverity.Fail,"no_feedback_correction_playback",turn.Key);if(summary!=null){long Diff(string a,string b){var x=events.FirstOrDefault(v=>v.item.eventType==a).item;var y=events.FirstOrDefault(v=>v.item.eventType==b).item;return x==null||y==null?-1:y.monotonicElapsedMs-x.monotonicElapsedMs;}if(summary.userEndToFeedbackAudioMs!=Diff("UserSpeechEnded","CorrectionPlaybackStarted")||summary.userEndToDialogueAudioMs!=Diff("UserSpeechEnded","DialoguePlaybackStarted")||summary.feedbackToDialogueGapMs!=Diff("CorrectionPlaybackEnded","DialoguePlaybackStarted"))Add(f,DataIntegritySeverity.Fail,"timing_summary_mismatch",turn.Key);}}
        }

        private static void ValidateStudy(List<(Envelope item,string file,int line)> records,SessionBundleManifest m,List<DataIntegrityFinding> f)
        {
            var study=records.Where(x=>x.file.IndexOf("study",StringComparison.OrdinalIgnoreCase)>=0).ToArray();var validRuns=study.Where(x=>x.item.eventType=="ConditionStarted"&&!string.Equals(x.item.technicalValidity,"TechnicalInvalid",StringComparison.OrdinalIgnoreCase)).Select(x=>x.item.conditionRunId).Distinct().ToArray();var expected=m?.sessionMode?.IndexOf("pilot",StringComparison.OrdinalIgnoreCase)>=0?3:m?.sessionMode?.IndexOf("formal",StringComparison.OrdinalIgnoreCase)>=0?4:0;if(expected>0&&validRuns.Length!=expected)Add(f,DataIntegritySeverity.Fail,"condition_count",$"Expected {expected}, got {validRuns.Length}.");foreach(var run in validRuns){var events=study.Where(x=>x.item.conditionRunId==run).Select(x=>x.item.eventType).ToArray();if(!events.Contains("ConditionPrepared")||!events.Contains("ConditionStarted")||!events.Contains("ConditionCompleted")||!events.Contains("ConditionBoundaryReset"))Add(f,DataIntegritySeverity.Fail,"condition_not_closed",run);var candidates=study.Where(x=>x.item.conditionRunId==run&&x.item.eventType=="GoalCandidateSubmitted").Select(x=>x.item.goalId).ToArray();foreach(var goal in candidates)if(!study.Any(x=>x.item.conditionRunId==run&&x.item.goalId==goal&&(x.item.eventType=="GoalConfirmed"||x.item.eventType=="GoalRejected")))Add(f,DataIntegritySeverity.Fail,"goal_untraceable",run+":"+goal);}
            foreach(var invalid in study.Where(x=>x.item.eventType=="ConditionTechnicalInvalid"))if(study.Any(x=>x.item.conditionRunId==invalid.item.conditionRunId&&x.item.eventType=="ConditionCompleted"))Add(f,DataIntegritySeverity.Fail,"invalid_not_completed",invalid.item.conditionRunId);var retries=study.Where(x=>x.item.eventType=="RetryAuthorized").ToArray();foreach(var retry in retries)if(study.Any(x=>x.item.conditionRunId==retry.item.conditionRunId&&x.item.eventType=="ConditionTechnicalInvalid"))Add(f,DataIntegritySeverity.Fail,"retry_run_id_reused",retry.item.conditionRunId);
        }

        private static void ValidateQuestionnaire(List<(Envelope item,string file,int line)> records,SessionBundleManifest m,List<DataIntegrityFinding> f)
        {
            var q=records.Where(x=>x.file.IndexOf("questionnaire",StringComparison.OrdinalIgnoreCase)>=0).ToArray();var completed=records.Where(x=>x.item.eventType=="ConditionCompleted"&&!string.Equals(x.item.technicalValidity,"TechnicalInvalid",StringComparison.OrdinalIgnoreCase)).Select(x=>x.item.conditionRunId).Distinct().ToArray();foreach(var run in completed){var submits=q.Where(x=>x.item.conditionRunId==run&&x.item.eventType=="QuestionnaireSubmitted").ToArray();if(submits.Length!=1)Add(f,DataIntegritySeverity.Fail,"questionnaire_submit_count",run);if(!q.Any(x=>x.item.conditionRunId==run&&x.item.eventType=="QuestionnaireItem"))Add(f,DataIntegritySeverity.Fail,"questionnaire_required_items_missing",run);}
            foreach(var item in q.Where(x=>x.item.eventType=="QuestionnaireItem")){if(item.item.revision<1||string.IsNullOrWhiteSpace(item.item.rawValue)||item.item.scoredValue<1||item.item.scoredValue>7)Add(f,DataIntegritySeverity.Fail,"questionnaire_value_invalid",item.item.conditionRunId,item.file,item.line);if(item.item.reverseScored&&float.TryParse(item.item.rawValue,NumberStyles.Float,CultureInfo.InvariantCulture,out var raw)&&Math.Abs(item.item.scoredValue-(8-raw))>.001f)Add(f,DataIntegritySeverity.Fail,"reverse_score_invalid",item.item.conditionRunId,item.file,item.line);}
            foreach(var link in q.Where(x=>x.item.eventType=="QuestionnaireSubmitted").GroupBy(x=>x.item.questionnaireLinkageKey))if(string.IsNullOrWhiteSpace(link.Key)||link.Count()!=1)Add(f,DataIntegritySeverity.Fail,"questionnaire_linkage_or_duplicate",link.Key??"<empty>");
        }

        private static void ValidateRankingInterview(List<(Envelope item,string file,int line)> records,SessionBundleManifest m,List<DataIntegrityFinding> f)
        {
            if(m==null)return;var formal=m.sessionMode?.IndexOf("formal",StringComparison.OrdinalIgnoreCase)>=0;var expected=formal?new[]{"NE","NR","SE","SR"}:new[]{"voice_only","floating_orb","humanoid_agent"};var type=formal?"FinalRankingEntry":"PilotFinalRankingEntry";var ranking=records.Where(x=>x.item.eventType==type).Select(x=>x.item).ToArray();if(ranking.Length!=expected.Length||ranking.Select(x=>x.rank).Distinct().Count()!=expected.Length||!new HashSet<string>(ranking.Select(x=>x.conditionLabel),StringComparer.OrdinalIgnoreCase).SetEquals(expected))Add(f,DataIntegritySeverity.Fail,"ranking_incomplete_or_duplicate",m.sessionMode);var lastCompleted=records.Where(x=>x.item.eventType=="ConditionCompleted").Select(x=>x.item.monotonicElapsedMs).DefaultIfEmpty(-1).Max();var submitted=records.FirstOrDefault(x=>x.item.eventType==(formal?"FinalRankingSubmitted":"PilotFinalRankingSubmitted")).item;if(submitted==null||submitted.monotonicElapsedMs<=lastCompleted)Add(f,DataIntegritySeverity.Fail,"ranking_submitted_too_early",m.sessionMode);if(formal&&!records.Any(x=>x.item.eventType=="InterviewSaved"&&!string.IsNullOrWhiteSpace(x.item.interviewLinkageKey)))Add(f,DataIntegritySeverity.Fail,"interview_linkage_missing","formal");if(!formal){var hashes=records.Where(x=>x.item.eventType=="CorrectionPlaybackStarted").Select(x=>x.item.feedbackTextHash).Distinct().ToArray();if(hashes.Length!=1||string.IsNullOrWhiteSpace(hashes[0]))Add(f,DataIntegritySeverity.Fail,"pilot_feedback_hash_mismatch","pilot");}
        }

        private static Envelope Parse(string json,string file,int line,List<DataIntegrityFinding> findings){try{return JsonUtility.FromJson<Envelope>(json);}catch{Add(findings,DataIntegritySeverity.Fail,"json_parse","Invalid JSON record.",file,line);return null;}}
        private static bool IsJsonData(string path)=>path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase);
        private static string[] Snapshot(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(x=>$"{x}|{new FileInfo(x).Length}|{File.GetLastWriteTimeUtc(x).Ticks}").ToArray();
        private static SessionDataIntegrityReport Build(string p,string s,List<DataIntegrityFinding> list,DataIntegritySeverity severity,string id,string source){Add(list,severity,id,id,source);return new SessionDataIntegrityReport{generatedAtUtc=DateTime.UtcNow.ToString("o"),participantId=p,sessionId=s,result=severity,findings=list.ToArray()};}
        private static void Add(List<DataIntegrityFinding> list,DataIntegritySeverity severity,string id,string message,string source="",int line=0)=>list.Add(new DataIntegrityFinding{severity=severity,checkId=id,message=message,sourceFile=source??"",sourceLine=line});
    }
}
