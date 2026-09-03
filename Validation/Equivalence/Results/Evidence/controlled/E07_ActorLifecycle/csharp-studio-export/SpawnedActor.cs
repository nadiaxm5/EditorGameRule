using UnityEngine;
using System.Collections.Generic;

public class SpawnedActor : MonoBehaviour, IGameRuleActor {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Compare("this.Active==1",scopeList)){
            Action.Delete(gameObject);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Compare(this.Active==1);Delete()");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}