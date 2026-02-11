using UnityEngine;
using System.Collections.Generic;

public class ActorVacio : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Spawn("Environment", gameObject, "#SunPosition.x", "", "", "", "", "", scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Spawn(Environment,,#SunPosition.x,,,,,)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
