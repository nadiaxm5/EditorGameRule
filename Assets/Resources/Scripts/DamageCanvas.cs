using UnityEngine;
using System.Collections.Generic;

public class DamageCanvas : MonoBehaviour {
    public bool Active = false;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void Start() {
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
