using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
            host = new GameObject("LlmReliabilityTests");
            service = host.AddComponent<RealLLMService>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
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

        [TestCase(429L, 1)]
        [TestCase(502L, 0)]
        public async Task TransientDialogueFailure_RetriesOnceAndReturnsSecondResult(
            long statusCode,
            int retryAfterSeconds)
        {
            var attempts = 0;
            var executeMethod = typeof(RealLLMService).GetMethod(
                "ExecuteWithRetry",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(string));
            var purposeType = typeof(RealLLMService).GetNestedType(
                "LlmRequestPurpose",
                BindingFlags.NonPublic)!;
            var dialoguePurpose = Enum.Parse(purposeType, "Dialogue");
            var failure = CreateRequestFailure(statusCode, retryAfterSeconds);
            Func<int, Task<string>> attempt = _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException<string>(failure)
                    : Task.FromResult("second-attempt-success");
            };

            var task = (Task<string>)executeMethod.Invoke(
                service,
                new object[] { attempt, dialoguePurpose, CancellationToken.None })!;
            var result = await task;

            Assert.That(result, Is.EqualTo("second-attempt-success"));
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public async Task TransientDialogueFailure_AfterTwoFailuresThrowsOneTerminalError()
        {
            var attempts = 0;
            Exception terminalError = null;
            var executeMethod = typeof(RealLLMService).GetMethod(
                "ExecuteWithRetry",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(string));
            var purposeType = typeof(RealLLMService).GetNestedType(
                "LlmRequestPurpose",
                BindingFlags.NonPublic)!;
            var dialoguePurpose = Enum.Parse(purposeType, "Dialogue");
            Func<int, Task<string>> attempt = _ =>
            {
                attempts++;
                return Task.FromException<string>(CreateRequestFailure(502, 1));
            };

            var task = (Task<string>)executeMethod.Invoke(
                service,
                new object[] { attempt, dialoguePurpose, CancellationToken.None })!;
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
    }
}
