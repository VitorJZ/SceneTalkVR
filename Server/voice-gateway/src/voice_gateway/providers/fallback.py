from typing import Any

from .base import ProviderError, SpeechProvider, SttResult, TtsResult


class FallbackSpeechProvider:
    def __init__(self, primary: SpeechProvider, fallback: SpeechProvider) -> None:
        self.name = primary.name
        self._primary = primary
        self._fallback = fallback

    def transcribe(self, request: dict[str, Any]) -> SttResult:
        try:
            return self._primary.transcribe(request)
        except ProviderError as exc:
            result = self._fallback.transcribe(request)
            return SttResult(
                request_id=result.request_id,
                provider=result.provider,
                transcript=result.transcript,
                confidence=result.confidence,
                confidence_available=result.confidence_available,
                duration_ms=result.duration_ms,
                latency_ms=result.latency_ms,
                fallback_level=f"mock_after_{self._primary.name}_error:{exc}",
            )

    def synthesize(self, request: dict[str, Any]) -> TtsResult:
        try:
            return self._primary.synthesize(request)
        except ProviderError as exc:
            result = self._fallback.synthesize(request)
            return TtsResult(
                request_id=result.request_id,
                provider=result.provider,
                audio_bytes=result.audio_bytes,
                format=result.format,
                sample_rate=result.sample_rate,
                text_characters=result.text_characters,
                latency_ms=result.latency_ms,
                cache_hit=result.cache_hit,
                fallback_level=f"mock_after_{self._primary.name}_error:{exc}",
            )
