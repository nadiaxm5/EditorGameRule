using UnityEngine;
using System.Collections.Generic;

public class ShellExplosion : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        {
            Action.PlayParticles("ShellExplosion",gameObject);
            Action.PlaySound("ShellExplosion",gameObject);
        }
        if(Condition.Timer("0.5",gameObject)){
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"PlayParticles(ShellExplosion);PlaySound(ShellExplosion);Timer(0.5);Delete(this)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
