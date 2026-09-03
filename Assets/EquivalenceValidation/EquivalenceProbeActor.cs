#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Editor-only probe used by the reproducible equivalence validation runner.
/// It is excluded from player builds and is never stored in a scene.
/// </summary>
public sealed class EquivalenceProbeActor : MonoBehaviour, IGameRuleActor
{
    [System.NonSerialized] public System.Action OnUpdateEvaluation;
    [System.NonSerialized] public System.Action OnFixedEvaluation;

    public void EvalUpdate()
    {
        OnUpdateEvaluation?.Invoke();
    }

    public void EvalFixedUpdate()
    {
        OnFixedEvaluation?.Invoke();
    }
}

/// <summary>
/// State target for the last-write-wins validation. The object is named after
/// this component so the production Utils property resolver follows its normal path.
/// </summary>
public sealed class EquivalenceStateActor : MonoBehaviour, IGameRuleActor
{
    public bool Active = true;
    public float value;

    public void EvalUpdate()
    {
    }

    public void EvalFixedUpdate()
    {
    }
}
#endif
