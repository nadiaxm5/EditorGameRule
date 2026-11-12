using UnityEngine;
using System.Collections.Generic;

public class NewActor : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Compare(" < ",scopeList) && Condition.Check("this.Active",scopeList) && Condition.Collision("Untagged",gameObject) && Condition.Keyboard("A","down") && Condition.Touch("press","false",gameObject) && Condition.Timer("0",gameObject)){
            Action.Edit("","",scopeList);
            Action.Spawn("", gameObject, "0", "0", "0", "0", "0", "0", scopeList);
            Action.Animate("",gameObject);
            Action.Move("0","0","0","0",gameObject,scopeList);
            Action.MoveTo("0","0","0","0",gameObject,scopeList);
            Action.NavigateTo("0","0","0","0",gameObject,scopeList);
            Action.LoadScene();
            Action.QuitGame();
            Action.PlaySound("",gameObject);
            Action.PlayParticles("",gameObject);
            Action.Delete(gameObject);
            Action.Rotate("0","0","0","0",gameObject,scopeList);
            Action.RotateTo("0","0","0","0","this.x","this.y","this.z",gameObject,scopeList);
            Action.Push("0","0","0","0",gameObject,scopeList);
            Action.PushTo("0","0","0","0",gameObject,scopeList);
            Action.Torque("0","0","0",gameObject,scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Compare( < );Check(this.Active);Collision(Untagged);Keyboard(A,down);Touch(press,false);Timer(0);Edit(,);Spawn(,this,0,0,0,0,0,0);Animate();Move(0,0,0,0);MoveTo(0,0,0,0);NavigateTo(0,0,0,0);LoadScene();QuitGame();PlaySound();PlayParticles();Delete();Rotate(0,0,0,0);RotateTo(0,0,0,0,this.x,this.y,this.z);Push(0,0,0,0);PushTo(0,0,0,0);Torque(0,0,0)");
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
        TagCollisions["Untagged"] = new HashSet<GameObject>();
        TagCollisions["Player"] = new HashSet<GameObject>();
        TagCollisions["Enemy"] = new HashSet<GameObject>();
        TagCollisions["Obstacle"] = new HashSet<GameObject>();
        TagCollisions["Custom"] = new HashSet<GameObject>();
    }
}
