install: ## [Local development] Upgrade pip, install requirements, install package.
	python -m pip install -U pip
	python -m pip install -e .

install-dev: ## [Local development] Install requirements
	python -m pip install -r requirements.txt

black: ## [Local development] Auto-format python code using black
	python -m black .

test: ## [Local development] Run unit tests
	python -m pytest -x -s -v tests

run-app: ## [Local development] Run the FastAPI backend
	xvfb-run -a /home/spring5/miniconda3/envs/holodeck/bin/python -m uvicorn app:app --host 0.0.0.0 --port 8080


.PHONY: help

help: # Run `make help` to get help on the make commands
	@grep -E '^[0-9a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'