using System.Collections;
using System.Collections.Generic;
using SceneTalkVR.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

#if ENABLE_INPUT_SYSTEM
using InputAction = UnityEngine.InputSystem.InputAction;
using InputActionProperty = UnityEngine.InputSystem.InputActionProperty;
using InputActionSetupExtensions = UnityEngine.InputSystem.InputActionSetupExtensions;
using UnityEngine.InputSystem.UI;
using XRTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;
#endif

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkInteractionBootstrap : MonoBehaviour
    {
        private const float DefaultCanvasScale = 0.005f;

        private enum ControllerTriggerOwner
        {
            None,
            Left,
            Right,
            Unknown
        }

        [SerializeField] private SceneTalkOrchestrator orchestrator;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 1.6f, -1.5f);
        [SerializeField, Range(20f, 120f)] private float cameraFieldOfView = 90f;
        [SerializeField] private Vector3 canvasPosition = new Vector3(0f, 1.5f, 1.4f);
        [SerializeField] private Vector3 canvasEulerAngles = Vector3.zero;
        [SerializeField] private float canvasScale = 0.005f;
        [SerializeField] private bool configureOnAwake = true;
        [SerializeField] private bool normalizeTrackedOrigin = true;
        [SerializeField] private float trackedOriginCameraYOffset = 1.6f;
        [SerializeField] private bool useHeadsetRelativeCanvas = true;
        [SerializeField] private float headsetCanvasDistance = 1.6f;
        [SerializeField] private float headsetCanvasVerticalOffset = -0.05f;
        [SerializeField] private float headsetCanvasRecenterDelay = 0.25f;
        [SerializeField] private bool enableControllerShortcuts = true;
        [SerializeField] private bool enableFlowUi = true;
        [SerializeField] private bool ensureQuitButton = true;
        [SerializeField] private bool enableControllerRay = true;
        [SerializeField] private bool enableControllerVisuals = true;
        [SerializeField] private float controllerRayLength = 6f;
        [SerializeField] private float controllerRayLineWidth = 0.01f;
        [SerializeField] private Vector3 controllerRayEulerOffset = Vector3.zero;
        [SerializeField] private bool transformControllerPoseFromTrackingSpace = true;
        [SerializeField] private Color controllerRayColor = new Color(0.1f, 0.55f, 1f, 0.85f);
        [SerializeField] private Color controllerRayHitColor = new Color(0.2f, 1f, 0.65f, 1f);
        [SerializeField] private Color leftControllerVisualColor = new Color(0.08f, 0.38f, 0.95f, 1f);
        [SerializeField] private Color rightControllerVisualColor = new Color(0.05f, 0.75f, 0.55f, 1f);
        [SerializeField] private Material controllerRayMaterial;
        [SerializeField] private Material controllerVisualMaterial;
        [SerializeField] private LineRenderer leftControllerRay;
        [SerializeField] private LineRenderer rightControllerRay;
        [SerializeField] private Transform leftControllerVisual;
        [SerializeField] private Transform rightControllerVisual;
        [SerializeField] private float triggerPressThreshold = 0.2f;

        private readonly List<InputDevice> controllerDevices = new List<InputDevice>(8);
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(16);
        private bool primaryShortcutHeld;
        private bool finishShortcutHeld;
        private bool recenterShortcutHeld;
        private bool leftTriggerHeld;
        private bool rightTriggerHeld;
        private bool unknownTriggerHeld;
        private ControllerTriggerOwner activeSpeechTriggerOwner = ControllerTriggerOwner.None;
        private GameObject hoveredRayTarget;
        private PointerEventData hoveredRayPointerEventData;

        private void Awake()
        {
            if (configureOnAwake)
            {
                Configure();
            }
        }

        public void Configure()
        {
            orchestrator = ResolveOrchestrator(orchestrator);
            interactionCamera = ResolveCamera(interactionCamera);
            worldCanvas = ResolveCanvas(worldCanvas);

            ConfigureCamera(interactionCamera);
            ConfigureCanvas(worldCanvas, interactionCamera);
            EnsureEventSystem();
            EnsureControllerRayVisuals();
            EnsureControllerVisuals();
            if (enableFlowUi)
            {
                EnsureFlowUiController();
            }
            else
            {
                EnsureQuitButton();
            }

            if (Application.isPlaying && useHeadsetRelativeCanvas)
            {
                StartCoroutine(RecenterCanvasAfterTracking());
            }

            // Cleanup duplicate AudioListeners to prevent Unity warnings
            var allListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allListeners.Length > 1)
            {
                AudioListener activeListener = null;
                foreach (var listener in allListeners)
                {
                    if (listener.gameObject.activeInHierarchy && listener.enabled)
                    {
                        if (activeListener == null)
                        {
                            activeListener = listener;
                        }
                        else
                        {
                            Debug.LogWarning($"[SceneTalkVR] Found duplicate active AudioListener on GameObject '{listener.gameObject.name}'. Disabling component to ensure a single active listener.", listener);
                            listener.enabled = false;
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (interactionCamera != null)
            {
                interactionCamera.fieldOfView = cameraFieldOfView;
            }

            if (enableControllerRay)
            {
                HandleControllerRay();
            }

            if (enableControllerShortcuts)
            {
                HandleControllerShortcuts();
            }
        }

        public void RecenterCanvasInFrontOfHeadset()
        {
            interactionCamera = ResolveCamera(interactionCamera);
            worldCanvas = ResolveCanvas(worldCanvas);

            if (interactionCamera == null || worldCanvas == null)
            {
                return;
            }

            var forward = Vector3.ProjectOnPlane(interactionCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            worldCanvas.transform.position = interactionCamera.transform.position
                + forward * headsetCanvasDistance
                + Vector3.up * headsetCanvasVerticalOffset;
            worldCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ApplyUserSettings(SceneTalkUserSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            worldCanvas = ResolveCanvas(worldCanvas);
            if (worldCanvas == null)
            {
                return;
            }

            canvasScale = DefaultCanvasScale * settings.uiScale;
            worldCanvas.transform.localScale = Vector3.one * canvasScale;
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

        private SceneTalkOrchestrator ResolveOrchestrator(SceneTalkOrchestrator preferredOrchestrator)
        {
            if (preferredOrchestrator != null)
            {
                return preferredOrchestrator;
            }

            return FindFirst<SceneTalkOrchestrator>();
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
            EnsureTrackedPoseDriver(cameraToConfigure);

            if (IsTrackedCamera(cameraToConfigure))
            {
                if (normalizeTrackedOrigin)
                {
                    NormalizeTrackedCameraOrigin(cameraToConfigure.transform, trackedOriginCameraYOffset);
                }
            }
            else
            {
                cameraToConfigure.transform.position = cameraPosition;
                cameraToConfigure.transform.rotation = Quaternion.identity;
            }

            cameraToConfigure.fieldOfView = cameraFieldOfView;
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
            canvasToConfigure.transform.localScale = Vector3.one * canvasScale;
            ConfigureCanvasHitArea(canvasToConfigure);

            if (!Application.isPlaying || !useHeadsetRelativeCanvas || cameraToUse == null)
            {
                canvasToConfigure.transform.position = canvasPosition;
                canvasToConfigure.transform.rotation = Quaternion.Euler(canvasEulerAngles);
            }

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

        private void ConfigureCanvasHitArea(Canvas canvasToConfigure)
        {
            var canvasRect = canvasToConfigure.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            var requiredSize = new Vector2(720f, 420f);
            var panel = canvasToConfigure.transform.Find("Panel") as RectTransform;
            if (panel != null)
            {
                requiredSize = new Vector2(
                    Mathf.Max(requiredSize.x, panel.sizeDelta.x),
                    Mathf.Max(requiredSize.y, panel.sizeDelta.y));
            }

            canvasRect.sizeDelta = new Vector2(
                Mathf.Max(canvasRect.sizeDelta.x, requiredSize.x),
                Mathf.Max(canvasRect.sizeDelta.y, requiredSize.y));
        }

        private void EnsureQuitButton()
        {
            if (!ensureQuitButton)
            {
                return;
            }

            worldCanvas = ResolveCanvas(worldCanvas);
            if (worldCanvas == null)
            {
                return;
            }

            var quitButton = FindButtonByName("QuitButton");
            if (quitButton == null)
            {
                quitButton = CreateQuitButton();
            }

            if (quitButton == null)
            {
                return;
            }

            quitButton.onClick.RemoveListener(QuitApplication);
            quitButton.onClick.AddListener(QuitApplication);
        }

        private void EnsureFlowUiController()
        {
            orchestrator = ResolveOrchestrator(orchestrator);
            worldCanvas = ResolveCanvas(worldCanvas);

            if (orchestrator == null || worldCanvas == null)
            {
                return;
            }

            var flowUiController = GetComponent<SceneTalkFlowUiController>();
            if (flowUiController == null)
            {
                flowUiController = gameObject.AddComponent<SceneTalkFlowUiController>();
            }

            flowUiController.Configure(orchestrator, worldCanvas, this);
        }

        private Button FindButtonByName(string buttonName)
        {
            if (worldCanvas == null)
            {
                return null;
            }

            foreach (var button in worldCanvas.GetComponentsInChildren<Button>(true))
            {
                if (button != null && button.name == buttonName)
                {
                    return button;
                }
            }

            return null;
        }

        private Button CreateQuitButton()
        {
            var parent = ResolveUiButtonParent();
            if (parent == null)
            {
                return null;
            }

            var buttonObject = new GameObject("QuitButton");
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.58f, 0.18f, 0.18f, 1f);

            var button = buttonObject.AddComponent<Button>();
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(140f, 44f);
            rect.anchoredPosition = new Vector2(0f, -178f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);

            var label = labelObject.AddComponent<Text>();
            label.text = "Quit";
            label.font = ResolveRuntimeFont();
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.sizeDelta = rect.sizeDelta;
            labelRect.anchoredPosition = Vector2.zero;
            return button;
        }

        private Transform ResolveUiButtonParent()
        {
            if (worldCanvas == null)
            {
                return null;
            }

            var panel = worldCanvas.transform.Find("Panel");
            return panel != null ? panel : worldCanvas.transform;
        }

        private static Font ResolveRuntimeFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        private IEnumerator RecenterCanvasAfterTracking()
        {
            yield return null;

            if (headsetCanvasRecenterDelay > 0f)
            {
                yield return new WaitForSeconds(headsetCanvasRecenterDelay);
            }

            RecenterCanvasInFrontOfHeadset();
        }

        private void HandleControllerShortcuts()
        {
            var primaryPressed = ReadAnyButton(CommonUsages.primaryButton);

            if (ConsumePress(primaryPressed, ref primaryShortcutHeld))
            {
                RunPrimaryAction();
            }

            var finishPressed = ReadAnyButton(CommonUsages.secondaryButton)
                || ReadAnyButton(CommonUsages.menuButton);

            if (ConsumePress(finishPressed, ref finishShortcutHeld))
            {
                orchestrator = ResolveOrchestrator(orchestrator);
                orchestrator?.FinishPractice();
            }

            var recenterPressed = ReadAnyButton(CommonUsages.gripButton)
                || ReadAnyButton(CommonUsages.primary2DAxisClick);

            if (ConsumePress(recenterPressed, ref recenterShortcutHeld))
            {
                RecenterCanvasInFrontOfHeadset();
            }
        }

        private void HandleControllerRay()
        {
            interactionCamera = ResolveCamera(interactionCamera);
            worldCanvas = ResolveCanvas(worldCanvas);

            if (interactionCamera == null || worldCanvas == null || EventSystem.current == null)
            {
                HideControllerRays();
                HideControllerVisuals();
                UpdateHoveredRayTarget(null, null);
                return;
            }

            EnsureControllerRayVisuals();
            EnsureControllerVisuals();
            HideControllerRays();
            HideControllerVisuals();
            RefreshControllerDevices();

            GameObject nextHoverTarget = null;
            PointerEventData nextHoverEventData = null;
            var hasLeftController = false;
            var hasRightController = false;
            var hasUnknownController = false;

            foreach (var device in controllerDevices)
            {
                if (!device.isValid || !TryGetControllerPose(device, out var position, out var rotation))
                {
                    continue;
                }

                TransformControllerPoseToWorld(ref position, ref rotation);

                var isLeftController = IsLeftController(device);
                var isRightController = IsRightController(device);
                hasLeftController |= isLeftController;
                hasRightController |= isRightController;
                hasUnknownController |= !isLeftController && !isRightController;

                var rayLine = ResolveControllerRayLine(device);
                var rayRotation = rotation * Quaternion.Euler(controllerRayEulerOffset);
                var controllerVisual = ResolveControllerVisual(device);
                var ray = new Ray(position, rayRotation * Vector3.forward);
                var hitCanvas = TryRaycastWorldCanvas(ray, out var rayEnd, out var clickTarget, out var pointerEventData);
                DrawControllerRay(rayLine, ray.origin, rayEnd, hitCanvas);
                DrawControllerVisual(controllerVisual, position, rayRotation, hitCanvas);

                if (clickTarget != null && nextHoverTarget == null)
                {
                    nextHoverTarget = clickTarget;
                    nextHoverEventData = pointerEventData;
                }

                HandleControllerTrigger(device, ReadTriggerPressed(device), clickTarget, pointerEventData);
            }

            if (!hasLeftController)
            {
                ReleaseMissingControllerTrigger(ControllerTriggerOwner.Left);
                leftTriggerHeld = false;
            }

            if (!hasRightController)
            {
                ReleaseMissingControllerTrigger(ControllerTriggerOwner.Right);
                rightTriggerHeld = false;
            }

            if (!hasUnknownController)
            {
                ReleaseMissingControllerTrigger(ControllerTriggerOwner.Unknown);
                unknownTriggerHeld = false;
            }

            UpdateHoveredRayTarget(nextHoverTarget, nextHoverEventData);
        }

        private bool TryRaycastWorldCanvas(Ray ray, out Vector3 rayEnd, out GameObject clickTarget, out PointerEventData pointerEventData)
        {
            rayEnd = ray.origin + ray.direction * controllerRayLength;
            clickTarget = null;
            pointerEventData = null;

            if (worldCanvas == null)
            {
                return false;
            }

            var canvasPlane = new Plane(worldCanvas.transform.forward, worldCanvas.transform.position);
            if (!canvasPlane.Raycast(ray, out var distance) || distance < 0f || distance > controllerRayLength)
            {
                return false;
            }

            rayEnd = ray.GetPoint(distance);
            pointerEventData = CreatePointerEventData(rayEnd);
            clickTarget = FindButtonAtWorldPoint(rayEnd)?.gameObject
                ?? FindRaycastClickTarget(pointerEventData)
                ?? FindClickTargetAtWorldPoint(rayEnd);
            return clickTarget != null;
        }

        private PointerEventData CreatePointerEventData(Vector3 worldPoint)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var screenPoint = RectTransformUtility.WorldToScreenPoint(interactionCamera, worldPoint);
            return new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                clickTime = Time.unscaledTime,
                delta = Vector2.zero,
                position = screenPoint,
                pressPosition = screenPoint,
                pointerId = -1
            };
        }

        private GameObject FindRaycastClickTarget(PointerEventData pointerEventData)
        {
            if (pointerEventData == null || worldCanvas == null)
            {
                return null;
            }

            var raycaster = worldCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                return null;
            }

            uiRaycastResults.Clear();
            raycaster.Raycast(pointerEventData, uiRaycastResults);

            foreach (var result in uiRaycastResults)
            {
                var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
                if (clickTarget != null)
                {
                    return clickTarget;
                }
            }

            return null;
        }

        private GameObject FindClickTargetAtWorldPoint(Vector3 worldPoint)
        {
            if (worldCanvas == null)
            {
                return null;
            }

            foreach (var rectTransform in worldCanvas.GetComponentsInChildren<RectTransform>(false))
            {
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var localPoint = rectTransform.InverseTransformPoint(worldPoint);
                if (!rectTransform.rect.Contains(localPoint))
                {
                    continue;
                }

                var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(rectTransform.gameObject);
                if (clickTarget != null)
                {
                    return clickTarget;
                }
            }

            return null;
        }

        private void DispatchRayClick(GameObject clickTarget, PointerEventData pointerEventData)
        {
            if (clickTarget == null || pointerEventData == null)
            {
                return;
            }

            pointerEventData.pointerEnter = clickTarget;
            pointerEventData.pointerPress = clickTarget;
            pointerEventData.rawPointerPress = clickTarget;
            pointerEventData.eligibleForClick = true;

            ExecuteEvents.Execute(clickTarget, pointerEventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(clickTarget, pointerEventData, ExecuteEvents.pointerUpHandler);

            if (clickTarget.TryGetComponent(out Button button) && button.IsActive() && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }

            ExecuteEvents.Execute(clickTarget, pointerEventData, ExecuteEvents.pointerClickHandler);
        }

        private Button FindButtonAtWorldPoint(Vector3 worldPoint)
        {
            if (worldCanvas == null)
            {
                return null;
            }

            Button bestButton = null;
            var bestDepth = int.MinValue;

            foreach (var button in worldCanvas.GetComponentsInChildren<Button>(false))
            {
                if (button == null || !button.IsActive() || !button.interactable)
                {
                    continue;
                }

                var rectTransform = button.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                var localPoint = rectTransform.InverseTransformPoint(worldPoint);
                if (!rectTransform.rect.Contains(localPoint))
                {
                    continue;
                }

                var depth = button.transform.GetSiblingIndex();
                if (depth >= bestDepth)
                {
                    bestButton = button;
                    bestDepth = depth;
                }
            }

            return bestButton;
        }

        private void UpdateHoveredRayTarget(GameObject nextTarget, PointerEventData pointerEventData)
        {
            if (hoveredRayTarget == nextTarget)
            {
                if (pointerEventData != null)
                {
                    hoveredRayPointerEventData = pointerEventData;
                }

                return;
            }

            var eventData = pointerEventData ?? CreateFallbackPointerEventData();
            if (hoveredRayTarget != null && eventData != null)
            {
                ExecuteEvents.Execute(hoveredRayTarget, eventData, ExecuteEvents.pointerExitHandler);
            }

            hoveredRayTarget = nextTarget;
            hoveredRayPointerEventData = nextTarget == null ? null : eventData;

            if (hoveredRayTarget != null && eventData != null)
            {
                eventData.pointerEnter = hoveredRayTarget;
                ExecuteEvents.Execute(hoveredRayTarget, eventData, ExecuteEvents.pointerEnterHandler);
            }
        }

        private PointerEventData CreateFallbackPointerEventData()
        {
            return EventSystem.current == null ? null : new PointerEventData(EventSystem.current);
        }

        private void EnsureControllerRayVisuals()
        {
            if (!enableControllerRay)
            {
                return;
            }

            leftControllerRay = EnsureControllerRayLine(leftControllerRay, "SceneTalkVR Left Controller Ray");
            rightControllerRay = EnsureControllerRayLine(rightControllerRay, "SceneTalkVR Right Controller Ray");
        }

        private void EnsureControllerVisuals()
        {
            if (!enableControllerVisuals)
            {
                return;
            }

            leftControllerVisual = EnsureControllerVisual(
                leftControllerVisual,
                "SceneTalkVR Left Controller Visual",
                leftControllerVisualColor);
            rightControllerVisual = EnsureControllerVisual(
                rightControllerVisual,
                "SceneTalkVR Right Controller Visual",
                rightControllerVisualColor);
        }

        private LineRenderer EnsureControllerRayLine(LineRenderer rayLine, string objectName)
        {
            if (rayLine == null)
            {
                var rayTransform = transform.Find(objectName);
                var rayObject = rayTransform == null ? new GameObject(objectName) : rayTransform.gameObject;
                rayObject.transform.SetParent(transform, false);
                rayLine = rayObject.GetComponent<LineRenderer>();

                if (rayLine == null)
                {
                    rayLine = rayObject.AddComponent<LineRenderer>();
                }
            }

            ConfigureControllerRayLine(rayLine);
            return rayLine;
        }

        private void ConfigureControllerRayLine(LineRenderer rayLine)
        {
            if (rayLine == null)
            {
                return;
            }

            rayLine.enabled = false;
            rayLine.positionCount = 2;
            rayLine.useWorldSpace = true;
            rayLine.widthMultiplier = controllerRayLineWidth;
            rayLine.numCapVertices = 4;
            rayLine.numCornerVertices = 2;
            rayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rayLine.receiveShadows = false;
            rayLine.startColor = controllerRayColor;
            rayLine.endColor = controllerRayColor;

            var material = ResolveControllerRayMaterial();
            if (material != null)
            {
                rayLine.sharedMaterial = material;
            }
        }

        private Transform EnsureControllerVisual(Transform visual, string objectName, Color color)
        {
            if (visual == null)
            {
                var visualTransform = transform.Find(objectName);
                visual = visualTransform == null ? new GameObject(objectName).transform : visualTransform;
                visual.SetParent(transform, false);
            }

            if (visual.childCount == 0)
            {
                CreateControllerVisualParts(visual, color);
            }

            visual.gameObject.SetActive(false);
            return visual;
        }

        private void CreateControllerVisualParts(Transform visualRoot, Color color)
        {
            var body = CreateControllerPrimitive(PrimitiveType.Capsule, "Body", visualRoot, color);
            body.localPosition = new Vector3(0f, -0.01f, 0.035f);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localScale = new Vector3(0.045f, 0.075f, 0.045f);

            var tip = CreateControllerPrimitive(PrimitiveType.Sphere, "Ray Origin", visualRoot, controllerRayColor);
            tip.localPosition = new Vector3(0f, 0f, 0.13f);
            tip.localScale = Vector3.one * 0.035f;

            var trigger = CreateControllerPrimitive(PrimitiveType.Cube, "Trigger", visualRoot, color * 0.7f);
            trigger.localPosition = new Vector3(0f, -0.045f, 0.045f);
            trigger.localScale = new Vector3(0.025f, 0.035f, 0.055f);
        }

        private Transform CreateControllerPrimitive(PrimitiveType primitiveType, string objectName, Transform parent, Color color)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);

            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntimeOrImmediate(collider);
            }

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateControllerVisualMaterial(color);
            }

            return primitive.transform;
        }

        private Material CreateControllerVisualMaterial(Color color)
        {
            var baseMaterial = ResolveControllerVisualMaterial();
            if (baseMaterial == null)
            {
                return null;
            }

            var material = new Material(baseMaterial)
            {
                name = "SceneTalkVR Controller Visual Material",
                color = color
            };
            return material;
        }

        private Material ResolveControllerVisualMaterial()
        {
            if (controllerVisualMaterial != null)
            {
                return controllerVisualMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return ResolveControllerRayMaterial();
            }

            controllerVisualMaterial = new Material(shader)
            {
                name = "SceneTalkVR Controller Visual Base Material",
                color = Color.white
            };
            return controllerVisualMaterial;
        }

        private Material ResolveControllerRayMaterial()
        {
            if (controllerRayMaterial != null)
            {
                return controllerRayMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            controllerRayMaterial = new Material(shader)
            {
                name = "SceneTalkVR Controller Ray Material",
                color = controllerRayColor
            };
            return controllerRayMaterial;
        }

        private void DrawControllerRay(LineRenderer rayLine, Vector3 start, Vector3 end, bool hitCanvas)
        {
            if (rayLine == null)
            {
                return;
            }

            var color = hitCanvas ? controllerRayHitColor : controllerRayColor;
            rayLine.enabled = true;
            rayLine.startColor = color;
            rayLine.endColor = color;
            rayLine.SetPosition(0, start);
            rayLine.SetPosition(1, end);
        }

        private void HideControllerRays()
        {
            if (leftControllerRay != null)
            {
                leftControllerRay.enabled = false;
            }

            if (rightControllerRay != null)
            {
                rightControllerRay.enabled = false;
            }
        }

        private void DrawControllerVisual(Transform controllerVisual, Vector3 position, Quaternion rotation, bool hitCanvas)
        {
            if (controllerVisual == null)
            {
                return;
            }

            controllerVisual.gameObject.SetActive(true);
            controllerVisual.SetPositionAndRotation(position, rotation);

            var rayOrigin = controllerVisual.Find("Ray Origin");
            if (rayOrigin != null
                && rayOrigin.TryGetComponent(out Renderer renderer)
                && renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial.color = hitCanvas ? controllerRayHitColor : controllerRayColor;
            }
        }

        private void HideControllerVisuals()
        {
            if (leftControllerVisual != null)
            {
                leftControllerVisual.gameObject.SetActive(false);
            }

            if (rightControllerVisual != null)
            {
                rightControllerVisual.gameObject.SetActive(false);
            }
        }

        private LineRenderer ResolveControllerRayLine(InputDevice device)
        {
            return IsLeftController(device) ? leftControllerRay : rightControllerRay;
        }

        private Transform ResolveControllerVisual(InputDevice device)
        {
            return IsLeftController(device) ? leftControllerVisual : rightControllerVisual;
        }

        private void TransformControllerPoseToWorld(ref Vector3 position, ref Quaternion rotation)
        {
            if (!transformControllerPoseFromTrackingSpace)
            {
                return;
            }

            var trackingSpace = ResolveTrackingSpaceTransform();
            if (trackingSpace == null)
            {
                return;
            }

            position = trackingSpace.TransformPoint(position);
            rotation = trackingSpace.rotation * rotation;
        }

        private Transform ResolveTrackingSpaceTransform()
        {
            interactionCamera = ResolveCamera(interactionCamera);
            if (interactionCamera == null)
            {
                return null;
            }

            var cameraOffset = FindAncestor(interactionCamera.transform, "Camera Offset");
            if (cameraOffset != null)
            {
                return cameraOffset;
            }

            var xrOrigin = FindAncestor(interactionCamera.transform, "XR Origin (VR)");
            return xrOrigin != null ? xrOrigin : interactionCamera.transform.parent;
        }

        private bool TryGetControllerPose(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            var hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return hasPosition && hasRotation;
        }

        private bool ReadTriggerPressed(InputDevice device)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButtonPressed) && triggerButtonPressed)
            {
                return true;
            }

            return device.TryGetFeatureValue(CommonUsages.trigger, out var triggerValue)
                && triggerValue >= triggerPressThreshold;
        }

        private void HandleControllerTrigger(
            InputDevice device,
            bool isPressed,
            GameObject clickTarget,
            PointerEventData pointerEventData)
        {
            var owner = ResolveTriggerOwner(device);
            var wasPressed = GetControllerTriggerHeld(owner);

            if (isPressed && !wasPressed)
            {
                SetControllerTriggerHeld(owner, true);

                if (clickTarget != null)
                {
                    DispatchRayClick(clickTarget, pointerEventData);
                    return;
                }

                TryBeginSpeechTriggerCapture(owner);
                return;
            }

            if (!isPressed && wasPressed)
            {
                SetControllerTriggerHeld(owner, false);
                TryEndSpeechTriggerCapture(owner);
            }
        }

        private ControllerTriggerOwner ResolveTriggerOwner(InputDevice device)
        {
            if (IsLeftController(device))
            {
                return ControllerTriggerOwner.Left;
            }

            if (IsRightController(device))
            {
                return ControllerTriggerOwner.Right;
            }

            return ControllerTriggerOwner.Unknown;
        }

        private bool GetControllerTriggerHeld(ControllerTriggerOwner owner)
        {
            return owner switch
            {
                ControllerTriggerOwner.Left => leftTriggerHeld,
                ControllerTriggerOwner.Right => rightTriggerHeld,
                ControllerTriggerOwner.Unknown => unknownTriggerHeld,
                _ => false
            };
        }

        private void SetControllerTriggerHeld(ControllerTriggerOwner owner, bool isHeld)
        {
            switch (owner)
            {
                case ControllerTriggerOwner.Left:
                    leftTriggerHeld = isHeld;
                    break;
                case ControllerTriggerOwner.Right:
                    rightTriggerHeld = isHeld;
                    break;
                case ControllerTriggerOwner.Unknown:
                    unknownTriggerHeld = isHeld;
                    break;
            }
        }

        private void TryBeginSpeechTriggerCapture(ControllerTriggerOwner owner)
        {
            orchestrator = ResolveOrchestrator(orchestrator);
            if (orchestrator == null || !orchestrator.CanUseControllerSpeechCapture())
            {
                return;
            }

            if (orchestrator.TryBeginControllerSpeechCapture())
            {
                activeSpeechTriggerOwner = owner;
            }
        }

        private void TryEndSpeechTriggerCapture(ControllerTriggerOwner owner)
        {
            if (activeSpeechTriggerOwner != owner)
            {
                return;
            }

            orchestrator = ResolveOrchestrator(orchestrator);
            orchestrator?.TryEndControllerSpeechCapture();
            activeSpeechTriggerOwner = ControllerTriggerOwner.None;
        }

        private void ReleaseMissingControllerTrigger(ControllerTriggerOwner owner)
        {
            if (activeSpeechTriggerOwner == owner)
            {
                TryEndSpeechTriggerCapture(owner);
            }
        }

        private static bool IsLeftController(InputDevice device)
        {
            return (device.characteristics & InputDeviceCharacteristics.Left) != 0;
        }

        private static bool IsRightController(InputDevice device)
        {
            return (device.characteristics & InputDeviceCharacteristics.Right) != 0;
        }

        private void RunPrimaryAction()
        {
            orchestrator = ResolveOrchestrator(orchestrator);

            if (orchestrator == null)
            {
                return;
            }

            if (hoveredRayTarget != null)
            {
                DispatchRayClick(hoveredRayTarget, hoveredRayPointerEventData ?? CreateFallbackPointerEventData());
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.Settings)
            {
                return;
            }

            if (orchestrator.IsSpeechRecording)
            {
                orchestrator.TryEndControllerSpeechCapture();
                return;
            }

            if (orchestrator.IsTurnRunning)
            {
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.Error)
            {
                orchestrator.RetryListening();
                return;
            }

            if (orchestrator.CurrentState == SceneTalkState.Listening
                && !string.IsNullOrWhiteSpace(orchestrator.LastTranscript))
            {
                orchestrator.ConfirmPracticeRequest();
                return;
            }

            if (orchestrator.IsDialogueActive)
            {
                orchestrator.StartDialogueTurn();
                return;
            }

            orchestrator.StartPractice();
        }

        private bool ReadAnyButton(InputFeatureUsage<bool> usage)
        {
            RefreshControllerDevices();

            foreach (var device in controllerDevices)
            {
                if (device.isValid && device.TryGetFeatureValue(usage, out var pressed) && pressed)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshControllerDevices()
        {
            controllerDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, controllerDevices);
        }

        private static bool ConsumePress(bool isPressed, ref bool wasPressed)
        {
            if (isPressed && !wasPressed)
            {
                wasPressed = true;
                return true;
            }

            if (!isPressed)
            {
                wasPressed = false;
            }

            return false;
        }

        private static bool IsTrackedCamera(Camera cameraToCheck)
        {
            if (cameraToCheck == null)
            {
                return false;
            }

            foreach (var behaviour in cameraToCheck.GetComponents<MonoBehaviour>())
            {
                var typeName = behaviour == null ? string.Empty : behaviour.GetType().FullName;
                if (!string.IsNullOrEmpty(typeName) && typeName.Contains("TrackedPoseDriver"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureTrackedPoseDriver(Camera cameraToConfigure)
        {
#if ENABLE_INPUT_SYSTEM
            if (cameraToConfigure == null)
            {
                return;
            }

            var trackedPoseDriver = cameraToConfigure.GetComponent<XRTrackedPoseDriver>();
            if (trackedPoseDriver != null)
            {
                ConfigureTrackedPoseDriver(trackedPoseDriver);
                return;
            }

            if (IsTrackedCamera(cameraToConfigure))
            {
                return;
            }

            if (!ShouldAutoAddTrackedPoseDriver())
            {
                return;
            }

            trackedPoseDriver = cameraToConfigure.gameObject.AddComponent<XRTrackedPoseDriver>();
            ConfigureTrackedPoseDriver(trackedPoseDriver);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool ShouldAutoAddTrackedPoseDriver()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
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
                InputActionSetupExtensions.AddBinding(action, fallbackBinding);
            }

            return action;
        }
#endif

        private static void NormalizeTrackedCameraOrigin(Transform cameraTransform, float cameraYOffset)
        {
            var xrOrigin = FindAncestor(cameraTransform, "XR Origin (VR)");
            if (xrOrigin != null)
            {
                xrOrigin.position = Vector3.zero;
                xrOrigin.rotation = Quaternion.identity;
            }

            var cameraOffset = FindAncestor(cameraTransform, "Camera Offset");
            if (cameraOffset != null)
            {
                cameraOffset.localPosition = new Vector3(0f, cameraYOffset, 0f);
                cameraOffset.localRotation = Quaternion.identity;
            }

            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }

        private static Transform FindAncestor(Transform start, string name)
        {
            var current = start;
            while (current != null)
            {
                if (current.name == name)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static void DestroyRuntimeOrImmediate(Object objectToDestroy)
        {
            if (objectToDestroy == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(objectToDestroy);
            }
            else
            {
                DestroyImmediate(objectToDestroy);
            }
        }

        private static T FindFirst<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }
    }
}
