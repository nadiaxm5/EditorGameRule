using UnityEngine;
using System.Collections.Generic;

public class RedTank : MonoBehaviour, IGameRuleActor {
    public bool Active = false;
    public bool SpawnReady = true;
    public int SpawnAmmo = 4;
    public float SpawnBoost = 0.75f;
    public Vector2 SpawnOffset = new Vector2(0f, 1.5f);
    public Vector3 SpawnGoal = new Vector3(0f, 0f, 2.5f);
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(SpawnReady), true },
        { nameof(SpawnAmmo), 4 },
        { nameof(SpawnBoost), 0.75f },
        { nameof(SpawnOffset), new Vector2(0f, 1.5f) },
        { nameof(SpawnGoal), new Vector3(0f, 0f, 2.5f) },
    };
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Check("this.SpawnReady",scopeList) && Condition.Compare("this.SpawnAmmo == 4",scopeList) && Condition.Compare("this.SpawnBoost == 0.75",scopeList) && Condition.Compare("this.SpawnOffset.y == 1.5",scopeList) && Condition.Compare("this.SpawnGoal.z == 2.5",scopeList)){
            Action.Edit("this.z","this.SpawnGoal.z+this.SpawnOffset.y",scopeList);
            Action.Edit("this.SpawnReady","0",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Check(this.SpawnReady);Compare(this.SpawnAmmo == 4);Compare(this.SpawnBoost == 0.75);Compare(this.SpawnOffset.y == 1.5);Compare(this.SpawnGoal.z == 2.5);Edit(this.z,this.SpawnGoal.z+this.SpawnOffset.y);Edit(this.SpawnReady,0)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
