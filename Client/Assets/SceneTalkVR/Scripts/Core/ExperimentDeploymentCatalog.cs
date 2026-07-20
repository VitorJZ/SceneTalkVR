using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum ExperimentDeploymentProfileId { DevelopmentEditor, PicoLab, PicoPortable, MockOffline, EditorDemo, RehearsalEditor }

    [Serializable]
    public sealed class ExperimentDeploymentProfile
    {
        public ExperimentDeploymentProfileId profileId;
        public string voiceGatewayBaseUrl;
        public int requestTimeoutSeconds = 30;
        public string sttProvider;
        public string ttsProvider;
        public string microphonePolicy;
        public bool networkRequired;
        public bool approvedForCollection;
        public bool approvedForEditorDemo;
        public bool loopbackAllowedForEditorDemo;
        public bool approvedForRehearsal;
        public bool loopbackAllowedForRehearsal;
        public bool collectionAllowed;
        public string evidenceReference;

        public string EndpointHost
        {
            get { return Uri.TryCreate(voiceGatewayBaseUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty; }
        }
    }

    [CreateAssetMenu(fileName = "ExperimentDeploymentCatalog", menuName = "SceneTalkVR/Experiment Deployment Catalog")]
    public sealed class ExperimentDeploymentCatalog : ScriptableObject
    {
        [SerializeField] private string catalogVersion = "1.1-stage7";
        [SerializeField] private ExperimentDeploymentProfile[] profiles = Array.Empty<ExperimentDeploymentProfile>();
        public string CatalogVersion => catalogVersion?.Trim() ?? string.Empty;
        public IReadOnlyList<ExperimentDeploymentProfile> Profiles => profiles;
        public bool TryGet(ExperimentDeploymentProfileId id, out ExperimentDeploymentProfile profile) { profile = profiles?.FirstOrDefault(x => x != null && x.profileId == id); return profile != null; }

        public bool ValidateForCollection(ExperimentDeploymentProfileId id, out string error)
        {
            var issues = new List<string>();
            if (!TryGet(id, out var profile)) { error = "deployment_profile_missing"; return false; }
            if (!profile.approvedForCollection || string.IsNullOrWhiteSpace(profile.evidenceReference)) issues.Add("deployment_profile_unapproved");
            if (profile.requestTimeoutSeconds <= 0) issues.Add("deployment_timeout_invalid");
            if (profile.networkRequired && string.IsNullOrWhiteSpace(profile.voiceGatewayBaseUrl)) issues.Add("deployment_endpoint_empty");
            if ((id == ExperimentDeploymentProfileId.PicoLab || id == ExperimentDeploymentProfileId.PicoPortable) && IsLoopback(profile.voiceGatewayBaseUrl)) issues.Add("pico_endpoint_loopback_forbidden");
            if (ContainsMock(profile.sttProvider) || ContainsMock(profile.ttsProvider)) issues.Add("mock_provider_forbidden_for_collection");
            if (ContainsSecretMaterial(profile.voiceGatewayBaseUrl)) issues.Add("deployment_endpoint_contains_secret_material");
            error = string.Join("; ", issues); return issues.Count == 0;
        }

        public bool ValidateForRehearsal(out string error)
        {
            if (!TryGet(ExperimentDeploymentProfileId.RehearsalEditor, out var profile)) { error = "rehearsal_deployment_missing"; return false; }
            var issues = new List<string>();
            if (!profile.approvedForRehearsal || profile.approvedForCollection || profile.collectionAllowed) issues.Add("rehearsal_deployment_qualification_invalid");
            if (!profile.loopbackAllowedForRehearsal || !IsLoopback(profile.voiceGatewayBaseUrl)) issues.Add("rehearsal_editor_loopback_invalid");
            if (profile.requestTimeoutSeconds <= 0 || string.IsNullOrWhiteSpace(profile.sttProvider) || string.IsNullOrWhiteSpace(profile.ttsProvider)) issues.Add("rehearsal_live_pipeline_invalid");
            if (ContainsMock(profile.sttProvider) || ContainsMock(profile.ttsProvider)) issues.Add("rehearsal_mock_provider_forbidden");
            error = string.Join("; ", issues); return issues.Count == 0;
        }

        public static bool IsLoopback(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1" || uri.Host == "::1";
        }
        public static bool ContainsSecretMaterial(string value) => !string.IsNullOrWhiteSpace(value) && (value.IndexOf("api_key", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("token=", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("apikey", StringComparison.OrdinalIgnoreCase) >= 0);
        private static bool ContainsMock(string value) => string.IsNullOrWhiteSpace(value) || value.IndexOf("mock", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("fake", StringComparison.OrdinalIgnoreCase) >= 0;
#if UNITY_EDITOR
        public void EditorSet(string version, ExperimentDeploymentProfile[] values) { catalogVersion = version; profiles = values ?? Array.Empty<ExperimentDeploymentProfile>(); }
#endif
    }
}
