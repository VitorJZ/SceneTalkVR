using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [CreateAssetMenu(fileName="PilotPresentationCatalog", menuName="SceneTalkVR/Pilot Presentation Catalog")]
    public sealed class PilotPresentationCatalog : ScriptableObject
    {
        [SerializeField] private string catalogVersion = "1.1-stage6.1";
        [SerializeField] private PilotPresentationProfile[] profiles = Array.Empty<PilotPresentationProfile>();
        public string CatalogVersion => catalogVersion;
        public IReadOnlyList<PilotPresentationProfile> Profiles => profiles;
        public PilotPresentationProfile Find(PilotEmbodimentCondition condition) => profiles?.FirstOrDefault(x => x != null && x.embodimentCondition == condition);

        public bool ValidateLocked(ExperimentV11ProtocolConfig protocol, out string error)
        {
            var issues = new List<string>();
            if (profiles == null || profiles.Length != 3 || profiles.Any(x => x == null) || profiles.Select(x=>x.embodimentCondition).Distinct().Count()!=3) issues.Add("three_unique_embodiments_required");
            var audio=PilotAudioSourcePolicy.Undefined;var decisionError="protocol_missing";
            if (protocol == null || !protocol.TryResolvePilotDecisions(out _, out audio, out decisionError)) issues.Add(decisionError);
            var voice = profiles?.Where(x=>x!=null).Select(x=>x.voiceProfileKey).Distinct().Count() ?? 0; if (voice != 1) issues.Add("pilot_voice_profile_mismatch");
            var voiceOnly=Find(PilotEmbodimentCondition.VoiceOnly); if (voiceOnly==null || voiceOnly.visualMode!=PilotVisualMode.None) issues.Add("voice_only_must_be_explicit_no_visual");
            if (voiceOnly!=null && audio!=PilotAudioSourcePolicy.Undefined && voiceOnly.audioSourcePolicy!=PilotAudioSourcePolicy.Undefined && voiceOnly.audioSourcePolicy!=audio) issues.Add("voice_only_audio_policy_mismatch");
            var orb=Find(PilotEmbodimentCondition.FloatingOrb); if (orb==null || orb.visualMode!=PilotVisualMode.FloatingOrb || orb.developerPlaceholder) issues.Add("orb_profile_missing_or_placeholder");
            var human=Find(PilotEmbodimentCondition.HumanoidAgent); if (human==null || human.visualMode!=PilotVisualMode.Humanoid || human.visualPrefab==null || human.developerPlaceholder) issues.Add("humanoid_prefab_missing_or_placeholder");
            error=string.Join("; ",issues.Where(x=>!string.IsNullOrWhiteSpace(x))); return issues.Count==0;
        }
#if UNITY_EDITOR
        public void EditorSet(string version, PilotPresentationProfile[] values){catalogVersion=version;profiles=values;}
#endif
    }
}
