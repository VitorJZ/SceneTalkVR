using SceneTalkVR.Demo;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Runtime;
using SceneTalkVR.Voice;
using UnityEditor;
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
        private const string AvatarRootName = "AvatarRoot";
        private const string EventSystemName = "EventSystem";
        private const string AvatarCatalogPath = "Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset";
        private const string AvatarPropCatalogPath = "Assets/SceneTalkVR/Avatar/Catalogs/AvatarPropCatalog.asset";
        private const string AvatarCommonControllerPath = "Assets/SceneTalkVR/Avatar/Animations/Common/AvatarCommonHumanoid.controller";
        private const string VoiceGatewaySettingsPath = "Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset";

        [MenuItem("SceneTalkVR/Setup/Rebuild Demo Rig", false, 10)]
        public static void CreateVitorDemoRig()
        {
            CreateCleanDemoRig(false);
        }

        [MenuItem("SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway", false, 11)]
        public static void CreateVitorDemoRigWithVoiceGateway()
        {
            CreateCleanDemoRig(true);
        }

        [MenuItem("SceneTalkVR/Setup/Create Voice Gateway Settings", false, 12)]
        public static void CreateVoiceGatewaySettingsAsset()
        {
            var settings = EnsureVoiceGatewaySettings();
            Selection.activeObject = settings;
        }

        public static void RepairVitorDemoRigCameraAndInput()
        {
            CreateCleanDemoRig(false);
        }

        [MenuItem("SceneTalkVR/Advanced/Clear Generated Demo Rig", false, 110)]
        public static void ClearVitorDemoRig()
        {
            CleanupGeneratedDemoObjects();
            NormalizeEventSystems();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void CreateCleanDemoRig(bool useVoiceGateway)
        {
            var root = GameObject.Find(DemoRigName);
            if (root == null)
            {
                root = new GameObject(DemoRigName);
            }

            var sceneRootTransform = root.transform.Find(SceneRootName);
            if (sceneRootTransform == null)
            {
                sceneRootTransform = new GameObject(SceneRootName).transform;
                sceneRootTransform.SetParent(root.transform);
            }

            var avatarRootTransform = root.transform.Find(AvatarRootName);
            if (avatarRootTransform == null)
            {
                avatarRootTransform = new GameObject(AvatarRootName).transform;
                avatarRootTransform.SetParent(root.transform);
                avatarRootTransform.localPosition = new Vector3(0f, 0f, 2.6f);
                avatarRootTransform.localRotation = Quaternion.identity;
                avatarRootTransform.localScale = Vector3.one * 1.25f;
            }

            var interactionCamera = ConfigureMainCamera();
            EnsureInputEventSystem();

            var audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = root.AddComponent<AudioSource>();

            MonoBehaviour speech;
            if (useVoiceGateway)
            {
                var gatewayClient = root.GetComponent<VoiceGatewayClient>();
                if (gatewayClient == null) gatewayClient = root.AddComponent<VoiceGatewayClient>();

                var microphoneRecorder = root.GetComponent<MicrophoneRecorder>();
                if (microphoneRecorder == null) microphoneRecorder = root.AddComponent<MicrophoneRecorder>();

                var gatewaySpeech = root.GetComponent<GatewaySpeechInputModule>();
                if (gatewaySpeech == null) gatewaySpeech = root.AddComponent<GatewaySpeechInputModule>();

                SetObject(gatewayClient, "settings", EnsureVoiceGatewaySettings());
                SetObject(gatewaySpeech, "gatewayClient", gatewayClient);
                SetObject(gatewaySpeech, "microphoneRecorder", microphoneRecorder);
                speech = gatewaySpeech;
            }
            else
            {
                speech = root.GetComponent<DemoSpeechInputModule>();
                if (speech == null) speech = root.AddComponent<DemoSpeechInputModule>();
            }

            var brain = root.GetComponent<DemoBrainModule>();
            if (brain == null) brain = root.AddComponent<DemoBrainModule>();
            
            var scenePresenter = root.GetComponent<SceneTalkScenePresenter>();
            if (scenePresenter == null) scenePresenter = root.AddComponent<SceneTalkScenePresenter>();

            // Prioritize RealLLM and HybridPresenter if they exist
            MonoBehaviour brainToUse = brain;
            var realLlm = root.GetComponent<SceneTalkVR.Runtime.Services.RealLLMService>();
            if (realLlm != null) brainToUse = realLlm;

            MonoBehaviour presenterToUse = scenePresenter;
            var hybridPresenter = root.GetComponent<SceneTalkVR.Runtime.Services.HybridScenePresenter>();
            if (hybridPresenter != null) presenterToUse = hybridPresenter;

            var avatarResolver = root.GetComponent<AvatarPresetResolver>();
            if (avatarResolver == null) avatarResolver = root.AddComponent<AvatarPresetResolver>();
            
            var avatarLoader = root.GetComponent<PrefabAvatarInstanceLoader>();
            if (avatarLoader == null) avatarLoader = root.AddComponent<PrefabAvatarInstanceLoader>();
            
            var avatarAnimation = root.GetComponent<AvatarAnimationDriver>();
            if (avatarAnimation == null) avatarAnimation = root.AddComponent<AvatarAnimationDriver>();
            
            var avatarProps = root.GetComponent<AvatarPropPresenter>();
            if (avatarProps == null) avatarProps = root.AddComponent<AvatarPropPresenter>();
            
            var avatarVoice = root.GetComponent<AvatarPresentationVoiceModule>();
            if (avatarVoice == null) avatarVoice = root.AddComponent<AvatarPresentationVoiceModule>();
            
            var orchestrator = root.GetComponent<SceneTalkOrchestrator>();
            if (orchestrator == null) orchestrator = root.AddComponent<SceneTalkOrchestrator>();
            
            var interactionBootstrap = root.GetComponent<SceneTalkInteractionBootstrap>();
            if (interactionBootstrap == null) interactionBootstrap = root.AddComponent<SceneTalkInteractionBootstrap>();

            // Cleanup old UI to avoid duplicates
            var existingUi = root.transform.Find(WorldUiName);
            if (existingUi != null) Object.DestroyImmediate(existingUi.gameObject);
            var ui = CreateWorldSpaceUi(root.transform, interactionCamera);

            SetObject(scenePresenter, "sceneRoot", sceneRootTransform);
            SetObject(avatarResolver, "catalog", LoadAvatarCatalog());
            SetObject(avatarProps, "catalog", LoadAvatarPropCatalog());
            SetObject(avatarVoice, "resolver", avatarResolver);
            SetObject(avatarVoice, "loaderModule", avatarLoader);
            SetObject(avatarVoice, "avatarRoot", avatarRootTransform);
            SetObject(avatarVoice, "propPresenter", avatarProps);
            SetObject(avatarVoice, "audioSource", audioSource);
            SetObject(avatarVoice, "animationDriver", avatarAnimation);
            SetObject(avatarVoice, "defaultAnimatorController", LoadAvatarCommonController());
            
            if (useVoiceGateway && speech is GatewaySpeechInputModule)
            {
                var gatewayClient = root.GetComponent<VoiceGatewayClient>();
                SetObject(avatarVoice, "voiceGatewayClient", gatewayClient);
                SetBool(avatarVoice, "useVoiceGatewayTts", true);
            }

            SetObject(orchestrator, "speechInputModule", speech);
            SetObject(orchestrator, "brainModule", brainToUse);
            SetObject(orchestrator, "scenePresenterModule", presenterToUse);
            SetObject(orchestrator, "avatarVoiceModule", avatarVoice);
            SetObject(interactionBootstrap, "orchestrator", orchestrator);
            SetObject(interactionBootstrap, "interactionCamera", interactionCamera);
            SetObject(interactionBootstrap, "worldCanvas", ui.canvas);

            var flowUi = root.GetComponent<SceneTalkFlowUiController>();
            if (flowUi == null) flowUi = root.AddComponent<SceneTalkFlowUiController>();
            flowUi.Configure(orchestrator, ui.canvas, interactionBootstrap);

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static AvatarCatalog LoadAvatarCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AvatarCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[SceneTalkVR] Avatar catalog not found at {AvatarCatalogPath}. Run SceneTalkVR/Avatar/Generate Placeholder Avatars first.");
            }

            return catalog;
        }

        private static RuntimeAnimatorController LoadAvatarCommonController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AvatarCommonControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"[SceneTalkVR] Avatar common animator controller not found at {AvatarCommonControllerPath}. Run SceneTalkVR/Avatar/P1 Build Humanoid Avatars first.");
            }

            return controller;
        }

        private static AvatarPropCatalog LoadAvatarPropCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPropCatalog>(AvatarPropCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[SceneTalkVR] Avatar prop catalog not found at {AvatarPropCatalogPath}. Run SceneTalkVR/Avatar/P1 Build Teacher Humanoid first.");
            }

            return catalog;
        }

        private static VoiceGatewaySettings EnsureVoiceGatewaySettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<VoiceGatewaySettings>(VoiceGatewaySettingsPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<VoiceGatewaySettings>();
            AssetDatabase.CreateAsset(settings, VoiceGatewaySettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SceneTalkVR] Created voice gateway settings at {VoiceGatewaySettingsPath}.");
            return settings;
        }

        private static DemoUi CreateWorldSpaceUi(
            Transform parent,
            Camera interactionCamera)
        {
            var canvasObject = new GameObject(WorldUiName);
            canvasObject.transform.SetParent(parent);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            ConfigureWorldCanvas(canvas, interactionCamera);

            return new DemoUi
            {
                canvas = canvas
            };
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
            var camera = Camera.main != null ? Camera.main : FindActiveCamera();
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            }

            camera.tag = "MainCamera";

            if (!IsTrackedCamera(camera))
            {
                camera.transform.position = new Vector3(0f, 1.6f, -1.5f);
                camera.transform.rotation = Quaternion.identity;
            }

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

        private static Camera FindActiveCamera()
        {
            foreach (var camera in FindAll<Camera>())
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera;
                }
            }

            return FindFirst<Camera>();
        }

        private static bool IsTrackedCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
            {
                var typeName = behaviour == null ? string.Empty : behaviour.GetType().FullName;
                if (!string.IsNullOrEmpty(typeName) && typeName.Contains("TrackedPoseDriver"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureWorldCanvas(Canvas canvas, Camera interactionCamera)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = interactionCamera;
            canvas.transform.position = new Vector3(0f, 1.5f, 1.4f);
            canvas.transform.rotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.005f;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(720f, 420f);
            }

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

        private static void SetBool(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            property.boolValue = value;
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
        }
    }
}
