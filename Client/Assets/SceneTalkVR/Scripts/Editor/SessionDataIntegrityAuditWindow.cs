using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class SessionDataIntegrityAuditWindow : EditorWindow
    {
        private string folder = ""; private string participantId = ""; private string sessionId = ""; private string outputPath = "";
        [MenuItem("SceneTalkVR/Diagnostics/Session Data Integrity Audit", false, 55)]
        private static void Open() => GetWindow<SessionDataIntegrityAuditWindow>("Session Integrity");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Read-only audit. Source Assignment/JSONL/JSON files are never edited.", MessageType.Info);
            folder=EditorGUILayout.TextField("Session Folder",folder);participantId=EditorGUILayout.TextField("Participant",participantId);sessionId=EditorGUILayout.TextField("Session",sessionId);outputPath=EditorGUILayout.TextField("Report JSON",outputPath);
            if(GUILayout.Button("Audit")){var report=SessionDataIntegrityAuditor.Audit(folder,participantId,sessionId);if(!string.IsNullOrWhiteSpace(outputPath))SessionDataIntegrityAuditor.WriteReport(report,outputPath);Debug.Log(JsonUtility.ToJson(report,true));}
        }
    }
}
