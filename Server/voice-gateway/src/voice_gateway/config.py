import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any


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
    tencent_transport: str = "auto"
    tencent_curl_path: str = "curl.exe"
    tencent_curl_ssl_no_revoke: bool = True

    @classmethod
    def from_env(cls) -> "GatewayConfig":
        config = cls.from_local_file()
        return config.with_overrides(
            {
                "host": os.getenv("VOICE_GATEWAY_HOST"),
                "port": os.getenv("VOICE_GATEWAY_PORT"),
                "provider": os.getenv("VOICE_GATEWAY_PROVIDER"),
                "mock_transcript": os.getenv("VOICE_GATEWAY_MOCK_TRANSCRIPT"),
                "tencent_secret_id": os.getenv("TENCENT_SECRET_ID"),
                "tencent_secret_key": os.getenv("TENCENT_SECRET_KEY"),
                "tencent_region": os.getenv("TENCENT_REGION"),
                "tencent_asr_endpoint": os.getenv("TENCENT_ASR_ENDPOINT"),
                "tencent_tts_endpoint": os.getenv("TENCENT_TTS_ENDPOINT"),
                "tencent_asr_engine": os.getenv("TENCENT_ASR_ENGINE"),
                "tencent_tts_voice_type": os.getenv("TENCENT_TTS_VOICE_TYPE"),
                "tencent_fallback_to_mock": os.getenv("TENCENT_FALLBACK_TO_MOCK"),
                "tencent_transport": os.getenv("TENCENT_TRANSPORT"),
                "tencent_curl_path": os.getenv("TENCENT_CURL_PATH"),
                "tencent_curl_ssl_no_revoke": os.getenv(
                    "TENCENT_CURL_SSL_NO_REVOKE"
                ),
            }
        )

    @classmethod
    def from_local_file(cls) -> "GatewayConfig":
        config_path = _find_local_config_path()
        if config_path is None:
            return cls()

        with config_path.open("r", encoding="utf-8") as handle:
            raw_config = json.load(handle)

        if not isinstance(raw_config, dict):
            raise ValueError(f"Gateway config must be a JSON object: {config_path}")

        return cls().with_overrides(raw_config)

    def with_overrides(self, values: dict[str, Any]) -> "GatewayConfig":
        clean_values = {
            key: _coerce_value(key, value)
            for key, value in values.items()
            if value is not None and key in self.__dataclass_fields__
        }

        if "provider" in clean_values:
            clean_values["provider"] = str(clean_values["provider"]).strip().lower()
        if "tencent_transport" in clean_values:
            clean_values["tencent_transport"] = (
                str(clean_values["tencent_transport"]).strip().lower()
            )

        return GatewayConfig(**{**self.__dict__, **clean_values})


def _find_local_config_path() -> Path | None:
    explicit_path = os.getenv("VOICE_GATEWAY_CONFIG")
    if explicit_path:
        path = Path(explicit_path).expanduser()
        return path if path.exists() else None

    gateway_root = Path(__file__).resolve().parents[2]
    candidates = (
        Path.cwd() / "voice-gateway.local.json",
        gateway_root / "voice-gateway.local.json",
    )

    for path in candidates:
        if path.exists():
            return path

    return None


def _coerce_value(key: str, value: Any) -> Any:
    if key in {"port", "tencent_tts_voice_type"}:
        return int(value)

    if key in {"tencent_fallback_to_mock", "tencent_curl_ssl_no_revoke"}:
        if isinstance(value, bool):
            return value
        return str(value).strip().lower() not in {"0", "false", "no", "off"}

    return value
