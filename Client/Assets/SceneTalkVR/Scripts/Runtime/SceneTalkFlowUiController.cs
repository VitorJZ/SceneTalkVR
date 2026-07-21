using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkFlowUiController : MonoBehaviour
    {
        private const string FlowRootName = "SceneTalkVR Flow UI";
        private static readonly Vector2 ExitButtonPosition = new Vector2(360f, 218f);
        private static readonly Vector2 ExitButtonSize = new Vector2(110f, 44f);
        private static readonly Color ExitButtonColor = new Color(0.58f, 0.18f, 0.18f, 1f);

        [SerializeField] private SceneTalkOrchestrator orchestrator;
        [SerializeField] private SceneTalkInteractionBootstrap interactionBootstrap;
        [SerializeField] private Canvas worldCanvas;

        private readonly Dictionary<TMP_Text, float> baseFontSizes = new Dictionary<TMP_Text, float>();
        private GameObject mainMenuPanel;
        private GameObject settingsPanel;
        private GameObject historyListPanel;
        private GameObject historyDetailPanel;
        private GameObject historyDeletePanel;
        private GameObject historyErrorPanel;
        private GameObject settingsGeneralGroup;
        private GameObject requestPanel;
        private GameObject taskSelectionPanel;
        private GameObject loadingPanel;
        private GameObject subtitlePanel;
        private GameObject subtitleTextContainer;
        private GameObject exitButtonObject;

        private Button startButton;
        private Button settingsButton;
        private Button historyButton;
        private Button quitButton;
        private Button fontMinusButton;
        private Button fontPlusButton;
        private Button uiMinusButton;
        private Button uiPlusButton;
        private Button subtitleChangeButton;
        private Button correctionSourceChangeButton;
        private Button correctionAppearanceChangeButton;
        private Button correctionStyleChangeButton;
        private Button listenButton;
        private Button confirmButton;
        private Button exitButton;
        private Button dialogueListenButton;

        private Button taskButton1;
        private Button taskButton2;
        private Button taskButton3;
        private Button taskButton4;
        private readonly Button[] historyRowButtons = new Button[LearningMemoryService.DefaultPageSize];
        private readonly string[] historyRowSessionIds = new string[LearningMemoryService.DefaultPageSize];
        private Button historyPreviousButton;
        private Button historyNextButton;
        private Button historyListBackButton;
        private Button historyContinueButton;
        private Button historyDeleteButton;
        private Button historyDetailBackButton;
        private Button historyPageUpButton;
        private Button historyPageDownButton;
        private Button historyDeleteConfirmButton;
        private Button historyDeleteCancelButton;
        private Button historyErrorBackButton;

        private TMP_Text settingsTitleText;
        private TMP_Text settingsPageText;
        private TMP_Text fontValueText;
        private TMP_Text uiValueText;
        private TMP_Text subtitleValueText;
        private TMP_Text correctionSourceValueText;
        private TMP_Text correctionAppearanceValueText;
        private TMP_Text correctionStyleValueText;
        private TMP_Text correctionSettingsStatusText;
        private TMP_Text historyEmptyText;
        private TMP_Text historyPageText;
        private TMP_Text historyDetailSummaryText;
        private TMP_Text historyDetailBodyText;
        private TMP_Text historyDeleteMessageText;
        private TMP_Text historyErrorText;
        private ScrollRect historyDetailScrollRect;
        private RectTransform historyDetailContentRect;
        private string lastRenderedHistorySessionId;
        private TMP_Text requestTitleText;
        private TMP_Text requestStatusText;
        private TMP_Text requestTranscriptText;
        private TMP_Text requestErrorText;
        private TMP_Text loadingText;
        private TMP_Text experimentDebugText;
        private TMP_Text dialogueStatusText;
        private TMP_Text correctionStatusText;
        private TMP_Text correctionFeedbackText;
        private TMP_Text playerSubtitleText;
        private TMP_Text avatarSubtitleText;
        private RectTransform subtitlePanelRect;
        private RectTransform subtitleTextContainerRect;

        private bool isSubscribed;

        private void Awake()
        {
            if (orchestrator != null && worldCanvas != null)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            SceneTalkUserSettingsStore.Changed += OnUserSettingsChanged;
            ApplyUserSettings(SceneTalkUserSettingsStore.Current);
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SceneTalkUserSettingsStore.Changed -= OnUserSettingsChanged;
        }

        private void Update()
        {
            Refresh();
        }

        public void Configure(
            SceneTalkOrchestrator targetOrchestrator,
            Canvas targetCanvas,
            SceneTalkInteractionBootstrap targetInteractionBootstrap)
        {
            Unsubscribe();

            orchestrator = targetOrchestrator;
            worldCanvas = targetCanvas;
            interactionBootstrap = targetInteractionBootstrap;

            Build();
            Subscribe();
            Refresh();
        }

        private void Build()
        {
            if (worldCanvas == null)
            {
                return;
            }

            ClearCanvasChildren();
            baseFontSizes.Clear();
            ConfigureCanvasRect();

            var root = new GameObject(FlowRootName).transform;
            root.SetParent(worldCanvas.transform, false);

            mainMenuPanel = CreatePanel(root, "InitialPanel", new Vector2(0f, 0f), new Vector2(380f, 430f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            CreateText(mainMenuPanel.transform, "Title", "SceneTalkVR", new Vector2(0f, 160f), new Vector2(320f, 54f), 34, TextAnchor.MiddleCenter, Color.white);
            startButton = CreateButton(mainMenuPanel.transform, "StartButton", "Start", new Vector2(0f, 86f), new Vector2(190f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            settingsButton = CreateButton(mainMenuPanel.transform, "SettingsButton", "Settings", new Vector2(0f, 20f), new Vector2(190f, 54f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyButton = CreateButton(mainMenuPanel.transform, "HistoryButton", "History", new Vector2(0f, -46f), new Vector2(190f, 54f), new Color(0.24f, 0.36f, 0.42f, 1f));
            quitButton = CreateButton(mainMenuPanel.transform, "QuitButton", "Quit", new Vector2(0f, -112f), new Vector2(190f, 54f), new Color(0.58f, 0.18f, 0.18f, 1f));

            historyListPanel = CreatePanel(root, "HistoryListPanel", Vector2.zero, new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(historyListPanel.transform, "Title", "Conversation History", new Vector2(0f, 210f), new Vector2(620f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
            historyEmptyText = CreateText(historyListPanel.transform, "Empty", "No conversation history yet.", new Vector2(0f, 10f), new Vector2(650f, 60f), 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.88f, 1f));
            for (var i = 0; i < historyRowButtons.Length; i++)
            {
                historyRowButtons[i] = CreateButton(
                    historyListPanel.transform,
                    $"HistoryRow{i + 1}",
                    string.Empty,
                    new Vector2(0f, 142f - i * 68f),
                    new Vector2(700f, 56f),
                    new Color(0.14f, 0.28f, 0.4f, 1f));
                var label = historyRowButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.alignment = TextAlignmentOptions.Left;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 13f;
                    label.fontSizeMax = 19f;
                }
            }
            historyPreviousButton = CreateButton(historyListPanel.transform, "PreviousButton", "Previous", new Vector2(-250f, -210f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyPageText = CreateText(historyListPanel.transform, "Page", "Page 1 / 1", new Vector2(0f, -210f), new Vector2(200f, 40f), 18, TextAnchor.MiddleCenter, Color.white);
            historyNextButton = CreateButton(historyListPanel.transform, "NextButton", "Next", new Vector2(250f, -210f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyListBackButton = CreateButton(historyListPanel.transform, "BackButton", "Back", new Vector2(-350f, 210f), new Vector2(100f, 40f), new Color(0.24f, 0.36f, 0.42f, 1f));

            historyDetailPanel = CreatePanel(root, "HistoryDetailPanel", Vector2.zero, new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(historyDetailPanel.transform, "Title", "History Details", new Vector2(0f, 212f), new Vector2(620f, 42f), 29, TextAnchor.MiddleCenter, Color.white);
            historyDetailSummaryText = CreateText(historyDetailPanel.transform, "Summary", string.Empty, new Vector2(-25f, 137f), new Vector2(690f, 104f), 16, TextAnchor.UpperLeft, new Color(0.82f, 0.9f, 1f, 1f));
            var historyViewport = CreatePanel(historyDetailPanel.transform, "ConversationViewport", new Vector2(-28f, -38f), new Vector2(690f, 230f), new Color(0f, 0f, 0f, 0.35f));
            historyViewport.AddComponent<RectMask2D>();
            historyDetailScrollRect = historyViewport.AddComponent<ScrollRect>();
            historyDetailScrollRect.horizontal = false;
            historyDetailScrollRect.vertical = true;
            historyDetailScrollRect.movementType = ScrollRect.MovementType.Clamped;
            historyDetailScrollRect.scrollSensitivity = 34f;
            historyDetailBodyText = CreateText(historyViewport.transform, "ConversationBody", string.Empty, Vector2.zero, new Vector2(660f, 220f), 16, TextAnchor.UpperLeft, Color.white);
            historyDetailBodyText.textWrappingMode = TextWrappingModes.Normal;
            historyDetailBodyText.overflowMode = TextOverflowModes.Overflow;
            historyDetailContentRect = historyDetailBodyText.rectTransform;
            historyDetailContentRect.anchorMin = new Vector2(0f, 1f);
            historyDetailContentRect.anchorMax = new Vector2(1f, 1f);
            historyDetailContentRect.pivot = new Vector2(0.5f, 1f);
            historyDetailContentRect.anchoredPosition = Vector2.zero;
            historyDetailContentRect.sizeDelta = new Vector2(-24f, 220f);
            historyDetailScrollRect.viewport = historyViewport.GetComponent<RectTransform>();
            historyDetailScrollRect.content = historyDetailContentRect;
            historyPageUpButton = CreateButton(historyDetailPanel.transform, "PageUpButton", "Up", new Vector2(355f, 8f), new Vector2(76f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyPageDownButton = CreateButton(historyDetailPanel.transform, "PageDownButton", "Down", new Vector2(355f, -52f), new Vector2(76f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyContinueButton = CreateButton(historyDetailPanel.transform, "ContinueButton", "Continue", new Vector2(-110f, -210f), new Vector2(170f, 46f), new Color(0.12f, 0.52f, 0.38f, 1f));
            historyDeleteButton = CreateButton(historyDetailPanel.transform, "DeleteButton", "Delete", new Vector2(110f, -210f), new Vector2(170f, 46f), ExitButtonColor);
            historyDetailBackButton = CreateButton(historyDetailPanel.transform, "BackButton", "Back", new Vector2(-330f, -210f), new Vector2(120f, 46f), new Color(0.24f, 0.36f, 0.42f, 1f));

            historyDeletePanel = CreatePanel(root, "HistoryDeletePanel", Vector2.zero, new Vector2(620f, 290f), new Color(0.04f, 0.05f, 0.07f, 0.97f));
            CreateText(historyDeletePanel.transform, "Title", "Delete History?", new Vector2(0f, 95f), new Vector2(500f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            historyDeleteMessageText = CreateText(historyDeletePanel.transform, "Message", string.Empty, new Vector2(0f, 25f), new Vector2(520f, 76f), 18, TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f, 1f));
            historyDeleteCancelButton = CreateButton(historyDeletePanel.transform, "CancelButton", "Cancel", new Vector2(-115f, -88f), new Vector2(170f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyDeleteConfirmButton = CreateButton(historyDeletePanel.transform, "ConfirmDeleteButton", "Delete", new Vector2(115f, -88f), new Vector2(170f, 48f), ExitButtonColor);

            historyErrorPanel = CreatePanel(root, "HistoryErrorPanel", Vector2.zero, new Vector2(680f, 260f), new Color(0.04f, 0.05f, 0.07f, 0.96f));
            CreateText(historyErrorPanel.transform, "Title", "History Error", new Vector2(0f, 82f), new Vector2(520f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            historyErrorText = CreateText(historyErrorPanel.transform, "Message", string.Empty, new Vector2(0f, -5f), new Vector2(560f, 110f), 18, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.42f, 1f));
            historyErrorBackButton = CreateButton(historyErrorPanel.transform, "BackButton", "Back", new Vector2(0f, -92f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));

            settingsPanel = CreatePanel(root, "SettingsPanel", new Vector2(0f, 0f), new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.92f));
            settingsTitleText = CreateText(settingsPanel.transform, "Title", "Settings", new Vector2(0f, 210f), new Vector2(480f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
            settingsPageText = CreateText(settingsPanel.transform, "Page", "Display & Correction", new Vector2(0f, 174f), new Vector2(700f, 30f), 18, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));

            settingsGeneralGroup = new GameObject("GeneralSettings");
            settingsGeneralGroup.transform.SetParent(settingsPanel.transform, false);
            CreateText(settingsGeneralGroup.transform, "FontLabel", "Font Size", new Vector2(-240f, 108f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            fontMinusButton = CreateButton(settingsGeneralGroup.transform, "FontMinusButton", "-", new Vector2(78f, 108f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            fontValueText = CreateText(settingsGeneralGroup.transform, "FontValue", string.Empty, new Vector2(174f, 108f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            fontPlusButton = CreateButton(settingsGeneralGroup.transform, "FontPlusButton", "+", new Vector2(270f, 108f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "UiLabel", "Interface Size", new Vector2(-240f, 58f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            uiMinusButton = CreateButton(settingsGeneralGroup.transform, "UiMinusButton", "-", new Vector2(78f, 58f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            uiValueText = CreateText(settingsGeneralGroup.transform, "UiValue", string.Empty, new Vector2(174f, 58f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            uiPlusButton = CreateButton(settingsGeneralGroup.transform, "UiPlusButton", "+", new Vector2(270f, 58f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "SubtitleLabel", "Dialogue Subtitles", new Vector2(-240f, 8f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            subtitleValueText = CreateText(settingsGeneralGroup.transform, "SubtitleValue", string.Empty, new Vector2(110f, 8f), new Vector2(140f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            subtitleChangeButton = CreateButton(settingsGeneralGroup.transform, "SubtitleChangeButton", "Change", new Vector2(293f, 8f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionSourceLabel", "Correction Source", new Vector2(-240f, -42f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionSourceValueText = CreateText(settingsGeneralGroup.transform, "CorrectionSourceValue", string.Empty, new Vector2(110f, -42f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionSourceChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionSourceChangeButton", "Change", new Vector2(293f, -42f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionAppearanceLabel", "Assistant Appearance", new Vector2(-240f, -92f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionAppearanceValueText = CreateText(settingsGeneralGroup.transform, "CorrectionAppearanceValue", string.Empty, new Vector2(110f, -92f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionAppearanceChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionAppearanceChangeButton", "Change", new Vector2(293f, -92f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionStyleLabel", "Correction Style", new Vector2(-240f, -142f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionStyleValueText = CreateText(settingsGeneralGroup.transform, "CorrectionStyleValue", string.Empty, new Vector2(110f, -142f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionStyleChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionStyleChangeButton", "Change", new Vector2(293f, -142f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            correctionSettingsStatusText = CreateText(settingsGeneralGroup.transform, "CorrectionSettingsStatus", string.Empty, new Vector2(0f, -202f), new Vector2(700f, 30f), 15, TextAnchor.MiddleCenter, new Color(0.72f, 0.8f, 0.86f, 1f));

            requestPanel = CreatePanel(root, "RequestPanel", new Vector2(0f, 0f), new Vector2(700f, 380f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            requestTitleText = CreateText(requestPanel.transform, "Title", "Scene And Avatar Request", new Vector2(0f, 146f), new Vector2(640f, 42f), 26, TextAnchor.MiddleCenter, Color.white);
            requestStatusText = CreateText(requestPanel.transform, "Status", "Listening...", new Vector2(0f, 104f), new Vector2(640f, 34f), 20, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));
            requestTranscriptText = CreateText(requestPanel.transform, "Transcript", "Transcript: -", new Vector2(0f, 28f), new Vector2(620f, 112f), 22, TextAnchor.MiddleCenter, Color.white);
            requestErrorText = CreateText(requestPanel.transform, "Error", string.Empty, new Vector2(0f, -64f), new Vector2(620f, 34f), 18, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.35f, 1f));
            listenButton = CreateButton(requestPanel.transform, "ListenButton", "Listen", new Vector2(-110f, -142f), new Vector2(150f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            confirmButton = CreateButton(requestPanel.transform, "ConfirmButton", "Confirm", new Vector2(110f, -142f), new Vector2(150f, 54f), new Color(0.12f, 0.52f, 0.38f, 1f));

            // Fixed Task Selection Panel
            taskSelectionPanel = CreatePanel(root, "TaskSelectionPanel", new Vector2(0f, 0f), new Vector2(900f, 520f), new Color(0.04f, 0.05f, 0.07f, 0.95f));
            CreateText(taskSelectionPanel.transform, "Title", "Select a Practice Task", new Vector2(0f, 220f), new Vector2(800f, 44f), 28, TextAnchor.MiddleCenter, Color.white);

            taskButton1 = CreateButton(taskSelectionPanel.transform, "Task1Button", "Restaurant Reservation", new Vector2(-210f, 90f), new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            CreateText(taskSelectionPanel.transform, "Task1Context", "Context: Reserve a table at an Italian restaurant for 5 people.\nGoals: corner table, bring cake, parking.", new Vector2(-210f, 10f), new Vector2(380f, 80f), 15, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 1f));

            taskButton2 = CreateButton(taskSelectionPanel.transform, "Task2Button", "Furniture Shopping", new Vector2(210f, 90f), new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            CreateText(taskSelectionPanel.transform, "Task2Context", "Context: Speak with a salesperson to buy a desk.\nGoals: desk size/style, colors, delivery, discounts.", new Vector2(210f, 10f), new Vector2(380f, 80f), 15, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 1f));

            taskButton3 = CreateButton(taskSelectionPanel.transform, "Task3Button", "Gym Membership", new Vector2(-210f, -100f), new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            CreateText(taskSelectionPanel.transform, "Task3Context", "Context: Ask about gym membership options.\nGoals: monthly price, student discount, opening hours, trial class.", new Vector2(-210f, -180f), new Vector2(380f, 80f), 15, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 1f));

            taskButton4 = CreateButton(taskSelectionPanel.transform, "Task4Button", "Hotel Check-In", new Vector2(210f, -100f), new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            CreateText(taskSelectionPanel.transform, "Task4Context", "Context: Check in at a hotel and confirm details.\nGoals: confirm booking, breakfast included, quiet room, check-out.", new Vector2(210f, -180f), new Vector2(380f, 80f), 15, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 1f));

            loadingPanel = CreatePanel(root, "LoadingPanel", new Vector2(0f, 0f), new Vector2(540f, 220f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            loadingText = CreateText(loadingPanel.transform, "LoadingText", "Loading scene and avatar...", new Vector2(0f, 0f), new Vector2(480f, 80f), 26, TextAnchor.MiddleCenter, Color.white);

            subtitlePanel = CreatePanel(root, "SubtitlePanel", new Vector2(0f, -136f), new Vector2(840f, 248f), new Color(0f, 0f, 0f, 0.62f));
            subtitlePanel.AddComponent<RectMask2D>();
            subtitlePanelRect = subtitlePanel.GetComponent<RectTransform>();
            
            subtitleTextContainer = new GameObject("TextContainer");
            subtitleTextContainer.transform.SetParent(subtitlePanel.transform, false);
            subtitleTextContainerRect = subtitleTextContainer.AddComponent<RectTransform>();
            subtitleTextContainerRect.anchoredPosition = new Vector2(-65f, 48f);
            subtitleTextContainerRect.sizeDelta = new Vector2(650f, 100f);

            playerSubtitleText = CreateText(subtitleTextContainer.transform, "PlayerSubtitle", "You: -", new Vector2(0f, 36f), new Vector2(650f, 28f), 18, TextAnchor.UpperLeft, new Color(0.45f, 0.9f, 1f, 1f));
            avatarSubtitleText = CreateText(subtitleTextContainer.transform, "AvatarSubtitle", "Avatar: -", new Vector2(0f, -14f), new Vector2(650f, 72f), 19, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.36f, 1f));
            ConfigureDialogueText(playerSubtitleText);
            ConfigureDialogueText(avatarSubtitleText);

            experimentDebugText = CreateText(subtitlePanel.transform, "ExperimentDebug", string.Empty, new Vector2(-65f, 111f), new Vector2(650f, 22f), 13, TextAnchor.MiddleLeft, new Color(0.72f, 0.8f, 0.86f, 1f));
            correctionFeedbackText = CreateText(subtitlePanel.transform, "CorrectionFeedback", string.Empty, new Vector2(-65f, -32f), new Vector2(650f, 36f), 16, TextAnchor.UpperLeft, new Color(0.78f, 0.95f, 0.74f, 1f));
            correctionStatusText = CreateText(subtitlePanel.transform, "CorrectionStatus", string.Empty, new Vector2(-65f, -77f), new Vector2(650f, 28f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            dialogueStatusText = CreateText(subtitlePanel.transform, "DialogueStatus", "Ready", new Vector2(-65f, -102f), new Vector2(650f, 28f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            ConfigureDialogueText(experimentDebugText);
            ConfigureDialogueText(correctionFeedbackText);
            ConfigureDialogueText(correctionStatusText);
            ConfigureDialogueText(dialogueStatusText);

            dialogueListenButton = CreateButton(subtitlePanel.transform, "DialogueListenButton", "Speak", new Vector2(350f, -92f), new Vector2(110f, 40f), new Color(0.12f, 0.52f, 0.38f, 1f));
            
            exitButton = CreateButton(root, "ExitButton", "Exit", ExitButtonPosition, ExitButtonSize, ExitButtonColor);
            exitButtonObject = exitButton.gameObject;

            BindButtons();
            CaptureBaseFontSizes(root);
            ApplyUserSettings(SceneTalkUserSettingsStore.Current);
        }

        private void BindButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(() => orchestrator?.StartPractice());
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OpenSettings);
            }

            if (historyButton != null)
            {
                historyButton.onClick.RemoveAllListeners();
                historyButton.onClick.AddListener(() => orchestrator?.OpenHistory());
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitApplication);
            }

            if (fontMinusButton != null)
            {
                fontMinusButton.onClick.RemoveAllListeners();
                fontMinusButton.onClick.AddListener(() => SceneTalkUserSettingsStore.AdjustFontScale(-SceneTalkUserSettings.FontScaleStep));
            }

            if (fontPlusButton != null)
            {
                fontPlusButton.onClick.RemoveAllListeners();
                fontPlusButton.onClick.AddListener(() => SceneTalkUserSettingsStore.AdjustFontScale(SceneTalkUserSettings.FontScaleStep));
            }

            if (uiMinusButton != null)
            {
                uiMinusButton.onClick.RemoveAllListeners();
                uiMinusButton.onClick.AddListener(() => SceneTalkUserSettingsStore.AdjustUiScale(-SceneTalkUserSettings.UiScaleStep));
            }

            if (uiPlusButton != null)
            {
                uiPlusButton.onClick.RemoveAllListeners();
                uiPlusButton.onClick.AddListener(() => SceneTalkUserSettingsStore.AdjustUiScale(SceneTalkUserSettings.UiScaleStep));
            }

            if (subtitleChangeButton != null)
            {
                subtitleChangeButton.onClick.RemoveAllListeners();
                subtitleChangeButton.onClick.AddListener(() =>
                    SceneTalkUserSettingsStore.SetHideDialogueSubtitles(!SceneTalkUserSettingsStore.Current.hideDialogueSubtitles));
            }

            if (correctionSourceChangeButton != null)
            {
                correctionSourceChangeButton.onClick.RemoveAllListeners();
                correctionSourceChangeButton.onClick.AddListener(() => orchestrator?.ChangeCorrectionProviderSetting());
            }

            if (correctionAppearanceChangeButton != null)
            {
                correctionAppearanceChangeButton.onClick.RemoveAllListeners();
                correctionAppearanceChangeButton.onClick.AddListener(() =>
                    orchestrator?.ChangeCorrectionAssistantEmbodimentSetting());
            }

            if (correctionStyleChangeButton != null)
            {
                correctionStyleChangeButton.onClick.RemoveAllListeners();
                correctionStyleChangeButton.onClick.AddListener(() => orchestrator?.ChangeCorrectionStyleSetting());
            }

            if (listenButton != null)
            {
                listenButton.onClick.RemoveAllListeners();
                listenButton.onClick.AddListener(() => orchestrator?.ToggleRequestSpeechCapture());
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => orchestrator?.ConfirmPracticeRequest());
            }

            if (taskButton1 != null)
            {
                taskButton1.onClick.RemoveAllListeners();
                taskButton1.onClick.AddListener(() => orchestrator?.ConfirmFixedTaskSelection("restaurant_reservation"));
            }

            if (taskButton2 != null)
            {
                taskButton2.onClick.RemoveAllListeners();
                taskButton2.onClick.AddListener(() => orchestrator?.ConfirmFixedTaskSelection("furniture_shopping"));
            }

            if (taskButton3 != null)
            {
                taskButton3.onClick.RemoveAllListeners();
                taskButton3.onClick.AddListener(() => orchestrator?.ConfirmFixedTaskSelection("gym_membership"));
            }

            if (taskButton4 != null)
            {
                taskButton4.onClick.RemoveAllListeners();
                taskButton4.onClick.AddListener(() => orchestrator?.ConfirmFixedTaskSelection("hotel_check_in"));
            }

            if (historyPreviousButton != null)
            {
                historyPreviousButton.onClick.RemoveAllListeners();
                historyPreviousButton.onClick.AddListener(() => orchestrator?.OpenPreviousHistoryPage());
            }

            if (historyNextButton != null)
            {
                historyNextButton.onClick.RemoveAllListeners();
                historyNextButton.onClick.AddListener(() => orchestrator?.OpenNextHistoryPage());
            }

            if (historyListBackButton != null)
            {
                historyListBackButton.onClick.RemoveAllListeners();
                historyListBackButton.onClick.AddListener(() => orchestrator?.BackFromHistory());
            }

            if (historyContinueButton != null)
            {
                historyContinueButton.onClick.RemoveAllListeners();
                historyContinueButton.onClick.AddListener(() => orchestrator?.ContinueSelectedHistory());
            }

            if (historyDeleteButton != null)
            {
                historyDeleteButton.onClick.RemoveAllListeners();
                historyDeleteButton.onClick.AddListener(() => orchestrator?.RequestDeleteSelectedHistory());
            }

            if (historyDetailBackButton != null)
            {
                historyDetailBackButton.onClick.RemoveAllListeners();
                historyDetailBackButton.onClick.AddListener(() => orchestrator?.BackFromHistory());
            }

            if (historyDeleteCancelButton != null)
            {
                historyDeleteCancelButton.onClick.RemoveAllListeners();
                historyDeleteCancelButton.onClick.AddListener(() => orchestrator?.CancelDeleteSelectedHistory());
            }

            if (historyDeleteConfirmButton != null)
            {
                historyDeleteConfirmButton.onClick.RemoveAllListeners();
                historyDeleteConfirmButton.onClick.AddListener(() => orchestrator?.ConfirmDeleteSelectedHistory());
            }

            if (historyErrorBackButton != null)
            {
                historyErrorBackButton.onClick.RemoveAllListeners();
                historyErrorBackButton.onClick.AddListener(() => orchestrator?.BackFromHistory());
            }

            if (historyPageUpButton != null)
            {
                historyPageUpButton.onClick.RemoveAllListeners();
                historyPageUpButton.onClick.AddListener(() => ScrollHistoryDetails(0.8f));
            }

            if (historyPageDownButton != null)
            {
                historyPageDownButton.onClick.RemoveAllListeners();
                historyPageDownButton.onClick.AddListener(() => ScrollHistoryDetails(-0.8f));
            }

            if (dialogueListenButton != null)
            {
                dialogueListenButton.onClick.RemoveAllListeners();
                dialogueListenButton.onClick.AddListener(() => orchestrator?.ToggleDialogueSpeechCapture());
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(ExitCurrentView);
            }
        }

        private void Refresh()
        {
            if (orchestrator == null)
            {
                return;
            }

            var state = orchestrator.CurrentState;
            var dialogueActive = orchestrator.IsDialogueActive;
            bool isFixedMode = orchestrator.RuntimeConfig != null && orchestrator.RuntimeConfig.UseFixedExperimentMode;

            var showMain = state == SceneTalkState.Idle || state == SceneTalkState.Finished;
            var showSettings = state == SceneTalkState.Settings;
            var showHistoryList = state == SceneTalkState.HistoryList;
            var showHistoryDetail = state == SceneTalkState.HistoryDetail;
            var showHistoryDelete = state == SceneTalkState.HistoryDeleteConfirm;
            var showHistoryError = state == SceneTalkState.HistoryError;
            var showRequest = !dialogueActive
                && (!isFixedMode || state != SceneTalkState.Listening)
                && (state == SceneTalkState.Listening
                    || state == SceneTalkState.Recording
                    || state == SceneTalkState.Transcribing
                    || state == SceneTalkState.Error);
            var showTaskSelection = isFixedMode
                && !dialogueActive
                && (state == SceneTalkState.Listening);
            var showLoading = !dialogueActive
                && (state == SceneTalkState.Processing
                    || state == SceneTalkState.SceneReady
                    || state == SceneTalkState.HistoryLoading
                    || state == SceneTalkState.HistoryRestoring);
            var showDialogue = dialogueActive
                || state == SceneTalkState.AvatarSpeaking
                || state == SceneTalkState.CorrectionFeedbackSpeaking
                || state == SceneTalkState.DialogueSpeaking
                || state == SceneTalkState.TurnReview;

            SetActive(mainMenuPanel, showMain);
            SetActive(historyButton?.gameObject, showMain && orchestrator.IsHistoryAvailable);
            SetActive(settingsPanel, showSettings);
            SetActive(historyListPanel, showHistoryList);
            SetActive(historyDetailPanel, showHistoryDetail);
            SetActive(historyDeletePanel, showHistoryDelete);
            SetActive(historyErrorPanel, showHistoryError);
            SetActive(requestPanel, showRequest);
            SetActive(taskSelectionPanel, showTaskSelection);
            SetActive(loadingPanel, showLoading);
            SetActive(subtitlePanel, showDialogue);
            SetActive(exitButtonObject, !showMain);

            RefreshSettingsPanel(showSettings);
            RefreshHistoryList(showHistoryList);
            RefreshHistoryDetail(showHistoryDetail);
            RefreshHistoryDelete(showHistoryDelete);
            RefreshHistoryError(showHistoryError);
            RefreshRequestPanel(showRequest);
            RefreshLoadingPanel(showLoading);
            RefreshSubtitlePanel(showDialogue);
        }

        private void RefreshSettingsPanel(bool isVisible)
        {
            if (!isVisible)
            {
                return;
            }

            var settings = SceneTalkUserSettingsStore.Current;

            SetActive(settingsGeneralGroup, true);

            if (settingsTitleText != null)
            {
                settingsTitleText.text = "Settings";
            }

            if (settingsPageText != null)
            {
                settingsPageText.text = "Display & Correction";
            }

            if (fontValueText != null)
            {
                fontValueText.text = $"{Mathf.RoundToInt(settings.fontScale * 100f)}%";
            }

            if (uiValueText != null)
            {
                uiValueText.text = $"{Mathf.RoundToInt(settings.uiScale * 100f)}%";
            }

            if (subtitleValueText != null)
            {
                subtitleValueText.text = settings.hideDialogueSubtitles ? "Hidden" : "Shown";
            }

            if (correctionSourceValueText != null)
            {
                correctionSourceValueText.text = ResolveCorrectionSourceDisplayName(
                    orchestrator.CorrectionProviderSetting);
            }

            if (correctionStyleValueText != null)
            {
                correctionStyleValueText.text = ResolveCorrectionStyleDisplayName(
                    orchestrator.CorrectionStyleSetting);
            }

            var canChangeCorrection = orchestrator.CanChangeCorrectionSetting;
            var usesAssistantAgent = string.Equals(
                orchestrator.CorrectionProviderSetting,
                ExperimentConditionManager.AssistantAgentProvider,
                System.StringComparison.OrdinalIgnoreCase);
            var canChangeAppearance = orchestrator.CanChangeCorrectionAssistantEmbodimentSetting;

            if (correctionAppearanceValueText != null)
            {
                correctionAppearanceValueText.text = usesAssistantAgent
                    ? ResolveCorrectionAppearanceDisplayName(
                        orchestrator.CorrectionAssistantEmbodimentSetting)
                    : "N/A";
            }

            SetInteractable(correctionSourceChangeButton, canChangeCorrection);
            SetInteractable(correctionAppearanceChangeButton, canChangeAppearance);
            SetInteractable(correctionStyleChangeButton, canChangeCorrection);
            SetButtonLabel(correctionSourceChangeButton, canChangeCorrection ? "Change" : "Locked");
            SetButtonLabel(
                correctionAppearanceChangeButton,
                !canChangeCorrection ? "Locked" : usesAssistantAgent ? "Change" : "N/A");
            SetButtonLabel(correctionStyleChangeButton, canChangeCorrection ? "Change" : "Locked");

            if (correctionSettingsStatusText != null)
            {
                correctionSettingsStatusText.text = !canChangeCorrection
                    ? orchestrator.CorrectionSettingLockReason
                    : usesAssistantAgent
                        ? "Changes apply from the next turn."
                        : "Choose Assistant Agent to change its appearance.";
            }
        }

        private void RefreshHistoryList(bool isVisible)
        {
            if (!isVisible)
            {
                return;
            }

            lastRenderedHistorySessionId = string.Empty;

            var page = orchestrator.CurrentHistoryPage ?? new LearningSessionPage();
            var items = page.items ?? System.Array.Empty<LearningSessionSummary>();
            SetActive(historyEmptyText?.gameObject, items.Length == 0);

            for (var i = 0; i < historyRowButtons.Length; i++)
            {
                var button = historyRowButtons[i];
                var hasItem = i < items.Length;
                SetActive(button?.gameObject, hasItem);
                if (!hasItem || button == null)
                {
                    historyRowSessionIds[i] = string.Empty;
                    continue;
                }

                var item = items[i];
                SetButtonLabel(
                    button,
                    $"{item.title}    {FormatHistoryTime(item.updatedAtUnixMs)}\n"
                    + $"{item.turnCount} turns  |  {item.correctionCount} corrections  |  "
                    + $"{ResolveCorrectionSourceDisplayName(item.correctionProvider)} / "
                    + ResolveCorrectionStyleDisplayName(item.correctionStyle));

                if (!string.Equals(historyRowSessionIds[i], item.sessionId, System.StringComparison.Ordinal))
                {
                    historyRowSessionIds[i] = item.sessionId;
                    button.onClick.RemoveAllListeners();
                    var capturedId = item.sessionId;
                    button.onClick.AddListener(() => orchestrator?.SelectHistorySession(capturedId));
                }
            }

            if (historyPageText != null)
            {
                historyPageText.text = $"Page {page.pageIndex + 1} / {page.TotalPages}";
            }

            SetInteractable(historyPreviousButton, page.pageIndex > 0);
            SetInteractable(historyNextButton, page.pageIndex + 1 < page.TotalPages);
        }

        private void RefreshHistoryDetail(bool isVisible)
        {
            if (!isVisible)
            {
                return;
            }

            var detail = orchestrator.SelectedHistorySession;
            if (detail?.summary == null)
            {
                return;
            }

            var scene = detail.sceneSnapshot;
            var appearance = scene?.avatarRole?.appearance;
            if (historyDetailSummaryText != null)
            {
                historyDetailSummaryText.text =
                    $"{detail.summary.title}\n"
                    + $"Created {FormatHistoryTime(detail.summary.createdAtUnixMs)}  |  Updated {FormatHistoryTime(detail.summary.updatedAtUnixMs)}\n"
                    + $"Scenario: {detail.summary.scenarioId}  |  Environment: {detail.summary.environmentType}\n"
                    + $"Avatar: {scene?.avatarRole?.role ?? "-"} / {appearance?.genderPresentation ?? "unknown"}  |  "
                    + $"Correction: {ResolveCorrectionSourceDisplayName(detail.summary.correctionProvider)} / "
                    + $"{ResolveCorrectionStyleDisplayName(detail.summary.correctionStyle)}  |  "
                    + $"Sensitivity: {detail.settings?.feedbackSensitivity ?? "moderate"}\n"
                    + $"Turns: {detail.summary.turnCount}  |  Corrections: {detail.summary.correctionCount}";
            }

            if (!string.Equals(
                    lastRenderedHistorySessionId,
                    detail.summary.sessionId,
                    System.StringComparison.Ordinal))
            {
                lastRenderedHistorySessionId = detail.summary.sessionId;
                if (historyDetailBodyText != null)
                {
                    historyDetailBodyText.text = BuildHistoryDetailText(detail);
                    var contentHeight = Mathf.Max(220f, historyDetailBodyText.preferredHeight + 24f);
                    historyDetailContentRect.sizeDelta = new Vector2(-24f, contentHeight);
                    Canvas.ForceUpdateCanvases();
                    historyDetailScrollRect.verticalNormalizedPosition = 1f;
                }
            }

            var canScroll = historyDetailContentRect != null
                && historyDetailScrollRect != null
                && historyDetailContentRect.rect.height > historyDetailScrollRect.viewport.rect.height + 1f;
            SetInteractable(historyPageUpButton, canScroll);
            SetInteractable(historyPageDownButton, canScroll);
        }

        private void RefreshHistoryDelete(bool isVisible)
        {
            if (!isVisible || historyDeleteMessageText == null)
            {
                return;
            }

            var title = orchestrator.SelectedHistorySession?.summary?.title ?? "this conversation";
            historyDeleteMessageText.text =
                $"Delete \"{title}\" permanently?\nThis removes its database rows and cached scene files.";
        }

        private void RefreshHistoryError(bool isVisible)
        {
            if (isVisible && historyErrorText != null)
            {
                historyErrorText.text = orchestrator.HistoryErrorMessage;
            }
        }

        private static string BuildHistoryDetailText(LearningSessionDetail detail)
        {
            var builder = new StringBuilder();
            var task = detail.settings?.condition?.task;
            var avatar = detail.sceneSnapshot?.avatarRole;
            builder.AppendLine("SETTINGS");
            builder.AppendLine($"Task context: {ResolveText(task?.context)}");
            builder.AppendLine($"Goals: {(task?.goals == null || task.goals.Length == 0 ? "-" : string.Join("; ", task.goals))}");
            builder.AppendLine($"Avatar speech: speed={ResolveText(avatar?.speakingSpeed)}, accent={ResolveText(avatar?.accent)}, attitude={ResolveText(avatar?.attitude)}");
            builder.AppendLine($"Scene mode: {ResolveText(detail.sceneSnapshot?.scene?.mode)}");
            builder.AppendLine();

            var turns = detail.turns ?? System.Array.Empty<DialogueTurnRecord>();
            foreach (var turn in turns.OrderBy(item => item.sequenceIndex))
            {
                if (turn == null)
                {
                    continue;
                }

                builder.AppendLine(turn.isOpening ? "OPENING" : $"TURN {turn.sequenceIndex}");
                if (!turn.isOpening)
                {
                    builder.AppendLine($"You: {ResolveText(turn.userText)}");
                }
                builder.AppendLine($"Avatar: {ResolveText(turn.assistantText)}");

                var feedback = turn.payload?.correctionFeedback;
                if (feedback != null && feedback.hasFeedback)
                {
                    builder.AppendLine($"Correction ({ResolveText(feedback.errorType)}):");
                    builder.AppendLine($"  Original: {ResolveText(feedback.originalText)}");
                    builder.AppendLine($"  Corrected: {ResolveText(feedback.correctedText)}");
                    var feedbackText = string.IsNullOrWhiteSpace(feedback.recastText)
                        ? feedback.feedbackText
                        : feedback.recastText;
                    builder.AppendLine($"  Feedback: {ResolveText(feedbackText)}");
                }
                else if (!turn.isOpening)
                {
                    builder.AppendLine("Correction: None");
                }

                builder.AppendLine();
            }

            return builder.Length == 0 ? "No dialogue turns were saved." : builder.ToString();
        }

        private static string ResolveText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string FormatHistoryTime(long unixMs)
        {
            if (unixMs <= 0)
            {
                return "Unknown time";
            }

            try
            {
                return System.DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm");
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return "Unknown time";
            }
        }

        private void ScrollHistoryDetails(float delta)
        {
            if (historyDetailScrollRect == null)
            {
                return;
            }

            historyDetailScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                historyDetailScrollRect.verticalNormalizedPosition + delta);
        }

        private void RefreshRequestPanel(bool isVisible)
        {
            if (!isVisible)
            {
                return;
            }

            var isRunning = orchestrator.IsTurnRunning;
            var isRecording = orchestrator.IsSpeechRecording;
            var isTranscribing = orchestrator.CurrentState == SceneTalkState.Transcribing;
            var hasTranscript = !string.IsNullOrWhiteSpace(orchestrator.LastTranscript);
            var hasError = !string.IsNullOrWhiteSpace(orchestrator.LastError);

            if (requestTitleText != null)
            {
                requestTitleText.text = "Scene And Avatar Request";
            }

            if (requestStatusText != null)
            {
                if (isRecording)
                {
                    requestStatusText.text = "Recording your request...";
                }
                else if (isTranscribing)
                {
                    requestStatusText.text = "Transcribing your voice...";
                }
                else if (hasTranscript)
                {
                    requestStatusText.text = "Review the transcript, then confirm.";
                }
                else
                {
                    requestStatusText.text = "Press Listen or hold a trigger to record.";
                }
            }

            if (requestTranscriptText != null)
            {
                requestTranscriptText.text = hasTranscript
                    ? $"Transcript:\n{orchestrator.LastTranscript}"
                    : "Transcript:\n-";
            }

            if (requestErrorText != null)
            {
                requestErrorText.text = hasError ? orchestrator.LastError : string.Empty;
            }

            SetButtonLabel(listenButton, ResolveRequestListenButtonLabel(isRecording, hasTranscript, hasError));
            SetInteractable(listenButton, isRecording || !isRunning);
            SetInteractable(confirmButton, !isRunning && hasTranscript);
        }

        private void RefreshLoadingPanel(bool isVisible)
        {
            if (!isVisible || loadingText == null)
            {
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.HistoryLoading)
            {
                loadingText.text = "Loading conversation history...";
            }
            else if (orchestrator.CurrentState == SceneTalkState.HistoryRestoring)
            {
                loadingText.text = "Restoring scene, avatar, and conversation context...";
            }
            else
            {
                loadingText.text = orchestrator.CurrentState == SceneTalkState.SceneReady
                    ? "Preparing avatar dialogue..."
                    : "Loading scene and avatar...";
            }
        }

        private void RefreshSubtitlePanel(bool isVisible)
        {
            if (!isVisible)
            {
                return;
            }

            var transcript = string.IsNullOrWhiteSpace(orchestrator.LastTranscript)
                ? "-"
                : orchestrator.LastTranscript;
            var reply = orchestrator.LastScenePayload == null || string.IsNullOrWhiteSpace(orchestrator.LastScenePayload.dialogueReply)
                ? "-"
                : orchestrator.LastScenePayload.dialogueReply;
            var hideSubtitles = SceneTalkUserSettingsStore.Current.hideDialogueSubtitles;
            ApplySubtitleLayout(hideSubtitles);

            SetActive(subtitleTextContainer, !hideSubtitles);

            if (playerSubtitleText != null)
            {
                SetActive(playerSubtitleText.gameObject, !hideSubtitles);
                playerSubtitleText.text = $"You: {transcript}";
            }

            if (avatarSubtitleText != null)
            {
                SetActive(avatarSubtitleText.gameObject, !hideSubtitles);
                avatarSubtitleText.text = $"Avatar: {reply}";
            }

            if (experimentDebugText != null)
            {
                var showDebug = orchestrator.ShouldShowExperimentDebug;
                SetActive(experimentDebugText.gameObject, showDebug);
                experimentDebugText.text = showDebug ? orchestrator.ExperimentDebugLabel : string.Empty;
            }

            if (correctionStatusText != null)
            {
                var status = orchestrator.LastCorrectionStatus;
                correctionStatusText.text = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            }

            if (correctionFeedbackText != null)
            {
                var feedbackText = ResolveCorrectionFeedbackText(hideSubtitles);
                SetActive(correctionFeedbackText.gameObject, !string.IsNullOrWhiteSpace(feedbackText));
                correctionFeedbackText.text = feedbackText;
            }

            if (dialogueStatusText != null)
            {
                dialogueStatusText.text = ResolveDialogueStatusText();
            }

            if (dialogueListenButton != null)
            {
                var isRecording = orchestrator.IsSpeechRecording;
                SetActive(dialogueListenButton.gameObject, true);
                SetButtonLabel(dialogueListenButton, isRecording ? "End" : "Speak");
                SetInteractable(dialogueListenButton, isRecording || !orchestrator.IsTurnRunning);
            }
        }

        private void ApplySubtitleLayout(bool hideSubtitles)
        {
            if (subtitlePanelRect != null)
            {
                subtitlePanelRect.anchoredPosition = hideSubtitles
                    ? new Vector2(0f, -194f)
                    : new Vector2(0f, -136f);
                subtitlePanelRect.sizeDelta = hideSubtitles
                    ? new Vector2(760f, 132f)
                    : new Vector2(840f, 248f);
            }

            if (subtitleTextContainerRect != null)
            {
                subtitleTextContainerRect.anchoredPosition = new Vector2(-65f, 48f);
                subtitleTextContainerRect.sizeDelta = new Vector2(650f, 100f);
            }

            if (experimentDebugText != null)
            {
                var debugRect = experimentDebugText.GetComponent<RectTransform>();
                debugRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 48f)
                    : new Vector2(-65f, 111f);
                debugRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 22f)
                    : new Vector2(650f, 22f);
            }

            if (correctionFeedbackText != null)
            {
                var feedbackRect = correctionFeedbackText.GetComponent<RectTransform>();
                feedbackRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 16f)
                    : new Vector2(-65f, -32f);
                feedbackRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(650f, 36f);
            }

            if (correctionStatusText != null)
            {
                var correctionRect = correctionStatusText.GetComponent<RectTransform>();
                correctionRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -14f)
                    : new Vector2(-65f, -77f);
                correctionRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(650f, 28f);
            }

            if (dialogueStatusText != null)
            {
                var statusRect = dialogueStatusText.GetComponent<RectTransform>();
                statusRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -42f)
                    : new Vector2(-65f, -102f);
                statusRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(650f, 28f);
            }

            if (dialogueListenButton != null)
            {
                var buttonRect = dialogueListenButton.GetComponent<RectTransform>();
                buttonRect.anchoredPosition = hideSubtitles
                    ? new Vector2(310f, -32f)
                    : new Vector2(350f, -92f);
                buttonRect.sizeDelta = hideSubtitles
                    ? new Vector2(110f, 38f)
                    : new Vector2(110f, 40f);
            }

        }

        private static string ResolveRequestListenButtonLabel(bool isRecording, bool hasTranscript, bool hasError)
        {
            if (isRecording)
            {
                return "End";
            }

            return hasTranscript || hasError ? "Retry" : "Listen";
        }

        private string ResolveDialogueStatusText()
        {
            if (orchestrator == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(orchestrator.LastError))
            {
                return orchestrator.LastError;
            }

            if (orchestrator.IsTurnRunning)
            {
                if (orchestrator.CurrentState == SceneTalkState.Recording)
                {
                    return "Recording...";
                }

                if (orchestrator.CurrentState == SceneTalkState.Transcribing)
                {
                    return "Transcribing...";
                }

                if (orchestrator.CurrentState == SceneTalkState.Processing)
                {
                    return "Thinking...";
                }

                if (orchestrator.CurrentState == SceneTalkState.CorrectionFeedbackSpeaking)
                {
                    return "Playing feedback...";
                }

                if (orchestrator.CurrentState == SceneTalkState.DialogueSpeaking)
                {
                    return "Avatar speaking...";
                }

                if (orchestrator.CurrentState == SceneTalkState.AvatarSpeaking)
                {
                    return "Speaking...";
                }
            }

            return "Ready for your next line.";
        }

        private string ResolveCorrectionFeedbackText(bool hideSubtitles)
        {
            if (orchestrator == null
                || !orchestrator.LastCorrectionHasFeedback
                || hideSubtitles)
            {
                return string.Empty;
            }

            var feedbackText = orchestrator.LastCorrectionDisplayText;
            if (string.IsNullOrWhiteSpace(feedbackText))
            {
                return string.Empty;
            }

            return $"Correction: {feedbackText}";
        }

        private void Subscribe()
        {
            if (orchestrator == null || isSubscribed)
            {
                return;
            }

            orchestrator.stateChanged.AddListener(OnStateChanged);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (orchestrator == null || !isSubscribed)
            {
                return;
            }

            orchestrator.stateChanged.RemoveListener(OnStateChanged);
            isSubscribed = false;
        }

        private void OnStateChanged(SceneTalkState state)
        {
            Refresh();
        }

        private void OnUserSettingsChanged(SceneTalkUserSettings settings)
        {
            ApplyUserSettings(settings);
            Refresh();
        }

        private void OpenSettings()
        {
            orchestrator?.OpenSettings();
        }

        private void ExitCurrentView()
        {
            if (orchestrator == null)
            {
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.Settings)
            {
                orchestrator.CloseSettings();
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.HistoryList
                || orchestrator.CurrentState == SceneTalkState.HistoryDetail
                || orchestrator.CurrentState == SceneTalkState.HistoryDeleteConfirm
                || orchestrator.CurrentState == SceneTalkState.HistoryLoading
                || orchestrator.CurrentState == SceneTalkState.HistoryError)
            {
                orchestrator.BackFromHistory();
                return;
            }

            orchestrator.ReturnToInitialMenu();
        }

        private void CaptureBaseFontSizes(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && !baseFontSizes.ContainsKey(text))
                {
                    baseFontSizes.Add(text, text.fontSize);
                }
            }
        }

        private void ApplyUserSettings(SceneTalkUserSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            foreach (var pair in baseFontSizes)
            {
                if (pair.Key != null)
                {
                    var scaledSize = Mathf.Max(1f, pair.Value * settings.fontScale);
                    pair.Key.fontSize = scaledSize;
                    if (pair.Key.enableAutoSizing)
                    {
                        pair.Key.fontSizeMax = scaledSize;
                        pair.Key.fontSizeMin = Mathf.Max(10f, scaledSize * 0.72f);
                    }
                }
            }

            interactionBootstrap?.ApplyUserSettings(settings);
        }

        private void QuitApplication()
        {
            if (interactionBootstrap != null)
            {
                interactionBootstrap.QuitApplication();
                return;
            }

            Application.Quit();
        }

        private void ClearCanvasChildren()
        {
            for (var i = worldCanvas.transform.childCount - 1; i >= 0; i--)
            {
                DestroyRuntimeOrImmediate(worldCanvas.transform.GetChild(i).gameObject);
            }
        }

        private void ConfigureCanvasRect()
        {
            var rectTransform = worldCanvas.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(860f, 520f);
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var image = panel.AddComponent<Image>();
            image.color = color;

            var rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            return panel;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color,
            bool autoFitHeight = false)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = ToTmpAlignment(alignment);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;

            if (autoFitHeight)
            {
                label.overflowMode = TextOverflowModes.Overflow;
                var fitter = textObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            else
            {
                label.overflowMode = TextOverflowModes.Truncate;
            }

            var rectTransform = label.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            return label;
        }

        private static void ConfigureDialogueText(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.lineSpacing = 0.92f;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Max(10f, label.fontSize * 0.72f);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = color;

            var button = buttonObject.AddComponent<Button>();
            var rectTransform = button.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            var labelText = CreateText(buttonObject.transform, "Label", label, Vector2.zero, size, 22, TextAnchor.MiddleCenter, Color.white);
            labelText.raycastTarget = false;
            return button;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private static string ResolveCorrectionSourceDisplayName(string provider)
        {
            return string.Equals(
                provider,
                ExperimentConditionManager.AssistantAgentProvider,
                System.StringComparison.OrdinalIgnoreCase)
                ? "Assistant Agent"
                : "Dialogue Avatar";
        }

        private static string ResolveCorrectionStyleDisplayName(string style)
        {
            return string.Equals(
                style,
                ExperimentConditionManager.RecastStyle,
                System.StringComparison.OrdinalIgnoreCase)
                ? "Recast"
                : "Explicit";
        }

        private static string ResolveCorrectionAppearanceDisplayName(string embodiment)
        {
            if (string.Equals(
                    embodiment,
                    ExperimentConditionManager.AudioOnlyAssistantEmbodiment,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return "Voice Only";
            }

            return string.Equals(
                embodiment,
                ExperimentConditionManager.HumanoidAssistantEmbodiment,
                System.StringComparison.OrdinalIgnoreCase)
                ? "Third Person"
                : "Little Orb";
        }

        private static void DestroyRuntimeOrImmediate(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
