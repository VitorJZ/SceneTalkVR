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
    public sealed class QuestionnaireVrPanel : MonoBehaviour
    {
        [SerializeField] private QuestionnaireRuntimeController controller;
        [SerializeField] private Canvas worldCanvas;
        private GameObject panel;
        private Transform content;
        private TMP_Text progressText;
        private TMP_Text validationText;
        private Button previousButton;
        private Button nextButton;
        private Button submitButton;
        private int page;
        private bool submitConfirmationArmed;
        private readonly List<GameObject> pageObjects = new List<GameObject>();
        private readonly Dictionary<string, Button> likertButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, int> itemPages = new Dictionary<string, int>();
        private string activeLinkageKey;
        private bool submissionInProgress;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<QuestionnaireRuntimeController>();
            if (controller != null) controller.QuestionnaireChanged += OnQuestionnaireChanged;
        }
        private void OnDestroy() { if (controller != null) controller.QuestionnaireChanged -= OnQuestionnaireChanged; }

        public void Configure(QuestionnaireRuntimeController target, Canvas canvas)
        {
            if (controller != null) controller.QuestionnaireChanged -= OnQuestionnaireChanged;
            controller = target; worldCanvas = canvas;
            if (controller != null) controller.QuestionnaireChanged += OnQuestionnaireChanged;
        }

        private void OnQuestionnaireChanged(QuestionnaireSession session)
        {
            if (session == null) { if (panel != null) panel.SetActive(false); return; }
            EnsureBuilt();
            if (!string.Equals(activeLinkageKey, session.questionnaireLinkageKey, StringComparison.Ordinal))
            {
                activeLinkageKey = session.questionnaireLinkageKey;
                RebuildPages();
            }
            ShowPage(Mathf.Clamp(session.currentPage, 0, Mathf.Max(0, pageObjects.Count - 1)), false);
            RefreshLikertSelection(session);
            panel.SetActive(session.completionStatus != QuestionnaireCompletionStatus.Submitted);
            if (panel.activeSelf)
            {
                panel.transform.SetAsLastSibling();
                FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include)?.BringExitButtonToFront();
            }
        }

        private void EnsureBuilt()
        {
            if (panel != null) return;
            if (worldCanvas == null) worldCanvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal));
            if (worldCanvas == null) return;
            panel = Node(worldCanvas.transform, "QuestionnairePanel");
            var image = panel.AddComponent<Image>(); image.color = new Color(0.035f, 0.05f, 0.08f, 0.97f);
            var group = panel.AddComponent<CanvasGroup>(); group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
            var rect = panel.GetComponent<RectTransform>(); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(920f, 560f);
            progressText = Label(panel.transform, "Progress", "", new Vector2(0, 238), new Vector2(820, 40), 22);
            validationText = Label(panel.transform, "RequiredStatus", "", new Vector2(0, -198), new Vector2(800, 36), 18);
            content = Node(panel.transform, "SectionContent").transform;
            previousButton = Button(panel.transform, "PreviousButton", "Previous", new Vector2(-260, -246), Previous);
            nextButton = Button(panel.transform, "NextButton", "Next", new Vector2(0, -246), Next);
            submitButton = Button(panel.transform, "SubmitButton", "Submit", new Vector2(260, -246), Submit);
            ApplyUserScale();
        }

        private void RebuildPages()
        {
            foreach (var child in pageObjects) if (child != null) Destroy(child);
            pageObjects.Clear();
            likertButtons.Clear(); itemPages.Clear();
            if (content == null || controller?.Service.Definition == null) return;
            var definition = controller.Service.Definition;
            var enabled = controller.GetComponent<ExperimentConditionManager>().QuestionnaireCatalog
                .GetEnabledItems(definition.questionnaireId, controller.GetComponent<ExperimentConditionManager>().ExperimentProtocol);
            foreach (var section in definition.sections.OrderBy(x => x.displayOrder))
            {
                var items = enabled.Where(x => x.sectionId == section.sectionId).ToArray();
                if (items.Length == 0) continue;
                var pageRoot = Node(content, "Page_" + section.sectionId); pageObjects.Add(pageRoot);
                Label(pageRoot.transform, "SectionTitle", section.displayNameEnglish + " / " + section.displayNameChinese, new Vector2(0, 176), new Vector2(820, 42), 25);
                var y = 116f;
                foreach (var item in items)
                {
                    itemPages[item.itemId] = pageObjects.Count - 1;
                    Label(pageRoot.transform, "Prompt_" + item.itemId, item.promptChinese + "\n" + item.promptEnglish,
                        new Vector2(-150, y), new Vector2(500, 62), 17, TextAnchor.MiddleLeft);
                    if (item.itemType == QuestionnaireItemType.Likert)
                    {
                        for (var value = item.scaleMin; value <= item.scaleMax; value++)
                        {
                            var capturedItem = item.itemId; var capturedValue = value;
                            var button = Button(pageRoot.transform, $"{item.itemId}_{value}", value.ToString(),
                                new Vector2(136 + (value - item.scaleMin) * 44, y),
                                () => { controller.SetResponse(capturedItem, capturedValue.ToString(), out var error); validationText.text = Humanize(error); },
                                new Vector2(40, 42));
                            likertButtons[capturedItem + ":" + capturedValue] = button;
                        }
                    }
                    y -= 82f;
                }
            }
        }

        public void ShowPage(int value) => ShowPage(value, true);
        private void ShowPage(int value, bool recordNavigation)
        {
            if (pageObjects.Count == 0) return;
            page = Mathf.Clamp(value, 0, pageObjects.Count - 1); submitConfirmationArmed = false;
            for (var i = 0; i < pageObjects.Count; i++) pageObjects[i].SetActive(i == page);
            progressText.text = $"Page {page + 1} / {pageObjects.Count}";
            previousButton.interactable = page > 0; nextButton.interactable = page < pageObjects.Count - 1;
            submitButton.gameObject.SetActive(page == pageObjects.Count - 1);
            submitButton.GetComponentInChildren<TMP_Text>().text = "Submit";
            if (recordNavigation) controller.CompletePage(page, out _);
        }
        public void Previous() => ShowPage(page - 1);
        public void Next() => ShowPage(page + 1);
        public void Submit()
        {
            if (submissionInProgress) return;
            if (!controller.Service.CanSubmit(out var error))
            {
                validationText.text = Humanize(error);
                if (error != null && error.StartsWith("required_item_missing:", StringComparison.Ordinal))
                {
                    var itemId = error.Substring("required_item_missing:".Length);
                    if (itemPages.TryGetValue(itemId, out var targetPage)) ShowPage(targetPage);
                }
                return;
            }
            if (!submitConfirmationArmed) { submitConfirmationArmed = true; validationText.text = "Press again to confirm submission."; submitButton.GetComponentInChildren<TMP_Text>().text = "Confirm"; return; }
            submissionInProgress = true;
            if (!controller.Submit(out error)) { submissionInProgress = false; validationText.text = Humanize(error); return; }
            submissionInProgress = false;
            validationText.text = "Submitted"; panel.SetActive(false);
        }

        private void RefreshLikertSelection(QuestionnaireSession session)
        {
            var selected = (session.responses ?? Array.Empty<QuestionnaireResponse>())
                .Where(x => !string.IsNullOrWhiteSpace(x.rawValue)).ToDictionary(x => x.itemId, x => x.rawValue);
            foreach (var pair in likertButtons)
            {
                var split = pair.Key.LastIndexOf(':');
                var item = split < 0 ? pair.Key : pair.Key.Substring(0, split);
                var value = split < 0 ? string.Empty : pair.Key.Substring(split + 1);
                pair.Value.GetComponent<Image>().color = selected.TryGetValue(item, out var current) && current == value
                    ? new Color(.12f, .68f, .34f, 1f) : new Color(0.12f, 0.38f, 0.62f, 1f);
            }
            if (validationText != null && session.completionStatus != QuestionnaireCompletionStatus.Submitted)
                validationText.text = session.hasMissing ? "Please answer all required items." : "All required items are answered.";
        }

        private static string Humanize(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return string.Empty;
            if (error.StartsWith("required_item_missing:", StringComparison.Ordinal)) return "Please answer every required question before submitting.";
            if (error == "questionnaire_already_submitted") return "This questionnaire has already been submitted.";
            return "Unable to submit: " + error;
        }

        private void ApplyUserScale()
        {
            var settings = SceneTalkUserSettingsStore.Current;
            panel.transform.localScale = Vector3.one * settings.uiScale;
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true)) label.fontSize *= settings.fontScale;
        }
        private static GameObject Node(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = Node(parent, name); var text = go.AddComponent<TextMeshProUGUI>(); text.text = value;
            text.color = Color.white; text.fontSize = fontSize; text.alignment = ToTmpAlignment(anchor); text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            var rect = text.rectTransform; rect.anchoredPosition = position; rect.sizeDelta = size; return text;
        }
        private static Button Button(Transform parent, string name, string value, Vector2 position,
            UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            var go = Node(parent, name); var image = go.AddComponent<Image>(); image.color = new Color(0.12f, 0.38f, 0.62f, 1f);
            var button = go.AddComponent<Button>(); button.onClick.AddListener(action); var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size ?? new Vector2(112, 42);
            var label = Label(go.transform, "Label", value, Vector2.zero, rect.sizeDelta, 18); label.raycastTarget = false; return button;
        }
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch { TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter => TextAlignmentOptions.Top, TextAnchor.UpperRight => TextAlignmentOptions.TopRight, TextAnchor.MiddleLeft => TextAlignmentOptions.Left, TextAnchor.MiddleRight => TextAlignmentOptions.Right, TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft, TextAnchor.LowerCenter => TextAlignmentOptions.Bottom, TextAnchor.LowerRight => TextAlignmentOptions.BottomRight, _ => TextAlignmentOptions.Center };
    }
}
