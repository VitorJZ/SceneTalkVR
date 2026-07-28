#!/usr/bin/env python3
"""Start SceneTalkVR gateways and maintain PICO adb reverse tunnels."""

from __future__ import annotations

import argparse
import atexit
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Mapping, Sequence
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


VOICE_PORT = 8787
LLM_PORT = 8788
REVERSE_PORTS = (VOICE_PORT, LLM_PORT)
SECRET_PATTERN = re.compile(
    r"(?i)(api[_-]?key|secret[_-]?(?:id|key)|authorization|token)"
    r"(\s*[:=]\s*)([^\s,;]+)"
)


class LauncherError(RuntimeError):
    """Expected launcher failure with a user-actionable message."""


@dataclass(frozen=True)
class AdbDevice:
    serial: str
    state: str
    description: str = ""


@dataclass
class ManagedProcess:
    name: str
    port: int
    process: subprocess.Popen[bytes]


def redact(value: object) -> str:
    """Return a bounded log-safe string without credentials."""
    text = str(value or "")
    text = SECRET_PATTERN.sub(lambda match: f"{match.group(1)}{match.group(2)}<redacted>", text)
    text = re.sub(r"(?i)(bearer\s+)[^\s,;]+", r"\1<redacted>", text)
    return text[:1000]


def _adb_name() -> str:
    return "adb.exe" if os.name == "nt" else "adb"


def _candidate_unity_adb_paths() -> Iterable[Path]:
    suffixes = (
        Path("Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools") / _adb_name(),
        Path("Unity.app/Contents/PlaybackEngines/AndroidPlayer/SDK/platform-tools") / _adb_name(),
    )
    roots: list[Path] = []
    if os.name == "nt":
        for variable in ("ProgramFiles", "ProgramFiles(x86)"):
            if os.getenv(variable):
                roots.append(Path(os.environ[variable]) / "Unity/Hub/Editor")
    elif sys.platform == "darwin":
        roots.append(Path("/Applications/Unity/Hub/Editor"))
    else:
        roots.extend((Path.home() / "Unity/Hub/Editor", Path("/opt/unity/editors")))

    for root in roots:
        if not root.is_dir():
            continue
        for editor in sorted(root.iterdir(), reverse=True):
            for suffix in suffixes:
                yield editor / suffix


def locate_adb(
    explicit: str = "",
    environ: Mapping[str, str] | None = None,
    which: Callable[[str], str | None] = shutil.which,
    unity_candidates: Iterable[Path] | None = None,
) -> Path:
    env = environ or os.environ
    candidates: list[Path] = []
    if explicit:
        candidates.append(Path(explicit).expanduser())
    for name in ("SCENETALK_ADB", "ADB"):
        if env.get(name):
            candidates.append(Path(env[name]).expanduser())
    for name in ("ANDROID_SDK_ROOT", "ANDROID_HOME"):
        if env.get(name):
            candidates.append(Path(env[name]).expanduser() / "platform-tools" / _adb_name())
    path_match = which("adb")
    if path_match:
        candidates.append(Path(path_match))
    candidates.extend(unity_candidates if unity_candidates is not None else _candidate_unity_adb_paths())

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise LauncherError(
        "ADB not found. Use --adb, SCENETALK_ADB, ANDROID_SDK_ROOT, PATH, "
        "or install Unity Android Build Support."
    )


def parse_adb_devices(output: str) -> list[AdbDevice]:
    devices: list[AdbDevice] = []
    for raw_line in output.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("List of devices attached") or line.startswith("*"):
            continue
        fields = line.split()
        if len(fields) >= 2:
            devices.append(AdbDevice(fields[0], fields[1], " ".join(fields[2:])))
    return devices


def choose_device(devices: Sequence[AdbDevice], requested_serial: str = "") -> AdbDevice:
    if requested_serial:
        selected = next((item for item in devices if item.serial == requested_serial), None)
        if selected is None:
            raise LauncherError(f"Requested PICO '{requested_serial}' is not connected.")
        if selected.state != "device":
            raise LauncherError(
                f"PICO '{requested_serial}' is '{selected.state}'. Unlock it and authorize USB debugging."
            )
        return selected

    authorized = [item for item in devices if item.state == "device"]
    unauthorized = [item for item in devices if item.state != "device"]
    if not authorized:
        suffix = f" Detected: {', '.join(item.state for item in unauthorized)}." if unauthorized else ""
        raise LauncherError("No authorized PICO is connected." + suffix)
    if len(authorized) != 1:
        serials = ", ".join(item.serial for item in authorized)
        raise LauncherError(f"Multiple authorized Android devices are connected ({serials}); use --serial.")
    return authorized[0]


def parse_reverse_list(output: str) -> dict[str, str]:
    mappings: dict[str, str] = {}
    for raw_line in output.splitlines():
        fields = raw_line.strip().split()
        if len(fields) >= 3 and fields[-2].startswith("tcp:"):
            mappings[fields[-2]] = fields[-1]
        elif len(fields) == 2 and fields[0].startswith("tcp:"):
            mappings[fields[0]] = fields[1]
    return mappings


def validate_reverse_mapping(mappings: Mapping[str, str], port: int) -> bool:
    endpoint = f"tcp:{port}"
    existing = mappings.get(endpoint)
    if existing is None:
        return False
    if existing != endpoint:
        raise LauncherError(
            f"ADB reverse endpoint {endpoint} is already mapped to {existing}; remove the conflicting mapping first."
        )
    return True


def compatible_health_payload(port: int, payload: object) -> bool:
    if not isinstance(payload, dict) or payload.get("status") != "ok":
        return False
    if port == VOICE_PORT:
        return isinstance(payload.get("provider"), str) and bool(payload.get("provider"))
    if port == LLM_PORT:
        return isinstance(payload.get("upstreamUrl"), str) and bool(payload.get("upstreamUrl"))
    return False


def check_health(port: int, timeout: float = 1.5) -> bool:
    request = Request(f"http://127.0.0.1:{port}/health", headers={"Accept": "application/json"})
    try:
        with urlopen(request, timeout=timeout) as response:
            payload = json.loads(response.read().decode("utf-8"))
            return response.status == 200 and compatible_health_payload(port, payload)
    except (HTTPError, URLError, TimeoutError, json.JSONDecodeError, OSError):
        return False


class GatewayLauncher:
    def __init__(
        self,
        repository_root: Path,
        adb_path: Path,
        serial: str = "",
        monitor_interval: float = 2.0,
        runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
        health_check: Callable[[int, float], bool] = check_health,
        popen: Callable[..., subprocess.Popen[bytes]] = subprocess.Popen,
    ) -> None:
        self.repository_root = repository_root.resolve()
        self.adb_path = adb_path.resolve()
        self.requested_serial = serial
        self.monitor_interval = max(0.25, monitor_interval)
        self.runner = runner
        self.health_check = health_check
        self.popen = popen
        self.device: AdbDevice | None = None
        self.created_mappings: set[int] = set()
        self.children: list[ManagedProcess] = []
        self.stopping = False

    def _run_adb(self, arguments: Sequence[str], *, serial: str = "") -> str:
        command = [str(self.adb_path)]
        if serial:
            command.extend(("-s", serial))
        command.extend(arguments)
        try:
            completed = self.runner(
                command,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                timeout=10,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            detail = redact(error.stderr or error.stdout or "")
            suffix = f": {detail}" if detail else ""
            raise LauncherError(
                f"ADB command timed out after 10 seconds ({' '.join(arguments)}){suffix}"
            ) from error
        if completed.returncode != 0:
            detail = redact(completed.stderr.strip() or completed.stdout.strip())
            raise LauncherError(f"ADB command failed ({' '.join(arguments)}): {detail}")
        return completed.stdout

    def _list_devices(self) -> list[AdbDevice]:
        return parse_adb_devices(self._run_adb(("devices", "-l")))

    def _assert_pico(self, device: AdbDevice) -> None:
        manufacturer = self._run_adb(("shell", "getprop", "ro.product.manufacturer"), serial=device.serial).strip()
        model = self._run_adb(("shell", "getprop", "ro.product.model"), serial=device.serial).strip()
        identity = f"{manufacturer} {model}".lower()
        if "pico" not in identity and "a8110" not in identity:
            raise LauncherError(
                f"Selected device '{device.serial}' is not recognized as PICO (manufacturer/model: {redact(manufacturer)} {redact(model)})."
            )

    def select_pico(self) -> AdbDevice:
        device = choose_device(self._list_devices(), self.requested_serial)
        self._assert_pico(device)
        self.device = device
        return device

    def ensure_reverse_mappings(self) -> None:
        if self.device is None:
            raise LauncherError("PICO has not been selected.")
        output = self._run_adb(("reverse", "--list"), serial=self.device.serial)
        mappings = parse_reverse_list(output)
        for port in REVERSE_PORTS:
            if validate_reverse_mapping(mappings, port):
                continue
            endpoint = f"tcp:{port}"
            self._run_adb(("reverse", "--no-rebind", endpoint, endpoint), serial=self.device.serial)
            self.created_mappings.add(port)
        verified = parse_reverse_list(
            self._run_adb(("reverse", "--list"), serial=self.device.serial)
        )
        for port in REVERSE_PORTS:
            if not validate_reverse_mapping(verified, port):
                raise LauncherError(f"ADB reverse mapping tcp:{port} was not created.")

    def _gateway_spec(self, port: int) -> tuple[str, Path, Sequence[str], Mapping[str, str]]:
        if port == VOICE_PORT:
            directory = self.repository_root / "Server" / "voice-gateway"
            module = "src.voice_gateway.main"
            env_updates = {"VOICE_GATEWAY_HOST": "0.0.0.0", "VOICE_GATEWAY_PORT": str(port)}
            name = "Voice Gateway"
        else:
            directory = self.repository_root / "Server" / "llm-gateway"
            module = "src.llm_gateway.main"
            env_updates = {"LLM_GATEWAY_HOST": "0.0.0.0", "LLM_GATEWAY_PORT": str(port)}
            name = "LLM Gateway"
        return name, directory, (sys.executable, "-m", module), env_updates

    def ensure_gateway(self, port: int) -> None:
        if self.health_check(port, 1.5):
            print(f"[launcher] Reusing compatible service on 127.0.0.1:{port}.", flush=True)
            return
        name, directory, command, updates = self._gateway_spec(port)
        if not directory.is_dir():
            raise LauncherError(f"{name} directory is missing: {directory}")
        env = os.environ.copy()
        env.update(updates)
        process = self.popen(command, cwd=directory, env=env)
        managed = ManagedProcess(name, port, process)
        self.children.append(managed)
        deadline = time.monotonic() + 10.0
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise LauncherError(f"{name} exited during startup with code {process.returncode}.")
            if self.health_check(port, 0.75):
                print(f"[launcher] Started {name} on 0.0.0.0:{port}.", flush=True)
                return
            time.sleep(0.2)
        raise LauncherError(
            f"Port {port} did not expose a compatible {name} /health endpoint. "
            "If another process owns the port, stop it or use the SceneTalkVR gateway."
        )

    def start(self) -> None:
        self.ensure_gateway(VOICE_PORT)
        self.ensure_gateway(LLM_PORT)
        device = self.select_pico()
        self.ensure_reverse_mappings()
        print(
            f"[launcher] USB route ready for PICO {device.serial}: "
            "127.0.0.1:8787 and 127.0.0.1:8788.",
            flush=True,
        )

    def monitor_once(self) -> None:
        for child in self.children:
            if child.process.poll() is not None:
                raise LauncherError(f"{child.name} exited unexpectedly with code {child.process.returncode}.")
        for port in REVERSE_PORTS:
            if self.health_check(port, 1.0):
                continue
            owned = next((child for child in self.children if child.port == port), None)
            if owned is not None:
                raise LauncherError(f"{owned.name} health check failed on port {port}.")
            self.ensure_gateway(port)
        devices = self._list_devices()
        try:
            selected = choose_device(devices, self.requested_serial or (self.device.serial if self.device else ""))
        except LauncherError as error:
            print(f"[launcher] USB disconnected: {redact(error)}", flush=True)
            self.device = None
            self.created_mappings.clear()
            return
        reconnected = self.device is None
        self.device = selected
        if reconnected:
            self._assert_pico(selected)
        self.ensure_reverse_mappings()
        if reconnected:
            print(f"[launcher] USB reconnected; ADB mappings restored for {selected.serial}.", flush=True)

    def run_forever(self) -> None:
        while not self.stopping:
            started = time.monotonic()
            try:
                self.monitor_once()
            except LauncherError as error:
                print(f"[launcher] Monitor warning: {redact(error)}", flush=True)
            remaining = self.monitor_interval - (time.monotonic() - started)
            if remaining > 0:
                time.sleep(remaining)

    def cleanup(self) -> None:
        if self.stopping:
            return
        self.stopping = True
        if self.device is not None:
            for port in sorted(self.created_mappings):
                endpoint = f"tcp:{port}"
                try:
                    self._run_adb(("reverse", "--remove", endpoint), serial=self.device.serial)
                except LauncherError as error:
                    print(f"[launcher] Cleanup warning: {redact(error)}", flush=True)
        for child in reversed(self.children):
            if child.process.poll() is not None:
                continue
            child.process.terminate()
            try:
                child.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.process.kill()
                child.process.wait(timeout=2)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default="", help="Path to adb executable.")
    parser.add_argument("--serial", default=os.getenv("SCENETALK_PICO_SERIAL", ""), help="PICO ADB serial.")
    parser.add_argument("--monitor-interval", type=float, default=2.0, help="Reconnect poll interval in seconds.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    repository_root = Path(__file__).resolve().parents[2]
    launcher: GatewayLauncher | None = None
    try:
        adb_path = locate_adb(args.adb)
        launcher = GatewayLauncher(repository_root, adb_path, args.serial, args.monitor_interval)
        atexit.register(launcher.cleanup)

        def stop(_signum: int, _frame: object) -> None:
            if launcher is not None:
                launcher.cleanup()

        signal.signal(signal.SIGINT, stop)
        if hasattr(signal, "SIGTERM"):
            signal.signal(signal.SIGTERM, stop)
        launcher.start()
        launcher.run_forever()
        return 0
    except (LauncherError, OSError) as error:
        print(f"[launcher] ERROR: {redact(error)}", file=sys.stderr, flush=True)
        if launcher is not None:
            launcher.cleanup()
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
