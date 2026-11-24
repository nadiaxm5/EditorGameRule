using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SceneJson
{
    public string GameName;
    public float[] ScreenResolution;
    public float[] CameraPosition;
    public float[] CameraRotation;
    public float[] SunPosition;
    public float[] SunRotation;
    public byte[] SunColor;
    public byte[] SunAmbientColor;
    public byte[] BackgroundColor;
    public float[] Gravity;
    public string SoundTrack = "";
    public float[] Mouse;
    public float[] MouseWorld;
    public List<ActorJson> Cast;
    public List<CustomVariable> CustomVariables;
}

[System.Serializable]
public class ActorComponentMeta
{
    public string type; // "Properties" | "Rules"
    public string name;
    public string id;   // unique id for Rules components — links rules to this component
}

[System.Serializable]
public class ActorJson
{
    public string ActorName;
    public bool Active = true;
    public string PrefabName;
    public string Tag;
    public string IconColorHex;
    public float[] Position;
    public float[] Rotation;
    public float[] Scale;
    public float[] Velocity;
    public float[] AngularVelocity;
    public float Density;
    public float Friction;
    public float Bounciness;
    public float Drag;
    public bool OverrideDensity;
    public bool OverrideFriction;
    public bool OverrideBounciness;
    public bool OverrideDrag;
    // Legacy entries are retained in the schema so old assets and JSON can be loaded.
    public List<string> Properties;
    public List<CustomVariable> CustomProperties = new List<CustomVariable>();

    public List<CustomVariable> GetCustomProperties()
    {
        if (CustomProperties == null) CustomProperties = new List<CustomVariable>();
        if (Properties != null && Properties.Count > 0)
        {
            foreach (string entry in Properties)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                CustomVariable property = CustomVariable.FromLegacyActorProperty(entry);
                if (!CustomProperties.Exists(existing => existing.name == property.name))
                    CustomProperties.Add(property);
            }
            Properties.Clear();
        }
        return CustomProperties;
    }
    public List<SentenceJson> Script = new List<SentenceJson>();
    public List<ActorComponentMeta> Components = new List<ActorComponentMeta>();
}

[System.Serializable]
public class SentenceJson
{
    public string Name;
    public List<string> When;
    public List<string> Do;
    public string groupId; // links this rule to a Rules component id
}

[System.Serializable]
public class CustomVariable
{
    public string name;
    public string type;
    public int intValue;
    public float floatValue;
    public bool boolValue;
    public float[] arrayValue;

    public static CustomVariable FromLegacyActorProperty(string entry)
    {
        var parts = entry.Split(new[] { '=' }, 2);
        float value = 0f;
        if (parts.Length == 2)
        {
            float.TryParse(parts[1].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        return new CustomVariable { name = parts[0].Trim(), type = "float", floatValue = value };
    }

    public object GetValue()
    {
        switch (type.ToLower())
        {
            case "int": return intValue;
            case "float": return floatValue;
            case "bool": return boolValue;
            case "vector2":
                return arrayValue != null && arrayValue.Length >= 2 ?
                    new Vector2(arrayValue[0], arrayValue[1]) : Vector2.zero;

            case "vector3":
                return arrayValue != null && arrayValue.Length >= 3 ?
                    new Vector3(arrayValue[0], arrayValue[1], arrayValue[2]) : Vector3.zero;

            default: return null;
        }
    }
}
