using UnityEngine;
using System.Collections.Generic;

public class ZomBearDead : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.Animate("Death",gameObject);
            Action.PlaySound("ZomBearDeath",gameObject);
            Action.PlayParticles("DeathParticles",gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Animate(Death);PlaySound(ZomBearDeath);PlayParticles(DeathParticles)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
