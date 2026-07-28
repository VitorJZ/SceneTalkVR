using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class LlmReliabilityTests
    {
        private GameObject host;
        private RealLLMService service;

        [SetUp]
        public void SetUp()
        {
            if (IsPureRetryTest())
            {
                return;
            }

            host = new GameObject("LlmReliabilityTests");
            service = host.AddComponent<RealLLMService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            host = null;
            service = null;
        }

        [Test]
        public void StreamingHandler_PreservesSplitUtf8AndFlushesFinalLine()
        {
            var chunks = new List<string>();
            var handler = CreateStreamingHandler(chunks.Add);
            const string content = "{\"dialogueReply\":\"你好.\"}";
            var eventJson = "{\"choices\":[{\"delta\":{\"content\":\""
                + EscapeJson(content)
                + "\"}}]}";
            var bytes = Encoding.UTF8.GetBytes("data: " + eventJson);
            var split = Array.IndexOf(bytes, (byte)0xE4) + 1;

            Feed(handler, bytes, 0, split);
            Feed(handler, bytes, split, bytes.Length - split);
            Complete(handler);

            Assert.That(chunks, Is.EqualTo(new[] { content }));
            Assert.That(GetProperty<int>(handler, "ParsedEventCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(handler, "ParseFailureCount"), Is.Zero);
        }

        [Test]
        public void NonStreamingHandler_PreservesUtf8SplitAcrossTransportChunks()
        {
            var firstBytesCount = 0;
            var handlerType = typeof(RealLLMService).GetNestedType(
                "FirstResponseBytesDownloadHandler",
                BindingFlags.NonPublic);
            var handler = Activator.CreateInstance(
                handlerType!,
                (Action)(() => firstBytesCount++))!;
            const string response = "{\"choices\":[{\"message\":{\"content\":\"你好。\"}}]}";
            var bytes = Encoding.UTF8.GetBytes(response);
            var split = Array.IndexOf(bytes, (byte)0xE4) + 1;

            Feed(handler, bytes, 0, split);
            Feed(handler, bytes, split, bytes.Length - split);
            Complete(handler);

            Assert.That(GetProperty<string>(handler, "Text"), Is.EqualTo(response));
            Assert.That(firstBytesCount, Is.EqualTo(1));
        }

        [Test]
        public void StreamingHandler_ReportsMalformedSseInsteadOfSwallowingIt()
        {
            var handler = CreateStreamingHandler(_ => { });
            var bytes = Encoding.UTF8.GetBytes("data: {not-json}\n");

            Feed(handler, bytes, 0, bytes.Length);
            Complete(handler);

            Assert.That(GetProperty<int>(handler, "ParseFailureCount"), Is.EqualTo(1));
            Assert.That(GetProperty<string>(handler, "LastParseFailure"), Is.Not.Empty);
            Assert.That(GetProperty<bool>(handler, "HasReceivedBytes"), Is.True);
        }

        [Test]
        public void RouteSwitch_IsAllowedOnlyBeforeAnyLlmResponseBytes()
        {
            service.ConfigureTransportRouter(new FakeRouteProvider());
            var exceptionType = typeof(RealLLMService).GetNestedType(
                "LlmRequestException",
                BindingFlags.NonPublic)!;
            var constructor = exceptionType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(string), typeof(long), typeof(bool), typeof(int),
                    typeof(bool), typeof(bool)
                },
                null)!;
            var zeroBytes = constructor.Invoke(new object[] { "transport", 0L, true, 0, true, false });
            var partialResponse = constructor.Invoke(new object[] { "transport", 0L, false, 0, true, true });
            var method = typeof(RealLLMService).GetMethod(
                "CanSwitchRoute",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.That((bool)method.Invoke(service, new[] { zeroBytes })!, Is.True);
            Assert.That((bool)method.Invoke(service, new[] { partialResponse })!, Is.False);
        }

        [Test]
        public void NonStreamingEnvelopeFallback_ExtractsDialogueJson()
        {
            const string expected = "{\"dialogueReply\":\"Hello.\"}";
            var envelope = "{\"choices\":[{\"message\":{\"content\":\""
                + EscapeJson(expected)
                + "\"}}]}";
            var method = typeof(RealLLMService).GetMethod(
                "TryExtractNonStreamingContent",
                BindingFlags.Static | BindingFlags.NonPublic);

            var actual = (string)method!.Invoke(null, new object[] { envelope });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void EmptyDialoguePayload_IsRejected()
        {
            var method = typeof(RealLLMService).GetMethod(
                "TryParseDialoguePayload",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var exception = Assert.Throws<TargetInvocationException>(
                () => method!.Invoke(service, new object[] { string.Empty }));

            Assert.That(exception!.InnerException, Is.TypeOf<FormatException>());
        }

        [Test]
        public void FormalDialogueGuard_AllowsOrdinaryRoleplayNegation()
        {
            const string reply = "You're welcome to take photos inside the museum, but flash photography is not allowed to protect the exhibits.";
            service.SetExperimentCondition(new CorrectionExperimentCondition
            {
                formalExperiment = true,
                provider = "assistant_agent",
                style = "explicit",
                scenarioId = "tourist_assistance",
                task = new SceneTalkExperimentTask
                {
                    taskId = "tourist_assistance",
                    fallbackEnvironmentType = "museum"
                }
            });
            var payload = new SpringScenePayload
            {
                dialogueReply = reply,
                correctionFeedback = new CorrectionFeedbackData { hasFeedback = false }
            };
            var apply = typeof(RealLLMService).GetMethod(
                "ApplyExperimentConditionToPayload",
                BindingFlags.Instance | BindingFlags.NonPublic);

            apply!.Invoke(service, new object[] { payload });

            Assert.That(GetField<bool>(service, "formalDialogueLeakageDetected"), Is.False);
            Assert.That(payload.dialogueReply, Is.EqualTo(reply));
        }

        [TestCase(429L, 1)]
        [TestCase(502L, 0)]
        public async Task TransientDialogueFailure_RetriesOnceAndReturnsSecondResult(
            long statusCode,
            int retryAfterSeconds)
        {
            var attempts = 0;
            var requestedDelays = new List<int>();
            var retryWarnings = new List<string>();
            var failure = CreateRequestFailure(statusCode, retryAfterSeconds);
            Func<int, Task<string>> attempt = _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException<string>(failure)
                    : Task.FromResult("second-attempt-success");
            };

            var task = ExecuteDialogueRetryCore(
                attempt,
                retryWarnings.Add,
                (delayMilliseconds, _) =>
                {
                    requestedDelays.Add(delayMilliseconds);
                    return Task.CompletedTask;
                });
            var result = await task;

            Assert.That(result, Is.EqualTo("second-attempt-success"));
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(retryWarnings, Has.Count.EqualTo(1));
            Assert.That(retryWarnings[0], Does.Contain($"HTTP {statusCode}"));
            Assert.That(requestedDelays, Has.Count.EqualTo(1));
            Assert.That(requestedDelays[0], Is.GreaterThan(0));
        }

        [Test]
        public async Task TransientDialogueFailure_AfterTwoFailuresThrowsOneTerminalError()
        {
            var attempts = 0;
            Exception terminalError = null;
            var retryWarnings = new List<string>();
            var failure = CreateRequestFailure(502, 1);
            Func<int, Task<string>> attempt = _ =>
            {
                attempts++;
                return Task.FromException<string>(failure);
            };

            var task = ExecuteDialogueRetryCore(
                attempt,
                retryWarnings.Add,
                (_, _) => Task.CompletedTask);
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                terminalError = exception;
            }

            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(terminalError, Is.Not.Null);
            Assert.That(terminalError!.Message, Is.EqualTo("HTTP 502"));
            Assert.That(retryWarnings, Has.Count.EqualTo(1));
        }

        private static bool IsPureRetryTest()
        {
            return TestContext.CurrentContext.Test.MethodName.StartsWith(
                "TransientDialogueFailure_",
                StringComparison.Ordinal);
        }

        private static Task<string> ExecuteDialogueRetryCore(
            Func<int, Task<string>> attempt,
            Action<string> onRetry,
            Func<int, CancellationToken, Task> delayAsync)
        {
            var executeMethod = typeof(RealLLMService).GetMethod(
                "ExecuteWithRetryCore",
                BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(string));
            var purposeType = typeof(RealLLMService).GetNestedType(
                "LlmRequestPurpose",
                BindingFlags.NonPublic)!;
            var dialoguePurpose = Enum.Parse(purposeType, "Dialogue");

            return (Task<string>)executeMethod.Invoke(
                null,
                new object[]
                {
                    attempt,
                    dialoguePurpose,
                    CancellationToken.None,
                    1,
                    45,
                    30,
                    onRetry,
                    delayAsync
                })!;
        }

        private static object CreateStreamingHandler(Action<string> callback)
        {
            var type = typeof(RealLLMService).GetNestedType(
                "StreamingDownloadHandler",
                BindingFlags.NonPublic);
            return Activator.CreateInstance(type!, callback)!;
        }

        private static void Feed(object handler, byte[] bytes, int offset, int count)
        {
            var data = new byte[count];
            Buffer.BlockCopy(bytes, offset, data, 0, count);
            var method = handler.GetType().GetMethod(
                "ReceiveData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)method!.Invoke(handler, new object[] { data, data.Length })!, Is.True);
        }

        private static void Complete(object handler)
        {
            var method = handler.GetType().GetMethod(
                "CompleteContent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(handler, Array.Empty<object>());
        }

        private static T GetProperty<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name)!.GetValue(target)!;
        }

        private static T GetField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
        }

        private static Exception CreateRequestFailure(long statusCode, int retryAfterSeconds)
        {
            var exceptionType = typeof(RealLLMService).GetNestedType(
                "LlmRequestException",
                BindingFlags.NonPublic)!;
            return (Exception)Activator.CreateInstance(
                exceptionType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    $"HTTP {statusCode}",
                    statusCode,
                    true,
                    retryAfterSeconds
                },
                null)!;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class FakeRouteProvider : IGatewayTransportRouteProvider
        {
            public GatewayTransportPreference Preference => GatewayTransportPreference.UsbPreferred;
            public GatewayTransportState State => GatewayTransportState.UsbReady;
            public GatewayRouteSnapshot CurrentRoute => new GatewayRouteSnapshot
            {
                transport = GatewayTransportKind.Usb,
                voiceBaseUrl = "http://127.0.0.1:8787",
                llmApiUrl = "http://127.0.0.1:8788/api/llm/chat/completions"
            };
            public bool IsReady => true;
            public bool RequiresLiveTransport => true;

            public Task<GatewayRouteSnapshot> AcquireRouteAsync(
                bool refreshUsb,
                GatewayRequestStage stage,
                CancellationToken cancellationToken = default)
                => Task.FromResult(CurrentRoute);

            public Task<GatewayRouteSnapshot> RecoverFromTransportFailureAsync(
                GatewayRouteSnapshot failedRoute,
                GatewayRequestStage stage,
                string failureReason,
                CancellationToken cancellationToken = default)
                => Task.FromResult(CurrentRoute);

            public void RequestBoundaryProbe(GatewayRequestStage stage)
            {
            }
        }
    }
}
