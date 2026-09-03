using UnityEngine;
using System.Collections.Generic;

public class NavigatorActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float speed=2f;
    public float angularSpeed=90f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Compare("TargetActor.Active==1",scopeList)){
            Action.NavigateTo("this.speed","TargetActor.x","TargetActor.y","TargetActor.z",gameObject,scopeList);
            Action.Rotate("this.angularSpeed","0","1","0",gameObject,scopeList);
            Action.RotateTo("this.angularSpeed","0","0","1","this.x","this.y","this.z",gameObject,scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Compare(TargetActor.Active==1);NavigateTo(this.speed,TargetActor.x,TargetActor.y,TargetActor.z);Rotate(this.angularSpeed,0,1,0);RotateTo(this.angularSpeed,0,0,1,this.x,this.y,this.z)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("speed=2;angularSpeed=90");
    }
}