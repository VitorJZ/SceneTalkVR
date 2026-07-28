import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "scenetalk_gateway_launcher.py"
SPEC = importlib.util.spec_from_file_location("scenetalk_gateway_launcher", MODULE_PATH)
launcher = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = launcher
SPEC.loader.exec_module(launcher)


class LauncherPolicyTests(unittest.TestCase):
    def test_locate_adb_prefers_explicit_path(self):
        with tempfile.TemporaryDirectory() as folder:
            executable = Path(folder) / ("adb.exe" if os.name == "nt" else "adb")
            executable.touch()
            result = launcher.locate_adb(
                str(executable), environ={}, which=lambda _: None, unity_candidates=[]
            )
            self.assertEqual(executable.resolve(), result)

    def test_locate_adb_uses_android_sdk(self):
        with tempfile.TemporaryDirectory() as folder:
            executable = Path(folder) / "platform-tools" / ("adb.exe" if os.name == "nt" else "adb")
            executable.parent.mkdir()
            executable.touch()
            result = launcher.locate_adb(
                environ={"ANDROID_SDK_ROOT": folder}, which=lambda _: None, unity_candidates=[]
            )
            self.assertEqual(executable.resolve(), result)

    def test_device_selection_requires_authorized_unique_device(self):
        devices = [launcher.AdbDevice("pico", "device")]
        self.assertEqual("pico", launcher.choose_device(devices).serial)
        with self.assertRaisesRegex(launcher.LauncherError, "Multiple"):
            launcher.choose_device(devices + [launcher.AdbDevice("phone", "device")])
        with self.assertRaisesRegex(launcher.LauncherError, "authorize"):
            launcher.choose_device([launcher.AdbDevice("pico", "unauthorized")], "pico")

    def test_reverse_parser_detects_conflicting_mapping(self):
        mappings = launcher.parse_reverse_list("UsbFfs tcp:8787 tcp:9999\nUsbFfs tcp:8788 tcp:8788\n")
        with self.assertRaisesRegex(launcher.LauncherError, "conflicting"):
            launcher.validate_reverse_mapping(mappings, 8787)
        self.assertTrue(launcher.validate_reverse_mapping(mappings, 8788))

    def test_health_payload_must_match_gateway_type(self):
        self.assertTrue(launcher.compatible_health_payload(8787, {"status": "ok", "provider": "tencent"}))
        self.assertFalse(launcher.compatible_health_payload(8787, {"status": "ok", "upstreamUrl": "x"}))
        self.assertTrue(launcher.compatible_health_payload(8788, {"status": "ok", "upstreamUrl": "https://example"}))

    def test_existing_healthy_gateway_is_reused_without_process(self):
        popen = Mock()
        app = launcher.GatewayLauncher(
            Path(__file__).resolve().parents[3],
            Path(__file__),
            health_check=lambda port, timeout: True,
            popen=popen,
        )
        app.ensure_gateway(8787)
        popen.assert_not_called()
        self.assertEqual([], app.children)

    def test_cleanup_removes_only_owned_mapping_and_process(self):
        calls = []
        process = Mock()
        process.poll.return_value = None
        app = launcher.GatewayLauncher(Path.cwd(), Path(__file__))
        app.device = launcher.AdbDevice("pico", "device")
        app.created_mappings.add(8787)
        app.children.append(launcher.ManagedProcess("owned", 8787, process))
        app._run_adb = lambda args, serial="": calls.append((tuple(args), serial)) or ""
        app.cleanup()
        self.assertEqual([(("reverse", "--remove", "tcp:8787"), "pico")], calls)
        process.terminate.assert_called_once()
        process.wait.assert_called_once_with(timeout=5)

    def test_disconnection_and_reconnection_restore_mappings(self):
        app = launcher.GatewayLauncher(
            Path.cwd(),
            Path(__file__),
            serial="pico",
            health_check=lambda port, timeout: True,
        )
        app.device = launcher.AdbDevice("pico", "device")
        app.created_mappings.add(8787)
        app._list_devices = lambda: []
        app.monitor_once()
        self.assertIsNone(app.device)
        self.assertEqual(set(), app.created_mappings)
        restored = []
        app._list_devices = lambda: [launcher.AdbDevice("pico", "device")]
        app._assert_pico = lambda device: None
        app.ensure_reverse_mappings = lambda: restored.append(True)
        app.monitor_once()
        self.assertEqual([True], restored)

    def test_adb_timeout_becomes_recoverable_launcher_warning(self):
        def timeout_runner(command, **kwargs):
            raise subprocess.TimeoutExpired(command, kwargs["timeout"])

        app = launcher.GatewayLauncher(
            Path.cwd(),
            Path(__file__),
            runner=timeout_runner,
        )
        with self.assertRaisesRegex(launcher.LauncherError, "timed out after 10 seconds"):
            app._run_adb(("devices", "-l"))

    def test_redaction_hides_common_secret_forms(self):
        text = launcher.redact("api_key=abc secretKey:xyz Authorization=Bearer123 bearer token456")
        self.assertNotIn("abc", text)
        self.assertNotIn("xyz", text)
        self.assertNotIn("Bearer123", text)
        self.assertNotIn("token456", text)


if __name__ == "__main__":
    unittest.main()
