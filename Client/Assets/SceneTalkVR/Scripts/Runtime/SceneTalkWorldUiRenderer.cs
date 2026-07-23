using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    /// <summary>
    /// Keeps the world-space UI visible when authored scene geometry is in front
    /// of the canvas. World-space positioning and GraphicRaycaster interaction
    /// are unchanged; only the UI materials bypass scene depth testing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class SceneTalkWorldUiRenderer : MonoBehaviour
    {
        private const string ImageShaderName = "SceneTalkVR/UI/Always On Top";
        private const string TextShaderName = "TextMeshPro/Distance Field Overlay";
        private const int DefaultSortingOrder = 1000;

        [SerializeField] private int sortingOrder = DefaultSortingOrder;

        private readonly HashSet<Graphic> appliedGraphics = new HashSet<Graphic>();
        private readonly Dictionary<Material, Material> graphicMaterialInstances = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Material> textMaterialInstances = new Dictionary<Material, Material>();
        private Canvas canvas;
        private bool hierarchyDirty;

        private void Awake()
        {
            Apply();
        }

        private void OnTransformChildrenChanged()
        {
            hierarchyDirty = true;
        }

        private void LateUpdate()
        {
            if (hierarchyDirty)
            {
                hierarchyDirty = false;
                Apply();
            }
        }

        public void Apply()
        {
            canvas = canvas != null ? canvas : GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            var imageShader = Shader.Find(ImageShaderName);
            var textShader = Shader.Find(TextShaderName);
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                {
                    continue;
                }

                if (graphic is TMP_Text text)
                {
                    ApplyTextMaterial(text, textShader);
                }
                else
                {
                    ApplyGraphicMaterial(graphic, imageShader);
                }
            }
        }

        private void ApplyGraphicMaterial(Graphic graphic, Shader shader)
        {
            if (shader == null || appliedGraphics.Contains(graphic))
            {
                return;
            }

            var source = graphic.material;
            if (source == null)
            {
                return;
            }

            if (!graphicMaterialInstances.TryGetValue(source, out var material))
            {
                material = new Material(source)
                {
                    shader = shader,
                    name = $"{source.name} - Always On Top"
                };
                graphicMaterialInstances.Add(source, material);
            }

            graphic.material = material;
            appliedGraphics.Add(graphic);
        }

        private void ApplyTextMaterial(TMP_Text text, Shader shader)
        {
            if (shader == null || appliedGraphics.Contains(text))
            {
                return;
            }

            // Access fontSharedMaterial directly. fontMaterial lazily creates a
            // material and throws for runtime text with no assigned shared one.
            var source = text.fontSharedMaterial;
            if (source == null && text.font != null)
            {
                source = text.font.material;
            }

            if (source == null)
            {
                return;
            }

            if (!textMaterialInstances.TryGetValue(source, out var material))
            {
                material = new Material(source)
                {
                    shader = shader,
                    name = $"{source.name} - Always On Top"
                };
                textMaterialInstances.Add(source, material);
            }

            text.fontSharedMaterial = material;
            appliedGraphics.Add(text);
        }

        private void OnDestroy()
        {
            DestroyMaterials(graphicMaterialInstances.Values);
            DestroyMaterials(textMaterialInstances.Values);
            appliedGraphics.Clear();
            graphicMaterialInstances.Clear();
            textMaterialInstances.Clear();
        }

        private static void DestroyMaterials(IEnumerable<Material> materials)
        {
            foreach (var material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }
}
