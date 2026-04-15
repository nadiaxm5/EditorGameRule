using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnityEngine;

    #region  AUDITOR — validates and fixes actor/game JSON automatically
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Auditor that validates and fixes game JSON before applying.
/// Key responsibilities:
///   1. Ensure all this.PROPERTY references in Script have matching Properties entries.
///   2. Validate required fields (ActorName, PrefabName).
///   3. Check Script structure (every rule must have Do).
///   4. Remove duplicate properties.
///   5. Cross-actor reference validation (full game audit).
///   6. Global variable reference validation.
/// </summary>
public static class GameJsonAuditor
{
    // ── Built-in properties available without declaration ──
    private static readonly HashSet<string> BuiltInProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "x", "y", "z", "rx", "ry", "rz", "Active", "value", "text"
    };

    // ── Built-in global references (always available) ──
    private static readonly HashSet<string> BuiltInGlobals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CameraPosition", "CameraRotation", "MouseWorld", "Mouse", "Gravity",
        "ScreenResolution", "SunPosition", "SunRotation"
    };

    // ── Regex patterns ──
    private static readonly Regex ThisPropRegex =
        new Regex(@"this\.([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex CrossActorRegex =
        new Regex(@"(?<![#\w])([A-Z][a-zA-Z0-9_]*)\.([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex GlobalRefRegex =
        new Regex(@"#([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════
    #endregion
    #region ACTION SIGNATURE DEFINITIONS — arity + parameter semantics
    // ══════════════════════════════════════════════════════════

    /// <summary>Semantic type of an action parameter.</summary>
    private enum ParamType
    {
        Speed,        // Velocity or angular speed (scalar, numeric or this.speed etc.)
        Force,        // Force magnitude (scalar)
        PosX,         // World X coordinate (scalar: literal, this.x, Actor.x)
        PosY,         // World Y coordinate
        PosZ,         // World Z coordinate
        RotX,         // Rotation/direction angle X (rx)
        RotY,         // Rotation/direction angle Y (ry)
        RotZ,         // Rotation/direction angle Z (rz)
        PivotX,       // Pivot/origin X (usually this.x)
        PivotY,       // Pivot/origin Y (usually this.y)
        PivotZ,       // Pivot/origin Z (usually this.z)
        OffsetX,      // Spawn offset X
        OffsetY,      // Spawn offset Y
        OffsetZ,      // Spawn offset Z
        Property,     // Property reference: this.prop, Actor.prop, #Global
        Value,        // Any expression (arithmetic, boolean, string)
        Name,         // Resource name (animation, sound, prefab, particle)
        Source,       // Reference to spawner actor (this, ActorName)
        Target,       // Actor reference: this (for Delete)
        Any           // Unconstrained
    }

    /// <summary>Definition of an action's valid signatures.</summary>
    private class ActionDef
    {
        public string Name;
        public int[] ValidArities;          // e.g. {7} for RotateTo, {2,5} for Spawn
        public ParamType[][] ParamSchemas;  // one ParamType[] per valid arity, in same order as ValidArities
        public string HumanSignature;       // readable signature for error messages

        public ActionDef(string name, string humanSig, params (int arity, ParamType[] schema)[] overloads)
        {
            Name = name;
            HumanSignature = humanSig;
            ValidArities = new int[overloads.Length];
            ParamSchemas = new ParamType[overloads.Length][];
            for (int i = 0; i < overloads.Length; i++)
            {
                ValidArities[i] = overloads[i].arity;
                ParamSchemas[i] = overloads[i].schema;
            }
        }
    }

    /// <summary>Registry of all known action signatures.</summary>
    private static readonly Dictionary<string, ActionDef> ActionRegistry =
        new Dictionary<string, ActionDef>(StringComparer.OrdinalIgnoreCase)
    {
        { "Move", new ActionDef("Move", "Move(speed, rx, ry, rz)",
            (4, new[] { ParamType.Speed, ParamType.RotX, ParamType.RotY, ParamType.RotZ })) },

        { "MoveTo", new ActionDef("MoveTo", "MoveTo(speed, x, y, z)",
            (4, new[] { ParamType.Speed, ParamType.PosX, ParamType.PosY, ParamType.PosZ })) },

        { "NavigateTo", new ActionDef("NavigateTo", "NavigateTo(speed, x, y, z)",
            (4, new[] { ParamType.Speed, ParamType.PosX, ParamType.PosY, ParamType.PosZ })) },

        { "Rotate", new ActionDef("Rotate", "Rotate(angSpeed, rx, ry, rz)",
            (4, new[] { ParamType.Speed, ParamType.RotX, ParamType.RotY, ParamType.RotZ })) },

        { "RotateTo", new ActionDef("RotateTo", "RotateTo(speed, targetX, targetY, targetZ, pivotX, pivotY, pivotZ)",
            (7, new[] { ParamType.Speed, ParamType.PosX, ParamType.PosY, ParamType.PosZ,
                        ParamType.PivotX, ParamType.PivotY, ParamType.PivotZ })) },

        { "Push", new ActionDef("Push", "Push(force, rx, ry, rz)",
            (4, new[] { ParamType.Force, ParamType.RotX, ParamType.RotY, ParamType.RotZ })) },

        { "PushTo", new ActionDef("PushTo", "PushTo(force, x, y, z)",
            (4, new[] { ParamType.Force, ParamType.PosX, ParamType.PosY, ParamType.PosZ })) },

        { "Torque", new ActionDef("Torque", "Torque(rx, ry, rz)",
            (3, new[] { ParamType.RotX, ParamType.RotY, ParamType.RotZ })) },

        { "Edit", new ActionDef("Edit", "Edit(property, value)",
            (2, new[] { ParamType.Property, ParamType.Value })) },

        { "Spawn", new ActionDef("Spawn", "Spawn(prefab, source) or Spawn(prefab, source, offX, offY, offZ)",
            (2, new[] { ParamType.Name, ParamType.Source }),
            (5, new[] { ParamType.Name, ParamType.Source, ParamType.OffsetX, ParamType.OffsetY, ParamType.OffsetZ })) },

        { "Delete", new ActionDef("Delete", "Delete(this)",
            (1, new[] { ParamType.Target })) },

        { "Animate", new ActionDef("Animate", "Animate(name)",
            (1, new[] { ParamType.Name })) },

        { "PlaySound", new ActionDef("PlaySound", "PlaySound(name)",
            (1, new[] { ParamType.Name })) },

        { "PlayParticles", new ActionDef("PlayParticles", "PlayParticles(name)",
            (1, new[] { ParamType.Name })) },

        { "LoadScene", new ActionDef("LoadScene", "LoadScene()",
            (0, Array.Empty<ParamType>())) },

        { "QuitGame", new ActionDef("QuitGame", "QuitGame()",
            (0, Array.Empty<ParamType>())) },
    };

    /// <summary>Regex to parse an action string into name + parameter list.</summary>
    private static readonly Regex ActionParseRegex =
        new Regex(@"^(\w+)\((.*)\)$", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Checks if a parameter value looks like a position reference (.x, .y, .z).
    /// </summary>
    private static bool LooksLikePosition(string param)
    {
        param = param.Trim();
        return param.EndsWith(".x", StringComparison.OrdinalIgnoreCase)
            || param.EndsWith(".y", StringComparison.OrdinalIgnoreCase)
            || param.EndsWith(".z", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a parameter value looks like a rotation reference (.rx, .ry, .rz).
    /// </summary>
    private static bool LooksLikeRotation(string param)
    {
        param = param.Trim();
        return param.EndsWith(".rx", StringComparison.OrdinalIgnoreCase)
            || param.EndsWith(".ry", StringComparison.OrdinalIgnoreCase)
            || param.EndsWith(".rz", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a parameter is a vector global (e.g. #CameraRotation, #CameraPosition)
    /// used as a single scalar — which is semantically invalid.
    /// </summary>
    private static bool IsVectorGlobalUsedAsScalar(string param)
    {
        param = param.Trim();
        // #CameraPosition, #CameraRotation, #MouseWorld used without .x/.y/.z component
        if (!param.StartsWith("#")) return false;
        string varName = param.Substring(1);
        // If it already has a component accessor (.x, .y, .z), it's fine
        if (varName.Contains(".")) return false;
        // These globals are vectors — using them bare is wrong
        return varName.Equals("CameraPosition", StringComparison.OrdinalIgnoreCase)
            || varName.Equals("CameraRotation", StringComparison.OrdinalIgnoreCase)
            || varName.Equals("MouseWorld", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a parameter is a self-position reference (this.x, this.y, this.z).
    /// </summary>
    private static bool IsSelfPosition(string param)
    {
        param = param.Trim();
        return param.Equals("this.x", StringComparison.OrdinalIgnoreCase)
            || param.Equals("this.y", StringComparison.OrdinalIgnoreCase)
            || param.Equals("this.z", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a parameter is a self-rotation reference (this.rx, this.ry, this.rz).
    /// </summary>
    private static bool IsSelfRotation(string param)
    {
        param = param.Trim();
        return param.Equals("this.rx", StringComparison.OrdinalIgnoreCase)
            || param.Equals("this.ry", StringComparison.OrdinalIgnoreCase)
            || param.Equals("this.rz", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Split action parameters respecting nested parentheses.
    /// E.g. "this.speed,rand(0,1),this.y" → ["this.speed", "rand(0,1)", "this.y"]
    /// </summary>
    private static List<string> SplitActionParams(string paramString)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(paramString)) return result;

        int depth = 0;
        int start = 0;
        for (int i = 0; i < paramString.Length; i++)
        {
            char c = paramString[i];
            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(paramString.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        result.Add(paramString.Substring(start).Trim());
        return result;
    }

    // ── Custom converter: serialize byte[] as JSON number array, not Base64 ──
    private class ByteArrayJsonConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetBytesFromBase64();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<byte>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add((byte)reader.GetInt32());
                return list.ToArray();
            }
            throw new JsonException("Expected array or string for byte[]");
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (byte b in value)
                writer.WriteNumberValue(b);
            writer.WriteEndArray();
        }
    }

    // ── Custom converter: ActorJson — omits default-value fields to keep JSON compact ──
    private class ActorJsonCompactConverter : JsonConverter<ActorJson>
    {
        public override ActorJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var actor = new ActorJson();

            foreach (var prop in root.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case "ActorName":       actor.ActorName = prop.Value.GetString(); break;
                    case "Active":          actor.Active = prop.Value.GetBoolean(); break;
                    case "PrefabName":      actor.PrefabName = prop.Value.GetString(); break;
                    case "Tag":             actor.Tag = prop.Value.GetString(); break;
                    case "Position":        actor.Position = ReadFloatArray(prop.Value); break;
                    case "Rotation":        actor.Rotation = ReadFloatArray(prop.Value); break;
                    case "Scale":           actor.Scale = ReadFloatArray(prop.Value); break;
                    case "Size":            actor.Size = ReadFloatArray(prop.Value); break;
                    case "Velocity":        actor.Velocity = ReadFloatArray(prop.Value); break;
                    case "AngularVelocity": actor.AngularVelocity = ReadFloatArray(prop.Value); break;
                    case "Density":         actor.Density = prop.Value.GetSingle(); break;
                    case "Friction":        actor.Friction = prop.Value.GetSingle(); break;
                    case "Bounciness":      actor.Bounciness = prop.Value.GetSingle(); break;
                    case "Drag":            actor.Drag = prop.Value.GetSingle(); break;
                    case "Properties":
                        actor.Properties = new List<string>();
                        foreach (var item in prop.Value.EnumerateArray())
                            actor.Properties.Add(item.GetString());
                        break;
                    case "Script":
                        actor.Script = new List<SentenceJson>();
                        foreach (var ruleEl in prop.Value.EnumerateArray())
                        {
                            var rule = new SentenceJson();
                            if (ruleEl.TryGetProperty("When", out var whenEl))
                            {
                                rule.When = new List<string>();
                                foreach (var w in whenEl.EnumerateArray())
                                    rule.When.Add(w.GetString());
                            }
                            if (ruleEl.TryGetProperty("Do", out var doEl))
                            {
                                rule.Do = new List<string>();
                                foreach (var d in doEl.EnumerateArray())
                                    rule.Do.Add(d.GetString());
                            }
                            actor.Script.Add(rule);
                        }
                        break;
                }
            }
            return actor;
        }

        public override void Write(Utf8JsonWriter writer, ActorJson actor, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (actor.ActorName != null)  writer.WriteString("ActorName", actor.ActorName);
            writer.WriteBoolean("Active", actor.Active);
            if (actor.PrefabName != null) writer.WriteString("PrefabName", actor.PrefabName);
            if (actor.Tag != null)        writer.WriteString("Tag", actor.Tag);

            WriteFloatArrayIf(writer, "Position", actor.Position);
            WriteFloatArrayIf(writer, "Rotation", actor.Rotation);
            WriteFloatArrayIf(writer, "Scale", actor.Scale);
            WriteFloatArrayIf(writer, "Size", actor.Size);
            WriteFloatArrayIf(writer, "Velocity", actor.Velocity);
            WriteFloatArrayIf(writer, "AngularVelocity", actor.AngularVelocity);

            if (actor.Density != 0f)   writer.WriteNumber("Density", actor.Density);
            if (actor.Friction != 0f)  writer.WriteNumber("Friction", actor.Friction);
            if (actor.Bounciness != 0f) writer.WriteNumber("Bounciness", actor.Bounciness);
            if (actor.Drag != 0f)      writer.WriteNumber("Drag", actor.Drag);

            if (actor.Properties != null && actor.Properties.Count > 0)
            {
                writer.WritePropertyName("Properties");
                JsonSerializer.Serialize(writer, actor.Properties, options);
            }

            if (actor.Script != null && actor.Script.Count > 0)
            {
                writer.WritePropertyName("Script");
                writer.WriteStartArray();
                foreach (var rule in actor.Script)
                {
                    writer.WriteStartObject();
                    if (rule.When != null && rule.When.Count > 0)
                    {
                        writer.WritePropertyName("When");
                        JsonSerializer.Serialize(writer, rule.When, options);
                    }
                    if (rule.Do != null && rule.Do.Count > 0)
                    {
                        writer.WritePropertyName("Do");
                        JsonSerializer.Serialize(writer, rule.Do, options);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static float[] ReadFloatArray(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array) return null;
            var arr = new float[el.GetArrayLength()];
            int i = 0;
            foreach (var item in el.EnumerateArray())
                arr[i++] = item.GetSingle();
            return arr;
        }

        private static void WriteFloatArrayIf(Utf8JsonWriter writer, string name, float[] arr)
        {
            if (arr == null) return;
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var v in arr) writer.WriteNumberValue(v);
            writer.WriteEndArray();
        }
    }

    // ── Custom converter: CustomVariable — only writes the value field matching the type ──
    private class CustomVariableCompactConverter : JsonConverter<CustomVariable>
    {
        public override CustomVariable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var cv = new CustomVariable();

            if (root.TryGetProperty("name", out var v))       cv.name = v.GetString();
            if (root.TryGetProperty("type", out v))            cv.type = v.GetString();
            if (root.TryGetProperty("intValue", out v))        cv.intValue = v.GetInt32();
            if (root.TryGetProperty("floatValue", out v))      cv.floatValue = v.GetSingle();
            if (root.TryGetProperty("boolValue", out v))       cv.boolValue = v.GetBoolean();
            if (root.TryGetProperty("arrayValue", out v) && v.ValueKind == JsonValueKind.Array)
            {
                cv.arrayValue = new float[v.GetArrayLength()];
                int i = 0;
                foreach (var item in v.EnumerateArray())
                    cv.arrayValue[i++] = item.GetSingle();
            }

            return cv;
        }

        public override void Write(Utf8JsonWriter writer, CustomVariable cv, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (cv.name != null) writer.WriteString("name", cv.name);
            if (cv.type != null) writer.WriteString("type", cv.type);

            switch (cv.type?.ToLower())
            {
                case "int":
                    writer.WriteNumber("intValue", cv.intValue);
                    break;
                case "float":
                    writer.WriteNumber("floatValue", cv.floatValue);
                    break;
                case "bool":
                    writer.WriteBoolean("boolValue", cv.boolValue);
                    break;
                case "vector2":
                case "vector3":
                    if (cv.arrayValue != null)
                    {
                        writer.WritePropertyName("arrayValue");
                        writer.WriteStartArray();
                        foreach (var val in cv.arrayValue)
                            writer.WriteNumberValue(val);
                        writer.WriteEndArray();
                    }
                    break;
                default:
                    if (cv.intValue != 0) writer.WriteNumber("intValue", cv.intValue);
                    if (cv.floatValue != 0f) writer.WriteNumber("floatValue", cv.floatValue);
                    if (cv.boolValue) writer.WriteBoolean("boolValue", cv.boolValue);
                    if (cv.arrayValue != null)
                    {
                        writer.WritePropertyName("arrayValue");
                        writer.WriteStartArray();
                        foreach (var val in cv.arrayValue)
                            writer.WriteNumberValue(val);
                        writer.WriteEndArray();
                    }
                    break;
            }

            writer.WriteEndObject();
        }
    }

    // ── JSON serialization options ──
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = true,
        PropertyNamingPolicy = null,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new ByteArrayJsonConverter(), new ActorJsonCompactConverter(), new CustomVariableCompactConverter() }
    };

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  AUDIT RESULT
    // ══════════════════════════════════════════════════════════

    public class AuditResult
    {
        public bool IsValid = true;
        public string Error;
        public List<string> Fixes = new List<string>();
        public List<string> Warnings = new List<string>();

        /// <summary>Fixed actor (only for single-actor audits).</summary>
        public ActorJson FixedActor;

        /// <summary>Fixed game (only for full-game audits).</summary>
        public SceneJson FixedGame;

        /// <summary>Formatted JSON string of the fixed result.</summary>
        public string FixedJson;

        /// <summary>
        /// Global variable names referenced in Script but not declared
        /// in CustomVariables or built-ins. The UI should ask the user
        /// which type to assign before creating them.
        /// </summary>
        public List<string> PendingGlobals = new List<string>();
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  AUDIT A SINGLE ACTOR
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Audit a single actor JSON string. Parses, validates, fixes missing
    /// properties, and returns the corrected actor.
    /// </summary>
    public static AuditResult AuditActor(string actorJson)
    {
        var result = new AuditResult();

        // Phase 0: Proactive sanitization (always, before anything else)
        try { actorJson = SanitizeRawJson(actorJson, result); }
        catch (Exception ex) { result.Warnings.Add($"Error en sanitización inicial: {ex.Message}"); }

        // Phase 1: Fix braces (best-effort)
        try { actorJson = FixBracePairs(actorJson, result); }
        catch (Exception ex) { result.Warnings.Add($"Error reparando llaves: {ex.Message}"); }

        // Phase 2: Parse (with repair retry)
        ActorJson actor = null;
        try
        {
            actor = JsonSerializer.Deserialize<ActorJson>(actorJson, JsonOptions);
        }
        catch (JsonException)
        {
            // First parse failed — attempt JSON repair and retry
            try
            {
                string repaired = RepairJsonString(actorJson);
                actor = JsonSerializer.Deserialize<ActorJson>(repaired, JsonOptions);
                result.Fixes.Add("JSON reparado automáticamente (claves sin comillas, comas extra, etc).");
                actorJson = repaired;
            }
            catch (Exception retryEx)
            {
                // Both attempts failed — set error but DON'T return yet
                result.IsValid = false;
                result.Error = $"JSON inválido tras reparación: {retryEx.Message}";
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Error = $"Error parseando actor: {ex.Message}";
        }

        if (actor == null && result.IsValid)
        {
            result.IsValid = false;
            result.Error = "No se pudo parsear el JSON del actor.";
        }

        // Even if parse failed, return result (caller handles null FixedActor)
        if (actor == null)
            return result;

        // Phase 3: Audit (non-blocking — errors become warnings)
        try { AuditActorJson(actor, null, null, result); }
        catch (Exception ex) { result.Warnings.Add($"Error en auditoría: {ex.Message}"); }

        // Phase 4: Serialize result (always produce output if actor parsed)
        result.IsValid = true; // override: if we got an actor, consider it valid
        result.Error = null;
        result.FixedActor = actor;
        try
        {
            result.FixedJson = FormatGameJson(JsonSerializer.Serialize(actor, JsonOptions));
        }
        catch (Exception ex)
        {
            // Fallback without formatting
            try { result.FixedJson = JsonSerializer.Serialize(actor, JsonOptions); }
            catch { result.FixedJson = actorJson; }
            result.Warnings.Add($"Error formateando JSON del actor: {ex.Message}");
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  AUDIT FULL GAME
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Audit the entire game JSON. Checks each actor individually and
    /// validates cross-actor references.
    /// </summary>
    public static AuditResult AuditGame(string gameJson)
    {
        var result = new AuditResult();

        // Sanitize raw JSON
        try { gameJson = SanitizeRawJson(gameJson, result); }
        catch (Exception ex) { result.Warnings.Add($"Error en sanitización: {ex.Message}"); }

        // Fix braces (best-effort)
        try { gameJson = FixBracePairs(gameJson, result); }
        catch (Exception ex) { result.Warnings.Add($"Error reparando llaves: {ex.Message}"); }

        try
        {
            var game = JsonSerializer.Deserialize<SceneJson>(gameJson, JsonOptions);
            if (game == null)
            {
                result.IsValid = false;
                result.Error = "No se pudo parsear el JSON del juego.";
                return result;
            }

            if (game.Cast == null)
            {
                game.Cast = new List<ActorJson>();
                result.Fixes.Add("Añadido array Cast vacío.");
            }

            // Collect all actor names and their declared properties
            var actorProperties = BuildActorPropertyMap(game.Cast);
            var actorNames = new HashSet<string>(actorProperties.Keys, StringComparer.OrdinalIgnoreCase);

            // Collect custom variable names
            var customVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (game.CustomVariables != null)
            {
                foreach (var cv in game.CustomVariables)
                    if (!string.IsNullOrEmpty(cv.name))
                        customVarNames.Add(cv.name);
            }

            // Audit each actor (non-blocking per actor)
            foreach (var actor in game.Cast)
            {
                try { AuditActorJson(actor, actorNames, customVarNames, result); }
                catch (Exception ex)
                {
                    result.Warnings.Add($"[{actor.ActorName ?? "???"}] Error en auditoría: {ex.Message}");
                }
            }

            // Cross-actor reference validation (non-blocking per actor)
            foreach (var actor in game.Cast)
            {
                try
                {
                    ValidateCrossActorReferences(actor, actorProperties, result);
                    ValidateGlobalReferences(actor, customVarNames, result);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"[{actor.ActorName ?? "???"}] Error validando refs: {ex.Message}");
                }
            }

            // Clean floating-point noise in SceneJson-level fields
            try { CleanFloatNoiseInSceneJson(game, result); }
            catch (Exception ex) { result.Warnings.Add($"Error limpiando ruido flotante global: {ex.Message}"); }

            result.FixedGame = game;
            try
            {
                result.FixedJson = FormatGameJson(JsonSerializer.Serialize(game, JsonOptions));
            }
            catch (Exception ex)
            {
                // Fallback: serialize without formatting
                result.FixedJson = JsonSerializer.Serialize(game, JsonOptions);
                result.Warnings.Add($"Error formateando JSON: {ex.Message}");
            }
        }
        catch (JsonException jex)
        {
            result.IsValid = false;
            result.Error = $"JSON de juego inválido: {jex.Message}";
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Error = $"Error auditando juego: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Audit a SceneJson object directly (not from string).
    /// </summary>
    public static AuditResult AuditSceneJson(SceneJson game)
    {
        string json = JsonSerializer.Serialize(game, JsonOptions);
        return AuditGame(json);
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  CORE AUDIT LOGIC
    // ══════════════════════════════════════════════════════════

    private static void AuditActorJson(
        ActorJson actor,
        HashSet<string> allActorNames,
        HashSet<string> customVarNames,
        AuditResult result)
    {
        string label = actor.ActorName ?? "???";

        // 1. Validate required fields
        try
        {
            if (string.IsNullOrEmpty(actor.ActorName))
                result.Warnings.Add("[???] Falta ActorName.");

            if (string.IsNullOrEmpty(actor.PrefabName))
            {
                actor.PrefabName = actor.ActorName ?? "UnknownPrefab";
                result.Fixes.Add($"[{label}] Añadido PrefabName = '{actor.PrefabName}'");
            }
        }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error validando campos: {ex.Message}"); }

        // 2-4. Property cross-reference check
        try
        {
            var declaredProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (actor.Properties != null)
            {
                foreach (var prop in actor.Properties)
                {
                    if (!string.IsNullOrEmpty(prop))
                    {
                        string name = prop.Split('=')[0].Trim();
                        if (!string.IsNullOrEmpty(name))
                            declaredProps.Add(name);
                    }
                }
            }

            var referencedProps = ExtractThisReferences(actor);

            var missingProps = new List<string>();
            foreach (var prop in referencedProps)
            {
                if (!BuiltInProperties.Contains(prop) && !declaredProps.Contains(prop))
                    missingProps.Add(prop);
            }

            if (missingProps.Count > 0)
            {
                if (actor.Properties == null)
                    actor.Properties = new List<string>();

                foreach (var missing in missingProps.OrderBy(p => p))
                {
                    actor.Properties.Add($"{missing}=0");
                    result.Fixes.Add($"[{label}] Añadida propiedad faltante: '{missing}=0'");
                }
            }
        }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error revisando propiedades: {ex.Message}"); }

        // 5. Validate Script structure
        try { ValidateSentenceJsons(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error validando Script: {ex.Message}"); }

        // 6. Check for duplicate properties
        try { CheckDuplicateProperties(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error comprobando duplicados: {ex.Message}"); }

        // 7. Validate action signatures (arity + semantic params)
        try { ValidateAndFixActions(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error validando acciones: {ex.Message}"); }

        // 8. Strip empty/default attributes (Tag="", zero vectors, etc.)
        try { StripDefaultAttributes(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error eliminando atributos vacíos: {ex.Message}"); }

        // 9. Round float vectors to remove precision artifacts (e.g. 1.20000005 → 1.2)
        try { RoundVectorArrays(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error redondeando vectores: {ex.Message}"); }

        // 10. Clean floating-point noise in Properties and Script strings
        try { CleanFloatNoiseInStrings(actor, result); }
        catch (Exception ex) { result.Warnings.Add($"[{label}] Error limpiando ruido de coma flotante: {ex.Message}"); }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  PROPERTY EXTRACTION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Extract all this.PROPERTY references from an actor's Script rules.
    /// </summary>
    private static HashSet<string> ExtractThisReferences(ActorJson actor)
    {
        var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (actor.Script == null) return props;

        foreach (var rule in actor.Script)
        {
            ExtractFromStringList(rule.When, ThisPropRegex, props, 1);
            ExtractFromStringList(rule.Do, ThisPropRegex, props, 1);
        }

        return props;
    }

    /// <summary>
    /// Extract all cross-actor references (ActorName.property) from an actor's Script.
    /// Returns dictionary: ActorName → set of referenced properties.
    /// </summary>
    private static Dictionary<string, HashSet<string>> ExtractCrossActorReferences(ActorJson actor)
    {
        var refs = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (actor.Script == null) return refs;

        foreach (var rule in actor.Script)
        {
            ExtractCrossRefsFromList(rule.When, refs);
            ExtractCrossRefsFromList(rule.Do, refs);
        }

        return refs;
    }

    /// <summary>
    /// Extract all #GlobalVariable references from an actor's Script.
    /// </summary>
    private static HashSet<string> ExtractGlobalReferences(ActorJson actor)
    {
        var globals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (actor.Script == null) return globals;

        foreach (var rule in actor.Script)
        {
            ExtractFromStringList(rule.When, GlobalRefRegex, globals, 1);
            ExtractFromStringList(rule.Do, GlobalRefRegex, globals, 1);
        }

        return globals;
    }

    private static void ExtractFromStringList(
        List<string> strings, Regex regex, HashSet<string> output, int groupIndex)
    {
        if (strings == null) return;
        foreach (var s in strings)
        {
            if (string.IsNullOrEmpty(s)) continue;
            foreach (Match m in regex.Matches(s))
                output.Add(m.Groups[groupIndex].Value);
        }
    }

    private static void ExtractCrossRefsFromList(
        List<string> strings, Dictionary<string, HashSet<string>> refs)
    {
        if (strings == null) return;
        foreach (var s in strings)
        {
            if (string.IsNullOrEmpty(s)) continue;
            foreach (Match m in CrossActorRegex.Matches(s))
            {
                string actorName = m.Groups[1].Value;
                string propName = m.Groups[2].Value;

                // Skip if it looks like "this.prop" (shouldn't match due to regex, but be safe)
                if (actorName.Equals("this", StringComparison.OrdinalIgnoreCase)) continue;

                if (!refs.ContainsKey(actorName))
                    refs[actorName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                refs[actorName].Add(propName);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region VALIDATION
    // ══════════════════════════════════════════════════════════

    private static void ValidateSentenceJsons(ActorJson actor, AuditResult result)
    {
        if (actor.Script == null) return;
        string label = actor.ActorName ?? "???";

        for (int i = 0; i < actor.Script.Count; i++)
        {
            var rule = actor.Script[i];
            if (rule.Do == null || rule.Do.Count == 0)
            {
                result.Warnings.Add($"[{label}] Regla #{i} no tiene campo 'Do'.");
            }
        }
    }

    private static void CheckDuplicateProperties(ActorJson actor, AuditResult result)
    {
        if (actor.Properties == null) return;
        string label = actor.ActorName ?? "???";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = actor.Properties.Count - 1; i >= 0; i--)
        {
            string s = actor.Properties[i];
            if (string.IsNullOrEmpty(s)) continue;
            string name = s.Split('=')[0].Trim();

            if (!seen.Add(name))
            {
                actor.Properties.RemoveAt(i);
                result.Fixes.Add($"[{label}] Eliminada propiedad duplicada: '{name}'");
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ACTION ARITY + SEMANTIC VALIDATION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Validate and attempt to fix all action strings in an actor's Script.
    /// Phase 1: Arity validation — check parameter count against ActionRegistry.
    /// Phase 2: Semantic validation — check parameter types match expected roles.
    /// </summary>
    private static void ValidateAndFixActions(ActorJson actor, AuditResult result)
    {
        if (actor?.Script == null) return;
        string label = actor.ActorName ?? "???";

        foreach (var rule in actor.Script)
        {
            if (rule.Do == null) continue;
            for (int i = 0; i < rule.Do.Count; i++)
            {
                string action = rule.Do[i];
                if (string.IsNullOrEmpty(action)) continue;

                var m = ActionParseRegex.Match(action);
                if (!m.Success)
                {
                    // Not parseable as Action(params) — could be "LoadScene()" or "QuitGame()"
                    // Try without params
                    if (action.EndsWith("()"))
                    {
                        string nameOnly = action.Substring(0, action.Length - 2);
                        if (ActionRegistry.ContainsKey(nameOnly)) continue; // valid 0-param action
                    }
                    result.Warnings.Add($"[{label}] Acción no reconocida o mal formada: '{action}'");
                    continue;
                }

                string actionName = m.Groups[1].Value;
                string paramStr = m.Groups[2].Value;
                var paramList = SplitActionParams(paramStr);
                int paramCount = paramList.Count;

                // Handle empty-param case: "Action()" parsed as 1 param = ""
                if (paramCount == 1 && string.IsNullOrWhiteSpace(paramList[0]))
                    paramCount = 0;

                // Look up action definition
                if (!ActionRegistry.TryGetValue(actionName, out var actionDef))
                {
                    result.Warnings.Add($"[{label}] Acción desconocida: '{actionName}'. " +
                        "Acciones válidas: Move, MoveTo, NavigateTo, Rotate, RotateTo, Push, PushTo, " +
                        "Torque, Edit, Spawn, Delete, Animate, PlaySound, PlayParticles, LoadScene, QuitGame.");
                    continue;
                }

                // ── Phase 1: ARITY VALIDATION ──
                bool arityValid = false;
                int bestArity = actionDef.ValidArities[0];
                ParamType[] bestSchema = actionDef.ParamSchemas[0];

                for (int a = 0; a < actionDef.ValidArities.Length; a++)
                {
                    if (paramCount == actionDef.ValidArities[a])
                    {
                        arityValid = true;
                        bestArity = actionDef.ValidArities[a];
                        bestSchema = actionDef.ParamSchemas[a];
                        break;
                    }
                }

                if (!arityValid)
                {
                    string expected = string.Join(" o ", actionDef.ValidArities);
                    string fixedAction = TryFixArity(actionName, paramList, actionDef, result, label);
                    if (fixedAction != null)
                    {
                        rule.Do[i] = fixedAction;
                        result.Fixes.Add($"[{label}] Aridad de '{actionName}' corregida: " +
                            $"{paramCount} → {expected} params. Firma: {actionDef.HumanSignature}");
                        // Re-parse to continue with semantic validation
                        m = ActionParseRegex.Match(fixedAction);
                        if (m.Success)
                        {
                            paramStr = m.Groups[2].Value;
                            paramList = SplitActionParams(paramStr);
                            paramCount = paramList.Count;
                            if (paramCount == 1 && string.IsNullOrWhiteSpace(paramList[0]))
                                paramCount = 0;
                            // Find matching schema
                            for (int a = 0; a < actionDef.ValidArities.Length; a++)
                            {
                                if (paramCount == actionDef.ValidArities[a])
                                {
                                    bestSchema = actionDef.ParamSchemas[a];
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"[{label}] '{actionName}' tiene {paramCount} params, " +
                            $"pero espera {expected}. Firma correcta: {actionDef.HumanSignature}");
                        continue; // Can't validate semantics if arity is wrong
                    }
                }

                // ── Phase 2: SEMANTIC VALIDATION ──
                if (paramCount > 0 && paramCount <= bestSchema.Length)
                {
                    ValidateActionSemantics(label, actionName, paramList, bestSchema, rule, i, result);
                }
            }
        }
    }

    /// <summary>
    /// Try to fix an action with wrong arity to the correct number of params.
    /// Returns the fixed action string, or null if unfixable.
    /// </summary>
    private static string TryFixArity(
        string actionName, List<string> paramList, ActionDef actionDef,
        AuditResult result, string label)
    {
        int actual = paramList.Count;
        if (actual == 1 && string.IsNullOrWhiteSpace(paramList[0])) actual = 0;

        // Find the closest valid arity
        int targetArity = actionDef.ValidArities[0];
        int minDiff = Math.Abs(actual - targetArity);
        int targetIdx = 0;
        for (int a = 1; a < actionDef.ValidArities.Length; a++)
        {
            int diff = Math.Abs(actual - actionDef.ValidArities[a]);
            if (diff < minDiff)
            {
                minDiff = diff;
                targetArity = actionDef.ValidArities[a];
                targetIdx = a;
            }
        }

        var schema = actionDef.ParamSchemas[targetIdx];

        // Special case: RotateTo with 4 params → expand to 7 (add this.x, this.y, this.z as pivot)
        if (actionName.Equals("RotateTo", StringComparison.OrdinalIgnoreCase) && actual == 4)
        {
            return $"RotateTo({paramList[0]},{paramList[1]},{paramList[2]},{paramList[3]},this.x,this.y,this.z)";
        }

        // Special case: RotateTo with 5 or 6 params → try to expand to 7
        if (actionName.Equals("RotateTo", StringComparison.OrdinalIgnoreCase) && (actual == 5 || actual == 6))
        {
            // If 5 params: speed, t1, t2, t3, extraParam → insert this.x,this.y,this.z as last 3
            // Check if the 5th param is a vector global like #CameraRotation
            if (actual == 5 && IsVectorGlobalUsedAsScalar(paramList[4]))
            {
                // The user probably meant the global's components as pivot
                string globalBase = paramList[4].Substring(1); // remove #
                return $"RotateTo({paramList[0]},{paramList[1]},{paramList[2]},{paramList[3]}," +
                       $"#{globalBase}.x,#{globalBase}.y,#{globalBase}.z)";
            }
            // Generic: pad with this.x/y/z for missing pivot params
            var fixedParams = new List<string>(paramList);
            while (fixedParams.Count < 7)
            {
                int idx = fixedParams.Count;
                if (idx == 4) fixedParams.Add("this.x");
                else if (idx == 5) fixedParams.Add("this.y");
                else if (idx == 6) fixedParams.Add("this.z");
                else fixedParams.Add("0");
            }
            return $"RotateTo({string.Join(",", fixedParams)})";
        }

        // Special case: Spawn with 3, 4, 6, 7 params → try to normalize
        if (actionName.Equals("Spawn", StringComparison.OrdinalIgnoreCase))
        {
            if (actual == 3)
            {
                // Spawn(prefab, source, singleOffset) → Spawn(prefab, source, singleOffset, 0, 0)
                return $"Spawn({paramList[0]},{paramList[1]},{paramList[2]},0,0)";
            }
            if (actual == 4)
            {
                return $"Spawn({paramList[0]},{paramList[1]},{paramList[2]},{paramList[3]},0)";
            }
            if (actual >= 6)
            {
                // Manuscript defines Spawn(name, dx,dy,dz, drx,dry,drz) = 7 params
                // But runtime uses Spawn(prefab, source, offX, offY, offZ) = 5 params
                // Keep just first 5
                return $"Spawn({paramList[0]},{paramList[1]},{paramList[2]},{paramList[3]},{paramList[4]})";
            }
        }

        // Too few params: pad with 0
        if (actual < targetArity)
        {
            var fixedParams = new List<string>(paramList);
            while (fixedParams.Count < targetArity)
                fixedParams.Add("0");
            return $"{actionName}({string.Join(",", fixedParams)})";
        }

        // Too many params: trim excess
        if (actual > targetArity)
        {
            var fixedParams = paramList.GetRange(0, targetArity);
            return $"{actionName}({string.Join(",", fixedParams)})";
        }

        return null;
    }

    /// <summary>
    /// Validate individual parameter semantics against expected types.
    /// Produces warnings (and attempts fixes) for mismatched parameter types.
    /// </summary>
    private static void ValidateActionSemantics(
        string label, string actionName, List<string> paramList,
        ParamType[] schema, SentenceJson rule, int actionIdx, AuditResult result)
    {
        bool needsRewrite = false;
        var fixedParams = new List<string>(paramList);

        for (int p = 0; p < Math.Min(paramList.Count, schema.Length); p++)
        {
            string param = paramList[p].Trim();
            ParamType expected = schema[p];

            switch (expected)
            {
                // ── Position params should NOT contain rotation references ──
                case ParamType.PosX:
                case ParamType.PosY:
                case ParamType.PosZ:
                    if (LooksLikeRotation(param))
                    {
                        string axis = expected == ParamType.PosX ? "x" : expected == ParamType.PosY ? "y" : "z";
                        result.Warnings.Add($"[{label}] {actionName}: param #{p + 1} espera posición " +
                            $"({axis}) pero recibió rotación '{param}'. " +
                            $"¿Querías usar .{axis} en vez de .r{axis}?");
                    }
                    if (IsVectorGlobalUsedAsScalar(param))
                    {
                        string axis = expected == ParamType.PosX ? "x" : expected == ParamType.PosY ? "y" : "z";
                        fixedParams[p] = $"{param}.{axis}";
                        needsRewrite = true;
                        result.Fixes.Add($"[{label}] {actionName}: '{param}' es un vector, expandido a '{param}.{axis}'.");
                    }
                    break;

                // ── Rotation params should NOT contain position references (unless in Move/Rotate which uses angles, not pos) ──
                case ParamType.RotX:
                case ParamType.RotY:
                case ParamType.RotZ:
                    // In Move and Rotate, rx/ry/rz are direction angles, so .x/.y/.z are wrong
                    if (actionName.Equals("Move", StringComparison.OrdinalIgnoreCase) ||
                        actionName.Equals("Push", StringComparison.OrdinalIgnoreCase))
                    {
                        // For Move/Push, direction angles are usually literals or this.ry
                        // Position refs like this.x here are suspicious
                        if (LooksLikePosition(param) && !param.Contains("ry") && !param.Contains("rx") && !param.Contains("rz"))
                        {
                            result.Warnings.Add($"[{label}] {actionName}: param #{p + 1} espera ángulo de dirección " +
                                $"pero recibió posición '{param}'. Usa ángulos (0, 90, 180, this.ry, etc.).");
                        }
                    }
                    break;

                // ── Pivot params should usually be this.x/y/z ──
                case ParamType.PivotX:
                case ParamType.PivotY:
                case ParamType.PivotZ:
                    // Pivots that are self-rotation (.rx, .ry, .rz) make no sense
                    if (IsSelfRotation(param))
                    {
                        string axis = expected == ParamType.PivotX ? "x" : expected == ParamType.PivotY ? "y" : "z";
                        fixedParams[p] = $"this.{axis}";
                        needsRewrite = true;
                        result.Fixes.Add($"[{label}] {actionName}: pivote '{param}' es una rotación, " +
                            $"corregido a 'this.{axis}' (posición del actor).");
                    }
                    if (IsVectorGlobalUsedAsScalar(param))
                    {
                        string axis = expected == ParamType.PivotX ? "x" : expected == ParamType.PivotY ? "y" : "z";
                        fixedParams[p] = $"{param}.{axis}";
                        needsRewrite = true;
                        result.Fixes.Add($"[{label}] {actionName}: pivote '{param}' es un vector, expandido a '{param}.{axis}'.");
                    }
                    break;

                // ── Speed/Force should be numeric or a speed-like property ──
                case ParamType.Speed:
                case ParamType.Force:
                    if (LooksLikePosition(param) || LooksLikeRotation(param))
                    {
                        result.Warnings.Add($"[{label}] {actionName}: param #{p + 1} espera " +
                            $"{(expected == ParamType.Speed ? "velocidad" : "fuerza")} (valor numérico), " +
                            $"pero recibió '{param}'. Usa un número o this.speed.");
                    }
                    break;
            }

            // ── Cross-cutting: Self-position as NavigateTo/MoveTo/PushTo target is a no-op ──
            if ((expected == ParamType.PosX || expected == ParamType.PosY || expected == ParamType.PosZ) &&
                (actionName.Equals("NavigateTo", StringComparison.OrdinalIgnoreCase) ||
                 actionName.Equals("MoveTo", StringComparison.OrdinalIgnoreCase) ||
                 actionName.Equals("PushTo", StringComparison.OrdinalIgnoreCase)))
            {
                // Check if ALL position params (indices 1,2,3) are self-references
                if (paramList.Count >= 4 && p == 1) // Only check once at first position param
                {
                    if (IsSelfPosition(paramList[1]) && IsSelfPosition(paramList[2]) && IsSelfPosition(paramList[3]))
                    {
                        result.Warnings.Add($"[{label}] {actionName}: objetivo es this.x/y/z " +
                            $"(ir a sí mismo = no-op). Usa ActorName.x/y/z para ir a otro actor.");
                    }
                }
            }
        }

        // Apply rewrite if we fixed any params
        if (needsRewrite)
        {
            rule.Do[actionIdx] = $"{actionName}({string.Join(",", fixedParams)})";
        }
    }

    private static void ValidateCrossActorReferences(
        ActorJson actor, Dictionary<string, HashSet<string>> actorProperties, AuditResult result)
    {
        string label = actor.ActorName ?? "???";
        var crossRefs = ExtractCrossActorReferences(actor);

        foreach (var kvp in crossRefs)
        {
            string refActorName = kvp.Key;
            var refProps = kvp.Value;

            if (!actorProperties.ContainsKey(refActorName))
            {
                result.Warnings.Add($"[{label}] Referencia a actor inexistente: '{refActorName}'");
                continue;
            }

            var targetDeclaredProps = actorProperties[refActorName];
            foreach (var prop in refProps)
            {
                if (!BuiltInProperties.Contains(prop) && !targetDeclaredProps.Contains(prop))
                {
                    result.Warnings.Add(
                        $"[{label}] Referencia a propiedad no declarada: '{refActorName}.{prop}'");
                }
            }
        }
    }

    private static void ValidateGlobalReferences(
        ActorJson actor, HashSet<string> customVarNames, AuditResult result)
    {
        string label = actor.ActorName ?? "???";
        var globals = ExtractGlobalReferences(actor);

        foreach (var globalName in globals)
        {
            // Remove sub-property (e.g., "CameraPosition" from "#CameraPosition.x")
            string baseName = globalName.Split('.')[0];

            if (!BuiltInGlobals.Contains(baseName) && !customVarNames.Contains(baseName))
            {
                result.Warnings.Add(
                    $"[{label}] Referencia a variable global no definida: '#{baseName}'");

                // Add to pending globals (avoid duplicates)
                if (!result.PendingGlobals.Contains(baseName, StringComparer.OrdinalIgnoreCase))
                    result.PendingGlobals.Add(baseName);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Build a map of ActorName → declared property names from the Cast list.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildActorPropertyMap(List<ActorJson> cast)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var actor in cast)
        {
            if (string.IsNullOrEmpty(actor.ActorName)) continue;
            var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (actor.Properties != null)
            {
                foreach (var p in actor.Properties)
                {
                    if (!string.IsNullOrEmpty(p))
                        props.Add(p.Split('=')[0].Trim());
                }
            }
            map[actor.ActorName] = props;
        }
        return map;
    }

    /// <summary>
    /// Post-process JSON to collapse short arrays onto single lines
    /// for readability (matches the reference game JSON style).
    /// </summary>
    public static string FormatGameJson(string json)
    {
        // Collapse 3-element numeric arrays: [x, y, z]
        json = Regex.Replace(json,
            @"\[\s*\r?\n\s*([-\d.eE+]+)\s*,\s*\r?\n\s*([-\d.eE+]+)\s*,\s*\r?\n\s*([-\d.eE+]+)\s*\r?\n\s*\]",
            "[$1, $2, $3]");

        // Collapse short string arrays (Properties, When, Do) onto fewer lines
        json = Regex.Replace(json,
            @"\[\s*\r?\n((?:\s*""(?:[^""\\]|\\.)*""\s*,?\s*\r?\n)+)\s*\]",
            m =>
            {
                string inner = m.Groups[1].Value;
                var items = Regex.Matches(inner, @"""(?:[^""\\]|\\.)*""")
                    .Cast<Match>()
                    .Select(x => x.Value)
                    .ToList();
                string collapsed = string.Join(", ", items);
                if (collapsed.Length < 120)
                    return $"[{collapsed}]";
                return m.Value; // keep expanded if too long
            });

        return json;
    }

    /// <summary>
    /// Create a default game template with sensible defaults.
    /// </summary>
    public static SceneJson CreateDefaultGame(string gameName = "NEW_GAME")
    {
        return new SceneJson
        {
            GameName = gameName,
            CameraPosition = new float[] { 0, 0, 0 },
            CameraRotation = new float[] { 0, 0, 0 },
            SunPosition = new float[] { 0, 0, 0 },
            SunRotation = new float[] { 0, 0, 0 },
            SunColor = new byte[] { 255, 255, 255 },
            SunAmbientColor = new byte[] { 180, 180, 180 },
            BackgroundColor = new byte[] { 135, 206, 235 },
            Gravity = new float[] { 0, -9.81f, 0 },
            Cast = new List<ActorJson>()
        };
    }

    /// <summary>
    /// Serialize a SceneJson object to formatted JSON.
    /// </summary>
    public static string SerializeGame(SceneJson game)
    {
        string json = JsonSerializer.Serialize(game, JsonOptions);
        return FormatGameJson(json);
    }

    /// <summary>
    /// Deserialize a game JSON string to SceneJson.
    /// </summary>
    public static SceneJson DeserializeGame(string json)
    {
        return JsonSerializer.Deserialize<SceneJson>(json, JsonOptions);
    }

    /// <summary>
    /// Deserialize an actor JSON string to ActorJson.
    /// </summary>
    public static ActorJson DeserializeActor(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ActorJson>(json, JsonOptions);
        }
        catch
        {
            // Attempt repair before giving up
            string repaired = RepairJsonString(json);
            return JsonSerializer.Deserialize<ActorJson>(repaired, JsonOptions);
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  THINK TAG HANDLING
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Regex to match &lt;think&gt;...&lt;/think&gt; blocks (including multiline content).
    /// </summary>
    private static readonly Regex ThinkTagRegex =
        new Regex(@"<think>[\s\S]*?</think>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extract the content inside &lt;think&gt;...&lt;/think&gt; tags from a response.
    /// Returns the reasoning text (without the tags), or null if no think block found.
    /// </summary>
    public static string ExtractThinkContent(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        var match = ThinkTagRegex.Match(response);
        if (!match.Success) return null;

        // Extract inner content between <think> and </think>
        string block = match.Value;
        int startTag = block.IndexOf(">", StringComparison.Ordinal) + 1;
        int endTag = block.LastIndexOf("<", StringComparison.Ordinal);
        if (startTag < 0 || endTag < 0 || endTag <= startTag) return null;

        return block.Substring(startTag, endTag - startTag).Trim();
    }

    /// <summary>
    /// Remove all &lt;think&gt;...&lt;/think&gt; blocks from a response string.
    /// Works for models with and without think tags — if none present, returns input unchanged.
    /// </summary>
    public static string StripThinkTags(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return response;
        return ThinkTagRegex.Replace(response, "").Trim();
    }

    /// <summary>
    /// Try to extract a valid JSON block from raw LLM output.
    /// Handles cases where the LLM wraps JSON in text or markdown.
    /// Automatically strips &lt;think&gt; tags before extraction.
    /// </summary>
    public static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        // Strip <think>...</think> tags before JSON extraction
        string trimmed = StripThinkTags(response).Trim();

        // If it starts with { and ends with }, it's probably clean JSON
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            return trimmed;

        // Try to extract from markdown code block
        var codeBlockMatch = Regex.Match(trimmed, @"```(?:json)?\s*\r?\n([\s\S]*?)\r?\n\s*```");
        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups[1].Value.Trim();

        // Find first { and last matching }
        int depth = 0;
        int start = -1;
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '{')
            {
                if (start < 0) start = i;
                depth++;
            }
            else if (trimmed[i] == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                    return trimmed.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  BRACE-PAIR FIXING
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Validate and fix unbalanced brace/bracket pairs in raw JSON.
    /// Removes extra closing braces/brackets and appends missing ones.
    /// Re-indents the result if any repair was needed.
    /// </summary>
    public static string FixBracePairs(string json, AuditResult result = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        bool inString = false;
        bool escape = false;
        int curly = 0, square = 0;

        // First pass: find indices of extra closing braces/brackets
        var toRemove = new List<int>();
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;

            switch (c)
            {
                case '{': curly++; break;
                case '}':
                    curly--;
                    if (curly < 0) { toRemove.Add(i); curly = 0; }
                    break;
                case '[': square++; break;
                case ']':
                    square--;
                    if (square < 0) { toRemove.Add(i); square = 0; }
                    break;
            }
        }

        bool modified = false;

        // Remove extra closers (reverse order to preserve indices)
        if (toRemove.Count > 0)
        {
            var chars = new List<char>(json);
            for (int i = toRemove.Count - 1; i >= 0; i--)
                chars.RemoveAt(toRemove[i]);
            json = new string(chars.ToArray());
            modified = true;
            result?.Fixes.Add($"Eliminadas {toRemove.Count} llaves/corchetes sobrantes.");
        }

        // Second pass: count remaining unclosed openers
        curly = 0; square = 0;
        inString = false; escape = false;
        foreach (char c in json)
        {
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            switch (c)
            {
                case '{': curly++; break;
                case '}': curly--; break;
                case '[': square++; break;
                case ']': square--; break;
            }
        }

        // Append missing closers
        if (curly > 0 || square > 0)
        {
            var sb = new System.Text.StringBuilder(json.TrimEnd());
            for (int i = 0; i < square; i++) sb.Append(']');
            for (int i = 0; i < curly; i++) sb.Append('}');
            json = sb.ToString();
            modified = true;
            if (curly > 0) result?.Fixes.Add($"Añadidas {curly} '}}' faltantes al final.");
            if (square > 0) result?.Fixes.Add($"Añadidos {square} ']' faltantes al final.");
        }

        // Re-indent via parse+serialize if we had to repair
        if (modified)
        {
            try
            {
                using var doc = JsonDocument.Parse(json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                json = FormatGameJson(JsonSerializer.Serialize(doc.RootElement, JsonOptions));
                result?.Fixes.Add("JSON re-indentado tras reparar llaves.");
            }
            catch
            {
                // If it still doesn't parse, return the best-effort repair
            }
        }

        return json;
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region DEFAULT ATTRIBUTE STRIPPING
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Remove empty or default-value attributes from an actor.
    /// Does NOT touch Properties or Script.
    /// </summary>
    private static void StripDefaultAttributes(ActorJson actor, AuditResult result)
    {
        string label = actor.ActorName ?? "???";

        if (actor.Tag != null && actor.Tag.Length == 0)
        {
            actor.Tag = null;
            result.Fixes.Add($"[{label}] Eliminado Tag vacío.");
        }

        // Non-nullable value-type fields: reset to 0 if they were noise
        // (These are handled by the compact converter — no need to null them)

        if (IsZeroVector3(actor.Position))
        {
            actor.Position = null;
            result.Fixes.Add($"[{label}] Eliminado Position=[0,0,0] (por defecto).");
        }

        if (IsZeroVector3(actor.Rotation))
        {
            actor.Rotation = null;
            result.Fixes.Add($"[{label}] Eliminado Rotation=[0,0,0] (por defecto).");
        }

        if (IsDefaultScale(actor.Scale))
        {
            actor.Scale = null;
            result.Fixes.Add($"[{label}] Eliminado Scale=[1,1,1] (por defecto).");
        }

        if (IsZeroVector3(actor.Velocity))
        {
            actor.Velocity = null;
            result.Fixes.Add($"[{label}] Eliminado Velocity=[0,0,0] (por defecto).");
        }

        if (IsZeroVector3(actor.AngularVelocity))
        {
            actor.AngularVelocity = null;
            result.Fixes.Add($"[{label}] Eliminado AngularVelocity=[0,0,0] (por defecto).");
        }
    }

    private static bool IsZeroVector3(float[] v)
    {
        return v != null && (v.Length == 0 || (v.Length == 3 && v[0] == 0f && v[1] == 0f && v[2] == 0f));
    }

    private static bool IsDefaultScale(float[] v)
    {
        return v != null && (v.Length == 0 || (v.Length == 3 && v[0] == 1f && v[1] == 1f && v[2] == 1f));
    }

    private static bool IsEmptyArray(float[] v)
    {
        return v != null && v.Length == 0;
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  FLOAT ROUNDING
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Round all float vector arrays on an actor to remove IEEE-754
    /// precision artifacts produced by LLM serialization or Unity.
    /// E.g. 1.20000005 → 1.2,  0.29999998 → 0.3
    /// Uses 4 decimal places which is more than enough for game coordinates.
    /// </summary>
    private static void RoundVectorArrays(ActorJson actor, AuditResult result)
    {
        string label = actor.ActorName ?? "???";
        bool anyChanged = false;

        anyChanged |= RoundArray(actor.Position);
        anyChanged |= RoundArray(actor.Rotation);
        anyChanged |= RoundArray(actor.Scale);
        anyChanged |= RoundArray(actor.Velocity);
        anyChanged |= RoundArray(actor.AngularVelocity);

        // Non-nullable floats: round directly
        {
            float r = RoundFloat(actor.Density);
            if (r != actor.Density) { actor.Density = r; anyChanged = true; }
        }
        {
            float r = RoundFloat(actor.Friction);
            if (r != actor.Friction) { actor.Friction = r; anyChanged = true; }
        }
        {
            float r = RoundFloat(actor.Bounciness);
            if (r != actor.Bounciness) { actor.Bounciness = r; anyChanged = true; }
        }
        {
            float r = RoundFloat(actor.Drag);
            if (r != actor.Drag) { actor.Drag = r; anyChanged = true; }
        }

        if (anyChanged)
            result.Fixes.Add($"[{label}] Redondeados valores float para eliminar artefactos de precisión.");
    }

    /// <summary>Round each element of a float array in-place. Returns true if any value changed.</summary>
    private static bool RoundArray(float[] arr)
    {
        if (arr == null) return false;
        bool changed = false;
        for (int i = 0; i < arr.Length; i++)
        {
            float rounded = RoundFloat(arr[i]);
            if (rounded != arr[i])
            {
                arr[i] = rounded;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Round a float to 4 decimal places to remove IEEE-754 noise.</summary>
    private static float RoundFloat(float v)
    {
        return (float)Math.Round(v, 4);
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  FLOAT NOISE CLEANING IN STRINGS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Regex to match floating-point literals with excessive decimal digits (≥5).
    /// Captures patterns like 5.0000000000007, 6.000000699999, 1.20000005.
    /// The negative lookbehind avoids matching inside identifiers.
    /// </summary>
    private static readonly Regex ExcessiveDecimalRegex =
        new Regex(@"(?<!\w)-?\d+\.\d{5,}", RegexOptions.Compiled);

    /// <summary>
    /// Clean floating-point precision noise from a text string.
    /// Rounds numbers with excessive decimal places (≥5 digits after the dot)
    /// to 4 decimal digits, then trims trailing zeros.
    /// E.g. "Move(5.0000000000007, 0, 6.000000699999, 0)" → "Move(5, 0, 6, 0)"
    ///      "speed=1.20000005" → "speed=1.2"
    /// </summary>
    public static string CleanFloatNoise(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return ExcessiveDecimalRegex.Replace(text, m =>
        {
            if (double.TryParse(m.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double val))
            {
                double rounded = Math.Round(val, 4);
                // "G" format trims trailing zeros automatically: 5.0000 → "5", 1.2000 → "1.2"
                return rounded.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            }
            return m.Value;
        });
    }

    /// <summary>
    /// Clean floating-point noise in all string fields of an actor:
    /// Properties (e.g. "speed=5.0000000000007" → "speed=5") and
    /// Script When/Do entries (e.g. "Move(1.20000005,0,0,0)" → "Move(1.2,0,0,0)").
    /// </summary>
    private static void CleanFloatNoiseInStrings(ActorJson actor, AuditResult result)
    {
        string label = actor.ActorName ?? "???";
        bool anyChanged = false;

        // Clean Properties
        if (actor.Properties != null)
        {
            for (int i = 0; i < actor.Properties.Count; i++)
            {
                string cleaned = CleanFloatNoise(actor.Properties[i]);
                if (cleaned != actor.Properties[i])
                {
                    actor.Properties[i] = cleaned;
                    anyChanged = true;
                }
            }
        }

        // Clean Script When/Do strings
        if (actor.Script != null)
        {
            foreach (var rule in actor.Script)
            {
                if (rule.When != null)
                {
                    for (int i = 0; i < rule.When.Count; i++)
                    {
                        string cleaned = CleanFloatNoise(rule.When[i]);
                        if (cleaned != rule.When[i])
                        {
                            rule.When[i] = cleaned;
                            anyChanged = true;
                        }
                    }
                }
                if (rule.Do != null)
                {
                    for (int i = 0; i < rule.Do.Count; i++)
                    {
                        string cleaned = CleanFloatNoise(rule.Do[i]);
                        if (cleaned != rule.Do[i])
                        {
                            rule.Do[i] = cleaned;
                            anyChanged = true;
                        }
                    }
                }
            }
        }

        if (anyChanged)
            result.Fixes.Add($"[{label}] Limpiado ruido de coma flotante en Properties/Script.");
    }

    /// <summary>
    /// Clean floating-point noise in SceneJson-level fields:
    /// CameraPosition, CameraRotation, SunPosition, SunRotation,
    /// and CustomVariables float values.
    /// </summary>
    private static void CleanFloatNoiseInSceneJson(SceneJson game, AuditResult result)
    {
        bool anyChanged = false;

        anyChanged |= RoundArray(game.CameraPosition);
        anyChanged |= RoundArray(game.CameraRotation);
        anyChanged |= RoundArray(game.SunPosition);
        anyChanged |= RoundArray(game.SunRotation);

        if (game.CustomVariables != null)
        {
            foreach (var cv in game.CustomVariables)
            {
                float rounded = RoundFloat(cv.floatValue);
                if (rounded != cv.floatValue)
                {
                    cv.floatValue = rounded;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
            result.Fixes.Add("[Game] Limpiado ruido de coma flotante en datos globales.");
    }

    // ════════════════════════════════════════════════════════
    #endregion
    #region  JSON STRING REPAIR
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Low-level sanitization pass: strips characters and patterns
    /// that are never valid in JSON but LLMs commonly produce.
    /// Runs BEFORE any parse attempt.
    /// </summary>
    public static string SanitizeRawJson(string json, AuditResult result = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        string original = json;

        // 1. Strip BOM and zero-width characters
        json = json.Trim('\uFEFF', '\u200B', '\u200C', '\u200D', '\u00A0');

        // 2. Remove // line comments (outside strings)
        json = StripLineComments(json);

        // 3. Remove /* block comments */ (outside strings)
        json = StripBlockComments(json);

        // 4. Replace parentheses used as brackets: ( ) → [ ]  (outside strings)
        json = ReplaceParentheses(json);

        // 5. Remove stray semicolons outside strings
        json = RemoveStrayChars(json, ';');

        // 6. Remove stray backslash-newlines (line continuations)
        json = Regex.Replace(json, @"\\\r?\n", "");

        // 7. Strip control characters (except \n, \r, \t) outside strings
        json = StripControlChars(json);

        if (json != original)
            result?.Fixes.Add("Sanitizado: eliminados caracteres o patrones inválidos del JSON.");

        return json;
    }

    /// <summary>
    /// Attempt to repair common LLM JSON mistakes:
    ///   • Unquoted property names   (Do: [...] → "Do": [...])
    ///   • Single-quoted strings      ('value' → "value")
    ///   • Trailing commas            ([1,2,] → [1,2])
    ///   • Duplicate commas           (,,  → ,)
    ///   • Missing commas between elements
    ///   • Equals used instead of colon in keys  ("key" = "val"  → "key": "val")
    /// </summary>
    public static string RepairJsonString(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        // 0. Run sanitization first
        json = SanitizeRawJson(json);

        // 1. Replace single quotes with double quotes (outside existing double-quoted strings)
        json = ReplaceSingleQuotes(json);

        // 2. Quote unquoted property keys:  key: value  →  "key": value
        json = Regex.Replace(json,
            @"(?<=^|[{,]\s*)([a-zA-Z_][a-zA-Z0-9_]*)\s*:",
            "\"$1\":", RegexOptions.Multiline);

        // 3. Replace = used as key-value separator:  "key" = "value"  →  "key": "value"
        json = Regex.Replace(json,
            @"(""[^""]+"")\s*=\s*",
            "$1: ");

        // 4. Remove trailing commas before } or ]
        json = Regex.Replace(json, @",\s*([}\]])", "$1");

        // 5. Remove duplicate/consecutive commas
        json = Regex.Replace(json, @",\s*,+", ",");

        // 6. Remove leading commas after { or [
        json = Regex.Replace(json, @"([{\[])\s*,", "$1");

        // 7. Insert missing commas:  }\n  {  or  ]\n  "  or  "\n  "
        json = Regex.Replace(json,
            @"(\})(\s*\r?\n\s*)(\{)",
            "$1,$2$3");
        json = Regex.Replace(json,
            @"(\}|\]|"")(\s*\r?\n\s*)("")",
            m => m.Groups[1].Value + "," + m.Groups[2].Value + m.Groups[3].Value);

        return json;
    }

    // ─────────────────────────────────────────────
    #endregion
    #region  Sanitization helpers
    // ─────────────────────────────────────────────

    /// <summary>Remove // line comments outside of JSON strings.</summary>
    private static string StripLineComments(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inStr = false, esc = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { sb.Append(c); esc = false; continue; }
            if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
            if (!inStr && c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                if (i < json.Length) sb.Append('\n');
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Remove /* block comments */ outside of JSON strings.</summary>
    private static string StripBlockComments(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inStr = false, esc = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { sb.Append(c); esc = false; continue; }
            if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
            if (!inStr && c == '/' && i + 1 < json.Length && json[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                i++; // skip */
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Replace ( ) with [ ] outside of JSON strings (LLM confuses arrays with parens).</summary>
    private static string ReplaceParentheses(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inStr = false, esc = false;
        bool modified = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { sb.Append(c); esc = false; continue; }
            if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
            if (!inStr)
            {
                if (c == '(') { sb.Append('['); modified = true; continue; }
                if (c == ')') { sb.Append(']'); modified = true; continue; }
            }
            sb.Append(c);
        }
        return modified ? sb.ToString() : json;
    }

    /// <summary>Remove a specific stray character outside of JSON strings.</summary>
    private static string RemoveStrayChars(string json, char stray)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inStr = false, esc = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { sb.Append(c); esc = false; continue; }
            if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
            if (!inStr && c == stray) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Strip control characters (except whitespace) outside JSON strings.</summary>
    private static string StripControlChars(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inStr = false, esc = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { sb.Append(c); esc = false; continue; }
            if (c == '\\' && inStr) { sb.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
            if (!inStr && char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Replace single-quoted strings with double-quoted strings,
    /// being careful not to touch apostrophes inside double-quoted strings.
    /// </summary>
    private static string ReplaceSingleQuotes(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inDouble = false;
        bool inSingle = false;
        bool escape = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (escape) { sb.Append(c); escape = false; continue; }
            if (c == '\\') { sb.Append(c); escape = true; continue; }

            if (c == '"' && !inSingle) { inDouble = !inDouble; sb.Append(c); continue; }
            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                sb.Append('"'); // replace ' with "
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

#endregion
}
