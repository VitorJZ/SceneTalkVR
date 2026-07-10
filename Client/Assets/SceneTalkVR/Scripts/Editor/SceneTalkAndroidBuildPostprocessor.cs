using System.IO;
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
    }
}
