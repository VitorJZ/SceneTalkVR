using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class PilotEmbodimentPresenter : MonoBehaviour, ISceneTalkSessionReset
    {
        public static PilotEmbodimentPresenter Active { get; private set; }

        private PilotPresentationProfile profile;
        private CorrectionAgentPresenter correctionAgentPresenter;

        public PilotPresentationProfile Profile => profile;
        public AudioSource AudioSource => ResolvePresenter(false)?.AudioSource;
        public bool HasVisualEntity => correctionAgentPresenter != null
            && correctionAgentPresenter.CurrentVisualMode != CorrectionAgentPresenter.VisualMode.AudioOnly
            && correctionAgentPresenter.TargetVisible;
        public string VisualEntityType => profile == null
            ? "none"
            : profile.embodimentCondition == PilotEmbodimentCondition.FloatingOrb
                ? "floating_orb"
                : profile.embodimentCondition == PilotEmbodimentCondition.HumanoidAgent
                    ? "humanoid_agent"
                    : "none";

        public bool Configure(
            PilotPresentationProfile value,
            PilotAudioSourcePolicy voiceOnlyPolicy,
            bool lockedPilot,
            out string error)
        {
            ResetSession();
            if (value == null)
            {
                error = "pilot_presentation_profile_missing";
                return false;
            }

            if (!HasExpectedVisualMode(value))
            {
                error = "pilot_presentation_profile_visual_mode_mismatch";
                return false;
            }

            if (lockedPilot
                && value.embodimentCondition == PilotEmbodimentCondition.VoiceOnly
                && voiceOnlyPolicy != PilotAudioSourcePolicy.NonSpatialHeadLocked)
            {
                error = "voice_only_audio_policy_mismatch";
                return false;
            }

            var presenter = ResolvePresenter(true);
            var appearanceId = ResolveAppearanceId(value.embodimentCondition);
            if (!presenter.SetAppearanceId(appearanceId))
            {
                error = "formal_assistant_embodiment_unavailable";
                return false;
            }

            if (!presenter.IsCurrentAppearanceConfigured)
            {
                error = value.embodimentCondition == PilotEmbodimentCondition.HumanoidAgent
                    ? "formal_humanoid_prefab_missing"
                    : "formal_assistant_appearance_missing";
                return false;
            }

            presenter.ConfigureAudioProfile(
                value.volume,
                value.spatialBlend,
                value.minDistance,
                value.maxDistance);
            profile = value;
            presenter.ShowImmediate();
            presenter.EndSpeaking();
            Active = this;
            error = string.Empty;
            return true;
        }

        public void BeginFeedback()
        {
            if (profile == null) return;
            var presenter = ResolvePresenter(false);
            if (presenter == null) return;
            presenter.ShowImmediate();
            presenter.BeginSpeaking();
        }

        public void EndFeedback()
        {
            ResolvePresenter(false)?.EndSpeaking();
        }

        public void ResetSession()
        {
            var presenter = ResolvePresenter(false);
            if (presenter != null)
            {
                presenter.EndSpeaking();
                var source = presenter.AudioSource;
                source.Stop();
                source.clip = null;
                presenter.HideImmediate();
            }

            if (Active == this) Active = null;
            profile = null;
        }

        private CorrectionAgentPresenter ResolvePresenter(bool createIfMissing)
        {
            if (correctionAgentPresenter == null)
                correctionAgentPresenter = GetComponent<CorrectionAgentPresenter>();
            if (correctionAgentPresenter == null && createIfMissing)
                correctionAgentPresenter = gameObject.AddComponent<CorrectionAgentPresenter>();
            return correctionAgentPresenter;
        }

        private static bool HasExpectedVisualMode(PilotPresentationProfile value)
        {
            return value.embodimentCondition switch
            {
                PilotEmbodimentCondition.VoiceOnly => value.visualMode == PilotVisualMode.None,
                PilotEmbodimentCondition.FloatingOrb => value.visualMode == PilotVisualMode.FloatingOrb,
                PilotEmbodimentCondition.HumanoidAgent => value.visualMode == PilotVisualMode.Humanoid,
                _ => false
            };
        }

        private static string ResolveAppearanceId(PilotEmbodimentCondition condition)
        {
            return condition switch
            {
                PilotEmbodimentCondition.VoiceOnly => ExperimentConditionManager.AudioOnlyAssistantEmbodiment,
                PilotEmbodimentCondition.FloatingOrb => ExperimentConditionManager.OrbAssistantEmbodiment,
                PilotEmbodimentCondition.HumanoidAgent => ExperimentConditionManager.HumanoidAssistantEmbodiment,
                _ => ExperimentConditionManager.NoAssistantEmbodiment
            };
        }
    }
}
