using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkInteractionBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 1.6f, -1.5f);
        [SerializeField] private Vector3 canvasPosition = new Vector3(0f, 1.5f, 1.4f);
        [SerializeField] private Vector3 canvasEulerAngles = Vector3.zero;
        [SerializeField] private float canvasScale = 0.005f;
        [SerializeField] private bool configureOnAwake = true;

        private void Awake()
        {
            if (configureOnAwake)
            {
                Configure();
            }
        }

        public void Configure()
        {
            interactionCamera = ResolveCamera(interactionCamera);
            worldCanvas = ResolveCanvas(worldCanvas);

            ConfigureCamera(interactionCamera);
            ConfigureCanvas(worldCanvas, interactionCamera);
            EnsureEventSystem();
        }

        private Camera ResolveCamera(Camera preferredCamera)
        {
            if (preferredCamera != null)
            {
                return preferredCamera;
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            var existingCamera = FindFirst<Camera>();
            if (existingCamera != null)
            {
                return existingCamera;
            }

            var cameraObject = new GameObject("Main Camera");
            return cameraObject.AddComponent<Camera>();
        }

        private Canvas ResolveCanvas(Canvas preferredCanvas)
        {
            if (preferredCanvas != null)
            {
                return preferredCanvas;
            }

            var namedCanvas = GameObject.Find("SceneTalkVR World UI");
            if (namedCanvas != null && namedCanvas.TryGetComponent(out Canvas canvas))
            {
                return canvas;
            }

            return FindFirst<Canvas>();
        }

        private void ConfigureCamera(Camera cameraToConfigure)
        {
            if (cameraToConfigure == null)
            {
                return;
            }

            cameraToConfigure.tag = "MainCamera";
            cameraToConfigure.transform.position = cameraPosition;
            cameraToConfigure.transform.rotation = Quaternion.identity;
            cameraToConfigure.fieldOfView = 60f;
            cameraToConfigure.nearClipPlane = 0.01f;
            cameraToConfigure.farClipPlane = 100f;

            if (cameraToConfigure.GetComponent<AudioListener>() == null && FindFirst<AudioListener>() == null)
            {
                cameraToConfigure.gameObject.AddComponent<AudioListener>();
            }

            if (cameraToConfigure.GetComponent<PhysicsRaycaster>() == null)
            {
                cameraToConfigure.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        private void ConfigureCanvas(Canvas canvasToConfigure, Camera cameraToUse)
        {
            if (canvasToConfigure == null)
            {
                return;
            }

            canvasToConfigure.renderMode = RenderMode.WorldSpace;
            canvasToConfigure.worldCamera = cameraToUse;
            canvasToConfigure.transform.position = canvasPosition;
            canvasToConfigure.transform.rotation = Quaternion.Euler(canvasEulerAngles);
            canvasToConfigure.transform.localScale = Vector3.one * canvasScale;

            var scaler = canvasToConfigure.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = 20f;
            }

            var raycaster = canvasToConfigure.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvasToConfigure.gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.ignoreReversedGraphics = false;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirst<EventSystem>();
            }

            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            var oldInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldInputModule != null)
            {
                Destroy(oldInputModule);
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
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private static T FindFirst<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }
    }
}
