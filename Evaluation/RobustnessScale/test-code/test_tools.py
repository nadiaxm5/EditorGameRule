"""Offline tests verify fixture isolation, identity/order, accounting and traceability only."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


generator = module("generator", ROOT / "scale/generator/generate.py")
summary = module("summary", ROOT / "test-code/summarize.py")


def diff(a, b, path="$"):
    if isinstance(a, dict) and isinstance(b, dict):
        return [p for k in a.keys() | b.keys() for p in
                ([f"{path}.{k}"] if k not in a or k not in b else diff(a[k], b[k], f"{path}.{k}"))]
    if isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            return [path]
        return [p for i, (x, y) in enumerate(zip(a, b)) for p in diff(x, y, f"{path}[{i}]")]
    return [] if a == b else [path]


class ToolTests(unittest.TestCase):
    def test_single_mutation(self):
        values, cases = generator.fixtures()
        base = json.loads(values["robustness/baseline/baseline.json"])
        for spec in cases:
            if spec["case_id"] == "malformed_json":
                with self.assertRaises(json.JSONDecodeError):
                    json.loads(values[spec["file"]])
                self.assertEqual(json.loads(values[spec["file"]].rstrip() + "}"), base)
            else:
                self.assertEqual(diff(base, json.loads(values[spec["file"]])), [spec["field"]])

    def test_scale_counts_order_and_references(self):
        values, _ = generator.fixtures()
        for a, r in generator.CONFIGS:
            d = json.loads(values[f"scale/descriptors/a{a}_r{r}.json"])
            self.assertEqual([x["ActorName"] for x in d["Cast"]], [f"EvalActor{i:03d}" for i in range(a)])
            self.assertEqual(sum(len(x["Script"]) for x in d["Cast"]), a * r)
            for i, actor in enumerate(d["Cast"]):
                self.assertEqual([x["Name"] for x in actor["Script"]], [f"Rule{j:03d}" for j in range(r)])
                for j, rule in enumerate(actor["Script"]):
                    self.assertEqual(rule["groupId"], actor["Components"][0]["id"])
                    self.assertEqual(rule["Do"], [f"Edit(EvalActor{i:03d}.Health,{j+1})"])

    def test_committed_fixtures_reproducible(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = generator.generate(root)
            for p in [*manifest["sha256"], "inputs.json"]:
                self.assertEqual((root / p).read_bytes(), (ROOT / p).read_bytes(), p)

    def test_quartiles(self):
        self.assertEqual(summary.quartiles([100, 1, 4, 2, 3]), dict(median=3, q1=2, q3=4, iqr=2))
        self.assertIsNone(summary.quartiles([1, 2, 3, 4])["median"])

    def test_pending_is_not_success(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            generator.generate(root)
            (root / "runs/test").mkdir(parents=True)
            result = summary.summarize("test", root)
            self.assertTrue(all(r["detected"] is None and r["status"] == "pending" for r in result["robustness"]))
            self.assertEqual(len(result["scale_measurements"]), 100)
            self.assertTrue(all(r["median"] is None for r in result["scale_summary"]))

    def test_timeout_and_warmup_not_silently_dropped(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            generator.generate(root)
            run = root / "runs/test"
            run.mkdir(parents=True)
            raw = root / "scale/raw-results/test"
            raw.mkdir(parents=True)
            for rep in range(5):
                record = dict(kind="scale", case_id="a10_r10", repetition=rep, warmup=rep == 0,
                              status="completed", correctness={"all": True}, **{op: 999 if rep == 0 else rep for op in summary.OPERATIONS})
                (raw / f"{rep}.json").write_text(json.dumps(record))
            (run / "launcher.json").write_text(json.dumps(dict(outcome="sample_timeout", last_sample=dict(
                kind="scale", case_id="a10_r10", repetition=5, warmup=False), last_stage="visual")))
            before = {p.name: p.read_bytes() for p in raw.glob("*.json")}
            result = summary.summarize("test", root)
            first = result["scale_summary"][0]
            self.assertEqual(first["values_ms"], [1, 2, 3, 4, None])
            self.assertEqual(first["statuses"][-1], "external_timeout")
            self.assertIsNone(first["median"])
            self.assertEqual(before, {p.name: p.read_bytes() for p in raw.glob("*.json")})


if __name__ == "__main__":
    unittest.main(verbosity=2)
