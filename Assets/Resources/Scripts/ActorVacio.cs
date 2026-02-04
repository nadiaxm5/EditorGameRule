using UnityEngine;
using System.Collections.Generic;

public class ActorVacio : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Move("5","0","0","0",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Move(5,0,0,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
