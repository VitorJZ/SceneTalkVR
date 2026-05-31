using SceneTalkVR.Demo;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SceneTalkVR.EditorTools
{
    public static class SceneTalkDemoSetupMenu
    {
        private const string DemoRigName = "SceneTalkVR Demo Rig";
        private const string WorldUiName = "SceneTalkVR World UI";
        private const string SceneRootName = "SceneRoot";
        private const string EventSystemName = "EventSystem";

        [MenuItem("SceneTalkVR/Setup/Rebuild Demo Rig", false, 10)]
        public static void CreateVitorDemoRig()
        {
            CreateCleanDemoRig();
        }

        public static void RepairVitorDemoRigCameraAndInput()
        {
            CreateCleanDemoRig();
        }

        [MenuItem("SceneTalkVR/Advanced/Clear Generated Demo Rig", false, 110)]
        public static void ClearVitorDemoRig()
        {
            CleanupGeneratedDemoObjects();
            NormalizeEventSystems();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void CreateCleanDemoRig()
        {
            CleanupGeneratedDemoObjects();

            var root = new GameObject(DemoRigName);
            var sceneRoot = new GameObject(SceneRootName).transform;
            sceneRoot.SetParent(root.transform);
            var interactionCamera = ConfigureMainCamera();
            EnsureInputEventSystem();

            var audioSource = root.AddComponent<AudioSource>();
            var speech = root.AddComponent<DemoSpeechInputModule>();
            var brain = root.AddComponent<DemoBrainModule>();
            var scenePresenter = root.AddComponent<SceneTalkScenePresenter>();
            var avatarVoice = root.AddComponent<DemoAvatarVoiceModule>();
            var orchestrator = root.AddComponent<SceneTalkOrchestrator>();
            var interactionBootstrap = root.AddComponent<SceneTalkInteractionBootstrap>();

            var ui = CreateWorldSpaceUi(root.transform, orchestrator, interactionCamera);

            SetObject(scenePresenter, "sceneRoot", sceneRoot);
            SetObject(avatarVoice, "audioSource", audioSource);
            SetObject(orchestrator, "speechInputModule", speech);
            SetObject(orchestrator, "brainModule", brain);
            SetObject(orchestrator, "scenePresenterModule", scenePresenter);
            SetObject(orchestrator, "avatarVoiceModule", avatarVoice);
            SetObject(orchestrator, "stateLabel", ui.stateLabel);
            SetObject(orchestrator, "transcriptLabel", ui.transcriptLabel);
            SetObject(orchestrator, "replyLabel", ui.replyLabel);
            SetObject(orchestrator, "errorLabel", ui.errorLabel);
            SetObject(interactionBootstrap, "interactionCamera", interactionCamera);
            SetObject(interactionBootstrap, "worldCanvas", ui.canvas);

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static DemoUi CreateWorldSpaceUi(Transform parent, SceneTalkOrchestrator orchestrator, Camera interactionCamera)
        {
            var canvasObject = new GameObject(WorldUiName);
            canvasObject.transform.SetParent(parent);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            ConfigureWorldCanvas(canvas, interactionCamera);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.88f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(720f, 420f);

            var stateLabel = CreateText(panel.transform, "StateLabel", "State: Idle", new Vector2(0f, 150f), 30, TextAnchor.MiddleCenter);
            var transcriptLabel = CreateText(panel.transform, "TranscriptLabel", "Transcript: -", new Vector2(0f, 95f), 22, TextAnchor.MiddleCenter);
            var replyLabel = CreateText(panel.transform, "ReplyLabel", "Avatar: -", new Vector2(0f, 45f), 22, TextAnchor.MiddleCenter);
            var errorLabel = CreateText(panel.transform, "ErrorLabel", string.Empty, new Vector2(0f, -5f), 20, TextAnchor.MiddleCenter);
            errorLabel.color = new Color(1f, 0.45f, 0.35f, 1f);

            var startButton = CreateButton(panel.transform, "StartButton", "Start Practice", new Vector2(-170f, -120f));
            var retryButton = CreateButton(panel.transform, "RetryButton", "Retry", new Vector2(0f, -120f));
            var finishButton = CreateButton(panel.transform, "FinishButton", "Finish", new Vector2(170f, -120f));

            UnityEventTools.AddPersistentListener(startButton.onClick, orchestrator.StartPractice);
            UnityEventTools.AddPersistentListener(retryButton.onClick, orchestrator.RetryAfterError);
            UnityEventTools.AddPersistentListener(finishButton.onClick, orchestrator.FinishPractice);

            return new DemoUi
            {
                canvas = canvas,
                stateLabel = stateLabel,
                transcriptLabel = transcriptLabel,
                replyLabel = replyLabel,
                errorLabel = errorLabel
            };
        }

        private static Text CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = GetDefaultFont();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;

            var rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(640f, 42f);
            rect.anchoredPosition = anchoredPosition;
            return label;
        }

        private static Font GetDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.34f, 0.58f, 1f);

            var button = buttonObject.AddComponent<Button>();
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 56f);
            rect.anchoredPosition = anchoredPosition;

            var labelText = CreateText(buttonObject.transform, "Label", label, Vector2.zero, 22, TextAnchor.MiddleCenter);
            labelText.raycastTarget = false;
            labelText.rectTransform.sizeDelta = rect.sizeDelta;
            return button;
        }

        private static void CleanupGeneratedDemoObjects()
        {
            foreach (var root in FindAll<SceneTalkOrchestrator>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var root in FindAll<SceneTalkInteractionBootstrap>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var root in FindAll<DemoSpeechInputModule>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var root in FindAll<DemoBrainModule>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var root in FindAll<DemoAvatarVoiceModule>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var root in FindAll<SceneTalkScenePresenter>())
            {
                DestroyGeneratedObject(root.gameObject);
            }

            foreach (var canvas in FindAll<Canvas>())
            {
                if (canvas != null && canvas.gameObject.name.StartsWith(WorldUiName))
                {
                    DestroyGeneratedObject(canvas.gameObject);
                }
            }

            foreach (var gameObject in FindAll<GameObject>())
            {
                if (gameObject != null && gameObject.name.StartsWith(DemoRigName))
                {
                    DestroyImmediateSafe(gameObject);
                }
            }
        }

        private static void DestroyGeneratedObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            var root = gameObject.transform.root.gameObject;
            if (root.name.StartsWith(DemoRigName))
            {
                DestroyImmediateSafe(root);
                return;
            }

            if (gameObject.name.StartsWith(WorldUiName))
            {
                DestroyImmediateSafe(gameObject);
            }
        }

        private static void DestroyImmediateSafe(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Object.DestroyImmediate(gameObject);
        }

        private static Camera ConfigureMainCamera()
        {
            var camera = Camera.main != null ? Camera.main : FindFirst<Camera>();
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            }

            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.6f, -1.5f);
            camera.transform.rotation = Quaternion.identity;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            if (camera.GetComponent<AudioListener>() == null && FindFirst<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }

            if (camera.GetComponent<PhysicsRaycaster>() == null)
            {
                camera.gameObject.AddComponent<PhysicsRaycaster>();
            }

            return camera;
        }

        private static void ConfigureWorldCanvas(Canvas canvas, Camera interactionCamera)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = interactionCamera;
            canvas.transform.position = new Vector3(0f, 1.5f, 1.4f);
            canvas.transform.rotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.005f;

            var canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.dynamicPixelsPerUnit = 20f;
            }

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.ignoreReversedGraphics = false;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        private static void EnsureInputEventSystem()
        {
            var eventSystem = EventSystem.current != null ? EventSystem.current : FindFirst<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject(EventSystemName).AddComponent<EventSystem>();
            }

            NormalizeEventSystems(eventSystem);

            var oldInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldInputModule != null)
            {
                Object.DestroyImmediate(oldInputModule);
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
        }

        private static void NormalizeEventSystems(EventSystem preferredEventSystem = null)
        {
            var eventSystems = FindAll<EventSystem>();
            if (eventSystems.Length <= 1)
            {
                return;
            }

            var keeper = preferredEventSystem != null ? preferredEventSystem : EventSystem.current;
            if (keeper == null)
            {
                keeper = eventSystems[0];
            }

            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem != null && eventSystem != keeper)
                {
                    DestroyImmediateSafe(eventSystem.gameObject);
                }
            }
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindFirst<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private struct DemoUi
        {
            public Canvas canvas;
            public Text stateLabel;
            public Text transcriptLabel;
            public Text replyLabel;
            public Text errorLabel;
        }
    }
}
