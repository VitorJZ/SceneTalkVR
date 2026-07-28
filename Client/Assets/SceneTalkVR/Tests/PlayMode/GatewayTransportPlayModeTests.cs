using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class GatewayTransportPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConnectingTransport_UsesChineseStatusAndBlocksLiveAttempt()
        {
            var routerType = Type.GetType(
                "SceneTalkVR.Core.GatewayTransportRouter, Assembly-CSharp",
                true)!;
            var configurationType = Type.GetType(
                "SceneTalkVR.Core.GatewayTransportConfiguration, Assembly-CSharp",
                true)!;
            var preferenceType = Type.GetType(
                "SceneTalkVR.Core.GatewayTransportPreference, Assembly-CSharp",
                true)!;
            var activeField = routerType.GetField(
                "<Active>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var previousRouter = activeField.GetValue(null);
            var host = new GameObject("GatewayTransportPlayModeTests");
            try
            {
                var router = host.AddComponent(routerType);
                var configuration = Activator.CreateInstance(configurationType)!;
                SetField(configuration, "preference", Enum.Parse(preferenceType, "LanOnly"));
                SetField(configuration, "lanVoiceBaseUrl", "http://127.0.0.1:1");
                SetField(configuration, "lanLlmApiUrl", "http://127.0.0.1:2/api/llm/chat/completions");
                SetField(configuration, "probeTimeoutSeconds", 1);
                SetField(configuration, "requireLiveTransport", true);
                routerType.GetMethod("Configure")!.Invoke(router, new[] { configuration });

                Assert.That(
                    routerType.GetProperty("ChineseStatus")!.GetValue(router),
                    Is.EqualTo("正在连接"));

                var arguments = new object[] { null };
                var canStart = (bool)routerType.GetMethod(
                    "CanStartLiveAttempt",
                    BindingFlags.Static | BindingFlags.Public)!.Invoke(null, arguments)!;
                Assert.That(canStart, Is.False);
                Assert.That(arguments[0] as string, Does.StartWith("gateway_transport_not_ready"));
            }
            finally
            {
                activeField.SetValue(null, previousRouter);
                UnityEngine.Object.Destroy(host);
            }

            yield return null;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name)!.SetValue(target, value);
        }
    }
}
