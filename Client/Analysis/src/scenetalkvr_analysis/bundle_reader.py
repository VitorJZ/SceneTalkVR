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


def _jsonl_directory(path: Path) -> list[dict[str, Any]]:
    values: list[dict[str, Any]] = []
    if not path.is_dir():
        return values
    for file in sorted(path.glob("*.jsonl")):
        values.extend(_normalize_event(value) for value in _jsonl(file))
    return values


def _goal_snapshots(path: Path) -> list[dict[str, Any]]:
    values: list[dict[str, Any]] = []
    if not path.is_dir():
        return values
    for file in sorted(path.glob("*.json")):
        snapshot = _json(file)
        for goal in snapshot.get("goals", []):
            row = dict(goal)
            row.update({
                "participantId": snapshot.get("participantId", ""),
                "sessionId": snapshot.get("sessionId", ""),
                "conditionRunId": goal.get("conditionRunId") or snapshot.get("pilotRunId", ""),
                "taskId": snapshot.get("taskId", ""),
                "eventType": "GoalConfirmed" if goal.get("state") == 2 else "GoalCandidateSubmitted" if goal.get("state") == 1 else "GoalPending",
                "timestampUtc": goal.get("confirmedAtUtc") or goal.get("candidateAtUtc") or snapshot.get("savedAtUtc", ""),
                "turnId": goal.get("evidenceTurnId", ""),
                "actor": goal.get("confirmedBy", ""),
                "_sourceFile": file.relative_to(path.parent).as_posix(),
            })
            values.append(row)
    return values


def _normalize_event(value: dict[str, Any]) -> dict[str, Any]:
    value = dict(value)
    value["conditionRunId"] = value.get("conditionRunId") or value.get("pilotRunId", "")
    value["conditionLabel"] = value.get("conditionLabel") or value.get("embodimentCondition", "")
    return value


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
            timing=_jsonl_directory(path / "timing"),
            study=_jsonl_directory(path / "study") + _goal_snapshots(path / "goals"),
            questionnaire=_jsonl_directory(path / "questionnaire"),
            ranking=_jsonl_directory(path / "ranking"),
            interview=_jsonl_directory(path / "interview"),
            source_hashes=hashes,
            manifest_hash=sha256_file(manifest_path),
        )
