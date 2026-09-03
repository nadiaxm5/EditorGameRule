using UnityEngine;
using System.Collections.Generic;

public class Ghost3 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float delta=0.5f;
    public float speed=1.0f;
    public float xTarget=3.2f;
    public float zTarget=12.3f;
    public float x0=3.2f;
    public float x1=6.5f;
    public float z0=5.7f;
    public float z1=12.3f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.NavigateTo("this.speed","this.xTarget","this.y","this.zTarget",gameObject,scopeList);
        }
        if(Condition.Compare("this.z<this.z1+this.delta",scopeList) && Condition.Compare("this.z>this.z1-this.delta",scopeList) && Condition.Compare("this.x<this.x0+this.delta",scopeList) && Condition.Compare("this.x>this.x0-this.delta",scopeList)){
            Action.Edit("this.xTarget","this.x1",scopeList);
        }
        if(Condition.Compare("this.z<this.z1+this.delta",scopeList) && Condition.Compare("this.z>this.z1-this.delta",scopeList) && Condition.Compare("this.x<this.x1+this.delta",scopeList) && Condition.Compare("this.x>this.x1-this.delta",scopeList)){
            Action.Edit("this.zTarget","this.z0",scopeList);
        }
        if(Condition.Compare("this.z<this.z0+this.delta",scopeList) && Condition.Compare("this.z>this.z0-this.delta",scopeList) && Condition.Compare("this.x<this.x1+this.delta",scopeList) && Condition.Compare("this.x>this.x1-this.delta",scopeList)){
            Action.Edit("this.xTarget","this.x0",scopeList);
        }
        if(Condition.Compare("this.z<this.z0+this.delta",scopeList) && Condition.Compare("this.z>this.z0-this.delta",scopeList) && Condition.Compare("this.x<this.x0+this.delta",scopeList) && Condition.Compare("this.x>this.x0-this.delta",scopeList)){
            Action.Edit("this.zTarget","this.z1",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(this.speed,this.xTarget,this.y,this.zTarget);Compare(this.z<this.z1+this.delta);Compare(this.z>this.z1-this.delta);Compare(this.x<this.x0+this.delta);Compare(this.x>this.x0-this.delta);Edit(this.xTarget,this.x1);Compare(this.x<this.x1+this.delta);Compare(this.x>this.x1-this.delta);Edit(this.zTarget,this.z0);Compare(this.z<this.z0+this.delta);Compare(this.z>this.z0-this.delta);Edit(this.xTarget,this.x0);Edit(this.zTarget,this.z1)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("delta=0.5;speed=1.0;xTarget=3.2;zTarget=12.3;x0=3.2;x1=6.5;z0=5.7;z1=12.3");
    }
}