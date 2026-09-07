"""Verify the supplied manual export; append evidence hashes without altering automatic records."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCREENSHOTS = {
    "manual-a1-rule000.png": "CapturaRule000",
    "manual-a1-rule099.png": "CapturaRule099",
    "manual-unsupported-action.png": "CapturaUnsupportedAction",
    "manual-a200-rule019.png": "CapturaEvalActor199Rule019",
    "manual-a200-hierarchy.png": "CapturaEvalActor199Jerarquia",
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify(run_id):
    if Path(run_id).name != run_id or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in run_id):
        raise ValueError("Invalid run id")
    run = ROOT / "runs" / run_id
    export = run / "manual-a1-r100-export.json"
    reference = run / "scale-a1_r100-5-export.json"
    descriptor = ROOT / "scale/descriptors/a1_r100.json"
    data = json.loads(export.read_text(encoding="utf-8-sig"))
    expected = json.loads(descriptor.read_text(encoding="utf-8-sig"))
    automatic = json.loads(reference.read_text(encoding="utf-8-sig"))
    actors = data.get("Cast") or []
    rules = actors[0].get("Script", []) if len(actors) == 1 else []
    names = [r.get("Name") for r in rules]
    checks = {
        "game_name": data.get("GameName") == "GR_Eval_a1_r100",
        "single_actor": len(actors) == 1,
        "actor_identifier": len(actors) == 1 and actors[0].get("ActorName") == "EvalActor000",
        "exactly_100_rules": len(rules) == 100,
        "consecutive_names_and_full_order": names == [f"Rule{i:03d}" for i in range(100)],
        "no_duplicate_rule_names": len(names) == 100 and len(set(names)) == 100,
        "first_action": bool(rules) and rules[0].get("Name") == "Rule000" and rules[0].get("Do") == ["Edit(EvalActor000.Health,1)"],
        "last_action": bool(rules) and rules[-1].get("Name") == "Rule099" and rules[-1].get("Do") == ["Edit(EvalActor000.Health,100)"],
        "all_conditions": len(rules) == 100 and all(r.get("When") == ["Compare(this.Health > 0)"] for r in rules),
        "all_ordered_rule_content_matches_descriptor": rules == expected["Cast"][0]["Script"],
        # Dict equality ignores key order, list equality preserves array order; numeric int/float equivalents compare equal.
        "full_structural_equality_with_automatic_export": data == automatic,
    }
    result = {
        "run_id": run_id,
        "scope": "Programmatic file verification, not a repeated manual interaction or runtime test",
        "export": export.name, "export_sha256": digest(export),
        "descriptor": descriptor.relative_to(ROOT).as_posix(), "descriptor_sha256": digest(descriptor),
        "automatic_reference": reference.name, "automatic_reference_sha256": digest(reference),
        "checks": checks, "all_checks_satisfied": all(checks.values()),
        "observed": {"game_name": data.get("GameName"), "actor_names": [a.get("ActorName") for a in actors],
                     "rule_count": len(rules), "rule_names_in_order": names,
                     "first_rule": rules[0] if rules else None, "last_rule": rules[-1] if rules else None},
    }
    verification = run / "manual-export-verification.json"
    verification.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    files = [run / "manual-panel-result.md", export, verification]
    screenshots = []
    for name, source in SCREENSHOTS.items():
        path = run / name
        if path.exists():
            if not path.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"):
                raise ValueError(f"Not a PNG: {name}")
            files.append(path)
        screenshots.append(dict(file=name, source_label=source, available=path.exists(),
                                treatment="Original bytes; no editing" if path.exists() else None))
    manifest_path = run / "inputs.json"
    # Preserve automatic keys and their values. Only the optional manual section is added/refreshed.
    original_manifest = manifest_path.read_bytes()
    manifest = json.loads(original_manifest.decode("utf-8-sig"))
    manifest["manual_evidence"] = {
        "observation_source": "User-reported manual observations, supplied PNGs and exported JSON",
        "manual_observation_utc": None, "window_dimensions": None,
        "unity": "6000.3.1f1", "windows_display_scale_percent": 150,
        "mode": "Edit mode; no scene generation or runtime",
        "result": (run / "manual-panel-result.md").relative_to(ROOT).as_posix(),
        "verification": verification.relative_to(ROOT).as_posix(),
        "screenshots": screenshots,
        "sha256": {p.relative_to(ROOT).as_posix(): digest(p) for p in files},
    }
    encoded = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    if b"\r\n" in original_manifest:
        encoded = encoded.replace("\n", "\r\n")
    manifest_path.write_bytes(encoded.encode("utf-8"))
    print(json.dumps({"checks": checks, "manual_hashed_files": len(files), "available_screenshots": sum(s["available"] for s in screenshots)}, indent=2))
    return 0 if result["all_checks_satisfied"] else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("run_id")
    raise SystemExit(verify(parser.parse_args().run_id))
