using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour {
    public bool Active = true;
    public float Health=50f;
    public Dictionary<string, float> propertyList = new Dictionary<string, float>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    void FixedUpdate(){
        if(Condition.Collision("Enemy",gameObject) && Condition.Compare("this.Health < 100",scopeList)){
            Action.Delete(gameObject);
        }
    }
    void Update(){
        if(Condition.Keyboard("D","press")){
            Action.Move("5","0","90","0",gameObject,scopeList);
            Action.Animate("",gameObject);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Collision(Enemy);Compare(this.Health < 100);Delete();Keyboard(D,press);Move(5,0,90,0);Animate()");
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
        propertyList = Utils.CreateProperties("Health=50");
        TagCollisions["Untagged"] = new HashSet<GameObject>();
        TagCollisions["Respawn"] = new HashSet<GameObject>();
        TagCollisions["Finish"] = new HashSet<GameObject>();
        TagCollisions["EditorOnly"] = new HashSet<GameObject>();
        TagCollisions["MainCamera"] = new HashSet<GameObject>();
        TagCollisions["Player"] = new HashSet<GameObject>();
        TagCollisions["GameController"] = new HashSet<GameObject>();
        TagCollisions["Enemy"] = new HashSet<GameObject>();
        TagCollisions["ZomBunny"] = new HashSet<GameObject>();
        TagCollisions["Hellephant"] = new HashSet<GameObject>();
        TagCollisions["Obstacle"] = new HashSet<GameObject>();
        TagCollisions["Bullet"] = new HashSet<GameObject>();
        TagCollisions["Canvas"] = new HashSet<GameObject>();
        TagCollisions["ZomBear"] = new HashSet<GameObject>();
    }
}
