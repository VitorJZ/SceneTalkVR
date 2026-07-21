using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.Core;
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

        private readonly Dictionary<Text, int> baseFontSizes = new Dictionary<Text, int>();
        private GameObject mainMenuPanel;
        private GameObject settingsPanel;
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

        private Button startButton;
        private Button settingsButton;
        private Button quitButton;
        private Button fontMinusButton;
        private Button fontPlusButton;
        private Button uiMinusButton;
        private Button uiPlusButton;
        private Button subtitleChangeButton;
        private Button listenButton;
        private Button confirmButton;
        private Button exitButton;
        private Button dialogueListenButton;

        private readonly List<Button> taskButtons = new List<Button>();
        private readonly List<ExperimentTaskDefinition> taskButtonDefinitions = new List<ExperimentTaskDefinition>();
        private readonly Dictionary<FormalConditionCode, Button> formalModeButtons = new Dictionary<FormalConditionCode, Button>();
        private readonly Dictionary<FormalConditionCode, Text> formalModeStatusTexts = new Dictionary<FormalConditionCode, Text>();
        private GoalProgressTracker subscribedGoalTracker;

        private Text settingsTitleText;
        private Text settingsPageText;
        private Text fontValueText;
        private Text uiValueText;
        private Text subtitleValueText;
        private Text requestTitleText;
        private Text requestStatusText;
        private Text requestTranscriptText;
        private Text requestErrorText;
        private Text loadingText;
        private Text experimentDebugText;
        private Text dialogueStatusText;
        private Text correctionStatusText;
        private Text correctionFeedbackText;
        private Text playerSubtitleText;
        private Text avatarSubtitleText;
        private Text taskGoalText;
        private Text demoBannerText;
        private Text demoStatusText;
        private Text demoRankingText;
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

            mainMenuPanel = CreatePanel(root, "InitialPanel", new Vector2(0f, 0f), new Vector2(380f, 360f), new Color(0.04f, 0.05f, 0.07f, 0.9f));
            CreateText(mainMenuPanel.transform, "Title", "SceneTalkVR", new Vector2(0f, 122f), new Vector2(320f, 54f), 34, TextAnchor.MiddleCenter, Color.white);
            startButton = CreateButton(mainMenuPanel.transform, "StartButton", "Start", new Vector2(0f, 48f), new Vector2(190f, 54f), new Color(0.16f, 0.38f, 0.68f, 1f));
            settingsButton = CreateButton(mainMenuPanel.transform, "SettingsButton", "Settings", new Vector2(0f, -24f), new Vector2(190f, 54f), new Color(0.24f, 0.36f, 0.42f, 1f));
            quitButton = CreateButton(mainMenuPanel.transform, "QuitButton", "Quit", new Vector2(0f, -96f), new Vector2(190f, 54f), new Color(0.58f, 0.18f, 0.18f, 1f));

            settingsPanel = CreatePanel(root, "SettingsPanel", new Vector2(0f, 0f), new Vector2(820f, 380f), new Color(0.04f, 0.05f, 0.07f, 0.92f));
            settingsTitleText = CreateText(settingsPanel.transform, "Title", "Settings", new Vector2(0f, 146f), new Vector2(480f, 48f), 30, TextAnchor.MiddleCenter, Color.white);
            settingsPageText = CreateText(settingsPanel.transform, "Page", "Display", new Vector2(0f, 106f), new Vector2(700f, 32f), 18, TextAnchor.MiddleCenter, new Color(0.74f, 0.86f, 1f, 1f));

            settingsGeneralGroup = new GameObject("GeneralSettings");
            settingsGeneralGroup.transform.SetParent(settingsPanel.transform, false);
            CreateText(settingsGeneralGroup.transform, "FontLabel", "Font Size", new Vector2(-240f, 42f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            fontMinusButton = CreateButton(settingsGeneralGroup.transform, "FontMinusButton", "-", new Vector2(78f, 42f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            fontValueText = CreateText(settingsGeneralGroup.transform, "FontValue", string.Empty, new Vector2(174f, 42f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            fontPlusButton = CreateButton(settingsGeneralGroup.transform, "FontPlusButton", "+", new Vector2(270f, 42f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "UiLabel", "Interface Size", new Vector2(-240f, -30f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            uiMinusButton = CreateButton(settingsGeneralGroup.transform, "UiMinusButton", "-", new Vector2(78f, -30f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));
            uiValueText = CreateText(settingsGeneralGroup.transform, "UiValue", string.Empty, new Vector2(174f, -30f), new Vector2(120f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            uiPlusButton = CreateButton(settingsGeneralGroup.transform, "UiPlusButton", "+", new Vector2(270f, -30f), new Vector2(52f, 48f), new Color(0.24f, 0.36f, 0.42f, 1f));

            CreateText(settingsGeneralGroup.transform, "SubtitleLabel", "Dialogue Subtitles", new Vector2(-240f, -102f), new Vector2(320f, 44f), 21, TextAnchor.MiddleLeft, Color.white);
            subtitleValueText = CreateText(settingsGeneralGroup.transform, "SubtitleValue", string.Empty, new Vector2(110f, -102f), new Vector2(140f, 44f), 20, TextAnchor.MiddleCenter, Color.white);
            subtitleChangeButton = CreateButton(settingsGeneralGroup.transform, "SubtitleChangeButton", "Change", new Vector2(293f, -102f), new Vector2(170f, 48f), new Color(0.12f, 0.52f, 0.38f, 1f));

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

            taskGoalPanel = CreatePanel(root, "ReadOnlyTaskGoalPanel", new Vector2(-390f, 120f), new Vector2(340f, 360f), new Color(0.03f, 0.04f, 0.06f, 0.84f));
            CreateText(taskGoalPanel.transform, "Title", "Task Goals", new Vector2(0f, 150f), new Vector2(300f, 36f), 22, TextAnchor.MiddleCenter, Color.white);
            taskGoalText = CreateText(taskGoalPanel.transform, "GoalStateText", string.Empty, new Vector2(0f, -5f), new Vector2(300f, 270f), 16, TextAnchor.UpperLeft, new Color(0.86f, 0.92f, 1f, 1f));
            
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
                startButton.onClick.AddListener(HandleParticipantStart);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OpenSettings);
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

            if (dialogueListenButton != null)
            {
                dialogueListenButton.onClick.RemoveAllListeners();
                dialogueListenButton.onClick.AddListener(() => orchestrator?.ToggleDialogueSpeechCapture());
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(() => orchestrator?.ReturnToInitialMenu());
            }
        }

        public IReadOnlyList<ExperimentTaskDefinition> CurrentTaskOptions => taskButtonDefinitions;

        public void ShowDeveloperTaskSelectionForQa()
        {
            sessionPreparationBlocked = false;
            orchestrator?.StartPractice();
        }

        private void HandleParticipantStart()
        {
            var collection = EditorCollectionSessionCoordinator.Active;
            if (collection == null || !collection.IsArmed)
            {
                sessionPreparationBlocked = true;
                Refresh();
                return;
            }
            sessionPreparationBlocked = false;
            if (!collection.BeginParticipantFlow(out var error))
            {
                sessionPreparationBlocked = true;
                Debug.LogError("[EditorCollection] Participant Start blocked: " + error, this);
            }
            Refresh();
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
            var questionnaireSession = FindFirstObjectByType<QuestionnaireRuntimeController>(FindObjectsInactive.Include)?.ActiveSession;
            var questionnaireActive = questionnaireSession != null
                && (questionnaireSession.completionStatus == QuestionnaireCompletionStatus.InProgress
                    || questionnaireSession.completionStatus == QuestionnaireCompletionStatus.Reopened);
            if (!rehearsalActive) rehearsalFinalRankingVisible = false;
            var showFinalRanking = rehearsalActive && rehearsalFinalRankingVisible;
            var rehearsalWaiting = rehearsalActive && string.IsNullOrWhiteSpace(rehearsal.CurrentTaskId);
            var showFormalModeSelection = !showFinalRanking && !collectionFinal
                && (collectionArmed ? collection.AwaitingParticipantConditionChoice
                    : rehearsalActive && rehearsal.IsFormal && rehearsal.AwaitingParticipantConditionChoice);
            bool isFixedMode = orchestrator.RuntimeConfig != null && orchestrator.RuntimeConfig.UseFixedExperimentMode;

            var showMain = !rehearsalActive && !collectionParticipantActive && !sessionPreparationBlocked
                && (state == SceneTalkState.Idle || state == SceneTalkState.Finished);
            var showSettings = state == SceneTalkState.Settings;
            var showRequest = !dialogueActive
                && (!isFixedMode || state != SceneTalkState.Listening)
                && (state == SceneTalkState.Listening
                    || state == SceneTalkState.Recording
                    || state == SceneTalkState.Transcribing
                    || state == SceneTalkState.Error);
            var showTaskSelection = !collectionArmed && !rehearsalActive && isFixedMode
                && !dialogueActive
                && (state == SceneTalkState.Listening);
            var showLoading = !dialogueActive && (state == SceneTalkState.Processing || state == SceneTalkState.SceneReady);
            var showDialogue = !showFormalModeSelection && !showFinalRanking && (dialogueActive
                || state == SceneTalkState.AvatarSpeaking
                || state == SceneTalkState.CorrectionFeedbackSpeaking
                || state == SceneTalkState.DialogueSpeaking
                || state == SceneTalkState.TurnReview) && !questionnaireActive;

            if (collectionFinal) { showRequest = false; showTaskSelection = false; showLoading = false; showDialogue = false; showFormalModeSelection = false; }
            SetActive(mainMenuPanel, showMain);
            SetActive(sessionNotPreparedPanel, sessionPreparationBlocked && !collectionArmed);
            SetActive(rehearsalWaitingPanel, rehearsalWaiting && !showFormalModeSelection && !showFinalRanking);
            SetActive(formalModeSelectionPanel, showFormalModeSelection);
            SetActive(settingsPanel, showSettings);
            SetActive(requestPanel, showRequest);
            SetActive(taskSelectionPanel, showTaskSelection);
            SetActive(loadingPanel, showLoading);
            SetActive(subtitlePanel, showDialogue);
            RefreshGoalPanel(showDialogue);
            SetActive(exitButtonObject, !showMain && !rehearsalActive && !collectionArmed);

            RefreshSettingsPanel(showSettings);
            RefreshRequestPanel(showRequest);
            RefreshLoadingPanel(showLoading);
            RefreshSubtitlePanel(showDialogue);
            RefreshDemoOverlay();
            RefreshFormalModeSelection(showFormalModeSelection);
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
                ? "Please rank the three feedback forms.\n\nVoice Only\nFloating Orb\nHumanoid Agent"
                : "Please rank the four feedback conditions.\n\nNE\nNR\nSE\nSR";
            demoRankingPanel.name = "ParticipantFinalRankingPanel";
            rehearsalFinalRankingVisible = true;
            demoRankingPanel.SetActive(true);
            demoRankingPanel.transform.SetAsLastSibling();
        }

        private void RefreshDemoOverlay()
        {
            if (RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsActive)
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
            var tracker = usePilotRehearsal || usePilotDemo ? pilot?.Goals : lifecycle?.GoalTracker;
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
            foreach (var goal in tracker.Goals)
                builder.Append(goal.state == GoalProgressState.Confirmed ? "[✓] "
                    : goal.state == GoalProgressState.Candidate ? "[…] "
                    : goal.state == GoalProgressState.Rejected ? "[↻] " : "[ ] ").AppendLine(goal.goalText);
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
                settingsPageText.text = "Display";
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

            loadingText.text = orchestrator.CurrentState == SceneTalkState.SceneReady
                ? "Preparing avatar dialogue..."
                : "Loading scene and avatar...";
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

        private void CaptureBaseFontSizes(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
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
                    var scaledSize = Mathf.Max(1, Mathf.RoundToInt(pair.Value * settings.fontScale));
                    pair.Key.fontSize = scaledSize;
                    if (pair.Key.resizeTextForBestFit)
                    {
                        pair.Key.resizeTextMaxSize = scaledSize;
                        pair.Key.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(scaledSize * 0.72f));
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

        private static Text CreateText(
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

            var label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = ResolveRuntimeFont();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            
            if (autoFitHeight)
            {
                label.verticalOverflow = VerticalWrapMode.Overflow;
                var fitter = textObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            else
            {
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }

            var rectTransform = label.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            return label;
        }

        private static void ConfigureDialogueText(Text label)
        {
            if (label == null)
            {
                return;
            }

            label.lineSpacing = 0.92f;
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(label.fontSize * 0.72f));
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

        private static Font ResolveRuntimeFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
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

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
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
