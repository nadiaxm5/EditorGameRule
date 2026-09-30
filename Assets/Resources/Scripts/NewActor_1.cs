using UnityEngine;
using System.Collections.Generic;

public class NewActor_1 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float NewProp=0f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
    }
    void Start() {
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("NewProp=0");
    }
}
