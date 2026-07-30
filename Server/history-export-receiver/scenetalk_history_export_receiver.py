#!/usr/bin/env python3
"""Receive SceneTalkVR history snapshots over the local ADB reverse tunnel."""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import tempfile
import zipfile
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence
from urllib.parse import urlparse
from xml.sax.saxutils import escape


SCHEMA_VERSION = "1.0"
DEFAULT_PORT = 8789
DEFAULT_MAX_BODY_BYTES = 128 * 1024 * 1024
EXPORT_ID_PATTERN = re.compile(r"^[A-Fa-f0-9-]{8,64}$")
INVALID_XML_PATTERN = re.compile(
    "[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD\U00010000-\U0010FFFF]"
)
CHINA_TIMEZONE = timezone(timedelta(hours=8))
EXCEL_EPOCH = datetime(1899, 12, 30)
FORMAL_CONDITIONS = ("NE", "NR", "SE", "SR")


class ExportError(RuntimeError):
    def __init__(self, code: str, message: str, status: HTTPStatus) -> None:
        super().__init__(message)
        self.code = code
        self.status = status


@dataclass(frozen=True)
class ReceiverConfig:
    host: str
    port: int
    export_dir: Path
    max_body_bytes: int = DEFAULT_MAX_BODY_BYTES

    @staticmethod
    def from_env() -> "ReceiverConfig":
        default_dir = Path.home() / "Documents" / "SceneTalkVRExports"
        return ReceiverConfig(
            host=os.getenv("SCENETALK_EXPORT_HOST", "127.0.0.1"),
            port=int(os.getenv("SCENETALK_EXPORT_PORT", str(DEFAULT_PORT))),
            export_dir=Path(os.getenv("SCENETALK_EXPORT_DIR", str(default_dir))).expanduser(),
            max_body_bytes=max(
                1024,
                int(os.getenv("SCENETALK_EXPORT_MAX_BODY_BYTES", str(DEFAULT_MAX_BODY_BYTES))),
            ),
        )


@dataclass(frozen=True)
class ExcelDateValue:
    serial: float


@dataclass(frozen=True)
class WorksheetSpec:
    name: str
    headers: Sequence[str]
    rows: Sequence[Sequence[Any]]
    freeze_columns: int = 0
    wrap_headers: bool = False
    max_column_width: int = 60


def _number(value: Any, default: int = 0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def _text(value: Any) -> str:
    return "" if value is None else str(value)


def _dictionary(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _iso_sort_key(value: Any) -> str:
    return _text(value).strip()


def _parse_timestamp(value: Any) -> datetime | None:
    text = _text(value).strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except (TypeError, ValueError):
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def _excel_local_date(value: Any) -> ExcelDateValue | str:
    parsed = _parse_timestamp(value)
    if parsed is None:
        return ""
    local_wall_time = parsed.astimezone(CHINA_TIMEZONE).replace(tzinfo=None)
    return ExcelDateValue((local_wall_time - EXCEL_EPOCH).total_seconds() / 86400.0)


def _timestamp_sort_key(value: Any) -> tuple[bool, datetime]:
    parsed = _parse_timestamp(value)
    return parsed is None, parsed or datetime.max.replace(tzinfo=timezone.utc)


def validate_and_sort_bundle(payload: Any) -> dict[str, Any]:
    if not isinstance(payload, dict):
        raise ExportError("invalid_json", "Export body must be a JSON object.", HTTPStatus.BAD_REQUEST)
    if payload.get("schemaVersion") != SCHEMA_VERSION:
        raise ExportError(
            "unsupported_schema",
            f"Expected history export schema {SCHEMA_VERSION}.",
            HTTPStatus.BAD_REQUEST,
        )
    export_id = _text(payload.get("exportId")).strip()
    if not EXPORT_ID_PATTERN.fullmatch(export_id):
        raise ExportError("invalid_export_id", "exportId is missing or invalid.", HTTPStatus.BAD_REQUEST)
    if not isinstance(payload.get("experiments"), list):
        raise ExportError("invalid_experiments", "experiments must be an array.", HTTPStatus.BAD_REQUEST)
    if not payload["experiments"]:
        raise ExportError(
            "history_export_empty",
            "There is no experiment history to export.",
            HTTPStatus.BAD_REQUEST,
        )

    definitions = _list(payload.get("questionnaireDefinitions"))
    for definition in definitions:
        value = _dictionary(definition)
        value["items"] = sorted(
            _list(value.get("items")),
            key=lambda item: (
                _number(_dictionary(item).get("displayOrder")),
                _text(_dictionary(item).get("itemId")),
            ),
        )
    payload["questionnaireDefinitions"] = sorted(
        definitions,
        key=lambda item: _text(_dictionary(item).get("questionnaireId")),
    )

    experiments = payload["experiments"]
    for experiment in experiments:
        if not isinstance(experiment, dict):
            raise ExportError(
                "invalid_experiment", "Every experiment must be an object.", HTTPStatus.BAD_REQUEST
            )
        summary = _dictionary(experiment.get("summary"))
        experiment["attempts"] = sorted(
            _list(experiment.get("attempts")),
            key=lambda item: (
                _number(_dictionary(item).get("startedAtUnixMs")),
                _number(_dictionary(item).get("attemptIndex")),
                _text(_dictionary(item).get("attemptId")),
            ),
        )
        conversations = _list(experiment.get("conversations"))
        for conversation in conversations:
            detail = _dictionary(conversation)
            detail["turns"] = sorted(
                _list(detail.get("turns")),
                key=lambda item: (
                    _number(_dictionary(item).get("createdAtUnixMs")),
                    _number(_dictionary(item).get("sequenceIndex")),
                ),
            )
        experiment["conversations"] = sorted(
            conversations,
            key=lambda item: (
                _number(_dictionary(_dictionary(item).get("summary")).get("createdAtUnixMs")),
                _text(_dictionary(_dictionary(item).get("summary")).get("sessionId")),
            ),
        )
        questionnaires = _list(experiment.get("questionnaires"))
        for questionnaire in questionnaires:
            session = _dictionary(_dictionary(questionnaire).get("session"))
            session["responses"] = sorted(
                _list(session.get("responses")),
                key=lambda item: (
                    _iso_sort_key(_dictionary(item).get("responseCapturedAtUtc")),
                    _text(_dictionary(item).get("itemId")),
                ),
            )
            session["sectionScores"] = sorted(
                _list(session.get("sectionScores")),
                key=lambda item: _text(_dictionary(item).get("sectionId")),
            )
        experiment["questionnaires"] = sorted(
            questionnaires,
            key=lambda item: (
                _iso_sort_key(_dictionary(_dictionary(item).get("session")).get("startedAtUtc")),
                _text(_dictionary(item).get("questionnaireRecordId")),
            ),
        )
        experiment["rankings"] = sorted(
            _list(experiment.get("rankings")),
            key=lambda item: _iso_sort_key(
                _dictionary(_dictionary(item).get("response")).get("submittedAtUtc")
            ),
        )
        experiment["summary"] = summary

    payload["experiments"] = sorted(
        experiments,
        key=lambda item: (
            _number(_dictionary(_dictionary(item).get("summary")).get("createdAtUnixMs")),
            _text(_dictionary(_dictionary(item).get("summary")).get("experimentId")),
        ),
    )
    return payload


def canonical_json(payload: Any) -> bytes:
    return json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def export_folder_name(exported_at_utc: str, export_id: str) -> str:
    try:
        parsed = datetime.fromisoformat(exported_at_utc.replace("Z", "+00:00"))
    except (TypeError, ValueError) as error:
        raise ExportError(
            "invalid_export_time", "exportedAtUtc must be an ISO-8601 timestamp.", HTTPStatus.BAD_REQUEST
        ) from error
    parsed = parsed.astimezone(timezone.utc)
    return f"{parsed.strftime('%Y%m%dT%H%M%SZ')}_{export_id[:8].lower()}"


def _xml_text(value: Any) -> str:
    cleaned = INVALID_XML_PATTERN.sub("", _text(value))
    if len(cleaned) > 32767:
        cleaned = cleaned[:32754] + "…[truncated]"
    return escape(cleaned, {'"': "&quot;"})


def _column_name(index: int) -> str:
    result = ""
    value = index + 1
    while value:
        value, remainder = divmod(value - 1, 26)
        result = chr(65 + remainder) + result
    return result


def _worksheet_xml(
    headers: Sequence[str],
    rows: Sequence[Sequence[Any]],
    *,
    freeze_columns: int = 0,
    wrap_headers: bool = False,
    max_column_width: int = 60,
) -> str:
    all_rows: list[Sequence[Any]] = [headers, *rows]
    row_xml: list[str] = []
    widths = [len(header) for header in headers]
    for row_index, row in enumerate(all_rows, start=1):
        cells: list[str] = []
        for column_index, raw_value in enumerate(row):
            reference = f"{_column_name(column_index)}{row_index}"
            if column_index < len(widths):
                display_width = 19 if isinstance(raw_value, ExcelDateValue) else len(_text(raw_value))
                widths[column_index] = min(
                    max(10, max_column_width),
                    max(widths[column_index], display_width),
                )
            style = ' s="3"' if row_index == 1 and wrap_headers else ' s="1"' if row_index == 1 else ""
            if isinstance(raw_value, ExcelDateValue):
                cells.append(f'<c r="{reference}" t="n" s="2"><v>{raw_value.serial}</v></c>')
            elif isinstance(raw_value, bool):
                cells.append(f'<c r="{reference}" t="b"{style}><v>{1 if raw_value else 0}</v></c>')
            elif isinstance(raw_value, (int, float)) and not isinstance(raw_value, bool):
                cells.append(f'<c r="{reference}" t="n"{style}><v>{raw_value}</v></c>')
            else:
                cells.append(
                    f'<c r="{reference}" t="inlineStr"{style}><is><t xml:space="preserve">'
                    f"{_xml_text(raw_value)}</t></is></c>"
                )
        row_style = ' ht="42" customHeight="1"' if row_index == 1 and wrap_headers else ""
        row_xml.append(f'<row r="{row_index}"{row_style}>{"".join(cells)}</row>')
    column_xml = "".join(
        f'<col min="{index + 1}" max="{index + 1}" width="{max(10, width + 2)}" customWidth="1"/>'
        for index, width in enumerate(widths)
    )
    last_cell = f"{_column_name(max(0, len(headers) - 1))}{max(1, len(all_rows))}"
    if freeze_columns > 0:
        top_left = f"{_column_name(freeze_columns)}2"
        pane = (
            f'<pane xSplit="{freeze_columns}" ySplit="1" topLeftCell="{top_left}" '
            'activePane="bottomRight" state="frozen"/>'
        )
    else:
        pane = '<pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>'
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        f'<dimension ref="A1:{last_cell}"/>'
        f'<sheetViews><sheetView workbookViewId="0">{pane}</sheetView></sheetViews>'
        '<sheetFormatPr defaultRowHeight="15"/>'
        f"<cols>{column_xml}</cols><sheetData>{''.join(row_xml)}</sheetData>"
        f'<autoFilter ref="A1:{_column_name(max(0, len(headers) - 1))}1"/>'
        "</worksheet>"
    )


QUESTIONNAIRE_HEADERS = (
    "experimentId", "experimentKind", "experimentStatus", "participantId", "experimentSessionId",
    "attemptId", "questionnaireRecordId", "protocolVersion", "questionnaireCatalogVersion",
    "questionnaireId", "questionnaireVersion", "conditionRunId", "questionnaireLinkageKey",
    "conditionPosition", "taskId", "taskAssignmentId", "formalCondition", "embodimentCondition",
    "completionStatus", "completionReason", "technicalValidity", "startedAtUtc", "submittedAtUtc",
    "skippedAtUtc", "completionRate", "hasMissing", "answeredCount", "responseCount", "promptCount",
    "revision", "runtimeMode", "dataOrigin", "collectionEligible", "developerTestAssignment",
)

RESPONSE_HEADERS = (
    "experimentId", "participantId", "experimentSessionId", "attemptId", "questionnaireRecordId",
    "questionnaireId", "questionnaireLinkageKey", "conditionRunId", "conditionPosition", "taskId",
    "sectionId", "itemId", "itemVersion", "promptEnglish", "promptChinese", "rawValue",
    "rawValueTruncated", "responseCapturedAtUtc", "scoredValue", "hasScoredValue", "reverseScored",
    "scaleMin", "scaleMax", "missing", "revision", "questionnaireStatus", "technicalValidity",
)

SCORE_HEADERS = (
    "experimentId", "participantId", "experimentSessionId", "attemptId", "questionnaireRecordId",
    "questionnaireId", "questionnaireLinkageKey", "conditionRunId", "conditionPosition", "taskId",
    "sectionId", "mean", "answeredCount", "itemCount", "hasMissing",
)


def _enum_label(value: Any, labels: Mapping[int, str]) -> str:
    if isinstance(value, str) and not value.isdigit():
        return value
    return labels.get(_number(value, -1), _text(value))


def workbook_rows(bundle: Mapping[str, Any]) -> tuple[list[list[Any]], list[list[Any]], list[list[Any]]]:
    questionnaire_rows: list[list[Any]] = []
    response_rows: list[list[Any]] = []
    score_rows: list[list[Any]] = []
    for experiment in _list(bundle.get("experiments")):
        record = _dictionary(experiment)
        summary = _dictionary(record.get("summary"))
        experiment_id = _text(summary.get("experimentId"))
        experiment_kind = _enum_label(summary.get("kind"), {0: "Pilot", 1: "Formal"})
        experiment_status = _enum_label(
            summary.get("status"), {0: "InProgress", 1: "Suspended", 2: "Completed"}
        )
        for questionnaire in _list(record.get("questionnaires")):
            item = _dictionary(questionnaire)
            session = _dictionary(item.get("session"))
            prompts = {
                _text(_dictionary(prompt).get("itemId")): _dictionary(prompt)
                for prompt in _list(item.get("prompts"))
            }
            responses = [_dictionary(response) for response in _list(session.get("responses"))]
            answered_count = sum(1 for response in responses if _text(response.get("rawValue")).strip())
            embodiment_condition = _text(session.get("embodimentCondition"))
            formal_condition = "" if embodiment_condition else _enum_label(
                session.get("formalCondition"), {0: "NE", 1: "NR", 2: "SE", 3: "SR"}
            )
            technical_validity = _enum_label(
                session.get("technicalValidity"),
                {0: "Valid", 1: "Retry", 2: "FallbackUsed", 3: "TechnicalInvalid"},
            )
            common = [
                experiment_id,
                _text(summary.get("participantId")),
                _text(summary.get("sessionId")),
                _text(item.get("attemptId")),
                _text(item.get("questionnaireRecordId")),
                _text(session.get("questionnaireId")),
                _text(session.get("questionnaireLinkageKey")),
                _text(session.get("conditionRunId")),
                _number(session.get("conditionPosition")),
                _text(session.get("taskId")),
            ]
            questionnaire_rows.append(
                [
                    experiment_id, experiment_kind, experiment_status, _text(summary.get("participantId")),
                    _text(summary.get("sessionId")), _text(item.get("attemptId")),
                    _text(item.get("questionnaireRecordId")), _text(session.get("protocolVersion")),
                    _text(session.get("questionnaireCatalogVersion")), _text(session.get("questionnaireId")),
                    _text(session.get("questionnaireVersion")), _text(session.get("conditionRunId")),
                    _text(session.get("questionnaireLinkageKey")), _number(session.get("conditionPosition")),
                    _text(session.get("taskId")), _text(session.get("taskAssignmentId")),
                    formal_condition, embodiment_condition,
                    _enum_label(session.get("completionStatus"), {
                        0: "NotStarted", 1: "InProgress", 2: "Submitted", 3: "Reopened",
                        4: "Incompatible", 5: "Rejected", 6: "Skipped",
                    }),
                    _text(session.get("completionReason")), technical_validity,
                    _text(session.get("startedAtUtc")), _text(session.get("submittedAtUtc")),
                    _text(session.get("skippedAtUtc")), float(session.get("completionRate") or 0),
                    bool(session.get("hasMissing")), answered_count, len(responses), len(prompts),
                    _number(session.get("revision")), _text(session.get("runtimeMode")),
                    _text(session.get("dataOrigin")), bool(session.get("collectionEligible")),
                    bool(session.get("developerTestAssignment")),
                ]
            )
            for response in responses:
                prompt = prompts.get(_text(response.get("itemId")), {})
                raw_value = _text(response.get("rawValue"))
                response_rows.append(
                    common
                    + [
                        _text(response.get("sectionId")), _text(response.get("itemId")),
                        _text(response.get("itemVersion")), _text(prompt.get("promptEnglish")),
                        _text(prompt.get("promptChinese")), raw_value, len(raw_value) > 32767,
                        _text(response.get("responseCapturedAtUtc")),
                        float(response.get("scoredValue") or 0) if response.get("hasScoredValue") else "",
                        bool(response.get("hasScoredValue")), bool(response.get("reverseScored")),
                        _number(response.get("scaleMin")), _number(response.get("scaleMax")),
                        bool(response.get("missing")), _number(response.get("revision")),
                        _text(response.get("questionnaireStatus")), _text(response.get("technicalValidity")),
                    ]
                )
            for score in _list(session.get("sectionScores")):
                score_value = _dictionary(score)
                score_rows.append(
                    common
                    + [
                        _text(score_value.get("sectionId")), float(score_value.get("mean") or 0),
                        _number(score_value.get("answeredCount")), _number(score_value.get("itemCount")),
                        bool(score_value.get("hasMissing")),
                    ]
                )
    return questionnaire_rows, response_rows, score_rows


def _is_formal_experiment(record: Mapping[str, Any]) -> bool:
    summary = _dictionary(record.get("summary"))
    label = _enum_label(summary.get("kind"), {0: "Pilot", 1: "Formal"})
    if label.lower() == "formal":
        return True
    return _text(record.get("experimentKind")).strip().lower() == "formal"


def _definition_items(bundle: Mapping[str, Any], questionnaire_id: str) -> list[dict[str, Any]]:
    for definition in _list(bundle.get("questionnaireDefinitions")):
        value = _dictionary(definition)
        if _text(value.get("questionnaireId")) != questionnaire_id:
            continue
        return [
            _dictionary(item)
            for item in sorted(
                _list(value.get("items")),
                key=lambda item: (
                    _number(_dictionary(item).get("displayOrder")),
                    _text(_dictionary(item).get("itemId")),
                ),
            )
        ]
    return []


def _prompt_header(prompt: Mapping[str, Any], item_id: str) -> str:
    label = _text(prompt.get("promptChinese")).strip() or _text(prompt.get("promptEnglish")).strip()
    label = " ".join(label.split()) or item_id
    return f"{label} [{item_id}]"


def _formal_scene_columns(bundle: Mapping[str, Any]) -> list[tuple[str, str]]:
    columns: list[tuple[str, str]] = []
    seen: set[str] = set()

    def add(prompt: Mapping[str, Any]) -> None:
        item_id = _text(prompt.get("itemId")).strip()
        if not item_id or item_id in seen:
            return
        seen.add(item_id)
        columns.append((item_id, _prompt_header(prompt, item_id)))

    for item in _definition_items(bundle, "formal_condition_v1"):
        add(item)
    for experiment in _list(bundle.get("experiments")):
        record = _dictionary(experiment)
        if not _is_formal_experiment(record):
            continue
        for questionnaire in _list(record.get("questionnaires")):
            value = _dictionary(questionnaire)
            session = _dictionary(value.get("session"))
            if _text(session.get("questionnaireId")) != "formal_condition_v1":
                continue
            for prompt in _list(value.get("prompts")):
                add(_dictionary(prompt))
    return columns


def _latest_responses(session: Mapping[str, Any]) -> dict[str, dict[str, Any]]:
    selected: dict[str, tuple[tuple[int, datetime], dict[str, Any]]] = {}
    for response_value in _list(session.get("responses")):
        response = _dictionary(response_value)
        item_id = _text(response.get("itemId")).strip()
        if not item_id:
            continue
        captured = _parse_timestamp(response.get("responseCapturedAtUtc"))
        key = (
            _number(response.get("revision")),
            captured or datetime.min.replace(tzinfo=timezone.utc),
        )
        if item_id not in selected or key >= selected[item_id][0]:
            selected[item_id] = (key, response)
    return {item_id: value[1] for item_id, value in selected.items()}


def _scored_value(response: Mapping[str, Any] | None) -> float | int:
    if not response or not bool(response.get("hasScoredValue")):
        return -1
    try:
        return float(response.get("scoredValue"))
    except (TypeError, ValueError):
        return -1


def formal_scene_statistics(bundle: Mapping[str, Any]) -> tuple[list[str], list[list[Any]]]:
    prompt_columns = _formal_scene_columns(bundle)
    headers = ["participantId", "完成时间", "taskId", *[header for _, header in prompt_columns]]
    ordered_rows: list[tuple[tuple[Any, ...], list[Any]]] = []

    for experiment in _list(bundle.get("experiments")):
        record = _dictionary(experiment)
        if not _is_formal_experiment(record):
            continue
        summary = _dictionary(record.get("summary"))
        participant_id = _text(summary.get("participantId"))
        experiment_id = _text(summary.get("experimentId"))
        for questionnaire in _list(record.get("questionnaires")):
            value = _dictionary(questionnaire)
            session = _dictionary(value.get("session"))
            if _text(session.get("questionnaireId")) != "formal_condition_v1":
                continue
            completion_status = _enum_label(
                session.get("completionStatus"),
                {
                    0: "NotStarted", 1: "InProgress", 2: "Submitted", 3: "Reopened",
                    4: "Incompatible", 5: "Rejected", 6: "Skipped",
                },
            )
            if completion_status not in {"Submitted", "Skipped"}:
                continue
            completed_at = (
                session.get("submittedAtUtc")
                if completion_status == "Submitted"
                else session.get("skippedAtUtc")
            )
            responses = _latest_responses(session)
            task_id = _text(session.get("taskId"))
            row = [
                participant_id,
                _excel_local_date(completed_at),
                task_id,
                *[_scored_value(responses.get(item_id)) for item_id, _ in prompt_columns],
            ]
            ordered_rows.append(
                (
                    (
                        *_timestamp_sort_key(completed_at),
                        participant_id,
                        task_id,
                        experiment_id,
                        _text(value.get("questionnaireRecordId")),
                    ),
                    row,
                )
            )

    ordered_rows.sort(key=lambda value: value[0])
    return headers, [value[1] for value in ordered_rows]


def _condition_label(value: Any) -> str:
    label = _enum_label(value, {0: "NE", 1: "NR", 2: "SE", 3: "SR"}).strip().upper()
    return label if label in FORMAL_CONDITIONS else ""


def _formal_task_map(record: Mapping[str, Any]) -> dict[str, str]:
    selected_attempts: dict[str, tuple[tuple[int, int, int], str]] = {}
    for attempt_value in _list(record.get("attempts")):
        attempt = _dictionary(attempt_value)
        condition = _text(attempt.get("conditionKey")).strip().upper()
        status = _enum_label(
            attempt.get("status"),
            {0: "Running", 1: "Suspended", 2: "Completed", 3: "TechnicalInvalid", 4: "Aborted"},
        )
        task_id = _text(attempt.get("taskId")).strip()
        if condition not in FORMAL_CONDITIONS or status != "Completed" or not task_id:
            continue
        key = (
            _number(attempt.get("endedAtUnixMs")),
            _number(attempt.get("attemptIndex")),
            _number(attempt.get("startedAtUnixMs")),
        )
        if condition not in selected_attempts or key >= selected_attempts[condition][0]:
            selected_attempts[condition] = (key, task_id)

    result = {condition: value[1] for condition, value in selected_attempts.items()}
    questionnaire_fallbacks: dict[str, tuple[tuple[bool, datetime], str]] = {}
    for questionnaire in _list(record.get("questionnaires")):
        session = _dictionary(_dictionary(questionnaire).get("session"))
        if _text(session.get("questionnaireId")) != "formal_condition_v1":
            continue
        condition = _condition_label(session.get("formalCondition"))
        task_id = _text(session.get("taskId")).strip()
        if not condition or not task_id:
            continue
        completed_at = session.get("submittedAtUtc") or session.get("skippedAtUtc")
        parsed = _parse_timestamp(completed_at)
        key = (
            parsed is not None,
            parsed or datetime.min.replace(tzinfo=timezone.utc),
        )
        if condition not in questionnaire_fallbacks or key >= questionnaire_fallbacks[condition][0]:
            questionnaire_fallbacks[condition] = (key, task_id)
    for condition, value in questionnaire_fallbacks.items():
        result.setdefault(condition, value[1])
    return result


def _formal_ranking_definition(bundle: Mapping[str, Any]) -> tuple[str, str, list[str]]:
    for item in _definition_items(bundle, "formal_final_v1"):
        choices = []
        for raw_choice in _list(item.get("choiceValues")):
            choice = _text(raw_choice).strip().upper()
            if choice and choice not in choices:
                choices.append(choice)
        if _number(item.get("itemType"), -1) == 2 or choices:
            prompt = _text(item.get("promptChinese")).strip() or _text(item.get("promptEnglish")).strip()
            return _text(item.get("itemId")).strip() or "formal_rank_01", " ".join(prompt.split()), choices
    return "formal_rank_01", "正式反馈条件排序", list(FORMAL_CONDITIONS)


def formal_ranking_statistics(bundle: Mapping[str, Any]) -> tuple[list[str], list[list[Any]]]:
    item_id, prompt, choices = _formal_ranking_definition(bundle)
    choices = choices or list(FORMAL_CONDITIONS)
    score_headers = [f"{prompt} [{item_id}:{choice}]" for choice in choices]
    headers = ["participantId", "完成时间", "taskId", *score_headers, "偏好内容"]
    latest_by_participant: dict[
        str,
        tuple[tuple[Any, ...], tuple[Any, ...], list[Any]],
    ] = {}

    for experiment in _list(bundle.get("experiments")):
        record = _dictionary(experiment)
        if not _is_formal_experiment(record):
            continue
        summary = _dictionary(record.get("summary"))
        participant_id = _text(summary.get("participantId"))
        experiment_id = _text(summary.get("experimentId"))
        task_map = _formal_task_map(record)
        task_mapping = "; ".join(
            f"{condition}={task_map.get(condition, '-1')}" for condition in FORMAL_CONDITIONS
        )
        for ranking_index, ranking_value in enumerate(_list(record.get("rankings"))):
            response = _dictionary(_dictionary(ranking_value).get("response"))
            if _text(response.get("questionnaireId")) != "formal_final_v1":
                continue
            ranks: dict[str, int] = {}
            for entry_value in _list(response.get("rankings")):
                entry = _dictionary(entry_value)
                condition = _text(entry.get("conditionCode")).strip().upper()
                rank = _number(entry.get("rank"), -1)
                if condition and rank > 0:
                    ranks[condition] = rank
            preferred = _text(response.get("preferredConditionCode")).strip().upper()
            preferred_label = preferred or "-"
            preferred_task = task_map.get(preferred, "-1") if preferred else "-1"
            reason = _text(response.get("reason")).strip() or "-"
            preference = (
                f"首选条件={preferred_label}；首选taskId={preferred_task}；理由={reason}"
            )
            completed_at = response.get("submittedAtUtc")
            row = [
                participant_id,
                _excel_local_date(completed_at),
                task_mapping,
                *[ranks.get(choice, -1) for choice in choices],
                preference,
            ]
            parsed_completed_at = _parse_timestamp(completed_at)
            selection_key = (
                parsed_completed_at is not None,
                parsed_completed_at or datetime.min.replace(tzinfo=timezone.utc),
                _number(summary.get("createdAtUnixMs")),
                experiment_id,
                ranking_index,
            )
            sort_key = (
                *_timestamp_sort_key(completed_at),
                participant_id,
                task_mapping,
                experiment_id,
            )
            participant_key = participant_id.strip() or f"__experiment__:{experiment_id}"
            current = latest_by_participant.get(participant_key)
            if current is None or selection_key >= current[0]:
                latest_by_participant[participant_key] = (selection_key, sort_key, row)

    ordered_rows = sorted(
        ((value[1], value[2]) for value in latest_by_participant.values()),
        key=lambda value: value[0],
    )
    return headers, [value[1] for value in ordered_rows]


def write_xlsx(path: Path, bundle: Mapping[str, Any]) -> tuple[int, int, int]:
    questionnaires, responses, scores = workbook_rows(bundle)
    formal_scene_headers, formal_scene_rows = formal_scene_statistics(bundle)
    formal_ranking_headers, formal_ranking_rows = formal_ranking_statistics(bundle)
    worksheets = (
        WorksheetSpec("Questionnaires", QUESTIONNAIRE_HEADERS, questionnaires),
        WorksheetSpec("Responses", RESPONSE_HEADERS, responses),
        WorksheetSpec("Scores", SCORE_HEADERS, scores),
        WorksheetSpec(
            "FormalSceneStats",
            formal_scene_headers,
            formal_scene_rows,
            freeze_columns=3,
            wrap_headers=True,
        ),
        WorksheetSpec(
            "FormalRankingStats",
            formal_ranking_headers,
            formal_ranking_rows,
            freeze_columns=3,
            wrap_headers=True,
            max_column_width=120,
        ),
    )
    created = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "[Content_Types].xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Default Extension="xml" ContentType="application/xml"/>'
            '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
            '<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>'
            '<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>'
            + "".join(
                f'<Override PartName="/xl/worksheets/sheet{index}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
                for index in range(1, len(worksheets) + 1)
            )
            + "</Types>",
        )
        archive.writestr(
            "_rels/.rels",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
            '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>'
            '<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>'
            "</Relationships>",
        )
        archive.writestr(
            "docProps/core.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
            'xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" '
            'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">'
            '<dc:creator>SceneTalkVR</dc:creator><cp:lastModifiedBy>SceneTalkVR</cp:lastModifiedBy>'
            f'<dcterms:created xsi:type="dcterms:W3CDTF">{created}</dcterms:created>'
            "</cp:coreProperties>",
        )
        archive.writestr(
            "docProps/app.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" '
            'xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">'
            '<Application>SceneTalkVR</Application></Properties>',
        )
        archive.writestr(
            "xl/workbook.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            '<sheets>'
            + "".join(
                f'<sheet name="{escape(sheet.name)}" sheetId="{index}" r:id="rId{index}"/>'
                for index, sheet in enumerate(worksheets, start=1)
            )
            + "</sheets></workbook>",
        )
        archive.writestr(
            "xl/_rels/workbook.xml.rels",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            + "".join(
                f'<Relationship Id="rId{index}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index}.xml"/>'
                for index in range(1, len(worksheets) + 1)
            )
            + f'<Relationship Id="rId{len(worksheets) + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
            "</Relationships>",
        )
        archive.writestr(
            "xl/styles.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
            '<numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy hh:mm:ss"/></numFmts>'
            '<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font>'
            '<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font></fonts>'
            '<fills count="3"><fill><patternFill patternType="none"/></fill>'
            '<fill><patternFill patternType="gray125"/></fill>'
            '<fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill></fills>'
            '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
            '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
            '<cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>'
            '<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>'
            '<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>'
            '<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1">'
            '<alignment wrapText="1" vertical="center"/></xf></cellXfs>'
            '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>'
            "</styleSheet>",
        )
        for index, sheet in enumerate(worksheets, start=1):
            archive.writestr(
                f"xl/worksheets/sheet{index}.xml",
                _worksheet_xml(
                    sheet.headers,
                    sheet.rows,
                    freeze_columns=sheet.freeze_columns,
                    wrap_headers=sheet.wrap_headers,
                    max_column_width=sheet.max_column_width,
                ),
            )
    return len(questionnaires), len(responses), len(scores)


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _existing_result(final_dir: Path, incoming: Mapping[str, Any]) -> dict[str, Any] | None:
    json_path = final_dir / "experiment_history.json"
    xlsx_path = final_dir / "questionnaire_records.xlsx"
    if not json_path.is_file() or not xlsx_path.is_file():
        return None
    try:
        existing = json.loads(json_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if canonical_json(existing) != canonical_json(incoming):
        raise ExportError(
            "export_id_conflict",
            "The exportId already exists with different content.",
            HTTPStatus.CONFLICT,
        )
    return build_success_response(final_dir, existing, json_path, xlsx_path)


def build_success_response(
    final_dir: Path,
    bundle: Mapping[str, Any],
    json_path: Path,
    xlsx_path: Path,
) -> dict[str, Any]:
    questionnaire_count = 0
    response_count = 0
    for experiment in _list(bundle.get("experiments")):
        for questionnaire in _list(_dictionary(experiment).get("questionnaires")):
            questionnaire_count += 1
            response_count += len(
                _list(_dictionary(_dictionary(questionnaire).get("session")).get("responses"))
            )
    return {
        "status": "ok",
        "exportId": _text(bundle.get("exportId")),
        "exportDirectory": str(final_dir.resolve()),
        "jsonFile": json_path.name,
        "excelFile": xlsx_path.name,
        "experimentCount": len(_list(bundle.get("experiments"))),
        "questionnaireCount": questionnaire_count,
        "responseCount": response_count,
        "warningCount": len(_list(bundle.get("warnings"))),
        "jsonSha256": _file_sha256(json_path),
        "excelSha256": _file_sha256(xlsx_path),
    }


def write_export(config: ReceiverConfig, payload: Any) -> dict[str, Any]:
    bundle = validate_and_sort_bundle(payload)
    export_id = _text(bundle.get("exportId"))
    folder_name = export_folder_name(_text(bundle.get("exportedAtUtc")), export_id)
    root = config.export_dir.resolve()
    root.mkdir(parents=True, exist_ok=True)
    final_dir = root / folder_name
    if final_dir.exists():
        existing = _existing_result(final_dir, bundle)
        if existing is not None:
            return existing
        raise ExportError(
            "incomplete_existing_export",
            "The target export directory already exists but is incomplete.",
            HTTPStatus.CONFLICT,
        )

    temporary_dir = Path(tempfile.mkdtemp(prefix=f".{folder_name}.", dir=root))
    json_path = temporary_dir / "experiment_history.json"
    xlsx_path = temporary_dir / "questionnaire_records.xlsx"
    try:
        json_path.write_text(
            json.dumps(bundle, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        write_xlsx(xlsx_path, bundle)
        os.replace(temporary_dir, final_dir)
    except ExportError:
        shutil.rmtree(temporary_dir, ignore_errors=True)
        raise
    except OSError as error:
        shutil.rmtree(temporary_dir, ignore_errors=True)
        raise ExportError(
            "export_write_failed", f"Failed to write export files: {error}", HTTPStatus.INTERNAL_SERVER_ERROR
        ) from error
    except Exception as error:
        shutil.rmtree(temporary_dir, ignore_errors=True)
        raise ExportError(
            "xlsx_generation_failed", f"Failed to create Excel workbook: {error}", HTTPStatus.INTERNAL_SERVER_ERROR
        ) from error

    return build_success_response(
        final_dir,
        bundle,
        final_dir / json_path.name,
        final_dir / xlsx_path.name,
    )


class HistoryExportHandler(BaseHTTPRequestHandler):
    receiver_config: ReceiverConfig

    def do_GET(self) -> None:
        if urlparse(self.path).path != "/health":
            self._send_error("not_found", "Unknown route.", HTTPStatus.NOT_FOUND)
            return
        self._send_json(
            {
                "status": "ok",
                "service": "history-export",
                "schemaVersion": SCHEMA_VERSION,
            }
        )

    def do_POST(self) -> None:
        if urlparse(self.path).path != "/api/history/export":
            self._send_error("not_found", "Unknown route.", HTTPStatus.NOT_FOUND)
            return
        if "application/json" not in self.headers.get("Content-Type", "").lower():
            self._send_error(
                "unsupported_content_type", "Use application/json.", HTTPStatus.UNSUPPORTED_MEDIA_TYPE
            )
            return
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            length = -1
        if length < 0:
            self._send_error("content_length_required", "Content-Length is required.", HTTPStatus.LENGTH_REQUIRED)
            return
        if length > self.receiver_config.max_body_bytes:
            self._send_error("payload_too_large", "History export payload is too large.", HTTPStatus.REQUEST_ENTITY_TOO_LARGE)
            return
        raw = self.rfile.read(length)
        try:
            payload = json.loads(raw.decode("utf-8"))
            result = write_export(self.receiver_config, payload)
        except UnicodeDecodeError:
            self._send_error("invalid_encoding", "JSON must use UTF-8.", HTTPStatus.BAD_REQUEST)
            return
        except json.JSONDecodeError as error:
            self._send_error("invalid_json", f"Invalid JSON: {error.msg}.", HTTPStatus.BAD_REQUEST)
            return
        except ExportError as error:
            self._send_error(error.code, str(error), error.status)
            return
        self._send_json(result)

    def log_message(self, format: str, *args: Any) -> None:
        try:
            print(f"[history-export] {self.address_string()} {format % args}")
        except (BrokenPipeError, OSError, ValueError):
            pass

    def _send_error(self, code: str, message: str, status: HTTPStatus) -> None:
        self._send_json({"status": "error", "errorCode": code, "message": message}, status)

    def _send_json(self, payload: Mapping[str, Any], status: HTTPStatus = HTTPStatus.OK) -> None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass


def run_server(config: ReceiverConfig) -> None:
    class RequestHandler(HistoryExportHandler):
        receiver_config = config

    server = ThreadingHTTPServer((config.host, config.port), RequestHandler)
    print(
        f"SceneTalkVR history export receiver listening on http://{config.host}:{config.port} "
        f"(exportDir={config.export_dir.resolve()})",
        flush=True,
    )
    server.serve_forever()


def main() -> None:
    run_server(ReceiverConfig.from_env())


if __name__ == "__main__":
    main()
