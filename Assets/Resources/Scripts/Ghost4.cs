using UnityEngine;
using System.Collections.Generic;

public class Ghost4 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float delta = 0.5f;
    public float speed = 1f;
    public float xTarget = 7.4f;
    public float zTarget = -2f;
    public float x0 = 3.2f;
    public float x1 = 7.4f;
    public float z0 = -5f;
    public float z1 = -2f;
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(delta), 0.5f },
        { nameof(speed), 1f },
        { nameof(xTarget), 7.4f },
        { nameof(zTarget), -2f },
        { nameof(x0), 3.2f },
        { nameof(x1), 7.4f },
        { nameof(z0), -5f },
        { nameof(z1), -2f },
    };
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
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(this.speed,this.xTarget,this.y,this.zTarget);Compare(this.z<this.z0+this.delta);Compare(this.z>this.z0-this.delta);Compare(this.x<this.x0+this.delta);Compare(this.x>this.x0-this.delta);Edit(this.xTarget,this.x1);Edit(this.zTarget,this.z1);Compare(this.z<this.z1+this.delta);Compare(this.z>this.z1-this.delta);Compare(this.x<this.x1+this.delta);Compare(this.x>this.x1-this.delta);Edit(this.xTarget,this.x0);Edit(this.zTarget,this.z0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
