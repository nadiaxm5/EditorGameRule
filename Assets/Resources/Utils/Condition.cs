using B83.LogicExpressionParser;
using System.Collections.Generic;
using UnityEngine;

public static class Condition
{
    public static bool Compare(string a, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (KeyValuePair<string, GameObject> s in scopeList)
            parser.ExpressionContext[s.Key].Set(Utils.GetProperty(s));
        return (parser.Parse(a).GetResult());
    }

    public static bool Check(string variable, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        var num = parser.ParseNumber(variable).GetNumber();
        return ((float)num) == 1f;
    }

    public static bool Collision(string tag, GameObject obj)
    {
        var script = obj.GetComponent(obj.name);
        var tagCollisionsField = script.GetType().GetField("TagCollisions");
        if (tagCollisionsField == null) return false;

        var tagCollisions = tagCollisionsField.GetValue(script) as Dictionary<string, HashSet<GameObject>>;
        if (tagCollisions == null || !tagCollisions.ContainsKey(tag)) return false;

        tagCollisions[tag].RemoveWhere(o => o == null);

        return tagCollisions[tag].Count > 0;
    }

    public static bool Keyboard(string key, string keyMode)
    {
        return GameRuleInput.KeyboardState(key, keyMode);
    }

    public static bool Touch(string type, string onActor, GameObject obj)
    {
        bool inputMatches = GameRuleInput.TouchState(type);
        bool isOverMode = string.Equals(type?.Trim(), "isOver", System.StringComparison.OrdinalIgnoreCase);

        if (onActor.Contains("false"))
            return isOverMode ? false : inputMatches;

        if (onActor.Contains("true"))
        {
            bool isOverActor = false;
            Camera camera = Camera.main;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(GameRuleInput.PointerPosition(type));
            if (Physics.Raycast(ray, out RaycastHit hit))
                isOverActor = hit.collider.gameObject == obj;

            return isOverMode ? isOverActor : isOverActor && inputMatches;
        }

        return false;
    }

    public static bool Timer(string secondsString, GameObject obj)
    {
        float seconds = float.Parse(secondsString, System.Globalization.CultureInfo.InvariantCulture);

        var script = obj.GetComponent(obj.name);
        var field = script.GetType().GetField("timers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return false;

        var timers = field.GetValue(script) as Dictionary<string, float>;
        if (timers == null) return false;

        string key = "timer_" + secondsString;

        if (!timers.ContainsKey(key))
        {
            timers[key] = Time.time;
            return false;
        }

        float lastTime = timers[key];

        if (Time.time - lastTime >= seconds)
        {
            timers[key] = Time.time;
            return true;
        }

        return false;
    }
}
