using UnityEngine;
using System.Collections.Generic;

public class Ghost4 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float delta=0.5f;
    public float speed=1.0f;
    public float xTarget=7.4f;
    public float zTarget=-2.0f;
    public float x0=3.2f;
    public float x1=7.4f;
    public float z0=-5f;
    public float z1=-2f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.NavigateTo("this.speed","this.xTarget","this.y","this.zTarget",gameObject,scopeList);
        }
        if(Condition.Compare("this.z<this.z0+this.delta",scopeList) && Condition.Compare("this.z>this.z0-this.delta",scopeList) && Condition.Compare("this.x<this.x0+this.delta",scopeList) && Condition.Compare("this.x>this.x0-this.delta",scopeList)){
            Action.Edit("this.xTarget","this.x1",scopeList);
            Action.Edit("this.zTarget","this.z1",scopeList);
        }
        if(Condition.Compare("this.z<this.z1+this.delta",scopeList) && Condition.Compare("this.z>this.z1-this.delta",scopeList) && Condition.Compare("this.x<this.x1+this.delta",scopeList) && Condition.Compare("this.x>this.x1-this.delta",scopeList)){
            Action.Edit("this.xTarget","this.x0",scopeList);
            Action.Edit("this.zTarget","this.z0",scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(this.speed,this.xTarget,this.y,this.zTarget);Compare(this.z<this.z0+this.delta);Compare(this.z>this.z0-this.delta);Compare(this.x<this.x0+this.delta);Compare(this.x>this.x0-this.delta);Edit(this.xTarget,this.x1);Edit(this.zTarget,this.z1);Compare(this.z<this.z1+this.delta);Compare(this.z>this.z1-this.delta);Compare(this.x<this.x1+this.delta);Compare(this.x>this.x1-this.delta);Edit(this.xTarget,this.x0);Edit(this.zTarget,this.z0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("delta=0.5;speed=1.0;xTarget=7.4;zTarget=-2.0;x0=3.2;x1=7.4;z0=-5;z1=-2");
    }
}