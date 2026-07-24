using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class PilotPresentationCatalogBuilder
    {
        public const string AssetPath="Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset";
        [MenuItem("SceneTalkVR/Experiment/Build Stage 6 Pilot Presentation Catalog",false,44)]
        public static void Build()
        {
            var asset=AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>(AssetPath);
            if(asset==null){asset=ScriptableObject.CreateInstance<PilotPresentationCatalog>();AssetDatabase.CreateAsset(asset,AssetPath);}
            var humanoid=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/correction_assistant_woman.prefab");
            var animator=humanoid==null?null:humanoid.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController;
            const string voice="editor_collection_feedback_voice";
            asset.EditorSet("1.2-editor-collection",new[]{
                new PilotPresentationProfile{embodimentCondition=PilotEmbodimentCondition.VoiceOnly,visualMode=PilotVisualMode.None,feedbackActor="voice_only_feedback_agent",voiceProfileKey=voice,audioSourcePolicy=PilotAudioSourcePolicy.NonSpatialHeadLocked,sourcePosition=Vector3.zero,spatialBlend=0,minDistance=.2f,maxDistance=4,volume=1,speakingSpeed=1,subtitlePolicy="feedback_only",visualPrefabKey="none",audioSourceRequired=true,mobileReady=true,assetVersion="editor-collection-1",approvedForCollection=true,evidenceReference="formal-editor-collection-directive-v1"},
                new PilotPresentationProfile{embodimentCondition=PilotEmbodimentCondition.FloatingOrb,visualMode=PilotVisualMode.FloatingOrb,feedbackActor="floating_orb_feedback_agent",voiceProfileKey=voice,audioSourcePolicy=PilotAudioSourcePolicy.SpatialFixedSource,sourcePosition=new Vector3(.9f,1.45f,1.8f),spatialBlend=1,minDistance=.2f,maxDistance=4,volume=1,speakingSpeed=1,subtitlePolicy="feedback_only",visualPrefabKey="generated_orb_v1",appearanceDelayMs=0,disappearanceDelayMs=0,audioSourceRequired=true,mobileReady=true,assetVersion="editor-collection-1",approvedForCollection=true,evidenceReference="formal-editor-collection-directive-v1"},
                new PilotPresentationProfile{embodimentCondition=PilotEmbodimentCondition.HumanoidAgent,visualMode=PilotVisualMode.Humanoid,feedbackActor="humanoid_feedback_agent",voiceProfileKey=voice,audioSourcePolicy=PilotAudioSourcePolicy.SpatialFixedSource,sourcePosition=new Vector3(.9f,0,1.8f),spatialBlend=1,minDistance=.2f,maxDistance=4,volume=1,speakingSpeed=1,subtitlePolicy="feedback_only",visualPrefabKey="correction_assistant_woman",visualPrefab=humanoid,animatorController=animator,idleParameterOrState="Idle",speakingParameterOrState="Talking",spawnRotation=new Vector3(0,180,0),scale=Vector3.one,audioSourceRequired=true,mobileReady=true,assetVersion="editor-collection-1",approvedForCollection=true,evidenceReference="formal-editor-collection-directive-v1"}
            });EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();Debug.Log("[Pilot] Presentation Catalog built with the shared formal correction assistant.");
        }
        [MenuItem("SceneTalkVR/Experiment/Bind Stage 6 Pilot Presentation Catalog",false,45)]
        public static void Bind()
        {
            var asset=AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>(AssetPath);var manager=Object.FindFirstObjectByType<ExperimentConditionManager>();
            if(asset==null||manager==null)throw new System.InvalidOperationException("Pilot Catalog or manager missing");var so=new SerializedObject(manager);so.FindProperty("pilotPresentationCatalog").objectReferenceValue=asset;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);
        }
    }
}
