using UnityEngine;
using System.Collections.Generic;

public class HUDCanvas : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Edit("this.sliderValue","Player.health",scopeList);
            Action.Edit("this.text","#Score",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(this.sliderValue,Player.health);Edit(this.text,#Score)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
