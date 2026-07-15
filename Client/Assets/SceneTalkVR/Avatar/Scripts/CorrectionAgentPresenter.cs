using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class CorrectionAgentPresenter : MonoBehaviour
    {
        private const string VisualRootName = "Assistant Visuals";
        private const int VoiceBarCount = 5;
        private const int SatelliteCount = 3;

        [Header("Generated Agent")]
        [SerializeField] private Transform agentRoot;
        [SerializeField] private Renderer agentRenderer;
        [SerializeField] private Light agentLight;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Vector3 localOffset = new Vector3(0.9f, 1.45f, 1.8f);
        [SerializeField] private float sphereDiameter = 0.22f;
        [SerializeField] private Color coreColor = new Color(0.15f, 0.95f, 1f, 1f);
        [SerializeField] private Color emissionColor = new Color(0.1f, 0.75f, 1f, 1f);
        [SerializeField] private float lightIntensity = 1.8f;
        [SerializeField] private float lightRange = 1.5f;

        [Header("Visual Identity")]
        [SerializeField] private Color shellColor = new Color(0.56f, 1f, 0.94f, 0.18f);
        [SerializeField] private Color visorColor = new Color(0.012f, 0.035f, 0.055f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.48f, 0.14f, 1f);
        [SerializeField, Range(1f, 2f)] private float visualScaleMultiplier = 1.4f;
        [SerializeField] private bool faceMainCamera = true;
        [SerializeField] private float lookResponsiveness = 5.5f;

        [Header("Motion")]
        [SerializeField] private float fadeSeconds = 0.28f;
        [SerializeField] private float idleFloatAmplitude = 0.06f;
        [SerializeField] private float idleFloatSpeed = 1.7f;
        [SerializeField] private float speakingPulseScale = 0.11f;
        [SerializeField] private float speakingPulseSpeed = 6.5f;
        [SerializeField, Min(0.05f)] private float speakingHaloCycleSpeed = 0.34f;
        [SerializeField] private float orbitSpeed = 22f;
        [SerializeField] private float audioResponse = 15f;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicProperty = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessProperty = Shader.PropertyToID("_Smoothness");
        private static readonly int SurfaceProperty = Shader.PropertyToID("_Surface");
        private static readonly int BlendProperty = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");
        private static readonly float[] VoiceBarIdleHeights = { 0.1f, 0.16f, 0.22f, 0.16f, 0.1f };

        private readonly float[] audioSamples = new float[64];
        private readonly Transform[] voiceBars = new Transform[VoiceBarCount];
        private readonly Transform[] satellites = new Transform[SatelliteCount];

        private Material runtimeMaterial;
        private Material shellMaterial;
        private Material visorMaterial;
        private Material accentMaterial;
        private Material guideMaterial;
        private Material voiceMaterial;
        private Material pulseMaterial;
        private Mesh ringMesh;
        private Transform visualRoot;
        private Transform bodyRoot;
        private Transform primaryRing;
        private Transform secondaryRing;
        private Transform satelliteRoot;
        private Transform faceRoot;
        private Transform pulseRing;
        private Renderer shellRenderer;
        private Renderer pulseRingRenderer;
        private Renderer[] visualRenderers;
        private Transform lookTarget;
        private Coroutine visibilityRoutine;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float visibleAmount;
        private float targetVisibility;
        private float speakingEnergy;
        private float orbitAngle;
        private bool isSpeaking;

        public AudioSource AudioSource
        {
            get
            {
                EnsureAgent();
                return audioSource;
            }
        }

        public bool IsVisible => agentRoot != null
            && agentRoot.gameObject.activeSelf
            && visibleAmount > 0.001f;

        public bool TargetVisible => targetVisibility > 0.5f;

        private void Awake()
        {
            EnsureAgent();
            HideImmediate();
        }

        private void OnEnable()
        {
            EnsureAgent();
            HideImmediate();
        }

        private void Update()
        {
            if (agentRoot == null || !agentRoot.gameObject.activeSelf)
            {
                return;
            }

            var time = Time.time;
            var deltaTime = Time.deltaTime;
            UpdateSpeakingEnergy(deltaTime);
            UpdateRootMotion(time);
            UpdateOrbitMotion(time, deltaTime);
            UpdateVoiceBars(time);
            UpdateFaceDirection(deltaTime);
            UpdatePulseRing(time);
            ApplyDynamicAppearance();
        }

        public IEnumerator Show()
        {
            EnsureAgent();
            targetVisibility = 1f;
            yield return FadeTo(1f);
        }

        public IEnumerator Hide()
        {
            StopSpeaking();
            targetVisibility = 0f;
            yield return FadeTo(0f);
        }

        public void SetVisible(bool shouldShow, bool animated = true)
        {
            EnsureAgent();
            var target = shouldShow ? 1f : 0f;
            if (Mathf.Approximately(targetVisibility, target)
                && (visibilityRoutine != null || Mathf.Approximately(visibleAmount, target)))
            {
                return;
            }

            targetVisibility = target;
            if (!shouldShow)
            {
                StopSpeaking();
            }

            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            if (!animated || !Application.isPlaying || !isActiveAndEnabled)
            {
                visibleAmount = target;
                ApplyVisibility(visibleAmount);
                return;
            }

            visibilityRoutine = StartCoroutine(FadeTo(target));
        }

        public void HideImmediate()
        {
            StopSpeaking();
            EnsureAgent();
            targetVisibility = 0f;
            visibleAmount = 0f;
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            ApplyVisibility(visibleAmount);
        }

        public void ShowImmediate()
        {
            EnsureAgent();
            targetVisibility = 1f;
            visibleAmount = 1f;
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            ApplyVisibility(visibleAmount);
        }

        public void BeginSpeaking()
        {
            EnsureAgent();
            isSpeaking = true;
        }

        public void EndSpeaking()
        {
            StopSpeaking();
        }

        private void StopSpeaking()
        {
            isSpeaking = false;
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }

        private void EnsureAgent()
        {
            if (agentRoot == null)
            {
                var rootObject = new GameObject("Correction Assistant Agent");
                rootObject.transform.SetParent(transform);
                rootObject.transform.localPosition = localOffset;
                rootObject.transform.localRotation = Quaternion.identity;
                rootObject.transform.localScale = Vector3.one * sphereDiameter;
                agentRoot = rootObject.transform;
            }

            baseLocalPosition = localOffset;
            baseLocalScale = Vector3.one * sphereDiameter * visualScaleMultiplier;

            if (visualRoot == null)
            {
                visualRoot = agentRoot.Find(VisualRootName);
                if (visualRoot == null)
                {
                    RemoveLegacyVisual();
                    BuildVisualHierarchy();
                }
                else
                {
                    BindVisualHierarchy();
                }
            }

            if (agentLight == null)
            {
                agentLight = agentRoot.gameObject.AddComponent<Light>();
                agentLight.type = LightType.Point;
                agentLight.shadows = LightShadows.None;
            }

            agentLight.color = Color.Lerp(emissionColor, accentColor, 0.12f);
            agentLight.range = lightRange;

            if (audioSource == null)
            {
                audioSource = agentRoot.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 0.2f;
                audioSource.maxDistance = 4f;
                audioSource.dopplerLevel = 0f;
            }

            if (lookTarget == null && Camera.main != null)
            {
                lookTarget = Camera.main.transform;
            }

            if (!agentRoot.gameObject.activeSelf)
            {
                agentRoot.localPosition = baseLocalPosition;
                agentRoot.localRotation = Quaternion.identity;
                agentRoot.localScale = baseLocalScale;
            }

            ApplyVisibility(visibleAmount);
        }

        private void RemoveLegacyVisual()
        {
            var legacyOrb = agentRoot.Find("Glow Orb");
            if (legacyOrb != null)
            {
                DestroyImmediateSafe(legacyOrb.gameObject);
            }

            agentRenderer = null;
        }

        private void BuildVisualHierarchy()
        {
            visualRoot = CreateTransform(VisualRootName, agentRoot);
            bodyRoot = CreateTransform("Body", visualRoot);

            runtimeMaterial = CreateLitMaterial(
                "CorrectionAssistant_Core_Runtime",
                ScaleRgb(coreColor, 0.22f),
                ScaleRgb(emissionColor, 1.45f),
                false,
                0.08f,
                0.82f);
            shellMaterial = CreateLitMaterial(
                "CorrectionAssistant_Glass_Runtime",
                shellColor,
                ScaleRgb(emissionColor, 0.28f),
                true,
                0.05f,
                0.96f);
            visorMaterial = CreateLitMaterial(
                "CorrectionAssistant_Visor_Runtime",
                visorColor,
                ScaleRgb(emissionColor, 0.035f),
                false,
                0.55f,
                0.9f);
            accentMaterial = CreateLitMaterial(
                "CorrectionAssistant_Accent_Runtime",
                ScaleRgb(accentColor, 0.58f),
                ScaleRgb(accentColor, 1.7f),
                false,
                0.28f,
                0.78f);
            guideMaterial = CreateLitMaterial(
                "CorrectionAssistant_Guide_Runtime",
                new Color(0.08f, 0.48f, 0.42f, 1f),
                new Color(0.08f, 1.05f, 0.82f, 1f),
                false,
                0.18f,
                0.74f);
            voiceMaterial = CreateLitMaterial(
                "CorrectionAssistant_Voice_Runtime",
                new Color(0.72f, 1f, 0.94f, 1f),
                ScaleRgb(Color.Lerp(emissionColor, Color.white, 0.72f), 2.2f),
                false,
                0f,
                0.65f);
            pulseMaterial = CreateLitMaterial(
                "CorrectionAssistant_Pulse_Runtime",
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.32f),
                ScaleRgb(accentColor, 1.15f),
                true,
                0.1f,
                0.74f);

            var body = CreatePrimitiveChild(
                "Glow Orb",
                PrimitiveType.Sphere,
                bodyRoot,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 0.82f,
                runtimeMaterial);
            agentRenderer = body.GetComponent<Renderer>();

            var shell = CreatePrimitiveChild(
                "Glass Shell",
                PrimitiveType.Sphere,
                bodyRoot,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 0.96f,
                shellMaterial);
            shellRenderer = shell.GetComponent<Renderer>();

            ringMesh = CreateTorusMesh(0.67f, 0.026f, 48, 6);
            primaryRing = CreateRing("Primary Orbit", visualRoot, accentMaterial, Vector3.one);
            secondaryRing = CreateRing(
                "Secondary Orbit",
                visualRoot,
                guideMaterial,
                Vector3.one * 1.16f);

            satelliteRoot = CreateTransform("Satellite Orbit", visualRoot);
            for (var index = 0; index < satellites.Length; index++)
            {
                var angle = index * Mathf.PI * 2f / satellites.Length;
                var position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.79f;
                var satellite = CreatePrimitiveChild(
                    $"Guide Node {index + 1}",
                    PrimitiveType.Sphere,
                    satelliteRoot,
                    position,
                    Quaternion.identity,
                    Vector3.one * (index == 0 ? 0.105f : 0.075f),
                    index == 0 ? accentMaterial : guideMaterial);
                satellites[index] = satellite.transform;
            }

            faceRoot = CreateTransform("Voice Face", visualRoot);
            var visor = CreatePrimitiveChild(
                "Voice Visor",
                PrimitiveType.Sphere,
                faceRoot,
                new Vector3(0f, 0f, 0.39f),
                Quaternion.identity,
                new Vector3(0.56f, 0.29f, 0.12f),
                visorMaterial);
            visor.GetComponent<Renderer>().sortingOrder = 2;

            for (var index = 0; index < voiceBars.Length; index++)
            {
                var bar = CreatePrimitiveChild(
                    $"Voice Bar {index + 1}",
                    PrimitiveType.Sphere,
                    faceRoot,
                    new Vector3((index - 2) * 0.095f, 0f, 0.505f),
                    Quaternion.identity,
                    new Vector3(0.047f, VoiceBarIdleHeights[index], 0.038f),
                    voiceMaterial);
                bar.GetComponent<Renderer>().sortingOrder = 3;
                voiceBars[index] = bar.transform;
            }

            pulseRing = CreateRing("Speaking Pulse", faceRoot, pulseMaterial, Vector3.one * 0.88f);
            pulseRing.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pulseRingRenderer = pulseRing.GetComponent<Renderer>();
            pulseRingRenderer.enabled = false;

            visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        private void BindVisualHierarchy()
        {
            bodyRoot = visualRoot.Find("Body");
            primaryRing = visualRoot.Find("Primary Orbit");
            secondaryRing = visualRoot.Find("Secondary Orbit");
            satelliteRoot = visualRoot.Find("Satellite Orbit");
            faceRoot = visualRoot.Find("Voice Face");
            pulseRing = faceRoot != null ? faceRoot.Find("Speaking Pulse") : null;

            var body = bodyRoot != null ? bodyRoot.Find("Glow Orb") : null;
            agentRenderer = body != null ? body.GetComponent<Renderer>() : null;
            runtimeMaterial = agentRenderer != null ? agentRenderer.sharedMaterial : null;

            var shell = bodyRoot != null ? bodyRoot.Find("Glass Shell") : null;
            shellRenderer = shell != null ? shell.GetComponent<Renderer>() : null;
            shellMaterial = shellRenderer != null ? shellRenderer.sharedMaterial : null;

            visorMaterial = GetSharedMaterial(faceRoot, "Voice Visor");
            voiceMaterial = GetSharedMaterial(faceRoot, "Voice Bar 1");
            accentMaterial = GetSharedMaterial(visualRoot, "Primary Orbit");
            guideMaterial = GetSharedMaterial(visualRoot, "Secondary Orbit");
            pulseRingRenderer = pulseRing != null ? pulseRing.GetComponent<Renderer>() : null;
            pulseMaterial = pulseRingRenderer != null ? pulseRingRenderer.sharedMaterial : null;

            for (var index = 0; index < voiceBars.Length; index++)
            {
                voiceBars[index] = faceRoot != null
                    ? faceRoot.Find($"Voice Bar {index + 1}")
                    : null;
            }

            for (var index = 0; index < satellites.Length; index++)
            {
                satellites[index] = satelliteRoot != null
                    ? satelliteRoot.Find($"Guide Node {index + 1}")
                    : null;
            }

            var ringFilter = primaryRing != null ? primaryRing.GetComponent<MeshFilter>() : null;
            ringMesh = ringFilter != null ? ringFilter.sharedMesh : null;
            visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        private IEnumerator FadeTo(float target)
        {
            EnsureAgent();
            if (target > 0f && agentRoot != null)
            {
                agentRoot.gameObject.SetActive(true);
            }

            var start = visibleAmount;
            var duration = Mathf.Max(0.01f, fadeSeconds * Mathf.Max(0.35f, Mathf.Abs(target - start)));
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = normalized * normalized * (3f - 2f * normalized);
                visibleAmount = Mathf.Lerp(start, target, eased);
                ApplyVisibility(visibleAmount);
                yield return null;
            }

            visibleAmount = target;
            ApplyVisibility(visibleAmount);
            visibilityRoutine = null;
        }

        private void UpdateSpeakingEnergy(float deltaTime)
        {
            var targetEnergy = 0f;
            if (isSpeaking)
            {
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.GetOutputData(audioSamples, 0);
                    var sum = 0f;
                    for (var index = 0; index < audioSamples.Length; index++)
                    {
                        sum += audioSamples[index] * audioSamples[index];
                    }

                    targetEnergy = Mathf.Clamp01(Mathf.Sqrt(sum / audioSamples.Length) * 14f);
                }

                var fallbackPulse = 0.42f
                    + Mathf.Sin(Time.time * speakingPulseSpeed * 0.62f) * 0.16f;
                targetEnergy = Mathf.Max(targetEnergy, fallbackPulse);
            }

            var damping = 1f - Mathf.Exp(-Mathf.Max(1f, audioResponse) * deltaTime);
            speakingEnergy = Mathf.Lerp(speakingEnergy, targetEnergy, damping);
        }

        private void UpdateRootMotion(float time)
        {
            var vertical = Mathf.Sin(time * idleFloatSpeed) * idleFloatAmplitude;
            var lateral = Mathf.Sin(time * idleFloatSpeed * 0.53f + 0.8f)
                * idleFloatAmplitude
                * 0.18f;
            var depth = Mathf.Cos(time * idleFloatSpeed * 0.39f)
                * idleFloatAmplitude
                * 0.1f;
            agentRoot.localPosition = baseLocalPosition + new Vector3(lateral, vertical, depth);

            var visibilityScale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(visibleAmount));
            var breathing = 1f + Mathf.Sin(time * idleFloatSpeed * 0.72f) * 0.018f;
            var voicePulse = speakingEnergy
                * (speakingPulseScale * 0.55f
                    + Mathf.Sin(time * speakingPulseSpeed * 0.58f) * speakingPulseScale * 0.2f);
            agentRoot.localScale = baseLocalScale
                * Mathf.Max(0.001f, visibilityScale * (breathing + voicePulse));

            if (bodyRoot != null)
            {
                bodyRoot.localRotation = Quaternion.Euler(
                    Mathf.Sin(time * 0.72f) * 2.5f,
                    time * (7f + speakingEnergy * 9f),
                    Mathf.Cos(time * 0.61f) * 2f);
            }
        }

        private void UpdateOrbitMotion(float time, float deltaTime)
        {
            orbitAngle = Mathf.Repeat(
                orbitAngle + deltaTime * orbitSpeed * (1f + speakingEnergy * 2.8f),
                360f);

            if (primaryRing != null)
            {
                primaryRing.localRotation = Quaternion.Euler(
                    18f + Mathf.Sin(time * 0.58f) * 5f,
                    orbitAngle * 0.35f,
                    62f + Mathf.Cos(time * 0.43f) * 5f);
            }

            var secondaryRotation = Quaternion.Euler(
                68f + Mathf.Cos(time * 0.47f) * 7f,
                -orbitAngle * 0.55f,
                18f + Mathf.Sin(time * 0.51f) * 6f);
            if (secondaryRing != null)
            {
                secondaryRing.localRotation = secondaryRotation;
            }

            if (satelliteRoot != null)
            {
                satelliteRoot.localRotation = secondaryRotation
                    * Quaternion.AngleAxis(orbitAngle * 1.8f, Vector3.up);
            }

            for (var index = 0; index < satellites.Length; index++)
            {
                if (satellites[index] == null)
                {
                    continue;
                }

                var pulse = 1f
                    + speakingEnergy * (0.22f + 0.12f * Mathf.Sin(time * 8f + index * 1.8f));
                var baseSize = index == 0 ? 0.105f : 0.075f;
                satellites[index].localScale = Vector3.one * baseSize * pulse;
            }
        }

        private void UpdateVoiceBars(float time)
        {
            for (var index = 0; index < voiceBars.Length; index++)
            {
                if (voiceBars[index] == null)
                {
                    continue;
                }

                var wave = 0.5f
                    + Mathf.Sin(time * speakingPulseSpeed * 0.92f + index * 1.28f) * 0.5f;
                var activeHeight = 0.11f + wave * 0.2f;
                var height = Mathf.Lerp(VoiceBarIdleHeights[index], activeHeight, speakingEnergy);
                voiceBars[index].localScale = new Vector3(0.047f, height, 0.038f);
            }
        }

        private void UpdateFaceDirection(float deltaTime)
        {
            if (!faceMainCamera || faceRoot == null)
            {
                return;
            }

            if (lookTarget == null && Camera.main != null)
            {
                lookTarget = Camera.main.transform;
            }

            if (lookTarget == null)
            {
                return;
            }

            var direction = lookTarget.position - faceRoot.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var responsiveness = Mathf.Max(0.1f, lookResponsiveness) * (1f + speakingEnergy * 0.35f);
            var damping = 1f - Mathf.Exp(-responsiveness * deltaTime);
            faceRoot.rotation = Quaternion.Slerp(faceRoot.rotation, targetRotation, damping);
        }

        private void UpdatePulseRing(float time)
        {
            if (pulseRing == null || pulseRingRenderer == null)
            {
                return;
            }

            var showPulse = isSpeaking && visibleAmount > 0.02f;
            pulseRingRenderer.enabled = showPulse;
            if (!showPulse)
            {
                return;
            }

            var phase = Mathf.Repeat(
                time * speakingHaloCycleSpeed * (1f + speakingEnergy * 0.18f),
                1f);
            var wave = 0.5f - Mathf.Cos(phase * Mathf.PI * 2f) * 0.5f;
            var scale = 0.92f + wave * 0.38f;
            pulseRing.localScale = Vector3.one * scale;

            var pulseColor = accentColor;
            pulseColor.a = (0.12f + speakingEnergy * 0.28f)
                * (0.82f + wave * 0.18f)
                * visibleAmount;
            SetMaterialColor(pulseMaterial, pulseColor);
            SetEmission(
                pulseMaterial,
                ScaleRgb(
                    accentColor,
                    (0.45f + wave * 0.55f) * (0.72f + speakingEnergy * 0.86f)));
        }

        private void ApplyVisibility(float amount)
        {
            var isVisible = amount > 0.001f || targetVisibility > 0.001f;
            if (agentRoot != null)
            {
                agentRoot.gameObject.SetActive(isVisible);
                var visibilityScale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
                agentRoot.localScale = baseLocalScale * Mathf.Max(0.001f, visibilityScale);
            }

            if (visualRenderers != null)
            {
                for (var index = 0; index < visualRenderers.Length; index++)
                {
                    if (visualRenderers[index] != null)
                    {
                        visualRenderers[index].enabled = isVisible;
                    }
                }
            }

            if (pulseRingRenderer != null)
            {
                pulseRingRenderer.enabled = isVisible && isSpeaking;
            }

            if (agentLight != null)
            {
                agentLight.enabled = isVisible;
                agentLight.intensity = lightIntensity * Mathf.Clamp01(amount);
            }

            ApplyDynamicAppearance();
        }

        private void ApplyDynamicAppearance()
        {
            var visibility = Mathf.Clamp01(visibleAmount);
            var coreBase = ScaleRgb(coreColor, Mathf.Lerp(0.18f, 0.32f, speakingEnergy));
            coreBase.a = 1f;
            SetMaterialColor(runtimeMaterial, coreBase);
            SetEmission(
                runtimeMaterial,
                ScaleRgb(emissionColor, visibility * (0.82f + speakingEnergy * 1.65f)));

            var glass = shellColor;
            glass.a = shellColor.a * visibility * (0.8f + speakingEnergy * 0.2f);
            SetMaterialColor(shellMaterial, glass);
            SetEmission(shellMaterial, ScaleRgb(emissionColor, visibility * (0.22f + speakingEnergy * 0.24f)));

            SetEmission(accentMaterial, ScaleRgb(accentColor, visibility * (1.05f + speakingEnergy * 1.25f)));
            SetEmission(
                guideMaterial,
                new Color(
                    0.08f * visibility,
                    (0.82f + speakingEnergy * 0.55f) * visibility,
                    (0.68f + speakingEnergy * 0.42f) * visibility,
                    1f));
            SetEmission(
                voiceMaterial,
                ScaleRgb(Color.Lerp(emissionColor, Color.white, 0.72f), visibility * (1.35f + speakingEnergy * 1.55f)));

            if (agentLight != null)
            {
                var breathe = 0.88f + Mathf.Sin(Time.time * idleFloatSpeed * 0.72f) * 0.08f;
                agentLight.intensity = lightIntensity
                    * visibility
                    * (breathe + speakingEnergy * 0.72f);
                agentLight.color = Color.Lerp(emissionColor, accentColor, speakingEnergy * 0.22f);
            }
        }

        private Transform CreateRing(
            string objectName,
            Transform parent,
            Material material,
            Vector3 localScale)
        {
            var ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localScale = localScale;
            var meshFilter = ringObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ringMesh;
            var meshRenderer = ringObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(meshRenderer, material);
            return ringObject.transform;
        }

        private static Transform CreateTransform(string objectName, Transform parent)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject CreatePrimitiveChild(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;

            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediateSafe(collider);
            }

            ConfigureRenderer(child.GetComponent<Renderer>(), material);
            return child;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateLitMaterial(
            string materialName,
            Color baseColor,
            Color emission,
            bool transparent,
            float metallic,
            float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };
            SetMaterialColor(material, baseColor);
            SetEmission(material, emission);
            if (material.HasProperty(MetallicProperty))
            {
                material.SetFloat(MetallicProperty, metallic);
            }

            if (material.HasProperty(SmoothnessProperty))
            {
                material.SetFloat(SmoothnessProperty, smoothness);
            }

            if (transparent)
            {
                ConfigureTransparentMaterial(material);
            }

            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty(SurfaceProperty))
            {
                material.SetFloat(SurfaceProperty, 1f);
            }

            if (material.HasProperty(BlendProperty))
            {
                material.SetFloat(BlendProperty, 0f);
            }

            if (material.HasProperty(SrcBlendProperty))
            {
                material.SetFloat(SrcBlendProperty, (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty(DstBlendProperty))
            {
                material.SetFloat(DstBlendProperty, (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty(ZWriteProperty))
            {
                material.SetFloat(ZWriteProperty, 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Mesh CreateTorusMesh(
            float majorRadius,
            float minorRadius,
            int majorSegments,
            int minorSegments)
        {
            var vertices = new Vector3[majorSegments * minorSegments];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[majorSegments * minorSegments * 6];

            for (var major = 0; major < majorSegments; major++)
            {
                var majorAngle = major * Mathf.PI * 2f / majorSegments;
                var radial = new Vector3(Mathf.Cos(majorAngle), 0f, Mathf.Sin(majorAngle));
                for (var minor = 0; minor < minorSegments; minor++)
                {
                    var minorAngle = minor * Mathf.PI * 2f / minorSegments;
                    var normal = radial * Mathf.Cos(minorAngle) + Vector3.up * Mathf.Sin(minorAngle);
                    var vertexIndex = major * minorSegments + minor;
                    vertices[vertexIndex] = radial * majorRadius + normal * minorRadius;
                    normals[vertexIndex] = normal;
                    uv[vertexIndex] = new Vector2(
                        major / (float)majorSegments,
                        minor / (float)minorSegments);
                }
            }

            var triangleIndex = 0;
            for (var major = 0; major < majorSegments; major++)
            {
                var nextMajor = (major + 1) % majorSegments;
                for (var minor = 0; minor < minorSegments; minor++)
                {
                    var nextMinor = (minor + 1) % minorSegments;
                    var current = major * minorSegments + minor;
                    var nextAround = nextMajor * minorSegments + minor;
                    var nextAcross = nextMajor * minorSegments + nextMinor;
                    var currentAcross = major * minorSegments + nextMinor;

                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = nextAround;
                    triangles[triangleIndex++] = nextAcross;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = nextAcross;
                    triangles[triangleIndex++] = currentAcross;
                }
            }

            var mesh = new Mesh
            {
                name = "CorrectionAssistant_Ring_Runtime",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetSharedMaterial(Transform parent, string childName)
        {
            var child = parent != null ? parent.Find(childName) : null;
            var renderer = child != null ? child.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static Color ScaleRgb(Color color, float multiplier)
        {
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, color);
            }

            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }
        }

        private static void SetEmission(Material material, Color color)
        {
            if (material == null || !material.HasProperty(EmissionColorProperty))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorProperty, color);
        }

        private void OnDestroy()
        {
            DestroyImmediateSafe(runtimeMaterial);
            DestroyImmediateSafe(shellMaterial);
            DestroyImmediateSafe(visorMaterial);
            DestroyImmediateSafe(accentMaterial);
            DestroyImmediateSafe(guideMaterial);
            DestroyImmediateSafe(voiceMaterial);
            DestroyImmediateSafe(pulseMaterial);
            DestroyImmediateSafe(ringMesh);
            runtimeMaterial = null;
            shellMaterial = null;
            visorMaterial = null;
            accentMaterial = null;
            guideMaterial = null;
            voiceMaterial = null;
            pulseMaterial = null;
            ringMesh = null;
        }

        private static void DestroyImmediateSafe(Object target)
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
