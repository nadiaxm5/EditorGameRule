using UnityEngine;
using System.Collections.Generic;

public class HUDCanvas : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Edit("this.value","Player.health",scopeList);
            Action.Edit("this.text","Player.score",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(this.value,Player.health);Edit(this.text,Player.score)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
