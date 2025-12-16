using UnityEngine;
using System.Collections.Generic;

public class HellephantDead : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Animate("Death",gameObject);
            Action.PlaySound("HellephantDeath",gameObject);
            Action.PlayParticles("DeathParticles",gameObject);
        }
        if(Condition.Timer("1",gameObject)){
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Animate(Death);PlaySound(HellephantDeath);PlayParticles(DeathParticles);Timer(1);Delete(this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
