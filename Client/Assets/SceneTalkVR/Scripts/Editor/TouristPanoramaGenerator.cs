#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime.Services;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class TouristPanoramaGenerator
    {
        private const string Output = "Assets/Resources/SceneTalkVR/Textures/tourist-information-360.png";
        [MenuItem("SceneTalkVR/Experiment/Generate Tourist Panorama (Developer Only)")]
        public static async void Generate()
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<ExperimentConditionManager>();
            if (manager != null && manager.IsFormalExperiment) { Debug.LogError("[Experiment] Tourist panorama generation is Developer Mode only."); return; }
            DotEnvLoader.LoadEnv();
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SILICONFLOW_API_KEY"))) { Debug.LogError("[Experiment] SILICONFLOW_API_KEY is unavailable."); return; }
            var service = UnityEngine.Object.FindFirstObjectByType<PanoramaSceneService>();
            if (service == null) { Debug.LogError("[Experiment] PanoramaSceneService is missing."); return; }
            try
            {
                service.ConfigureFormalModeLock(false);
                service.ConfigureRuntime(false, "Tongyi-MAI/Z-Image", "2048x1024");
                var texture = await service.GenerateSkyboxAsync("tourist information center interior, visitor maps and city brochures, staffed information desk, daylight, empty room, equirectangular 360 degree panorama");
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                File.WriteAllBytes(Output, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Experiment] Generated and saved fixed Tourist panorama: " + Output);
            }
            catch (Exception ex) { Debug.LogError("[Experiment] Tourist panorama generation failed: " + ex.Message); }
        }
    }
}
#endif
