using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.NavigateTo("5","this.x","this.y","this.z",gameObject,scopeList);
        }
    }
    public void EvalUpdate(){
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(5,this.x,this.y,this.z)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
