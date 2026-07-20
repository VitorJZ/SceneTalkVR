using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum SyntheticDryRunKind { Formal, Pilot }

    [Serializable]
    public sealed class SyntheticDryRunEvent
    {
        public string schemaVersion="1.0"; public string dataOrigin="synthetic_dry_run"; public bool collectionEligible;
        public bool developerTestAssignment; public bool demoMode; public string runtimeMode; public string demoProtocolVersion;
        public string participantId; public string sessionId; public string protocolVersion; public string taskCatalogVersion; public string questionnaireCatalogVersion; public string assignmentVersion;
        public string eventType; public string conditionRunId; public string questionnaireLinkageKey; public string taskAssignmentId; public string conditionLabel; public string taskId; public string turnId;
        public string technicalValidity="Valid"; public long monotonicElapsedMs; public bool hasFeedback; public string feedbackTextHash; public string goalId; public int revision;
        public string itemId; public string rawValue; public float scoredValue; public bool reverseScored; public int rank; public string interviewLinkageKey; public int runAttempt;
        public long userEndToFeedbackAudioMs=-1; public long userEndToDialogueAudioMs=-1; public long feedbackToDialogueGapMs=-1;
        public string fakeStt="deterministic_fake_stt"; public string fakePlanner="deterministic_fake_planner"; public string fakeTts="deterministic_fake_tts"; public string visual="developer_placeholder";
    }

    [Serializable]
    public sealed class SessionBundleFileRecord { public string relativePath; public long sizeBytes; public string sha256; }

    [Serializable]
    public sealed class SessionBundleManifest
    {
        public string bundleSchemaVersion="1.0"; public string dataOrigin; public bool collectionEligible; public string sessionMode;
        public bool developerTestAssignment; public bool demoMode; public string demoProtocolVersion; public string officialProtocolVersion; public string runtimeMode;
        public string flowMode; public string runQualification; public string protocolSnapshotId; public string resourceSnapshotId;
        public string participantId; public string sessionId; public string gitCommit; public string protocolVersion; public string taskCatalogVersion; public string questionnaireCatalogVersion; public string assignmentVersion;
        public string formalConditionOrderPolicy; public string taskAssignmentPolicy; public string goalConfirmationPolicy; public string questionnaireReturnPolicy; public string assignmentAlgorithmVersion; public string randomSeedHash;
        public string createdAtUtc; public SessionBundleFileRecord[] files=Array.Empty<SessionBundleFileRecord>(); public string integrityStatus="PENDING";
    }

    [Serializable]
    public sealed class SyntheticDryRunResult
    {
        public bool success; public string mode; public string participantId; public string sessionId; public string sourceDirectory; public string bundleDirectory; public string integrityStatus; public string error;
    }

    public static class SyntheticDryRunEngine
    {
        private static readonly string[] FormalConditions={"NE","NR","SE","SR"};
        private static readonly string[] FormalTasks={"hotel_check_in","furniture_shopping","gym_membership","tourist_assistance"};
        private static readonly string[] PilotConditions={"voice_only","floating_orb","humanoid_agent"};
        private static readonly string[] PilotTasks={"pilot_restaurant_walk_in","pilot_restaurant_ordering","pilot_restaurant_wrong_dish"};
        public const string Banner="SYNTHETIC DRY RUN — NOT PARTICIPANT DATA";

        public static SyntheticDryRunResult RunFormal(string root,string participantId,string sessionId,string gitCommit="test-commit") => Run(root,participantId,sessionId,gitCommit,SyntheticDryRunKind.Formal);
        public static SyntheticDryRunResult RunPilot(string root,string participantId,string sessionId,string gitCommit="test-commit") => Run(root,participantId,sessionId,gitCommit,SyntheticDryRunKind.Pilot);

        private static SyntheticDryRunResult Run(string root,string participant,string session,string commit,SyntheticDryRunKind kind)
        {
            var result=new SyntheticDryRunResult{mode=kind.ToString(),participantId=participant,sessionId=session};
            if(string.IsNullOrWhiteSpace(root)||string.IsNullOrWhiteSpace(participant)||string.IsNullOrWhiteSpace(session)){result.error="synthetic_root_participant_session_required";return result;}
            var raw=Path.Combine(root,"SyntheticRaw",Safe(participant)+"_"+Safe(session));var bundle=Path.Combine(root,"SyntheticBundles",Safe(participant)+"_"+Safe(session));
            if(Directory.Exists(raw)||Directory.Exists(bundle)){result.error="synthetic_session_already_exists_resume_or_use_new_session";return result;}
            foreach(var folder in new[]{"assignment","timing","study","questionnaire","ranking","interview"})Directory.CreateDirectory(Path.Combine(raw,folder));
            var timing=new List<SyntheticDryRunEvent>();var study=new List<SyntheticDryRunEvent>();var questionnaire=new List<SyntheticDryRunEvent>();var ranking=new List<SyntheticDryRunEvent>();var interview=new List<SyntheticDryRunEvent>();
            if(kind==SyntheticDryRunKind.Formal)BuildFormal(participant,session,timing,study,questionnaire,ranking,interview);
            else BuildPilot(participant,session,timing,study,questionnaire,ranking);
            var assignment=kind==SyntheticDryRunKind.Formal?BuildFormalAssignment(participant,session):null;
            if(assignment!=null)File.WriteAllText(Path.Combine(raw,"assignment","assignment.json"),JsonUtility.ToJson(assignment,true),Encoding.UTF8);
            else File.WriteAllText(Path.Combine(raw,"assignment","assignment.json"),JsonUtility.ToJson(BuildPilotAssignment(participant,session),true),Encoding.UTF8);
            WriteJsonl(Path.Combine(raw,"timing","timing.jsonl"),timing);WriteJsonl(Path.Combine(raw,"study","study.jsonl"),study);WriteJsonl(Path.Combine(raw,"questionnaire","questionnaire.jsonl"),questionnaire);WriteJsonl(Path.Combine(raw,"ranking","ranking.jsonl"),ranking);WriteJsonl(Path.Combine(raw,"interview","interview.jsonl"),interview);
            var manifest=new SessionBundleManifest{dataOrigin="synthetic_dry_run",collectionEligible=false,sessionMode=kind.ToString().ToLowerInvariant(),participantId=participant,sessionId=session,gitCommit=commit,protocolVersion="synthetic-v1.1-stage8-test-only",taskCatalogVersion="1.1.0-stage2",questionnaireCatalogVersion="1.1-stage5.1",assignmentVersion="synthetic-1.0",createdAtUtc=DateTime.UtcNow.ToString("o")};
            if(!SessionBundleExporter.Export(raw,bundle,manifest,out var exportError)){result.error=exportError;return result;}
            var audit=SessionDataIntegrityAuditor.Audit(bundle,participant,session);manifest.integrityStatus=audit.result.ToString().ToUpperInvariant();SessionBundleExporter.UpdateIntegrity(bundle,manifest,audit);
            result.success=audit.result==DataIntegritySeverity.Pass;result.sourceDirectory=raw;result.bundleDirectory=bundle;result.integrityStatus=manifest.integrityStatus;result.error=result.success?"":string.Join(";",audit.findings.Where(x=>x.severity==DataIntegritySeverity.Fail).Select(x=>x.checkId));return result;
        }

        private static void BuildFormal(string p,string s,List<SyntheticDryRunEvent> timing,List<SyntheticDryRunEvent> study,List<SyntheticDryRunEvent> q,List<SyntheticDryRunEvent> ranking,List<SyntheticDryRunEvent> interview)
        {
            long clock=0;for(var i=0;i<4;i++){var run=$"formal-run-{i+1}";var task=$"formal-ta-{i+1}";var link=$"formal-q-{i+1}";Add(study,p,s,"ConditionPrepared",run,link,task,FormalConditions[i],FormalTasks[i],ref clock);Add(study,p,s,"ConditionStarted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock);Add(study,p,s,"GoalCandidateSubmitted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-1");Add(study,p,s,"GoalConfirmed",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-1");Add(study,p,s,"GoalCandidateSubmitted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-2");Add(study,p,s,"GoalRejected",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-2");Add(study,p,s,"GoalCandidateSubmitted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-2");Add(study,p,s,"GoalConfirmed",run,link,task,FormalConditions[i],FormalTasks[i],ref clock,goal:"goal-2");
                BuildTurn(timing,p,s,run,link,task,FormalConditions[i],FormalTasks[i],$"turn-{i+1}-feedback",true,ref clock);BuildTurn(timing,p,s,run,link,task,FormalConditions[i],FormalTasks[i],$"turn-{i+1}-no-feedback",false,ref clock);
                q.Add(E(p,s,"QuestionnaireItem",run,link,task,FormalConditions[i],FormalTasks[i],clock++,item:"reverse_item",raw:"2",score:6,reverse:true,revision:1));q.Add(E(p,s,"QuestionnaireItem",run,link,task,FormalConditions[i],FormalTasks[i],clock++,item:"support_item",raw:"5",score:5,revision:1));Add(q,p,s,"QuestionnaireSubmitted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock);Add(study,p,s,"ConditionCompleted",run,link,task,FormalConditions[i],FormalTasks[i],ref clock);Add(study,p,s,"ConditionBoundaryReset",run,link,task,FormalConditions[i],FormalTasks[i],ref clock);}
            for(var i=0;i<4;i++)ranking.Add(E(p,s,"FinalRankingEntry","","",$"formal-ta-{i+1}",FormalConditions[i],FormalTasks[i],clock++,rank:i+1));Add(study,p,s,"FinalRankingSubmitted","","","","","",ref clock);interview.Add(E(p,s,"InterviewSaved","","","","","",clock++,interviewLink:"formal-interview-1"));Add(study,p,s,"InterviewCompleted","","","","","",ref clock);Add(study,p,s,"ExperimentCompleted","","","","","",ref clock);
        }

        private static void BuildPilot(string p,string s,List<SyntheticDryRunEvent> timing,List<SyntheticDryRunEvent> study,List<SyntheticDryRunEvent> q,List<SyntheticDryRunEvent> ranking)
        {
            long clock=0;var hash=ExperimentEventTimeline.HashText("Use: I went to the restaurant.");
            Add(study,p,s,"ConditionStarted","pilot-run-invalid","pilot-q-invalid","pilot-ta-1",PilotConditions[0],PilotTasks[0],ref clock,validity:"TechnicalInvalid",attempt:1);Add(study,p,s,"ConditionTechnicalInvalid","pilot-run-invalid","pilot-q-invalid","pilot-ta-1",PilotConditions[0],PilotTasks[0],ref clock,validity:"TechnicalInvalid",attempt:1);Add(study,p,s,"RetryAuthorized","pilot-run-1","pilot-q-1","pilot-ta-1",PilotConditions[0],PilotTasks[0],ref clock,attempt:2);
            for(var i=0;i<3;i++){var run=$"pilot-run-{i+1}";var task=$"pilot-ta-{i+1}";var link=$"pilot-q-{i+1}";Add(study,p,s,"ConditionPrepared",run,link,task,PilotConditions[i],PilotTasks[i],ref clock,attempt:i==0?2:1);Add(study,p,s,"ConditionStarted",run,link,task,PilotConditions[i],PilotTasks[i],ref clock,attempt:i==0?2:1);BuildTurn(timing,p,s,run,link,task,PilotConditions[i],PilotTasks[i],$"pilot-turn-{i+1}",true,ref clock,hash);q.Add(E(p,s,"QuestionnaireItem",run,link,task,PilotConditions[i],PilotTasks[i],clock++,item:"pilot_item",raw:"5",score:5,revision:1));Add(q,p,s,"QuestionnaireSubmitted",run,link,task,PilotConditions[i],PilotTasks[i],ref clock);Add(study,p,s,"ConditionCompleted",run,link,task,PilotConditions[i],PilotTasks[i],ref clock);Add(study,p,s,"ConditionBoundaryReset",run,link,task,PilotConditions[i],PilotTasks[i],ref clock);}
            for(var i=0;i<3;i++)ranking.Add(E(p,s,"PilotFinalRankingEntry","","",$"pilot-ta-{i+1}",PilotConditions[i],PilotTasks[i],clock++,rank:i+1));Add(study,p,s,"PilotFinalRankingSubmitted","","","","","",ref clock);
        }

        private static void BuildTurn(List<SyntheticDryRunEvent> list,string p,string s,string run,string link,string ta,string condition,string task,string turn,bool feedback,ref long clock,string forcedHash="")
        {
            var localClock=clock;AddTiming("UserSpeechEnded");AddTiming("DialogueGateClosed");if(feedback){AddTiming("CorrectionRequestStarted");AddTiming("CorrectionPlaybackStarted");AddTiming("CorrectionPlaybackEnded");}AddTiming("DialogueGateOpened");AddTiming("DialoguePlaybackStarted");AddTiming("DialoguePlaybackEnded");AddTiming("TurnCompleted");
            var summary=E(p,s,"TurnSummary",run,link,ta,condition,task,localClock++,turn:turn,feedback:feedback,hash:feedback?(string.IsNullOrEmpty(forcedHash)?ExperimentEventTimeline.HashText("synthetic correction"):forcedHash):"");summary.userEndToFeedbackAudioMs=feedback?3:-1;summary.userEndToDialogueAudioMs=feedback?6:3;summary.feedbackToDialogueGapMs=feedback?2:-1;list.Add(summary);clock=localClock;
            void AddTiming(string type){var item=E(p,s,type,run,link,ta,condition,task,localClock++,turn:turn,feedback:feedback,hash:feedback?(string.IsNullOrEmpty(forcedHash)?ExperimentEventTimeline.HashText("synthetic correction"):forcedHash):"");list.Add(item);}
        }
        private static void Add(List<SyntheticDryRunEvent> target,string p,string s,string type,string run,string link,string ta,string condition,string task,ref long clock,string goal="",string validity="Valid",int attempt=1)=>target.Add(E(p,s,type,run,link,ta,condition,task,clock++,goal:goal,validity:validity,attempt:attempt));
        private static SyntheticDryRunEvent E(string p,string s,string type,string run,string link,string ta,string condition,string task,long clock,string turn="",bool feedback=false,string hash="",string goal="",string validity="Valid",string item="",string raw="",float score=0,bool reverse=false,int revision=0,int rank=0,string interviewLink="",int attempt=1)=>new SyntheticDryRunEvent{participantId=p,sessionId=s,protocolVersion="synthetic-v1.1-stage8-test-only",taskCatalogVersion="1.1.0-stage2",questionnaireCatalogVersion="1.1-stage5.1",assignmentVersion="synthetic-1.0",eventType=type,conditionRunId=run,questionnaireLinkageKey=link,taskAssignmentId=ta,conditionLabel=condition,taskId=task,turnId=turn,hasFeedback=feedback,feedbackTextHash=hash,goalId=goal,technicalValidity=validity,monotonicElapsedMs=clock,itemId=item,rawValue=raw,scoredValue=score,reverseScored=reverse,revision=revision,rank=rank,interviewLinkageKey=interviewLink,runAttempt=attempt};

        private static ExperimentAssignment BuildFormalAssignment(string p,string s)=>new ExperimentAssignment{participantId=p,experimentSessionId=s,assignmentVersion="synthetic-1.0",protocolVersion="synthetic-v1.1-stage8-test-only",taskCatalogVersion="1.1.0-stage2",sequenceId="test-a-b-c-d",createdAtUtc=DateTime.UtcNow.ToString("o"),policy=AssignmentPolicy.StrictWithoutReplacement,status=AssignmentStatus.Completed,developerTestAssignment=true,dataOrigin="synthetic_dry_run",collectionEligible=false,conditions=FormalConditions.Select((x,i)=>new ConditionAssignment{conditionPosition=i,formalConditionCode=(FormalConditionCode)i,formalConditionLabel=x,task=new TaskAssignment{taskId=FormalTasks[i],taskAssignmentId=$"formal-ta-{i+1}"},status=ConditionRunStatus.Completed,latestConditionRunId=$"formal-run-{i+1}",runAttempt=1}).ToArray()};
        private static PilotAssignment BuildPilotAssignment(string p,string s)=>new PilotAssignment{participantId=p,sessionId=s,pilotProtocolVersion="synthetic-v1.1-stage8-test-only",pilotAssignmentVersion="synthetic-1.0",taskCatalogVersion="1.1.0-stage2",sequenceId="test-a-b-c",createdAtUtc=DateTime.UtcNow.ToString("o"),developerTestAssignment=true,dataOrigin="synthetic_dry_run",collectionEligible=false,feedbackStyle=PilotFeedbackStyleChoice.Explicit,feedbackStyleLabel="explicit",voiceOnlyAudioPolicy=PilotAudioSourcePolicy.NonSpatialHeadLocked,voiceOnlyAudioPolicyLabel="non_spatial_head_locked",conditions=PilotConditions.Select((x,i)=>new PilotConditionAssignment{conditionPosition=i,embodimentCondition=(PilotEmbodimentCondition)i,embodimentConditionLabel=x,task=new PilotTaskAssignment{taskId=PilotTasks[i],taskAssignmentId=$"pilot-ta-{i+1}"},status=PilotRunStatus.Completed,latestPilotRunId=$"pilot-run-{i+1}",runAttempt=i==0?2:1}).ToArray()};
        private static void WriteJsonl(string path,IEnumerable<SyntheticDryRunEvent> events)=>File.WriteAllLines(path,events.Select(JsonUtility.ToJson),Encoding.UTF8);
        private static string Safe(string value)=>new string((value??"").Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
    }

    public static class SessionBundleExporter
    {
        public static bool Export(string source,string destination,SessionBundleManifest manifest,out string error)
        {
            if(!Directory.Exists(source)){error="bundle_source_missing";return false;}if(Directory.Exists(destination)){error="bundle_destination_exists";return false;}
            var required=new[]{"assignment","timing","study","questionnaire","ranking"};if(required.Any(x=>!Directory.Exists(Path.Combine(source,x))||Directory.GetFiles(Path.Combine(source,x)).Length==0)){error="bundle_required_category_missing";return false;}
            Directory.CreateDirectory(destination);var records=new List<SessionBundleFileRecord>();
            foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
            {var relative=Path.GetRelativePath(source,file).Replace('\\','/');var text=File.ReadAllText(file);if(ContainsSensitive(text)){error="bundle_sensitive_content_rejected:"+relative;return false;}var target=Path.Combine(destination,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target,false);records.Add(Record(target,destination));}
            manifest.files=records.OrderBy(x=>x.relativePath).ToArray();File.WriteAllText(Path.Combine(destination,"manifest.json"),JsonUtility.ToJson(manifest,true),Encoding.UTF8);WriteChecksums(destination);error="";return true;
        }
        public static void UpdateIntegrity(string bundle,SessionBundleManifest manifest,SessionDataIntegrityReport audit)
        {var folder=Path.Combine(bundle,"integrity");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"integrity-report.json"),JsonUtility.ToJson(audit,true),Encoding.UTF8);var all=Directory.GetFiles(bundle,"*",SearchOption.AllDirectories).Where(x=>Path.GetFileName(x)!="checksums.sha256"&&Path.GetFileName(x)!="manifest.json").Select(x=>Record(x,bundle)).OrderBy(x=>x.relativePath).ToArray();manifest.files=all;File.WriteAllText(Path.Combine(bundle,"manifest.json"),JsonUtility.ToJson(manifest,true),Encoding.UTF8);WriteChecksums(bundle);}
        public static string Sha256File(string path){using var sha=SHA256.Create();using var stream=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static SessionBundleFileRecord Record(string file,string root)=>new SessionBundleFileRecord{relativePath=Path.GetRelativePath(root,file).Replace('\\','/'),sizeBytes=new FileInfo(file).Length,sha256=Sha256File(file)};
        private static void WriteChecksums(string root){var lines=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(x=>Path.GetFileName(x)!="checksums.sha256").OrderBy(x=>x).Select(x=>$"{Sha256File(x)}  {Path.GetRelativePath(root,x).Replace('\\','/')}");File.WriteAllLines(Path.Combine(root,"checksums.sha256"),lines,Encoding.UTF8);}
        private static bool ContainsSensitive(string text)=>text.IndexOf("BEGIN PRIVATE KEY",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("Authorization: Bearer",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("SILICONFLOW_API_KEY=",StringComparison.OrdinalIgnoreCase)>=0;
    }
}
