import base64
import time
from typing import Any
from uuid import uuid4

from ..audio.wav_tone import generate_tone_wav
from .base import SttResult, TtsResult


class MockSpeechProvider:
    name = "mock"

    def __init__(self, transcript: str) -> None:
        self._transcript = transcript

    def transcribe(self, request: dict[str, Any]) -> SttResult:
        started = time.perf_counter()
        audio_bytes = self._read_audio_base64(request.get("audioBase64", ""))
        sample_rate = self._read_positive_int(request.get("sampleRate"), 16000)
        channels = self._read_positive_int(request.get("channels"), 1)
        bytes_per_second = sample_rate * channels * 2
        duration_ms = int(len(audio_bytes) / bytes_per_second * 1000) if audio_bytes else 0

        return SttResult(
            request_id=f"stt_{uuid4().hex[:12]}",
            provider=self.name,
            transcript=self._transcript,
            confidence=1.0,
            confidence_available=True,
            duration_ms=duration_ms,
            latency_ms=self._elapsed_ms(started),
            fallback_level="mock_transcript",
        )

    def synthesize(self, request: dict[str, Any]) -> TtsResult:
        started = time.perf_counter()
        text = str(request.get("text") or "")
        output = request.get("output") if isinstance(request.get("output"), dict) else {}
        sample_rate = self._read_positive_int(output.get("sampleRate"), 24000)

        return TtsResult(
            request_id=f"tts_{uuid4().hex[:12]}",
            provider=self.name,
            audio_bytes=generate_tone_wav(text=text, sample_rate=sample_rate),
            format="wav",
            sample_rate=sample_rate,
            text_characters=len(text),
            latency_ms=self._elapsed_ms(started),
            cache_hit=False,
            fallback_level="mock_audio",
        )

    @staticmethod
    def _read_audio_base64(value: Any) -> bytes:
        if not value:
            return b""

        if not isinstance(value, str):
            return b""

        try:
            return base64.b64decode(value, validate=True)
        except Exception:
            return b""

    @staticmethod
    def _read_positive_int(value: Any, fallback: int) -> int:
        try:
            parsed = int(value)
        except (TypeError, ValueError):
            return fallback

        return parsed if parsed > 0 else fallback

    @staticmethod
    def _elapsed_ms(started: float) -> int:
        return max(1, int((time.perf_counter() - started) * 1000))
