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
        private static readonly Vector2 PanelSize = new Vector2(1120f, 720f);
        private const float NavigationY = -328f;
        private const float LikertStartX = 175f;
        private const float LikertSpacing = 56f;

        [SerializeField] private QuestionnaireRuntimeController controller;
        [SerializeField] private Canvas worldCanvas;
        private GameObject panel;
        private Transform content;
        private TMP_Text progressText;
        private TMP_Text validationText;
        private Button previousButton;
        private Button nextButton;
        private Button skipButton;
        private Button submitButton;
        private int page;
        private bool submitConfirmationArmed;
        private bool skipConfirmationArmed;
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

        public void ResetForCanvasRebuild()
        {
            panel = null;
            content = null;
            progressText = null;
            validationText = null;
            previousButton = null;
            nextButton = null;
            skipButton = null;
            submitButton = null;
            page = 0;
            submitConfirmationArmed = false;
            skipConfirmationArmed = false;
            submissionInProgress = false;
            activeLinkageKey = string.Empty;
            pageObjects.Clear();
            likertButtons.Clear();
            itemPages.Clear();
        }

        private void OnQuestionnaireChanged(QuestionnaireSession session)
        {
            if (session == null) { if (panel != null) panel.SetActive(false); return; }
            EnsureBuilt();
            ResetConfirmations();
            if (!string.Equals(activeLinkageKey, session.questionnaireLinkageKey, StringComparison.Ordinal))
            {
                activeLinkageKey = session.questionnaireLinkageKey;
                RebuildPages();
            }
            ShowPage(Mathf.Clamp(session.currentPage, 0, Mathf.Max(0, pageObjects.Count - 1)), false);
            RefreshLikertSelection(session);
            panel.SetActive(!IsTerminal(session.completionStatus));
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
            var rect = panel.GetComponent<RectTransform>(); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = PanelSize;
            progressText = Label(panel.transform, "Progress", "", new Vector2(0, 316), new Vector2(1000, 46), 28);
            validationText = Label(panel.transform, "RequiredStatus", "", new Vector2(0, -268), new Vector2(1000, 42), 22);
            content = Node(panel.transform, "SectionContent").transform;
            previousButton = Button(panel.transform, "PreviousButton", "上一页", new Vector2(-360, NavigationY), Previous, new Vector2(160, 54));
            nextButton = Button(panel.transform, "NextButton", "下一页", new Vector2(-120, NavigationY), Next, new Vector2(160, 54));
            skipButton = Button(panel.transform, "SkipButton", "跳过", new Vector2(130, NavigationY), Skip, new Vector2(160, 54));
            submitButton = Button(panel.transform, "SubmitButton", "提交", new Vector2(370, NavigationY), Submit, new Vector2(160, 54));
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
                Label(pageRoot.transform, "SectionTitle",
                    SceneTalkUiText.IsEnglish ? section.displayNameEnglish : section.displayNameChinese,
                    new Vector2(0, 252), new Vector2(1000, 52), 30);
                var y = 174f;
                foreach (var item in items)
                {
                    itemPages[item.itemId] = pageObjects.Count - 1;
                    Label(pageRoot.transform, "Prompt_" + item.itemId,
                        SceneTalkUiText.IsEnglish ? item.promptEnglish : item.promptChinese,
                        new Vector2(-180, y), new Vector2(620, 70), 22, TextAnchor.MiddleLeft);
                    if (item.itemType == QuestionnaireItemType.Likert)
                    {
                        for (var value = item.scaleMin; value <= item.scaleMax; value++)
                        {
                            var capturedItem = item.itemId; var capturedValue = value;
                            var button = Button(pageRoot.transform, $"{item.itemId}_{value}", value.ToString(),
                                new Vector2(LikertStartX + (value - item.scaleMin) * LikertSpacing, y),
                                () => { controller.SetResponse(capturedItem, capturedValue.ToString(), out var error); validationText.text = Humanize(error); },
                                new Vector2(50, 52));
                            likertButtons[capturedItem + ":" + capturedValue] = button;
                        }
                    }
                    y -= 92f;
                }
            }
        }

        public void ShowPage(int value) => ShowPage(value, true);
        private void ShowPage(int value, bool recordNavigation)
        {
            if (pageObjects.Count == 0) return;
            page = Mathf.Clamp(value, 0, pageObjects.Count - 1);
            ResetConfirmations();
            for (var i = 0; i < pageObjects.Count; i++) pageObjects[i].SetActive(i == page);
            progressText.text = SceneTalkUiText.Select(
                $"第 {page + 1} / {pageObjects.Count} 页",
                $"Page {page + 1} / {pageObjects.Count}");
            previousButton.interactable = page > 0; nextButton.interactable = page < pageObjects.Count - 1;
            skipButton.gameObject.SetActive(page == pageObjects.Count - 1);
            submitButton.gameObject.SetActive(page == pageObjects.Count - 1);
            if (recordNavigation) controller.CompletePage(page, out _);
        }
        public void Previous() => ShowPage(page - 1);
        public void Next() => ShowPage(page + 1);
        public void Submit()
        {
            if (submissionInProgress) return;
            CancelSkipConfirmation();
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
            if (!submitConfirmationArmed) { submitConfirmationArmed = true; validationText.text = SceneTalkUiText.Text("请再次点击以确认提交。"); submitButton.GetComponentInChildren<TMP_Text>().text = SceneTalkUiText.Text("确认提交"); return; }
            submissionInProgress = true;
            if (!controller.Submit(out error)) { submissionInProgress = false; validationText.text = Humanize(error); return; }
            submissionInProgress = false;
            validationText.text = SceneTalkUiText.Text("已提交"); panel.SetActive(false);
        }

        public void Skip()
        {
            if (submissionInProgress) return;
            CancelSubmitConfirmation();
            if (!controller.Service.CanSkip(out var error))
            { validationText.text = Humanize(error); return; }
            if (!skipConfirmationArmed)
            {
                skipConfirmationArmed = true;
                validationText.text = SceneTalkUiText.Text("跳过将保留已填写内容并继续实验，请再次点击“确认跳过”。");
                skipButton.GetComponentInChildren<TMP_Text>().text = SceneTalkUiText.Text("确认跳过");
                return;
            }
            submissionInProgress = true;
            if (!controller.Skip(out error)) { submissionInProgress = false; validationText.text = Humanize(error); return; }
            submissionInProgress = false;
            validationText.text = SceneTalkUiText.Text("已跳过"); panel.SetActive(false);
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
            if (validationText != null && !IsTerminal(session.completionStatus))
                validationText.text = session.hasMissing
                    ? SceneTalkUiText.Text("请完成所有必答题。")
                    : SceneTalkUiText.Text("所有必答题均已完成。");
        }

        private void ResetConfirmations()
        {
            CancelSubmitConfirmation();
            CancelSkipConfirmation();
        }

        private void CancelSubmitConfirmation()
        {
            submitConfirmationArmed = false;
            if (submitButton != null) submitButton.GetComponentInChildren<TMP_Text>().text = SceneTalkUiText.Text("提交");
        }

        private void CancelSkipConfirmation()
        {
            skipConfirmationArmed = false;
            if (skipButton != null) skipButton.GetComponentInChildren<TMP_Text>().text = SceneTalkUiText.Text("跳过");
        }

        private static bool IsTerminal(QuestionnaireCompletionStatus status)
            => status == QuestionnaireCompletionStatus.Submitted || status == QuestionnaireCompletionStatus.Skipped;

        private static string Humanize(string error)
        {
            return SceneTalkUiText.Error(error);
        }

        private void ApplyUserScale()
        {
            var settings = SceneTalkUserSettingsStore.Current;
            panel.transform.localScale = Vector3.one * settings.uiScale;
        }
        private static GameObject Node(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = Node(parent, name); var text = go.AddComponent<TextMeshProUGUI>(); text.text = SceneTalkUiText.Text(value);
            text.color = Color.white; text.fontSize = Mathf.Max(1f, fontSize * SceneTalkUserSettingsStore.Current.fontScale); text.alignment = ToTmpAlignment(anchor); text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            var rect = text.rectTransform; rect.anchoredPosition = position; rect.sizeDelta = size; return text;
        }
        private static Button Button(Transform parent, string name, string value, Vector2 position,
            UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            var go = Node(parent, name); var image = go.AddComponent<Image>(); image.color = new Color(0.12f, 0.38f, 0.62f, 1f);
            var button = go.AddComponent<Button>(); button.onClick.AddListener(action); var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size ?? new Vector2(112, 42);
            var label = Label(go.transform, "Label", value, Vector2.zero, rect.sizeDelta, 20); label.raycastTarget = false; return button;
        }
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch { TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter => TextAlignmentOptions.Top, TextAnchor.UpperRight => TextAlignmentOptions.TopRight, TextAnchor.MiddleLeft => TextAlignmentOptions.Left, TextAnchor.MiddleRight => TextAlignmentOptions.Right, TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft, TextAnchor.LowerCenter => TextAlignmentOptions.Bottom, TextAnchor.LowerRight => TextAlignmentOptions.BottomRight, _ => TextAlignmentOptions.Center };
    }
}
