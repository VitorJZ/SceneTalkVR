from dataclasses import dataclass
from typing import Any, Protocol


@dataclass(frozen=True)
class SttResult:
    request_id: str
    provider: str
    transcript: str
    confidence: float
    confidence_available: bool
    duration_ms: int
    latency_ms: int
    fallback_level: str = "none"


@dataclass(frozen=True)
class TtsResult:
    request_id: str
    provider: str
    audio_bytes: bytes
    format: str
    sample_rate: int
    text_characters: int
    latency_ms: int
    cache_hit: bool = False
    fallback_level: str = "none"


class SpeechProvider(Protocol):
    name: str

    def transcribe(self, request: dict[str, Any]) -> SttResult:
        ...

    def synthesize(self, request: dict[str, Any]) -> TtsResult:
        ...


class ProviderError(RuntimeError):
    pass
