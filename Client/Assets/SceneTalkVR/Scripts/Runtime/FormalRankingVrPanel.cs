using System;
using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FormalRankingVrPanel : MonoBehaviour
    {
        private GameObject panel;
        private GameObject completionPanel;
        private TMP_Text validation;
        private TMP_InputField reasonInput;
        private readonly Dictionary<FormalConditionCode, int> ranks = new Dictionary<FormalConditionCode, int>();
        private readonly Dictionary<string, Button> rankButtons = new Dictionary<string, Button>();
        private readonly Dictionary<FormalConditionCode, Button> preferredButtons = new Dictionary<FormalConditionCode, Button>();
        private FormalConditionCode? preferredCondition;
        private bool submitted;
        private string activeSessionId;

        private void Update()
        {
            var coordinator = EditorCollectionSessionCoordinator.Active;
            if (coordinator == null || !coordinator.IsArmed)
            {
                SetActive(panel, false); SetActive(completionPanel, false); return;
            }
            if (!string.Equals(activeSessionId, coordinator.SessionId, StringComparison.Ordinal))
            { activeSessionId = coordinator.SessionId; ResetResponse(); }
            if (coordinator.FinalRankingVisible) { EnsureBuilt(); SetActive(panel, true); SetActive(completionPanel, false); panel.transform.SetAsLastSibling(); }
            else if (coordinator.ExperimentCompleted) { EnsureBuilt(); SetActive(panel, false); SetActive(completionPanel, true); completionPanel.transform.SetAsLastSibling(); }
            else { SetActive(panel, false); SetActive(completionPanel, false); }
        }

        private void EnsureBuilt()
        {
            if (panel != null) return;
            var canvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal));
            if (canvas == null) return;
            panel = Node(canvas.transform, "FormalFinalRankingPanel");
            var image = panel.AddComponent<Image>(); image.color = new Color(.035f, .05f, .08f, .98f);
            var rect = panel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(860, 540);
            Label(panel.transform, "Title", "Final Ranking / 最终排序", new Vector2(0, 224), new Vector2(760, 44), 28);
            Label(panel.transform, "Instruction", "Assign each feedback mode a unique rank from 1 (most preferred) to 4.", new Vector2(0, 182), new Vector2(760, 34), 18);
            var codes = new[] { FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR };
            for (var i = 0; i < codes.Length; i++)
            {
                var y = 120 - i * 68;
                Label(panel.transform, codes[i] + "Label", Friendly(codes[i]), new Vector2(-210, y), new Vector2(430, 44), 19, TextAnchor.MiddleLeft);
                ranks[codes[i]] = 0;
                for (var rank = 1; rank <= 4; rank++)
                {
                    var capturedCode = codes[i]; var capturedRank = rank;
                    var button = Button(panel.transform, codes[i] + "Rank" + rank, rank.ToString(), new Vector2(90 + rank * 52, y),
                        () => SelectRank(capturedCode, capturedRank), new Vector2(46, 42));
                    rankButtons[codes[i] + ":" + rank] = button;
                }
                var preferredCode = codes[i];
                preferredButtons[codes[i]] = Button(panel.transform, codes[i] + "Preferred", "Preferred", new Vector2(360, y),
                    () => SelectPreferred(preferredCode), new Vector2(118, 42));
            }
            reasonInput = Input(panel.transform, "RankingReason", "Why do you prefer your top-ranked mode?", new Vector2(0, -166), new Vector2(700, 64));
            validation = Label(panel.transform, "Validation", string.Empty, new Vector2(0, -218), new Vector2(700, 30), 17);
            Button(panel.transform, "RankingSubmitButton", "Submit Ranking", new Vector2(0, -258), Submit);

            completionPanel = Node(canvas.transform, "FormalExperimentCompletionPanel");
            var completionImage = completionPanel.AddComponent<Image>(); completionImage.color = new Color(.035f, .05f, .08f, .98f);
            completionPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 300);
            Label(completionPanel.transform, "Title", "Experiment Completed", new Vector2(0, 66), new Vector2(620, 54), 32);
            Label(completionPanel.transform, "Message", "Thank you. Please contact the experimenter.\nYour session is ready for bundle export and integrity audit.", new Vector2(0, -28), new Vector2(620, 110), 21);
            completionPanel.SetActive(false);
        }

        public void Submit()
        {
            if (submitted) { validation.text = "This ranking has already been submitted."; return; }
            var values = ranks.ToDictionary(x => x.Key, x => x.Value);
            if (values.Values.Any(x => x < 1 || x > 4) || values.Values.Distinct().Count() != 4)
            { validation.text = "Please assign each rank from 1 to 4 exactly once."; return; }
            if (!preferredCondition.HasValue)
            { validation.text = "Please select the overall preferred condition."; return; }
            if (string.IsNullOrWhiteSpace(reasonInput.text))
            { validation.text = "Please provide a short reason for your ranking."; return; }
            var entries = values.Select(x => new PreferenceRankEntry { conditionCode = x.Key.ToString(), rank = x.Value })
                .OrderBy(x => x.rank).ToArray();
            var response = new PreferenceRankingResponse
            {
                rankings = entries,
                preferredConditionCode = preferredCondition.Value.ToString(),
                reason = reasonInput.text.Trim()
            };
            if (!EditorCollectionSessionCoordinator.Active.SubmitFinalRanking(response, out var error))
            { validation.text = error; return; }
            submitted = true;
            validation.text = string.Empty;
        }

        private void SelectRank(FormalConditionCode code, int rank)
        {
            ranks[code] = rank;
            foreach (var pair in rankButtons)
            {
                var selected = pair.Key == code + ":" + rank;
                pair.Value.GetComponent<Image>().color = selected ? new Color(.12f, .66f, .36f, 1f) : new Color(.12f, .38f, .62f, 1f);
            }
            validation.text = string.Empty;
        }

        private void SelectPreferred(FormalConditionCode code)
        {
            preferredCondition = code;
            foreach (var pair in preferredButtons)
                pair.Value.GetComponent<Image>().color = pair.Key == code
                    ? new Color(.12f, .66f, .36f, 1f) : new Color(.12f, .38f, .62f, 1f);
            validation.text = string.Empty;
        }

        private void ResetResponse()
        {
            submitted=false;preferredCondition=null;
            foreach(var code in new[]{FormalConditionCode.NE,FormalConditionCode.NR,FormalConditionCode.SE,FormalConditionCode.SR})ranks[code]=0;
            foreach(var button in rankButtons.Values)if(button!=null)button.GetComponent<Image>().color=new Color(.12f,.38f,.62f,1f);
            foreach(var button in preferredButtons.Values)if(button!=null)button.GetComponent<Image>().color=new Color(.12f,.38f,.62f,1f);
            if(reasonInput!=null)reasonInput.text=string.Empty;if(validation!=null)validation.text=string.Empty;
        }

        private static string Friendly(FormalConditionCode code) => code switch
        {
            FormalConditionCode.NE => "Avatar — Direct Correction (NE)",
            FormalConditionCode.NR => "Avatar — Reformulation (NR)",
            FormalConditionCode.SE => "Assistant — Direct Correction (SE)",
            _ => "Assistant — Reformulation (SR)"
        };
        private static GameObject Node(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, int font, TextAnchor anchor = TextAnchor.MiddleCenter)
        { var go = Node(parent, name); var text = go.AddComponent<TextMeshProUGUI>(); text.text = value; text.color = Color.white; text.fontSize = font; text.alignment = ToTmpAlignment(anchor); text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.rectTransform.anchoredPosition = pos; text.rectTransform.sizeDelta = size; return text; }
        private static TMP_InputField Input(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = Node(parent, name); go.AddComponent<Image>().color = new Color(.12f, .16f, .22f, 1f); var input = go.AddComponent<TMP_InputField>();
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size;
            var text = Label(go.transform, "Text", string.Empty, Vector2.zero, size - new Vector2(20, 10), 17, TextAnchor.MiddleLeft);
            var hint = Label(go.transform, "Placeholder", placeholder, Vector2.zero, size - new Vector2(20, 10), 17, TextAnchor.MiddleLeft); hint.color = new Color(.65f, .7f, .75f, 1f);
            input.textViewport = rect; input.textComponent = text; input.placeholder = hint; return input;
        }
        private static Button Button(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction action, Vector2? size = null)
        { var actual = size ?? new Vector2(220, 44); var go = Node(parent, name); go.AddComponent<Image>().color = new Color(.12f, .52f, .38f, 1f); var button = go.AddComponent<Button>(); button.onClick.AddListener(action); go.GetComponent<RectTransform>().anchoredPosition = pos; go.GetComponent<RectTransform>().sizeDelta = actual; var text = Label(go.transform, "Label", label, Vector2.zero, actual, 19); text.raycastTarget = false; return button; }
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch { TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter => TextAlignmentOptions.Top, TextAnchor.UpperRight => TextAlignmentOptions.TopRight, TextAnchor.MiddleLeft => TextAlignmentOptions.Left, TextAnchor.MiddleRight => TextAlignmentOptions.Right, TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft, TextAnchor.LowerCenter => TextAlignmentOptions.Bottom, TextAnchor.LowerRight => TextAlignmentOptions.BottomRight, _ => TextAlignmentOptions.Center };
        private static void SetActive(GameObject value, bool active) { if (value != null && value.activeSelf != active) value.SetActive(active); }
    }
}
