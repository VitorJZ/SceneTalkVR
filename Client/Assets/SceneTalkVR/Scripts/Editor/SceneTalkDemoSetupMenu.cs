using SceneTalkVR.Demo;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Runtime;
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
            var avatarRoot = new GameObject(AvatarRootName).transform;
            avatarRoot.SetParent(root.transform);
            avatarRoot.localPosition = new Vector3(0f, 0f, 1.1f);
            avatarRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var interactionCamera = ConfigureMainCamera();
            EnsureInputEventSystem();

            var audioSource = root.AddComponent<AudioSource>();
            var speech = root.AddComponent<DemoSpeechInputModule>();
            var brain = root.AddComponent<DemoBrainModule>();
            var scenePresenter = root.AddComponent<SceneTalkScenePresenter>();
            var avatarResolver = root.AddComponent<AvatarPresetResolver>();
            var avatarLoader = root.AddComponent<PrefabAvatarInstanceLoader>();
            var avatarVoice = root.AddComponent<AvatarPresentationVoiceModule>();
            var orchestrator = root.AddComponent<SceneTalkOrchestrator>();
            var interactionBootstrap = root.AddComponent<SceneTalkInteractionBootstrap>();

            var ui = CreateWorldSpaceUi(root.transform, interactionCamera);

            SetObject(scenePresenter, "sceneRoot", sceneRoot);
            SetObject(avatarResolver, "catalog", LoadAvatarCatalog());
            SetObject(avatarVoice, "resolver", avatarResolver);
            SetObject(avatarVoice, "loaderModule", avatarLoader);
            SetObject(avatarVoice, "avatarRoot", avatarRoot);
            SetObject(avatarVoice, "audioSource", audioSource);
            SetObject(orchestrator, "speechInputModule", speech);
            SetObject(orchestrator, "brainModule", brain);
            SetObject(orchestrator, "scenePresenterModule", scenePresenter);
            SetObject(orchestrator, "avatarVoiceModule", avatarVoice);
            SetObject(interactionBootstrap, "orchestrator", orchestrator);
            SetObject(interactionBootstrap, "interactionCamera", interactionCamera);
            SetObject(interactionBootstrap, "worldCanvas", ui.canvas);

            var flowUi = root.AddComponent<SceneTalkFlowUiController>();
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
