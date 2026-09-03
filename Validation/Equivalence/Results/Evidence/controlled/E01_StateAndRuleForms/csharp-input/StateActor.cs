using UnityEngine;
using System.Collections.Generic;

public class StateActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float value=1f;
    public float flag=1f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Compare("this.value>=1",scopeList) && Condition.Check("this.flag",scopeList)){
            Action.Edit("this.value","this.value+1",scopeList);
            Action.Edit("#GlobalCounter","#GlobalCounter+1",scopeList);
        }
        {
            Action.Edit("this.flag","1",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Compare(this.value>=1);Check(this.flag);Edit(this.value,this.value+1);Edit(#GlobalCounter,#GlobalCounter+1);Edit(this.flag,1)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("value=1;flag=1");
    }
}