using System.Collections.Generic;
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
            var showRequest = !dialogueActive
                && (!isFixedMode || state != SceneTalkState.Listening)
                && (state == SceneTalkState.Listening
                    || state == SceneTalkState.Recording
                    || state == SceneTalkState.Transcribing
                    || state == SceneTalkState.Error);
            var showTaskSelection = isFixedMode
                && !dialogueActive
                && (state == SceneTalkState.Listening);
            var showLoading = !dialogueActive && (state == SceneTalkState.Processing || state == SceneTalkState.SceneReady);
            var showDialogue = dialogueActive
                || state == SceneTalkState.AvatarSpeaking
                || state == SceneTalkState.CorrectionFeedbackSpeaking
                || state == SceneTalkState.DialogueSpeaking
                || state == SceneTalkState.TurnReview;

            SetActive(mainMenuPanel, showMain);
            SetActive(settingsPanel, showSettings);
            SetActive(requestPanel, showRequest);
            SetActive(taskSelectionPanel, showTaskSelection);
            SetActive(loadingPanel, showLoading);
            SetActive(subtitlePanel, showDialogue);
            SetActive(exitButtonObject, !showMain);

            RefreshSettingsPanel(showSettings);
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
