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
    public enum PilotEmbodimentCondition { VoiceOnly, FloatingOrb, HumanoidAgent }
    public enum PilotVisualMode { None, FloatingOrb, Humanoid }
    public enum PilotFeedbackStyleChoice { Undefined, Explicit, Recast }
    public enum PilotAudioSourcePolicy { Undefined, SpatialFixedSource, NonSpatialHeadLocked }
    public enum PilotRunStatus { Assigned, Preparing, Running, TaskCompleted, AwaitingPilotQuestionnaire, PilotQuestionnaireInProgress, PilotQuestionnaireSubmitted, Completed, TechnicalInvalid, Aborted, PilotQuestionnaireSkipped }

    public static class PilotProtocolValues
    {
        public static string Label(PilotEmbodimentCondition value) => value == PilotEmbodimentCondition.VoiceOnly ? "voice_only" : value == PilotEmbodimentCondition.FloatingOrb ? "floating_orb" : "humanoid_agent";
        public static string Label(PilotFeedbackStyleChoice value) => value == PilotFeedbackStyleChoice.Explicit ? "explicit" : value == PilotFeedbackStyleChoice.Recast ? "recast" : "undefined";
        public static string Label(PilotAudioSourcePolicy value) => value == PilotAudioSourcePolicy.SpatialFixedSource ? "spatial_fixed_source" : value == PilotAudioSourcePolicy.NonSpatialHeadLocked ? "non_spatial_head_locked" : "undefined";
        public static bool TryParseFeedbackStyle(string value, out PilotFeedbackStyleChoice result) { result = string.Equals(value?.Trim(), "explicit", StringComparison.OrdinalIgnoreCase) ? PilotFeedbackStyleChoice.Explicit : string.Equals(value?.Trim(), "recast", StringComparison.OrdinalIgnoreCase) ? PilotFeedbackStyleChoice.Recast : PilotFeedbackStyleChoice.Undefined; return result != PilotFeedbackStyleChoice.Undefined; }
        public static bool TryParseAudioPolicy(string value, out PilotAudioSourcePolicy result) { result = string.Equals(value?.Trim(), "spatial_fixed_source", StringComparison.OrdinalIgnoreCase) ? PilotAudioSourcePolicy.SpatialFixedSource : string.Equals(value?.Trim(), "non_spatial_head_locked", StringComparison.OrdinalIgnoreCase) ? PilotAudioSourcePolicy.NonSpatialHeadLocked : PilotAudioSourcePolicy.Undefined; return result != PilotAudioSourcePolicy.Undefined; }
    }

    [Serializable] public sealed class PilotSequenceDefinition { public string sequenceId; public PilotEmbodimentCondition[] conditions = Array.Empty<PilotEmbodimentCondition>(); public bool confirmed; }
    [Serializable] public sealed class PilotTaskAssignment { public string taskId; public string taskAssignmentId; }
    [Serializable] public sealed class PilotConditionAssignment { public int conditionPosition; public PilotEmbodimentCondition embodimentCondition; public string embodimentConditionLabel; public PilotTaskAssignment task = new PilotTaskAssignment(); public PilotRunStatus status = PilotRunStatus.Assigned; public string latestPilotRunId; public int runAttempt; public int participantSelectionPosition = -1; public string selectedAtUtc; }
    [Serializable] public sealed class PilotAssignment
    {
        public string pilotProtocolVersion; public string pilotAssignmentVersion; public string taskCatalogVersion;
        public string participantId; public string sessionId; public string sequenceId; public string assignmentSeed;
        public string createdAtUtc; public bool developerTestAssignment; public string dataOrigin; public bool collectionEligible; public PilotFeedbackStyleChoice feedbackStyle; public string feedbackStyleLabel;
        public ExperimentRuntimeMode runtimeMode; public string deploymentProfile; public bool demoMode; public string demoProtocolVersion;
        public ExperimentFlowMode flowMode; public ExperimentRunQualification runQualification; public string protocolSnapshotId; public string resourceSnapshotId;
        public PilotAudioSourcePolicy voiceOnlyAudioPolicy; public string voiceOnlyAudioPolicyLabel; public PilotEmbodimentCondition[] participantSelectionOrder = Array.Empty<PilotEmbodimentCondition>(); public PilotConditionAssignment[] conditions = Array.Empty<PilotConditionAssignment>();
    }
    [Serializable] public struct PilotRunContext { public string participantId; public string sessionId; public string pilotRunId; public PilotEmbodimentCondition embodimentCondition; public string taskId; public PilotFeedbackStyleChoice feedbackStyle; }

    [Serializable]
    public sealed class PilotPresentationProfile
    {
        public PilotEmbodimentCondition embodimentCondition; public PilotVisualMode visualMode; public string feedbackActor;
        public string voiceProfileKey; public PilotAudioSourcePolicy audioSourcePolicy; public Vector3 sourcePosition;
        [Range(0,1)] public float spatialBlend; public float minDistance = 3.2f; public float maxDistance = 8f;
        public float volume = 1f; public float speakingSpeed = 1f; public string subtitlePolicy = "feedback_only";
        public int appearanceDelayMs; public int disappearanceDelayMs; public string visualPrefabKey; public GameObject visualPrefab;
        public RuntimeAnimatorController animatorController; public string idleParameterOrState; public string speakingParameterOrState;
        public Vector3 spawnRotation; public Vector3 scale = Vector3.one; public bool audioSourceRequired = true;
        public bool mobileReady; public string assetVersion; public bool approvedForCollection; public string evidenceReference;
        public bool developerPlaceholder;
    }

    public sealed class PilotAssignmentAllocator
    {
        public const string Version="2.1-temporary-walk-in-only";
        public const string TemporaryTaskId="pilot_restaurant_walk_in";
        public bool TryCreateCollection(string participantId,string sessionId,ExperimentV11ProtocolConfig protocol,
            ExperimentTaskCatalog tasks,PilotPresentationCatalog presentations,string resourceSnapshotId,
            out PilotAssignment assignment,out string error)
        {
            assignment=null;error="";
            if(protocol==null){error="protocol_missing";return false;}if(!protocol.ValidateForFormalMode(out error))return false;
            if(!protocol.TryResolvePilotDecisions(out var style,out var audio,out error))return false;
            if(!protocol.TryResolvePilotSequences(out var sequences,out error))return false;
            var pilotTasks=tasks?.GetTasks(ExperimentTaskPhase.Pilot).ToArray()??Array.Empty<ExperimentTaskDefinition>();
            if(!ExperimentTaskCatalog.ValidatePilotTasks(pilotTasks,out error))return false;
            if(presentations==null){error="presentation_catalog_missing";return false;}if(!presentations.ValidateLocked(protocol,out error))return false;
            return TryCreateCollectionBalanced(participantId,sessionId,protocol.ProtocolVersion,tasks.CatalogVersion,
                sequences,pilotTasks.Select(x=>x.taskId).ToArray(),style,audio,protocol.ProtocolSnapshotId,
                resourceSnapshotId,out assignment,out error);
        }
        public bool TryCreateLocked(string participantId,string sessionId,ExperimentV11ProtocolConfig protocol,ExperimentTaskCatalog tasks,PilotPresentationCatalog presentations,out PilotAssignment assignment,out string error)
        {
            assignment=null; var issues=new List<string>();var style=PilotFeedbackStyleChoice.Undefined;var audio=PilotAudioSourcePolicy.Undefined;var decisionError="protocol_missing";var presentationError="presentation_catalog_missing";
            if(protocol==null || !protocol.TryResolvePilotDecisions(out style,out audio,out decisionError)) issues.Add(decisionError);
            if(presentations==null || !presentations.ValidateLocked(protocol,out presentationError)) issues.Add(presentationError);
            var sequences=Array.Empty<PilotSequenceDefinition>();
            if(protocol!=null && !protocol.TryResolvePilotSequences(out sequences,out var sequenceError)){issues.Add(sequenceError);issues.Add("pilot_sequences_unconfirmed");}
            if(sequences.Length!=3) issues.Add("pilot_sequences_unconfirmed");
            var pilotTasks=tasks?.GetTasks(ExperimentTaskPhase.Pilot).ToArray()??Array.Empty<ExperimentTaskDefinition>();
            if(!ExperimentTaskCatalog.ValidatePilotTasks(pilotTasks,out var taskError)) issues.Add(taskError);
            if(issues.Any(x=>!string.IsNullOrWhiteSpace(x))){error=string.Join("; ",issues);return false;}
            return TryCreateForTesting(participantId,sessionId,protocol.ProtocolVersion,tasks.CatalogVersion,sequences,pilotTasks.Select(x=>x.taskId).ToArray(),style,audio,false,out assignment,out error);
        }
        public bool TryCreateForTesting(string participantId,string sessionId,string protocolVersion,string taskCatalogVersion,PilotSequenceDefinition[] sequences,string[] taskIds,PilotFeedbackStyleChoice style,PilotAudioSourcePolicy audio,bool developer,out PilotAssignment assignment,out string error)
        {
            return TryCreate(participantId,sessionId,protocolVersion,taskCatalogVersion,sequences,taskIds,style,audio,
                ExperimentFlowMode.Synthetic,ExperimentRunQualification.Development,developer?"synthetic_dry_run":"participant_collection",developer,string.Empty,string.Empty,out assignment,out error);
        }
        public bool TryCreateRehearsal(string participantId,string sessionId,ExperimentV11RehearsalProtocol protocol,ExperimentTaskCatalog tasks,string resourceSnapshotId,out PilotAssignment assignment,out string error)
        {
            assignment=null;if(protocol==null){error="rehearsal_protocol_missing";return false;}if(!protocol.Validate(out error))return false;if(tasks==null){error="task_catalog_missing";return false;}
            var taskIds=tasks.GetTasks(ExperimentTaskPhase.Pilot).Select(x=>x.taskId).ToArray();
            return TryCreate(participantId,sessionId,protocol.ProtocolVersion,tasks.CatalogVersion,protocol.PilotSequences,taskIds,
                protocol.PilotFeedbackStyle,protocol.VoiceOnlyAudioPolicy,ExperimentFlowMode.Pilot,ExperimentRunQualification.Rehearsal,
                "rehearsal",false,protocol.ProtocolSnapshotId,resourceSnapshotId,out assignment,out error);
        }
        private bool TryCreate(string participantId,string sessionId,string protocolVersion,string taskCatalogVersion,PilotSequenceDefinition[] sequences,string[] taskIds,PilotFeedbackStyleChoice style,PilotAudioSourcePolicy audio,ExperimentFlowMode flowMode,ExperimentRunQualification qualification,string dataOrigin,bool developer,string protocolSnapshotId,string resourceSnapshotId,out PilotAssignment assignment,out string error)
        {
            assignment=null; if(string.IsNullOrWhiteSpace(participantId)){error="participant_missing";return false;}
            if(style==PilotFeedbackStyleChoice.Undefined){error="pilot_feedback_style_unconfirmed";return false;} if(audio==PilotAudioSourcePolicy.Undefined){error="voice_only_spatial_audio_unconfirmed";return false;}
            if(sequences==null||sequences.Length!=3||sequences.Any(x=>x.conditions==null||x.conditions.Length!=3||x.conditions.Distinct().Count()!=3)){error="pilot_sequences_invalid";return false;}
            if(taskIds==null||taskIds.Length!=3||taskIds.Distinct().Count()!=3||!taskIds.Contains(TemporaryTaskId)){error="pilot_tasks_invalid";return false;}
            var seed=Hash(participantId+"|"+protocolVersion+"|"+Version); var sequence=sequences[(int)(seed%3)];
            assignment=new PilotAssignment{pilotProtocolVersion=protocolVersion,pilotAssignmentVersion=Version,taskCatalogVersion=taskCatalogVersion,participantId=participantId,sessionId=sessionId,sequenceId=sequence.sequenceId,assignmentSeed=seed.ToString(CultureInfo.InvariantCulture),createdAtUtc=DateTime.UtcNow.ToString("o"),developerTestAssignment=developer,dataOrigin=dataOrigin,collectionEligible=qualification==ExperimentRunQualification.Collection,flowMode=flowMode,runQualification=qualification,protocolSnapshotId=protocolSnapshotId,resourceSnapshotId=resourceSnapshotId,feedbackStyle=style,feedbackStyleLabel=PilotProtocolValues.Label(style),voiceOnlyAudioPolicy=audio,voiceOnlyAudioPolicyLabel=PilotProtocolValues.Label(audio),conditions=new PilotConditionAssignment[3]};
            // Temporary experiment override: keep all three embodiment conditions on the first Pilot task.
            for(var i=0;i<3;i++) assignment.conditions[i]=new PilotConditionAssignment{conditionPosition=i,embodimentCondition=sequence.conditions[i],embodimentConditionLabel=PilotProtocolValues.Label(sequence.conditions[i]),task=new PilotTaskAssignment{taskId=TemporaryTaskId,taskAssignmentId=$"pta-{seed}-{i}"}};
            error="";return true;
        }
        private bool TryCreateCollectionBalanced(string participantId,string sessionId,string protocolVersion,
            string taskCatalogVersion,PilotSequenceDefinition[] sequences,string[] taskIds,
            PilotFeedbackStyleChoice style,PilotAudioSourcePolicy audio,string protocolSnapshotId,
            string resourceSnapshotId,out PilotAssignment assignment,out string error)
        {
            assignment=null;
            participantId=participantId?.Trim();sessionId=sessionId?.Trim();
            if(string.IsNullOrWhiteSpace(participantId)||string.IsNullOrWhiteSpace(sessionId)){error="participant_and_session_required";return false;}
            if(style!=PilotFeedbackStyleChoice.Explicit){error="pilot_collection_requires_explicit_feedback";return false;}
            if(audio!=PilotAudioSourcePolicy.NonSpatialHeadLocked){error="pilot_collection_requires_head_locked_voice_only";return false;}
            if(sequences==null||sequences.Length!=3||sequences.Any(x=>x.conditions==null||x.conditions.Length!=3||x.conditions.Distinct().Count()!=3)){error="pilot_sequences_invalid";return false;}
            var approvedTasks=new[]{"pilot_restaurant_walk_in","pilot_restaurant_ordering","pilot_restaurant_wrong_dish"};
            if(taskIds==null||taskIds.Length!=3||approvedTasks.Any(x=>!taskIds.Contains(x))){error="pilot_tasks_invalid";return false;}
            var seed=Hash(participantId+protocolVersion);
            var group=(int)(seed%3);
            var expectedOrders=new[]{
                new[]{PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent},
                new[]{PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly},
                new[]{PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb}};
            var sequence=sequences.FirstOrDefault(x=>x.conditions.SequenceEqual(expectedOrders[group]))
                ??new PilotSequenceDefinition{sequenceId=((char)('A'+group)).ToString(),conditions=expectedOrders[group],confirmed=true};
            assignment=new PilotAssignment{pilotProtocolVersion=protocolVersion,pilotAssignmentVersion=Version,
                taskCatalogVersion=taskCatalogVersion,participantId=participantId,sessionId=sessionId,
                sequenceId="Sequence "+((char)('A'+group)),assignmentSeed=seed.ToString(CultureInfo.InvariantCulture),
                createdAtUtc=DateTime.UtcNow.ToString("o"),developerTestAssignment=false,
                dataOrigin="participant_collection",collectionEligible=true,feedbackStyle=style,
                feedbackStyleLabel=PilotProtocolValues.Label(style),runtimeMode=ExperimentRuntimeMode.EditorCollectionPilot,
                deploymentProfile="editor_collection",
                demoMode=false,demoProtocolVersion="",flowMode=ExperimentFlowMode.Pilot,
                runQualification=ExperimentRunQualification.Collection,protocolSnapshotId=protocolSnapshotId,
                resourceSnapshotId=resourceSnapshotId,voiceOnlyAudioPolicy=audio,
                voiceOnlyAudioPolicyLabel=PilotProtocolValues.Label(audio),conditions=new PilotConditionAssignment[3]};
            // Temporary experiment override: keep all three embodiment conditions on the first Pilot task.
            for(var i=0;i<3;i++) assignment.conditions[i]=new PilotConditionAssignment{conditionPosition=i,
                embodimentCondition=sequence.conditions[i],embodimentConditionLabel=PilotProtocolValues.Label(sequence.conditions[i]),
                task=new PilotTaskAssignment{taskId=TemporaryTaskId,taskAssignmentId=$"pilot-{seed}-{group}-{i}"}};
            error="";return true;
        }
        public static void Save(PilotAssignment value,string path){SyncLabels(value);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(value,true));}
        public static PilotAssignment Load(string path){var value=File.Exists(path)?JsonUtility.FromJson<PilotAssignment>(File.ReadAllText(path)):null;SyncLabels(value);return value;}
        public static void SyncLabels(PilotAssignment value){if(value==null)return;value.feedbackStyleLabel=PilotProtocolValues.Label(value.feedbackStyle);value.voiceOnlyAudioPolicyLabel=PilotProtocolValues.Label(value.voiceOnlyAudioPolicy);foreach(var condition in value.conditions??Array.Empty<PilotConditionAssignment>())if(condition!=null)condition.embodimentConditionLabel=PilotProtocolValues.Label(condition.embodimentCondition);}
        public static bool IsCompatible(PilotAssignment a,string protocol,string tasks,out string error){if(a==null){error="assignment_missing";return false;}if(a.pilotProtocolVersion!=protocol){error="pilot_protocol_version_changed";return false;}if(a.taskCatalogVersion!=tasks){error="task_catalog_version_changed";return false;}if(a.pilotAssignmentVersion!=Version){error="pilot_assignment_version_changed";return false;}error="";return true;}
        private static ulong Hash(string value){using var sha=SHA256.Create();var b=sha.ComputeHash(Encoding.UTF8.GetBytes(value));return BitConverter.ToUInt64(b,0);}
    }
}
