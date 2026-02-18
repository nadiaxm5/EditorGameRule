using UnityEngine;
using System.Collections.Generic;

public class Actor1 : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void Start() {
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
