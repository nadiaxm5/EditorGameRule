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
        return Utils.HasCollision(tag, obj);
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
            bool isOverActor = Utils.IsPointerOverActor(obj, GameRuleInput.PointerPosition(type));

            return isOverMode ? isOverActor : isOverActor && inputMatches;
        }

        return false;
    }

    public static bool Timer(string secondsString, GameObject obj)
    {
        float seconds = float.Parse(secondsString, System.Globalization.CultureInfo.InvariantCulture);

        var timers = Utils.GetActorTimers(obj);
        if (timers == null) return false;

        string key = "timer_" + secondsString;

        if (!timers.ContainsKey(key))
        {
            timers[key] = Utils.ElapsedTime;
            return false;
        }

        float lastTime = timers[key];

        if (Utils.ElapsedTime - lastTime >= seconds)
        {
            timers[key] = Utils.ElapsedTime;
            return true;
        }

        return false;
    }
}
