using System.Collections.Generic;
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
            // Update the Cast in sceneData with current actors
            sceneData.Cast = new List<ActorJson>(actors);

            return JsonUtility.ToJson(sceneData, true);
        }

        /// <summary>
        /// Saves the project to a JSON file at the specified path
        /// </summary>
        public void SaveToJsonFile(string path)
        {
            string json = ExportToJson();
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Project saved to: {path}");
        }

        /// <summary>
        /// Imports a JSON file and creates a new GameRuleProject from it
        /// </summary>
        public static GameRuleProject ImportFromJson(string jsonPath)
        {
            if (!System.IO.File.Exists(jsonPath))
            {
                Debug.LogError($"JSON file not found: {jsonPath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(jsonPath);
            SceneJson sceneData = JsonUtility.FromJson<SceneJson>(json);

            // Create a new project instance
            GameRuleProject project = CreateInstance<GameRuleProject>();
            project.projectName = sceneData.GameName ?? "ImportedProject";
            project.sceneData = sceneData;
            project.actors = sceneData.Cast ?? new List<ActorJson>();

            return project;
        }

        /// <summary>
        /// Adds a new actor to the project
        /// </summary>
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

        /// <summary>
        /// Removes an actor from the project
        /// </summary>
        public void RemoveActor(ActorJson actor)
        {
            actors.Remove(actor);
        }

        /// <summary>
        /// Duplicates an existing actor
        /// </summary>
        public ActorJson DuplicateActor(ActorJson original)
        {
            string json = JsonUtility.ToJson(original);
            ActorJson duplicate = JsonUtility.FromJson<ActorJson>(json);

            // Generate unique name
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

        /// <summary>
        /// Validates the entire project
        /// </summary>
        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            // Validate scene data
            if (string.IsNullOrEmpty(sceneData.GameName))
            {
                errors.Add("Game name is required");
            }

            // Validate actors
            HashSet<string> actorNames = new HashSet<string>();
            foreach (var actor in actors)
            {
                // Check for duplicate names
                if (actorNames.Contains(actor.ActorName))
                {
                    errors.Add($"Duplicate actor name: {actor.ActorName}");
                }
                else
                {
                    actorNames.Add(actor.ActorName);
                }

                // Check if prefab exists
                if (string.IsNullOrEmpty(actor.PrefabName))
                {
                    errors.Add($"Actor '{actor.ActorName}' has no prefab assigned");
                }
                else
                {
                    GameObject prefab = Resources.Load<GameObject>($"Prefabs/{actor.PrefabName}");
                    if (prefab == null)
                    {
                        errors.Add($"Prefab not found for actor '{actor.ActorName}': {actor.PrefabName}");
                    }
                }
            }

            return errors;
        }
    }
}
