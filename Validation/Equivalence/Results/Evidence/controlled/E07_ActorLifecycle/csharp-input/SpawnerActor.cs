using UnityEngine;
using System.Collections.Generic;

public class SpawnerActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Timer("1",gameObject)){
            Action.Spawn("SpawnedActor", gameObject, "1", "0", "0", "0", "0", "0", scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Timer(1);Spawn(SpawnedActor,this,1,0,0,0,0,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}