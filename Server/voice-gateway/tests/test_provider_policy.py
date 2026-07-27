import unittest

from voice_gateway.api.server import build_provider
from voice_gateway.config import GatewayConfig
from voice_gateway.providers import FallbackSpeechProvider, MockSpeechProvider, TencentSpeechProvider


class ProviderPolicyTests(unittest.TestCase):
    def test_tencent_default_does_not_fall_back_to_mock(self) -> None:
        config = GatewayConfig(provider="tencent")

        provider = build_provider(config)

        self.assertIsInstance(provider, TencentSpeechProvider)
        self.assertNotIsInstance(provider, FallbackSpeechProvider)

    def test_mock_requires_explicit_mock_provider(self) -> None:
        provider = build_provider(GatewayConfig(provider="mock"))

        self.assertIsInstance(provider, MockSpeechProvider)

    def test_legacy_fallback_requires_explicit_opt_in(self) -> None:
        provider = build_provider(
            GatewayConfig(provider="tencent", tencent_fallback_to_mock=True)
        )

        self.assertIsInstance(provider, FallbackSpeechProvider)


if __name__ == "__main__":
    unittest.main()
