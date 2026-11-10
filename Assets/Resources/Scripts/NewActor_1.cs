using UnityEngine;
using System.Collections.Generic;

public class NewActor_1 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public float NewProp=0f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Collision("Untagged",gameObject) || Condition.Compare("this.z < ",scopeList)){
            Action.Spawn("", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
            Action.Edit("","",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Collision(Untagged);Compare(this.z < );Spawn(,this,0,0,0,0,0,0);Edit(,)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
    public Dictionary<string, HashSet<GameObject>> TagCollisions = new Dictionary<string, HashSet<GameObject>>();
    void OnTriggerEnter(Collider other) {
        GameObject otherActor = other.transform.root.gameObject;
        if (TagCollisions.ContainsKey(otherActor.tag))
            TagCollisions[otherActor.tag].Add(otherActor);
    }
    void OnTriggerExit(Collider other) {
        GameObject otherActor = other.transform.root.gameObject;
        if (TagCollisions.ContainsKey(otherActor.tag))
            TagCollisions[otherActor.tag].Remove(otherActor);
    }
    void Awake() {
        propertyList = Utils.CreateProperties("NewProp=0");
        TagCollisions["Untagged"] = new HashSet<GameObject>();
        TagCollisions["Player"] = new HashSet<GameObject>();
        TagCollisions["Enemy"] = new HashSet<GameObject>();
        TagCollisions["Obstacle"] = new HashSet<GameObject>();
        TagCollisions["Custom"] = new HashSet<GameObject>();
    }
}
