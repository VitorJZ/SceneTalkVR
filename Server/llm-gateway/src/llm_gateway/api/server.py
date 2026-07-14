import json
import subprocess
import time
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib import request as urllib_request
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse

from ..config import GatewayConfig


class LlmGatewayState:
    def __init__(self, config: GatewayConfig) -> None:
        self.config = config


def run_server(config: GatewayConfig) -> None:
    state = LlmGatewayState(config)

    class RequestHandler(LlmGatewayHandler):
        gateway_state = state

    server = ThreadingHTTPServer((config.host, config.port), RequestHandler)
    print(
        f"SceneTalkVR LLM gateway listening on http://{config.host}:{config.port} "
        f"(upstream={config.upstream_url})"
    )
    server.serve_forever()


class LlmGatewayHandler(BaseHTTPRequestHandler):
    gateway_state: LlmGatewayState

    def do_GET(self) -> None:
        route = urlparse(self.path).path

        if route == "/health":
            self._send_json(
                {
                    "status": "ok",
                    "upstreamUrl": self.gateway_state.config.upstream_url,
                    "hasApiKey": bool(self.gateway_state.config.api_key),
                }
            )
            return

        self._send_json(
            self._error("not_found", f"Unknown route: {route}"),
            status=HTTPStatus.NOT_FOUND,
        )

    def do_POST(self) -> None:
        route = urlparse(self.path).path

        if route != "/api/llm/chat/completions":
            self._send_json(
                self._error("not_found", f"Unknown route: {route}"),
                status=HTTPStatus.NOT_FOUND,
            )
            return

        body = self._read_raw_json_body()
        if body is None:
            return

        if not self.gateway_state.config.api_key:
            self._send_json(
                self._error(
                    "missing_api_key",
                    "LLM gateway API key is not configured. Set LLM_GATEWAY_API_KEY or OPENAI_API_KEY.",
                ),
                status=HTTPStatus.INTERNAL_SERVER_ERROR,
            )
            return

        started = time.perf_counter()
        try:
            status, response_body, content_type = self._forward_to_upstream(body)
        except ProviderError as exc:
            self._send_json(
                self._error("provider_error", str(exc)),
                status=HTTPStatus.BAD_GATEWAY,
            )
            return

        elapsed_ms = round((time.perf_counter() - started) * 1000)
        print(
            "[llm-gateway] "
            f"upstream status={int(status)} elapsedMs={elapsed_ms} bytes={len(response_body)}"
        )
        self._send_bytes(response_body, content_type=content_type, status=status)

    def log_message(self, format: str, *args: Any) -> None:
        print(f"[llm-gateway] {self.address_string()} {format % args}")

    def _read_raw_json_body(self) -> bytes | None:
        content_type = self.headers.get("Content-Type", "")
        if "application/json" not in content_type:
            self._send_json(
                self._error("unsupported_content_type", "Use application/json."),
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
            parsed = json.loads(raw_body.decode("utf-8")) if raw_body else {}
        except json.JSONDecodeError as exc:
            self._send_json(
                self._error("invalid_json", f"Invalid JSON body: {exc.msg}."),
                status=HTTPStatus.BAD_REQUEST,
            )
            return None

        if not isinstance(parsed, dict):
            self._send_json(
                self._error("invalid_json", "JSON body must be an object."),
                status=HTTPStatus.BAD_REQUEST,
            )
            return None

        if not parsed.get("messages"):
            self._send_json(
                self._error("invalid_request", "Chat completion body must include messages."),
                status=HTTPStatus.BAD_REQUEST,
            )
            return None

        return raw_body

    def _forward_to_upstream(self, body: bytes) -> tuple[HTTPStatus, bytes, str]:
        config = self.gateway_state.config
        transport = (config.transport or "auto").strip().lower()
        if transport == "curl":
            return self._forward_to_upstream_with_curl(body)

        try:
            return self._forward_to_upstream_with_urllib(body)
        except ProviderError:
            if transport != "auto":
                raise

        return self._forward_to_upstream_with_curl(body)

    def _forward_to_upstream_with_urllib(
        self, body: bytes
    ) -> tuple[HTTPStatus, bytes, str]:
        config = self.gateway_state.config
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": f"Bearer {config.api_key}",
        }
        request = urllib_request.Request(
            config.upstream_url,
            data=body,
            headers=headers,
            method="POST",
        )

        try:
            with urllib_request.urlopen(request, timeout=config.timeout_seconds) as response:
                content_type = response.headers.get("Content-Type", "application/json")
                return HTTPStatus(response.status), response.read(), content_type
        except HTTPError as exc:
            content_type = exc.headers.get("Content-Type", "application/json")
            return HTTPStatus(exc.code), exc.read(), content_type
        except URLError as exc:
            raise ProviderError(f"LLM upstream network error: {exc.reason}") from exc
        except TimeoutError as exc:
            raise ProviderError("LLM upstream request timed out.") from exc

    def _forward_to_upstream_with_curl(
        self, body: bytes
    ) -> tuple[HTTPStatus, bytes, str]:
        config = self.gateway_state.config
        command = [
            config.curl_path or "curl",
            "-sS",
            "-m",
            str(max(1, config.timeout_seconds)),
            "-H",
            "Content-Type: application/json",
            "-H",
            "Accept: application/json",
            "-H",
            f"Authorization: Bearer {config.api_key}",
            "--data-binary",
            "@-",
            "-w",
            "\nSCENETALK_HTTP_STATUS:%{http_code}",
        ]
        if config.curl_ssl_no_revoke:
            command.insert(1, "--ssl-no-revoke")

        command.append(config.upstream_url)
        try:
            completed = subprocess.run(
                command,
                input=body,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=max(2, config.timeout_seconds + 5),
                check=False,
            )
        except FileNotFoundError as exc:
            raise ProviderError(f"curl executable not found: {config.curl_path}") from exc
        except subprocess.TimeoutExpired as exc:
            raise ProviderError("curl upstream request timed out.") from exc

        stdout = completed.stdout
        marker = b"\nSCENETALK_HTTP_STATUS:"
        if marker not in stdout:
            detail = completed.stderr.decode("utf-8", errors="replace").strip()
            raise ProviderError(
                f"curl upstream request failed with exit code {completed.returncode}: {detail}"
            )

        response_body, raw_status = stdout.rsplit(marker, 1)
        try:
            status_code = int(raw_status.strip()[:3])
        except ValueError as exc:
            raise ProviderError("curl upstream response did not include HTTP status.") from exc

        if completed.returncode != 0 and status_code == 0:
            detail = completed.stderr.decode("utf-8", errors="replace").strip()
            raise ProviderError(
                f"curl upstream request failed with exit code {completed.returncode}: {detail}"
            )

        return HTTPStatus(status_code), response_body, "application/json"

    @staticmethod
    def _error(error_code: str, message: str) -> dict[str, Any]:
        return {
            "errorCode": error_code,
            "message": message,
            "retryable": error_code
            not in {"invalid_json", "invalid_request", "unsupported_content_type"},
        }

    def _send_json(
        self, payload: dict[str, Any], status: HTTPStatus = HTTPStatus.OK
    ) -> None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self._send_bytes(data, content_type="application/json; charset=utf-8", status=status)

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


class ProviderError(Exception):
    pass
