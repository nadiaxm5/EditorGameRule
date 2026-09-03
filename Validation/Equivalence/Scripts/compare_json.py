#!/usr/bin/env python3
"""Independent canonical JSON check for the three full-game descriptor pairs."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


SCENE_DEFAULTS: dict[str, Any] = {
    "ScreenResolution": [1920, 1080],
    "CameraPosition": [0, 1, -10],
    "CameraRotation": [0, 0, 0],
    "SunPosition": [0, 3, 0],
    "SunRotation": [50, -30, 0],
    "SunColor": [255, 255, 255],
    "SunAmbientColor": [128, 128, 128],
    "BackgroundColor": [0, 0, 0],
    "Gravity": [0, -9.81, 0],
    "CustomVariables": [],
}

PAIRS = (
    ("Tanks", "TanksGameRule.json", "TANKS.json"),
    ("Survival Shooter", "SurvivalShooterGameRule.json", "SURVIVAL_SHOOTER.json"),
    ("John Lemon", "JohnLemonGameRule.json", "JHON_LEMON.json"),
)


def normalize_numbers(value: Any) -> Any:
    if isinstance(value, dict):
        return {key: normalize_numbers(item) for key, item in value.items()}
    if isinstance(value, list):
        return [normalize_numbers(item) for item in value]
    if isinstance(value, float):
        rounded = round(value, 6)
        return int(rounded) if rounded.is_integer() else rounded
    return value


def remove_if_default(owner: dict[str, Any], key: str, default: Any) -> None:
    if owner.get(key) == default:
        owner.pop(key, None)


def canonical_value(descriptor: dict[str, Any]) -> dict[str, Any]:
    descriptor = normalize_numbers(descriptor)

    for key, default in SCENE_DEFAULTS.items():
        remove_if_default(descriptor, key, default)

    for actor in descriptor.get("Cast", []):
        remove_if_default(actor, "Active", True)
        remove_if_default(actor, "Tag", "")
        remove_if_default(actor, "IconColorHex", "")
        remove_if_default(actor, "Properties", [])
        remove_if_default(actor, "Components", [])
        remove_if_default(actor, "Script", [])

        for rule in actor.get("Script", []):
            remove_if_default(rule, "Name", "")
            remove_if_default(rule, "groupId", "")
            remove_if_default(rule, "When", [])
            remove_if_default(rule, "Do", [])

    return descriptor


def canonical_text(path: Path) -> str:
    with path.open("r", encoding="utf-8-sig") as handle:
        descriptor = json.load(handle)
    normalized = canonical_value(descriptor)
    return json.dumps(normalized, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sha256(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--project-root",
        type=Path,
        default=Path(__file__).resolve().parents[3],
        help="Root of the EditorGameRule Unity project.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional JSON output path.",
    )
    args = parser.parse_args()

    games = args.project_root / "Assets" / "Resources" / "Games"
    results: list[dict[str, Any]] = []

    for name, manual_name, studio_name in PAIRS:
        manual = canonical_text(games / manual_name)
        studio = canonical_text(games / studio_name)
        results.append(
            {
                "name": name,
                "pass": manual == studio,
                "manualCanonicalSha256": sha256(manual),
                "studioCanonicalSha256": sha256(studio),
            }
        )

    payload = {"allPass": all(item["pass"] for item in results), "pairs": results}
    rendered = json.dumps(payload, ensure_ascii=False, indent=2)
    print(rendered)

    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")

    return 0 if payload["allPass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
