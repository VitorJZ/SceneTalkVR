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
    public enum PilotRunStatus { Assigned, Preparing, Running, TaskCompleted, AwaitingPilotQuestionnaire, PilotQuestionnaireInProgress, PilotQuestionnaireSubmitted, Completed, TechnicalInvalid, Aborted }

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
    [Serializable] public sealed class PilotConditionAssignment { public int conditionPosition; public PilotEmbodimentCondition embodimentCondition; public string embodimentConditionLabel; public PilotTaskAssignment task = new PilotTaskAssignment(); public PilotRunStatus status = PilotRunStatus.Assigned; public string latestPilotRunId; public int runAttempt; }
    [Serializable] public sealed class PilotAssignment
    {
        public string pilotProtocolVersion; public string pilotAssignmentVersion; public string taskCatalogVersion;
        public string participantId; public string sessionId; public string sequenceId; public string assignmentSeed;
        public string createdAtUtc; public bool developerTestAssignment; public PilotFeedbackStyleChoice feedbackStyle; public string feedbackStyleLabel;
        public PilotAudioSourcePolicy voiceOnlyAudioPolicy; public string voiceOnlyAudioPolicyLabel; public PilotConditionAssignment[] conditions = Array.Empty<PilotConditionAssignment>();
    }
    [Serializable] public struct PilotRunContext { public string participantId; public string sessionId; public string pilotRunId; public PilotEmbodimentCondition embodimentCondition; public string taskId; public PilotFeedbackStyleChoice feedbackStyle; }

    [Serializable]
    public sealed class PilotPresentationProfile
    {
        public PilotEmbodimentCondition embodimentCondition; public PilotVisualMode visualMode; public string feedbackActor;
        public string voiceProfileKey; public PilotAudioSourcePolicy audioSourcePolicy; public Vector3 sourcePosition;
        [Range(0,1)] public float spatialBlend; public float minDistance = .2f; public float maxDistance = 4f;
        public float volume = 1f; public float speakingSpeed = 1f; public string subtitlePolicy = "feedback_only";
        public int appearanceDelayMs; public int disappearanceDelayMs; public string visualPrefabKey; public GameObject visualPrefab;
        public RuntimeAnimatorController animatorController; public string idleParameterOrState; public string speakingParameterOrState;
        public Vector3 spawnRotation; public Vector3 scale = Vector3.one; public bool audioSourceRequired = true;
        public bool mobileReady; public string assetVersion; public bool approvedForCollection; public string evidenceReference;
        public bool developerPlaceholder;
    }

    public sealed class PilotAssignmentAllocator
    {
        public const string Version="1.0";
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
            assignment=null; if(string.IsNullOrWhiteSpace(participantId)){error="participant_missing";return false;}
            if(style==PilotFeedbackStyleChoice.Undefined){error="pilot_feedback_style_unconfirmed";return false;} if(audio==PilotAudioSourcePolicy.Undefined){error="voice_only_spatial_audio_unconfirmed";return false;}
            if(sequences==null||sequences.Length!=3||sequences.Any(x=>x.conditions==null||x.conditions.Length!=3||x.conditions.Distinct().Count()!=3)){error="pilot_sequences_invalid";return false;}
            if(taskIds==null||taskIds.Length!=3||taskIds.Distinct().Count()!=3){error="pilot_tasks_invalid";return false;}
            var seed=Hash(participantId+"|"+protocolVersion+"|"+Version); var sequence=sequences[(int)(seed%3)]; var offset=(int)((seed/3)%3);
            assignment=new PilotAssignment{pilotProtocolVersion=protocolVersion,pilotAssignmentVersion=Version,taskCatalogVersion=taskCatalogVersion,participantId=participantId,sessionId=sessionId,sequenceId=sequence.sequenceId,assignmentSeed=seed.ToString(CultureInfo.InvariantCulture),createdAtUtc=DateTime.UtcNow.ToString("o"),developerTestAssignment=developer,feedbackStyle=style,feedbackStyleLabel=PilotProtocolValues.Label(style),voiceOnlyAudioPolicy=audio,voiceOnlyAudioPolicyLabel=PilotProtocolValues.Label(audio),conditions=new PilotConditionAssignment[3]};
            for(var i=0;i<3;i++) assignment.conditions[i]=new PilotConditionAssignment{conditionPosition=i,embodimentCondition=sequence.conditions[i],embodimentConditionLabel=PilotProtocolValues.Label(sequence.conditions[i]),task=new PilotTaskAssignment{taskId=taskIds[(i+offset)%3],taskAssignmentId=$"pta-{seed}-{i}"}};
            error="";return true;
        }
        public static void Save(PilotAssignment value,string path){SyncLabels(value);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(value,true));}
        public static PilotAssignment Load(string path){var value=File.Exists(path)?JsonUtility.FromJson<PilotAssignment>(File.ReadAllText(path)):null;SyncLabels(value);return value;}
        public static void SyncLabels(PilotAssignment value){if(value==null)return;value.feedbackStyleLabel=PilotProtocolValues.Label(value.feedbackStyle);value.voiceOnlyAudioPolicyLabel=PilotProtocolValues.Label(value.voiceOnlyAudioPolicy);foreach(var condition in value.conditions??Array.Empty<PilotConditionAssignment>())if(condition!=null)condition.embodimentConditionLabel=PilotProtocolValues.Label(condition.embodimentCondition);}
        public static bool IsCompatible(PilotAssignment a,string protocol,string tasks,out string error){if(a==null){error="assignment_missing";return false;}if(a.pilotProtocolVersion!=protocol){error="pilot_protocol_version_changed";return false;}if(a.taskCatalogVersion!=tasks){error="task_catalog_version_changed";return false;}if(a.pilotAssignmentVersion!=Version){error="pilot_assignment_version_changed";return false;}error="";return true;}
        private static ulong Hash(string value){using var sha=SHA256.Create();var b=sha.ComputeHash(Encoding.UTF8.GetBytes(value));return BitConverter.ToUInt64(b,0);}
    }
}
