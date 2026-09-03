using UnityEngine;
using System.Collections.Generic;

public class TargetActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float health=10f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
    }
    public void EvalUpdate(){
    }
    void Start() {
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("health=10");
    }
}