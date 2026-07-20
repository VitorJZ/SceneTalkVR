from __future__ import annotations

from typing import Any


def markdown_qc(summary: dict[str, Any]) -> str:
    lines=["# SceneTalkVR Analysis QC Report","","This report contains no transcript, open response, or interview text.",""]
    for key,value in summary.items(): lines.append(f"- **{key}**: `{value}`")
    return "\n".join(lines)+"\n"
