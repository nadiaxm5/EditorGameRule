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

    report_path = evidence_directory.parent / "equivalence-report.json"
    if report_path.is_file():
        with report_path.open("r", encoding="utf-8") as handle:
            report: dict[str, Any] = json.load(handle)
        evidence_summary = report.get("evidence", {})
        actual_manifest_sha256 = file_sha256(manifest_path)
        if evidence_summary.get("manifestSha256") != actual_manifest_sha256:
            failures.append("Manifest SHA-256 does not match the machine-readable report")
        if evidence_summary.get("artifactCount") != len(artifacts):
            failures.append("Manifest artifact count does not match the machine-readable report")
        if manifest.get("overallPass") != report.get("overallPass"):
            failures.append("Overall result differs between the manifest and report")

        hash_pairs = (
            ("canonicalJson", "sourceCanonicalSha256", "outputCanonicalSha256"),
            ("parsedAst", "sourceParsedAstSha256", "outputParsedAstSha256"),
            ("generatedCSharp", "sourceGeneratedCSharpSha256", "outputGeneratedCSharpSha256"),
        )
        for result in report.get("controlledCases", []) + report.get("integrationCases", []):
            for check, source_hash, output_hash in hash_pairs:
                if result.get(check) and result.get(source_hash) != result.get(output_hash):
                    failures.append(f"Paired {check} hashes differ for {result.get('name')}")

    if failures:
        print("Evidence verification: FAIL")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print(f"Evidence verification: PASS ({len(artifacts)} indexed artifacts)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
