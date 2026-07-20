using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class SceneTalkAndroidBuildPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 2000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var usesPicoCustomOpenXrLoader = RemoveDuplicateOpenXrLoaderDependency(path);
            if (usesPicoCustomOpenXrLoader)
            {
                EnsurePicoPlatformLibraryDependency(path);
            }

            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[SceneTalkVR] Android manifest not found at {manifestPath}.");
                return;
            }

            var document = XDocument.Load(manifestPath);
            var application = document.Root?.Element("application");
            if (application == null)
            {
                Debug.LogWarning($"[SceneTalkVR] Android manifest has no application element: {manifestPath}.");
                return;
            }

            XNamespace android = "http://schemas.android.com/apk/res/android";
            application.SetAttributeValue(android + "usesCleartextTraffic", "true");
            application.SetAttributeValue(android + "networkSecurityConfig", "@xml/scenetalk_network_security_config");
            if (usesPicoCustomOpenXrLoader)
            {
                ConfigurePicoOpenXrManifest(application, android);
            }

            document.Save(manifestPath);

            var xmlDirectory = Path.Combine(path, "src", "main", "res", "xml");
            Directory.CreateDirectory(xmlDirectory);

            var securityConfigPath = Path.Combine(xmlDirectory, "scenetalk_network_security_config.xml");
            var securityConfig = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(
                    "network-security-config",
                    new XElement("base-config", new XAttribute("cleartextTrafficPermitted", "true"))));

            securityConfig.Save(securityConfigPath);
            Debug.Log($"[SceneTalkVR] Enabled Android cleartext traffic for local gateway debugging: {manifestPath}");
        }

        private static bool RemoveDuplicateOpenXrLoaderDependency(string unityLibraryPath)
        {
            var gradlePath = Path.Combine(unityLibraryPath, "build.gradle");
            var librariesPath = Path.Combine(unityLibraryPath, "libs");
            if (!File.Exists(gradlePath) || !Directory.Exists(librariesPath))
            {
                return false;
            }

            var hasPicoCustomLoader = Directory
                .EnumerateFiles(librariesPath, "LoaderForUnitySDK*.aar", SearchOption.TopDirectoryOnly)
                .Any();
            var hasUnityDefaultLoader = File.Exists(Path.Combine(librariesPath, "openxr_loader.aar"));
            if (!hasPicoCustomLoader || !hasUnityDefaultLoader)
            {
                return hasPicoCustomLoader;
            }

            var originalLines = File.ReadAllLines(gradlePath);
            var filteredLines = originalLines
                .Where(line => !IsDefaultOpenXrLoaderDependency(line))
                .ToArray();
            if (filteredLines.Length == originalLines.Length)
            {
                Debug.LogWarning(
                    $"[SceneTalkVR] Both PICO and Unity OpenXR loader AARs are present, "
                    + $"but the default loader dependency was not found in {gradlePath}.");
                return hasPicoCustomLoader;
            }

            File.WriteAllLines(gradlePath, filteredLines);
            Debug.Log(
                "[SceneTalkVR] Removed Unity's default openxr_loader dependency because "
                + "PICO LoaderForUnitySDK is the active custom OpenXR loader.");
            return hasPicoCustomLoader;
        }

        private static bool IsDefaultOpenXrLoaderDependency(string line)
        {
            return line.Contains("openxr_loader", System.StringComparison.OrdinalIgnoreCase)
                && line.Contains("implementation", System.StringComparison.OrdinalIgnoreCase)
                && line.Contains("aar", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsurePicoPlatformLibraryDependency(string unityLibraryPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var sourcePath = projectRoot == null
                ? string.Empty
                : Path.Combine(
                    projectRoot,
                    "Packages",
                    "com.unity.xr.picoxr",
                    "Runtime",
                    "Android",
                    "PxrPlatform.aar");
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogWarning($"[SceneTalkVR] PICO platform AAR not found: {sourcePath}");
                return;
            }

            var librariesPath = Path.Combine(unityLibraryPath, "libs");
            Directory.CreateDirectory(librariesPath);
            var destinationPath = Path.Combine(librariesPath, "PxrPlatform.aar");
            File.Copy(sourcePath, destinationPath, true);

            var gradlePath = Path.Combine(unityLibraryPath, "build.gradle");
            if (!File.Exists(gradlePath))
            {
                Debug.LogWarning($"[SceneTalkVR] Unity library Gradle file not found: {gradlePath}");
                return;
            }

            var lines = File.ReadAllLines(gradlePath).ToList();
            if (!lines.Any(line => line.Contains("PxrPlatform", System.StringComparison.OrdinalIgnoreCase)))
            {
                var insertIndex = lines.FindIndex(
                    line => line.Contains("LoaderForUnitySDK", System.StringComparison.OrdinalIgnoreCase));
                if (insertIndex < 0)
                {
                    insertIndex = lines.FindIndex(
                        line => line.Trim().Equals("dependencies {", System.StringComparison.Ordinal));
                }

                lines.Insert(
                    insertIndex >= 0 ? insertIndex + 1 : 0,
                    "    implementation(name: 'PxrPlatform', ext:'aar')");
                File.WriteAllLines(gradlePath, lines);
            }

            Debug.Log("[SceneTalkVR] Ensured PICO PxrPlatform Android dependency.");
        }

        private static void ConfigurePicoOpenXrManifest(XElement application, XNamespace android)
        {
            application.SetAttributeValue(android + "requestLegacyExternalStorage", "true");
            SetAndroidMetaData(application, android, "pvr.app.type", "vr");
            SetAndroidMetaData(application, android, "use.pxr.sdk", "2");
            SetAndroidMetaData(application, android, "pxr.sdk.version_code", "5150");
            SetAndroidMetaData(application, android, "pvr.sdk.version", "Unity OpenXR 3.4.0");
            SetAndroidMetaData(application, android, "controller", "1");
            Debug.Log("[SceneTalkVR] Ensured required PICO OpenXR Android manifest metadata.");
        }

        private static void SetAndroidMetaData(
            XElement application,
            XNamespace android,
            string name,
            string value)
        {
            var metaData = application
                .Elements("meta-data")
                .FirstOrDefault(element => (string)element.Attribute(android + "name") == name);
            if (metaData == null)
            {
                metaData = new XElement("meta-data");
                metaData.SetAttributeValue(android + "name", name);
                application.Add(metaData);
            }

            metaData.SetAttributeValue(android + "value", value);
        }
    }
}
