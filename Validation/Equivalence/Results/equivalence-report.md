# GameRule Studio equivalence validation

- Result: **PASS**
- Unity: `6000.3.1f1` (expected `6000.3.1f1`)
- Source revision: `c58f222091dbb642797006045ded9c226309fc3f`
- Generated: `2026-09-03T11:16:20.2902103Z`

## Controlled cases

| Case | Canonical JSON | Parsed AST | Generated C# | Round-trip |
|---|---:|---:|---:|---:|
| E01_StateAndRuleForms | Pass | Pass | Pass | Pass |
| E02_InputAndMovement | Pass | Pass | Pass | Pass |
| E03_EventsAndMedia | Pass | Pass | Pass | Pass |
| E04_BooleanAndCrossActor | Pass | Pass | Pass | Pass |
| E05_NavigationAndRotation | Pass | Pass | Pass | Pass |
| E06_PhysicsActions | Pass | Pass | Pass | Pass |
| E07_ActorLifecycle | Pass | Pass | Pass | Pass |
| E08_OrderingAndMultipleWrites | Pass | Pass | Pass | Pass |

## Full-game integration pairs

| Case | Canonical JSON | Parsed AST | Generated C# | Round-trip |
|---|---:|---:|---:|---:|
| Tanks | Pass | Pass | Pass | Pass |
| Survival Shooter | Pass | Pass | Pass | Pass |
| John Lemon | Pass | Pass | Pass | Pass |

## Runtime checks

| Check | Result | Detail |
|---|---|---|
| Actor declaration order | Pass | Observed declaration order: Second -> First -> Third |
| Multiple writes and last-write-wins | Pass | A then B produced 3; B then A produced 2. The final write in the declared order prevailed. |
| Deferred Spawn and Delete scheduling | Pass | Spawned actors began on the next pass; removed actors were excluded from the next pass. |

## Coverage

- Six formal conditions: Check, Collision, Compare, Keyboard, Timer, Touch
- Fourteen formal actions: Animate, Delete, Edit, Move, MoveTo, NavigateTo, PlayParticles, PlaySound, Push, PushTo, Rotate, RotateTo, Spawn, Torque
- Boolean operators: AND, NOT, OR
- Conditional and unconditional rules: Pass
- Local, global, and cross-actor references: Pass
- Coverage result: **Pass**
