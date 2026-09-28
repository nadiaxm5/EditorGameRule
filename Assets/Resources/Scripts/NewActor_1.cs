using UnityEngine;
using System.Collections.Generic;

public class NewActor_1 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Timer("2",gameObject)){
            Action.Spawn("BlueTank", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Timer(2);Spawn(BlueTank,this,0,0,0,0,0,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
