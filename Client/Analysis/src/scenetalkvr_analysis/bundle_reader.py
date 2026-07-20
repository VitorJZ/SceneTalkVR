from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .checksum_validator import sha256_file, validate_checksums


class BundleError(RuntimeError):
    pass


def _json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        raise BundleError(f"json_invalid:{path}") from exc


def _jsonl(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        return []
    values: list[dict[str, Any]] = []
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        try:
            value = json.loads(line)
        except Exception as exc:
            raise BundleError(f"jsonl_invalid:{path}:{number}") from exc
        value["_sourceFile"] = path.relative_to(path.parents[1]).as_posix()
        value["_sourceLine"] = number
        values.append(value)
    return values


@dataclass(frozen=True)
class SessionBundle:
    root: Path
    manifest: dict[str, Any]
    assignment: dict[str, Any]
    timing: list[dict[str, Any]]
    study: list[dict[str, Any]]
    questionnaire: list[dict[str, Any]]
    ranking: list[dict[str, Any]]
    interview: list[dict[str, Any]]
    source_hashes: dict[str, str]
    manifest_hash: str

    @classmethod
    def read(cls, root: str | Path, verify_checksums: bool = True) -> "SessionBundle":
        path = Path(root).resolve()
        manifest_path = path / "manifest.json"
        assignment_path = path / "assignment" / "assignment.json"
        if not manifest_path.is_file():
            raise BundleError("manifest_missing")
        if not assignment_path.is_file():
            raise BundleError("assignment_missing")
        hashes, errors = validate_checksums(path)
        if verify_checksums and errors:
            raise BundleError(";".join(errors))
        return cls(
            root=path,
            manifest=_json(manifest_path),
            assignment=_json(assignment_path),
            timing=_jsonl(path / "timing" / "timing.jsonl"),
            study=_jsonl(path / "study" / "study.jsonl"),
            questionnaire=_jsonl(path / "questionnaire" / "questionnaire.jsonl"),
            ranking=_jsonl(path / "ranking" / "ranking.jsonl"),
            interview=_jsonl(path / "interview" / "interview.jsonl"),
            source_hashes=hashes,
            manifest_hash=sha256_file(manifest_path),
        )
