using UnityEngine;
using System.Collections.Generic;

public class Actor2 : MonoBehaviour {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        if(Condition.Collision("OtroTag",gameObject)){
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Collision(OtroTag);Delete()");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    public Dictionary<string, HashSet<GameObject>> TagCollisions = new Dictionary<string, HashSet<GameObject>>();
    void OnTriggerEnter(Collider other) {
        if (TagCollisions.ContainsKey(other.tag))
            TagCollisions[other.tag].Add(other.gameObject);
    }
    void OnTriggerExit(Collider other) {
        if (TagCollisions.ContainsKey(other.tag))
            TagCollisions[other.tag].Remove(other.gameObject);
    }
    void Awake() {
        TagCollisions["Untagged"] = new HashSet<GameObject>();
        TagCollisions["Respawn"] = new HashSet<GameObject>();
        TagCollisions["Finish"] = new HashSet<GameObject>();
        TagCollisions["EditorOnly"] = new HashSet<GameObject>();
        TagCollisions["MainCamera"] = new HashSet<GameObject>();
        TagCollisions["Player"] = new HashSet<GameObject>();
        TagCollisions["GameController"] = new HashSet<GameObject>();
        TagCollisions["Obstacle"] = new HashSet<GameObject>();
        TagCollisions["ZomBunny"] = new HashSet<GameObject>();
        TagCollisions["NuevoTag"] = new HashSet<GameObject>();
        TagCollisions["OtroTag"] = new HashSet<GameObject>();
    }
}
