import base64
import hashlib
import hmac
import json
import time
from datetime import datetime, timezone
from typing import Any
from urllib import request as urllib_request
from urllib.error import HTTPError, URLError
from uuid import uuid4

from ..config import GatewayConfig
from .base import ProviderError, SttResult, TtsResult


class TencentCloudApiClient:
    algorithm = "TC3-HMAC-SHA256"

    def __init__(self, secret_id: str, secret_key: str, region: str) -> None:
        self._secret_id = secret_id
        self._secret_key = secret_key
        self._region = region

    def post_json(
        self,
        *,
        endpoint: str,
        service: str,
        action: str,
        version: str,
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        if not self._secret_id or not self._secret_key:
            raise ProviderError("Tencent credentials are not configured.")

        timestamp = int(time.time())
        body = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        headers = self._build_headers(
            endpoint=endpoint,
            service=service,
            action=action,
            version=version,
            timestamp=timestamp,
            body=body,
        )

        request = urllib_request.Request(
            f"https://{endpoint}",
            data=body.encode("utf-8"),
            headers=headers,
            method="POST",
        )

        try:
            with urllib_request.urlopen(request, timeout=20) as response:
                response_body = response.read().decode("utf-8")
        except HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise ProviderError(f"Tencent API HTTP {exc.code}: {detail}") from exc
        except URLError as exc:
            raise ProviderError(f"Tencent API network error: {exc.reason}") from exc

        try:
            parsed = json.loads(response_body)
        except json.JSONDecodeError as exc:
            raise ProviderError("Tencent API returned invalid JSON.") from exc

        response_payload = parsed.get("Response")
        if not isinstance(response_payload, dict):
            raise ProviderError("Tencent API response missing Response object.")

        error = response_payload.get("Error")
        if isinstance(error, dict):
            code = error.get("Code", "Unknown")
            message = error.get("Message", "")
            raise ProviderError(f"{code}: {message}")

        return response_payload

    def _build_headers(
        self,
        *,
        endpoint: str,
        service: str,
        action: str,
        version: str,
        timestamp: int,
        body: str,
    ) -> dict[str, str]:
        date = datetime.fromtimestamp(timestamp, tz=timezone.utc).strftime("%Y-%m-%d")
        content_type = "application/json; charset=utf-8"
        canonical_headers = f"content-type:{content_type}\nhost:{endpoint}\n"
        signed_headers = "content-type;host"
        hashed_payload = hashlib.sha256(body.encode("utf-8")).hexdigest()
        canonical_request = "\n".join(
            [
                "POST",
                "/",
                "",
                canonical_headers,
                signed_headers,
                hashed_payload,
            ]
        )

        credential_scope = f"{date}/{service}/tc3_request"
        string_to_sign = "\n".join(
            [
                self.algorithm,
                str(timestamp),
                credential_scope,
                hashlib.sha256(canonical_request.encode("utf-8")).hexdigest(),
            ]
        )
        signature = self._sign_string(date, service, string_to_sign)
        authorization = (
            f"{self.algorithm} "
            f"Credential={self._secret_id}/{credential_scope}, "
            f"SignedHeaders={signed_headers}, "
            f"Signature={signature}"
        )

        return {
            "Authorization": authorization,
            "Content-Type": content_type,
            "Host": endpoint,
            "X-TC-Action": action,
            "X-TC-Version": version,
            "X-TC-Timestamp": str(timestamp),
            "X-TC-Region": self._region,
        }

    def _sign_string(self, date: str, service: str, string_to_sign: str) -> str:
        secret_date = _hmac_sha256(("TC3" + self._secret_key).encode("utf-8"), date)
        secret_service = _hmac_sha256(secret_date, service)
        secret_signing = _hmac_sha256(secret_service, "tc3_request")
        return hmac.new(
            secret_signing,
            string_to_sign.encode("utf-8"),
            hashlib.sha256,
        ).hexdigest()


class TencentSpeechProvider:
    name = "tencent"

    def __init__(self, config: GatewayConfig) -> None:
        self._config = config
        self._client = TencentCloudApiClient(
            config.tencent_secret_id,
            config.tencent_secret_key,
            config.tencent_region,
        )

    def transcribe(self, request: dict[str, Any]) -> SttResult:
        started = time.perf_counter()
        audio_base64 = str(request.get("audioBase64") or "")
        if not audio_base64:
            raise ProviderError("STT audioBase64 is empty.")

        audio_bytes = _decode_base64(audio_base64, "STT audioBase64")
        if len(audio_base64.encode("utf-8")) > 3 * 1024 * 1024:
            raise ProviderError("STT audioBase64 exceeds Tencent 3MB request limit.")

        voice_format = _normalize_audio_format(str(request.get("format") or "wav"))
        payload = {
            "ProjectId": 0,
            "SubServiceType": 2,
            "EngSerViceType": self._config.tencent_asr_engine,
            "SourceType": 1,
            "VoiceFormat": voice_format,
            "Data": audio_base64,
            "DataLen": len(audio_bytes),
        }

        response = self._client.post_json(
            endpoint=self._config.tencent_asr_endpoint,
            service="asr",
            action="SentenceRecognition",
            version="2019-06-14",
            payload=payload,
        )
        transcript = str(response.get("Result") or "").strip()
        if not transcript:
            raise ProviderError("Tencent ASR response did not include Result.")

        return SttResult(
            request_id=str(response.get("RequestId") or f"stt_{uuid4().hex[:12]}"),
            provider=self.name,
            transcript=transcript,
            confidence=1.0,
            duration_ms=_read_int(response.get("AudioDuration"), 0),
            latency_ms=_elapsed_ms(started),
            fallback_level="none",
        )

    def synthesize(self, request: dict[str, Any]) -> TtsResult:
        started = time.perf_counter()
        text = str(request.get("text") or "").strip()
        if not text:
            raise ProviderError("TTS text is empty.")

        voice_profile = request.get("voiceProfile")
        if not isinstance(voice_profile, dict):
            voice_profile = {}

        output = request.get("output")
        if not isinstance(output, dict):
            output = {}

        codec = str(output.get("format") or "wav").upper()
        if codec not in {"WAV", "MP3", "PCM"}:
            codec = "WAV"

        sample_rate = _choose_tencent_sample_rate(output.get("sampleRate"))
        session_id = str(request.get("turnId") or request.get("sessionId") or uuid4())
        voice_type = _choose_voice_type(
            voice_profile.get("voiceId"),
            self._config.tencent_tts_voice_type,
        )

        payload = {
            "Text": text,
            "SessionId": session_id,
            "Volume": 0,
            "Speed": _map_tts_speed(voice_profile.get("speakingSpeed")),
            "ProjectId": 0,
            "ModelType": 1,
            "VoiceType": voice_type,
            "PrimaryLanguage": _map_primary_language(str(request.get("language") or "")),
            "SampleRate": sample_rate,
            "Codec": codec.lower(),
            "EnableSubtitle": False,
        }

        fallback_level = "none"
        try:
            response = self._request_tts(payload)
        except ProviderError as exc:
            fallback_voice_type = self._config.tencent_tts_voice_type
            if not _should_retry_with_fallback_voice(exc, voice_type, fallback_voice_type):
                raise

            payload["VoiceType"] = fallback_voice_type
            response = self._request_tts(payload)
            fallback_level = (
                f"voice_type_fallback:{voice_type}->{fallback_voice_type}:pkg_exhausted"
            )

        audio_base64 = str(response.get("Audio") or "")
        if not audio_base64:
            raise ProviderError("Tencent TTS response did not include Audio.")

        audio_bytes = _decode_base64(audio_base64, "Tencent TTS Audio")
        return TtsResult(
            request_id=str(response.get("RequestId") or f"tts_{uuid4().hex[:12]}"),
            provider=self.name,
            audio_bytes=audio_bytes,
            format=codec.lower(),
            sample_rate=sample_rate,
            text_characters=len(text),
            latency_ms=_elapsed_ms(started),
            cache_hit=False,
            fallback_level=fallback_level,
        )

    def _request_tts(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._client.post_json(
            endpoint=self._config.tencent_tts_endpoint,
            service="tts",
            action="TextToVoice",
            version="2019-08-23",
            payload=payload,
        )


def _hmac_sha256(key: bytes, message: str) -> bytes:
    return hmac.new(key, message.encode("utf-8"), hashlib.sha256).digest()


def _decode_base64(value: str, label: str) -> bytes:
    try:
        return base64.b64decode(value, validate=True)
    except Exception as exc:
        raise ProviderError(f"{label} is not valid base64.") from exc


def _normalize_audio_format(value: str) -> str:
    normalized = value.strip().lower()
    return normalized if normalized in {"wav", "pcm", "mp3", "m4a", "aac", "ogg-opus"} else "wav"


def _choose_tencent_sample_rate(value: Any) -> int:
    parsed = _read_int(value, 16000)
    return 8000 if parsed == 8000 else 16000


def _choose_voice_type(value: Any, fallback: int) -> int:
    aliases = {
        "default_female_en": 1051,
        "default_male_en": 1050,
        "female_en": 1051,
        "male_en": 1050,
        "we_rose": 1051,
        "we_jack": 1050,
    }
    if isinstance(value, str):
        key = value.strip().lower()
        if key in aliases:
            return aliases[key]
        try:
            return int(key)
        except ValueError:
            return fallback

    return _read_int(value, fallback)


def _should_retry_with_fallback_voice(
    error: ProviderError,
    requested_voice_type: int,
    fallback_voice_type: int,
) -> bool:
    return (
        requested_voice_type != fallback_voice_type
        and "UnsupportedOperation.PkgExhausted" in str(error)
    )


def _map_tts_speed(value: Any) -> float:
    if isinstance(value, str):
        normalized = value.strip().lower()
        if normalized in {"slow", "slower"}:
            return -1
        if normalized in {"fast", "faster"}:
            return 1
        if normalized in {"very_fast", "very-fast"}:
            return 2
        try:
            return float(normalized)
        except ValueError:
            return 0

    try:
        return float(value)
    except (TypeError, ValueError):
        return 0


def _map_primary_language(language: str) -> int:
    return 2 if language.strip().lower().startswith("en") else 1


def _read_int(value: Any, fallback: int) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def _elapsed_ms(started: float) -> int:
    return max(1, int((time.perf_counter() - started) * 1000))
