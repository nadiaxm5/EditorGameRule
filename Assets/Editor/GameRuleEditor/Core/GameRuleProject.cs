using System.Collections.Generic;
using System.Text;
using System.Globalization;
using UnityEngine;

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
            // Sync Actors
            sceneData.Cast = new List<ActorJson>(actors);

            // Backup and Temps cleanup of variables
            var realVariables = sceneData.CustomVariables;
            sceneData.CustomVariables = new List<CustomVariable>();

            // Temp cleanup of When (To handle Unconditional Rules)
            foreach (var actor in sceneData.Cast)
            {
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When != null && sentence.When.Count == 0)
                        sentence.When = null;
                }
            }

            // Generate base json
            string json = JsonUtility.ToJson(sceneData, true);

            // Restore editor state
            sceneData.CustomVariables = realVariables;
            foreach (var actor in sceneData.Cast)
            {
                if (actor.Script == null) continue;
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When == null)
                        sentence.When = new List<string>();
                }
            }

            // Build CustomVariables manually and cleanly
            if (realVariables != null && realVariables.Count > 0)
            {
                string cleanVariablesJson = BuildCleanCustomVariablesJson(realVariables);
                json = json.Replace("\"CustomVariables\": []", cleanVariablesJson);
            }

            // Clean residual When from unconditional rules
            json = System.Text.RegularExpressions.Regex.Replace(json, "\\s*\"When\": \\[\\],", "");

            return json;
        }

        private string BuildCleanCustomVariablesJson(List<CustomVariable> variables)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\"CustomVariables\": [");

            for (int i = 0; i < variables.Count; i++)
            {
                var v = variables[i];
                sb.Append("        {"); // Indentation for pretty print
                sb.Append($"\"name\": \"{v.name}\", \"type\": \"{v.type}\"");

                // Write only the relevant value based on the type
                switch (v.type.ToLower())
                {
                    case "int":
                        sb.Append($", \"intValue\": {v.intValue}");
                        break;
                    case "float":
                        sb.Append($", \"floatValue\": {v.floatValue.ToString(CultureInfo.InvariantCulture)}");
                        break;
                    case "bool":
                        sb.Append($", \"boolValue\": {v.boolValue.ToString().ToLower()}");
                        break;
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

            // Sanitization on import
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
                Position = new float[] { 0, 0, 0 },
                Rotation = new float[] { 0, 0, 0 },
                Scale = new float[] { 1, 1, 1 },
                Properties = new List<string>(),
                Script = new List<SentenceJson>()
            };

            actors.Add(newActor);
            return newActor;
        }

        public void RemoveActor(ActorJson actor)
        {
            actors.Remove(actor);
        }

        public ActorJson DuplicateActor(ActorJson original)
        {
            string json = JsonUtility.ToJson(original);
            ActorJson duplicate = JsonUtility.FromJson<ActorJson>(json);

            int counter = 1;
            string baseName = original.ActorName;
            string newName = $"{baseName}_{counter}";

            while (actors.Exists(a => a.ActorName == newName))
            {
                counter++;
                newName = $"{baseName}_{counter}";
            }

            duplicate.ActorName = newName;
            actors.Add(duplicate);

            return duplicate;
        }

        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrEmpty(sceneData.GameName))
                errors.Add("Game name is required");

            HashSet<string> actorNames = new HashSet<string>();
            foreach (var actor in actors)
            {
                if (actorNames.Contains(actor.ActorName))
                    errors.Add($"Duplicate actor name: {actor.ActorName}");
                else
                    actorNames.Add(actor.ActorName);

                if (string.IsNullOrEmpty(actor.PrefabName))
                    errors.Add($"Actor '{actor.ActorName}' has no prefab assigned");
                else
                {
                    GameObject prefab = Resources.Load<GameObject>($"Prefabs/{actor.PrefabName}");
                    if (prefab == null)
                        errors.Add($"Prefab not found for actor '{actor.ActorName}': {actor.PrefabName}");
                }
            }

            return errors;
        }
    }
}