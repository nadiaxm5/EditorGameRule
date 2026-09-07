"""Deterministic fixtures from SceneJson/ActorJson/SentenceJson; no Unity simulation."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIGS = [(10, 10), (50, 20), (100, 20), (200, 20), (1, 100)]


def descriptor(actors, rules, name):
    return {
        "GameName": name, "ScreenResolution": [1920, 1080],
        "CameraPosition": [0, 1, -10], "CameraRotation": [0, 0, 0],
        "SunPosition": [0, 3, 0], "SunRotation": [50, -30, 0],
        "SunColor": [255, 255, 255], "SunAmbientColor": [128, 128, 128],
        "BackgroundColor": [0, 0, 0], "Gravity": [0, -9.81, 0],
        "CustomVariables": [],
        "Cast": [{
            "ActorName": f"EvalActor{i:03d}", "Active": True,
            "PrefabName": "Empty", "Tag": "Untagged",
            "Properties": ["Health=100"],
            "Components": [{"type": "Rules", "name": "Evaluation rules", "id": f"rules{i:03d}"}],
            "Script": [{"Name": f"Rule{j:03d}", "groupId": f"rules{i:03d}",
                        "When": ["Compare(this.Health > 0)"],
                        "Do": [f"Edit(EvalActor{i:03d}.Health,{j + 1})"]}
                       for j in range(rules)]
        } for i in range(actors)]
    }


def encode(value):
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def fixtures():
    base = descriptor(2, 2, "GR_Eval_Baseline")
    values = {"robustness/baseline/baseline.json": encode(base)}
    specs = [
        ("malformed_json", "Parsing JSON", "Remove final root closing brace", "$", "Invalid JSON syntax", None),
        ("missing_required", "Required field", "Remove GameName", "$.GameName", "GameRuleProject.Validate requires a nonempty GameName; import does not call Validate", lambda d: d.pop("GameName")),
        ("missing_actor", "Actor reference", "EvalActor000.Health -> MissingActor.Health", "$.Cast[0].Script[0].Do[0]", "MissingActor is absent from Cast; Edit targets an actor property", lambda d: d["Cast"][0]["Script"][0].update(Do=["Edit(MissingActor.Health,1)"])),
        ("missing_property", "Property reference", "Health -> MissingProperty in Edit target", "$.Cast[0].Script[0].Do[0]", "MissingProperty is neither a declared nor a built-in actor property", lambda d: d["Cast"][0]["Script"][0].update(Do=["Edit(EvalActor000.MissingProperty,1)"])),
        ("unsupported_action", "Action", "Edit -> NoSuchAction", "$.Cast[0].Script[0].Do[0]", "NoSuchAction has no public static method in Action", lambda d: d["Cast"][0]["Script"][0].update(Do=["NoSuchAction(EvalActor000.Health,1)"])),
        ("invalid_parameter", "Parameter/type", "Numeric expression 1 -> )", "$.Cast[0].Script[0].Do[0]", "An unmatched closing parenthesis cannot be a numeric expression for Action.Edit", lambda d: d["Cast"][0]["Script"][0].update(Do=["Edit(EvalActor000.Health,))"])),
    ]
    cases = []
    for case_id, category, mutation, field, reason, mutate in specs:
        data = copy.deepcopy(base)
        if mutate:
            mutate(data)
        path = f"robustness/invalid-inputs/{case_id}.json"
        values[path] = encode(data) if mutate else encode(data).rstrip()[:-1] + "\n"
        cases.append(dict(case_id=case_id, invalid_category=category, mutation=mutation,
                          field=field, reason=reason, file=path))
    for a, r in CONFIGS:
        values[f"scale/descriptors/a{a}_r{r}.json"] = encode(descriptor(a, r, f"GR_Eval_a{a}_r{r}"))
    return values, cases


def generate(root=ROOT):
    values, cases = fixtures()
    for relative, content in values.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content.encode("utf-8"))
    manifest = {"generator": "scale/generator/generate.py", "randomness": "none",
                "cases": cases, "scale": [{"case_id": f"a{a}_r{r}", "actors": a, "rules_per_actor": r,
                "file": f"scale/descriptors/a{a}_r{r}.json"} for a, r in CONFIGS],
                "sha256": {p: hashlib.sha256(v.encode()).hexdigest() for p, v in values.items()}}
    (root / "inputs.json").write_bytes(encode(manifest).encode("utf-8"))
    return manifest


if __name__ == "__main__":
    generate()
