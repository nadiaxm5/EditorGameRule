using UnityEngine;
using System.Collections.Generic;

public class BlueHealth : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float offsetY = 0.05f;
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(offsetY), 0.05f },
    };
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.Edit("this.x","BlueTank.x",scopeList);
            Action.Edit("this.y","BlueTank.y+this.offsetY",scopeList);
            Action.Edit("this.z","BlueTank.z",scopeList);
            Action.Edit("this.ry","BlueTank.ry",scopeList);
            Action.Edit("this.sliderValue","BlueTank.health",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Edit(this.x,BlueTank.x);Edit(this.y,BlueTank.y+this.offsetY);Edit(this.z,BlueTank.z);Edit(this.ry,BlueTank.ry);Edit(this.sliderValue,BlueTank.health)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
