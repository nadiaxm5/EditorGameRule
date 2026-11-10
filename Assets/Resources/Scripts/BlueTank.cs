using UnityEngine;
using System.Collections.Generic;

public class BlueTank : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public int Ammo = 1;
    public float Boost = 0.5f;
    public bool Armed = false;
    public Vector2 Offset = new Vector2(7f, 1f);
    public Vector3 Goal = new Vector3(0f, 0f, 1f);
    public float LegacyStep = 2f;
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(Ammo), 1 },
        { nameof(Boost), 0.5f },
        { nameof(Armed), false },
        { nameof(Offset), new Vector2(7f, 1f) },
        { nameof(Goal), new Vector3(0f, 0f, 1f) },
        { nameof(LegacyStep), 2f },
    };
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Keyboard("Space","down") && !Condition.Check("this.Armed",scopeList) && Condition.Compare("this.LegacyStep == 2",scopeList) && Condition.Compare("this.Boost == 0.5",scopeList)){
            Action.Edit("this.Ammo","2.6",scopeList);
            Action.Edit("this.Boost","this.Boost+1",scopeList);
            Action.Edit("this.Offset.x","this.Offset.x+1",scopeList);
            Action.Edit("this.Goal.z","this.Goal.z+this.LegacyStep",scopeList);
            Action.Edit("this.Armed","1",scopeList);
            Action.Edit("#TypedTestPassed","1",scopeList);
        }
        if(Condition.Check("this.Armed",scopeList) && Condition.Check("#TypedTestPassed",scopeList) && Condition.Compare("this.Ammo == 3",scopeList) && Condition.Compare("this.Boost == 1.5",scopeList) && Condition.Compare("this.Offset.x == 8",scopeList) && Condition.Compare("this.Goal.z == 3",scopeList)){
            Action.Spawn("RedTank", gameObject, "this.Offset.x", "0", "0", "0", "0", "0", scopeList);
            Action.Edit("this.z","this.Goal.z",scopeList);
            Action.Edit("#TypedTestPassed","0",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Keyboard(Space,down);Check(this.Armed);Compare(this.LegacyStep == 2);Compare(this.Boost == 0.5);Edit(this.Ammo,2.6);Edit(this.Boost,this.Boost+1);Edit(this.Offset.x,this.Offset.x+1);Edit(this.Goal.z,this.Goal.z+this.LegacyStep);Edit(this.Armed,1);Edit(#TypedTestPassed,1);Check(#TypedTestPassed);Compare(this.Ammo == 3);Compare(this.Boost == 1.5);Compare(this.Offset.x == 8);Compare(this.Goal.z == 3);Spawn(RedTank,this,this.Offset.x,0,0,0,0,0);Edit(this.z,this.Goal.z);Edit(#TypedTestPassed,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
