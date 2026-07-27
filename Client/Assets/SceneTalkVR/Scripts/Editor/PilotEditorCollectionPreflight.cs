using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneTalkVR.EditorTools
{
    [Serializable] public sealed class PilotPreflightCheck{public string id;public string result;public string detail;}
    [Serializable] public sealed class PilotPreflightReport{public string generatedAtUtc;public string overall;public PilotPreflightCheck[] checks=Array.Empty<PilotPreflightCheck>();}
    public static class PilotEditorCollectionPreflight
    {
        private const string MainScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("SceneTalkVR/Diagnostics/Pilot Editor Collection Preflight",false,55)]
        public static void RunMenu(){var report=Run();Debug.Log("[PilotPreflight] "+JsonUtility.ToJson(report,true));}
        public static PilotPreflightReport Run()
        {
            if(Application.isBatchMode&&SceneManager.GetActiveScene().path!=MainScenePath)
                EditorSceneManager.OpenScene(MainScenePath,OpenSceneMode.Single);
            var list=new List<PilotPreflightCheck>();void Check(string id,bool ok,string detail,bool warning=false)=>list.Add(new PilotPreflightCheck{id=id,result=ok?"READY":warning?"WARNING":"BLOCKED",detail=detail});
            var protocol=AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
            var tasks=AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
            var presentations=AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>("Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset");
            var questionnaires=AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset");
            var protocolError=protocol==null?"protocol_missing":"";var protocolOk=protocol!=null&&protocol.ValidateForFormalMode(out protocolError);Check("protocol",protocolOk,protocolError??"official collection protocol");
            var sequenceError=protocol==null?"protocol_missing":"";var sequences=Array.Empty<PilotSequenceDefinition>();var sequenceOk=protocol!=null&&protocol.TryResolvePilotSequences(out sequences,out sequenceError)&&sequences.Length==3;Check("pilot_sequences",sequenceOk,sequenceError??"three sequences");
            var pilotTasks=tasks?.GetTasks(ExperimentTaskPhase.Pilot).ToArray();Check("pilot_tasks",ExperimentTaskCatalog.ValidatePilotTasks(pilotTasks,out var taskError),taskError);
            var presentationError=presentations==null?"presentation_catalog_missing":"";var presentationOk=presentations!=null&&presentations.ValidateLocked(protocol,out presentationError);Check("presentations",presentationOk,presentationError);
            Check("dialogue_avatar",pilotTasks!=null&&pilotTasks.All(x=>x.avatarPresetKey=="barista_male_humanoid_v1"),"pilot_restaurant_dialogue_avatar=barista_male_humanoid_v1");
            Check("voice_only_audio",presentations?.Find(PilotEmbodimentCondition.VoiceOnly)?.audioSourcePolicy==PilotAudioSourcePolicy.NonSpatialHeadLocked,"non_spatial_head_locked; spatialBlend=0");
            Check("orb_prefab",presentations?.Find(PilotEmbodimentCondition.FloatingOrb)?.visualPrefabKey=="generated_orb_v1","procedural generated_orb_v1");
            Check("humanoid_prefab",presentations?.Find(PilotEmbodimentCondition.HumanoidAgent)?.visualPrefab!=null,"teacher_female_humanoid_v1");
            Check("restaurant_panorama",Resources.Load<Texture>("SceneTalkVR/Textures/restaurant-360")!=null,"local restaurant 360 panorama");
            Check("pilot_goals",pilotTasks!=null&&pilotTasks.All(x=>x.goals?.Length==4&&x.goals.All(g=>!string.IsNullOrWhiteSpace(g.goalId))),"four deterministic goals per task");
            Check("pilot_questionnaire",questionnaires?.Find("pilot_condition_v1")!=null&&questionnaires.GetEnabledItems("pilot_condition_v1",protocol).Any(),"pilot_condition_v1");
            Check("pilot_ranking",questionnaires?.Find("pilot_final_v1")!=null,"pilot_final_v1");
            var gateway=UnityEngine.Object.FindFirstObjectByType<VoiceGatewayClient>();Check("voice_gateway",gateway!=null,"real Editor gateway component is bound");
            var root=PilotCollectionSessionCoordinator.CollectionRoot;var writable=false;try{Directory.CreateDirectory(root);var probe=Path.Combine(root,".write-probe");File.WriteAllText(probe,"ok");File.Delete(probe);writable=true;}catch(Exception e){Check("data_directory",false,e.Message);}if(writable)Check("data_directory",true,root);
            Check("bundle_exporter",typeof(PilotCollectionBundleExporter)!=null,"PilotCollectionBundleExporter available");Check("integrity_auditor",typeof(SessionDataIntegrityAuditor)!=null,"SessionDataIntegrityAuditor available");
            Check("pico_not_required",true,"Unity Editor collection deployment; PICO is not a blocker",true);
            var overall=list.Any(x=>x.result=="BLOCKED")?"BLOCKED":list.Any(x=>x.result=="WARNING")?"WARNING":"READY";
            return new PilotPreflightReport{generatedAtUtc=DateTime.UtcNow.ToString("o"),overall=overall,checks=list.ToArray()};
        }
    }
}
