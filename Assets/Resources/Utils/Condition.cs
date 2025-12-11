using B83.LogicExpressionParser;
using UnityEngine;
using System;
using System.Collections.Generic;

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

        tagCollisions[tag].RemoveWhere(obj => obj == null);

        return tagCollisions[tag].Count > 0;
    }

    public static bool Keyboard(string key, string keyMode)
    {
        KeyCode k = (KeyCode)Enum.Parse(typeof(KeyCode), key);
        switch (keyMode)
        {
            case "press": return Input.GetKey(k);
            case "down": return Input.GetKeyDown(k);
            case "up": return Input.GetKeyUp(k);
            default: break;
        }
        return false;
    }

    public static bool Touch(string type, string onActor, GameObject obj)
    {
        // If on actor is false
        if (onActor.Contains("false"))
        {
            switch (type)
            {
                case "press": return Input.GetMouseButton(0);
                case "down": return Input.GetMouseButtonDown(0);
                case "up": return Input.GetMouseButtonUp(0);
                case "tap":
                    return Input.GetMouseButtonUp(0) && !Input.GetMouseButton(0);

                case "isOver": return false;
            }
            return false;
        }

        // If on actor is true
        bool isOverActor =
            Physics.Raycast(
                Camera.main.ScreenPointToRay(Input.mousePosition),
                out RaycastHit hit
            ) && hit.collider.gameObject == obj;

        switch (type)
        {
            case "isOver": return isOverActor;
            case "press": return isOverActor && Input.GetMouseButton(0);
            case "down": return isOverActor && Input.GetMouseButtonDown(0);
            case "up": return isOverActor && Input.GetMouseButtonUp(0);
            case "tap": return isOverActor && Input.GetMouseButtonUp(0) && !Input.GetMouseButton(0);
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