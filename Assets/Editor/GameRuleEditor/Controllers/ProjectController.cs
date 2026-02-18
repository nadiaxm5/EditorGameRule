using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace GameRuleEditor.Controllers
{
    /// <summary>
    /// Handles all business logic for project operations.
    /// Integrates with Unity's Undo system for full undo/redo support.
    /// </summary>
    public class ProjectController
    {
        private GameRuleEditor.Core.EditorContext context;

        public ProjectController(GameRuleEditor.Core.EditorContext editorContext)
        {
            context = editorContext;
        }

        #region Project Operations

        /// <summary>
        /// Creates a new empty project
        /// </summary>
        public void CreateNewProject(string projectName)
        {
            context.CreateNewProject(projectName);
            EditorUtility.SetDirty(context);
        }

        /// <summary>
        /// Loads an existing project
        /// </summary>
        public void LoadProject(GameRuleEditor.Core.GameRuleProject project)
        {
            Undo.RecordObject(context, "Load Project");
            context.LoadProject(project);
            EditorUtility.SetDirty(context);
        }

        /// <summary>
        /// Saves the current project to JSON file
        /// </summary>
        public void SaveProjectToJson(string path)
        {
            if (context.currentProject == null)
            {
                Debug.LogError("No project loaded to save");
                return;
            }

            context.currentProject.SaveToJsonFile(path);
        }

        /// <summary>
        /// Imports a JSON file and loads it as a project
        /// </summary>
        public void ImportJsonAsProject(string jsonPath, string projectSavePath)
        {
            var project = GameRuleEditor.Core.GameRuleProject.ImportFromJson(jsonPath);
            if (project != null)
            {
                AssetDatabase.CreateAsset(project, projectSavePath);
                AssetDatabase.SaveAssets();
                LoadProject(project);
            }
        }

        #endregion Project Operations

        #region Scene Settings Operations

        /// <summary>
        /// Updates a scene property with undo support
        /// </summary>
        public void UpdateSceneProperty(System.Action modifyAction, string undoName = "Modify Scene")
        {
            if (context.currentProject == null) return;

            Undo.RecordObject(context.currentProject, undoName);
            modifyAction?.Invoke();
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Adds a custom global variable with an initial value
        /// </summary>
        public void AddCustomVariable(string name, string type, object value)
        {
            if (context.currentProject == null) return;

            Undo.RecordObject(context.currentProject, "Add Custom Variable");

            CustomVariable newVar = new CustomVariable
            {
                name = name,
                type = type
            };

            // Assign the initial value depending on the type
            switch (type)
            {
                case "int":
                    newVar.intValue = (int)value;
                    break;

                case "float":
                    newVar.floatValue = (float)value;
                    break;

                case "bool":
                    newVar.boolValue = (bool)value;
                    break;

                case "vector2":
                    var v2 = (Vector2)value;
                    newVar.arrayValue = new float[] { v2.x, v2.y };
                    break;

                case "vector3":
                    var v3 = (Vector3)value;
                    newVar.arrayValue = new float[] { v3.x, v3.y, v3.z };
                    break;
            }

            if (context.currentProject.sceneData.CustomVariable == null)
            {
                context.currentProject.sceneData.CustomVariable = new List<CustomVariable>();
            }

            context.currentProject.sceneData.CustomVariable.Add(newVar);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Removes a custom global variable
        /// </summary>
        public void RemoveCustomVariable(int index)
        {
            if (context.currentProject == null ||
                context.currentProject.sceneData.CustomVariable == null ||
                index < 0 || index >= context.currentProject.sceneData.CustomVariable.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Remove Custom Variable");
            context.currentProject.sceneData.CustomVariable.RemoveAt(index);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        #endregion Scene Settings Operations

        #region Actor Operations

        /// <summary>
        /// Adds a new actor to the project with a default "Empty" prefab.
        /// Automatically creates the Empty prefab if it doesn't exist.
        /// </summary>
        public void AddActor(string actorName)
        {
            if (context.currentProject == null) return;

            Undo.RecordObject(context.currentProject, "Add Actor");

            EnsureEmptyPrefabExists();

            // Initialize with null arrays to use prefab defaults
            ActorJson newActor = new ActorJson
            {
                ActorName = actorName,
                PrefabName = "Empty",
                Active = true,
                Tag = "Untagged",
                Position = null,
                Rotation = null,
                Scale = null,
                Velocity = null,
                AngularVelocity = null,
                Size = null,
                Properties = new List<string>(),
                Script = new List<SentenceJson>()
            };

            context.currentProject.actors.Add(newActor);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyActorListChanged();

            int newIndex = context.currentProject.actors.Count - 1;
            context.SelectActor(newIndex);
        }

        public void RevertActorProperty(int actorIndex, string propertyName)
        {
            if (context.currentProject == null || actorIndex < 0) return;

            Undo.RecordObject(context.currentProject, "Revert Property");
            var actor = context.currentProject.actors[actorIndex];

            switch (propertyName)
            {
                case "Position": actor.Position = null; break;
                case "Rotation": actor.Rotation = null; break;
                case "Scale": actor.Scale = null; break;
                case "Size": actor.Size = null; break;
                case "Velocity": actor.Velocity = null; break;
                case "AngularVelocity": actor.AngularVelocity = null; break;
                case "Physics":
                    actor.Density = 0;
                    actor.Friction = 0;
                    actor.Bounciness = 0;
                    actor.Drag = 0;
                    break;
            }

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        private void EnsureEmptyPrefabExists()
        {
            string folderPath = "Assets/Resources/Prefabs";
            string prefabPath = folderPath + "/Empty.prefab";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            if (!File.Exists(prefabPath))
            {
                GameObject emptyGO = new GameObject("Empty");
                PrefabUtility.SaveAsPrefabAsset(emptyGO, prefabPath);
                Object.DestroyImmediate(emptyGO);
                AssetDatabase.Refresh();
                Debug.Log("Created default 'Empty' prefab at " + prefabPath);
            }
        }

        /// <summary>
        /// Removes an actor from the project
        /// </summary>
        public void RemoveActor(int actorIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Remove Actor");

            context.currentProject.actors.RemoveAt(actorIndex);

            EditorUtility.SetDirty(context.currentProject);

            if (context.selectedActorIndex == actorIndex)
            {
                context.SelectActor(-1);
            }
            else if (context.selectedActorIndex > actorIndex)
            {
                context.SelectActor(context.selectedActorIndex - 1);
            }

            context.NotifyActorListChanged();
        }

        /// <summary>
        /// Duplicates an existing actor
        /// </summary>
        public void DuplicateActor(int actorIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Duplicate Actor");

            ActorJson original = context.currentProject.actors[actorIndex];
            ActorJson duplicate = context.currentProject.DuplicateActor(original);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyActorListChanged();

            int newIndex = context.currentProject.actors.IndexOf(duplicate);
            context.SelectActor(newIndex);
        }

        /// <summary>
        /// Updates an actor's property with undo support
        /// </summary>
        public void UpdateActorProperty(int actorIndex, System.Action modifyAction, string undoName = "Modify Actor")
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, undoName);
            modifyAction?.Invoke();
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Adds a custom property to an actor
        /// </summary>
        public void AddActorProperty(int actorIndex, string propertyDefinition)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Add Actor Property");

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Properties == null)
            {
                actor.Properties = new List<string>();
            }

            actor.Properties.Add(propertyDefinition);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Removes a custom property from an actor
        /// </summary>
        public void RemoveActorProperty(int actorIndex, int propertyIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Properties == null ||
                propertyIndex < 0 ||
                propertyIndex >= actor.Properties.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Remove Actor Property");
            actor.Properties.RemoveAt(propertyIndex);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        #endregion Actor Operations

        #region Script/Rule Operations

        /// <summary>
        /// Adds a new rule (when-do) to an actor's script
        /// </summary>
        public void AddRule(int actorIndex, bool hasCondition = true)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Add Rule");

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null)
            {
                actor.Script = new List<SentenceJson>();
            }

            SentenceJson newRule = new SentenceJson
            {
                When = hasCondition ? new List<string> { "" } : new List<string>(),
                Do = new List<string> { "" }
            };

            actor.Script.Add(newRule);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();

            context.SelectScript(actor.Script.Count - 1);
        }

        /// <summary>
        /// Removes a rule from an actor's script
        /// </summary>
        public void RemoveRule(int actorIndex, int ruleIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex < 0 ||
                ruleIndex >= actor.Script.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Remove Rule");
            actor.Script.RemoveAt(ruleIndex);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();

            if (context.selectedScriptIndex == ruleIndex)
            {
                context.SelectScript(-1);
            }
            else if (context.selectedScriptIndex > ruleIndex)
            {
                context.SelectScript(context.selectedScriptIndex - 1);
            }
        }

        /// <summary>
        /// Moves a rule up in the list
        /// </summary>
        public void MoveRuleUp(int actorIndex, int ruleIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex <= 0 ||
                ruleIndex >= actor.Script.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Move Rule Up");

            SentenceJson temp = actor.Script[ruleIndex];
            actor.Script[ruleIndex] = actor.Script[ruleIndex - 1];
            actor.Script[ruleIndex - 1] = temp;

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            context.SelectScript(ruleIndex - 1);
        }

        /// <summary>
        /// Moves a rule down in the list
        /// </summary>
        public void MoveRuleDown(int actorIndex, int ruleIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex < 0 ||
                ruleIndex >= actor.Script.Count - 1)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Move Rule Down");

            SentenceJson temp = actor.Script[ruleIndex];
            actor.Script[ruleIndex] = actor.Script[ruleIndex + 1];
            actor.Script[ruleIndex + 1] = temp;

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            context.SelectScript(ruleIndex + 1);
        }

        /// <summary>
        /// Duplicates a rule
        /// </summary>
        public void DuplicateRule(int actorIndex, int ruleIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex < 0 ||
                ruleIndex >= actor.Script.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Duplicate Rule");

            SentenceJson original = actor.Script[ruleIndex];
            SentenceJson duplicate = new SentenceJson
            {
                When = new List<string>(original.When),
                Do = new List<string>(original.Do)
            };

            actor.Script.Insert(ruleIndex + 1, duplicate);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            context.SelectScript(ruleIndex + 1);
        }

        /// <summary>
        /// Updates a rule's condition (When)
        /// </summary>
        public void UpdateRuleCondition(int actorIndex, int ruleIndex, List<string> conditions)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex < 0 ||
                ruleIndex >= actor.Script.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Update Rule Condition");
            actor.Script[ruleIndex].When = conditions;
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Updates a rule's actions (Do)
        /// </summary>
        public void UpdateRuleActions(int actorIndex, int ruleIndex, List<string> actions)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                ruleIndex < 0 ||
                ruleIndex >= actor.Script.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Update Rule Actions");
            actor.Script[ruleIndex].Do = actions;
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        #endregion Script/Rule Operations

        #region Validation

        /// <summary>
        /// Validates the current project and returns errors
        /// </summary>
        public List<string> ValidateProject()
        {
            if (context.currentProject == null)
            {
                return new List<string> { "No project loaded" };
            }

            return context.currentProject.Validate();
        }

        #endregion Validation
    }
}