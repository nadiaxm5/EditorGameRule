using UnityEngine;
using System.Collections.Generic;

public class ZomBearSpawner : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        if(Condition.Timer("2",gameObject)){
            Action.Spawn("ZomBear", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Timer(2);Spawn(ZomBear,this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
