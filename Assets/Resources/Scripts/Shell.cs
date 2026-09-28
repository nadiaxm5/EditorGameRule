using UnityEngine;
using System.Collections.Generic;

public class Shell : MonoBehaviour, IGameRuleActor {
    public bool Active = false;
    public float speed=15f;
    public float damage=50f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        {
            Action.Move("this.speed","0","this.ry","0",gameObject,scopeList);
        }
        if(Condition.Collision("BlueTank",gameObject)){
            Action.Spawn("ShellExplosion", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
            Action.Delete(gameObject);
        }
        if(Condition.Collision("RedTank",gameObject)){
            Action.Spawn("ShellExplosion", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
            Action.Delete(gameObject);
        }
        if(Condition.Collision("Obstacle",gameObject)){
            Action.Spawn("ShellExplosion", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
            Action.Delete(gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Move(this.speed,0,this.ry,0);Collision(BlueTank);Spawn(ShellExplosion,this);Delete(this);Collision(RedTank);Collision(Obstacle)");
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
        propertyList = Utils.CreateProperties("speed=15;damage=50");
        TagCollisions["Untagged"] = new HashSet<GameObject>();
        TagCollisions["Player"] = new HashSet<GameObject>();
        TagCollisions["Enemy"] = new HashSet<GameObject>();
        TagCollisions["Obstacle"] = new HashSet<GameObject>();
        TagCollisions["Shell"] = new HashSet<GameObject>();
        TagCollisions["RedTank"] = new HashSet<GameObject>();
        TagCollisions["BlueTank"] = new HashSet<GameObject>();
    }
}
