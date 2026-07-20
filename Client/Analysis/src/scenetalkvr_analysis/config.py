from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


DEFAULT_CONFIG: dict[str, Any] = {
    "analysisVersion": "1.0",
    "includeSyntheticForTesting": False,
    "includeDemoForTesting": False,
    "requireCollectionEligible": True,
    "requireIntegrityPass": True,
    "primaryAttemptPolicy": "UNCONFIRMED",
    "timingToleranceMs": 0,
    "includeTranscriptText": False,
    "includeInterviewTextInAggregate": False,
    "allowedProtocolVersions": [],
    "missingRequiredQuestionnaireAction": "exclude_condition",
    "technicalInvalidAction": "retain_and_flag",
}


def load_config(path: str | Path | None) -> tuple[dict[str, Any], str]:
    config = dict(DEFAULT_CONFIG)
    if path:
        config.update(json.loads(Path(path).read_text(encoding="utf-8-sig")))
    canonical = json.dumps(config, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return config, hashlib.sha256(canonical.encode("utf-8")).hexdigest()
