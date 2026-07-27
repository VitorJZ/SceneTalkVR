using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public static class PilotCollectionBundleExporter
    {
        [Serializable] private sealed class PilotRankingEvent
        {
            public string eventType;public string participantId;public string sessionId;public string conditionLabel;
            public int rank;public string preferredCondition;public string reason;public long monotonicElapsedMs;
        }
        public const string BundleSchemaVersion="1.2-pilot-editor-collection";
        public static bool Export(string root,PilotAssignment assignment,ExperimentV11ProtocolConfig protocol,
            QuestionnaireCatalog questionnaires,bool rankingSubmitted,out string bundle,out string error)
        {
            bundle="";error="";
            if(assignment==null||protocol==null||questionnaires==null){error="pilot_collection_bundle_context_missing";return false;}
            if(!rankingSubmitted||assignment.conditions==null||assignment.conditions.Any(x=>x.status!=PilotRunStatus.Completed)){error="pilot_collection_bundle_session_incomplete";return false;}
            if(assignment.flowMode!=ExperimentFlowMode.Pilot||assignment.runQualification!=ExperimentRunQualification.Collection||assignment.dataOrigin!="participant_collection"||!assignment.collectionEligible||assignment.developerTestAssignment||assignment.demoMode){error="pilot_collection_bundle_identity_invalid";return false;}
            var raw=Path.Combine(root,"raw");var source=Path.Combine(root,"bundle-source");bundle=Path.Combine(root,"bundle");
            if(Directory.Exists(source))Directory.Delete(source,true);if(Directory.Exists(bundle))Directory.Delete(bundle,true);
            foreach(var folder in new[]{"assignment","timing","study","goals","questionnaire","ranking","integrity"})Directory.CreateDirectory(Path.Combine(source,folder));
            File.WriteAllText(Path.Combine(source,"assignment","assignment.json"),JsonUtility.ToJson(assignment,true),Encoding.UTF8);CopyRaw(raw,source);
            var build=UnityEngine.Object.FindFirstObjectByType<ExperimentConditionManager>()?.ExperimentBuildInfo;
            var manifest=new SessionBundleManifest{bundleSchemaVersion=BundleSchemaVersion,dataOrigin="participant_collection",collectionEligible=true,developerTestAssignment=false,demoMode=false,synthetic=false,runtimeMode=ExperimentRuntimeMode.EditorCollectionPilot.ToString(),flowMode=ExperimentFlowMode.Pilot.ToString(),runQualification=ExperimentRunQualification.Collection.ToString(),sessionMode="pilot",participantId=assignment.participantId,sessionId=assignment.sessionId,gitCommit=build?.GitCommit??"",protocolVersion=protocol.ProtocolVersion,officialProtocolVersion=protocol.ProtocolVersion,protocolSnapshotId=assignment.protocolSnapshotId,resourceSnapshotId=assignment.resourceSnapshotId,taskCatalogVersion=assignment.taskCatalogVersion,questionnaireCatalogVersion=questionnaires.CatalogVersion,assignmentVersion=assignment.pilotAssignmentVersion,assignmentAlgorithmVersion=PilotAssignmentAllocator.Version,deploymentProfile="editor_collection",primaryAttemptPolicy=protocol.PrimaryAttemptPolicy,conditionToTaskMapping=assignment.conditions.Select(x=>x.embodimentConditionLabel+"="+x.task.taskId).ToArray(),conditionSelectionOrder=(assignment.participantSelectionOrder??Array.Empty<PilotEmbodimentCondition>()).Select(PilotProtocolValues.Label).ToArray(),conditionRunIds=assignment.conditions.Select(x=>x.latestPilotRunId??"").ToArray(),createdAtUtc=DateTime.UtcNow.ToString("o")};
            if(!SessionBundleExporter.Export(source,bundle,manifest,out error))return false;var audit=SessionDataIntegrityAuditor.Audit(bundle,assignment.participantId,assignment.sessionId);manifest.integrityStatus=audit.result.ToString().ToUpperInvariant();SessionBundleExporter.UpdateIntegrity(bundle,manifest,audit);return audit.result!=DataIntegritySeverity.Fail;
        }
        private static void CopyRaw(string raw,string source)
        {
            if(!Directory.Exists(raw))return;var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in Directory.GetFiles(raw,"*",SearchOption.TopDirectoryOnly).Where(x=>x.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".json",StringComparison.OrdinalIgnoreCase)))
            {var name=Path.GetFileName(file);if(name=="pilot_assignment.json")continue;var lower=name.ToLowerInvariant();var folder=lower.Contains("goal")?"goals":lower.Contains("questionnaire")?"questionnaire":lower.Contains("ranking")?"ranking":lower.Contains("timing")?"timing":"study";var suffix=1;var extension=Path.GetExtension(name);var target=folder+"-records-"+suffix.ToString("00")+extension;while(!used.Add(folder+"/"+target)){suffix++;target=folder+"-records-"+suffix.ToString("00")+extension;}File.Copy(file,Path.Combine(source,folder,target),true);if(lower.Contains("ranking")&&file.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase))WriteNormalizedRanking(file,Path.Combine(source,"ranking","pilot_ranking_events.jsonl"));}
            Ensure(source,"timing","timing-empty.jsonl","{\"eventType\":\"NoTimingEvents\"}");Ensure(source,"study","study-empty.jsonl","{\"eventType\":\"NoStudyEvents\"}");Ensure(source,"goals","goals-empty.jsonl","{\"eventType\":\"NoGoalEvents\"}");Ensure(source,"questionnaire","questionnaire-empty.jsonl","{\"eventType\":\"NoQuestionnaireEvents\"}");Ensure(source,"ranking","ranking-empty.jsonl","{\"eventType\":\"NoRankingEvents\"}");
        }
        private static void Ensure(string root,string folder,string name,string line){var path=Path.Combine(root,folder);if(Directory.GetFiles(path).Length==0)File.WriteAllText(Path.Combine(path,name),line+Environment.NewLine,Encoding.UTF8);}
        private static void WriteNormalizedRanking(string source,string target){var line=File.ReadLines(source).LastOrDefault(x=>!string.IsNullOrWhiteSpace(x));if(string.IsNullOrWhiteSpace(line))return;var value=JsonUtility.FromJson<PreferenceRankingResponse>(line);if(value?.rankings==null)return;var events=value.rankings.Select(x=>new PilotRankingEvent{participantId=value.participantId,sessionId=value.sessionId,eventType="PilotFinalRankingEntry",conditionLabel=x.embodimentCondition,rank=x.rank,preferredCondition=value.preferredEmbodimentCondition,reason=value.reason,monotonicElapsedMs=x.rank}).ToList();events.Add(new PilotRankingEvent{participantId=value.participantId,sessionId=value.sessionId,eventType="PilotFinalRankingSubmitted",preferredCondition=value.preferredEmbodimentCondition,reason=value.reason,monotonicElapsedMs=10});File.WriteAllLines(target,events.Select(JsonUtility.ToJson),Encoding.UTF8);}
    }
}
