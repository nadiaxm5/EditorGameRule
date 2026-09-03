# Preliminary canonical JSON results

These results were produced with `Scripts/compare_json.py` from the six descriptors stored in `Assets/Resources/Games` at source revision `c58f222091dbb642797006045ded9c226309fc3f`.

| Pair | Canonical JSON | Manual SHA-256 | Studio SHA-256 |
|---|---:|---|---|
| Tanks | Pass | `b71a4f6d0036173c0c4541380807bddc1a13ad221ca42c119acba6ec8b4e25b1` | `b71a4f6d0036173c0c4541380807bddc1a13ad221ca42c119acba6ec8b4e25b1` |
| Survival Shooter | Pass | `d9f9de538142609bee19959c01b1c5b335567772a4a341008f07f27f6c1b6561` | `d9f9de538142609bee19959c01b1c5b335567772a4a341008f07f27f6c1b6561` |
| John Lemon | Pass | `6d4bacefa292b2e27df99c5235ffae84e42b93bedd7d0db62adc13965ee76e94` | `6d4bacefa292b2e27df99c5235ffae84e42b93bedd7d0db62adc13965ee76e94` |

The comparison sorts object keys but never reorders arrays. It removes only documented scene defaults, missing-versus-empty editor metadata, and binary `float` serialization noise rounded to six decimal places.

These are preliminary JSON-level results only. The parsed AST, generated C#, repeated round-trip, and runtime columns must be taken from the Unity-generated report and must not be marked as passed before that run completes.
