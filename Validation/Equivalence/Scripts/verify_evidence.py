#!/usr/bin/env python3
"""Verify the size and SHA-256 digest of every retained evidence artifact."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--evidence-directory",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "Results" / "Evidence",
        help="Directory containing evidence-manifest.json and its indexed artifacts.",
    )
    args = parser.parse_args()

    evidence_directory = args.evidence_directory.resolve()
    manifest_path = evidence_directory / "evidence-manifest.json"
    with manifest_path.open("r", encoding="utf-8") as handle:
        manifest: dict[str, Any] = json.load(handle)

    failures: list[str] = []
    artifacts = manifest.get("artifacts", [])
    for artifact in artifacts:
        relative = Path(artifact["path"])
        path = (evidence_directory / relative).resolve()
        try:
            path.relative_to(evidence_directory)
        except ValueError:
            failures.append(f"Path escapes evidence directory: {relative}")
            continue

        if not path.is_file():
            failures.append(f"Missing: {relative}")
            continue
        actual_bytes = path.stat().st_size
        actual_sha256 = file_sha256(path)
        if actual_bytes != artifact["bytes"]:
            failures.append(
                f"Size mismatch: {relative} (expected {artifact['bytes']}, got {actual_bytes})"
            )
        if actual_sha256 != artifact["sha256"]:
            failures.append(
                f"SHA-256 mismatch: {relative} "
                f"(expected {artifact['sha256']}, got {actual_sha256})"
            )

    indexed_paths = {artifact["path"] for artifact in artifacts}
    actual_paths = {
        path.relative_to(evidence_directory).as_posix()
        for path in evidence_directory.rglob("*")
        if path.is_file() and path != manifest_path
    }
    for relative in sorted(actual_paths - indexed_paths):
        failures.append(f"Unindexed artifact: {relative}")

    if failures:
        print("Evidence verification: FAIL")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print(f"Evidence verification: PASS ({len(artifacts)} indexed artifacts)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
