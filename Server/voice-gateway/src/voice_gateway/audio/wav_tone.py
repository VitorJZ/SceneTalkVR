import math
import struct
import wave
from io import BytesIO


def generate_tone_wav(
    *,
    text: str,
    sample_rate: int = 24000,
    frequency_hz: float = 440.0,
    max_seconds: float = 2.4,
) -> bytes:
    """Generate a tiny WAV placeholder for mock TTS responses."""
    seconds = min(max(0.45, len(text) / 45.0), max_seconds)
    frame_count = int(sample_rate * seconds)
    amplitude = 0.18

    buffer = BytesIO()
    with wave.open(buffer, "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)

        for frame_index in range(frame_count):
            t = frame_index / sample_rate
            fade = min(1.0, frame_index / max(1, sample_rate * 0.04))
            fade *= min(1.0, (frame_count - frame_index) / max(1, sample_rate * 0.08))
            sample = amplitude * fade * math.sin(2.0 * math.pi * frequency_hz * t)
            wav_file.writeframes(struct.pack("<h", int(sample * 32767)))

    return buffer.getvalue()

