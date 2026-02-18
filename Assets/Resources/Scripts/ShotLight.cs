using UnityEngine;
using System.Collections.Generic;

public class ShotLight : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Edit("this.x","Player.x",scopeList);
            Action.Edit("this.y","Player.y",scopeList);
            Action.Edit("this.z","Player.z",scopeList);
            Action.Edit("this.Active","0",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(this.x,Player.x);Edit(this.y,Player.y);Edit(this.z,Player.z);Edit(this.Active,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
