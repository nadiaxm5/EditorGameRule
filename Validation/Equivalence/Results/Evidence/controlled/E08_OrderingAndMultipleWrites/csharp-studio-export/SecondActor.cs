using UnityEngine;
using System.Collections.Generic;

public class SecondActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Compare("#SharedValue==3",scopeList)){
            Action.Edit("#SharedValue","4",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Compare(#SharedValue==3);Edit(#SharedValue,4)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}