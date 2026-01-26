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

        [Header("Scene Configuration")]
        public SceneJson sceneData = new SceneJson();

        [Header("Actors")]
        public List<ActorJson> actors = new List<ActorJson>();

        /// <summary>
        /// Exports the current project to a JSON file
        /// </summary>
        public string ExportToJson()
        {
            // 1. Sync Actors
            sceneData.Cast = new List<ActorJson>(actors);

            // 2. BACKUP AND TEMP CLEANUP OF VARIABLES
            var realVariables = sceneData.CustomVariables;
            sceneData.CustomVariables = new List<CustomVariable>();

            // 3. TEMP CLEANUP OF "WHEN"
            foreach (var actor in sceneData.Cast)
            {
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When != null && sentence.When.Count == 0)
                        sentence.When = null;
                }
            }

            // 4. GENERATE BASE JSON
            string rawJson = JsonUtility.ToJson(sceneData, true);

            // --- RESTORE EDITOR STATE ---
            sceneData.CustomVariables = realVariables;
            foreach (var actor in sceneData.Cast)
            {
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When == null) sentence.When = new List<string>();
                }
            }

            // =================================================================================
            // 5. CLEANUP: REMOVE EMPTY ARRAYS AND DEFAULT FLOATS
            // =================================================================================

            // A. Remove Empty Arrays ("Key": [],)
            string emptyArrayPattern = @"\s*""[a-zA-Z0-9_]+"": \[\],?";
            rawJson = Regex.Replace(rawJson, emptyArrayPattern, "");

            // B. Remove Default Floats ("Key": 0.0,)
            string zeroFloatPattern = @"\s*""[a-zA-Z0-9_]+"": 0(\.0)?,?";
            rawJson = Regex.Replace(rawJson, zeroFloatPattern, "");

            // =================================================================================
            // 6. MANUAL CONSTRUCTION
            // =================================================================================

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

            // Extract Cast from Cleaned Raw JSON
            // We clean up the temp variable list first (handles trailing commas if it was last)
            rawJson = rawJson.Replace("\"CustomVariables\": [],", "").Replace("\"CustomVariables\": []", "");

            int castIndex = rawJson.IndexOf("\"Cast\":");
            if (castIndex != -1)
            {
                string castContent = rawJson.Substring(castIndex);

                // CRITICAL FIX: TrimEnd includes ',' to remove the trailing comma
                castContent = castContent.TrimEnd('}', '\r', '\n', ' ', ',');

                // C. Compact multi-line number arrays: [ \n 1, \n 2 ] -> [1, 2]
                string compactPattern = @":\s*\[\s*([0-9.-]+),\s*([0-9.-]+),\s*([0-9.-]+)\s*\]";
                castContent = Regex.Replace(castContent, compactPattern, ": [$1, $2, $3]");

                // D. FIX TRAILING COMMAS INSIDE OBJECTS
                // Removes commas that were left behind because we deleted properties like "Position": []
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
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Project saved to: {path}");
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

            if (sceneData.Cast != null)
            {
                foreach (var actor in sceneData.Cast)
                {
                    if (actor.Script != null)
                    {
                        foreach (var sentence in actor.Script)
                        {
                            if (sentence.When == null) sentence.When = new List<string>();
                            if (sentence.Do == null) sentence.Do = new List<string>();
                        }
                    }
                }
            }

            GameRuleProject project = CreateInstance<GameRuleProject>();
            project.projectName = sceneData.GameName ?? "ImportedProject";
            project.sceneData = sceneData;
            project.actors = sceneData.Cast ?? new List<ActorJson>();

            return project;
        }

        // --- Helpers ---
        public ActorJson AddActor(string actorName, string prefabName)
        {
            ActorJson newActor = new ActorJson
            {
                ActorName = actorName,
                PrefabName = prefabName,
                Active = true,
                Position = null,
                Rotation = null,
                Scale = null,
                Properties = new List<string>(),
                Script = new List<SentenceJson>()
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