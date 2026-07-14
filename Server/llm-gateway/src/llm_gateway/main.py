from .api.server import run_server
from .config import GatewayConfig


def main() -> None:
    config = GatewayConfig.from_env()
    run_server(config)


if __name__ == "__main__":
    main()
