# SceneTalkVR USB Gateway Launcher

The launcher starts or reuses both PC gateways and maintains the PICO USB route:

```text
PICO 127.0.0.1:8787 --ADB reverse/USB--> PC Voice Gateway
PICO 127.0.0.1:8788 --ADB reverse/USB--> PC LLM Gateway
```

The PC still needs Internet access for Tencent Cloud and the configured LLM upstream.

## Start

Enable developer mode and USB debugging on the PICO, connect the data cable, then run from the repository root:

```bash
python Server/gateway-launcher/scenetalk_gateway_launcher.py
```

When more than one authorized Android device is connected, select the PICO explicitly:

```bash
python Server/gateway-launcher/scenetalk_gateway_launcher.py --serial <adb-serial>
```

ADB is resolved from `--adb`, `SCENETALK_ADB`, `ADB`, `ANDROID_SDK_ROOT`, `ANDROID_HOME`, `PATH`, or a Unity Hub Android SDK. The launcher reuses compatible `/health` services, restores mappings after reconnect, and removes only mappings and gateway processes that it created.

Run the isolated tests with:

```bash
python -m unittest discover -s Server/gateway-launcher/tests -v
```
