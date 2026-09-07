"""Audit result/input provenance and accounting; reports product failures without relabeling them."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def audit(run_id):
    run = ROOT / "runs" / run_id
    manifest = json.loads((run / "inputs.json").read_text(encoding="utf-8-sig"))
    summary = json.loads((run / "summary/results.json").read_text(encoding="utf-8-sig"))
    errors = []
    observations = []
    for path, digest in manifest["sha256"].items():
        if hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest:
            errors.append("Input hash mismatch: " + path)
    for relative, digest in summary["raw_sha256"].items():
        path = ROOT / relative
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            errors.append("Raw result modified since summary: " + relative)
        record = json.loads(path.read_text(encoding="utf-8-sig"))
        rel_input = str(Path(record["input"]).relative_to("Evaluation/RobustnessScale")).replace("\\", "/")
        if record["input_sha256"] != manifest["sha256"][rel_input]:
            errors.append("Raw result/input hash mismatch: " + relative)
        observations.append(record)
    keys = [(r["kind"], r["case_id"], r["repetition"]) for r in observations]
    if len(keys) != len(set(keys)):
        errors.append("Duplicate sample identity")
    expected = [("baseline", "baseline", -1)]
    expected += [("robustness", s["case_id"], 1) for s in manifest["cases"]]
    expected += [("scale", s["case_id"], rep) for s in manifest["scale"] for rep in range(6)]
    missing = [k for k in expected if k not in keys]
    result = dict(run_id=run_id, provenance_errors=errors, observed=len(keys), expected=len(expected),
                  missing=missing, sample_statuses=[dict(kind=r["kind"], case_id=r["case_id"],
                  repetition=r["repetition"], status=r["status"], correctness=r.get("correctness", {}).get("all")) for r in observations],
                  interpretation="Correctness=false on invalid inputs is an observation, not an audit failure. Missing observations remain pending.")
    (run / "summary/audit.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "sample_statuses"}, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("run_id")
    raise SystemExit(audit(parser.parse_args().run_id))
