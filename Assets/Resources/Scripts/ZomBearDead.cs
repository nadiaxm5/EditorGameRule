using UnityEngine;
using System.Collections.Generic;

public class ZomBearDead : MonoBehaviour {
    public bool Active = false;
    public float counter=30f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Animate("Death",gameObject);
            Action.Edit("this.counter","this.counter-1",scopeList);
            Action.PlaySound("ZomBearDeath",gameObject);
            Action.PlayParticles("DeathParticles",gameObject);
        }
        if(Condition.Compare("this.counter<0",scopeList)){
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Animate(Death);Edit(this.counter,this.counter-1);PlaySound(ZomBearDeath);PlayParticles(DeathParticles);Compare(this.counter<0);Delete(this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("counter=30");
    }
}
