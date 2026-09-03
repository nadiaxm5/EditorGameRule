using UnityEngine;
using System.Collections.Generic;

public class ObserverActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float flag=1f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(!Condition.Check("#Alarm",scopeList) && Condition.Compare("TargetActor.health>0",scopeList)){
            Action.Edit("TargetActor.health","TargetActor.health-1",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Check(#Alarm);Compare(TargetActor.health>0);Edit(TargetActor.health,TargetActor.health-1)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("flag=1");
    }
}