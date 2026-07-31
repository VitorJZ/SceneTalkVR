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
        private const string TaskGoalCanvasName = "SceneTalkVR Task Goal UI";
        private static readonly Vector2 ExitButtonInset = new Vector2(-18f, -18f);
        private static readonly Vector2 ExitButtonSize = new Vector2(110f, 44f);
        private static readonly Color ExitButtonColor = new Color(0.58f, 0.18f, 0.18f, 1f);
        private static readonly Vector2 TaskGoalCanvasPosition = new Vector2(-560f, 100f);
        private static readonly Vector2 TaskGoalPanelSize = new Vector2(300f, 320f);
        private const float DialoguePanelCenterX = 0f;
        private const float DialoguePanelVisibleCenterY = -170f;
        private const float DialoguePanelVisibleHeight = 180f;
        private const float DialoguePanelHiddenCenterY = -200f;
        private const float DialoguePanelHiddenHeight = 120f;
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
        private Canvas taskGoalCanvas;
        private GameObject taskGoalPanel;
        private GameObject demoBanner;
        private GameObject demoStatusPanel;
        private GameObject demoRankingPanel;
        private GameObject rehearsalWaitingPanel;
        private GameObject formalModeSelectionPanel;
        private GameObject sessionNotPreparedPanel;
        private PilotCollectionParticipantUi pilotCollectionUi;

        private Button pilotButton;
        private Button historyExportButton;
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
        private TMP_Text transportStatusText;
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
        private PicoHistoryExportCoordinator historyExportCoordinator;
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
            UpdateTaskGoalCanvasFacingUser();
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
            ConfigureHistoryExportCoordinator();

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
            demoBannerText = CreateText(demoBanner.transform, "EditorDemoBannerText", "编辑器演示——非参与者数据", Vector2.zero, new Vector2(680f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
            demoStatusPanel = CreatePanel(root, "EditorDemoStatusPanel", new Vector2(382f, 72f), new Vector2(270f, 190f), new Color(0.05f, 0.06f, 0.08f, 0.82f));
            demoStatusText = CreateText(demoStatusPanel.transform, "EditorDemoStatusText", string.Empty, Vector2.zero, new Vector2(246f, 170f), 14, TextAnchor.UpperLeft, new Color(1f, .86f, .42f, 1f));
            demoRankingPanel = CreatePanel(root, "EditorDemoRankingPreview", Vector2.zero, new Vector2(520f, 330f), new Color(0.03f, 0.04f, 0.07f, 0.94f));
            demoRankingText = CreateText(demoRankingPanel.transform, "EditorDemoRankingText", string.Empty, Vector2.zero, new Vector2(480f, 290f), 22, TextAnchor.MiddleCenter, Color.white);
            demoRankingPanel.SetActive(false);

            rehearsalWaitingPanel = CreatePanel(root, "RehearsalWaitingPanel", Vector2.zero, new Vector2(620f, 260f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(rehearsalWaitingPanel.transform, "Title", "欢迎使用 SceneTalkVR", new Vector2(0f, 62f), new Vector2(560f, 52f), 30, TextAnchor.MiddleCenter, Color.white);
            CreateText(rehearsalWaitingPanel.transform, "Instruction", "请稍候，实验人员正在准备下一个任务。", new Vector2(0f, -20f), new Vector2(540f, 90f), 21, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));

            formalModeSelectionPanel = CreatePanel(root, "FormalModeSelectionPanel", Vector2.zero, new Vector2(900f, 520f), new Color(0.04f, 0.05f, 0.07f, 0.96f));
            CreateText(formalModeSelectionPanel.transform, "Title", "选择反馈模式", new Vector2(0f, 220f), new Vector2(800f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            CreateText(formalModeSelectionPanel.transform, "Instruction", "请选择任一可用模式，任务已为你分配。", new Vector2(0f, 182f), new Vector2(800f, 30f), 17, TextAnchor.MiddleCenter, new Color(.78f, .86f, 1f, 1f));
            BuildFormalModeButtons();

            sessionNotPreparedPanel = CreatePanel(root, "SessionNotPreparedPanel", Vector2.zero, new Vector2(660f, 250f), new Color(0.04f, 0.05f, 0.07f, 0.97f));
            CreateText(sessionNotPreparedPanel.transform, "Title", "实验会话尚未准备", new Vector2(0f, 62f), new Vector2(590f, 44f), 27, TextAnchor.MiddleCenter, Color.white);
            CreateText(sessionNotPreparedPanel.transform, "Instruction", "实验会话尚未准备。\n请联系实验人员。", new Vector2(0f, -20f), new Vector2(590f, 92f), 21, TextAnchor.MiddleCenter, new Color(.85f, .9f, 1f, 1f));
            CreateButton(sessionNotPreparedPanel.transform, "SessionNotPreparedBackButton", "返回", new Vector2(0f, -92f), new Vector2(160f, 44f), new Color(.24f, .36f, .42f, 1f))
                .onClick.AddListener(() => { sessionPreparationBlocked = false; Refresh(); });

            mainMenuPanel = CreatePanel(root, "InitialPanel", new Vector2(0f, 0f), new Vector2(430f, 680f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            CreateText(mainMenuPanel.transform, "Title", "SceneTalkVR", new Vector2(0f, 292f), new Vector2(360f, 54f), 34, TextAnchor.MiddleCenter, Color.white);
            experimentPilotButton = CreateButton(mainMenuPanel.transform, "PilotExperimentButton", "预实验", new Vector2(0f, 210f), new Vector2(270f, 50f), new Color(0.18f, 0.48f, 0.58f, 1f));
            experimentFormalButton = CreateButton(mainMenuPanel.transform, "FormalExperimentButton", "正式实验", new Vector2(0f, 148f), new Vector2(270f, 50f), new Color(0.16f, 0.38f, 0.68f, 1f));
            pilotButton = CreateButton(mainMenuPanel.transform, "ExperimentHistoryButton", "实验历史", new Vector2(0f, 86f), new Vector2(270f, 50f), new Color(0.18f, 0.48f, 0.58f, 1f));
            historyButton = CreateButton(mainMenuPanel.transform, "HistoryButton", "对话历史", new Vector2(0f, 24f), new Vector2(270f, 50f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyExportButton = CreateButton(mainMenuPanel.transform, "ExportHistoryButton", "导出历史数据", new Vector2(0f, -38f), new Vector2(270f, 50f), new Color(0.12f, 0.52f, 0.38f, 1f));
            settingsButton = CreateButton(mainMenuPanel.transform, "SettingsButton", "设置", new Vector2(0f, -100f), new Vector2(270f, 50f), new Color(0.24f, 0.36f, 0.42f, 1f));
            quitButton = CreateButton(mainMenuPanel.transform, "QuitButton", "退出", new Vector2(0f, -162f), new Vector2(270f, 50f), ExitButtonColor);
            homeExperimentMessageText = CreateText(mainMenuPanel.transform, "ExperimentMessage", string.Empty, new Vector2(0f, -252f), new Vector2(380f, 100f), 16, TextAnchor.MiddleCenter, new Color(1f, .58f, .42f, 1f));

            BuildExperimentPanels(root);

            historyListPanel = CreatePanel(root, "HistoryListPanel", Vector2.zero, new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(historyListPanel.transform, "Title", "对话历史", new Vector2(0f, 210f), new Vector2(620f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
            historyEmptyText = CreateText(historyListPanel.transform, "Empty", "暂无对话历史。", new Vector2(0f, 10f), new Vector2(650f, 60f), 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.88f, 1f));
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
            historyPreviousButton = CreateButton(historyListPanel.transform, "PreviousButton", "上一页", new Vector2(-250f, -210f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyPageText = CreateText(historyListPanel.transform, "Page", "第 1 / 1 页", new Vector2(0f, -210f), new Vector2(200f, 40f), 18, TextAnchor.MiddleCenter, Color.white);
            historyNextButton = CreateButton(historyListPanel.transform, "NextButton", "下一页", new Vector2(250f, -210f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyListBackButton = CreateButton(historyListPanel.transform, "BackButton", "返回", new Vector2(-350f, 210f), new Vector2(100f, 40f), new Color(0.24f, 0.36f, 0.42f, 1f));

            historyDetailPanel = CreatePanel(root, "HistoryDetailPanel", Vector2.zero, new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            CreateText(historyDetailPanel.transform, "Title", "历史详情", new Vector2(0f, 212f), new Vector2(620f, 42f), 29, TextAnchor.MiddleCenter, Color.white);
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
            historyPageUpButton = CreateButton(historyDetailPanel.transform, "PageUpButton", "向上", new Vector2(355f, 8f), new Vector2(76f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyPageDownButton = CreateButton(historyDetailPanel.transform, "PageDownButton", "向下", new Vector2(355f, -52f), new Vector2(76f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyContinueButton = CreateButton(historyDetailPanel.transform, "ContinueButton", "继续", new Vector2(-110f, -210f), new Vector2(170f, 46f), new Color(0.12f, 0.52f, 0.38f, 1f));
            historyDeleteButton = CreateButton(historyDetailPanel.transform, "DeleteButton", "删除", new Vector2(110f, -210f), new Vector2(170f, 46f), ExitButtonColor);
            historyDetailBackButton = CreateButton(historyDetailPanel.transform, "BackButton", "返回", new Vector2(-330f, -210f), new Vector2(120f, 46f), new Color(0.24f, 0.36f, 0.42f, 1f));

            historyDeletePanel = CreatePanel(root, "HistoryDeletePanel", Vector2.zero, new Vector2(620f, 290f), new Color(0.04f, 0.05f, 0.07f, 0.97f));
            CreateText(historyDeletePanel.transform, "Title", "删除历史记录？", new Vector2(0f, 95f), new Vector2(500f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            historyDeleteMessageText = CreateText(historyDeletePanel.transform, "Message", string.Empty, new Vector2(0f, 25f), new Vector2(520f, 76f), 18, TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f, 1f));
            historyDeleteCancelButton = CreateButton(historyDeletePanel.transform, "CancelButton", "取消", new Vector2(-115f, -88f), new Vector2(170f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            historyDeleteConfirmButton = CreateButton(historyDeletePanel.transform, "ConfirmDeleteButton", "删除", new Vector2(115f, -88f), new Vector2(170f, 48f), ExitButtonColor);

            historyErrorPanel = CreatePanel(root, "HistoryErrorPanel", Vector2.zero, new Vector2(680f, 260f), new Color(0.04f, 0.05f, 0.07f, 0.96f));
            CreateText(historyErrorPanel.transform, "Title", "历史记录错误", new Vector2(0f, 82f), new Vector2(520f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            historyErrorText = CreateText(historyErrorPanel.transform, "Message", string.Empty, new Vector2(0f, -5f), new Vector2(560f, 110f), 18, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.42f, 1f));
            historyErrorBackButton = CreateButton(historyErrorPanel.transform, "BackButton", "返回", new Vector2(0f, -92f), new Vector2(150f, 44f), new Color(0.24f, 0.36f, 0.42f, 1f));

            settingsPanel = CreatePanel(root, "SettingsPanel", new Vector2(0f, 0f), new Vector2(820f, 500f), new Color(0.04f, 0.05f, 0.07f, 0.92f));
            settingsTitleText = CreateText(settingsPanel.transform, "Title", "设置", new Vector2(0f, 210f), new Vector2(480f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
            settingsPageText = CreateText(settingsPanel.transform, "Page", "显示、纠错与连接", new Vector2(0f, 174f), new Vector2(700f, 30f), 18, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));

            settingsGeneralGroup = new GameObject("GeneralSettings");
            settingsGeneralGroup.transform.SetParent(settingsPanel.transform, false);
            CreateText(settingsGeneralGroup.transform, "FontLabel", "字体大小", new Vector2(-240f, 108f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            fontMinusButton = CreateButton(settingsGeneralGroup.transform, "FontMinusButton", "-", new Vector2(78f, 108f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            fontValueText = CreateText(settingsGeneralGroup.transform, "FontValue", string.Empty, new Vector2(174f, 108f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            fontPlusButton = CreateButton(settingsGeneralGroup.transform, "FontPlusButton", "+", new Vector2(270f, 108f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "UiLabel", "界面大小", new Vector2(-240f, 58f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            uiMinusButton = CreateButton(settingsGeneralGroup.transform, "UiMinusButton", "-", new Vector2(78f, 58f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            uiValueText = CreateText(settingsGeneralGroup.transform, "UiValue", string.Empty, new Vector2(174f, 58f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            uiPlusButton = CreateButton(settingsGeneralGroup.transform, "UiPlusButton", "+", new Vector2(270f, 58f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "SubtitleLabel", "对话字幕", new Vector2(-240f, 8f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            subtitleValueText = CreateText(settingsGeneralGroup.transform, "SubtitleValue", string.Empty, new Vector2(110f, 8f), new Vector2(140f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            subtitleChangeButton = CreateButton(settingsGeneralGroup.transform, "SubtitleChangeButton", "切换", new Vector2(293f, 8f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionSourceLabel", "纠错来源", new Vector2(-240f, -42f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionSourceValueText = CreateText(settingsGeneralGroup.transform, "CorrectionSourceValue", string.Empty, new Vector2(110f, -42f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionSourceChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionSourceChangeButton", "切换", new Vector2(293f, -42f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionAppearanceLabel", "辅助角色外观", new Vector2(-240f, -92f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionAppearanceValueText = CreateText(settingsGeneralGroup.transform, "CorrectionAppearanceValue", string.Empty, new Vector2(110f, -92f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionAppearanceChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionAppearanceChangeButton", "切换", new Vector2(293f, -92f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "CorrectionStyleLabel", "纠错方式", new Vector2(-240f, -142f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            correctionStyleValueText = CreateText(settingsGeneralGroup.transform, "CorrectionStyleValue", string.Empty, new Vector2(110f, -142f), new Vector2(190f, 44f), 18, TextAnchor.MiddleCenter, Color.white);
            correctionStyleChangeButton = CreateButton(settingsGeneralGroup.transform, "CorrectionStyleChangeButton", "切换", new Vector2(293f, -142f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

            CreateText(settingsGeneralGroup.transform, "TransportLabel", "数据通道", new Vector2(-240f, -184f), new Vector2(320f, 34f), 19, TextAnchor.MiddleLeft, Color.white);
            transportStatusText = CreateText(settingsGeneralGroup.transform, "TransportStatus", "正在连接", new Vector2(196f, -184f), new Vector2(360f, 34f), 18, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));
            correctionSettingsStatusText = CreateText(settingsGeneralGroup.transform, "CorrectionSettingsStatus", string.Empty, new Vector2(0f, -222f), new Vector2(700f, 24f), 14, TextAnchor.MiddleCenter, new Color(0.72f, 0.8f, 0.86f, 1f));

            requestPanel = CreatePanel(root, "RequestPanel", new Vector2(0f, 0f), new Vector2(700f, 380f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            requestTitleText = CreateText(requestPanel.transform, "Title", "场景与角色需求", new Vector2(0f, 146f), new Vector2(640f, 42f), 26, TextAnchor.MiddleCenter, Color.white);
            requestStatusText = CreateText(requestPanel.transform, "Status", "正在聆听……", new Vector2(0f, 104f), new Vector2(640f, 34f), 20, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));
            requestTranscriptText = CreateText(requestPanel.transform, "Transcript", "识别文本：-", new Vector2(0f, 28f), new Vector2(620f, 112f), 22, TextAnchor.MiddleCenter, Color.white);
            requestErrorText = CreateText(requestPanel.transform, "Error", string.Empty, new Vector2(0f, -64f), new Vector2(620f, 34f), 18, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.35f, 1f));
            listenButton = CreateButton(requestPanel.transform, "ListenButton", "开始录音", new Vector2(-110f, -142f), new Vector2(150f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            confirmButton = CreateButton(requestPanel.transform, "ConfirmButton", "确认", new Vector2(110f, -142f), new Vector2(150f, 54f), new Color(0.12f, 0.52f, 0.38f, 1f));

            // Fixed Task Selection Panel
            taskSelectionPanel = CreatePanel(root, "TaskSelectionPanel", new Vector2(0f, 0f), new Vector2(900f, 520f), new Color(0.04f, 0.05f, 0.07f, 0.95f));
            CreateText(taskSelectionPanel.transform, "Title", "选择练习任务", new Vector2(0f, 220f), new Vector2(800f, 44f), 28, TextAnchor.MiddleCenter, Color.white);

            BuildTaskButtons();

            loadingPanel = CreatePanel(root, "LoadingPanel", new Vector2(0f, 0f), new Vector2(540f, 220f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            loadingText = CreateText(loadingPanel.transform, "LoadingText", "正在加载场景与角色……", new Vector2(0f, 0f), new Vector2(480f, 80f), 26, TextAnchor.MiddleCenter, Color.white);

            subtitlePanel = CreatePanel(root, "SubtitlePanel", new Vector2(DialoguePanelCenterX, DialoguePanelVisibleCenterY), new Vector2(DialoguePanelWidth, DialoguePanelVisibleHeight), new Color(0f, 0f, 0f, 0.62f));
            subtitlePanel.AddComponent<RectMask2D>();
            subtitlePanelRect = subtitlePanel.GetComponent<RectTransform>();
            
            subtitleTextContainer = new GameObject("TextContainer");
            subtitleTextContainer.transform.SetParent(subtitlePanel.transform, false);
            subtitleTextContainerRect = subtitleTextContainer.AddComponent<RectTransform>();
            subtitleTextContainerRect.anchoredPosition = new Vector2(DialogueContentCenterX, 35f);
            subtitleTextContainerRect.sizeDelta = new Vector2(DialogueContentWidth, 64f);

            playerSubtitleText = CreateText(subtitleTextContainer.transform, "PlayerSubtitle", "你：-", new Vector2(0f, 22f), new Vector2(DialogueContentWidth, 26f), 18, TextAnchor.UpperLeft, new Color(0.45f, 0.9f, 1f, 1f));
            avatarSubtitleText = CreateText(subtitleTextContainer.transform, "AvatarSubtitle", "角色：-", new Vector2(0f, -14f), new Vector2(DialogueContentWidth, 42f), 19, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.36f, 1f));
            ConfigureDialogueText(playerSubtitleText);
            ConfigureDialogueText(avatarSubtitleText);

            experimentDebugText = CreateText(subtitlePanel.transform, "ExperimentDebug", string.Empty, new Vector2(DialogueContentCenterX, 78f), new Vector2(DialogueContentWidth, 18f), 13, TextAnchor.MiddleLeft, new Color(0.72f, 0.8f, 0.86f, 1f));
            correctionFeedbackText = CreateText(subtitlePanel.transform, "CorrectionFeedback", string.Empty, new Vector2(DialogueContentCenterX, -17f), new Vector2(DialogueContentWidth, 28f), 16, TextAnchor.UpperLeft, new Color(0.78f, 0.95f, 0.74f, 1f));
            correctionStatusText = CreateText(subtitlePanel.transform, "CorrectionStatus", string.Empty, new Vector2(DialogueContentCenterX, -46f), new Vector2(DialogueContentWidth, 24f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            dialogueStatusText = CreateText(subtitlePanel.transform, "DialogueStatus", "准备就绪", new Vector2(DialogueContentCenterX, -70f), new Vector2(DialogueContentWidth, 20f), 16, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 1f, 1f));
            ConfigureDialogueText(experimentDebugText);
            ConfigureDialogueText(correctionFeedbackText);
            ConfigureDialogueText(correctionStatusText);
            ConfigureDialogueText(dialogueStatusText);

            dialogueListenButton = CreateButton(subtitlePanel.transform, "DialogueListenButton", "发言", new Vector2(DialogueButtonCenterX, -60f), new Vector2(110f, 40f), new Color(0.12f, 0.52f, 0.38f, 1f));

            taskGoalCanvas = CreateTaskGoalCanvas();
            UpdateTaskGoalCanvasFacingUser();
            taskGoalPanel = CreatePanel(taskGoalCanvas.transform, "ReadOnlyTaskGoalPanel", Vector2.zero, TaskGoalPanelSize, new Color(0.03f, 0.04f, 0.06f, 0.84f));
            CreateText(taskGoalPanel.transform, "Title", "任务目标", new Vector2(0f, 118f), new Vector2(280f, 38f), 26, TextAnchor.MiddleCenter, Color.white);
            taskGoalText = CreateText(taskGoalPanel.transform, "GoalStateText", string.Empty, new Vector2(0f, -24f), new Vector2(280f, 218f), 20, TextAnchor.UpperLeft, new Color(0.86f, 0.92f, 1f, 1f));
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
            else if (ExperimentRuntimePlatform.IsPicoCollection)
            {
                Debug.Log("[ExperimentRuntime] PICO formal collection UI ready. "
                    + "qualification=Collection; dataOrigin=participant_collection; collectionEligible=true; "
                    + "profile=pico_lab", this);
            }

            BindButtons();
            CaptureBaseFontSizes(worldCanvas.transform);
            ApplyUserSettings(SceneTalkUserSettingsStore.Current);
        }

        private void BuildExperimentPanels(Transform root)
        {
            experimentExitConfirmPanel = CreatePanel(root, "ExperimentExitConfirmPanel", Vector2.zero, new Vector2(680f, 320f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentExitConfirmPanel.transform, "Title", "退出实验？", new Vector2(0f, 104f), new Vector2(580f, 48f), 30, TextAnchor.MiddleCenter, Color.white);
            CreateText(experimentExitConfirmPanel.transform, "Message", "实验尚未完成。系统会保留历史记录，你可以稍后从“实验历史”继续。", new Vector2(0f, 25f), new Vector2(580f, 100f), 19, TextAnchor.MiddleCenter, new Color(.84f, .9f, 1f, 1f));
            experimentExitCancelButton = CreateButton(experimentExitConfirmPanel.transform, "ContinueExperimentButton", "继续实验", new Vector2(-135f, -102f), new Vector2(230f, 48f), new Color(.12f, .52f, .38f, 1f));
            experimentExitConfirmButton = CreateButton(experimentExitConfirmPanel.transform, "ConfirmExitExperimentButton", "退出到主页", new Vector2(135f, -102f), new Vector2(210f, 48f), ExitButtonColor);

            experimentHistoryListPanel = CreatePanel(root, "ExperimentHistoryListPanel", Vector2.zero, new Vector2(860f, 520f), new Color(.04f, .05f, .07f, .96f));
            CreateText(experimentHistoryListPanel.transform, "Title", "实验历史", new Vector2(0f, 220f), new Vector2(680f, 46f), 30, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryEmptyText = CreateText(experimentHistoryListPanel.transform, "Empty", "暂无实验历史。", new Vector2(0f, 10f), new Vector2(680f, 60f), 22, TextAnchor.MiddleCenter, new Color(.75f, .82f, .88f, 1f));
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
            experimentHistoryPreviousButton = CreateButton(experimentHistoryListPanel.transform, "ExperimentHistoryPreviousButton", "上一页", new Vector2(-250f, -225f), new Vector2(150f, 44f), new Color(.24f, .36f, .42f, 1f));
            experimentHistoryPageText = CreateText(experimentHistoryListPanel.transform, "Page", "第 1 / 1 页", new Vector2(0f, -225f), new Vector2(200f, 40f), 18, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryNextButton = CreateButton(experimentHistoryListPanel.transform, "ExperimentHistoryNextButton", "下一页", new Vector2(250f, -225f), new Vector2(150f, 44f), new Color(.24f, .36f, .42f, 1f));

            experimentHistoryActionsPanel = CreatePanel(root, "ExperimentHistoryActionsPanel", Vector2.zero, new Vector2(760f, 400f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryActionsPanel.transform, "Title", "实验记录", new Vector2(0f, 155f), new Vector2(650f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryActionsSummaryText = CreateText(experimentHistoryActionsPanel.transform, "Summary", string.Empty, new Vector2(0f, 66f), new Vector2(650f, 120f), 18, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));
            experimentHistoryContinueButton = CreateButton(experimentHistoryActionsPanel.transform, "ContinueExperimentRecordButton", "继续", new Vector2(-220f, -102f), new Vector2(180f, 48f), new Color(.12f, .52f, .38f, 1f));
            experimentHistoryViewButton = CreateButton(experimentHistoryActionsPanel.transform, "ViewExperimentRecordButton", "查看记录", new Vector2(0f, -102f), new Vector2(180f, 48f), new Color(.16f, .38f, .68f, 1f));
            experimentHistoryDeleteButton = CreateButton(experimentHistoryActionsPanel.transform, "DeleteExperimentRecordButton", "删除", new Vector2(220f, -102f), new Vector2(180f, 48f), ExitButtonColor);

            experimentHistoryRecordPanel = CreatePanel(root, "ExperimentHistoryRecordPanel", Vector2.zero, new Vector2(950f, 570f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryRecordPanel.transform, "Title", "实验记录详情", new Vector2(0f, 252f), new Vector2(780f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryRecordText = CreateScrollableText(experimentHistoryRecordPanel.transform, "ExperimentRecord", new Vector2(0f, 135f), new Vector2(830f, 180f), out experimentRecordScrollRect, out experimentRecordContentRect);
            CreateText(experimentHistoryRecordPanel.transform, "EntriesTitle", "对话与问卷", new Vector2(0f, 28f), new Vector2(760f, 30f), 18, TextAnchor.MiddleCenter, new Color(.78f, .88f, 1f, 1f));
            for (var i = 0; i < experimentRecordEntryButtons.Length; i++)
            {
                var index = i;
                experimentRecordEntryButtons[i] = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordEntry" + (i + 1), string.Empty,
                    new Vector2(0f, -8f - i * 43f), new Vector2(790f, 36f), new Color(.14f, .28f, .4f, 1f));
                var label = experimentRecordEntryButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) { label.alignment = TextAlignmentOptions.Left; label.fontSize = 15f; }
                experimentRecordEntryButtons[i].onClick.AddListener(() => OpenExperimentRecordEntry(index));
            }
            experimentRecordPreviousButton = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordPreviousButton", "上一页", new Vector2(-220f, -252f), new Vector2(140f, 38f), new Color(.24f, .36f, .42f, 1f));
            experimentRecordEntriesPageText = CreateText(experimentHistoryRecordPanel.transform, "EntriesPage", "第 1 / 1 页", new Vector2(0f, -252f), new Vector2(180f, 34f), 16, TextAnchor.MiddleCenter, Color.white);
            experimentRecordNextButton = CreateButton(experimentHistoryRecordPanel.transform, "ExperimentRecordNextButton", "下一页", new Vector2(220f, -252f), new Vector2(140f, 38f), new Color(.24f, .36f, .42f, 1f));

            experimentHistoryConversationPanel = CreatePanel(root, "ExperimentHistoryConversationDetailPanel", Vector2.zero, new Vector2(900f, 550f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryConversationPanel.transform, "Title", "对话详情", new Vector2(0f, 235f), new Vector2(760f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryConversationSummaryText = CreateText(experimentHistoryConversationPanel.transform, "Summary", string.Empty, new Vector2(0f, 174f), new Vector2(760f, 78f), 16, TextAnchor.MiddleCenter, new Color(.82f, .9f, 1f, 1f));
            experimentHistoryConversationBodyText = CreateScrollableText(experimentHistoryConversationPanel.transform, "ExperimentConversation", new Vector2(0f, -40f), new Vector2(790f, 330f), out experimentConversationScrollRect, out experimentConversationContentRect);

            experimentHistoryQuestionnairePanel = CreatePanel(root, "ExperimentHistoryQuestionnaireDetailPanel", Vector2.zero, new Vector2(900f, 550f), new Color(.04f, .05f, .07f, .97f));
            CreateText(experimentHistoryQuestionnairePanel.transform, "Title", "问卷详情", new Vector2(0f, 235f), new Vector2(760f, 42f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryQuestionnaireText = CreateScrollableText(experimentHistoryQuestionnairePanel.transform, "ExperimentQuestionnaire", new Vector2(0f, -5f), new Vector2(800f, 410f), out experimentQuestionnaireScrollRect, out experimentQuestionnaireContentRect);

            experimentHistoryDeletePanel = CreatePanel(root, "ExperimentHistoryDeleteConfirmPanel", Vector2.zero, new Vector2(650f, 300f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentHistoryDeletePanel.transform, "Title", "删除实验记录？", new Vector2(0f, 98f), new Vector2(560f, 46f), 29, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryDeleteMessageText = CreateText(experimentHistoryDeletePanel.transform, "Message", string.Empty, new Vector2(0f, 24f), new Vector2(560f, 88f), 18, TextAnchor.MiddleCenter, new Color(.84f, .9f, 1f, 1f));
            experimentHistoryDeleteCancelButton = CreateButton(experimentHistoryDeletePanel.transform, "CancelExperimentDeleteButton", "取消", new Vector2(-115f, -93f), new Vector2(170f, 46f), new Color(.24f, .36f, .42f, 1f));
            experimentHistoryDeleteConfirmButton = CreateButton(experimentHistoryDeletePanel.transform, "ConfirmExperimentDeleteButton", "删除", new Vector2(115f, -93f), new Vector2(170f, 46f), ExitButtonColor);

            experimentHistoryErrorPanel = CreatePanel(root, "ExperimentHistoryErrorPanel", Vector2.zero, new Vector2(680f, 270f), new Color(.04f, .05f, .07f, .98f));
            CreateText(experimentHistoryErrorPanel.transform, "Title", "实验历史错误", new Vector2(0f, 88f), new Vector2(580f, 44f), 28, TextAnchor.MiddleCenter, Color.white);
            experimentHistoryErrorText = CreateText(experimentHistoryErrorPanel.transform, "Message", string.Empty, new Vector2(0f, -5f), new Vector2(570f, 120f), 18, TextAnchor.MiddleCenter, new Color(1f, .55f, .42f, 1f));
        }

        private void BindButtons()
        {
            if (pilotButton != null)
            {
                pilotButton.onClick.RemoveAllListeners();
                pilotButton.onClick.AddListener(OpenExperimentHistory);
            }

            if (historyExportButton != null)
            {
                historyExportButton.onClick.RemoveAllListeners();
                historyExportButton.onClick.AddListener(StartHistoryExport);
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

        private void ConfigureHistoryExportCoordinator()
        {
            var host = orchestrator == null ? gameObject : orchestrator.gameObject;
            historyExportCoordinator = host.GetComponent<PicoHistoryExportCoordinator>()
                ?? host.AddComponent<PicoHistoryExportCoordinator>();
            var history = host.GetComponent<ExperimentHistoryService>()
                ?? FindFirstObjectByType<ExperimentHistoryService>(FindObjectsInactive.Include)
                ?? host.AddComponent<ExperimentHistoryService>();
            var memory = host.GetComponent<LearningMemoryService>()
                ?? FindFirstObjectByType<LearningMemoryService>(FindObjectsInactive.Include)
                ?? host.AddComponent<LearningMemoryService>();
            historyExportCoordinator.Configure(orchestrator?.RuntimeConfig, history, memory);
        }

        private void StartHistoryExport()
        {
            ConfigureHistoryExportCoordinator();
            if (homeExperimentMessageText != null)
            {
                homeExperimentMessageText.text = string.Empty;
            }
            if (!historyExportCoordinator.TryStartExport(out var error) && homeExperimentMessageText != null)
            {
                homeExperimentMessageText.text = HumanizeHistoryExportError(error, historyExportCoordinator.LastResult?.message);
            }
        }

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
            historyExportCoordinator?.ClearCompletedStatus();
            var coordinator = GetExperimentCoordinator();
            if (coordinator == null) return;
            if (homeExperimentMessageText != null) homeExperimentMessageText.text = string.Empty;
            if (!coordinator.StartNewExperiment(ExperimentKind.Pilot, out var error))
                homeExperimentMessageText.text = HumanizeExperimentError(error);
        }

        private void EnterFormalExperiment()
        {
            historyExportCoordinator?.ClearCompletedStatus();
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

            if (ExperimentRuntimePlatform.IsPicoCollection)
            {
                sessionPreparationBlocked = true;
                Debug.LogError("[ExperimentRuntime] PICO collection session is not armed.", this);
                Refresh();
                return;
            }

            // An unarmed editor start is a rehearsal. Explicit PICO device-validation test
            // mode also remains ineligible for participant collection.
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
                taskButtons.Add(CreateButton(taskSelectionPanel.transform, $"Task{i + 1}Button",
                    SceneTalkChineseUiText.TaskName(task.taskId, task.displayName), new Vector2(x, y),
                    new Vector2(380f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f)));
                CreateText(taskSelectionPanel.transform, $"Task{i + 1}Context",
                    SceneTalkChineseUiText.TaskContext(task.taskId, task.context) + "\n开场白：" + task.initialQuestion,
                    new Vector2(x, y - 80f), new Vector2(380f, 88f), 15, TextAnchor.UpperCenter,
                    new Color(0.8f, 0.8f, 0.8f, 1f));
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
                formalModeStatusTexts[code] = CreateText(formalModeSelectionPanel.transform, code + "Status", "可选择",
                    new Vector2(x, y - 52f), new Vector2(360f, 26f), 15, TextAnchor.MiddleCenter, new Color(.75f, .9f, .75f, 1f));
            }
        }

        private static string FriendlyConditionLabel(FormalConditionCode code) => code switch
        {
            FormalConditionCode.NE => "NE — 对话角色直接纠错",
            FormalConditionCode.NR => "NR — 对话角色重述反馈",
            FormalConditionCode.SE => "SE — 辅助角色直接纠错",
            FormalConditionCode.SR => "SR — 辅助角色重述反馈",
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
            RefreshHistoryExportUi(showMain);
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

        private void RefreshHistoryExportUi(bool showMain)
        {
            if (!showMain || historyExportButton == null) return;
            if (historyExportCoordinator == null)
            {
                ConfigureHistoryExportCoordinator();
            }

            var busy = historyExportCoordinator.IsBusy;
            experimentPilotButton.interactable = !busy;
            experimentFormalButton.interactable = !busy;
            pilotButton.interactable = !busy;
            historyButton.interactable = !busy;
            historyExportButton.interactable = !busy;
            settingsButton.interactable = !busy;
            quitButton.interactable = !busy;
            SetButtonLabel(historyExportButton, busy ? "正在导出…" : "导出历史数据");

            if (homeExperimentMessageText == null) return;
            switch (historyExportCoordinator.State)
            {
                case PicoHistoryExportState.ProbingUsb:
                    homeExperimentMessageText.color = new Color(.78f, .88f, 1f, 1f);
                    homeExperimentMessageText.text = "正在检查 USB 数据线和电脑导出服务…";
                    break;
                case PicoHistoryExportState.BuildingSnapshot:
                    homeExperimentMessageText.color = new Color(.78f, .88f, 1f, 1f);
                    homeExperimentMessageText.text = "正在整理实验历史数据…";
                    break;
                case PicoHistoryExportState.Uploading:
                    homeExperimentMessageText.color = new Color(.78f, .88f, 1f, 1f);
                    homeExperimentMessageText.text = "正在通过 USB 导出到电脑…";
                    break;
                case PicoHistoryExportState.Succeeded:
                    var success = historyExportCoordinator.LastResult;
                    homeExperimentMessageText.color = new Color(.55f, .9f, .65f, 1f);
                    homeExperimentMessageText.text = success == null
                        ? "历史数据已导出到电脑。"
                        : $"导出成功：{success.experimentCount} 条实验记录、{success.questionnaireCount} 份问卷。\n电脑目录：{success.exportDirectory}";
                    break;
                case PicoHistoryExportState.Failed:
                    var failed = historyExportCoordinator.LastResult;
                    homeExperimentMessageText.color = new Color(1f, .58f, .42f, 1f);
                    homeExperimentMessageText.text = HumanizeHistoryExportError(
                        failed?.errorCode,
                        failed?.message);
                    break;
            }
        }

        private static string HumanizeHistoryExportError(string code, string detail)
        {
            return code switch
            {
                "history_export_in_progress" => "历史数据正在导出，请稍候。",
                "history_export_empty" => "暂无可导出的实验历史数据。",
                "history_export_usb_unavailable" => "未检测到电脑导出服务。请连接数据线并启动电脑端后台。",
                "history_export_service_incompatible" => "电脑端导出服务版本不兼容，请重新启动最新后台。",
                "history_export_usb_endpoint_invalid" => "USB 导出地址配置无效，请联系实验人员。",
                "payload_too_large" => "历史数据过大，电脑端拒绝接收。",
                "export_write_failed" => "电脑无法写入导出目录，请检查磁盘空间和目录权限。",
                "xlsx_generation_failed" => "电脑生成问卷 Excel 文件失败。",
                "export_id_conflict" => "电脑上存在冲突的同编号导出，请重新点击导出。",
                "history_service_unavailable" => "PICO 历史数据服务不可用。",
                "history_export_snapshot_failed" => "整理 PICO 历史数据失败：" + SafeExportDetail(detail),
                _ => "历史数据导出失败：" + SafeExportDetail(detail)
            };
        }

        private static string SafeExportDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail)) return "请检查数据线和电脑端后台。";
            var trimmed = detail.Trim();
            return trimmed.Length <= 120 ? trimmed : trimmed.Substring(0, 120) + "…";
        }

        public void RefreshExternalState() => Refresh();

        public void ShowDemoRankingPreview(bool pilot)
        {
            if (demoRankingPanel == null || demoRankingText == null) return;
            demoRankingText.text = pilot
                ? "预实验最终排序\n\n1  仅语音\n2  悬浮球\n3  人形辅助角色\n\n演示操作员预览\n仅用于自动填充演示"
                : "正式条件最终排序\n\n1  NE\n2  NR\n3  SE\n4  SR\n\n演示操作员预览\n仅用于自动填充演示";
            demoRankingPanel.SetActive(true);
            demoRankingPanel.transform.SetAsLastSibling();
            if (demoBanner != null) demoBanner.transform.SetAsLastSibling();
        }

        public void ShowRehearsalRanking(bool pilot)
        {
            if (demoRankingPanel == null || demoRankingText == null) return;
            demoRankingText.text = pilot
                ? "预实验演练排序\n\n请使用演练控制窗口执行质量验证排序提交。"
                : "正式实验演练排序\n\n请使用参与者收集流程完成交互式最终排序。";
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
                SetActive(demoBanner, false);
                SetActive(demoStatusPanel, false);
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
            demoStatusText.text = $"模式：{(demo.IsFormalDemo ? "编辑器正式实验演示" : "编辑器预实验演示")}\n"
                + $"条件：{condition ?? "尚未准备"}\n任务：{SceneTalkChineseUiText.TaskName(demo.CurrentTaskId, demo.CurrentTaskId)}\n"
                + $"序号：{Mathf.Max(0, demo.CurrentPosition + 1)}/{demo.TotalConditions}\n"
                + $"角色：演示角色（{avatar}）\n语音：编辑器演示\n可用于正式收集：否\n"
                + "编辑器演示资源——未经正式收集批准";
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
            var taskName = SceneTalkChineseUiText.TaskName(tracker.Context.taskId, tracker.Context.taskId);
            var builder = new System.Text.StringBuilder(taskName).AppendLine();
            for (var i = 0; i < tracker.Goals.Count; i++)
            {
                var goal = tracker.Goals[i];
                if (goal.state != GoalProgressState.Confirmed && i != tracker.ActiveGoalIndex) continue;
                builder.Append(goal.state == GoalProgressState.Confirmed ? "[✓] "
                    : goal.state == GoalProgressState.Candidate ? "[…] "
                    : goal.state == GoalProgressState.Rejected ? "[↻] " : "[ ] ")
                    .AppendLine(SceneTalkChineseUiText.Goal(goal.goalId, goal.goalText));
            }
            if (tracker.SequenceState == GoalSequenceState.AwaitingParticipantTurn)
                builder.AppendLine().AppendLine("正在准备下一目标……");
            else if (tracker.SequenceState == GoalSequenceState.AwaitingAvatarReply)
                builder.AppendLine().AppendLine("正在等待本轮语音完整播放……");
            else if (tracker.SequenceState == GoalSequenceState.Completed)
                builder.AppendLine().AppendLine("全部目标已完成。");
            builder.AppendLine().Append("已完成 ").Append(tracker.ConfirmedCount).Append(" / ").Append(tracker.Goals.Count);
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
                var status = item == null ? "不可用" : item.status == ConditionRunStatus.Completed ? "已完成"
                    : item.status == ConditionRunStatus.TechnicalInvalid ? "可重试"
                    : item.status == ConditionRunStatus.Assigned ? "可选择"
                    : item.status == ConditionRunStatus.Running || item.status == ConditionRunStatus.AwaitingQuestionnaire
                        || item.status == ConditionRunStatus.QuestionnaireInProgress ? "继续" : "进行中";
                pair.Value.interactable = item != null && (item.status == ConditionRunStatus.Assigned
                    || item.status == ConditionRunStatus.TechnicalInvalid
                    || item.status == ConditionRunStatus.Running
                    || item.status == ConditionRunStatus.AwaitingQuestionnaire
                    || item.status == ConditionRunStatus.QuestionnaireInProgress);
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
                settingsTitleText.text = "设置";
            }

            if (settingsPageText != null)
            {
                settingsPageText.text = "显示、纠错与连接";
            }

            if (transportStatusText != null)
            {
                var router = GatewayTransportRouter.Active;
                transportStatusText.text = router == null
                    ? "局域网备用"
                    : router.ChineseStatus;
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
                subtitleValueText.text = settings.hideDialogueSubtitles ? "隐藏" : "显示";
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
                    : "不适用";
            }

            SetInteractable(correctionSourceChangeButton, canChangeCorrection);
            SetInteractable(correctionAppearanceChangeButton, canChangeAppearance);
            SetInteractable(correctionStyleChangeButton, canChangeCorrection);
            SetButtonLabel(correctionSourceChangeButton, canChangeCorrection ? "切换" : "已锁定");
            SetButtonLabel(
                correctionAppearanceChangeButton,
                !canChangeCorrection ? "已锁定" : usesAssistantAgent ? "切换" : "不适用");
            SetButtonLabel(correctionStyleChangeButton, canChangeCorrection ? "切换" : "已锁定");

            if (correctionSettingsStatusText != null)
            {
                correctionSettingsStatusText.text = !canChangeCorrection
                    ? SceneTalkChineseUiText.Error(orchestrator.CorrectionSettingLockReason)
                    : usesAssistantAgent
                        ? "辅助角色外观会全局保存，并在正式实验开始后锁定。"
                        : "选择辅助角色后可以更改其外观。";
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
                    $"[{SceneTalkChineseUiText.ExperimentKindName(item.kind)}] {item.participantId}    {FormatHistoryTime(item.updatedAtUnixMs)}\n"
                    + $"状态：{FriendlyExperimentStatus(item.status)}"
                    + (item.kind == ExperimentKind.Formal && !string.IsNullOrWhiteSpace(item.assistantEmbodimentSnapshot)
                        ? $"  |  外观：{ResolveCorrectionAppearanceDisplayName(item.assistantEmbodimentSnapshot)}"
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
                experimentHistoryPageText.text = $"第 {page.pageIndex + 1} / {page.TotalPages} 页";
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
                    $"{SceneTalkChineseUiText.ExperimentKindName(summary.kind)}  |  {FriendlyExperimentStatus(summary.status)}\n"
                    + $"参与者：{summary.participantId}\n"
                    + $"更新时间：{FormatHistoryTime(summary.updatedAtUnixMs)}"
                    + (summary.kind == ExperimentKind.Formal && !string.IsNullOrWhiteSpace(summary.assistantEmbodimentSnapshot)
                        ? $"\n外观：{ResolveCorrectionAppearanceDisplayName(summary.assistantEmbodimentSnapshot)}"
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
                        label = $"对话  {SceneTalkChineseUiText.TaskName(string.Empty, conversation.title)}  |  {conversation.turnCount} 轮  |  {FormatHistoryTime(conversation.updatedAtUnixMs)}"
                    });
                }
                foreach (var questionnaire in detail.questionnaires ?? System.Array.Empty<ExperimentQuestionnaireRecord>())
                {
                    var session = questionnaire.session;
                    experimentRecordEntries.Add(new ExperimentRecordEntry
                    {
                        isConversation = false,
                        id = questionnaire.questionnaireRecordId,
                        label = $"问卷  {session?.questionnaireId ?? "-"}  |  {SceneTalkChineseUiText.QuestionnaireStatusName(session?.completionStatus ?? QuestionnaireCompletionStatus.NotStarted)}  |  {(session?.completionRate ?? 0f):P0}"
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
                experimentRecordEntriesPageText.text = $"第 {experimentRecordEntryPage + 1} / {pages} 页";
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
                    $"{SceneTalkChineseUiText.TaskName(detail.summary.taskType, detail.summary.title)}  |  {SceneTalkChineseUiText.ExperimentKindName(detail.summary.experimentKind)}  |  {detail.summary.turnCount} 轮\n"
                    + $"任务：{SceneTalkChineseUiText.TaskName(detail.summary.taskType, detail.summary.taskType)}  |  更新时间：{FormatHistoryTime(detail.summary.updatedAtUnixMs)}";
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
                ? "所选实验记录已不可用。"
                : $"永久删除参与者 {selected.participantId} 的实验记录？\n这会删除数据库记录及其缓存数据。";
        }

        private void RefreshExperimentHistoryError(bool isVisible)
        {
            if (isVisible && experimentHistoryErrorText != null)
                experimentHistoryErrorText.text = SceneTalkChineseUiText.Error(
                    GetExperimentCoordinator()?.ErrorMessage ?? "Experiment history operation failed.");
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
            builder.AppendLine($"实验：{SceneTalkChineseUiText.ExperimentKindName(detail.summary.kind)}");
            builder.AppendLine($"参与者：{detail.summary.participantId}");
            builder.AppendLine($"会话：{detail.summary.sessionId}");
            builder.AppendLine($"状态：{FriendlyExperimentStatus(detail.summary.status)}  |  创建时间：{FormatHistoryTime(detail.summary.createdAtUnixMs)}");
            if (detail.summary.kind == ExperimentKind.Formal
                && !string.IsNullOrWhiteSpace(detail.summary.assistantEmbodimentSnapshot))
                builder.AppendLine("辅助角色外观："
                    + ResolveCorrectionAppearanceDisplayName(detail.summary.assistantEmbodimentSnapshot));

            builder.AppendLine();
            builder.AppendLine("尝试记录");
            var attempts = detail.attempts ?? System.Array.Empty<ExperimentAttemptRecord>();
            if (attempts.Length == 0)
                builder.AppendLine("暂无尝试记录。");
            foreach (var attempt in attempts.OrderBy(item => item.attemptIndex).ThenBy(item => item.startedAtUnixMs))
            {
                builder.Append($"第 {attempt.attemptIndex} 次：{ResolveText(attempt.conditionKey)} / {SceneTalkChineseUiText.TaskName(attempt.taskId, attempt.taskId)} - {SceneTalkChineseUiText.ExperimentAttemptStatusName(attempt.status)}");
                if (!string.IsNullOrWhiteSpace(attempt.completionReason)) builder.Append(" (" + attempt.completionReason + ")");
                builder.AppendLine();
            }

            var ranking = detail.rankings?.FirstOrDefault()?.response;
            if (ranking != null)
            {
                builder.AppendLine();
                builder.AppendLine("最终排序");
                var ranked = (ranking.rankings ?? System.Array.Empty<PreferenceRankEntry>())
                    .OrderBy(item => item.rank)
                    .Select(item => $"{item.rank}. {(string.IsNullOrWhiteSpace(item.conditionCode) ? item.embodimentCondition : item.conditionCode)}");
                builder.AppendLine("排序：" + string.Join("  ", ranked));
                var preferred = string.IsNullOrWhiteSpace(ranking.preferredConditionCode)
                    ? ranking.preferredEmbodimentCondition : ranking.preferredConditionCode;
                builder.AppendLine($"首选：{ResolveText(preferred)}  |  原因：{ResolveText(ranking.reason)}");
            }
            return builder.ToString();
        }

        private static string BuildQuestionnaireDetailText(ExperimentQuestionnaireRecord record)
        {
            var session = record.session ?? new QuestionnaireSession();
            var builder = new StringBuilder();
            builder.AppendLine($"问卷：{ResolveText(session.questionnaireId)}");
            builder.AppendLine($"状态：{SceneTalkChineseUiText.QuestionnaireStatusName(session.completionStatus)}  |  完成度：{session.completionRate:P0}  |  有缺失项：{SceneTalkChineseUiText.YesNo(session.hasMissing)}");
            builder.AppendLine($"任务：{SceneTalkChineseUiText.TaskName(session.taskId, session.taskId)}  |  条件运行：{ResolveText(session.conditionRunId)}");
            builder.AppendLine();
            builder.AppendLine("分区得分");
            foreach (var score in session.sectionScores ?? System.Array.Empty<QuestionnaireScoreResult>())
                builder.AppendLine($"{score.sectionId}：平均分={score.mean:0.##}，已回答={score.answeredCount}/{score.itemCount}，有缺失={SceneTalkChineseUiText.YesNo(score.hasMissing)}");
            builder.AppendLine();
            builder.AppendLine("回答记录");
            foreach (var prompt in record.prompts ?? System.Array.Empty<QuestionnairePromptSnapshot>())
            {
                var response = session.responses?.FirstOrDefault(item => item.itemId == prompt.itemId);
                builder.AppendLine($"[{prompt.sectionId}] {ResolveText(prompt.promptChinese)}");
                builder.Append("回答：" + ResolveText(response?.rawValue));
                if (response?.hasScoredValue == true) builder.Append($"  |  得分：{response.scoredValue:0.##}");
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
            return SceneTalkChineseUiText.ExperimentStatusName(status);
        }

        private static string HumanizeExperimentError(string error)
        {
            return SceneTalkChineseUiText.Error(error);
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
                    $"{SceneTalkChineseUiText.TaskName(item.taskType, item.title)}    {FormatHistoryTime(item.updatedAtUnixMs)}\n"
                    + $"{item.turnCount} 轮  |  {item.correctionCount} 次纠错  |  "
                    + $"{ResolveCorrectionSourceDisplayName(item.correctionProvider)} / "
                    + ResolveCorrectionStyleDisplayName(item.correctionStyle)
                    + (item.IsExperimentConversation
                        ? $"  |  实验 {ShortHistoryId(item.experimentId)} / {SceneTalkChineseUiText.ExperimentKindName(item.experimentKind)}"
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
                historyPageText.text = $"第 {page.pageIndex + 1} / {page.TotalPages} 页";
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
                    $"{SceneTalkChineseUiText.TaskName(detail.summary.taskType, detail.summary.title)}\n"
                    + (detail.summary.IsExperimentConversation
                        ? $"实验：{detail.summary.experimentId}  |  类型：{SceneTalkChineseUiText.ExperimentKindName(detail.summary.experimentKind)}\n"
                        : string.Empty)
                    + $"创建时间：{FormatHistoryTime(detail.summary.createdAtUnixMs)}  |  更新时间：{FormatHistoryTime(detail.summary.updatedAtUnixMs)}\n"
                    + $"任务：{SceneTalkChineseUiText.TaskName(detail.summary.taskType, detail.summary.scenarioId)}  |  环境：{SceneTalkChineseUiText.DisplayValue(detail.summary.environmentType)}\n"
                    + $"角色：{scene?.avatarRole?.role ?? "-"} / {SceneTalkChineseUiText.DisplayValue(appearance?.genderPresentation)}  |  "
                    + $"纠错：{ResolveCorrectionSourceDisplayName(detail.summary.correctionProvider)} / "
                    + $"{ResolveCorrectionStyleDisplayName(detail.summary.correctionStyle)}  |  "
                    + $"敏感度：{SceneTalkChineseUiText.DisplayValue(detail.settings?.feedbackSensitivity ?? "moderate")}\n"
                    + $"轮次：{detail.summary.turnCount}  |  纠错次数：{detail.summary.correctionCount}";
            }

            SetInteractable(historyContinueButton, detail.summary.CanContinue);
            SetInteractable(historyDeleteButton, detail.summary.CanDelete);
            SetButtonLabel(historyContinueButton, detail.summary.CanContinue ? "继续" : "仅限实验查看");
            SetButtonLabel(historyDeleteButton, detail.summary.CanDelete ? "删除" : "仅限实验查看");

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

            var summary = orchestrator.SelectedHistorySession?.summary;
            var title = summary == null
                ? "此对话"
                : SceneTalkChineseUiText.TaskName(summary.taskType, summary.title);
            historyDeleteMessageText.text =
                $"永久删除“{title}”？\n这会删除对应数据库记录和场景缓存文件。";
        }

        private void RefreshHistoryError(bool isVisible)
        {
            if (isVisible && historyErrorText != null)
            {
                historyErrorText.text = SceneTalkChineseUiText.Error(orchestrator.HistoryErrorMessage);
            }
        }

        private static string BuildHistoryDetailText(LearningSessionDetail detail)
        {
            var builder = new StringBuilder();
            var task = detail.settings?.condition?.task;
            var avatar = detail.sceneSnapshot?.avatarRole;
            builder.AppendLine("设置");
            builder.AppendLine($"任务情境：{SceneTalkChineseUiText.TaskContext(detail.summary?.taskType, task?.context)}");
            builder.AppendLine($"目标：{(task?.goals == null || task.goals.Length == 0 ? "-" : string.Join("；", task.goals.Select(goal => SceneTalkChineseUiText.Goal(string.Empty, goal))))}");
            builder.AppendLine($"角色语音：语速={SceneTalkChineseUiText.DisplayValue(avatar?.speakingSpeed)}，口音={SceneTalkChineseUiText.DisplayValue(avatar?.accent)}，态度={SceneTalkChineseUiText.DisplayValue(avatar?.attitude)}");
            builder.AppendLine($"场景模式：{SceneTalkChineseUiText.DisplayValue(detail.sceneSnapshot?.scene?.mode)}");
            builder.AppendLine();

            var turns = detail.turns ?? System.Array.Empty<DialogueTurnRecord>();
            foreach (var turn in turns.OrderBy(item => item.sequenceIndex))
            {
                if (turn == null)
                {
                    continue;
                }

                builder.AppendLine(turn.isOpening ? "开场" : $"第 {turn.sequenceIndex} 轮");
                if (!turn.isOpening)
                {
                    builder.AppendLine($"你：{ResolveText(turn.userText)}");
                }
                builder.AppendLine($"角色：{ResolveText(turn.assistantText)}");

                var feedback = turn.payload?.correctionFeedback;
                if (feedback != null && feedback.hasFeedback)
                {
                    builder.AppendLine($"纠错（{SceneTalkChineseUiText.DisplayValue(feedback.errorType)}）：");
                    builder.AppendLine($"  原句：{ResolveText(feedback.originalText)}");
                    builder.AppendLine($"  修改后：{ResolveText(feedback.correctedText)}");
                    var feedbackText = string.IsNullOrWhiteSpace(feedback.recastText)
                        ? feedback.feedbackText
                        : feedback.recastText;
                    builder.AppendLine($"  反馈：{ResolveText(feedbackText)}");
                }
                else if (!turn.isOpening)
                {
                    builder.AppendLine("纠错：无");
                }

                builder.AppendLine();
            }

            return builder.Length == 0 ? "未保存任何对话轮次。" : builder.ToString();
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
                return "未知时间";
            }

            try
            {
                return System.DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm");
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return "未知时间";
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
            var isTechnicalInvalid = orchestrator.IsTaskAttemptTechnicalInvalid;

            if (requestTitleText != null)
            {
                requestTitleText.text = "场景与角色需求";
            }

            if (requestStatusText != null)
            {
                if (isRecording)
                {
                    requestStatusText.text = "正在录制你的需求……";
                }
                else if (isTranscribing)
                {
                    requestStatusText.text = "正在识别语音……";
                }
                else if (hasTranscript)
                {
                    requestStatusText.text = "请检查识别结果，然后确认。";
                }
                else
                {
                    requestStatusText.text = "点击“录音”开始录制。";
                }
            }

            if (requestTranscriptText != null)
            {
                requestTranscriptText.text = hasTranscript
                    ? $"识别结果：\n{orchestrator.LastTranscript}"
                    : "识别结果：\n-";
            }

            if (requestErrorText != null)
            {
                requestErrorText.text = hasError ? SceneTalkChineseUiText.Error(orchestrator.LastError) : string.Empty;
            }

            SetButtonLabel(
                listenButton,
                isTechnicalInvalid ? "任务失效" : ResolveRequestListenButtonLabel(isRecording, hasTranscript, hasError));
            SetInteractable(listenButton, !isTechnicalInvalid && (isRecording || !isRunning));
            SetInteractable(confirmButton, !isTechnicalInvalid && !isRunning && hasTranscript);
        }

        private void RefreshLoadingPanel(bool isVisible)
        {
            if (!isVisible || loadingText == null)
            {
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.HistoryLoading)
            {
                loadingText.text = "正在加载对话历史……";
            }
            else if (orchestrator.CurrentState == SceneTalkState.ExperimentHistoryLoading)
            {
                loadingText.text = "正在加载实验历史……";
            }
            else if (orchestrator.CurrentState == SceneTalkState.HistoryRestoring)
            {
                loadingText.text = "正在恢复场景、角色与对话上下文……";
            }
            else
            {
                loadingText.text = orchestrator.CurrentState == SceneTalkState.SceneReady
                    ? "正在准备角色对话……"
                    : "正在加载场景与角色……";
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
                playerSubtitleText.text = $"你：{transcript}";
            }

            if (avatarSubtitleText != null)
            {
                SetActive(avatarSubtitleText.gameObject, !hideSubtitles);
                avatarSubtitleText.text = $"角色：{reply}";
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
                correctionStatusText.text = SceneTalkChineseUiText.CorrectionStatus(status);
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
                var isTechnicalInvalid = orchestrator.IsTaskAttemptTechnicalInvalid;
                SetActive(dialogueListenButton.gameObject, true);
                SetButtonLabel(
                    dialogueListenButton,
                    isTechnicalInvalid
                        ? "任务失效"
                        : isRecording
                            ? "结束"
                            : orchestrator.CurrentState == SceneTalkState.Error
                                ? "重试"
                                : "发言");
                SetInteractable(
                    dialogueListenButton,
                    !isTechnicalInvalid && (isRecording || !orchestrator.IsTurnRunning));
            }
        }

        private void ApplySubtitleLayout(bool hideSubtitles)
        {
            if (subtitlePanelRect != null)
            {
                subtitlePanelRect.anchoredPosition = hideSubtitles
                    ? new Vector2(DialoguePanelCenterX, DialoguePanelHiddenCenterY)
                    : new Vector2(DialoguePanelCenterX, DialoguePanelVisibleCenterY);
                subtitlePanelRect.sizeDelta = hideSubtitles
                    ? new Vector2(DialoguePanelWidth, DialoguePanelHiddenHeight)
                    : new Vector2(DialoguePanelWidth, DialoguePanelVisibleHeight);
            }

            if (subtitleTextContainerRect != null)
            {
                subtitleTextContainerRect.anchoredPosition = new Vector2(DialogueContentCenterX, 35f);
                subtitleTextContainerRect.sizeDelta = new Vector2(DialogueContentWidth, 64f);
            }

            if (experimentDebugText != null)
            {
                var debugRect = experimentDebugText.GetComponent<RectTransform>();
                debugRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 48f)
                    : new Vector2(DialogueContentCenterX, 78f);
                debugRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 22f)
                    : new Vector2(DialogueContentWidth, 18f);
            }

            if (correctionFeedbackText != null)
            {
                var feedbackRect = correctionFeedbackText.GetComponent<RectTransform>();
                feedbackRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, 16f)
                    : new Vector2(DialogueContentCenterX, -17f);
                feedbackRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 28f);
            }

            if (correctionStatusText != null)
            {
                var correctionRect = correctionStatusText.GetComponent<RectTransform>();
                correctionRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -14f)
                    : new Vector2(DialogueContentCenterX, -46f);
                correctionRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 24f);
            }

            if (dialogueStatusText != null)
            {
                var statusRect = dialogueStatusText.GetComponent<RectTransform>();
                statusRect.anchoredPosition = hideSubtitles
                    ? new Vector2(-100f, -42f)
                    : new Vector2(DialogueContentCenterX, -70f);
                statusRect.sizeDelta = hideSubtitles
                    ? new Vector2(480f, 28f)
                    : new Vector2(DialogueContentWidth, 20f);
            }

            if (dialogueListenButton != null)
            {
                var buttonRect = dialogueListenButton.GetComponent<RectTransform>();
                buttonRect.anchoredPosition = hideSubtitles
                    ? new Vector2(310f, -32f)
                    : new Vector2(DialogueButtonCenterX, -60f);
                buttonRect.sizeDelta = hideSubtitles
                    ? new Vector2(110f, 38f)
                    : new Vector2(110f, 40f);
            }

        }

        private static string ResolveRequestListenButtonLabel(bool isRecording, bool hasTranscript, bool hasError)
        {
            if (isRecording)
            {
                return "结束";
            }

            return hasTranscript || hasError ? "重试" : "录音";
        }

        private string ResolveDialogueStatusText()
        {
            if (orchestrator == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(orchestrator.LastError))
            {
                return SceneTalkChineseUiText.Error(orchestrator.LastError);
            }

            if (orchestrator.IsTurnRunning)
            {
                if (orchestrator.CurrentState == SceneTalkState.Recording)
                {
                    return "正在录音……";
                }

                if (orchestrator.CurrentState == SceneTalkState.Transcribing)
                {
                    return "正在识别语音……";
                }

                if (orchestrator.CurrentState == SceneTalkState.Processing)
                {
                    return "正在思考……";
                }

                if (orchestrator.CurrentState == SceneTalkState.CorrectionFeedbackSpeaking)
                {
                    return "正在播放纠错反馈……";
                }

                if (orchestrator.CurrentState == SceneTalkState.DialogueSpeaking)
                {
                    return "角色正在发言……";
                }

                if (orchestrator.CurrentState == SceneTalkState.AvatarSpeaking)
                {
                    return "正在播放语音……";
                }
            }

            return "可以继续发言。";
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

            return $"纠错：{feedbackText}";
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
                "退出",
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
            UpdateTaskGoalCanvasFacingUser();
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

        private Canvas CreateTaskGoalCanvas()
        {
            var canvasObject = new GameObject(TaskGoalCanvasName);
            canvasObject.transform.SetParent(worldCanvas.transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCanvas.worldCamera;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = worldCanvas.sortingLayerID;
            canvas.sortingOrder = worldCanvas.sortingOrder + 1;
            canvas.additionalShaderChannels = worldCanvas.additionalShaderChannels;

            var rect = canvas.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = TaskGoalCanvasPosition;
            rect.sizeDelta = TaskGoalPanelSize;
            rect.localScale = Vector3.one;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            var sourceScaler = worldCanvas.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = sourceScaler != null ? sourceScaler.dynamicPixelsPerUnit : 20f;
            return canvas;
        }

        private void UpdateTaskGoalCanvasFacingUser()
        {
            if (taskGoalCanvas == null)
            {
                return;
            }

            var targetCamera = taskGoalCanvas.worldCamera != null
                ? taskGoalCanvas.worldCamera
                : Camera.main;
            if (targetCamera == null)
            {
                return;
            }

            var direction = Vector3.ProjectOnPlane(
                taskGoalCanvas.transform.position - targetCamera.transform.position,
                Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            taskGoalCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
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
                ? "辅助角色"
                : "对话角色";
        }

        private static string ResolveCorrectionStyleDisplayName(string style)
        {
            return string.Equals(
                style,
                ExperimentConditionManager.RecastStyle,
                System.StringComparison.OrdinalIgnoreCase)
                ? "重述反馈"
                : "直接纠错";
        }

        private static string ResolveCorrectionAppearanceDisplayName(string embodiment)
        {
            if (string.Equals(
                    embodiment,
                    ExperimentConditionManager.AudioOnlyAssistantEmbodiment,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return "仅语音";
            }

            return string.Equals(
                embodiment,
                ExperimentConditionManager.HumanoidAssistantEmbodiment,
                System.StringComparison.OrdinalIgnoreCase)
                ? "第三人称角色"
                : "悬浮球";
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
