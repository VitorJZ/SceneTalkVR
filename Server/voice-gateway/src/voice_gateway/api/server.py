import json
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib.parse import urlparse

from ..config import GatewayConfig
from ..providers import (
    FallbackSpeechProvider,
    MockSpeechProvider,
    ProviderError,
    SpeechProvider,
    TencentSpeechProvider,
)


class VoiceGatewayState:
    def __init__(self, config: GatewayConfig) -> None:
        self.config = config
        self.provider = build_provider(config)
        self.audio_cache: dict[str, bytes] = {}


def build_provider(config: GatewayConfig) -> SpeechProvider:
    mock_provider = MockSpeechProvider(config.mock_transcript)
    if config.provider == "mock":
        return mock_provider

    if config.provider == "tencent":
        tencent_provider = TencentSpeechProvider(config)
        if config.tencent_fallback_to_mock:
            return FallbackSpeechProvider(tencent_provider, mock_provider)

        return tencent_provider

    raise ValueError(
        f"Unsupported VOICE_GATEWAY_PROVIDER='{config.provider}'. "
        "Use 'mock' or 'tencent'."
    )


def run_server(config: GatewayConfig) -> None:
    state = VoiceGatewayState(config)

    class RequestHandler(VoiceGatewayHandler):
        gateway_state = state

    server = ThreadingHTTPServer((config.host, config.port), RequestHandler)
    print(
        f"SceneTalkVR voice gateway listening on http://{config.host}:{config.port} "
        f"(provider={state.provider.name})"
    )
    server.serve_forever()


class VoiceGatewayHandler(BaseHTTPRequestHandler):
    gateway_state: VoiceGatewayState

    def do_GET(self) -> None:
        route = urlparse(self.path).path

        if route == "/health":
            self._send_json(
                {
                    "status": "ok",
                    "provider": self.gateway_state.provider.name,
                }
            )
            return

        if route.startswith("/api/voice/audio/") and route.endswith(".wav"):
            request_id = route.removeprefix("/api/voice/audio/").removesuffix(".wav")
            audio = self.gateway_state.audio_cache.get(request_id)
            if audio is None:
                self._send_json(
                    self._error("audio_not_found", f"No audio found for {request_id}."),
                    status=HTTPStatus.NOT_FOUND,
                )
                return

            self._send_bytes(audio, content_type="audio/wav")
            return

        self._send_json(
            self._error("not_found", f"Unknown route: {route}"),
            status=HTTPStatus.NOT_FOUND,
        )

    def do_POST(self) -> None:
        route = urlparse(self.path).path

        if route == "/api/voice/stt":
            request = self._read_json_body()
            if request is None:
                return

            try:
                result = self.gateway_state.provider.transcribe(request)
            except ProviderError as exc:
                self._send_json(
                    self._error("provider_error", str(exc)),
                    status=HTTPStatus.BAD_GATEWAY,
                )
                return

            self._send_json(
                {
                    "requestId": result.request_id,
                    "provider": result.provider,
                    "isFinal": True,
                    "transcript": result.transcript,
                    "confidence": result.confidence,
                    "confidenceAvailable": result.confidence_available,
                    "durationMs": result.duration_ms,
                    "latencyMs": result.latency_ms,
                    "fallbackLevel": result.fallback_level,
                }
            )
            return

        if route == "/api/voice/tts":
            request = self._read_json_body()
            if request is None:
                return

            text = str(request.get("text") or "")
            if not text.strip():
                self._send_json(
                    self._error("empty_text", "TTS text cannot be empty."),
                    status=HTTPStatus.BAD_REQUEST,
                )
                return

            try:
                result = self.gateway_state.provider.synthesize(request)
            except ProviderError as exc:
                self._send_json(
                    self._error("provider_error", str(exc)),
                    status=HTTPStatus.BAD_GATEWAY,
                )
                return

            self.gateway_state.audio_cache[result.request_id] = result.audio_bytes
            self._send_json(
                {
                    "requestId": result.request_id,
                    "provider": result.provider,
                    "audioUrl": f"/api/voice/audio/{result.request_id}.wav",
                    "format": result.format,
                    "sampleRate": result.sample_rate,
                    "textCharacters": result.text_characters,
                    "latencyMs": result.latency_ms,
                    "cacheHit": result.cache_hit,
                    "fallbackLevel": result.fallback_level,
                }
            )
            return

        self._send_json(
            self._error("not_found", f"Unknown route: {route}"),
            status=HTTPStatus.NOT_FOUND,
        )

    def log_message(self, format: str, *args: Any) -> None:
        print(f"[voice-gateway] {self.address_string()} {format % args}")

    def _read_json_body(self) -> dict[str, Any] | None:
        content_type = self.headers.get("Content-Type", "")
        if "application/json" not in content_type:
            self._send_json(
                self._error(
                    "unsupported_content_type",
                    "Use application/json for this P0 gateway scaffold.",
                ),
                status=HTTPStatus.UNSUPPORTED_MEDIA_TYPE,
            )
            return None

        raw_length = self.headers.get("Content-Length", "0")
        try:
            length = int(raw_length)
        except ValueError:
            length = 0

        raw_body = self.rfile.read(length)
        try:
            body = json.loads(raw_body.decode("utf-8")) if raw_body else {}
        except json.JSONDecodeError as exc:
            self._send_json(
                self._error("invalid_json", f"Invalid JSON body: {exc.msg}."),
                status=HTTPStatus.BAD_REQUEST,
            )
            return None

        if not isinstance(body, dict):
            self._send_json(
                self._error("invalid_json", "JSON body must be an object."),
                status=HTTPStatus.BAD_REQUEST,
            )
            return None

        return body

    @staticmethod
    def _error(error_code: str, message: str) -> dict[str, Any]:
        return {
            "errorCode": error_code,
            "message": message,
            "retryable": error_code not in {"empty_text", "invalid_json"},
        }

    def _send_json(
        self, payload: dict[str, Any], status: HTTPStatus = HTTPStatus.OK
    ) -> None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _send_bytes(
        self,
        data: bytes,
        *,
        content_type: str,
        status: HTTPStatus = HTTPStatus.OK,
    ) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)
