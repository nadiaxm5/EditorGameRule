using UnityEngine;
using System.Collections.Generic;

public class ZomBunnySpawner : MonoBehaviour {
    public bool Active = true;
    public float spawnTime=150.0f;
    public float currentTime=0.0f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Edit("this.currentTime","this.currentTime+1",scopeList);
        }
        if(Condition.Compare("this.currentTime==this.spawnTime",scopeList)){
            Action.Edit("this.currentTime","0",scopeList);
            Action.Spawn("ZomBunny", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(this.currentTime,this.currentTime+1);Compare(this.currentTime==this.spawnTime);Edit(this.currentTime,0);Spawn(ZomBunny,this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("spawnTime=150.0;currentTime=0.0");
    }
}
