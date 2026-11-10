using B83.LogicExpressionParser;
using UnityEngine;
using System.Collections.Generic;

public static class Action
{
    public static void Edit(string property, string valueExp, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float value = (float)parser.ParseNumber(valueExp).GetNumber();
        GameObject obj = scopeList[property];
        Utils.SetProperty(property, value, obj);
    }

    public static void Spawn(string prefabName, GameObject spawnerObj, string offsetXExp, string offsetYExp, string offsetZExp,
                            string offsetRxExp, string offsetRyExp, string offsetRzExp, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float ParseNum(string exp) => (float)parser.ParseNumber(exp).GetNumber();

        Vector3 offsetPos = new(ParseNum(offsetXExp), ParseNum(offsetYExp), ParseNum(offsetZExp));
        Vector3 offsetRot = new(ParseNum(offsetRxExp), ParseNum(offsetRyExp), ParseNum(offsetRzExp));

        Utils.SpawnActor(prefabName, spawnerObj, offsetPos, offsetRot);
    }

    public static void Animate(string animationName, GameObject obj)
    {
        Utils.PlayAnimation(animationName, obj);
    }

    public static void Move(string vExp, string rxExp, string ryExp, string rzExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float v = (float)parser.ParseNumber(vExp).GetNumber();
        float rx = (float)parser.ParseNumber(rxExp).GetNumber() * Mathf.Deg2Rad;
        float ry = (float)parser.ParseNumber(ryExp).GetNumber() * Mathf.Deg2Rad;
        float rz = (float)parser.ParseNumber(rzExp).GetNumber() * Mathf.Deg2Rad;

        Vector3 direction = new Vector3(Mathf.Cos(rx) * Mathf.Sin(ry), Mathf.Sin(rx), Mathf.Cos(rx) * Mathf.Cos(ry)).normalized;
        Vector3 delta = direction * v * Utils.DeltaTime;
        Utils.SetPosition(obj, Utils.GetPosition(obj) + delta);
    }

    public static void MoveTo(string speedExp, string xExp, string yExp, string zExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float targetX = (float)parser.ParseNumber(xExp).GetNumber();
        float targetY = (float)parser.ParseNumber(yExp).GetNumber();
        float targetZ = (float)parser.ParseNumber(zExp).GetNumber();
        float speed = (float)parser.ParseNumber(speedExp).GetNumber();

        Vector3 currentPos = Utils.GetPosition(obj);
        Vector3 targetPos = new Vector3(targetX, targetY, targetZ);
        Vector3 direction = (targetPos - currentPos).normalized;
        Vector3 step = direction * speed * Utils.DeltaTime;

        if (step.magnitude > Vector3.Distance(currentPos, targetPos))
            step = targetPos - currentPos;
        Vector3 newPos = currentPos + step;

        Utils.SetPosition(obj, newPos);
    }

    public static void NavigateTo(string speedExp, string xExp, string yExp, string zExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float x = (float)parser.ParseNumber(xExp).GetNumber();
        float y = (float)parser.ParseNumber(yExp).GetNumber();
        float z = (float)parser.ParseNumber(zExp).GetNumber();
        float speed = (float)parser.ParseNumber(speedExp).GetNumber();

        Utils.SetNavigationTarget(obj, speed, new Vector3(x, y, z));
    }

    public static void LoadScene()
    {
        Utils.LoadFirstScene();
    }

    public static void QuitGame()
    {
        Utils.QuitApplication();
    }

    public static void PlaySound(string audioClip, GameObject obj)
    {
        Utils.PlaySound(audioClip, obj);
    }

    public static void PlayParticles(string particleSystemName, GameObject obj)
    {
        Utils.PlayParticles(particleSystemName, obj);
    }

    public static void Delete(GameObject me)
    {
        Utils.DeleteActor(me);
    }

    public static void Rotate(string angleExp, string rxExp, string ryExp, string rzExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float angleSpeed = (float)parser.ParseNumber(angleExp).GetNumber();
        float rx = (float)parser.ParseNumber(rxExp).GetNumber();
        float ry = (float)parser.ParseNumber(ryExp).GetNumber();
        float rz = (float)parser.ParseNumber(rzExp).GetNumber();

        float angleDelta = angleSpeed * Utils.DeltaTime;

        Vector3 localAxis = new Vector3(rx, ry, rz).normalized;
        if (localAxis == Vector3.zero) localAxis = Vector3.up;
        Quaternion newRotation = Utils.GetRotation(obj) * Quaternion.AngleAxis(angleDelta, localAxis);
        Utils.SetRotation(obj, newRotation);
    }

    public static void RotateTo(string aExp, string dxExp, string dyExp, string dzExp, string pxExp, string pyExp, string pzExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float a = (float)parser.ParseNumber(aExp).GetNumber();
        float dx = (float)parser.ParseNumber(dxExp).GetNumber();
        float dy = (float)parser.ParseNumber(dyExp).GetNumber();
        float dz = (float)parser.ParseNumber(dzExp).GetNumber();
        float px = (float)parser.ParseNumber(pxExp).GetNumber();
        float py = (float)parser.ParseNumber(pyExp).GetNumber();
        float pz = (float)parser.ParseNumber(pzExp).GetNumber();

        Vector3 pivot = new Vector3(px, py, pz);
        Vector3 targetDir = new Vector3(dx, dy, dz) - pivot;
        Vector3 currentDir = Utils.GetPosition(obj) - pivot;

        Quaternion targetRot = Quaternion.LookRotation(targetDir.normalized, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(Utils.GetRotation(obj), targetRot, a * Utils.DeltaTime);
        Utils.SetRotation(obj, newRotation);

        Vector3 rotatedPos = pivot + targetRot * currentDir.normalized * currentDir.magnitude;
        Utils.SetPosition(obj, rotatedPos);
    }

    public static void Push(string forceExp, string rxExp, string ryExp, string rzExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float force = (float)parser.ParseNumber(forceExp).GetNumber();
        float rx = (float)parser.ParseNumber(rxExp).GetNumber() * Mathf.Deg2Rad;
        float ry = (float)parser.ParseNumber(ryExp).GetNumber() * Mathf.Deg2Rad;
        float rz = (float)parser.ParseNumber(rzExp).GetNumber() * Mathf.Deg2Rad;

        Vector3 direction = new Vector3(
            Mathf.Cos(rx) * Mathf.Sin(ry),
            Mathf.Sin(rx),
            Mathf.Cos(rx) * Mathf.Cos(ry)
        ).normalized;

        Vector3 forceVector = direction * force;
        Utils.ApplyForce(obj, forceVector);
    }

    public static void PushTo(string forceExp, string xExp, string yExp, string zExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float force = (float)parser.ParseNumber(forceExp).GetNumber();
        float targetX = (float)parser.ParseNumber(xExp).GetNumber();
        float targetY = (float)parser.ParseNumber(yExp).GetNumber();
        float targetZ = (float)parser.ParseNumber(zExp).GetNumber();

        Vector3 targetPos = new Vector3(targetX, targetY, targetZ);
        Vector3 direction = (targetPos - Utils.GetPosition(obj)).normalized;
        Vector3 forceVector = direction * force;

        Utils.ApplyForce(obj, forceVector);
    }

    public static void Torque(string rxExp, string ryExp, string rzExp, GameObject obj, Dictionary<string, GameObject> scopeList)
    {
        Parser parser = new Parser();
        foreach (var pair in scopeList)
            parser.ExpressionContext[pair.Key].Set(Utils.GetProperty(pair));

        float rx = (float)parser.ParseNumber(rxExp).GetNumber();
        float ry = (float)parser.ParseNumber(ryExp).GetNumber();
        float rz = (float)parser.ParseNumber(rzExp).GetNumber();

        Vector3 torqueVector = new Vector3(rx, ry, rz);
        Utils.ApplyTorque(obj, torqueVector);
    }

}
