import subprocess
import unittest
from unittest.mock import patch

from src.llm_gateway.api import server as gateway_server
from src.llm_gateway.config import GatewayConfig


class _FakeResponse:
    def __init__(self, body: bytes, content_type: str, retry_after: str = "") -> None:
        self.status = 200
        self._body = body
        self.headers = {
            "Content-Type": content_type,
            "Retry-After": retry_after,
        }

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, traceback):
        return False

    def read(self) -> bytes:
        return self._body


class LlmGatewayForwardingTests(unittest.TestCase):
    def _handler(self, **overrides):
        config = GatewayConfig(
            api_key="test-key",
            **overrides,
        )
        handler = object.__new__(gateway_server.LlmGatewayHandler)
        handler.gateway_state = gateway_server.LlmGatewayState(config)
        return handler

    def test_urllib_forwards_event_stream_accept_and_retry_after(self):
        handler = self._handler(transport="urllib")
        response = _FakeResponse(
            b'data: {"choices":[]}\n\n',
            "text/event-stream",
            "5",
        )

        with patch.object(
            gateway_server.urllib_request,
            "urlopen",
            return_value=response,
        ) as urlopen:
            status, body, content_type, retry_after = (
                handler._forward_to_upstream_with_urllib(
                    b'{"messages":[{"role":"user","content":"hi"}]}',
                    "text/event-stream",
                )
            )

        request = urlopen.call_args.args[0]
        self.assertEqual(request.get_header("Accept"), "text/event-stream")
        self.assertEqual(int(status), 200)
        self.assertEqual(body, response._body)
        self.assertEqual(content_type, "text/event-stream")
        self.assertEqual(retry_after, "5")

    def test_curl_forwards_event_stream_accept_and_response_metadata(self):
        handler = self._handler(transport="curl", curl_ssl_no_revoke=False)
        completed = subprocess.CompletedProcess(
            args=[],
            returncode=0,
            stdout=(
                b'data: [DONE]\n\nSCENETALK_HTTP_STATUS:200\n'
                b'SCENETALK_CONTENT_TYPE:text/event-stream; charset=utf-8\n'
                b'SCENETALK_RETRY_AFTER:5'
            ),
            stderr=b"",
        )

        with patch.object(
            gateway_server.subprocess,
            "run",
            return_value=completed,
        ) as run:
            status, _, content_type, retry_after = handler._forward_to_upstream_with_curl(
                b'{"messages":[{"role":"user","content":"hi"}]}',
                "text/event-stream",
            )

        command = run.call_args.args[0]
        write_out = command[command.index("-w") + 1]
        self.assertIn("Accept: text/event-stream", command)
        self.assertIn("%{content_type}", write_out)
        self.assertIn("%header{retry-after}", write_out)
        self.assertEqual(int(status), 200)
        self.assertEqual(content_type, "text/event-stream; charset=utf-8")
        self.assertEqual(retry_after, "5")

    def test_default_upstream_timeout_is_shorter_than_unity_first_attempt(self):
        self.assertEqual(GatewayConfig().timeout_seconds, 28)


if __name__ == "__main__":
    unittest.main()
