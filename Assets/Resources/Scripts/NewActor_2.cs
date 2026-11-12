using UnityEngine;
using System.Collections.Generic;

public class NewActor_2 : MonoBehaviour, IGameRuleActor {
    public bool Active = true;
    public bool NewProp = true;
    public float NewProp2 = 0f;
    public Dictionary<string, object> propertyList = new Dictionary<string, object> {
        { nameof(NewProp), true },
        { nameof(NewProp2), 0f },
    };
    private Dictionary<string, float> timers = new Dictionary<string, float>();
    public void EvalFixedUpdate(){
        if(Condition.Check("this.Active",scopeList)){
            Action.Edit("this.Active","5",scopeList);
        }
    }
    public Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
    void Start() {
        scopeList = Utils.CreateScope(gameObject.GetInstanceID(),"Check(this.Active);Edit(this.Active,5)");
        if (Active) gameObject.SetActive(true);
        else gameObject.SetActive(false);
    }
}
