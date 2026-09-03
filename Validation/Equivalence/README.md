# GameRule Studio equivalence validation

This package implements the reproducible equivalence checks used for the revised evaluation of GameRule Studio. It targets Unity 6.3 LTS (`6000.3.1f1`) and the `EditorDeterminista` source snapshot identified by commit `c58f222091dbb642797006045ded9c226309fc3f`.

## Scope

The primary coverage matrix concerns the six formal GameRule conditions and fourteen formal GameRule actions described in the architecture article. The three full-game descriptor pairs are compared without filtering their contents; auxiliary project-control commands already present in those descriptors are preserved, but they are not counted as part of the formal action repertoire.

The validation does not claim equality of configured Unity scenes or complete gameplay outcomes. It establishes descriptor, parsed-structure, generated-source, round-trip, and targeted execution properties under the stated oracles.

## Controlled cases

| Case | Main coverage |
|---|---|
| E01 | Compare, Check, Edit, local/global state, conditional and unconditional rules |
| E02 | Keyboard, Touch, Move, MoveTo, and OR |
| E03 | Collision, Timer, Animate, PlaySound, and PlayParticles |
| E04 | AND, NOT, global state, and cross-actor references |
| E05 | NavigateTo, Rotate, and RotateTo |
| E06 | Push, PushTo, and Torque |
| E07 | Spawn and Delete |
| E08 | Actor/rule/action order and multiple writes to the same property |

Together, the cases cover all six conditions, all fourteen formal actions, AND, OR, NOT, conditional and unconditional rules, local and global state, cross-actor references, actor/rule/action ordering, Spawn/Delete, and repeated writes.

## Comparison levels

For each controlled case, the runner performs four checks:

1. **Canonical JSON.** The original descriptor is compared with the descriptor exported after Studio import. Object keys are sorted, but arrays are never reordered. The normalizer removes only documented default values and empty editor metadata and rounds binary `float` noise to six decimal places.
2. **Parsed AST.** The runner builds an ordered validation AST with the production `GameRuleParser`. It records actors, properties, rules, Boolean operators, condition/action types, parameters, references, and action order. This is an explicit test oracle; the editor does not persist a separate AST asset.
3. **Generated C#.** The production `Scripts.CreateGameManager` and `Scripts.Create` generators are run for both representations. Line endings and trailing whitespace are normalized, while expressions, parameters, methods, and ordering remain significant.
4. **Round-trip.** The exported descriptor is re-imported and exported a second time. Canonical stability and the ordered actor/property/rule/action topology are checked.

The same four comparisons are applied to the manual and Studio-exported descriptors for Tanks, Survival Shooter, and John Lemon.

## Runtime checks

Three narrow checks exercise the execution semantics that can change results:

- actors are evaluated in the descriptor declaration order;
- immediate writes follow last-write-wins according to rule/action and actor order;
- scheduler additions and removals take effect only after the current traversal.

These checks intentionally do not substitute for a gameplay experiment.

## Running the suite

Inside Unity, use:

`GameRule > Validation > Run equivalence suite`

On Windows, close any other instance using the project and run from PowerShell:

```powershell
.\Validation\Equivalence\Run-EquivalenceValidation.ps1
```

If Unity is installed elsewhere, pass its executable explicitly:

```powershell
.\Validation\Equivalence\Run-EquivalenceValidation.ps1 -UnityPath "D:\Unity\6000.3.1f1\Editor\Unity.exe"
```

The runner writes:

- `Validation/Equivalence/Results/equivalence-report.json`
- `Validation/Equivalence/Results/equivalence-report.md`
- `Validation/Equivalence/Results/Evidence/evidence-manifest.json`
- canonical JSON, parsed validation AST, and normalized generated C# pairs under `Validation/Equivalence/Results/Evidence`
- `Validation/Equivalence/Results/unity-equivalence.log` when run through PowerShell

The evidence directory is recreated on every run so that no artifact from an older execution can be mistaken for a current result. Controlled cases retain the canonical input, first Studio export, second round-trip export, both parsed representations, and both generated-source snapshots. Full-game pairs retain the manual and Studio canonical inputs, first and second exports, both parsed representations, and both complete generated-source snapshots. The manifest records the overall result together with the relative path, byte count, and SHA-256 digest of every retained artifact. The machine-readable report also records paired SHA-256 digests for canonical JSON, parsed representations, and generated-source snapshots.

The overall result is `PASS` only when the Unity version matches exactly, all eight controlled cases pass at all four levels, all three full-game pairs pass at all four levels, formal construct coverage is complete, and all three runtime checks pass.

## Independent JSON check

The canonical comparison of the three full-game pairs can also be repeated without Unity:

```bash
python Validation/Equivalence/Scripts/compare_json.py
```

This independent check uses only the Python standard library and the same documented normalization policy. It does not replace the Unity-based parsed AST, generated C#, or runtime checks.

After the Unity suite has generated the inspectable evidence, its manifest can be verified independently with:

```bash
python Validation/Equivalence/Scripts/verify_evidence.py
```

This command fails if an indexed artifact is missing or if its byte count or SHA-256 digest has changed. It also reports unindexed files and cross-checks the manifest digest, artifact count, overall result, and paired representation hashes against the machine-readable report.
