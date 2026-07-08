using SceneTalkVR.Demo;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Runtime;
using SceneTalkVR.Voice;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using XRTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

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
            CreateCleanDemoRig(true);
        }

        [MenuItem("SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway", false, 11)]
        public static void CreateVitorDemoRigWithVoiceGateway()
        {
            ConfigureExistingDemoRigVoiceGateway();
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

            // Check for modern components (RealLLM, Panorama, Holodeck, HybridPresenter)
            // Force destroy existing instances to refresh serializable default prompt values and properties
            var oldRealLlm = root.GetComponent<SceneTalkVR.Runtime.Services.RealLLMService>();
            if (oldRealLlm != null) Object.DestroyImmediate(oldRealLlm);
            
            var realLlm = root.AddComponent<SceneTalkVR.Runtime.Services.RealLLMService>();
            SetString(realLlm, "apiUrl", "https://models.sjtu.edu.cn/api/v1/chat/completions");
            SetString(realLlm, "apiKey", "");
            SetString(realLlm, "modelName", "minimax-m2.7");

            var oldPanorama = root.GetComponent<SceneTalkVR.Runtime.Services.PanoramaSceneService>();
            if (oldPanorama != null) Object.DestroyImmediate(oldPanorama);

            var panorama = root.AddComponent<SceneTalkVR.Runtime.Services.PanoramaSceneService>();
            SetString(panorama, "apiKey", "");
            SetString(panorama, "modelName", "Tongyi-MAI/Z-Image");
            SetString(panorama, "imageSize", "1024x1024");
            SetString(panorama, "localFallbackPath", "SceneTalkVR/Textures/FallbackPanorama");
            var fallbackTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SceneTalkVR/Textures/FallbackPanorama.png");
            SetObject(panorama, "fallbackTexture", fallbackTex);
            SetBool(panorama, "forceUseFallback", false);
            SetBool(panorama, "useSkySphere", false);
            SetFloat(panorama, "skySphereScale", 20.0f);
            SetVector3(panorama, "skySpherePositionOffset", new Vector3(0f, -1.6f, 0f));

            var oldHolodeck = root.GetComponent<SceneTalkVR.Runtime.Services.HolodeckSceneService>();
            if (oldHolodeck != null) Object.DestroyImmediate(oldHolodeck);

            var holodeck = root.AddComponent<SceneTalkVR.Runtime.Services.HolodeckSceneService>();
            SetBool(holodeck, "useLocalBackend", true);
            SetString(holodeck, "backendUrl", "http://localhost:8080/generate_scene");

            var oldHybridPresenter = root.GetComponent<SceneTalkVR.Runtime.Services.HybridScenePresenter>();
            if (oldHybridPresenter != null) Object.DestroyImmediate(oldHybridPresenter);

            var hybridPresenter = root.AddComponent<SceneTalkVR.Runtime.Services.HybridScenePresenter>();
            SetObject(hybridPresenter, "panoramaService", panorama);
            SetObject(hybridPresenter, "holodeckService", holodeck);
            SetObject(hybridPresenter, "sceneRoot", sceneRootTransform);
            SetFloat(hybridPresenter, "spawnScale", 1.0f);
            SetBool(hybridPresenter, "autoCenterObjects", true);
            SetVector3(hybridPresenter, "sceneOffset", new Vector3(0f, 0f, 2.5f));
            var assetCatalog = AssetDatabase.LoadAssetAtPath<SceneTalkAssetCatalog>("Assets/SceneTalkVR/Prefabs/SceneTalkAssetCatalog.asset");
            SetObject(hybridPresenter, "assetCatalog", assetCatalog);

            MonoBehaviour brainToUse = realLlm;
            MonoBehaviour presenterToUse = hybridPresenter;

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
            var avatarPropCatalog = LoadAvatarPropCatalog();


            SetObject(avatarResolver, "catalog", LoadAvatarCatalog());
            SetObject(avatarProps, "catalog", avatarPropCatalog);
            SetObject(avatarVoice, "resolver", avatarResolver);
            SetObject(avatarVoice, "loaderModule", avatarLoader);
            SetObject(avatarVoice, "avatarRoot", avatarRootTransform);
            SetObject(avatarVoice, "propPresenter", avatarProps);
            SetObject(avatarVoice, "propCatalog", avatarPropCatalog);
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

        private static void ConfigureExistingDemoRigVoiceGateway()
        {
            var orchestrator = FindFirst<SceneTalkOrchestrator>();
            if (orchestrator == null)
            {
                Debug.LogWarning("[SceneTalkVR] No existing SceneTalkOrchestrator found. Run SceneTalkVR/Setup/Rebuild Demo Rig first, then configure Voice Gateway.");
                return;
            }

            var root = orchestrator.gameObject;
            var gatewayClient = root.GetComponent<VoiceGatewayClient>();
            if (gatewayClient == null)
            {
                gatewayClient = root.AddComponent<VoiceGatewayClient>();
            }

            var microphoneRecorder = root.GetComponent<MicrophoneRecorder>();
            if (microphoneRecorder == null)
            {
                microphoneRecorder = root.AddComponent<MicrophoneRecorder>();
            }

            var gatewaySpeech = root.GetComponent<GatewaySpeechInputModule>();
            if (gatewaySpeech == null)
            {
                gatewaySpeech = root.AddComponent<GatewaySpeechInputModule>();
            }

            SetObject(gatewayClient, "settings", EnsureVoiceGatewaySettings());
            SetObject(gatewaySpeech, "gatewayClient", gatewayClient);
            SetObject(gatewaySpeech, "microphoneRecorder", microphoneRecorder);
            SetObject(orchestrator, "speechInputModule", gatewaySpeech);

            var avatarVoice = root.GetComponent<AvatarPresentationVoiceModule>();
            if (avatarVoice != null)
            {
                var avatarAnimation = root.GetComponent<AvatarAnimationDriver>();
                if (avatarAnimation == null)
                {
                    avatarAnimation = root.AddComponent<AvatarAnimationDriver>();
                }

                SetObject(avatarVoice, "animationDriver", avatarAnimation);
                SetObject(avatarVoice, "defaultAnimatorController", LoadAvatarCommonController());
                SetObject(avatarVoice, "voiceGatewayClient", gatewayClient);
                SetBool(avatarVoice, "useVoiceGatewayTts", true);
            }
            else
            {
                Debug.LogWarning("[SceneTalkVR] AvatarPresentationVoiceModule not found. STT was configured, but TTS playback was not switched to Voice Gateway.");
            }

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SceneTalkVR] Configured Voice Gateway on existing demo rig without rebuilding scene, UI, scene presenter, or avatar setup.");
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
                Debug.LogWarning($"[SceneTalkVR] Avatar prop catalog not found at {AvatarPropCatalogPath}. Run SceneTalkVR/Avatar/P1 Build Humanoid Avatars first.");
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
            var hadTrackedCamera = IsTrackedCamera(camera);

            if (!hadTrackedCamera)
            {
                camera.transform.position = new Vector3(0f, 1.6f, -1.5f);
                camera.transform.rotation = Quaternion.identity;
            }

            EnsureTrackedPoseDriver(camera);

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

        private static void EnsureTrackedPoseDriver(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var trackedPoseDriver = camera.GetComponent<XRTrackedPoseDriver>();
            if (trackedPoseDriver != null)
            {
                ConfigureTrackedPoseDriver(trackedPoseDriver);
                return;
            }

            if (IsTrackedCamera(camera))
            {
                return;
            }

            trackedPoseDriver = camera.gameObject.AddComponent<XRTrackedPoseDriver>();
            ConfigureTrackedPoseDriver(trackedPoseDriver);
        }

        private static void ConfigureTrackedPoseDriver(XRTrackedPoseDriver trackedPoseDriver)
        {
            if (trackedPoseDriver == null)
            {
                return;
            }

            trackedPoseDriver.trackingType = XRTrackedPoseDriver.TrackingType.RotationAndPosition;
            trackedPoseDriver.updateType = XRTrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            trackedPoseDriver.ignoreTrackingState = false;
            trackedPoseDriver.positionInput = new InputActionProperty(CreatePoseAction(
                "Position",
                "<XRHMD>/centerEyePosition",
                "Vector3",
                "<HandheldARInputDevice>/devicePosition"));
            trackedPoseDriver.rotationInput = new InputActionProperty(CreatePoseAction(
                "Rotation",
                "<XRHMD>/centerEyeRotation",
                "Quaternion",
                "<HandheldARInputDevice>/deviceRotation"));
            trackedPoseDriver.trackingStateInput = new InputActionProperty(CreatePoseAction(
                "Tracking State",
                "<XRHMD>/trackingState",
                "Integer",
                null));
        }

        private static InputAction CreatePoseAction(
            string actionName,
            string binding,
            string expectedControlType,
            string fallbackBinding)
        {
            var action = new InputAction(actionName, binding: binding, expectedControlType: expectedControlType);
            if (!string.IsNullOrEmpty(fallbackBinding))
            {
                action.AddBinding(fallbackBinding);
            }

            return action;
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

        private static void SetString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetVector3(Object target, string propertyName, Vector3 value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
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
