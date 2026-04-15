using UnityEngine;
using System.Collections.Generic;

public class Jeff : MonoBehaviour {
    public bool Active = true;
    public float speed=4f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.NavigateTo("this.speed","Player.x","Player.y","Player.z",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"NavigateTo(this.speed,Player.x,Player.y,Player.z)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("speed=4");
    }
}
