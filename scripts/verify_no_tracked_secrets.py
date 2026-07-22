#!/usr/bin/env python3
"""Fail when tracked worktree files contain high-confidence credential material."""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MAX_FILE_BYTES = 32 * 1024 * 1024
TEXT_SUFFIXES = {
    ".asmdef", ".asmref", ".asset", ".cfg", ".config", ".cs", ".csv", ".env",
    ".gradle", ".html", ".ini", ".java", ".js", ".json", ".kt", ".md", ".meta",
    ".prefab", ".properties", ".ps1", ".py", ".sh", ".toml", ".ts", ".txt", ".unity",
    ".uss", ".uxml", ".xml", ".yaml", ".yml",
}
PATTERNS = (
    ("api_token", re.compile(rb"(?<![A-Za-z0-9_-])sk-[A-Za-z0-9_-]{20,}")),
    (
        "private_key",
        re.compile(rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    ),
    (
        "credential_assignment",
        re.compile(
            rb"(?i)(?:api[_-]?key|client[_-]?secret|access[_-]?token)"
            rb"\s*[:=]\s*[\"'][A-Za-z0-9._-]{20,}[\"']"
        ),
    ),
    (
        "jwt",
        re.compile(rb"(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}"),
    ),
)


def tracked_files() -> list[Path]:
    result = subprocess.run(
        ["git", "-C", str(ROOT), "ls-files", "-z"],
        check=True,
        stdout=subprocess.PIPE,
    )
    return [ROOT / item.decode("utf-8", errors="surrogateescape") for item in result.stdout.split(b"\0") if item]


def main() -> int:
    findings: list[tuple[str, int, str]] = []
    scanned = 0
    skipped_large = 0
    skipped_binary = 0
    for path in tracked_files():
        if not path.is_file():
            continue
        if path.suffix.lower() not in TEXT_SUFFIXES and path.name.lower() != ".env":
            skipped_binary += 1
            continue
        size = path.stat().st_size
        if size > MAX_FILE_BYTES:
            skipped_large += 1
            continue
        data = path.read_bytes()
        scanned += 1
        for label, pattern in PATTERNS:
            for match in pattern.finditer(data):
                line = data.count(b"\n", 0, match.start()) + 1
                findings.append((path.relative_to(ROOT).as_posix(), line, label))

    if findings:
        print("FAIL: high-confidence credential material exists in tracked worktree files.")
        for path, line, label in findings:
            print(f"{path}:{line}: {label}")
        print("Credential values are intentionally not printed.")
        return 1

    print(
        f"PASS: scanned {scanned} tracked text files; skipped {skipped_binary} non-text files "
        f"and {skipped_large} files larger than {MAX_FILE_BYTES} bytes."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
