"""Read-only raw inputs -> separately versioned summaries, retaining failed/missing runs."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = ["read_process_persist_ms", "visual_rebuild_ms", "export_ms", "total_ms"]
ROBUST_COLUMNS = ["case_id", "invalid_category", "mutation", "detected", "detection_stage", "message",
                 "user_visible_diagnostic", "import_rejected", "accepted_silently", "unhandled_exception",
                 "previous_state_preserved", "partial_or_inconsistent_state", "notes"]


def quartiles(values):
    """R/type-7 linear interpolation: with n=5 Q1=x[1], Q2=x[2], Q3=x[3]."""
    if len(values) != 5:
        return {k: None for k in ("median", "q1", "q3", "iqr")}
    x = sorted(values)
    return dict(median=x[2], q1=x[1], q3=x[3], iqr=x[3] - x[1])


def dump_csv(path, records, columns):
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, columns, extrasaction="ignore")
        writer.writeheader()
        for r in records:
            writer.writerow({k: json.dumps(v, ensure_ascii=False) if isinstance(v, (list, dict)) else v for k, v in r.items()})


def summarize(run_id, root=ROOT):
    run = root / "runs" / run_id
    manifest = json.loads((run / "inputs.json" if (run / "inputs.json").exists() else root / "inputs.json").read_text(encoding="utf-8-sig"))
    raw = []
    hashes = {}
    for kind in ("robustness", "scale"):
        for path in sorted((root / kind / "raw-results" / run_id).glob("*.json")):
            raw.append(json.loads(path.read_text(encoding="utf-8-sig")))
            hashes[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    launcher = json.loads((run / "launcher.json").read_text(encoding="utf-8-sig")) if (run / "launcher.json").exists() else {}
    # A hung process cannot write its result: preserve its last sample as a censored observation.
    sample = launcher.get("last_sample")
    if launcher.get("outcome") == "sample_timeout" and sample and not any(
        all(r.get(k) == sample.get(k) for k in ("kind", "case_id", "repetition")) for r in raw
    ):
        raw.append(dict(sample, status="external_timeout", failure_stage=launcher.get("last_stage")))
    robust = []
    for spec in manifest["cases"]:
        record = next((r for r in raw if r["kind"] == "robustness" and r["case_id"] == spec["case_id"]), None)
        r = {k: None for k in ROBUST_COLUMNS}
        r.update(spec)
        r.update({k: v for k, v in (record or {}).items() if k in ROBUST_COLUMNS})
        r["status"] = record.get("status") if record else "pending"
        r["notes"] = (record or {}).get("notes", "No completed Unity observation; no behavior inferred.")
        r["explicit_validation_messages"] = (record or {}).get("explicit_validation_messages")
        r["acceptance"] = (record or {}).get("acceptance")
        r["previous_asset_content_preserved"] = (record or {}).get("previous_asset_content_preserved")
        robust.append(r)
    measurements, summaries = [], []
    for spec in manifest["scale"]:
        samples = [r for r in raw if r["kind"] == "scale" and r["case_id"] == spec["case_id"] and not r.get("warmup")]
        for op in OPERATIONS:
            values = []
            statuses = []
            for rep in range(1, 6):
                r = next((r for r in samples if r["repetition"] == rep), {})
                v = r.get(op)
                values.append(v)
                statuses.append(r.get("status", "pending"))
                measurements.append(dict(case_id=spec["case_id"], operation=op, repetition=rep, value_ms=v,
                                         status=r.get("status", "pending"), correctness=r.get("correctness", {}).get("all")))
            # Never silently drop failed or missing samples. A partial group has no five-observation summary.
            complete = all(s == "completed" for s in statuses) and all(isinstance(v, (float, int)) for v in values)
            summaries.append(dict(case_id=spec["case_id"], operation=op, values_ms=values, statuses=statuses,
                                  summary_status="complete" if complete else "incomplete",
                                  **quartiles(values if complete else [])))
    report = {"run_id": run_id, "launcher": launcher, "raw_sha256": hashes,
              "robustness": robust, "scale_measurements": measurements, "scale_summary": summaries,
              "quartile_method": "type 7; n=5: Q1=x(2), median=x(3), Q3=x(4), IQR=x(4)-x(2)",
              "manual_long_panel": "pending; see manual-panel-checklist.md"}
    out = run / "summary"
    out.mkdir(exist_ok=True)
    (out / "results.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    dump_csv(out / "robustness.csv", robust, ROBUST_COLUMNS + ["status", "acceptance", "explicit_validation_messages", "previous_asset_content_preserved"])
    dump_csv(out / "scale-raw.csv", measurements, ["case_id", "operation", "repetition", "value_ms", "status", "correctness"])
    dump_csv(out / "scale-statistics.csv", summaries, ["case_id", "operation", "values_ms", "statuses", "summary_status", "median", "q1", "q3", "iqr"])
    lines = [f"# Observations: {run_id}", "", f"Launcher: `{launcher.get('outcome', 'menu run')}`.",
             "Null/— means unmeasured, never zero or PASS. Production generation/runtime and manual interaction are not evaluated.", "",
             "| Case | Status | Detected | Rejected | Prior active state preserved | Message |", "|---|---|---|---|---|---|"]
    for r in robust:
        lines.append("| " + " | ".join(str(r.get(k)) if r.get(k) is not None else "—" for k in
                     ["case_id", "status", "detected", "import_rejected", "previous_state_preserved", "message"]).replace("\n", "<br>") + " |")
    lines += ["", "| Configuration | Operation (ms) | R1 | R2 | R3 | R4 | R5 | Median | Q1 | Q3 | IQR |", "|---|---|---|---|---|---|---|---|---|---|---|"]
    for s in summaries:
        vals = [s["case_id"], s["operation"], *s["values_ms"], *[s[k] for k in ("median", "q1", "q3", "iqr")]]
        lines.append("| " + " | ".join("—" if v is None else str(v) for v in vals) + " |")
    lines += ["", report["quartile_method"], "", "Failures/timeouts remain in CSV/JSON with status; incomplete groups have no statistics.",
              "Correctness details, exact exceptions, before/after snapshots and UI observations are in the immutable raw files listed by SHA-256 in results.json."]
    (out / "report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("run_id")
    args = p.parse_args()
    summarize(args.run_id)
