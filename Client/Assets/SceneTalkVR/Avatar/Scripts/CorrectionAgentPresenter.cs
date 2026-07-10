using System.Collections;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class CorrectionAgentPresenter : MonoBehaviour
    {
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

        [Header("Motion")]
        [SerializeField] private float fadeSeconds = 0.2f;
        [SerializeField] private float idleFloatAmplitude = 0.06f;
        [SerializeField] private float idleFloatSpeed = 1.7f;
        [SerializeField] private float speakingPulseScale = 0.16f;
        [SerializeField] private float speakingPulseSpeed = 5.5f;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private Material runtimeMaterial;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float visibleAmount;
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
            if (agentRoot == null || visibleAmount <= 0f)
            {
                return;
            }

            var floatOffset = Mathf.Sin(Time.time * idleFloatSpeed) * idleFloatAmplitude;
            agentRoot.localPosition = baseLocalPosition + Vector3.up * floatOffset;

            var pulse = isSpeaking
                ? 1f + Mathf.Sin(Time.time * speakingPulseSpeed) * speakingPulseScale
                : 1f;
            agentRoot.localScale = baseLocalScale * Mathf.Max(0.05f, pulse);
        }

        public IEnumerator Show()
        {
            EnsureAgent();
            yield return FadeTo(1f);
        }

        public IEnumerator Hide()
        {
            StopSpeaking();
            yield return FadeTo(0f);
        }

        public void HideImmediate()
        {
            StopSpeaking();
            EnsureAgent();
            visibleAmount = 0f;
            ApplyVisibility(visibleAmount);
        }

        public void ShowImmediate()
        {
            EnsureAgent();
            visibleAmount = 1f;
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

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Glow Orb";
                sphere.transform.SetParent(agentRoot);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localRotation = Quaternion.identity;
                sphere.transform.localScale = Vector3.one;
                agentRenderer = sphere.GetComponent<Renderer>();

                var collider = sphere.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediateSafe(collider);
                }
            }

            baseLocalPosition = localOffset;
            baseLocalScale = Vector3.one * sphereDiameter;
            agentRoot.localPosition = baseLocalPosition;

            if (agentRenderer == null)
            {
                agentRenderer = agentRoot.GetComponentInChildren<Renderer>();
            }

            if (agentRenderer != null && runtimeMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                runtimeMaterial = new Material(shader);
                runtimeMaterial.name = "CorrectionAgent_Glow_Runtime";
                runtimeMaterial.EnableKeyword("_EMISSION");
                agentRenderer.sharedMaterial = runtimeMaterial;
            }

            if (agentLight == null)
            {
                agentLight = agentRoot.gameObject.AddComponent<Light>();
                agentLight.type = LightType.Point;
            }

            agentLight.color = emissionColor;
            agentLight.range = lightRange;

            if (audioSource == null)
            {
                audioSource = agentRoot.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 0.2f;
                audioSource.maxDistance = 4f;
            }

            ApplyVisibility(visibleAmount);
        }

        private IEnumerator FadeTo(float target)
        {
            EnsureAgent();

            var start = visibleAmount;
            var duration = Mathf.Max(0.01f, fadeSeconds);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                visibleAmount = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                ApplyVisibility(visibleAmount);
                yield return null;
            }

            visibleAmount = target;
            ApplyVisibility(visibleAmount);
        }

        private void ApplyVisibility(float amount)
        {
            var isVisible = amount > 0.001f;
            if (agentRoot != null)
            {
                agentRoot.gameObject.SetActive(isVisible);
            }

            if (agentRenderer != null)
            {
                agentRenderer.enabled = isVisible;
            }

            if (runtimeMaterial != null)
            {
                var color = coreColor;
                color.a = amount;
                if (runtimeMaterial.HasProperty(BaseColorProperty))
                {
                    runtimeMaterial.SetColor(BaseColorProperty, color);
                }

                if (runtimeMaterial.HasProperty(ColorProperty))
                {
                    runtimeMaterial.SetColor(ColorProperty, color);
                }

                if (runtimeMaterial.HasProperty(EmissionColorProperty))
                {
                    runtimeMaterial.SetColor(EmissionColorProperty, emissionColor * Mathf.Clamp01(amount));
                }
            }

            if (agentLight != null)
            {
                agentLight.enabled = isVisible;
                agentLight.intensity = lightIntensity * Mathf.Clamp01(amount);
            }
        }

        private void OnDestroy()
        {
            DestroyImmediateSafe(runtimeMaterial);
            runtimeMaterial = null;
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
