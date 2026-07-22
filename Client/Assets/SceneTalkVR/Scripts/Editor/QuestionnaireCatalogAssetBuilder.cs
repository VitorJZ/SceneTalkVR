using System;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class QuestionnaireCatalogAssetBuilder
    {
        public const string AssetPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset";

        [MenuItem("SceneTalkVR/Experiment/Build v1.1 Questionnaire Catalog", false, 42)]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>(AssetPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<QuestionnaireCatalog>(); AssetDatabase.CreateAsset(catalog, AssetPath); }
            catalog.EditorSet("1.1-stage5.1", new[] { FormalCondition(), PilotCondition(), FormalFinal(), PilotFinal(), Interview() });
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Experiment] Questionnaire Catalog frozen at " + AssetPath);
        }

        [MenuItem("SceneTalkVR/Experiment/Bind v1.1 Questionnaire Catalog to SampleScene", false, 43)]
        public static void BindToLoadedScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>(AssetPath);
            var manager = UnityEngine.Object.FindFirstObjectByType<ExperimentConditionManager>();
            if (catalog == null || manager == null) throw new InvalidOperationException("Questionnaire Catalog or ExperimentConditionManager is missing.");
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("questionnaireCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            Debug.Log("[Experiment] Questionnaire Catalog bound to " + manager.gameObject.scene.path);
        }

        private static QuestionnaireDefinition FormalCondition() => Def("formal_condition_v1", QuestionnaireAudience.FormalCondition,
            Section("role_clarity", "Role Clarity", "角色清晰度",
                L("formal_rc_01", "I could clearly understand which agent was giving me feedback.", "我能清楚理解是哪个 Agent 在给我反馈。"),
                L("formal_rc_02", "I could easily distinguish which content continued the conversation and which provided language feedback.", "我能很容易分清哪些内容是在继续交流，哪些内容是在提供语言反馈。")),
            Section("conversation_continuity", "Conversation Continuity", "对话连续性",
                L("formal_cc_01", "After a correction appeared, I could still continue the conversation smoothly.", "纠正出现后，我仍然能顺畅地继续对话。"),
                L("formal_cc_02", "After a correction appeared, I needed time to return to the conversation.", "纠正出现后，我需要花时间重新回到对话中。", reverse: true),
                L("formal_cc_03", "After feedback appeared, I still felt that I was in the current conversational situation.", "反馈出现后，我仍然感觉自己处在当前的对话情境中。")),
            Section("interest_enjoyment", "Interest / Enjoyment", "兴趣与愉悦",
                L("formal_ie_01", "While doing this VR English speaking practice, I thought about how much I enjoyed it.", "在进行这次 VR 英语口语练习时，我会想到自己有多享受这个过程。"),
                L("formal_ie_02", "This VR English speaking practice did not hold my attention.", "这次 VR 英语口语练习没有吸引我的注意力。", reverse: true),
                L("formal_ie_03", "I thought this VR English speaking practice was very interesting.", "我认为这次 VR 英语口语练习非常有趣。"),
                L("formal_ie_04", "I enjoyed this VR English speaking practice very much.", "我非常享受这次 VR 英语口语练习。"),
                L("formal_ie_05", "Participating in this VR English speaking practice was fun.", "参与这次 VR 英语口语练习很有趣。")),
            Section("pressure_tension", "Pressure / Tension", "压力与紧张",
                L("formal_pt_01", "I did not feel nervous at all while doing this VR English speaking practice.", "在进行这次 VR 英语口语练习时，我一点也不紧张。", reverse: true),
                L("formal_pt_02", "I felt very tense while doing this VR English speaking practice.", "在进行这次 VR 英语口语练习时，我感到非常紧张。")),
            Section("learning_support", "Learning Support", "学习支持",
                L("formal_ls_01", "I understood what this feedback wanted me to improve.", "我能理解这条反馈想要我改进什么。"),
                L("formal_ls_02", "I could relate this feedback to what I had just said.", "我能把这条反馈和自己刚才的表达联系起来。"),
                L("formal_ls_03", "I found the feedback useful for improving my English expression.", "我认为这条反馈有助于改进我的英语表达。"),
                L("formal_ls_04", "This feedback was practical for improving my future English conversations.", "这条反馈对改进我之后的英语对话具有实用价值。")),
            SocialSection("formal_social"));

        private static QuestionnaireDefinition PilotCondition() => Def("pilot_condition_v1", QuestionnaireAudience.PilotCondition,
            Section("role_clarity", "Role Clarity", "角色清晰度", L("pilot_rc_01", "I clearly understood the role of this agent.", "我能清楚理解这个角色的作用。")),
            Section("social_comfort", "Social Comfort", "社交舒适度", L("pilot_sc_01", "Receiving feedback from this agent did not make me uncomfortable.", "从这个角色那里收到反馈不会让我不安。")),
            Section("acceptance", "Overall Acceptance", "总体接受度", L("pilot_accept_01", "Overall, I would accept using this agent form for corrective feedback.", "总体而言，我能接受使用这种 Agent 形态提供纠错反馈。")));

        private static QuestionnaireDefinition FormalFinal() => Def("formal_final_v1", QuestionnaireAudience.FormalFinal,
            Section("ranking", "Final Preference Ranking", "最终偏好排序",
                Ranking("formal_rank_01", "Rank NE, NR, SE and SR from most preferred to least preferred.", "请将 NE、NR、SE、SR 从最希望使用到最不希望使用排序。", "NE", "NR", "SE", "SR"),
                Text("formal_rank_reason", QuestionnaireItemType.LongText, "Please explain your ranking.", "请说明排序理由。")));

        private static QuestionnaireDefinition PilotFinal() => Def("pilot_final_v1", QuestionnaireAudience.PilotFinal,
            Section("ranking", "Embodiment Ranking", "Agent 形态排序",
                Ranking("pilot_rank_01", "Rank the three agent forms from most preferred to least preferred.", "请将三种 Agent 形态从最希望使用到最不希望使用排序。", "voice_only", "floating_orb", "humanoid_agent"),
                Text("pilot_rank_reason", QuestionnaireItemType.LongText, "Which form would you most like to use long term, and why?", "你最希望长期使用哪一种形态？请说明理由。")));

        private static QuestionnaireDefinition Interview() => Def("formal_interview_v1", QuestionnaireAudience.Interview,
            Section("interview", "Structured Interview", "结构化访谈",
                Text("interview_preference_reason", QuestionnaireItemType.ExperimenterNote, "Why did you prefer this feedback condition?", "你为什么更偏好这种反馈条件？"),
                Text("interview_role_reason", QuestionnaireItemType.ExperimenterNote, "How did you understand the agents' conversational and teaching roles?", "你如何理解 Agent 的交流者与教师角色？")));

        private static QuestionnaireSection SocialSection(string prefix)
        {
            var section = Section("social_comfort", "Social Comfort", "社交舒适度",
                L(prefix + "_01", "I don’t worry about making mistakes during this VR speaking practice.", "在这次 VR 口语练习中，我不担心犯英语错误。"),
                L(prefix + "_02", "I am afraid that the feedback provider is ready to correct every mistake I make.", "我担心这个反馈者会随时纠正我犯的每一个错误。", reverse: true),
                L(prefix + "_03", "I felt nervous when I had to speak during this session.", "在这次会话中我必须发言时感到紧张。", reverse: true));
            foreach (var item in section.items) { item.enabled = false; item.protocolDecisionDependency = "formal_social_comfort"; }
            return section;
        }

        private static QuestionnaireDefinition Def(string id, QuestionnaireAudience audience, params QuestionnaireSection[] sections)
        {
            foreach (var section in sections) foreach (var item in section.items) item.questionnaireId = id;
            return new QuestionnaireDefinition { questionnaireId = id, questionnaireVersion = "1.0", audience = audience, sections = sections };
        }
        private static QuestionnaireSection Section(string id, string en, string zh, params QuestionnaireItem[] items)
        { for (var i = 0; i < items.Length; i++) { items[i].questionnaireId = string.Empty; items[i].sectionId = id; items[i].displayOrder = i + 1; } return new QuestionnaireSection { sectionId = id, displayNameEnglish = en, displayNameChinese = zh, items = items }; }
        private static QuestionnaireItem L(string id, string en, string zh, bool reverse = false) => new QuestionnaireItem
        { itemId = id, itemVersion = "1.0", promptEnglish = en, promptChinese = zh, itemType = QuestionnaireItemType.Likert, required = true, reverseScored = reverse, scaleMin = 1, scaleMax = 7, enabled = true };
        private static QuestionnaireItem Text(string id, QuestionnaireItemType type, string en, string zh) => new QuestionnaireItem
        { itemId = id, itemVersion = "1.0", promptEnglish = en, promptChinese = zh, itemType = type, required = true, enabled = true };
        private static QuestionnaireItem Ranking(string id, string en, string zh, params string[] choices) => new QuestionnaireItem
        { itemId = id, itemVersion = "1.0", promptEnglish = en, promptChinese = zh, itemType = QuestionnaireItemType.Ranking, required = true, enabled = true, choiceValues = choices };
    }
}
