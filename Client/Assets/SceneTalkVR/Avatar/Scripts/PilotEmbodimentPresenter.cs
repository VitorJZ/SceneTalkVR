using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class PilotEmbodimentPresenter : MonoBehaviour, ISceneTalkSessionReset
    {
        public static PilotEmbodimentPresenter Active { get; private set; }
        private PilotPresentationProfile profile;
        private AudioSource audioSource;
        private GameObject audioObject;
        private CorrectionAgentPresenter orb;
        private GameObject humanoid;
        private Animator humanoidAnimator;
        public PilotPresentationProfile Profile => profile;
        public AudioSource AudioSource => audioSource;
        public bool HasVisualEntity => orb != null && orb.TargetVisible || humanoid != null && humanoid.activeSelf;
        public string VisualEntityType => profile == null ? "none" : profile.visualMode == PilotVisualMode.FloatingOrb ? "floating_orb" : profile.visualMode == PilotVisualMode.Humanoid ? "humanoid_agent" : "none";

        public bool Configure(PilotPresentationProfile value, PilotAudioSourcePolicy voiceOnlyPolicy, bool lockedPilot, out string error)
        {
            ResetSession(); profile=value;
            if(value==null){error="pilot_presentation_profile_missing";return false;}
            if(value.embodimentCondition==PilotEmbodimentCondition.VoiceOnly && value.visualMode!=PilotVisualMode.None){error="voice_only_visual_forbidden";return false;}
            if(value.embodimentCondition==PilotEmbodimentCondition.HumanoidAgent && lockedPilot && (value.visualPrefab==null||value.developerPlaceholder)){error="humanoid_prefab_missing_or_placeholder";return false;}
            if(value.embodimentCondition==PilotEmbodimentCondition.FloatingOrb && lockedPilot && value.developerPlaceholder){error="orb_placeholder_forbidden";return false;}
            EnsureAudio(value.embodimentCondition==PilotEmbodimentCondition.VoiceOnly?voiceOnlyPolicy:value.audioSourcePolicy);
            if(value.visualMode==PilotVisualMode.FloatingOrb) { orb=gameObject.GetComponent<CorrectionAgentPresenter>()??gameObject.AddComponent<CorrectionAgentPresenter>(); orb.HideImmediate(); }
            else if(value.visualMode==PilotVisualMode.Humanoid && value.visualPrefab!=null)
            {
                humanoid=Instantiate(value.visualPrefab,transform,false);humanoid.name="Pilot Humanoid Feedback Agent";humanoid.transform.localPosition=value.sourcePosition;humanoid.transform.localRotation=Quaternion.Euler(value.spawnRotation);humanoid.transform.localScale=value.scale;humanoidAnimator=humanoid.GetComponentInChildren<Animator>();if(humanoidAnimator!=null&&value.animatorController!=null)humanoidAnimator.runtimeAnimatorController=value.animatorController;humanoid.SetActive(false);
            }
            Active=this; error="";return true;
        }

        public void BeginFeedback()
        {
            if(profile==null)return;
            if(orb!=null){orb.ShowImmediate();orb.BeginSpeaking();}
            if(humanoid!=null){humanoid.SetActive(true);SetHumanoidSpeaking(true);}
        }
        public void EndFeedback(){if(orb!=null){orb.EndSpeaking();orb.HideImmediate();}if(humanoid!=null){SetHumanoidSpeaking(false);humanoid.SetActive(false);}}
        public void ResetSession()
        {
            EndFeedback(); if(audioSource!=null){audioSource.Stop();audioSource.clip=null;}
            if(humanoid!=null){if(Application.isPlaying)Destroy(humanoid);else DestroyImmediate(humanoid);humanoid=null;}
            if(orb!=null)orb.HideImmediate(); if(Active==this)Active=null; profile=null;
        }
        private void EnsureAudio(PilotAudioSourcePolicy policy)
        {
            if(audioObject==null){audioObject=new GameObject("Pilot Feedback Audio Source");audioObject.transform.SetParent(transform,false);audioSource=audioObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;}
            var spatial=policy==PilotAudioSourcePolicy.SpatialFixedSource; audioSource.spatialBlend=spatial?1f:0f;audioSource.minDistance=profile.minDistance;audioSource.maxDistance=profile.maxDistance;audioSource.volume=profile.volume;audioSource.dopplerLevel=0;
            if(!spatial && Camera.main!=null){audioObject.transform.SetParent(Camera.main.transform,false);audioObject.transform.localPosition=Vector3.zero;}else{audioObject.transform.SetParent(transform,false);audioObject.transform.localPosition=profile.sourcePosition;}
        }
        private void SetHumanoidSpeaking(bool speaking)
        {
            if(humanoidAnimator==null||profile==null||string.IsNullOrWhiteSpace(profile.speakingParameterOrState))return;
            foreach(var parameter in humanoidAnimator.parameters) if(parameter.name==profile.speakingParameterOrState&&parameter.type==AnimatorControllerParameterType.Bool){humanoidAnimator.SetBool(parameter.name,speaking);return;}
            if(speaking)humanoidAnimator.Play(profile.speakingParameterOrState);
            else if(!string.IsNullOrWhiteSpace(profile.idleParameterOrState))humanoidAnimator.Play(profile.idleParameterOrState);
        }
    }
}
