import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class GatewayConfig:
    host: str = "127.0.0.1"
    port: int = 8788
    upstream_url: str = "https://models.sjtu.edu.cn/api/v1/chat/completions"
    api_key: str = ""
    timeout_seconds: int = 60
    transport: str = "auto"
    curl_path: str = "curl.exe"
    curl_ssl_no_revoke: bool = True

    @classmethod
    def from_env(cls) -> "GatewayConfig":
        _load_dotenv_if_present()
        config = cls.from_local_file()
        return config.with_overrides(
            {
                "host": os.getenv("LLM_GATEWAY_HOST"),
                "port": os.getenv("LLM_GATEWAY_PORT"),
                "upstream_url": os.getenv("LLM_GATEWAY_UPSTREAM_URL"),
                "api_key": os.getenv("LLM_GATEWAY_API_KEY")
                or os.getenv("OPENAI_API_KEY"),
                "timeout_seconds": os.getenv("LLM_GATEWAY_TIMEOUT_SECONDS"),
                "transport": os.getenv("LLM_GATEWAY_TRANSPORT"),
                "curl_path": os.getenv("LLM_GATEWAY_CURL_PATH"),
                "curl_ssl_no_revoke": os.getenv("LLM_GATEWAY_CURL_SSL_NO_REVOKE"),
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
        return GatewayConfig(**{**self.__dict__, **clean_values})


def _find_local_config_path() -> Path | None:
    explicit_path = os.getenv("LLM_GATEWAY_CONFIG")
    if explicit_path:
        path = Path(explicit_path).expanduser()
        return path if path.exists() else None

    gateway_root = Path(__file__).resolve().parents[2]
    candidates = (
        Path.cwd() / "llm-gateway.local.json",
        gateway_root / "llm-gateway.local.json",
    )

    for path in candidates:
        if path.exists():
            return path

    return None


def _load_dotenv_if_present() -> None:
    gateway_root = Path(__file__).resolve().parents[2]
    repo_root = gateway_root.parents[1]
    candidates = (
        Path.cwd() / ".env",
        gateway_root / ".env",
        repo_root / ".env",
    )

    for path in candidates:
        if not path.exists():
            continue

        for raw_line in path.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue

            key, value = line.split("=", 1)
            key = key.strip()
            value = value.strip()
            if (
                (value.startswith('"') and value.endswith('"'))
                or (value.startswith("'") and value.endswith("'"))
            ):
                value = value[1:-1]

            os.environ.setdefault(key, value)
        return


def _coerce_value(key: str, value: Any) -> Any:
    if key in {"port", "timeout_seconds"}:
        return int(value)

    if key == "curl_ssl_no_revoke":
        if isinstance(value, bool):
            return value
        return str(value).strip().lower() not in {"0", "false", "no", "off"}

    return value
