using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.NavigateTo("5","Player.x","Player.y","Player.z",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(5,Player.x,Player.y,Player.z)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
