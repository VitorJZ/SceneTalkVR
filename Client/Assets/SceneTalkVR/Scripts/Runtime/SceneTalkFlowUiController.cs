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
        private sealed class ExperimentRecordEntry
        {
            public bool isConversation;
            public string id;
            public string label;
        }

        private const string FlowRootName = "SceneTalkVR Flow UI";
        private static readonly Vector2 ExitButtonInset = new Vector2(-18f, -18f);
        private static readonly Vector2 ExitButtonSize = new Vector2(110f, 44f);
        private static readonly Color ExitButtonColor = new Color(0.58f, 0.18f, 0.18f, 1f);
        private static readonly Vector2 TaskGoalPanelPosition = new Vector2(-245f, 135f);
        private static readonly Vector2 TaskGoalPanelSize = new Vector2(360f, 230f);
        private const float DialoguePanelCenterX = 0f;
        private const float DialoguePanelWidth = 840f;
        private const float DialogueContentCenterX = -65f;
        private const float DialogueContentWidth = 650f;
        private const float DialogueButtonCenterX = 350f;

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
        private GameObject experimentExitConfirmPanel;
        private GameObject experimentHistoryListPanel;
        private GameObject experimentHistoryActionsPanel;
        private GameObject experimentHistoryRecordPanel;
        private GameObject experimentHistoryConversationPanel;
        private GameObject experimentHistoryQuestionnairePanel;
        private GameObject experimentHistoryDeletePanel;
        private GameObject experimentHistoryErrorPanel;
        private GameObject settingsGeneralGroup;
        private GameObject requestPanel;
        private GameObject taskSelectionPanel;
        private GameObject loadingPanel;
        private GameObject subtitlePanel;
        private GameObject subtitleTextContainer;
        private GameObject exitButtonObject;
        private GameObject taskGoalPanel;
        private GameObject demoBanner;
        private GameObject demoStatusPanel;
        private GameObject demoRankingPanel;
        private GameObject rehearsalWaitingPanel;
        private GameObject formalModeSelectionPanel;
        private GameObject sessionNotPreparedPanel;
        private PilotCollectionParticipantUi pilotCollectionUi;

        private Button pilotButton;
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

        private readonly List<Button> taskButtons = new List<Button>();
        private readonly List<ExperimentTaskDefinition> taskButtonDefinitions = new List<ExperimentTaskDefinition>();
        private readonly Dictionary<FormalConditionCode, Button> formalModeButtons = new Dictionary<FormalConditionCode, Button>();
        private readonly Dictionary<FormalConditionCode, TMP_Text> formalModeStatusTexts = new Dictionary<FormalConditionCode, TMP_Text>();
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
        private Button experimentPilotButton;
        private Button experimentFormalButton;
        private Button experimentExitConfirmButton;
        private Button experimentExitCancelButton;
        private readonly Button[] experimentHistoryRowButtons = new Button[ExperimentHistoryService.DefaultPageSize];
        private readonly string[] experimentHistoryRowIds = new string[ExperimentHistoryService.DefaultPageSize];
        private Button experimentHistoryPreviousButton;
        private Button experimentHistoryNextButton;
        private Button experimentHistoryContinueButton;
        private Button experimentHistoryViewButton;
        private Button experimentHistoryDeleteButton;
        private readonly Button[] experimentRecordEntryButtons = new Button[5];
        private Button experimentRecordPreviousButton;
        private Button experimentRecordNextButton;
        private Button experimentHistoryDeleteConfirmButton;
        private Button experimentHistoryDeleteCancelButton;

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
        private TMP_Text homeExperimentMessageText;
        private TMP_Text experimentHistoryEmptyText;
        private TMP_Text experimentHistoryPageText;
        private TMP_Text experimentHistoryActionsSummaryText;
        private TMP_Text experimentHistoryRecordText;
        private TMP_Text experimentRecordEntriesPageText;
        private TMP_Text experimentHistoryConversationSummaryText;
        private TMP_Text experimentHistoryConversationBodyText;
        private TMP_Text experimentHistoryQuestionnaireText;
        private TMP_Text experimentHistoryDeleteMessageText;
        private TMP_Text experimentHistoryErrorText;
        private ScrollRect historyDetailScrollRect;
        private RectTransform historyDetailContentRect;
        private ScrollRect experimentRecordScrollRect;
        private RectTransform experimentRecordContentRect;
        private ScrollRect experimentConversationScrollRect;
        private RectTransform experimentConversationContentRect;
        private ScrollRect experimentQuestionnaireScrollRect;
        private RectTransform experimentQuestionnaireContentRect;
        private string lastRenderedHistorySessionId;
        private string lastRenderedExperimentRecordId;
        private string lastRenderedExperimentConversationId;
        private string lastRenderedExperimentQuestionnaireId;
        private readonly List<ExperimentRecordEntry> experimentRecordEntries = new List<ExperimentRecordEntry>();
        private int experimentRecordEntryPage;
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
        private TMP_Text taskGoalText;
        private TMP_Text demoBannerText;
        private TMP_Text demoStatusText;
        private TMP_Text demoRankingText;
        private GoalProgressTracker subscribedGoalTracker;
        private bool goalPanelVisible = true;
        private bool rehearsalFinalRankingVisible;
        private bool sessionPreparationBlocked;
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

        private void LateUpdate()
        {
            BringExitButtonToFront();
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

            pilotCollectionUi?.ResetForCanvasRebuild();
            ClearCanvasChildren();
            baseFontSizes.Clear();
            ConfigureCanvasRect();

            var root = new GameObject(FlowRootName).transform;
            root.SetParent(worldCanvas.transform, false);

            demoBanner = CreatePanel(root, "EditorDemoBanner", new Vector2(0f, 278f), new Vector2(700f, 34f), new Color(0.65f, 0.12f, 0.08f, 0.92f));
            demoBannerText = CreateText(demoBanner.transform, "EditorDemoBannerText", "EDITOR DEMONSTRATION — NOT PARTICIPANT DATA", Vector2.zero, new Vector2(680f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
            demoStatusPanel = CreatePanel(root, "EditorDemoStatusPanel", new Vector2(382f, 72f), new Vector2(270f, 190f), new Color(0.05f, 0.06f, 0.08f, 0.82f));
            demoStatusText = CreateText(demoStatusPanel.transform, "EditorDemoStatusText", string.Empty, Vector2.zero, new Vector2(246f, 170f), 14, TextAnchor.UpperLeft, new Color(1f, .86f, .42f, 1f));
            demoRankingPanel = CreatePanel(root, "EditorDemoRankingPreview", Vector2.zero, new Vector2(520f, 330f), new Color(0.03f, 0.04f, 0.07f, 0.94f));
            demoRankingText = CreateText(demoRankingPanel.transform, "EditorDemoRankingText", string.Empty, Vector2.zero, new Vector2(480f, 290f), 22, TextAnchor.MiddleCenter, Color.white);
            demoRankingPanel.SetActive(false);

            rehearsalWaitingPanel = CreatePanel(root, "RehearsalWaitingPanel", Vector2.zero, new Vector2(620f, 260f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(rehearsalWaitingPanel.transform, "Title", "Welcome to SceneTalkVR", new Vector2(0f, 62f), new Vector2(560f, 52f), 30, TextAnchor.MiddleCenter, Color.white);
            CreateText(rehearsalWaitingPanel.transform, "Instruction", "Please wait while the experimenter prepares your next task.", new Vector2(0f, -20f), new Vector2(540f, 90f), 21, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));

            formalModeSelectionPanel = CreatePanel(root, "FormalModeSelectionPanel", Vector2.zero, new Vector2(900f, 520f), new Color(0.04f, 0.05f, 0.07f, 0.96f));
            CreateText(formalModeSelectionPanel.transform, "Title", "Choose a Feedback Mode", new Vector2(0f, 220f), new Vector2(800f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            CreateText(formalModeSelectionPanel.transform, "Instruction", "Choose any available mode. Your task has already been assigned.", new Vector2(0f, 182f), new Vector2(800f, 30f), 17, TextAnchor.MiddleCenter, new Color(.78f, .86f, 1f, 1f));
            BuildFormalModeButtons();

            sessionNotPreparedPanel = CreatePanel(root, "SessionNotPreparedPanel", Vector2.zero, new Vector2(660f, 250f), new Color(0.04f, 0.05f, 0.07f, 0.97f));
            CreateText(sessionNotPreparedPanel.transform, "Title", "Experiment Session Not Prepared", new Vector2(0f, 62f), new Vector2(590f, 44f), 27, TextAnchor.MiddleCenter, Color.white);
            CreateText(sessionNotPreparedPanel.transform, "Instruction", "The experiment session has not been prepared.\nPlease contact the experimenter.", new Vector2(0f, -20f), new Vector2(590f, 92f), 21, TextAnchor.MiddleCenter, new Color(.85f, .9f, 1f, 1f));
            CreateButton(sessionNotPreparedPanel.transform, "SessionNotPreparedBackButton", "Back", new Vector2(0f, -92f), new Vector2(160f, 44f), new Color(.24f, .36f, .42f, 1f))
                .onClick.AddListener(() => { sessionPreparationBlocked = false; Refresh(); });

            mainMenuPanel = CreatePanel(root, "InitialPanel", new Vector2(0f, 0f), new Vector2(430f, 600f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            CreateText(mainMenuPanel.transform, "Title", "SceneTalkVR", new Vector2(0f, 255f), new Vector2(360f, 54f), 34, TextAnchor.MiddleCenter, Color.white);
            experimentPilotButton = CreateButton(mainMenuPanel.transform, "PilotExperimentButton", "Pilot Experiment", new Vector2(0f, 172f), new Vector2(270f, 50f), new Color(0.18f, 0.48f, 0.58f, 1f));
            experimentFormalButton = CreateButton(mainMenuPanel.transform, "FormalExperimentButton", "Formal Experiment", new Vector2(0f, 110f), new Vector2(270f, 50f), new Color(0.16f, 0.38f, 0.68f, 1f));
            pilotButton = CreateButton(mainMenuPanel.transform, "ExperimentHistoryButton", "Experiment History", new Vector2(0f, 48f), new Vector2(270f, 50f), new Color(0.18f, 0.48f, 0.58f, 1f));
            historyButton = CreateButton(mainMenuPanel.transform, "HistoryButton", "History", new Vector2(0f, -14f), new Vector2(270f, 50f), new Color(0.24f, 0.36f, 0.42f, 1f));
            settingsButton = CreateButton(mainMenuPanel.transform, "SettingsButton", "Settings", new Vector2(0f, -76f), new Vector2(270f, 50f), new Color(0.24f, 0.36f, 0.42f, 1f));
            quitButton = CreateButton(mainMenuPanel.transform, "QuitButton", "Quit", new Vector2(0f, -138f), new Vector2(270f, 50f), ExitButtonColor);
            homeExperimentMessageText = CreateText(mainMenuPanel.transform, "ExperimentMessage", string.Empty, new Vector2(0f, -218f), new Vector2(360f, 74f), 16, TextAnchor.MiddleCenter, new Color(1f, .58f, .42f, 1f));

            BuildExperimentPanels(root);

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

            BuildTaskButtons();

            loadingPanel = CreatePanel(root, "LoadingPanel", new Vector2(0f, 0f), new Vector2(540f, 220f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            loadingText = CreateText(loadingPanel.transform, "LoadingText", "Loading scene and avatar...", new Vector2(0f, 0f), new Vector2(480f, 80f), 26, TextAnchor.MiddleCenter, Color.white);

            subtitlePanel = CreatePanel(root, "SubtitlePanel", new Vector2(DialoguePanelCenterX, -136f), new Vector2(DialoguePanelWidth, 248f), new Color(0f, 0f, 0f, 0.62f));
            subtitlePanel.AddComponent<RectMask2D>();
            subtitlePanelRect = subtitlePanel.GetComponent<RectTransform>();
            
            subtitleTextContainer = new GameObject("TextContainer");
            subtitleTextContainer.transform.SetParent(subtitlePanel.transform, false);
            subtitleTextContainerRect = subtitleTextContainer.AddComponent<RectTransform>();
            subtitleTextContainerRect.anchoredPosition = new Vector2(DialogueContentCenterX, 48f);
            subtitleTextContainerRect.sizeDelta = new Vector2(DialogueContentWidth, 100f);

            playerSubtitleText = CreateText(subtitleTextContainer.transform, "PlayerSubtitle", "You: -", new Vector2(0f, 36f), new Vector2(DialogueContentWidth, 28f), 18, TextAnchor.UpperLeft, new Color(0.45f, 0.9f, 1f, 1f));
            avatarSubtitleText = CreateText(subtitleTextContainer.transform, "AvatarSubtitle", "Avatar: -", new Vector2(0f, -14f), new Vector2(DialogueContentWidth, 72f), 19, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.36f, 1f));
            ConfigureDialogueText(playerSubtitleText);
            ConfigureDialogueText(avatarSubtitleText);

            experimentDebugText = CreateText(subtitlePanel.transform, "ExperimentDebug", string.Empty, new Vector2(DialogueContentCenterX, 111f), new Vector2(DialogueContentWidth, 22f), 13, TextAnchor.MiddleLeft, new Color(0.72f, 0.8f, 0.86f, 1f));
            correctionFeedbackText = CreateText(subtitlePanel.transform, "CorrectionFeedback", string.Empty, new Vector2(DialogueContentCenterX, -32f), new Vector2(DialogueContentWidth, 36f), 16, TextAnchor.UpperLeft, new Color(0.78f, 0.95f, 0.74f, 1f));
            correctionStatusText = CreateText(subtitlePanel.transform, "CorrectionStatus", string.Empty, new Vector2(DialogueContentCenterX, -77f), new Vector2(DialogueContentWidth, 28f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            dialogueStatusText = CreateText(subtitlePanel.transform, "DialogueStatus", "Ready", new Vector2(DialogueContentCenterX, -102f), new Vector2(DialogueContentWidth, 28f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            ConfigureDialogueText(experimentDebugText);
            ConfigureDialogueText(correctionFeedbackText);
            ConfigureDialogueText(correctionStatusText);
            ConfigureDialogueText(dialogueStatusText);

            dialogueListenButton = CreateButton(subtitlePanel.transform, "DialogueListenButton", "Speak", new Vector2(DialogueButtonCenterX, -92f), new Vector2(110f, 40f), new Color(0.12f, 0.52f, 0.38f, 1f));

            taskGoalPanel = CreatePanel(root, "ReadOnlyTaskGoalPanel", TaskGoalPanelPosition, TaskGoalPanelSize, new Color(0.03f, 0.04f, 0.06f, 0.84f));
            CreateText(taskGoalPanel.transform, "Title", "Task Goals", new Vector2(0f, 90f), new Vector2(320f, 34f), 22, TextAnchor.MiddleCenter, Color.white);
            taskGoalText = CreateText(taskGoalPanel.transform, "GoalStateText", string.Empty, new Vector2(0f, -18f), new Vector2(320f, 160f), 16, TextAnchor.UpperLeft, new Color(0.86f, 0.92f, 1f, 1f));
            ConfigureDialogueText(taskGoalText);
            
            pilotCollectionUi = GetComponent<PilotCollectionParticipantUi>() ?? gameObject.AddComponent<PilotCollectionParticipantUi>();
            pilotCollectionUi.Configure(worldCanvas, orchestrator);

            exitButton = CreateGlobalExitButton();
            exitButtonObject = exitButton.gameObject;

            if (ExperimentRuntimePlatform.IsPicoDeviceValidation)
            {
                var manager = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
                Debug.Log("[ExperimentRuntime] PICO device validation UI ready. "
                    + $"qualification=Rehearsal; dataOrigin=rehearsal; collectionEligible=false; "
                    + $"profile=pico_device_validation; protocolBound={manager?.DeviceValidationProtocol != null}; "
                    + $"resourcesBound={manager?.DeviceValidationResources != null}; voiceCatalogBound={manager?.DeviceValidationVoiceCatalog != null}; "
                    + $"deploymentCatalogBound={manager?.DeviceValidationDeploymentCatalog != null}", this);
            }

            BindButtons();
            CaptureBaseFontSizes(worldCanvas.transform);
            ApplyUserSettings(SceneTalkUserSettingsStore.Current);
        }

        private void BuildExperimentPanels(Transform root)
        {
            experimentExitConfirmPanel = CreatePanel(root, "ExperimentExitConfirmPanel", Vector2.zero, new Vector2(680f, 320f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentExitConfirmPanel.transform, "Title", "Exit Experiment?", new Vector2(0f, 104f), new Vector2(580f, 48f), 30, TextAnchor.MiddleCenter, Color.white);
            CreateText(experimentExitConfirmPanel.transform, "Message", "This experiment is not complete. Your history will be kept, and you can continue it later from Experiment History.", new Vector2(0f, 25f), new Vector2(580f, 100f), 19, TextAnchor.MiddleCenter, new Color(.84f, .9f, 1f, 1f));
            experimentExitCancelButton = CreateButton(experimentExitConfirmPanel.transform, "ContinueExperimentButton", "Continue Experiment", new Vector2(-135f, -102f), new Vector2(230f, 48f), new Color(.12f, .52f, .38f, 1f));
            experimentExitConfirmButton = CreateButton(experimentExitConfirmPanel.transform, "ConfirmExitExperimentButton", "Exit to Home", new Vector2(135f, -102f), new Vector2(210f, 48f), ExitButtonColor);

            experimentHistoryListPanel = CreatePanel(root, "ExperimentHistoryListPanel", Vector2.zero, new Vector2(860f, 520f), new Color(.04f, .05f, .07f, .96f));
            CreateText(experimentHistoryListPanel.transform, "Title", "Experiment History", new Vector2(0f, 220f), new Vector2(680f, 46f), 30, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryEmptyText = CreateText(experimentHistoryListPanel.transform, "Empty", "No experiment history yet.", new Vector2(0f, 10f), new Vector2(680f, 60f), 22, TextAnchor.MiddleCenter, new Color(.75f, .82f, .88f, 1f));
            for (var i = 0; i < experimentHistoryRowButtons.Length; i++)
            {
                experimentHistoryRowButtons[i] = CreateButton(experimentHistoryListPanel.transform, "ExperimentHistoryRow" + (i + 1), string.Empty,
                    new Vector2(0f, 150f - i * 70f), new Vector2(720f, 58f), new Color(.14f, .28f, .4f, 1f));
                var label = experimentHistoryRowButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.alignment = TextAlignmentOptions.Left;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 13f;
                    label.fontSizeMax = 18f;
                }
            }
            experimentHistoryPreviousButton = CreateButton(experimentHistoryListPanel.transform, "ExperimentHistoryPreviousButton", "Previous", new Vector2(-250f, -225f), new Vector2(150f, 44f), new Color(.24f, .36f, .42f, 1f));
            experimentHistoryPageText = CreateText(experimentHistoryListPanel.transform, "Page", "Page 1 / 1", new Vector2(0f, -225f), new Vector2(200f, 40f), 18, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryNextButton = CreateButton(experimentHistoryListPanel.transform, "ExperimentHistoryNextButton", "Next", new Vector2(250f, -225f), new Vector2(150f, 44f), new Color(.24f, .36f, .42f, 1f));

            experimentHistoryActionsPanel = CreatePanel(root, "ExperimentHistoryActionsPanel", Vector2.zero, new Vector2(760f, 400f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryActionsPanel.transform, "Title", "Experiment Record", new Vector2(0f, 155f), new Vector2(650f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryActionsSummaryText = CreateText(experimentHistoryActionsPanel.transform, "Summary", string.Empty, new Vector2(0f, 66f), new Vector2(650f, 120f), 18, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));
            experimentHistoryContinueButton = CreateButton(experimentHistoryActionsPanel.transform, "ContinueExperimentRecordButton", "Continue", new Vector2(-220f, -102f), new Vector2(180f, 48f), new Color(.12f, .52f, .38f, 1f));
            experimentHistoryViewButton = CreateButton(experimentHistoryActionsPanel.transform, "ViewExperimentRecordButton", "View Record", new Vector2(0f, -102f), new Vector2(180f, 48f), new Color(.16f, .38f, .68f, 1f));
            experimentHistoryDeleteButton = CreateButton(experimentHistoryActionsPanel.transform, "DeleteExperimentRecordButton", "Delete", new Vector2(220f, -102f), new Vector2(180f, 48f), ExitButtonColor);

            experimentHistoryRecordPanel = CreatePanel(root, "ExperimentHistoryRecordPanel", Vector2.zero, new Vector2(950f, 570f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryRecordPanel.transform, "Title", "Experiment Record Details", new Vector2(0f, 252f), new Vector2(780f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryRecordText = CreateScrollableText(experimentHistoryRecordPanel.transform, "ExperimentRecord", new Vector2(0f, 135f), new Vector2(830f, 180f), out experimentRecordScrollRect, out experimentRecordContentRect);
            CreateText(experimentHistoryRecordPanel.transform, "EntriesTitle", "Conversations and Questionnaires", new Vector2(0f, 28f), new Vector2(760f, 30f), 18, TextAnchor.MiddleCenter, new Color(.78f, .88f, 1f, 1f));
            for (var i = 0; i < experimentRecordEntryButtons.Length; i++)
            {
                var index = i;
                experimentRecordEntryButtons[i] = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordEntry" + (i + 1), string.Empty,
                    new Vector2(0f, -8f - i * 43f), new Vector2(790f, 36f), new Color(.14f, .28f, .4f, 1f));
                var label = experimentRecordEntryButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) { label.alignment = TextAlignmentOptions.Left; label.fontSize = 15f; }
                experimentRecordEntryButtons[i].onClick.AddListener(() => OpenExperimentRecordEntry(index));
            }
            experimentRecordPreviousButton = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordPreviousButton", "Previous", new Vector2(-220f, -252f), new Vector2(140f, 38f), new Color(.24f, .36f, .42f, 1f));
            experimentRecordEntriesPageText = CreateText(experimentHistoryRecordPanel.transform, "EntriesPage", "Page 1 / 1", new Vector2(0f, -252f), new Vector2(180f, 34f), 16, TextAnchor.MiddleCenter, Color.white);
            experimentRecordNextButton = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordNextButton", "Next", new Vector2(220f, -252f), new Vector2(140f, 38f), new Color(.24f, .36f, .42f, 1f));

            experimentHistoryConversationPanel = CreatePanel(root, "ExperimentHistoryConversationDetailPanel", Vector2.zero, new Vector2(900f, 550f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryConversationPanel.transform, "Title", "Conversation Details", new Vector2(0f, 235f), new Vector2(760f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryConversationSummaryText = CreateText(experimentHistoryConversationPanel.transform, "Summary", string.Empty, new Vector2(0f, 174f), new Vector2(760f, 78f), 16, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));
            experimentHistoryConversationBodyText = CreateScrollableText(experimentHistoryConversationPanel.transform, "ExperimentConversation", new Vector2(0f, -40f), new Vector2(790f, 330f), out experimentConversationScrollRect, out experimentConversationContentRect);

            experimentHistoryQuestionnairePanel = CreatePanel(root, "ExperimentHistoryQuestionnaireDetailPanel", Vector2.zero, new Vector2(900f, 550f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryQuestionnairePanel.transform, "Title", "Questionnaire Details", new Vector2(0f, 235f), new Vector2(760f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryQuestionnaireText = CreateScrollableText(experimentHistoryQuestionnairePanel.transform, "ExperimentQuestionnaire", new Vector2(0f, -5f), new Vector2(800f, 410f), out experimentQuestionnaireScrollRect, out experimentQuestionnaireContentRect);

            experimentHistoryDeletePanel = CreatePanel(root, "ExperimentHistoryDeleteConfirmPanel", Vector2.zero, new Vector2(650f, 300f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentHistoryDeletePanel.transform, "Title", "Delete Experiment?", new Vector2(0f, 98f), new Vector2(560f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryDeleteMessageText = CreateText(experimentHistoryDeletePanel.transform, "Message", string.Empty, new Vector2(0f, 24f), new Vector2(560f, 88f), 18, TextAnchor.MiddleCenter, new Color(.84f, .9f, 1f, 1f));
            experimentHistoryDeleteCancelButton = CreateButton(experimentHistoryDeletePanel.transform, "CancelExperimentDeleteButton", "Cancel", new Vector2(-115f, -93f), new Vector2(170f, 46f), new Color(.24f, .36f, .42f, 1f));
            experimentHistoryDeleteConfirmButton = CreateButton(experimentHistoryDeletePanel.transform, "ConfirmExperimentDeleteButton", "Delete", new Vector2(115f, -93f), new Vector2(170f, 46f), ExitButtonColor);

            experimentHistoryErrorPanel = CreatePanel(root, "ExperimentHistoryErrorPanel", Vector2.zero, new Vector2(680f, 270f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentHistoryErrorPanel.transform, "Title", "Experiment History Error", new Vector2(0f, 88f), new Vector2(580f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryErrorText = CreateText(experimentHistoryErrorPanel.transform, "Message", string.Empty, new Vector2(0f, -5f), new Vector2(570f, 120f), 18, TextAnchor.MiddleCenter, new Color(1f, .55f, .42f, 1f));
        }

        private void BindButtons()
        {
            if (pilotButton != null)
            {
                pilotButton.onClick.RemoveAllListeners();
                pilotButton.onClick.AddListener(OpenExperimentHistory);
            }

            if (experimentPilotButton != null)
            {
                experimentPilotButton.onClick.RemoveAllListeners();
                experimentPilotButton.onClick.AddListener(EnterPilotExperiment);
            }

            if (experimentFormalButton != null)
            {
                experimentFormalButton.onClick.RemoveAllListeners();
                experimentFormalButton.onClick.AddListener(EnterFormalExperiment);
            }

            if (experimentExitCancelButton != null)
            {
                experimentExitCancelButton.onClick.RemoveAllListeners();
                experimentExitCancelButton.onClick.AddListener(() => GetExperimentCoordinator()?.CancelLeaveExperiment());
            }

            if (experimentExitConfirmButton != null)
            {
                experimentExitConfirmButton.onClick.RemoveAllListeners();
                experimentExitConfirmButton.onClick.AddListener(() => GetExperimentCoordinator()?.ConfirmLeaveExperiment());
            }

            if (experimentHistoryPreviousButton != null)
            {
                experimentHistoryPreviousButton.onClick.RemoveAllListeners();
                experimentHistoryPreviousButton.onClick.AddListener(() => GetExperimentCoordinator()?.OpenPreviousExperimentHistoryPage());
            }

            if (experimentHistoryNextButton != null)
            {
                experimentHistoryNextButton.onClick.RemoveAllListeners();
                experimentHistoryNextButton.onClick.AddListener(() => GetExperimentCoordinator()?.OpenNextExperimentHistoryPage());
            }

            if (experimentHistoryContinueButton != null)
            {
                experimentHistoryContinueButton.onClick.RemoveAllListeners();
                experimentHistoryContinueButton.onClick.AddListener(() => GetExperimentCoordinator()?.ContinueSelectedExperiment());
            }

            if (experimentHistoryViewButton != null)
            {
                experimentHistoryViewButton.onClick.RemoveAllListeners();
                experimentHistoryViewButton.onClick.AddListener(() =>
                {
                    experimentRecordEntryPage = 0;
                    GetExperimentCoordinator()?.ViewSelectedExperiment();
                });
            }

            if (experimentHistoryDeleteButton != null)
            {
                experimentHistoryDeleteButton.onClick.RemoveAllListeners();
                experimentHistoryDeleteButton.onClick.AddListener(() => GetExperimentCoordinator()?.RequestDeleteSelectedExperiment());
            }

            if (experimentHistoryDeleteCancelButton != null)
            {
                experimentHistoryDeleteCancelButton.onClick.RemoveAllListeners();
                experimentHistoryDeleteCancelButton.onClick.AddListener(() => GetExperimentCoordinator()?.BackFromExperimentHistory());
            }

            if (experimentHistoryDeleteConfirmButton != null)
            {
                experimentHistoryDeleteConfirmButton.onClick.RemoveAllListeners();
                experimentHistoryDeleteConfirmButton.onClick.AddListener(() => GetExperimentCoordinator()?.ConfirmDeleteSelectedExperiment());
            }

            if (experimentRecordPreviousButton != null)
            {
                experimentRecordPreviousButton.onClick.RemoveAllListeners();
                experimentRecordPreviousButton.onClick.AddListener(() =>
                {
                    experimentRecordEntryPage = Mathf.Max(0, experimentRecordEntryPage - 1);
                    RefreshExperimentHistoryRecord(true);
                });
            }

            if (experimentRecordNextButton != null)
            {
                experimentRecordNextButton.onClick.RemoveAllListeners();
                experimentRecordNextButton.onClick.AddListener(() =>
                {
                    experimentRecordEntryPage++;
                    RefreshExperimentHistoryRecord(true);
                });
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

            for (var i = 0; i < taskButtons.Count && i < taskButtonDefinitions.Count; i++)
            {
                var button = taskButtons[i];
                var taskId = taskButtonDefinitions[i].taskId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => orchestrator?.LoadAssignedTask(taskId));
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
                exitButton.onClick.AddListener(HandleGlobalExit);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitApplication);
            }
        }

        public IReadOnlyList<ExperimentTaskDefinition> CurrentTaskOptions => taskButtonDefinitions;

        private ExperimentSessionCoordinator GetExperimentCoordinator()
        {
            var coordinator = ExperimentSessionCoordinator.Active
                ?? FindFirstObjectByType<ExperimentSessionCoordinator>(FindObjectsInactive.Include);
            if (coordinator != null) return coordinator;
            var manager = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            if (manager == null) return null;
            coordinator = manager.GetComponent<ExperimentSessionCoordinator>()
                ?? manager.gameObject.AddComponent<ExperimentSessionCoordinator>();
            coordinator.Configure(manager, orchestrator);
            return coordinator;
        }

        private void OpenExperimentHistory()
        {
            GetExperimentCoordinator()?.OpenExperimentHistory();
        }

        private void EnterPilotExperiment()
        {
            var coordinator = GetExperimentCoordinator();
            if (coordinator == null) return;
            if (homeExperimentMessageText != null) homeExperimentMessageText.text = string.Empty;
            if (!coordinator.StartNewExperiment(ExperimentKind.Pilot, out var error))
                homeExperimentMessageText.text = HumanizeExperimentError(error);
        }

        private void EnterFormalExperiment()
        {
            var coordinator = GetExperimentCoordinator();
            if (coordinator == null) return;
            if (homeExperimentMessageText != null) homeExperimentMessageText.text = string.Empty;
            if (!coordinator.StartNewExperiment(ExperimentKind.Formal, out var error))
                homeExperimentMessageText.text = HumanizeExperimentError(error);
        }

        public void ShowDeveloperTaskSelectionForQa()
        {
            sessionPreparationBlocked = false;
            orchestrator?.StartPractice();
        }

        private void HandleParticipantStart()
        {
            var collection = EditorCollectionSessionCoordinator.Active;
            if (collection != null && collection.IsArmed)
            {
                sessionPreparationBlocked = false;
                if (!collection.BeginParticipantFlow(out var collectionError))
                {
                    sessionPreparationBlocked = true;
                    Debug.LogError("[EditorCollection] Participant Start blocked: " + collectionError, this);
                }
                Refresh();
                return;
            }

            // An unarmed start is a rehearsal. On PICO it is explicitly a device-validation
            // session and remains ineligible for participant collection.
            // This keeps the four-condition participant UI usable without requiring an
            // operator-window pre-step, while preserving the armed collection boundary.
            var rehearsal = RehearsalSessionCoordinator.Active
                ?? FindFirstObjectByType<RehearsalSessionCoordinator>(FindObjectsInactive.Include);
            rehearsal ??= EnsureRuntimeRehearsalCoordinator();
            var token = System.Guid.NewGuid().ToString("N");
            var deviceValidation = ExperimentRuntimePlatform.IsPicoDeviceValidation;
            var sessionPrefix = deviceValidation ? "pico-val" : "editor-manual";
            var participantId = deviceValidation ? "PICO-VAL" : "EDITOR-MANUAL";
            var sessionId = $"{sessionPrefix}-{System.DateTime.UtcNow:yyyyMMdd-HHmmss}-{token.Substring(0, 8)}";
            var error = rehearsal == null ? "rehearsal_session_coordinator_missing" : string.Empty;
            sessionPreparationBlocked = false;
            if (rehearsal == null || !rehearsal.CreateFormalSession(participantId, sessionId, out error))
            {
                sessionPreparationBlocked = true;
                Debug.LogError("[Rehearsal] Automatic Formal rehearsal failed: " + error, this);
            }
            else
            {
                Debug.Log("[ExperimentRuntime] Formal rehearsal created. "
                    + $"deviceValidation={deviceValidation}; dataOrigin=rehearsal; collectionEligible=false; sessionId={sessionId}", this);
            }
            Refresh();
        }

        internal static RehearsalSessionCoordinator EnsureRuntimeRehearsalCoordinator()
        {
            var manager = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            if (manager == null) return null;
            var coordinator = manager.GetComponent<RehearsalSessionCoordinator>()
                ?? manager.gameObject.AddComponent<RehearsalSessionCoordinator>();
            var protocol = manager.DeviceValidationProtocol;
            var resources = manager.DeviceValidationResources;
            var voices = manager.DeviceValidationVoiceCatalog;
            var deployments = manager.DeviceValidationDeploymentCatalog;
#if UNITY_EDITOR
            const string root = "Assets/SceneTalkVR/ExperimentProtocol/";
            protocol ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ExperimentV11RehearsalProtocol>(root + "ExperimentV11RehearsalProtocol.asset");
            resources ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ExperimentV11RehearsalResourceCatalog>(root + "ExperimentV11RehearsalResources.asset");
            voices ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(root + "ExperimentV11RehearsalVoiceProfileCatalog.asset");
            deployments ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(root + "ExperimentV11RehearsalDeploymentCatalog.asset");
#endif
            coordinator.Configure(protocol, resources, voices, deployments);
            return coordinator;
        }

        private void BuildTaskButtons()
        {
            taskButtons.Clear();
            taskButtonDefinitions.Clear();
            var manager = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            var catalog = manager == null ? null : manager.TaskCatalog;
            if (catalog == null) return;
            var phase = manager.ExperimentProtocol != null && manager.ExperimentProtocol.ExperimentPhase == ExperimentPhase.Pilot
                ? ExperimentTaskPhase.Pilot : ExperimentTaskPhase.Formal;
            taskButtonDefinitions.AddRange(catalog.GetTasks(phase));
            for (var i = 0; i < taskButtonDefinitions.Count; i++)
            {
                var task = taskButtonDefinitions[i];
                var column = i % 2; var row = i / 2;
                var x = column == 0 ? -210f : 210f; var y = 90f - row * 190f;
                taskButtons.Add(CreateButton(taskSelectionPanel.transform, $"Task{i + 1}Button", task.displayName, new Vector2(x, y), new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f)));
                CreateText(taskSelectionPanel.transform, $"Task{i + 1}Context", task.context + "\nOpening: " + task.initialQuestion, new Vector2(x, y - 80f), new Vector2(380f, 88f), 15, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 1f));
            }
        }

        private void BuildFormalModeButtons()
        {
            formalModeButtons.Clear(); formalModeStatusTexts.Clear();
            var codes = new[] { FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR };
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i]; var column = i % 2; var row = i / 2;
                var x = column == 0 ? -215f : 215f; var y = 92f - row * 180f;
                var captured = code;
                var button = CreateButton(formalModeSelectionPanel.transform, code + "ModeButton", FriendlyConditionLabel(code),
                    new Vector2(x, y), new Vector2(390f, 70f), new Color(0.16f, 0.38f, 0.68f, 1f));
                button.onClick.AddListener(() => SelectFormalMode(captured));
                formalModeButtons[code] = button;
                formalModeStatusTexts[code] = CreateText(formalModeSelectionPanel.transform, code + "Status", "Available",
                    new Vector2(x, y - 52f), new Vector2(360f, 26f), 15, TextAnchor.MiddleCenter, new Color(.75f, .9f, .75f, 1f));
            }
        }

        private static string FriendlyConditionLabel(FormalConditionCode code) => code switch
        {
            FormalConditionCode.NE => "NE — Explicit feedback from partner",
            FormalConditionCode.NR => "NR — Recast feedback from partner",
            FormalConditionCode.SE => "SE — Explicit feedback from support agent",
            FormalConditionCode.SR => "SR — Recast feedback from support agent",
            _ => code.ToString()
        };

        private void SelectFormalMode(FormalConditionCode code)
        {
            var collection = EditorCollectionSessionCoordinator.Active;
            if (collection != null && collection.IsArmed)
            {
                if (!collection.SelectFormalCondition(code, out var collectionError))
                    Debug.LogWarning("[EditorCollection] Formal mode selection blocked: " + collectionError, this);
                Refresh();
                return;
            }
            var rehearsal = RehearsalSessionCoordinator.Active;
            var error = rehearsal == null ? "rehearsal_session_missing" : string.Empty;
            if (rehearsal == null || !rehearsal.SelectFormalCondition(code, out error))
            {
                if (!string.IsNullOrWhiteSpace(error)) Debug.LogWarning("[Rehearsal] Formal mode selection blocked: " + error, this);
            }
            Refresh();
        }

        private void Refresh()
        {
            if (orchestrator == null)
            {
                return;
            }

            var state = orchestrator.CurrentState;
            var dialogueActive = orchestrator.IsDialogueActive;
            var rehearsal = RehearsalSessionCoordinator.Active;
            var rehearsalActive = rehearsal != null && rehearsal.IsActive;
            var collection = EditorCollectionSessionCoordinator.Active;
            var collectionArmed = collection != null && collection.IsArmed;
            if (collectionArmed) sessionPreparationBlocked = false;
            var collectionParticipantActive = collectionArmed && collection.ParticipantStarted;
            var collectionFinal = collectionArmed && (collection.FinalRankingVisible || collection.ExperimentCompleted);
            var pilotCollection = PilotCollectionSessionCoordinator.Active;
            var pilotStage = pilotCollection?.Stage ?? PilotParticipantStage.None;
            var pilotFlowVisible = pilotStage != PilotParticipantStage.None;
            var questionnaireSession = FindFirstObjectByType<QuestionnaireRuntimeController>(FindObjectsInactive.Include)?.ActiveSession;
            var questionnaireActive = questionnaireSession != null
                && (questionnaireSession.completionStatus == QuestionnaireCompletionStatus.InProgress
                    || questionnaireSession.completionStatus == QuestionnaireCompletionStatus.Reopened);
            if (!rehearsalActive) rehearsalFinalRankingVisible = false;
            var showFinalRanking = rehearsalActive && (rehearsalFinalRankingVisible || rehearsal.FinalRankingVisible);
            var rehearsalWaiting = rehearsalActive && string.IsNullOrWhiteSpace(rehearsal.CurrentTaskId);
            var showFormalModeSelection = state != SceneTalkState.ExperimentExitConfirm
                && !showFinalRanking && !collectionFinal
                && (collectionArmed ? collection.AwaitingParticipantConditionChoice
                    : rehearsalActive && rehearsal.IsFormal && rehearsal.AwaitingParticipantConditionChoice);
            bool isFixedMode = orchestrator.RuntimeConfig != null && orchestrator.RuntimeConfig.UseFixedExperimentMode;

            var showMain = !rehearsalActive && !collectionParticipantActive && !sessionPreparationBlocked && !pilotFlowVisible
                && (state == SceneTalkState.Idle || state == SceneTalkState.Finished);
            var showSettings = state == SceneTalkState.Settings;
            var showHistoryList = state == SceneTalkState.HistoryList;
            var showHistoryDetail = state == SceneTalkState.HistoryDetail;
            var showHistoryDelete = state == SceneTalkState.HistoryDeleteConfirm;
            var showHistoryError = state == SceneTalkState.HistoryError;
            var showExperimentExitConfirm = state == SceneTalkState.ExperimentExitConfirm;
            var showExperimentHistoryList = state == SceneTalkState.ExperimentHistoryList;
            var showExperimentHistoryActions = state == SceneTalkState.ExperimentHistoryActions;
            var showExperimentHistoryRecord = state == SceneTalkState.ExperimentHistoryRecord;
            var showExperimentHistoryConversation = state == SceneTalkState.ExperimentHistoryConversationDetail;
            var showExperimentHistoryQuestionnaire = state == SceneTalkState.ExperimentHistoryQuestionnaireDetail;
            var showExperimentHistoryDelete = state == SceneTalkState.ExperimentHistoryDeleteConfirm;
            var showExperimentHistoryError = state == SceneTalkState.ExperimentHistoryError;
            var showRequest = !dialogueActive
                && (!isFixedMode || state != SceneTalkState.Listening)
                && (state == SceneTalkState.Listening
                    || state == SceneTalkState.Recording
                    || state == SceneTalkState.Transcribing
                    || state == SceneTalkState.Error);
            var showTaskSelection = !collectionArmed && !rehearsalActive && isFixedMode
                && !dialogueActive
                && (state == SceneTalkState.Listening);
            var showLoading = !dialogueActive
                && (state == SceneTalkState.Processing
                    || state == SceneTalkState.SceneReady
                    || state == SceneTalkState.HistoryLoading
                    || state == SceneTalkState.HistoryRestoring
                    || state == SceneTalkState.ExperimentHistoryLoading);
            var showDialogue = !showFormalModeSelection && !showFinalRanking && (dialogueActive
                || state == SceneTalkState.AvatarSpeaking
                || state == SceneTalkState.CorrectionFeedbackSpeaking
                || state == SceneTalkState.DialogueSpeaking
                || state == SceneTalkState.TurnReview) && !questionnaireActive;
            if (pilotFlowVisible) showDialogue = pilotStage == PilotParticipantStage.Dialogue && (dialogueActive
                || state == SceneTalkState.AvatarSpeaking || state == SceneTalkState.CorrectionFeedbackSpeaking
                || state == SceneTalkState.DialogueSpeaking || state == SceneTalkState.TurnReview);

            if (collectionFinal) { showRequest = false; showTaskSelection = false; showLoading = false; showDialogue = false; showFormalModeSelection = false; }
            SetActive(mainMenuPanel, showMain);
            SetActive(historyButton?.gameObject, showMain && orchestrator.IsHistoryAvailable);
            SetActive(sessionNotPreparedPanel, sessionPreparationBlocked && !collectionArmed);
            SetActive(rehearsalWaitingPanel, rehearsalWaiting && !showFormalModeSelection && !showFinalRanking);
            SetActive(formalModeSelectionPanel, showFormalModeSelection);
            SetActive(settingsPanel, showSettings);
            SetActive(historyListPanel, showHistoryList);
            SetActive(historyDetailPanel, showHistoryDetail);
            SetActive(historyDeletePanel, showHistoryDelete);
            SetActive(historyErrorPanel, showHistoryError);
            SetActive(experimentExitConfirmPanel, showExperimentExitConfirm);
            if (showExperimentExitConfirm) experimentExitConfirmPanel.transform.SetAsLastSibling();
            SetActive(experimentHistoryListPanel, showExperimentHistoryList);
            SetActive(experimentHistoryActionsPanel, showExperimentHistoryActions);
            SetActive(experimentHistoryRecordPanel, showExperimentHistoryRecord);
            SetActive(experimentHistoryConversationPanel, showExperimentHistoryConversation);
            SetActive(experimentHistoryQuestionnairePanel, showExperimentHistoryQuestionnaire);
            SetActive(experimentHistoryDeletePanel, showExperimentHistoryDelete);
            SetActive(experimentHistoryErrorPanel, showExperimentHistoryError);
            SetActive(requestPanel, showRequest);
            SetActive(taskSelectionPanel, showTaskSelection);
            SetActive(loadingPanel, showLoading);
            SetActive(subtitlePanel, showDialogue);
            // Keep assigned goals visible while the initial scene is loading so participants
            // can review the read-only task goals before their first speaking turn.
            RefreshGoalPanel(showDialogue || showLoading);
            SetActive(exitButtonObject, !showMain);

            RefreshSettingsPanel(showSettings);
            RefreshHistoryList(showHistoryList);
            RefreshHistoryDetail(showHistoryDetail);
            RefreshHistoryDelete(showHistoryDelete);
            RefreshHistoryError(showHistoryError);
            RefreshExperimentHistoryList(showExperimentHistoryList);
            RefreshExperimentHistoryActions(showExperimentHistoryActions);
            RefreshExperimentHistoryRecord(showExperimentHistoryRecord);
            RefreshExperimentHistoryConversation(showExperimentHistoryConversation);
            RefreshExperimentHistoryQuestionnaire(showExperimentHistoryQuestionnaire);
            RefreshExperimentHistoryDelete(showExperimentHistoryDelete);
            RefreshExperimentHistoryError(showExperimentHistoryError);
            RefreshRequestPanel(showRequest);
            RefreshLoadingPanel(showLoading);
            RefreshSubtitlePanel(showDialogue);
            RefreshDemoOverlay();
            RefreshFormalModeSelection(showFormalModeSelection);
            BringExitButtonToFront();
        }

        public void RefreshExternalState() => Refresh();

        public void ShowDemoRankingPreview(bool pilot)
        {
            if (demoRankingPanel == null || demoRankingText == null) return;
            demoRankingText.text = pilot
                ? "PILOT FINAL RANKING / 预实验最终排序\n\n1  Voice Only\n2  Floating Orb\n3  Humanoid Agent\n\nDEMO OPERATOR PREVIEW\nautoFilledForDemo only"
                : "FORMAL FINAL RANKING / 正式条件最终排序\n\n1  NE\n2  NR\n3  SE\n4  SR\n\nDEMO OPERATOR PREVIEW\nautoFilledForDemo only";
            demoRankingPanel.SetActive(true);
            demoRankingPanel.transform.SetAsLastSibling();
            if (demoBanner != null) demoBanner.transform.SetAsLastSibling();
        }

        public void ShowRehearsalRanking(bool pilot)
        {
            if (demoRankingPanel == null || demoRankingText == null) return;
            demoRankingText.text = pilot
                ? "PILOT REHEARSAL RANKING\n\nUse Rehearsal Control to exercise QA ranking submission."
                : "FORMAL REHEARSAL RANKING\n\nUse the collection participant flow for the interactive final ranking.";
            demoRankingPanel.name = "RehearsalRankingOperatorNotice";
            rehearsalFinalRankingVisible = true;
            demoRankingPanel.SetActive(true);
            demoRankingPanel.transform.SetAsLastSibling();
        }

        private void RefreshDemoOverlay()
        {
            var rehearsal = RehearsalSessionCoordinator.Active;
            var deviceValidation = ExperimentRuntimePlatform.IsPicoDeviceValidation
                || rehearsal?.IsDeviceValidation == true
                || PilotCollectionSessionCoordinator.Active?.IsDeviceValidation == true;
            if (deviceValidation)
            {
                SetActive(demoBanner, true); SetActive(demoStatusPanel, true);
                if (demoBannerText != null) demoBannerText.text = "PICO DEVICE VALIDATION — NOT PARTICIPANT DATA";
                if (demoStatusText != null)
                {
                    var mode = rehearsal?.IsFormal == true ? "Formal" : rehearsal?.IsPilot == true ? "Pilot" : "Not started";
                    demoStatusText.text = $"Mode: PICO Device Validation {mode}\n"
                        + $"Qualification: Rehearsal\nData origin: rehearsal\nCollection eligible: No\n"
                        + $"Profile: pico_device_validation\nSession: {rehearsal?.SessionId ?? string.Empty}";
                }
                demoBanner?.transform.SetAsLastSibling();
                return;
            }
            if (rehearsal != null && rehearsal.IsActive)
            {
                SetActive(demoBanner, false); SetActive(demoStatusPanel, false);
                return;
            }
            var demo = EditorDemoSessionCoordinator.Active;
            var visible = demo != null && demo.IsDemoMode;
            SetActive(demoBanner, visible); SetActive(demoStatusPanel, visible);
            if (!visible)
            {
                SetActive(demoRankingPanel, false);
                return;
            }
            if (demoStatusText == null) return;
            var formalCondition = demo.CurrentPosition >= 0 && demo.FormalAssignment?.conditions != null && demo.CurrentPosition < demo.FormalAssignment.conditions.Length ? demo.FormalAssignment.conditions[demo.CurrentPosition] : null;
            var pilotCondition = demo.CurrentPosition >= 0 && demo.PilotAssignment?.conditions != null && demo.CurrentPosition < demo.PilotAssignment.conditions.Length ? demo.PilotAssignment.conditions[demo.CurrentPosition] : null;
            var condition = demo.IsFormalDemo ? formalCondition?.formalConditionCode.ToString() : pilotCondition?.embodimentConditionLabel;
            var avatar = demo.IsFormalDemo ? demo.ResolveFormalAvatarKey(demo.CurrentTaskId) : demo.ResolvePilotProfile(pilotCondition?.embodimentCondition ?? PilotEmbodimentCondition.VoiceOnly)?.visualPrefabKey;
            demoStatusText.text = $"Mode: {(demo.IsFormalDemo ? "Editor Demo Formal" : "Editor Demo Pilot")}\n"
                + $"Condition: {condition ?? "not prepared"}\nTask: {demo.CurrentTaskId}\n"
                + $"Sequence position: {Mathf.Max(0, demo.CurrentPosition + 1)}/{demo.TotalConditions}\n"
                + $"Avatar: DEMO AVATAR ({avatar})\nVoice: Editor Demo\nCollection eligible: No\n"
                + "Editor Demo Resource — Not Collection Approved";
        }

        public void SetGoalPanelVisible(bool visible)
        {
            goalPanelVisible = visible;
            Refresh();
        }

        private void RefreshGoalPanel(bool dialogueVisible)
        {
            var lifecycle = FindFirstObjectByType<ExperimentLifecycleCoordinator>(FindObjectsInactive.Include);
            var pilot = PilotWorkflowCoordinator.Active;
            var usePilotRehearsal = RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsPilot;
            var usePilotDemo = EditorDemoSessionCoordinator.Active != null && EditorDemoSessionCoordinator.Active.IsPilotDemo;
            var usePilotCollection = PilotCollectionSessionCoordinator.Active?.IsArmed == true;
            var tracker = usePilotRehearsal || usePilotDemo || usePilotCollection ? pilot?.Goals : lifecycle?.GoalTracker;
            BindGoalTracker(tracker);
            var hasGoals = tracker != null && tracker.Goals.Count > 0;
            SetActive(taskGoalPanel, dialogueVisible && goalPanelVisible && hasGoals);
            if (!hasGoals && taskGoalText != null) taskGoalText.text = string.Empty;
        }

        private void BindGoalTracker(GoalProgressTracker tracker)
        {
            if (ReferenceEquals(subscribedGoalTracker, tracker)) return;
            UnsubscribeGoalTracker();
            subscribedGoalTracker = tracker;
            if (subscribedGoalTracker == null) return;
            subscribedGoalTracker.OnGoalProgressChanged += OnGoalProgressChanged;
            subscribedGoalTracker.OnGoalCollectionReset += OnGoalProgressChanged;
            subscribedGoalTracker.OnGoalStateChanged += OnGoalProgressChanged;
            subscribedGoalTracker.OnAllGoalsConfirmed += OnGoalProgressChanged;
            RenderGoalPanel(subscribedGoalTracker);
        }

        private void UnsubscribeGoalTracker()
        {
            if (subscribedGoalTracker == null) return;
            subscribedGoalTracker.OnGoalProgressChanged -= OnGoalProgressChanged;
            subscribedGoalTracker.OnGoalCollectionReset -= OnGoalProgressChanged;
            subscribedGoalTracker.OnGoalStateChanged -= OnGoalProgressChanged;
            subscribedGoalTracker.OnAllGoalsConfirmed -= OnGoalProgressChanged;
            subscribedGoalTracker = null;
        }

        private void OnGoalProgressChanged(GoalProgressChangedEvent value)
        {
            if (subscribedGoalTracker == null || value == null) return;
            var activeRun = subscribedGoalTracker.Context.conditionRunId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value.conditionRunId) && !string.Equals(value.conditionRunId, activeRun, System.StringComparison.Ordinal)) return;
            RenderGoalPanel(subscribedGoalTracker);
        }

        private void RenderGoalPanel(GoalProgressTracker tracker)
        {
            if (taskGoalText == null || tracker == null || tracker.Goals.Count == 0) { if (taskGoalText != null) taskGoalText.text = string.Empty; return; }
            var taskName = string.IsNullOrWhiteSpace(tracker.Context.taskId) ? "Task" : tracker.Context.taskId;
            var builder = new System.Text.StringBuilder(taskName).AppendLine();
            for (var i = 0; i < tracker.Goals.Count; i++)
            {
                var goal = tracker.Goals[i];
                if (goal.state != GoalProgressState.Confirmed && i != tracker.ActiveGoalIndex) continue;
                builder.Append(goal.state == GoalProgressState.Confirmed ? "[✓] "
                    : goal.state == GoalProgressState.Candidate ? "[…] "
                    : goal.state == GoalProgressState.Rejected ? "[↻] " : "[ ] ").AppendLine(goal.goalText);
            }
            if (tracker.SequenceState == GoalSequenceState.AwaitingParticipantTurn)
                builder.AppendLine().AppendLine("Goal completed. Speak once more to continue...");
            else if (tracker.SequenceState == GoalSequenceState.AwaitingAvatarReply)
                builder.AppendLine().AppendLine("Waiting for the Avatar's reply to finish...");
            else if (tracker.SequenceState == GoalSequenceState.Completed)
                builder.AppendLine().AppendLine("All goals completed.");
            builder.AppendLine().Append(tracker.ConfirmedCount).Append(" / ").Append(tracker.Goals.Count).Append(" completed");
            taskGoalText.text = builder.ToString();
        }

        private void RefreshFormalModeSelection(bool visible)
        {
            if (!visible) return;
            var assignment = EditorCollectionSessionCoordinator.Active?.IsArmed == true
                ? EditorCollectionSessionCoordinator.Active.Assignment
                : RehearsalSessionCoordinator.Active?.FormalAssignment;
            foreach (var pair in formalModeButtons)
            {
                var item = assignment?.conditions?.FirstOrDefault(x => x.formalConditionCode == pair.Key);
                var status = item == null ? "Unavailable" : item.status == ConditionRunStatus.Completed ? "Completed"
                    : item.status == ConditionRunStatus.TechnicalInvalid ? "Retry available"
                    : item.status == ConditionRunStatus.Assigned ? "Available" : "In progress";
                pair.Value.interactable = item != null && (item.status == ConditionRunStatus.Assigned || item.status == ConditionRunStatus.TechnicalInvalid);
                if (formalModeStatusTexts.TryGetValue(pair.Key, out var label)) label.text = status;
            }
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
                        SceneTalkUserSettingsStore.Current.assistantEmbodiment)
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
                        ? "Appearance is saved globally and locked when a Formal experiment starts."
                        : "Choose Assistant Agent to change its appearance.";
            }
        }

        private void RefreshExperimentHistoryList(bool isVisible)
        {
            if (!isVisible) return;
            lastRenderedExperimentRecordId = string.Empty;
            var page = GetExperimentCoordinator()?.CurrentHistoryPage ?? new ExperimentRecordPage();
            var items = page.items ?? System.Array.Empty<ExperimentRecordSummary>();
            SetActive(experimentHistoryEmptyText?.gameObject, items.Length == 0);
            for (var i = 0; i < experimentHistoryRowButtons.Length; i++)
            {
                var button = experimentHistoryRowButtons[i];
                var hasItem = i < items.Length;
                SetActive(button?.gameObject, hasItem);
                if (!hasItem || button == null)
                {
                    experimentHistoryRowIds[i] = string.Empty;
                    continue;
                }
                var item = items[i];
                SetButtonLabel(button,
                    $"[{item.kind}] {item.participantId}    {FormatHistoryTime(item.updatedAtUnixMs)}\n"
                    + $"Status: {FriendlyExperimentStatus(item.status)}"
                    + (item.kind == ExperimentKind.Formal && !string.IsNullOrWhiteSpace(item.assistantEmbodimentSnapshot)
                        ? $"  |  Appearance: {ResolveCorrectionAppearanceDisplayName(item.assistantEmbodimentSnapshot)}"
                        : string.Empty));
                if (!string.Equals(experimentHistoryRowIds[i], item.experimentId, System.StringComparison.Ordinal))
                {
                    experimentHistoryRowIds[i] = item.experimentId;
                    button.onClick.RemoveAllListeners();
                    var id = item.experimentId;
                    button.onClick.AddListener(() => GetExperimentCoordinator()?.SelectExperiment(id));
                }
            }
            if (experimentHistoryPageText != null)
                experimentHistoryPageText.text = $"Page {page.pageIndex + 1} / {page.TotalPages}";
            SetInteractable(experimentHistoryPreviousButton, page.pageIndex > 0);
            SetInteractable(experimentHistoryNextButton, page.pageIndex + 1 < page.TotalPages);
        }

        private void RefreshExperimentHistoryActions(bool isVisible)
        {
            if (!isVisible) return;
            var summary = GetExperimentCoordinator()?.SelectedExperiment?.summary;
            if (summary == null) return;
            if (experimentHistoryActionsSummaryText != null)
            {
                experimentHistoryActionsSummaryText.text =
                    $"{summary.kind} experiment  |  {FriendlyExperimentStatus(summary.status)}\n"
                    + $"Participant: {summary.participantId}\n"
                    + $"Updated: {FormatHistoryTime(summary.updatedAtUnixMs)}"
                    + (summary.kind == ExperimentKind.Formal && !string.IsNullOrWhiteSpace(summary.assistantEmbodimentSnapshot)
                        ? $"\nAppearance: {ResolveCorrectionAppearanceDisplayName(summary.assistantEmbodimentSnapshot)}"
                        : string.Empty);
            }
            SetInteractable(experimentHistoryContinueButton, summary.CanContinue);
            SetInteractable(experimentHistoryViewButton, true);
            SetInteractable(experimentHistoryDeleteButton, true);
        }

        private void RefreshExperimentHistoryRecord(bool isVisible)
        {
            if (!isVisible) return;
            var detail = GetExperimentCoordinator()?.SelectedExperiment;
            if (detail?.summary == null) return;
            var renderKey = detail.summary.experimentId + ":" + detail.summary.updatedAtUnixMs;
            if (!string.Equals(lastRenderedExperimentRecordId, renderKey, System.StringComparison.Ordinal))
            {
                lastRenderedExperimentRecordId = renderKey;
                SetScrollableText(experimentHistoryRecordText, experimentRecordContentRect, experimentRecordScrollRect,
                    BuildExperimentRecordText(detail), 168f);
                experimentRecordEntries.Clear();
                foreach (var conversation in detail.conversations ?? System.Array.Empty<LearningSessionSummary>())
                {
                    experimentRecordEntries.Add(new ExperimentRecordEntry
                    {
                        isConversation = true,
                        id = conversation.sessionId,
                        label = $"Conversation  {conversation.title}  |  {conversation.turnCount} turns  |  {FormatHistoryTime(conversation.updatedAtUnixMs)}"
                    });
                }
                foreach (var questionnaire in detail.questionnaires ?? System.Array.Empty<ExperimentQuestionnaireRecord>())
                {
                    var session = questionnaire.session;
                    experimentRecordEntries.Add(new ExperimentRecordEntry
                    {
                        isConversation = false,
                        id = questionnaire.questionnaireRecordId,
                        label = $"Questionnaire  {session?.questionnaireId ?? "-"}  |  {session?.completionStatus}  |  {(session?.completionRate ?? 0f):P0}"
                    });
                }
            }

            var pages = Mathf.Max(1, Mathf.CeilToInt(experimentRecordEntries.Count / (float)experimentRecordEntryButtons.Length));
            experimentRecordEntryPage = Mathf.Clamp(experimentRecordEntryPage, 0, pages - 1);
            var offset = experimentRecordEntryPage * experimentRecordEntryButtons.Length;
            for (var i = 0; i < experimentRecordEntryButtons.Length; i++)
            {
                var index = offset + i;
                var hasItem = index < experimentRecordEntries.Count;
                SetActive(experimentRecordEntryButtons[i]?.gameObject, hasItem);
                if (hasItem) SetButtonLabel(experimentRecordEntryButtons[i], experimentRecordEntries[index].label);
            }
            if (experimentRecordEntriesPageText != null)
                experimentRecordEntriesPageText.text = $"Page {experimentRecordEntryPage + 1} / {pages}";
            SetInteractable(experimentRecordPreviousButton, experimentRecordEntryPage > 0);
            SetInteractable(experimentRecordNextButton, experimentRecordEntryPage + 1 < pages);
        }

        private void RefreshExperimentHistoryConversation(bool isVisible)
        {
            if (!isVisible) return;
            var detail = GetExperimentCoordinator()?.SelectedConversation;
            if (detail?.summary == null) return;
            if (experimentHistoryConversationSummaryText != null)
            {
                experimentHistoryConversationSummaryText.text =
                    $"{detail.summary.title}  |  {detail.summary.experimentKind}  |  {detail.summary.turnCount} turns\n"
                    + $"Task: {detail.summary.taskType}  |  Updated: {FormatHistoryTime(detail.summary.updatedAtUnixMs)}";
            }
            if (string.Equals(lastRenderedExperimentConversationId, detail.summary.sessionId, System.StringComparison.Ordinal)) return;
            lastRenderedExperimentConversationId = detail.summary.sessionId;
            SetScrollableText(experimentHistoryConversationBodyText, experimentConversationContentRect,
                experimentConversationScrollRect, BuildHistoryDetailText(detail), 318f);
        }

        private void RefreshExperimentHistoryQuestionnaire(bool isVisible)
        {
            if (!isVisible) return;
            var questionnaire = GetExperimentCoordinator()?.SelectedQuestionnaire;
            var recordId = questionnaire?.questionnaireRecordId;
            if (questionnaire == null || string.Equals(lastRenderedExperimentQuestionnaireId, recordId, System.StringComparison.Ordinal)) return;
            lastRenderedExperimentQuestionnaireId = recordId;
            SetScrollableText(experimentHistoryQuestionnaireText, experimentQuestionnaireContentRect,
                experimentQuestionnaireScrollRect, BuildQuestionnaireDetailText(questionnaire), 398f);
        }

        private void RefreshExperimentHistoryDelete(bool isVisible)
        {
            if (!isVisible || experimentHistoryDeleteMessageText == null) return;
            var selected = GetExperimentCoordinator()?.SelectedExperiment?.summary;
            experimentHistoryDeleteMessageText.text = selected == null
                ? "The selected experiment is no longer available."
                : $"Delete experiment for {selected.participantId} permanently?\nThis removes its database records and owned cached data.";
        }

        private void RefreshExperimentHistoryError(bool isVisible)
        {
            if (isVisible && experimentHistoryErrorText != null)
                experimentHistoryErrorText.text = GetExperimentCoordinator()?.ErrorMessage ?? "Experiment history operation failed.";
        }

        private void OpenExperimentRecordEntry(int visibleIndex)
        {
            var index = experimentRecordEntryPage * experimentRecordEntryButtons.Length + visibleIndex;
            if (index < 0 || index >= experimentRecordEntries.Count) return;
            var entry = experimentRecordEntries[index];
            if (entry.isConversation)
            {
                lastRenderedExperimentConversationId = string.Empty;
                GetExperimentCoordinator()?.SelectExperimentConversation(entry.id);
            }
            else
            {
                lastRenderedExperimentQuestionnaireId = string.Empty;
                GetExperimentCoordinator()?.SelectExperimentQuestionnaire(entry.id);
            }
        }

        private static string BuildExperimentRecordText(ExperimentRecordDetail detail)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Experiment: {detail.summary.kind}");
            builder.AppendLine($"Participant: {detail.summary.participantId}");
            builder.AppendLine($"Session: {detail.summary.sessionId}");
            builder.AppendLine($"Status: {FriendlyExperimentStatus(detail.summary.status)}  |  Created: {FormatHistoryTime(detail.summary.createdAtUnixMs)}");
            if (detail.summary.kind == ExperimentKind.Formal
                && !string.IsNullOrWhiteSpace(detail.summary.assistantEmbodimentSnapshot))
                builder.AppendLine("Assistant appearance: "
                    + ResolveCorrectionAppearanceDisplayName(detail.summary.assistantEmbodimentSnapshot));

            builder.AppendLine();
            builder.AppendLine("ATTEMPTS");
            var attempts = detail.attempts ?? System.Array.Empty<ExperimentAttemptRecord>();
            if (attempts.Length == 0)
                builder.AppendLine("No attempts recorded.");
            foreach (var attempt in attempts.OrderBy(item => item.attemptIndex).ThenBy(item => item.startedAtUnixMs))
            {
                builder.Append($"Attempt {attempt.attemptIndex}: {ResolveText(attempt.conditionKey)} / {ResolveText(attempt.taskId)} - {attempt.status}");
                if (!string.IsNullOrWhiteSpace(attempt.completionReason)) builder.Append(" (" + attempt.completionReason + ")");
                builder.AppendLine();
            }

            var ranking = detail.rankings?.FirstOrDefault()?.response;
            if (ranking != null)
            {
                builder.AppendLine();
                builder.AppendLine("FINAL RANKING");
                var ranked = (ranking.rankings ?? System.Array.Empty<PreferenceRankEntry>())
                    .OrderBy(item => item.rank)
                    .Select(item => $"{item.rank}. {(string.IsNullOrWhiteSpace(item.conditionCode) ? item.embodimentCondition : item.conditionCode)}");
                builder.AppendLine("Ranking: " + string.Join("  ", ranked));
                var preferred = string.IsNullOrWhiteSpace(ranking.preferredConditionCode)
                    ? ranking.preferredEmbodimentCondition : ranking.preferredConditionCode;
                builder.AppendLine($"Preferred: {ResolveText(preferred)}  |  Reason: {ResolveText(ranking.reason)}");
            }
            return builder.ToString();
        }

        private static string BuildQuestionnaireDetailText(ExperimentQuestionnaireRecord record)
        {
            var session = record.session ?? new QuestionnaireSession();
            var builder = new StringBuilder();
            builder.AppendLine($"Questionnaire: {ResolveText(session.questionnaireId)}");
            builder.AppendLine($"Status: {session.completionStatus}  |  Completion: {session.completionRate:P0}  |  Missing: {session.hasMissing}");
            builder.AppendLine($"Task: {ResolveText(session.taskId)}  |  Condition run: {ResolveText(session.conditionRunId)}");
            builder.AppendLine();
            builder.AppendLine("SECTION SCORES");
            foreach (var score in session.sectionScores ?? System.Array.Empty<QuestionnaireScoreResult>())
                builder.AppendLine($"{score.sectionId}: mean={score.mean:0.##}, answered={score.answeredCount}/{score.itemCount}, missing={score.hasMissing}");
            builder.AppendLine();
            builder.AppendLine("RESPONSES");
            foreach (var prompt in record.prompts ?? System.Array.Empty<QuestionnairePromptSnapshot>())
            {
                var response = session.responses?.FirstOrDefault(item => item.itemId == prompt.itemId);
                builder.AppendLine($"[{prompt.sectionId}] {ResolveText(prompt.promptEnglish)}");
                if (!string.IsNullOrWhiteSpace(prompt.promptChinese)) builder.AppendLine(prompt.promptChinese);
                builder.Append("Answer: " + ResolveText(response?.rawValue));
                if (response?.hasScoredValue == true) builder.Append($"  |  Score: {response.scoredValue:0.##}");
                builder.AppendLine();
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static void SetScrollableText(TMP_Text text, RectTransform content, ScrollRect scroll, string value, float minimumHeight)
        {
            if (text == null || content == null || scroll == null) return;
            text.text = value ?? string.Empty;
            content.sizeDelta = new Vector2(-24f, Mathf.Max(minimumHeight, text.preferredHeight + 24f));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        private static string FriendlyExperimentStatus(ExperimentRecordStatus status)
        {
            return status == ExperimentRecordStatus.InProgress ? "In progress" : status.ToString();
        }

        private static string HumanizeExperimentError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? string.Empty : error.Replace('_', ' ');
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
                    + ResolveCorrectionStyleDisplayName(item.correctionStyle)
                    + (item.IsExperimentConversation
                        ? $"  |  Experiment {ShortHistoryId(item.experimentId)} / {item.experimentKind}"
                        : string.Empty));

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
                    + (detail.summary.IsExperimentConversation
                        ? $"Experiment: {detail.summary.experimentId}  |  Kind: {detail.summary.experimentKind}\n"
                        : string.Empty)
                    + $"Created {FormatHistoryTime(detail.summary.createdAtUnixMs)}  |  Updated {FormatHistoryTime(detail.summary.updatedAtUnixMs)}\n"
                    + $"Scenario: {detail.summary.scenarioId}  |  Environment: {detail.summary.environmentType}\n"
                    + $"Avatar: {scene?.avatarRole?.role ?? "-"} / {appearance?.genderPresentation ?? "unknown"}  |  "
                    + $"Correction: {ResolveCorrectionSourceDisplayName(detail.summary.correctionProvider)} / "
                    + $"{ResolveCorrectionStyleDisplayName(detail.summary.correctionStyle)}  |  "
                    + $"Sensitivity: {detail.settings?.feedbackSensitivity ?? "moderate"}\n"
                    + $"Turns: {detail.summary.turnCount}  |  Corrections: {detail.summary.correctionCount}";
            }

            SetInteractable(historyContinueButton, detail.summary.CanContinue);
            SetInteractable(historyDeleteButton, detail.summary.CanDelete);
            SetButtonLabel(historyContinueButton, detail.summary.CanContinue ? "Continue" : "Experiment only");
            SetButtonLabel(historyDeleteButton, detail.summary.CanDelete ? "Delete" : "Experiment only");

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

        private static string ShortHistoryId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            return value.Length <= 8 ? value : value.Substring(0, 8);
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
            else if (orchestrator.CurrentState == SceneTalkState.ExperimentHistoryLoading)
            {
                loadingText.text = "Loading experiment history...";
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
                var showDebug = orchestrator.ShouldShowExperimentDebug
                    && !(RehearsalSessionCoordinator.Active?.IsActive ?? false)
                    && !(EditorCollectionSessionCoordinator.Active?.IsArmed ?? false);
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
                    ? new Vector2(DialoguePanelCenterX, -194f)
                    : new Vector2(DialoguePanelCenterX, -136f);
                subtitlePanelRect.sizeDelta = hideSubtitles
                    ? new Vector2(DialoguePanelWidth, 132f)
                    : new Vector2(DialoguePanelWidth, 248f);
            }

            if (subtitleTextContainerRect != null)
            {
                subtitleTextContainerRect.anchoredPosition = new Vector2(DialogueContentCenterX, 48f);
                subtitleTextContainerRect.sizeDelta = new Vector2(DialogueContentWidth, 100f);
            }

            if (experimentDebugText != null)
            {
                var debugRect = experimentDebugText.GetComponent<RectTransform>();
                debugRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 48f)
                    : new Vector2(DialogueContentCenterX, 111f);
                debugRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 22f)
                    : new Vector2(DialogueContentWidth, 22f);
            }

            if (correctionFeedbackText != null)
            {
                var feedbackRect = correctionFeedbackText.GetComponent<RectTransform>();
                feedbackRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 16f)
                    : new Vector2(DialogueContentCenterX, -32f);
                feedbackRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 36f);
            }

            if (correctionStatusText != null)
            {
                var correctionRect = correctionStatusText.GetComponent<RectTransform>();
                correctionRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -14f)
                    : new Vector2(DialogueContentCenterX, -77f);
                correctionRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 28f);
            }

            if (dialogueStatusText != null)
            {
                var statusRect = dialogueStatusText.GetComponent<RectTransform>();
                statusRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -42f)
                    : new Vector2(DialogueContentCenterX, -102f);
                statusRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 28f);
            }

            if (dialogueListenButton != null)
            {
                var buttonRect = dialogueListenButton.GetComponent<RectTransform>();
                buttonRect.anchoredPosition = hideSubtitles
                    ? new Vector2(310f, -32f)
                    : new Vector2(DialogueButtonCenterX, -92f);
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
            UnsubscribeGoalTracker();
            if (orchestrator == null || !isSubscribed) return;
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

            if (orchestrator.CurrentState == SceneTalkState.ExperimentExitConfirm)
            {
                GetExperimentCoordinator()?.CancelLeaveExperiment();
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.ExperimentHistoryLoading
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryList
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryActions
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryRecord
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryConversationDetail
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryQuestionnaireDetail
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryDeleteConfirm
                || orchestrator.CurrentState == SceneTalkState.ExperimentHistoryError)
            {
                GetExperimentCoordinator()?.BackFromExperimentHistory();
                return;
            }

            orchestrator.ReturnToInitialMenu();
        }

        private void HandleGlobalExit()
        {
            if (orchestrator == null)
            {
                return;
            }

            var experiment = GetExperimentCoordinator();
            if (experiment?.HasActiveExperiment == true)
            {
                if (orchestrator.CurrentState == SceneTalkState.ExperimentCompleted)
                {
                    experiment.ContinueAfterExperimentCompletion();
                    return;
                }
                if (orchestrator.CurrentState == SceneTalkState.ExperimentExitConfirm)
                    experiment.CancelLeaveExperiment();
                else if (experiment.HasActiveConversation)
                {
                    if (!experiment.ReturnFromCurrentConversationToSelection(out var returnError))
                        Debug.LogWarning("[Experiment] Could not return to condition selection: " + returnError, this);
                }
                else
                    experiment.RequestLeaveExperiment();
                return;
            }

            var pilot = PilotCollectionSessionCoordinator.Active;
            if (pilot != null && pilot.Stage != PilotParticipantStage.None)
            {
                if (pilot.HasActiveDialogueCondition)
                {
                    if (!pilot.ReturnToAppearanceSelectionFromDialogue("participant_return_to_selection", out var returnError))
                        Debug.LogWarning("[Pilot] Could not return to appearance selection: " + returnError, this);
                    return;
                }
                pilot.SuspendAndEndSession("participant_exit_checkpoint");
                return;
            }

            var collection = EditorCollectionSessionCoordinator.Active;
            if (collection?.IsArmed == true)
            {
                if (collection.HasActiveDialogueCondition)
                {
                    if (!collection.ReturnToModeSelectionFromDialogue("participant_return_to_selection", out var returnError))
                        Debug.LogWarning("[Formal] Could not return to mode selection: " + returnError, this);
                    return;
                }
                collection.SuspendAndEndRuntimeSession("participant_exit_checkpoint");
                return;
            }

            var rehearsal = RehearsalSessionCoordinator.Active;
            if (rehearsal?.IsActive == true)
            {
                if (rehearsal.HasActiveDialogueCondition)
                {
                    if (!rehearsal.ReturnToConditionSelectionFromDialogue("participant_return_to_selection", out var returnError))
                        Debug.LogWarning("[Rehearsal] Could not return to condition selection: " + returnError, this);
                    return;
                }
                rehearsal.SuspendSession("participant_exit_checkpoint");
                return;
            }

            var demo = EditorDemoSessionCoordinator.Active;
            if (demo?.IsDemoMode == true)
            {
                demo.ResetDemoSession();
                return;
            }

            if (sessionPreparationBlocked)
            {
                sessionPreparationBlocked = false;
                Refresh();
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.Idle
                || orchestrator.CurrentState == SceneTalkState.Finished)
            {
                QuitApplication();
                return;
            }

            ExitCurrentView();
        }

        private Button CreateGlobalExitButton()
        {
            var button = CreateButton(
                worldCanvas.transform,
                "ExitButton",
                "Exit",
                Vector2.zero,
                ExitButtonSize,
                ExitButtonColor);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = ExitButtonInset;
            return button;
        }

        public void BringExitButtonToFront()
        {
            if (exitButtonObject != null && exitButtonObject.transform.parent == worldCanvas?.transform)
            {
                exitButtonObject.transform.SetAsLastSibling();
            }
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

        private static TMP_Text CreateScrollableText(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            out ScrollRect scrollRect,
            out RectTransform contentRect)
        {
            var viewport = CreatePanel(parent, name + "Viewport", position, size, new Color(0f, 0f, 0f, .34f));
            viewport.AddComponent<RectMask2D>();
            scrollRect = viewport.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 34f;
            var text = CreateText(viewport.transform, name + "Text", string.Empty, Vector2.zero,
                new Vector2(size.x - 24f, size.y - 12f), 16, TextAnchor.UpperLeft, Color.white);
            text.overflowMode = TextOverflowModes.Overflow;
            contentRect = text.rectTransform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(-24f, size.y - 12f);
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            return text;
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
