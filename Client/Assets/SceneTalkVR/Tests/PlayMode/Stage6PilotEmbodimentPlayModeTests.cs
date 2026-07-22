using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage6PilotEmbodimentPlayModeTests
    {
        [UnityTest] public IEnumerator SampleScene_HasBoundPilotCatalogAndWorkflow()
        {
            if(SceneManager.GetActiveScene().name!="SampleScene")SceneManager.LoadScene("SampleScene"); yield return null;
            var managerType=Type.GetType("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            var workflowType=Type.GetType("SceneTalkVR.Core.PilotWorkflowCoordinator, Assembly-CSharp");
            var presenterType=Type.GetType("SceneTalkVR.AvatarSystem.PilotEmbodimentPresenter, Assembly-CSharp");
            Assert.That(managerType,Is.Not.Null);Assert.That(workflowType,Is.Not.Null);Assert.That(presenterType,Is.Not.Null);
            var managers=Resources.FindObjectsOfTypeAll(managerType);Assert.That(managers.Length,Is.EqualTo(1));var manager=(Component)managers[0];
            var catalog=managerType.GetProperty("PilotPresentationCatalog").GetValue(manager);Assert.That(catalog,Is.Not.Null);
            var profiles=(System.Collections.ICollection)catalog.GetType().GetProperty("Profiles").GetValue(catalog);Assert.That(profiles.Count,Is.EqualTo(3));
            Assert.That(manager.GetComponent(workflowType),Is.Not.Null); Assert.That(manager.GetComponent(presenterType),Is.Not.Null);
        }

        [UnityTest] public IEnumerator VoiceOnly_DoesNotCreateVisibleAgent()
        {
            var presenterType=Type.GetType("SceneTalkVR.AvatarSystem.PilotEmbodimentPresenter, Assembly-CSharp");
            var profileType=Type.GetType("SceneTalkVR.Core.PilotPresentationProfile, Assembly-CSharp");
            var embodimentType=Type.GetType("SceneTalkVR.Core.PilotEmbodimentCondition, Assembly-CSharp");
            var visualType=Type.GetType("SceneTalkVR.Core.PilotVisualMode, Assembly-CSharp");
            var audioPolicyType=Type.GetType("SceneTalkVR.Core.PilotAudioSourcePolicy, Assembly-CSharp");
            var go=new GameObject("stage6-playmode-voice");var presenter=go.AddComponent(presenterType);var profile=Activator.CreateInstance(profileType);
            profileType.GetField("embodimentCondition").SetValue(profile,Enum.Parse(embodimentType,"VoiceOnly"));profileType.GetField("visualMode").SetValue(profile,Enum.Parse(visualType,"None"));profileType.GetField("feedbackActor").SetValue(profile,"voice_only_feedback_agent");profileType.GetField("voiceProfileKey").SetValue(profile,"shared");profileType.GetField("volume").SetValue(profile,1f);profileType.GetField("speakingSpeed").SetValue(profile,1f);
            var args=new[]{profile,Enum.Parse(audioPolicyType,"NonSpatialHeadLocked"),(object)false,null};var ok=(bool)presenterType.GetMethod("Configure").Invoke(presenter,args);Assert.That(ok,Is.True,args[3] as string);presenterType.GetMethod("BeginFeedback").Invoke(presenter,null);yield return null;Assert.That((bool)presenterType.GetProperty("HasVisualEntity").GetValue(presenter),Is.False);UnityEngine.Object.Destroy(go);yield return null;
        }
    }
}
