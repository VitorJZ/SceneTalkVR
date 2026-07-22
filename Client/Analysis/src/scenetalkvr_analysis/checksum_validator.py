from __future__ import annotations

import hashlib
from pathlib import Path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_checksums(root: Path) -> tuple[dict[str, str], list[str]]:
    checksum_file = root / "checksums.sha256"
    if not checksum_file.is_file():
        return {}, ["checksum_file_missing"]
    hashes: dict[str, str] = {}
    errors: list[str] = []
    for number, line in enumerate(checksum_file.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        parts = line.split("  ", 1)
        if len(parts) != 2:
            errors.append(f"checksum_format:{number}")
            continue
        expected, relative = parts
        target = root / Path(relative)
        if not target.is_file():
            errors.append(f"checksum_target_missing:{relative}")
            continue
        observed = sha256_file(target)
        hashes[relative.replace("\\", "/")] = observed
        if observed.lower() != expected.lower():
            errors.append(f"checksum_failure:{relative}")
    return hashes, errors
