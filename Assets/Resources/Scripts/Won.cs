using UnityEngine;
using System.Collections.Generic;

public class Won : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        if(Condition.Timer("1",gameObject)){
            Action.QuitGame();
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Timer(1);QuitGame()");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
