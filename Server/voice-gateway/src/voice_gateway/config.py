import os
from dataclasses import dataclass


@dataclass(frozen=True)
class GatewayConfig:
    host: str = "127.0.0.1"
    port: int = 8787
    provider: str = "mock"
    mock_transcript: str = (
        "I want to practice ordering coffee with a fast-speaking foreign barista."
    )
    tencent_secret_id: str = ""
    tencent_secret_key: str = ""
    tencent_region: str = "ap-guangzhou"
    tencent_asr_endpoint: str = "asr.tencentcloudapi.com"
    tencent_tts_endpoint: str = "tts.tencentcloudapi.com"
    tencent_asr_engine: str = "16k_en"
    tencent_tts_voice_type: int = 1051
    tencent_fallback_to_mock: bool = True

    @classmethod
    def from_env(cls) -> "GatewayConfig":
        return cls(
            host=os.getenv("VOICE_GATEWAY_HOST", cls.host),
            port=int(os.getenv("VOICE_GATEWAY_PORT", str(cls.port))),
            provider=os.getenv("VOICE_GATEWAY_PROVIDER", cls.provider).strip().lower(),
            mock_transcript=os.getenv(
                "VOICE_GATEWAY_MOCK_TRANSCRIPT", cls.mock_transcript
            ),
            tencent_secret_id=os.getenv("TENCENT_SECRET_ID", cls.tencent_secret_id),
            tencent_secret_key=os.getenv("TENCENT_SECRET_KEY", cls.tencent_secret_key),
            tencent_region=os.getenv("TENCENT_REGION", cls.tencent_region),
            tencent_asr_endpoint=os.getenv(
                "TENCENT_ASR_ENDPOINT", cls.tencent_asr_endpoint
            ),
            tencent_tts_endpoint=os.getenv(
                "TENCENT_TTS_ENDPOINT", cls.tencent_tts_endpoint
            ),
            tencent_asr_engine=os.getenv("TENCENT_ASR_ENGINE", cls.tencent_asr_engine),
            tencent_tts_voice_type=int(
                os.getenv("TENCENT_TTS_VOICE_TYPE", str(cls.tencent_tts_voice_type))
            ),
            tencent_fallback_to_mock=os.getenv(
                "TENCENT_FALLBACK_TO_MOCK",
                "true" if cls.tencent_fallback_to_mock else "false",
            ).strip().lower()
            not in {"0", "false", "no", "off"},
        )
