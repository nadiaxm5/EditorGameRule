using UnityEngine;
using System.Collections.Generic;

public class Laser : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Delete(this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
