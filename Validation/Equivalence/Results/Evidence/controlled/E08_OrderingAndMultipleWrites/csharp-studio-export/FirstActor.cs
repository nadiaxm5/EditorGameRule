using UnityEngine;
using System.Collections.Generic;

public class FirstActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.Edit("#SharedValue","1",scopeList);
            Action.Edit("#SharedValue","2",scopeList);
        }
        {
            Action.Edit("#SharedValue","3",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(#SharedValue,1);Edit(#SharedValue,2);Edit(#SharedValue,3)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}