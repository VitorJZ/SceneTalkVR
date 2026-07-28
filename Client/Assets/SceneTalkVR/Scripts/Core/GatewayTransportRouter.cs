using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.Core
{
    public enum GatewayTransportPreference
    {
        LanOnly = 0,
        UsbPreferred = 1,
        UsbOnly = 2
    }

    public enum GatewayTransportState
    {
        Uninitialized = 0,
        ProbingUsb = 1,
        UsbReady = 2,
        ProbingLan = 3,
        LanReady = 4,
        Unavailable = 5
    }

    public enum GatewayTransportKind
    {
        None = 0,
        Usb = 1,
        Lan = 2
    }

    public enum GatewayRequestStage
    {
        Startup = 0,
        SessionBoundary = 1,
        TurnBoundary = 2,
        Stt = 3,
        Llm = 4,
        LlmStream = 5,
        TtsRequest = 6,
        TtsAudioDownload = 7,
        Retry = 8
    }

    [Serializable]
    public sealed class GatewayRouteSnapshot
    {
        public GatewayTransportKind transport;
        public string voiceBaseUrl;
        public string llmApiUrl;
        public string selectedAtUtc;

        public bool IsValid => transport != GatewayTransportKind.None
            && !string.IsNullOrWhiteSpace(voiceBaseUrl)
            && !string.IsNullOrWhiteSpace(llmApiUrl);

        public GatewayRouteSnapshot Clone()
        {
            return new GatewayRouteSnapshot
            {
                transport = transport,
                voiceBaseUrl = voiceBaseUrl,
                llmApiUrl = llmApiUrl,
                selectedAtUtc = selectedAtUtc
            };
        }
    }

    [Serializable]
    public sealed class GatewayTransportConfiguration
    {
        public GatewayTransportPreference preference = GatewayTransportPreference.LanOnly;
        public string usbVoiceBaseUrl = "http://127.0.0.1:8787";
        public string usbLlmApiUrl = "http://127.0.0.1:8788/api/llm/chat/completions";
        public string lanVoiceBaseUrl = string.Empty;
        public string lanLlmApiUrl = string.Empty;
        public int probeTimeoutSeconds = 3;
        public bool requireLiveTransport = true;

        public GatewayTransportConfiguration Normalized()
        {
            return new GatewayTransportConfiguration
            {
                preference = preference,
                usbVoiceBaseUrl = SceneTalkRuntimeConfig.NormalizeUrl(usbVoiceBaseUrl),
                usbLlmApiUrl = SceneTalkRuntimeConfig.NormalizeUrl(usbLlmApiUrl),
                lanVoiceBaseUrl = SceneTalkRuntimeConfig.NormalizeUrl(lanVoiceBaseUrl),
                lanLlmApiUrl = SceneTalkRuntimeConfig.NormalizeUrl(lanLlmApiUrl),
                probeTimeoutSeconds = Mathf.Clamp(probeTimeoutSeconds, 1, 10),
                requireLiveTransport = requireLiveTransport
            };
        }
    }

    [Serializable]
    public sealed class GatewayTransportAuditRecord
    {
        public string schemaVersion = "1.0";
        public string timestampUtc;
        public string eventType;
        public string transport;
        public string requestStage;
        public string failureReason;
    }

    [Serializable]
    internal sealed class GatewayHealthResponse
    {
        public string status;
        public string provider;
        public string upstreamUrl;
    }

    public sealed class GatewayTransportUnavailableException : Exception
    {
        public GatewayTransportUnavailableException(string message) : base(message)
        {
        }
    }

    public interface IGatewayTransportRouteProvider
    {
        GatewayTransportPreference Preference { get; }
        GatewayTransportState State { get; }
        GatewayRouteSnapshot CurrentRoute { get; }
        bool IsReady { get; }
        bool RequiresLiveTransport { get; }
        Task<GatewayRouteSnapshot> AcquireRouteAsync(
            bool refreshUsb,
            GatewayRequestStage stage,
            CancellationToken cancellationToken = default);
        Task<GatewayRouteSnapshot> RecoverFromTransportFailureAsync(
            GatewayRouteSnapshot failedRoute,
            GatewayRequestStage stage,
            string failureReason,
            CancellationToken cancellationToken = default);
        void RequestBoundaryProbe(GatewayRequestStage stage);
    }

    public sealed class GatewayTransportStateMachine
    {
        public GatewayTransportPreference Preference { get; private set; } = GatewayTransportPreference.LanOnly;
        public GatewayTransportState State { get; private set; } = GatewayTransportState.Uninitialized;
        public GatewayRouteSnapshot CurrentRoute { get; private set; }

        public void Configure(GatewayTransportPreference preference)
        {
            Preference = preference;
            State = GatewayTransportState.Uninitialized;
            CurrentRoute = null;
        }

        public IReadOnlyList<GatewayTransportKind> PreferredProbeOrder(bool refreshUsb)
        {
            if (!refreshUsb && CurrentRoute?.IsValid == true)
            {
                return Array.Empty<GatewayTransportKind>();
            }

            return Preference switch
            {
                GatewayTransportPreference.UsbOnly => new[] { GatewayTransportKind.Usb },
                GatewayTransportPreference.UsbPreferred => new[]
                {
                    GatewayTransportKind.Usb,
                    GatewayTransportKind.Lan
                },
                _ => new[] { GatewayTransportKind.Lan }
            };
        }

        public IReadOnlyList<GatewayTransportKind> RecoveryOrder(GatewayTransportKind failedTransport)
        {
            if (Preference != GatewayTransportPreference.UsbPreferred)
            {
                return Array.Empty<GatewayTransportKind>();
            }

            return failedTransport == GatewayTransportKind.Usb
                ? new[] { GatewayTransportKind.Lan }
                : failedTransport == GatewayTransportKind.Lan
                    ? new[] { GatewayTransportKind.Usb }
                    : Array.Empty<GatewayTransportKind>();
        }

        public void BeginProbe(GatewayTransportKind transport)
        {
            State = transport == GatewayTransportKind.Usb
                ? GatewayTransportState.ProbingUsb
                : GatewayTransportState.ProbingLan;
        }

        public void MarkReady(GatewayRouteSnapshot route)
        {
            if (route?.IsValid != true)
            {
                throw new ArgumentException("A complete Voice/LLM route is required.", nameof(route));
            }

            CurrentRoute = route.Clone();
            State = route.transport == GatewayTransportKind.Usb
                ? GatewayTransportState.UsbReady
                : GatewayTransportState.LanReady;
        }

        public void MarkUnavailable()
        {
            CurrentRoute = null;
            State = GatewayTransportState.Unavailable;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GatewayTransportRouter : MonoBehaviour, IGatewayTransportRouteProvider
    {
        private readonly SemaphoreSlim routeGate = new SemaphoreSlim(1, 1);
        private readonly List<GatewayTransportAuditRecord> auditTrail = new List<GatewayTransportAuditRecord>();
        private readonly GatewayTransportStateMachine stateMachine = new GatewayTransportStateMachine();
        private GatewayTransportConfiguration configuration;
        private Coroutine boundaryProbe;
        private string lastAuthorizedAttemptRouteTimestamp = string.Empty;
        private CancellationTokenSource configurationCancellation = new CancellationTokenSource();

        public static GatewayTransportRouter Active { get; private set; }
        public event Action<GatewayTransportState> StateChanged;
        public event Action<GatewayTransportAuditRecord> AuditRecorded;

        public GatewayTransportPreference Preference => stateMachine.Preference;
        public GatewayTransportState State => stateMachine.State;
        public GatewayRouteSnapshot CurrentRoute => stateMachine.CurrentRoute?.Clone();
        public bool IsReady => State == GatewayTransportState.UsbReady
            || State == GatewayTransportState.LanReady;
        public bool RequiresLiveTransport => configuration?.requireLiveTransport == true;
        public IReadOnlyList<GatewayTransportAuditRecord> AuditTrail => auditTrail;
        public string ChineseStatus => ResolveChineseStatus(State);

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            configurationCancellation.Cancel();
            configurationCancellation.Dispose();
        }

        public void Configure(GatewayTransportConfiguration value)
        {
            configurationCancellation.Cancel();
            configurationCancellation.Dispose();
            configurationCancellation = new CancellationTokenSource();
            if (boundaryProbe != null)
            {
                StopCoroutine(boundaryProbe);
                boundaryProbe = null;
            }

            configuration = (value ?? new GatewayTransportConfiguration()).Normalized();
            ValidateConfiguration(configuration);
            stateMachine.Configure(configuration.preference);
            lastAuthorizedAttemptRouteTimestamp = string.Empty;
            NotifyStateChanged();
            RequestBoundaryProbe(GatewayRequestStage.Startup);
        }

        public void RequestBoundaryProbe(GatewayRequestStage stage)
        {
            if (!isActiveAndEnabled || configuration == null || boundaryProbe != null)
            {
                return;
            }

            boundaryProbe = StartCoroutine(ProbeBoundary(stage));
        }

        public async Task<GatewayRouteSnapshot> AcquireRouteAsync(
            bool refreshUsb,
            GatewayRequestStage stage,
            CancellationToken cancellationToken = default)
        {
            if (configuration == null)
            {
                throw new GatewayTransportUnavailableException("gateway_transport_not_configured");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                configurationCancellation.Token);
            var routeCancellation = linkedCancellation.Token;
            await routeGate.WaitAsync(routeCancellation);
            try
            {
                if (!refreshUsb && stateMachine.CurrentRoute?.IsValid == true)
                {
                    return stateMachine.CurrentRoute.Clone();
                }

                var failure = string.Empty;
                foreach (var transport in stateMachine.PreferredProbeOrder(refreshUsb))
                {
                    var route = BuildRoute(transport);
                    SetProbing(transport);
                    var result = await ProbeRouteAsync(route, routeCancellation);
                    if (result.success)
                    {
                        SelectRoute(route, "route_selected", stage, failure);
                        return route.Clone();
                    }

                    failure = result.failureReason;
                    RecordAudit("probe_failed", transport, stage, failure);
                }

                SetUnavailable(stage, failure);
                throw new GatewayTransportUnavailableException(
                    string.IsNullOrWhiteSpace(failure)
                        ? "gateway_transport_unavailable"
                        : $"gateway_transport_unavailable:{failure}");
            }
            finally
            {
                routeGate.Release();
            }
        }

        public async Task<GatewayRouteSnapshot> RecoverFromTransportFailureAsync(
            GatewayRouteSnapshot failedRoute,
            GatewayRequestStage stage,
            string failureReason,
            CancellationToken cancellationToken = default)
        {
            if (configuration == null || failedRoute?.IsValid != true)
            {
                throw new GatewayTransportUnavailableException("gateway_transport_recovery_not_configured");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                configurationCancellation.Token);
            var routeCancellation = linkedCancellation.Token;
            await routeGate.WaitAsync(routeCancellation);
            try
            {
                if (stateMachine.CurrentRoute?.IsValid == true
                    && stateMachine.CurrentRoute.transport != failedRoute.transport)
                {
                    return stateMachine.CurrentRoute.Clone();
                }

                var boundedFailure = SanitizeFailure(failureReason);
                foreach (var transport in stateMachine.RecoveryOrder(failedRoute.transport))
                {
                    var route = BuildRoute(transport);
                    SetProbing(transport);
                    var probe = await ProbeRouteAsync(route, routeCancellation);
                    if (probe.success)
                    {
                        SelectRoute(route, "route_switched", stage, boundedFailure);
                        return route.Clone();
                    }

                    boundedFailure = probe.failureReason;
                    RecordAudit("fallback_probe_failed", transport, stage, boundedFailure);
                }

                SetUnavailable(stage, boundedFailure);
                throw new GatewayTransportUnavailableException(
                    "gateway_transport_fallback_unavailable:" + boundedFailure);
            }
            finally
            {
                routeGate.Release();
            }
        }

        public static bool CanStartLiveAttempt(out string error)
        {
            var router = Active;
            if (router == null || !router.RequiresLiveTransport)
            {
                error = string.Empty;
                return true;
            }

            if (router.IsReady)
            {
                var route = router.CurrentRoute;
                if (router.Preference == GatewayTransportPreference.UsbPreferred
                    && route?.IsValid == true
                    && string.Equals(
                        router.lastAuthorizedAttemptRouteTimestamp,
                        route.selectedAtUtc,
                        StringComparison.Ordinal))
                {
                    router.RequestBoundaryProbe(GatewayRequestStage.SessionBoundary);
                    error = "gateway_transport_reprobe_in_progress";
                    return false;
                }

                router.lastAuthorizedAttemptRouteTimestamp = route?.selectedAtUtc ?? string.Empty;
                error = string.Empty;
                return true;
            }

            router.RequestBoundaryProbe(GatewayRequestStage.SessionBoundary);
            error = "gateway_transport_not_ready:" + router.State;
            return false;
        }

        public static string ResolveChineseStatus(GatewayTransportState state)
        {
            return state switch
            {
                GatewayTransportState.UsbReady => "USB 数据线",
                GatewayTransportState.LanReady => "局域网备用",
                GatewayTransportState.Unavailable => "不可用",
                _ => "正在连接"
            };
        }

        public static string BuildHealthUrl(string serviceUrl)
        {
            if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }

            var builder = new UriBuilder(uri)
            {
                Path = "/health",
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        private IEnumerator ProbeBoundary(GatewayRequestStage stage)
        {
            var refreshUsb = configuration.preference == GatewayTransportPreference.UsbPreferred;
            var task = AcquireRouteAsync(refreshUsb, stage);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            boundaryProbe = null;
            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException().Message ?? "gateway_transport_unavailable";
                Debug.LogWarning("[GatewayTransport] " + SanitizeFailure(error), this);
            }
        }

        private GatewayRouteSnapshot BuildRoute(GatewayTransportKind transport)
        {
            var route = new GatewayRouteSnapshot
            {
                transport = transport,
                voiceBaseUrl = transport == GatewayTransportKind.Usb
                    ? configuration.usbVoiceBaseUrl
                    : configuration.lanVoiceBaseUrl,
                llmApiUrl = transport == GatewayTransportKind.Usb
                    ? configuration.usbLlmApiUrl
                    : configuration.lanLlmApiUrl,
                selectedAtUtc = DateTime.UtcNow.ToString("o")
            };
            return route;
        }

        private async Task<(bool success, string failureReason)> ProbeRouteAsync(
            GatewayRouteSnapshot route,
            CancellationToken cancellationToken)
        {
            if (route?.IsValid != true)
            {
                return (false, "route_endpoints_incomplete");
            }

            var voice = await ProbeHealthAsync(
                BuildHealthUrl(route.voiceBaseUrl),
                "voice",
                cancellationToken);
            if (!voice.success)
            {
                return voice;
            }

            return await ProbeHealthAsync(
                BuildHealthUrl(route.llmApiUrl),
                "llm",
                cancellationToken);
        }

        private async Task<(bool success, string failureReason)> ProbeHealthAsync(
            string url,
            string service,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (false, service + "_health_url_invalid");
            }

            using var request = UnityWebRequest.Get(url);
            request.timeout = configuration.probeTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (Exception error)
            {
                return (false, service + "_probe_exception:" + SanitizeFailure(error.Message));
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                var code = request.responseCode > 0 ? request.responseCode.ToString() : "transport";
                return (false, service + "_health_failed:" + code + ":" + SanitizeFailure(request.error));
            }

            var body = request.downloadHandler?.text ?? string.Empty;
            GatewayHealthResponse health;
            try
            {
                health = JsonUtility.FromJson<GatewayHealthResponse>(body);
            }
            catch (ArgumentException)
            {
                health = null;
            }

            var compatible = health != null
                && string.Equals(health.status, "ok", StringComparison.OrdinalIgnoreCase)
                && (service == "voice"
                    ? !string.IsNullOrWhiteSpace(health.provider)
                    : !string.IsNullOrWhiteSpace(health.upstreamUrl));
            if (!compatible)
            {
                return (false, service + "_health_incompatible");
            }

            return (true, string.Empty);
        }

        private void SetProbing(GatewayTransportKind transport)
        {
            stateMachine.BeginProbe(transport);
            NotifyStateChanged();
        }

        private void SelectRoute(
            GatewayRouteSnapshot route,
            string eventType,
            GatewayRequestStage stage,
            string failureReason)
        {
            route.selectedAtUtc = DateTime.UtcNow.ToString("o");
            stateMachine.MarkReady(route);
            NotifyStateChanged();
            RecordAudit(eventType, route.transport, stage, failureReason);
        }

        private void SetUnavailable(GatewayRequestStage stage, string failureReason)
        {
            stateMachine.MarkUnavailable();
            NotifyStateChanged();
            RecordAudit("route_unavailable", GatewayTransportKind.None, stage, failureReason);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke(State);
        }

        private void RecordAudit(
            string eventType,
            GatewayTransportKind transport,
            GatewayRequestStage stage,
            string failureReason)
        {
            var record = new GatewayTransportAuditRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                eventType = eventType ?? string.Empty,
                transport = transport.ToString(),
                requestStage = stage.ToString(),
                failureReason = SanitizeFailure(failureReason)
            };
            auditTrail.Add(record);
            if (auditTrail.Count > 200)
            {
                auditTrail.RemoveAt(0);
            }

            AuditRecorded?.Invoke(record);
            var json = JsonUtility.ToJson(record);
            Debug.Log("[GatewayTransportAudit] " + json, this);
            try
            {
                var folder = Path.Combine(
                    Application.persistentDataPath,
                    "SceneTalkVR",
                    "TransportAudit");
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, "transport-events.jsonl"),
                    json + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    "[GatewayTransport] Audit persistence failed: " + SanitizeFailure(error.Message),
                    this);
            }
        }

        private static void ValidateConfiguration(GatewayTransportConfiguration value)
        {
            if (value.preference != GatewayTransportPreference.LanOnly
                && (!SceneTalkRuntimeConfig.IsLoopbackUrl(value.usbVoiceBaseUrl)
                    || !SceneTalkRuntimeConfig.IsLoopbackUrl(value.usbLlmApiUrl)))
            {
                throw new ArgumentException(
                    "USB ADB transport requires explicit loopback Voice and LLM endpoints.",
                    nameof(value));
            }
        }

        private static string SanitizeFailure(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            normalized = StripQueryValues(normalized);
            const int maximumLength = 256;
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength);
        }

        private static string StripQueryValues(string value)
        {
            var question = value.IndexOf('?');
            if (question < 0)
            {
                return value;
            }

            var end = value.IndexOf(' ', question);
            return end < 0
                ? value.Substring(0, question) + "?<redacted>"
                : value.Substring(0, question) + "?<redacted>" + value.Substring(end);
        }
    }
}
