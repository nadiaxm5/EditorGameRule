using System.Collections.Generic;
using System.Text;
using System.Globalization;
using UnityEngine;
using System.Text.RegularExpressions;

namespace GameRuleEditor.Core
{
    /// <summary>
    /// ScriptableObject that represents a complete GameRule project.
    /// This is the central data model that replaces direct JSON editing.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGameRuleProject", menuName = "GameRule/Project", order = 1)]
    public class GameRuleProject : ScriptableObject
    {
        [Header("Project Info")]
        public string projectName = "NewProject";

        // Editor bookkeeping; deliberately excluded from JSON export and save-point comparisons.
        [SerializeField, HideInInspector] internal string jsonFileName;

        [System.Serializable]
        private class EditableState
        {
            public string projectName;
            public SceneJson sceneData;
            public List<ActorJson> actors;
        }

        internal string SerializeEditableState() => JsonUtility.ToJson(new EditableState
        {
            projectName = projectName, sceneData = sceneData, actors = actors
        });

        [Header("Scene Configuration")]
        public SceneJson sceneData = new SceneJson();

        [Header("Actors")]
        public List<ActorJson> actors = new List<ActorJson>();

        /// <summary>
        /// Exports the current project to a JSON file
        /// </summary>
        public string ExportToJson()
        {
            // Export cleanup must not mutate the editable project or its save point.
            var exportScene = JsonUtility.FromJson<SceneJson>(JsonUtility.ToJson(this.sceneData));
            exportScene.Cast = actors;
            var sceneData = JsonUtility.FromJson<SceneJson>(JsonUtility.ToJson(exportScene));
            // Sync Actors

            // Backup and temp cleanup of variables
            var realVariables = sceneData.CustomVariables;
            sceneData.CustomVariables = new List<CustomVariable>();

            // Temp cleanup of "when"
            foreach (var actor in sceneData.Cast)
            {
                actor.GetCustomProperties();
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When != null && sentence.When.Count == 0)
                        sentence.When = null;
                }
            }

            // Generate base json
            string rawJson = JsonUtility.ToJson(sceneData, true);

            // Restore editor state
            sceneData.CustomVariables = realVariables;
            foreach (var actor in sceneData.Cast)
            {
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When == null) sentence.When = new List<string>();
                }
            }

            // Remove empty arrays
            string emptyArrayPattern = @"\s*""[a-zA-Z0-9_]+"": \[\],?";
            rawJson = Regex.Replace(rawJson, emptyArrayPattern, "");

            // Remove default floats
            string zeroFloatPattern = @"\s*""[a-zA-Z0-9_]+"": 0(?:\.0+)?(?=\s*[,}]),?";
            rawJson = Regex.Replace(rawJson, zeroFloatPattern, "");

            // False physics override flags mean "inherit from prefab" and need not clutter JSON.
            string inheritedPhysicsPattern = @"\s*""Override(?:Density|Friction|Bounciness|Drag)"": false,?";
            rawJson = Regex.Replace(rawJson, inheritedPhysicsPattern, "");

            // Manual construction
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"GameName\": \"{sceneData.GameName}\",");
            sb.AppendLine($"    \"ScreenResolution\": [{F(sceneData.ScreenResolution[0])}, {F(sceneData.ScreenResolution[1])}],");

            sb.AppendLine($"    \"CameraPosition\": {Vec3ToJson(sceneData.CameraPosition)},");
            sb.AppendLine($"    \"CameraRotation\": {Vec3ToJson(sceneData.CameraRotation)},");

            sb.AppendLine($"    \"SunPosition\": {Vec3ToJson(sceneData.SunPosition)},");
            sb.AppendLine($"    \"SunRotation\": {Vec3ToJson(sceneData.SunRotation)},");
            sb.AppendLine($"    \"SunColor\": {ColorToJson(sceneData.SunColor)},");
            sb.AppendLine($"    \"SunAmbientColor\": {ColorToJson(sceneData.SunAmbientColor)},");

            sb.AppendLine($"    \"BackgroundColor\": {ColorToJson(sceneData.BackgroundColor)},");
            sb.AppendLine($"    \"Gravity\": {Vec3ToJson(sceneData.Gravity)},");
            sb.AppendLine($"    \"SoundTrack\": \"{EscapeJsonString(sceneData.SoundTrack)}\",");

            // Custom Variables
            if (realVariables != null && realVariables.Count > 0)
            {
                sb.Append(BuildCleanCustomVariablesJson(realVariables));
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine("    \"CustomVariables\": [],");
            }

            // Clean Raw JSON and temp variable list and extract
            rawJson = rawJson.Replace("\"CustomVariables\": [],", "").Replace("\"CustomVariables\": []", "");

            int castIndex = rawJson.IndexOf("\"Cast\":");
            if (castIndex != -1)
            {
                string castContent = rawJson.Substring(castIndex);

                castContent = castContent.TrimEnd('}', '\r', '\n', ' ', ',');

                // Compact multi-line number arrays
                string compactPattern = @":\s*\[\s*([0-9.-]+),\s*([0-9.-]+),\s*([0-9.-]+)\s*\]";
                castContent = Regex.Replace(castContent, compactPattern, ": [$1, $2, $3]");

                // Removes commas left behind
                castContent = Regex.Replace(castContent, @",(\s*})", "$1");

                sb.Append("    " + castContent);
            }
            else
            {
                sb.Append("    \"Cast\": []");
            }

            sb.AppendLine();
            sb.Append("}");

            return sb.ToString();
        }

        private string F(float f) => f.ToString(CultureInfo.InvariantCulture);

        private string Vec3ToJson(float[] v)
        {
            if (v == null || v.Length < 3) return "[0.0, 0.0, 0.0]";
            return $"[{F(v[0])}, {F(v[1])}, {F(v[2])}]";
        }

        private string ColorToJson(byte[] c)
        {
            if (c == null || c.Length < 3) return "[255, 255, 255]";
            return $"[{c[0]}, {c[1]}, {c[2]}]";
        }

        private string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private string BuildCleanCustomVariablesJson(List<CustomVariable> variables)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("    \"CustomVariables\": [");

            for (int i = 0; i < variables.Count; i++)
            {
                var v = variables[i];
                sb.Append("        {");
                sb.Append($"\"name\": \"{v.name}\", \"type\": \"{v.type}\"");

                switch (v.type.ToLower())
                {
                    case "int": sb.Append($", \"intValue\": {v.intValue}"); break;
                    case "float": sb.Append($", \"floatValue\": {v.floatValue.ToString(CultureInfo.InvariantCulture)}"); break;
                    case "bool": sb.Append($", \"boolValue\": {v.boolValue.ToString().ToLower()}"); break;
                    case "vector2":
                    case "vector3":
                        string arrayStr = "[]";
                        if (v.arrayValue != null)
                        {
                            List<string> floats = new List<string>();
                            foreach (float f in v.arrayValue) floats.Add(f.ToString(CultureInfo.InvariantCulture));
                            arrayStr = $"[{string.Join(", ", floats)}]";
                        }
                        sb.Append($", \"arrayValue\": {arrayStr}");
                        break;
                }
                sb.Append("}");
                if (i < variables.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.Append("    ]");
            return sb.ToString();
        }

        public void SaveToJsonFile(string path)
        {
            string json = ExportToJson();
            string temporaryPath = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                System.IO.File.WriteAllText(temporaryPath, json);
                if (System.IO.File.Exists(path)) System.IO.File.Replace(temporaryPath, path, null);
                else System.IO.File.Move(temporaryPath, path);
            }
            finally
            {
                if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath);
            }
            Debug.Log($"Project saved to: {path.Replace('\\', '/')}");
        }

        public static GameRuleProject ImportFromJson(string jsonPath)
        {
            if (!System.IO.File.Exists(jsonPath))
            {
                Debug.LogError($"JSON file not found: {jsonPath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(jsonPath);
            SceneJson sceneData = JsonUtility.FromJson<SceneJson>(json);
            if (sceneData == null || string.IsNullOrWhiteSpace(sceneData.GameName))
                throw new System.FormatException("The JSON must describe a GameRule project with a GameName.");

            // --- SANITIZE & DEFAULTS ---
            // Si el JSON importado no ten�a estos campos, JsonUtility los deja null.
            // Aqu� asignamos los valores por defecto.

            if (sceneData.ScreenResolution == null || sceneData.ScreenResolution.Length < 2)
                sceneData.ScreenResolution = new float[] { 1920, 1080 };

            if (sceneData.CameraPosition == null || sceneData.CameraPosition.Length < 3)
                sceneData.CameraPosition = new float[] { 0, 1, -10 };

            if (sceneData.CameraRotation == null || sceneData.CameraRotation.Length < 3)
                sceneData.CameraRotation = new float[] { 0, 0, 0 };

            if (sceneData.SunPosition == null || sceneData.SunPosition.Length < 3)
                sceneData.SunPosition = new float[] { 0, 3, 0 };

            if (sceneData.SunRotation == null || sceneData.SunRotation.Length < 3)
                sceneData.SunRotation = new float[] { 50, -30, 0 };

            if (sceneData.SunColor == null || sceneData.SunColor.Length < 3)
                sceneData.SunColor = new byte[] { 255, 255, 255 };

            if (sceneData.SunAmbientColor == null || sceneData.SunAmbientColor.Length < 3)
                sceneData.SunAmbientColor = new byte[] { 128, 128, 128 };

            if (sceneData.BackgroundColor == null || sceneData.BackgroundColor.Length < 3)
                sceneData.BackgroundColor = new byte[] { 0, 0, 0 };

            if (sceneData.Gravity == null || sceneData.Gravity.Length < 3)
                sceneData.Gravity = new float[] { 0, -9.81f, 0 };

            if (sceneData.SoundTrack == null)
                sceneData.SoundTrack = "";

            if (sceneData.CustomVariables == null)
                sceneData.CustomVariables = new List<CustomVariable>();

            // Validar actores
            if (sceneData.Cast != null)
            {
                foreach (var actor in sceneData.Cast)
                {
                    ActorPhysicsUtility.NormalizeLegacyOverrides(actor);
                    if (actor.Script != null)
                    {
                        foreach (var sentence in actor.Script)
                        {
                            if (sentence.When == null) sentence.When = new List<string>();
                            if (sentence.Do == null) sentence.Do = new List<string>();
                        }
                    }
                    actor.GetCustomProperties();
                    if (actor.Components == null) actor.Components = new List<ActorComponentMeta>();
                }
            }
            else
            {
                sceneData.Cast = new List<ActorJson>();
            }
            // ---------------------------

            GameRuleProject project = CreateInstance<GameRuleProject>();
            project.projectName = sceneData.GameName ?? "ImportedProject";
            project.sceneData = sceneData;
            project.actors = sceneData.Cast ?? new List<ActorJson>();

            return project;
        }

        // Helpers
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(
            ("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double " +
             "else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is " +
             "lock long namespace new null object operator out override params private protected public readonly ref return sbyte " +
             "sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe " +
             "ushort using virtual void volatile while __arglist __makeref __reftype __refvalue").Split(' '));
        private static readonly HashSet<string> ActorMembers = new HashSet<string>(
            "Active propertyList timers scopeList TagCollisions EvalFixedUpdate Start Awake OnTriggerEnter OnTriggerExit".Split(' '));
        private static readonly HashSet<string> GlobalMembers = new HashSet<string>(
            ("GameManager Instance ActorOrder GameName ScreenResolution CameraPosition CameraRotation SunPosition SunRotation " +
             "SunColor SunAmbientColor BackgroundColor Gravity SoundTrack FPS Time DeltaTime Mouse MouseWorld mainCamera sunLight " +
             "previousCameraPosition currentCameraPosition previousCameraRotation currentCameraRotation Start OnDestroy OnSceneLoaded " +
             "Update FixedUpdate LateUpdate UpdateRuntimeVariables UpdateMousePosition InitializeCameraState BeginCameraFixedTick " +
             "EndCameraFixedTick ApplyCameraSettingsImmediate ApplyCameraSettingsInterpolated ApplySunSettings ApplyGlobalSettings").Split(' '));
        private static readonly HashSet<string> BuiltInProperties = new HashSet<string>(
            "x y z rx ry rz sx sy sz Active sliderValue text".Split(' '));

        private static string IdentifierError(string name)
        {
            if (string.IsNullOrEmpty(name)) return "A name is required.";
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                return "Use letters, digits and underscores; start with a letter or underscore.";
            if (ReservedNames.Contains(name)) return $"'{name}' is a reserved C# keyword.";
            if (name == "AND" || name == "OR" || name == "NOT") return $"'{name}' is a GameRule logic operator.";
            return null;
        }

        // These names are emitted unqualified in generated scripts. Existing actor types
        // generated by GameRule may be reused when opening another project.
        private static bool ConflictsWithType(string name, bool allowGeneratedActor)
        {
            if (name == "System" || name == "UnityEngine") return true;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, false);
                if (type != null && !(allowGeneratedActor && typeof(IGameRuleActor).IsAssignableFrom(type))) return true;
                if (assembly.GetType("UnityEngine." + name, false) != null ||
                    assembly.GetType("UnityEngine.SceneManagement." + name, false) != null ||
                    assembly.GetType("System.Collections.Generic." + name + "`1", false) != null ||
                    assembly.GetType("System.Collections.Generic." + name + "`2", false) != null) return true;
            }
            return false;
        }

        public static string ActorNameError(string name, IList<ActorJson> actors, ActorJson current = null)
        {
            string error = IdentifierError(name);
            if (error != null) return error;
            if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase))
                return $"'{name}' is reserved by the file system and cannot be a script filename.";
            if (name == "var" || name == "dynamic" || name == "nint" || name == "nuint")
                return $"'{name}' cannot be used as a generated C# class name.";
            if (name == "GameManager" || ActorMembers.Contains(name) || ConflictsWithType(name, true))
                return $"'{name}' conflicts with an internal type or generated member.";
            if (actors != null)
                foreach (var actor in actors)
                    if (actor != current && string.Equals(actor.ActorName, name, System.StringComparison.OrdinalIgnoreCase))
                        return "Another actor already uses this name (including letter case).";
            if (current != null && current.GetCustomProperties().Exists(property => property.name == name))
                return "An actor cannot have the same name as one of its custom properties.";
            return null;
        }

        public static string PropertyNameError(string name, ActorJson owner, List<CustomVariable> properties, CustomVariable current = null)
        {
            string error = IdentifierError(name);
            if (error != null) return error;
            bool inherited = typeof(MonoBehaviour).GetMember(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static).Length > 0;
            if (inherited || ConflictsWithType(name, false) ||
                (owner == null ? GlobalMembers.Contains(name) :
                    ActorMembers.Contains(name) || BuiltInProperties.Contains(name) || name == owner.ActorName))
                return $"'{name}' is reserved for a built-in property, type or generated member.";
            if (properties != null && properties.Exists(property => property != current && property.name == name))
                return "Another variable in this scope already uses this name.";
            return null;
        }

        public static void ValidateSceneNames(SceneJson scene)
        {
            if (scene.Cast != null)
                foreach (var actor in scene.Cast)
                {
                    string error = ActorNameError(actor.ActorName, scene.Cast, actor);
                    if (error != null) throw new System.InvalidOperationException($"Actor '{actor.ActorName}': {error}");
                    var properties = actor.GetCustomProperties();
                    foreach (var property in properties)
                    {
                        error = PropertyNameError(property.name, actor, properties, property);
                        if (error != null) throw new System.InvalidOperationException($"Actor '{actor.ActorName}', property '{property.name}': {error}");
                    }
                }
            if (scene.CustomVariables != null)
                foreach (var variable in scene.CustomVariables)
                {
                    string error = PropertyNameError(variable.name, null, scene.CustomVariables, variable);
                    if (error != null) throw new System.InvalidOperationException($"Global variable '{variable.name}': {error}");
                }
        }

        internal void RenameActor(ActorJson actor, string newName)
        {
            if (!string.IsNullOrEmpty(actor.ActorName))
            {
                string pattern = @"(?<![A-Za-z0-9_.#])" + Regex.Escape(actor.ActorName) + @"(?=\.)";
                RewriteReferences((owner, expression) => ReplaceReference(expression, pattern, _ => newName));
            }
            actor.ActorName = newName;
        }

        internal void RenameCustomProperty(ActorJson owner, CustomVariable property, string newName)
        {
            string oldName = property.name;
            if (!string.IsNullOrEmpty(oldName))
                RewriteReferences((ruleOwner, expression) =>
                {
                    string scope = owner == null ? "#" :
                        "(?:" + Regex.Escape(owner.ActorName) + (ruleOwner == owner ? "|this" : "") + @")\.";
                    string pattern = @"(?<![A-Za-z0-9_.#])" + scope + Regex.Escape(oldName) + @"(?![A-Za-z0-9_])";
                    return ReplaceReference(expression, pattern,
                        match => match.Value.Substring(0, match.Value.Length - oldName.Length) + newName);
                });
            property.name = newName;
        }

        private void RewriteReferences(System.Func<ActorJson, string, string> rewrite)
        {
            foreach (var actor in actors)
                if (actor.Script != null)
                    foreach (var rule in actor.Script)
                        foreach (var list in new[] { rule.When, rule.Do })
                            if (list != null)
                                for (int i = 0; i < list.Count; i++)
                                    list[i] = RewriteReferenceParameters(list[i], expression => rewrite(actor, expression));
        }

        // Edit parameter spans in place: keep logical operators, whitespace, quotes and
        // nested expressions intact. Asset names, tags, keys and event names are literals.
        private static string RewriteReferenceParameters(string source, System.Func<string, string> rewrite)
        {
            if (string.IsNullOrEmpty(source)) return source;
            var result = new StringBuilder();
            int copied = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '\'' || source[i] == '"') { i = QuotedEnd(source, i); continue; }
                if (!char.IsLetter(source[i]) && source[i] != '_') continue;
                int start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                string function = source.Substring(start, i - start);
                int open = i;
                while (open < source.Length && char.IsWhiteSpace(source[open])) open++;
                if (open >= source.Length || source[open] != '(') { i--; continue; }
                int depth = 0, parameterStart = open + 1, parameterIndex = 0;
                string destination = null;
                for (int j = open + 1; j < source.Length; j++)
                {
                    char c = source[j];
                    if (c == '\'' || c == '"') { j = QuotedEnd(source, j); continue; }
                    if (c == '(') { depth++; continue; }
                    if (c == ')' && depth > 0) { depth--; continue; }
                    if ((c != ',' && c != ')') || depth != 0) continue;
                    string parameter = source.Substring(parameterStart, j - parameterStart);
                    if (parameterIndex == 0) destination = parameter.Trim().Trim('"', '\'');
                    if (IsReferenceParameter(function, parameterIndex))
                    {
                        result.Append(source, copied, parameterStart - copied);
                        // Quoted text values stay literal; quoted numeric/property parameters
                        // are also accepted by imported GameRule descriptors.
                        bool textValue = function == "Edit" && parameterIndex == 1 &&
                            (destination.EndsWith(".text", System.StringComparison.Ordinal) ||
                             destination == "#GameName" || destination == "#SoundTrack");
                        int left = 0, right = parameter.Length - 1;
                        while (left <= right && char.IsWhiteSpace(parameter[left])) left++;
                        while (right >= left && char.IsWhiteSpace(parameter[right])) right--;
                        if (!textValue && right > left && (parameter[left] == '"' || parameter[left] == '\'') &&
                            QuotedEnd(parameter, left) == right)
                            result.Append(parameter.Substring(0, left + 1))
                                .Append(rewrite(parameter.Substring(left + 1, right - left - 1)))
                                .Append(parameter.Substring(right));
                        else result.Append(rewrite(parameter));
                        copied = j;
                    }
                    parameterStart = j + 1;
                    parameterIndex++;
                    if (c == ')') { i = j; break; }
                }
            }
            result.Append(source, copied, source.Length - copied);
            return result.ToString();
        }

        private static bool IsReferenceParameter(string function, int index)
        {
            switch (function)
            {
                case "Check": case "Compare": case "Timer":
                case "Move": case "MoveTo": case "NavigateTo": case "Rotate":
                case "RotateTo": case "Push": case "PushTo": case "Torque": return true;
                case "Edit": return index < 2;
                case "Spawn": return index >= 2;
                default: return false;
            }
        }

        private static int QuotedEnd(string text, int start)
        {
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == text[start]) return i;
            }
            return text.Length - 1;
        }

        private static string ReplaceReference(string expression, string pattern,
            System.Func<Match, string> replacement)
        {
            // Protect quoted literals inside expressions, including escaped quotes.
            return Regex.Replace(expression, "\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|(?<reference>" + pattern + ")",
                match => match.Groups["reference"].Success ? replacement(match) : match.Value);
        }

        public ActorJson AddActor(string actorName, string prefabName)
        {
            string error = ActorNameError(actorName, actors);
            if (error != null) throw new System.ArgumentException(error, nameof(actorName));
            ActorJson newActor = new ActorJson
            {
                ActorName = actorName,
                PrefabName = prefabName,
                Active = true,
                Position = null,
                Rotation = null,
                Scale = null,
                Properties = new List<string>(),
                Script = new List<SentenceJson>(),
                Components = new List<ActorComponentMeta>
                {
                    new ActorComponentMeta
                    {
                        type = "Properties",
                        name = "Custom Properties"
                    },
                    new ActorComponentMeta
                    {
                        type = "Rules",
                        name = "Rule Set",
                        id = "g" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                    }
                }
            };
            actors.Add(newActor);
            return newActor;
        }

        public void RemoveActor(ActorJson actor)
        { actors.Remove(actor); }

        public ActorJson DuplicateActor(ActorJson original)
        {
            string json = JsonUtility.ToJson(original);
            ActorJson duplicate = JsonUtility.FromJson<ActorJson>(json);
            int counter = 1;
            string baseName = original.ActorName;
            string newName = $"{baseName}_{counter}";
            while (actors.Exists(a => a.ActorName == newName)) { counter++; newName = $"{baseName}_{counter}"; }
            duplicate.ActorName = newName;
            actors.Add(duplicate);
            return duplicate;
        }

        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrEmpty(sceneData.GameName)) errors.Add("Game name is required");
            HashSet<string> actorNames = new HashSet<string>();
            foreach (var actor in actors)
            {
                if (actorNames.Contains(actor.ActorName)) errors.Add($"Duplicate actor name: {actor.ActorName}");
                else actorNames.Add(actor.ActorName);

                if (string.IsNullOrEmpty(actor.PrefabName)) errors.Add($"Actor '{actor.ActorName}' has no prefab assigned");
                else
                {
                    GameObject prefab = Resources.Load<GameObject>($"Prefabs/{actor.PrefabName}");
                    if (prefab == null) errors.Add($"Prefab not found for actor '{actor.ActorName}': {actor.PrefabName}");
                }
            }
            return errors;
        }
    }
}
