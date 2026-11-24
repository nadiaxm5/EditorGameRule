using UnityEngine;
using System.Collections.Generic;

public class Ghost1 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float speed = 1f;
    public float zTarget = 6.4f;
    public float z0 = -4.5f;
    public float z1 = 6.4f;
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(speed), 1f },
        { nameof(zTarget), 6.4f },
        { nameof(z0), -4.5f },
        { nameof(z1), 6.4f },
    };
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.NavigateTo("this.speed","this.x","this.y","this.zTarget",gameObject,scopeList);
        }
        if(Condition.Compare("this.z>=this.z1",scopeList)){
            Action.Edit("this.zTarget","this.z0",scopeList);
        }
        if(Condition.Compare("this.z<=this.z0",scopeList)){
            Action.Edit("this.zTarget","this.z1",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(this.speed,this.x,this.y,this.zTarget);Compare(this.z>=this.z1);Edit(this.zTarget,this.z0);Compare(this.z<=this.z0);Edit(this.zTarget,this.z1)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
