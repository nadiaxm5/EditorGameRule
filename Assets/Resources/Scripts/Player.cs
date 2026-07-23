using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour {
    public bool Active = true;
    public float Health=100f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void Update(){
        if(Condition.Keyboard("D","press")){
            Action.Move("5","0","90","0",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Keyboard(D,press);Move(5,0,90,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("Health=100");
    }
}
