using UnityEngine;
using System.Collections.Generic;

public class InputActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float speed=3f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
    }
    public void EvalUpdate(){
        if(Condition.Keyboard("W","press") || Condition.Touch("tap","true",gameObject)){
            Action.Move("this.speed","0","0","0",gameObject,scopeList);
            Action.MoveTo("this.speed","10","0","5",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Keyboard(W,press);Touch(tap,true);Move(this.speed,0,0,0);MoveTo(this.speed,10,0,5)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("speed=3");
    }
}