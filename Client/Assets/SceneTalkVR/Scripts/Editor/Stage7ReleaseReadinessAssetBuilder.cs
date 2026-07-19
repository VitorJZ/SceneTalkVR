using System;
using System.Collections.Generic;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class Stage7ReleaseReadinessAssetBuilder
    {
        private const string Folder = "Assets/SceneTalkVR/ExperimentProtocol";
        private static readonly (string id,string question)[] Decisions =
        {
            ("condition_letter_mapping","Map a/b/c/d to NE/NR/SE/SR."),("formal_task_no_replacement","Confirm the formal task replacement policy."),
            ("formal_social_comfort","Confirm whether Social Comfort is in the formal questionnaire."),("pilot_feedback_style","Confirm the Pilot feedback style."),
            ("voice_only_spatial_audio","Confirm Voice Only spatial audio policy."),("pilot_sequence_mapping","Map pilot a/b/c to voice_only/floating_orb/humanoid_agent."),
            ("formal_max_turns","Confirm formal maximum turns."),("formal_max_duration","Confirm formal maximum duration."),
            ("pilot_max_turns","Confirm pilot maximum turns."),("pilot_max_duration","Confirm pilot maximum duration."),
            ("questionnaire_scale_anchors","Confirm questionnaire 1-7 anchors.")
        };

        [MenuItem("SceneTalkVR/Experiment/Stage 7/Create Readiness Assets", false, 83)]
        public static void CreateAssets()
        {
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>($"{Folder}/ExperimentV11Protocol.asset");
            if(protocol==null) throw new InvalidOperationException("ExperimentV11Protocol.asset is missing.");
            UpgradeProtocol(protocol);
            var voices=CreateIfMissing<ExperimentVoiceProfileCatalog>($"{Folder}/ExperimentVoiceProfileCatalog.asset");
            var deployments=CreateIfMissing<ExperimentDeploymentCatalog>($"{Folder}/ExperimentDeploymentCatalog.asset");
            deployments.EditorSet("1.1-stage7",new[]
            {
                new ExperimentDeploymentProfile{profileId=ExperimentDeploymentProfileId.DevelopmentEditor,requestTimeoutSeconds=30,sttProvider="unconfirmed",ttsProvider="unconfirmed",microphonePolicy="editor_default",networkRequired=false,approvedForCollection=false},
                new ExperimentDeploymentProfile{profileId=ExperimentDeploymentProfileId.PicoLab,requestTimeoutSeconds=30,sttProvider="unconfirmed",ttsProvider="unconfirmed",microphonePolicy="runtime_permission_required",networkRequired=true,approvedForCollection=false},
                new ExperimentDeploymentProfile{profileId=ExperimentDeploymentProfileId.PicoPortable,requestTimeoutSeconds=30,sttProvider="unconfirmed",ttsProvider="unconfirmed",microphonePolicy="runtime_permission_required",networkRequired=true,approvedForCollection=false},
                new ExperimentDeploymentProfile{profileId=ExperimentDeploymentProfileId.MockOffline,requestTimeoutSeconds=5,sttProvider="mock",ttsProvider="mock",microphonePolicy="disabled",networkRequired=false,approvedForCollection=false}
            });
            EditorUtility.SetDirty(deployments);
            foreach(var manager in UnityEngine.Object.FindObjectsByType<ExperimentConditionManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var managerObject=new SerializedObject(manager);managerObject.FindProperty("voiceProfileCatalog").objectReferenceValue=voices;managerObject.FindProperty("deploymentCatalog").objectReferenceValue=deployments;managerObject.FindProperty("deploymentProfile").enumValueIndex=(int)ExperimentDeploymentProfileId.DevelopmentEditor;managerObject.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(manager);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes(); AssetDatabase.Refresh();
            Debug.Log("Stage 7 readiness assets created. Research values and official resources remain intentionally unconfirmed.");
        }

        private static void UpgradeProtocol(ExperimentV11ProtocolConfig protocol)
        {
            var so=new SerializedObject(protocol);so.FindProperty("protocolVersion").stringValue="1.1.0-stage7";so.FindProperty("buildVersion").stringValue="stage7-20260719";
            var old=new Dictionary<string,ExperimentProtocolDecision>(StringComparer.OrdinalIgnoreCase);foreach(var item in protocol.RequiredDecisions)if(item!=null&&!string.IsNullOrWhiteSpace(item.decisionId))old[item.decisionId]=item;
            var list=so.FindProperty("requiredDecisions");list.arraySize=Decisions.Length;
            for(var i=0;i<Decisions.Length;i++)
            {
                var dst=list.GetArrayElementAtIndex(i);var id=Decisions[i].id;dst.FindPropertyRelative("decisionId").stringValue=id;dst.FindPropertyRelative("question").stringValue=Decisions[i].question;
                if(old.TryGetValue(id,out var source)){dst.FindPropertyRelative("status").enumValueIndex=(int)source.status;dst.FindPropertyRelative("confirmedValue").stringValue=source.confirmedValue??"";dst.FindPropertyRelative("confirmedBy").stringValue=source.confirmedBy??"";dst.FindPropertyRelative("confirmedAtUtc").stringValue=source.confirmedAtUtc??"";dst.FindPropertyRelative("evidenceReference").stringValue=source.evidenceReference??"";dst.FindPropertyRelative("notes").stringValue=source.notes??"";}
                else {dst.FindPropertyRelative("status").enumValueIndex=(int)ProtocolDecisionStatus.Unconfirmed;dst.FindPropertyRelative("confirmedValue").stringValue="";dst.FindPropertyRelative("confirmedBy").stringValue="";dst.FindPropertyRelative("confirmedAtUtc").stringValue="";dst.FindPropertyRelative("evidenceReference").stringValue="";dst.FindPropertyRelative("notes").stringValue="";}
            }
            var log=so.FindProperty("changeLog");if(log.arraySize==0){log.arraySize=1;var entry=log.GetArrayElementAtIndex(0);entry.FindPropertyRelative("changedAtUtc").stringValue="2026-07-19T00:00:00Z";entry.FindPropertyRelative("changedBy").stringValue="Codex implementation (no research decisions)";entry.FindPropertyRelative("previousProtocolVersion").stringValue="1.1.0-stage1";entry.FindPropertyRelative("newProtocolVersion").stringValue="1.1.0-stage7";entry.FindPropertyRelative("evidenceReference").stringValue="EXPERIMENT_V1_1_STAGE7_RELEASE_READINESS_REPORT.md";entry.FindPropertyRelative("summary").stringValue="Expanded decision provenance and readiness validation schema. All new research decisions remain Unconfirmed.";}
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(protocol);
        }
        private static T CreateIfMissing<T>(string path) where T:ScriptableObject {var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;}
    }
}
