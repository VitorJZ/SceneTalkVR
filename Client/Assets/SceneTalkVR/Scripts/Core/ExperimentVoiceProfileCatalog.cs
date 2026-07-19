using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum VoiceSubtitlePolicy { Undefined, None, FeedbackOnly, AllSpeech }

    [Serializable]
    public sealed class ExperimentVoiceProfile
    {
        public string voiceProfileKey;
        public string provider;
        public string voiceId;
        public string language = "en-US";
        public float speakingSpeed = 1f;
        [Range(0f, 1f)] public float volume = 1f;
        public float pitch = 1f;
        public int sampleRate = 24000;
        public VoiceSubtitlePolicy subtitlePolicy;
        public bool approvedForCollection;
        public string approvedBy;
        public string evidenceReference;
        public string assetVersion;
    }

    [CreateAssetMenu(fileName = "ExperimentVoiceProfileCatalog", menuName = "SceneTalkVR/Experiment Voice Profile Catalog")]
    public sealed class ExperimentVoiceProfileCatalog : ScriptableObject
    {
        [SerializeField] private string catalogVersion = "1.1-stage7";
        [SerializeField] private string formalExplicitFeedbackProfileKey;
        [SerializeField] private string formalRecastFeedbackProfileKey;
        [SerializeField] private string pilotSharedFeedbackProfileKey;
        [SerializeField] private ExperimentVoiceProfile[] profiles = Array.Empty<ExperimentVoiceProfile>();

        public string CatalogVersion => catalogVersion?.Trim() ?? string.Empty;
        public string FormalExplicitFeedbackProfileKey => formalExplicitFeedbackProfileKey?.Trim() ?? string.Empty;
        public string FormalRecastFeedbackProfileKey => formalRecastFeedbackProfileKey?.Trim() ?? string.Empty;
        public string PilotSharedFeedbackProfileKey => pilotSharedFeedbackProfileKey?.Trim() ?? string.Empty;
        public IReadOnlyList<ExperimentVoiceProfile> Profiles => profiles;

        public bool TryGet(string key, out ExperimentVoiceProfile profile)
        {
            profile = profiles?.FirstOrDefault(x => x != null && string.Equals(x.voiceProfileKey?.Trim(), key?.Trim(), StringComparison.OrdinalIgnoreCase));
            return profile != null;
        }

        public bool ValidateForLockedCollection(IEnumerable<string> dialogueProfileKeys, out string error)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(CatalogVersion)) issues.Add("voice_catalog_version_missing");
            if (profiles == null || profiles.Length == 0) issues.Add("approved_voice_profiles_missing");
            if (profiles != null && profiles.Where(x => x != null).GroupBy(x => x.voiceProfileKey, StringComparer.OrdinalIgnoreCase).Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1)) issues.Add("voice_profile_key_missing_or_duplicate");
            var required = new List<string> { FormalExplicitFeedbackProfileKey, FormalRecastFeedbackProfileKey, PilotSharedFeedbackProfileKey };
            if (dialogueProfileKeys != null) required.AddRange(dialogueProfileKeys);
            foreach (var key in required.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(key)) { issues.Add("required_voice_profile_key_unconfirmed"); continue; }
                if (!TryGet(key, out var item)) { issues.Add($"voice_profile_missing:{key}"); continue; }
                if (!item.approvedForCollection || string.IsNullOrWhiteSpace(item.provider) || string.IsNullOrWhiteSpace(item.voiceId)
                    || string.IsNullOrWhiteSpace(item.evidenceReference) || string.IsNullOrWhiteSpace(item.assetVersion)
                    || item.sampleRate <= 0 || item.speakingSpeed <= 0 || item.subtitlePolicy == VoiceSubtitlePolicy.Undefined)
                    issues.Add($"voice_profile_not_collection_ready:{key}");
            }
            error = string.Join("; ", issues.Distinct()); return issues.Count == 0;
        }

#if UNITY_EDITOR
        public void EditorSet(string version, string explicitKey, string recastKey, string pilotKey, ExperimentVoiceProfile[] values)
        { catalogVersion = version; formalExplicitFeedbackProfileKey = explicitKey; formalRecastFeedbackProfileKey = recastKey; pilotSharedFeedbackProfileKey = pilotKey; profiles = values ?? Array.Empty<ExperimentVoiceProfile>(); }
#endif
    }
}
