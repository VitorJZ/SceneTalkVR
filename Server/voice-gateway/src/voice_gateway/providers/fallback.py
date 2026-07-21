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
        except Exception as primary_error:
            try:
                result = self._fallback.transcribe(request)
            except Exception as fallback_error:
                raise ProviderError(
                    _combined_failure_message(
                        self._primary.name,
                        primary_error,
                        self._fallback.name,
                        fallback_error,
                    )
                ) from fallback_error

            return SttResult(
                request_id=result.request_id,
                provider=result.provider,
                transcript=result.transcript,
                confidence=result.confidence,
                confidence_available=result.confidence_available,
                duration_ms=result.duration_ms,
                latency_ms=result.latency_ms,
                fallback_level=(
                    f"mock_after_{self._primary.name}_error:"
                    f"{_error_detail(primary_error)}"
                ),
            )

    def synthesize(self, request: dict[str, Any]) -> TtsResult:
        try:
            return self._primary.synthesize(request)
        except Exception as primary_error:
            try:
                result = self._fallback.synthesize(request)
            except Exception as fallback_error:
                raise ProviderError(
                    _combined_failure_message(
                        self._primary.name,
                        primary_error,
                        self._fallback.name,
                        fallback_error,
                    )
                ) from fallback_error

            return TtsResult(
                request_id=result.request_id,
                provider=result.provider,
                audio_bytes=result.audio_bytes,
                format=result.format,
                sample_rate=result.sample_rate,
                text_characters=result.text_characters,
                latency_ms=result.latency_ms,
                cache_hit=result.cache_hit,
                fallback_level=(
                    f"mock_after_{self._primary.name}_error:"
                    f"{_error_detail(primary_error)}"
                ),
            )


def _error_detail(error: Exception) -> str:
    message = str(error).strip()
    error_name = type(error).__name__
    return f"{error_name}:{message}" if message else error_name


def _combined_failure_message(
    primary_name: str,
    primary_error: Exception,
    fallback_name: str,
    fallback_error: Exception,
) -> str:
    return (
        f"{primary_name} provider failed ({_error_detail(primary_error)}); "
        f"{fallback_name} fallback also failed ({_error_detail(fallback_error)})."
    )
