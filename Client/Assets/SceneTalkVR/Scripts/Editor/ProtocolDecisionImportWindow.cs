using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class ProtocolDecisionImportWindow : EditorWindow
    {
        private const string ProtocolPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset";
        private string jsonPath = "";
        private Vector2 scroll;
        private ProtocolDecisionImportPreview preview;
        private ProtocolDecisionIntakeDocument document;
        private string sourceJson;

        [MenuItem("SceneTalkVR/Experiment/Protocol Decision Import", false, 84)]
        private static void Open() => GetWindow<ProtocolDecisionImportWindow>("Protocol Decision Import");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Preview-only by default. The protocol asset changes only after an explicit confirmation; invalid input never writes partially.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                jsonPath = EditorGUILayout.TextField("Intake JSON", jsonPath);
                if (GUILayout.Button("Browse", GUILayout.Width(70))) jsonPath = EditorUtility.OpenFilePanel("Protocol decision intake", Application.dataPath, "json");
            }
            if (GUILayout.Button("Load, Validate and Preview")) LoadPreview();
            using (new EditorGUI.DisabledScope(preview == null || !preview.valid))
                if (GUILayout.Button("CONFIRM: Apply All Decisions Transactionally")) ConfirmAndApply();
            if (preview != null)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(JsonUtility.ToJson(preview, true), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        public static ProtocolDecisionImportPreview BuildPreview(string json, ExperimentV11ProtocolConfig protocol)
        {
            var result = new ProtocolDecisionImportPreview { sourceHash = Sha256(json), targetProtocolVersion = protocol?.ProtocolVersion ?? string.Empty };
            ProtocolDecisionIntakeDocument intake;
            try { intake = JsonUtility.FromJson<ProtocolDecisionIntakeDocument>(json); }
            catch (Exception ex) { result.errors = new[] { "json_invalid:" + ex.Message }; return result; }
            result.valid = ProtocolDecisionIntakeValidator.Validate(intake, true, out var errors);
            result.errors = errors;
            result.proposedProtocolVersion = NextVersion(result.targetProtocolVersion);
            if (protocol != null && intake?.decisions != null)
            {
                var changes = new List<ProtocolDecisionImportChange>();
                foreach (var proposed in intake.decisions)
                {
                    var old = protocol.RequiredDecisions.FirstOrDefault(x => x != null && string.Equals(x.decisionId, proposed.decisionId, StringComparison.OrdinalIgnoreCase));
                    changes.Add(new ProtocolDecisionImportChange { decisionId=proposed.decisionId,previousStatus=old?.status.ToString()??"Missing",previousValue=old?.confirmedValue??"",proposedStatus=proposed.approvalStatus,proposedValue=proposed.proposedValue??"" });
                }
                result.changes = changes.ToArray();
            }
            return result;
        }

        public static bool ApplyTransaction(ExperimentV11ProtocolConfig protocol, ProtocolDecisionIntakeDocument intake, string sourceJson, string operatorConfirmation, out string error)
        {
            if (protocol == null) { error = "protocol_missing"; return false; }
            if (operatorConfirmation != "APPLY_APPROVED_PROTOCOL_DECISIONS") { error = "explicit_operator_confirmation_required"; return false; }
            if (!ProtocolDecisionIntakeValidator.Validate(intake, true, out var errors)) { error = string.Join(";", errors); return false; }
            var before = EditorJsonUtility.ToJson(protocol, true);
            var beforeHash = Sha256(before);
            var backupFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "SceneTalkVR", "ProtocolBackups");
            Directory.CreateDirectory(backupFolder);
            var backupPath = Path.Combine(backupFolder, $"ExperimentV11Protocol_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{beforeHash.Substring(0,12)}.json");
            File.WriteAllText(backupPath, before, Encoding.UTF8);
            try
            {
                var serialized = new SerializedObject(protocol);
                var decisions = serialized.FindProperty("requiredDecisions");
                foreach (var item in intake.decisions)
                {
                    var index = FindDecision(decisions, item.decisionId);
                    if (index < 0) throw new InvalidOperationException("decision_missing_in_protocol:" + item.decisionId);
                    var target = decisions.GetArrayElementAtIndex(index);
                    target.FindPropertyRelative("status").enumValueIndex = (int)ProtocolDecisionStatus.Confirmed;
                    target.FindPropertyRelative("confirmedValue").stringValue = item.proposedValue.Trim();
                    target.FindPropertyRelative("confirmedBy").stringValue = item.confirmedBy.Trim();
                    target.FindPropertyRelative("confirmedAtUtc").stringValue = item.confirmedAtUtc.Trim();
                    target.FindPropertyRelative("evidenceReference").stringValue = item.evidenceReference.Trim();
                    target.FindPropertyRelative("notes").stringValue = item.notes?.Trim() ?? string.Empty;
                }
                var oldVersion = protocol.ProtocolVersion;
                var newVersion = NextVersion(oldVersion);
                serialized.FindProperty("protocolVersion").stringValue = newVersion;
                var log = serialized.FindProperty("changeLog"); var logIndex = log.arraySize; log.arraySize++;
                var entry = log.GetArrayElementAtIndex(logIndex);
                entry.FindPropertyRelative("changedAtUtc").stringValue = DateTime.UtcNow.ToString("o");
                entry.FindPropertyRelative("changedBy").stringValue = string.Join(",", intake.decisions.Select(x => x.confirmedBy).Distinct());
                entry.FindPropertyRelative("previousProtocolVersion").stringValue = oldVersion;
                entry.FindPropertyRelative("newProtocolVersion").stringValue = newVersion;
                entry.FindPropertyRelative("evidenceReference").stringValue = "intake-sha256:" + Sha256(sourceJson);
                entry.FindPropertyRelative("summary").stringValue = "Imported all 11 approved Stage 8 protocol decisions transactionally. Backup SHA-256: " + beforeHash;
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(protocol); AssetDatabase.SaveAssets();
                if (!protocol.ValidateForFormalMode(out var validationError)) throw new InvalidOperationException("post_write_validation_failed:" + validationError);
                error = string.Empty; return true;
            }
            catch (Exception ex)
            {
                EditorJsonUtility.FromJsonOverwrite(before, protocol); EditorUtility.SetDirty(protocol); AssetDatabase.SaveAssets();
                error = ex.Message; return false;
            }
        }

        private void LoadPreview()
        {
            sourceJson = File.Exists(jsonPath) ? File.ReadAllText(jsonPath, Encoding.UTF8) : string.Empty;
            document = string.IsNullOrWhiteSpace(sourceJson) ? null : JsonUtility.FromJson<ProtocolDecisionIntakeDocument>(sourceJson);
            preview = BuildPreview(sourceJson, AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(ProtocolPath));
        }
        private void ConfirmAndApply()
        {
            if (!EditorUtility.DisplayDialog("Apply approved protocol decisions?", "This writes all 11 decisions, appends the change log, increases protocolVersion, and invalidates old Assignments. Continue?", "Apply All", "Cancel")) return;
            if (!ApplyTransaction(AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(ProtocolPath), document, sourceJson, "APPLY_APPROVED_PROTOCOL_DECISIONS", out var error)) EditorUtility.DisplayDialog("Import failed — protocol restored", error, "OK");
            else { EditorUtility.DisplayDialog("Import complete", "Protocol updated. Run Preflight before collection.", "OK"); SceneTalkPreflightMenu.RunPreflightCheck(); }
            LoadPreview();
        }
        private static int FindDecision(SerializedProperty decisions, string id) { for(var i=0;i<decisions.arraySize;i++) if(decisions.GetArrayElementAtIndex(i).FindPropertyRelative("decisionId").stringValue==id)return i;return -1; }
        private static string NextVersion(string current) => (current ?? "1.1") + ".decision." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        public static string Sha256(string value) { using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value??string.Empty))).Replace("-","").ToLowerInvariant(); }
    }
}
