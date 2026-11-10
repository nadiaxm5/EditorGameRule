using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameRuleEditor.Core;

namespace GameRuleEditor.Controllers
{
    /// <summary>
    /// Handles all business logic for project operations.
    /// Integrates with Unity's Undo system for full undo/redo support.
    /// </summary>
    public class ProjectController
    {
        public const string ProjectsAssetFolder = "Assets/Projects";
        public static string ProjectsAbsoluteFolder => Path.Combine(Application.dataPath, "Projects");

        private GameRuleEditor.Core.EditorContext context;

        public ProjectController(GameRuleEditor.Core.EditorContext editorContext)
        {
            context = editorContext;
            EnsureSavePoint();
        }

        // Enable update loop listener
        public void Enable()
        {
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedoPerformed; // Escucha eventos de undo/redo para forzar refresh
        }

        // Disable update loop listener
        public void Disable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed; // Deja de escuchar eventos de undo/redo
        }

        /// <summary>
        /// Basicamente se llama cada vez que el usuario hace Ctrl+Z o Ctrl+Y, para asegurarnos de que la UI se actualice con los datos restaurados por Unity. Sin esto, la UI podría quedar desincronizada después de un undo/redo.
        /// </summary>
        private void OnUndoRedoPerformed()
        {
            if (context == null) return;

            // After undo/redo, the project data may have changed (e.g. actor removed),
            // but selectedActorIndex/selectedScriptIndex on EditorContext are NOT part of
            // the undo record. Clamp them to valid ranges to avoid null references.
            if (context.currentProject != null)
            {
                int actorCount = context.currentProject.actors.Count;
                if (context.selectedActorIndex >= actorCount)
                {
                    context.selectedActorIndex = actorCount - 1;
                    context.selectedScriptIndex = -1;
                }

                if (context.selectedActorIndex >= 0)
                {
                    var actor = context.currentProject.actors[context.selectedActorIndex];
                    int scriptCount = actor.Script?.Count ?? 0;
                    if (context.selectedScriptIndex >= scriptCount)
                        context.selectedScriptIndex = scriptCount - 1;
                }
                else
                {
                    context.selectedScriptIndex = -1;
                }
            }

            SyncGravityToScene();
            if (context.SelectedActor != null)
                SyncDataToScene(context.SelectedActor);

            context.isUndoRedoRefresh = true;
            context.NotifyAll();
            context.isUndoRedoRefresh = false;
        }

        // Check for scene changes every frame (efficiently)
        private void OnEditorUpdate()
        {
            if (context.currentProject == null || context.selectedActorIndex < 0) return;
            var actor = context.SelectedActor;
            if (actor == null) return;
            SyncSceneToData(actor);
        }

        #region Project Operations

        public static void EnsureProjectsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ProjectsAssetFolder))
                AssetDatabase.CreateFolder("Assets", "Projects");
        }

        /// <summary>
        /// Creates a new empty project
        /// </summary>
        public void CreateNewProject(string projectName)
        {
            context.CloseInspector();
            context.CreateNewProject(projectName);
            SyncGravityToScene();
            EditorUtility.SetDirty(context);
        }

        /// <summary>
        /// Creates and saves a new project, then generates its scene and scripts without entering Play mode.
        /// Keeping the whole operation here ensures every New Project entry point behaves like Open Project.
        /// </summary>
        public bool CreateAndGenerateProject(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName) || !ConfirmLeaveProject()) return false;
            EnsureProjectsFolder();
            projectName = UniqueProjectName(projectName);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{ProjectsAssetFolder}/{SanitizeFileName(projectName)}.asset");

            CreateNewProject(projectName);
            AssetDatabase.CreateAsset(context.currentProject, assetPath);
            SaveProject();

            // A New Project operation must never inherit a pending auto-play request.
            EditorPrefs.DeleteKey("GameRule_AutoPlayAfterGenerate");
            GenerateScene();
            return true;
        }

        /// <summary>
        /// Loads an existing project
        /// </summary>
        public void LoadProject(GameRuleEditor.Core.GameRuleProject project)
        {
            context.CloseInspector();
            context.LoadProject(project);
            CaptureSavePoint();
            SyncGravityToScene();
            SyncSoundTrackToScene();
            EditorUtility.SetDirty(context);
        }

        /// <summary>
        /// Loads a project and generates its scene and scripts without entering Play mode.
        /// Script attachment still happens after Unity finishes compiling them.
        /// </summary>
        public bool OpenAndGenerateProject(GameRuleEditor.Core.GameRuleProject project)
        {
            if (project == null || !ConfirmLeaveProject()) return false;

            // An Open Project operation must never inherit a pending auto-play request.
            EditorPrefs.DeleteKey("GameRule_AutoPlayAfterGenerate");
            LoadProject(project);
            GenerateScene();
            return true;
        }

        public void SyncGravityToScene()
        {
            float[] gravity = context?.currentProject?.sceneData?.Gravity;
            if (gravity == null || gravity.Length < 3) return;

            Physics.gravity = new Vector3(gravity[0], gravity[1], gravity[2]);
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
        public void ImportJsonAsProject(string jsonPath)
        {
            var project = GameRuleEditor.Core.GameRuleProject.ImportFromJson(jsonPath);
            if (project == null) return;
            try
            {
                if (!ConfirmLeaveProject()) return;
                EnsureProjectsFolder();
                project.projectName = UniqueProjectName(project.projectName);
                project.sceneData.GameName = project.projectName;
                string savePath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ProjectsAssetFolder}/{SanitizeFileName(project.projectName)}.asset");
                AssetDatabase.CreateAsset(project, savePath);
                LoadProject(project);
                SaveProject();
                EditorPrefs.DeleteKey("GameRule_AutoPlayAfterGenerate");
                GenerateScene();
            }
            finally
            {
                if (!AssetDatabase.Contains(project)) Object.DestroyImmediate(project);
            }
        }

        public List<GameRuleProject> GetProjects()
        {
            return AssetDatabase.FindAssets("t:GameRuleProject", new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameRuleProject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(project => project != null)
                .OrderBy(project => project.projectName, System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string UniqueProjectName(string requested)
        {
            string baseName = SanitizeFileName(requested.Trim()).TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "New Project";
            var names = new HashSet<string>(GetProjects().Select(project => project.projectName),
                System.StringComparer.OrdinalIgnoreCase);
            string gamesFolder = Path.Combine(Application.dataPath, "Resources/Games");
            if (Directory.Exists(gamesFolder))
                foreach (string json in Directory.GetFiles(gamesFolder, "*.json"))
                    names.Add(Path.GetFileNameWithoutExtension(json));
            string name = baseName;
            for (int suffix = 2; names.Contains(name); suffix++) name = baseName + " (" + suffix + ")";
            return name;
        }

        private void EnsureSavePoint()
        {
            if (context.currentProject != null && (context.savedProject != context.currentProject ||
                string.IsNullOrEmpty(context.savedProjectState))) CaptureSavePoint();
        }

        private void CaptureSavePoint()
        {
            context.savedProject = context.currentProject;
            context.savedProjectState = context.currentProject == null ? null : context.currentProject.SerializeEditableState();
            EditorUtility.SetDirty(context);
            AssetDatabase.SaveAssetIfDirty(context);
        }

        public bool HasUnsavedChanges
        {
            get
            {
                EnsureSavePoint();
                return context.currentProject != null &&
                    context.currentProject.SerializeEditableState() != context.savedProjectState;
            }
        }

        public void SaveProject()
        {
            if (context.currentProject == null) return;
            context.currentProject.sceneData.GameName = context.currentProject.projectName;
            WriteProjectJson();
            EditorUtility.SetDirty(context.currentProject);
            AssetDatabase.SaveAssetIfDirty(context.currentProject);
            CaptureSavePoint();
            AssetDatabase.Refresh();
        }

        private bool ConfirmLeaveProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play mode before changing projects.");
            if (!HasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved changes",
                $"Save changes to '{context.currentProject.projectName}'?", "Save", "Cancel", "Discard");
            if (choice == 1) return false;
            if (choice == 0) SaveProject();
            else
            {
                JsonUtility.FromJsonOverwrite(context.savedProjectState, context.currentProject);
                Undo.ClearUndo(context.currentProject);
                // Other Unity operations may already have flushed the edited asset to disk.
                EditorUtility.SetDirty(context.currentProject);
                AssetDatabase.SaveAssetIfDirty(context.currentProject);
                WriteProjectJson();
                AssetDatabase.Refresh();
            }
            return true;
        }

        public bool CloseProject()
        {
            if (!ConfirmLeaveProject()) return false;
            EditorPrefs.DeleteKey("GameRule_AutoPlayAfterGenerate");
            context.Clear();
            EditorUtility.SetDirty(context);
            AssetDatabase.SaveAssetIfDirty(context);
            return true;
        }

        private string WriteProjectJson()
        {
            const string gamesFolder = "Assets/Resources/Games";
            var project = context.currentProject;
            string assetPath = AssetDatabase.GetAssetPath(project);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                throw new System.InvalidOperationException("Save the project locally before generating its JSON.");
            string name = project.projectName;
            if (string.IsNullOrWhiteSpace(name) || name != SanitizeFileName(name) ||
                name.EndsWith(".") || name.EndsWith(" "))
                throw new System.InvalidOperationException("Choose a Game Name that is a valid file name before saving.");
            string fileName = name + ".json";
            var projects = GetProjects();
            if (projects.Any(other => other != project &&
                (SameJsonName(other.jsonFileName, fileName) || SameJsonName(other.projectName + ".json", fileName))))
                throw new System.InvalidOperationException($"Another project uses '{name}'. Choose a different Game Name.");

            string folder = Path.Combine(Application.dataPath, "Resources/Games");
            Directory.CreateDirectory(folder);
            string previous = project.jsonFileName;
            if (!string.IsNullOrEmpty(previous) && previous != Path.GetFileName(previous))
                throw new System.InvalidOperationException("The project's saved JSON file name is invalid.");

            // Migrate the named JSON used before file ownership was recorded. Only claim
            // a file matching this asset's original name and the GameName inside the JSON.
            if (string.IsNullOrEmpty(previous))
            {
                string legacyName = Path.GetFileNameWithoutExtension(assetPath);
                string legacyFile = legacyName + ".json";
                string legacyPath = Path.Combine(folder, legacyFile);
                bool claimed = projects.Any(other => other != project &&
                    (SameJsonName(other.jsonFileName, legacyFile) ||
                     SameJsonName(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(other)) + ".json", legacyFile)));
                if (!claimed && File.Exists(legacyPath))
                {
                    try
                    {
                        var legacyScene = JsonUtility.FromJson<SceneJson>(File.ReadAllText(legacyPath));
                        if (legacyScene != null && legacyScene.GameName == legacyName) previous = legacyFile;
                    }
                    catch (System.ArgumentException) { /* An unrelated/invalid JSON is never adopted. */ }
                }
            }

            string destination = gamesFolder + "/" + fileName;
            string fullPath = Path.Combine(folder, fileName);
            if (File.Exists(fullPath) && !SameJsonName(previous, fileName))
                throw new System.InvalidOperationException($"'{fileName}' already exists in Resources/Games. Choose a different Game Name to avoid replacing it.");

            string generated = gamesFolder + "/Generated/" + guid + ".json";
            string source = !string.IsNullOrEmpty(previous) && File.Exists(Path.Combine(folder, previous))
                ? gamesFolder + "/" + previous : File.Exists(Path.Combine(Application.dataPath, "Resources/Games/Generated", guid + ".json"))
                    ? generated : null;
            bool moved = source != null && !SameJsonName(source, destination);
            if (moved)
            {
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new System.IO.IOException(error);
            }
            try { project.SaveToJsonFile(fullPath); }
            catch
            {
                if (moved) AssetDatabase.MoveAsset(destination, source);
                throw;
            }
            project.jsonFileName = fileName;
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(project);

            // Remove only this project's obsolete generated copy, after the named file is saved.
            if (File.Exists(Path.Combine(folder, "Generated", guid + ".json")))
                AssetDatabase.DeleteAsset(generated);
            string generatedFolder = Path.Combine(folder, "Generated");
            if (Directory.Exists(generatedFolder) && !Directory.EnumerateFileSystemEntries(generatedFolder).Any())
                AssetDatabase.DeleteAsset(gamesFolder + "/Generated");
            return fileName;
        }

        private static bool SameJsonName(string first, string second) =>
            string.Equals(first, second, System.StringComparison.OrdinalIgnoreCase);

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "ImportedProject";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        #endregion Project Operations

        #region Scene Generation

        /// <summary>
        /// Generates the Unity scene from the current project data.
        /// Exports a temporary JSON to Resources/Games and calls Loader.LoadJson.
        /// </summary>
        public void GenerateScene()
        {
            if (context.currentProject == null)
            {
                Debug.LogError("No project loaded to generate scene from.");
                return;
            }

            string fileName = WriteProjectJson();
            AssetDatabase.Refresh();

            Loader.LoadJson(fileName);
            Debug.Log($"Scene generated from project '{context.currentProject.projectName}'");
        }

        #endregion Scene Generation

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
                case "int": newVar.intValue = (int)value; break;
                case "float": newVar.floatValue = (float)value; break;
                case "bool": newVar.boolValue = (bool)value; break;
                case "vector2": var v2 = (Vector2)value; newVar.arrayValue = new float[] { v2.x, v2.y }; break;
                case "vector3": var v3 = (Vector3)value; newVar.arrayValue = new float[] { v3.x, v3.y, v3.z }; break;
            }

            if (context.currentProject.sceneData.CustomVariables == null)
            {
                context.currentProject.sceneData.CustomVariables = new List<CustomVariable>();
            }

            context.currentProject.sceneData.CustomVariables.Add(newVar);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Removes a custom global variable
        /// </summary>
        public void RemoveCustomVariable(int index)
        {
            if (context.currentProject == null ||
                context.currentProject.sceneData.CustomVariables == null ||
                index < 0 || index >= context.currentProject.sceneData.CustomVariables.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Remove Custom Variable");
            context.currentProject.sceneData.CustomVariables.RemoveAt(index);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Pushes the camera settings onto the scene camera (a child of GameManager).
        /// Without this the GameManager script only applies them on Play, so edits made in
        /// Scene Settings are invisible while working in the editor.
        /// </summary>
        public void SyncCameraToScene()
        {
            if (context.currentProject == null) return;

            Camera camera = FindSceneCamera();
            if (camera == null) return;

            var scene = context.currentProject.sceneData;
            Undo.RecordObject(camera.transform, "Change Camera Transform");

            if (scene.CameraPosition != null && scene.CameraPosition.Length >= 3)
                camera.transform.position = new Vector3(scene.CameraPosition[0], scene.CameraPosition[1], scene.CameraPosition[2]);

            if (scene.CameraRotation != null && scene.CameraRotation.Length >= 3)
                camera.transform.eulerAngles = new Vector3(scene.CameraRotation[0], scene.CameraRotation[1], scene.CameraRotation[2]);

            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        }

        /// <summary>
        /// Applies the selected soundtrack to the generated GameManager immediately.
        /// The GameManager prefab itself deliberately contains no fixed AudioSource.
        /// </summary>
        public void SyncSoundTrackToScene(bool recordUndo = true)
        {
            if (context.currentProject == null) return;

            GameObject gameManager = FindSceneObjectByName("GameManager");
            if (gameManager == null) return;

            SoundTrackUtility.ApplyToGameManager(
                gameManager,
                context.currentProject.sceneData.SoundTrack,
                recordUndo);

            // Keep an already-generated GameManager component in sync when the scene
            // does not need to be regenerated before Play.
            foreach (MonoBehaviour behaviour in gameManager.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "GameManager") continue;

                var field = behaviour.GetType().GetField("SoundTrack");
                if (field == null || field.FieldType != typeof(string)) continue;

                string value = context.currentProject.sceneData.SoundTrack ?? "";
                if (Equals(field.GetValue(behaviour), value)) continue;

                if (recordUndo)
                    Undo.RecordObject(behaviour, "Change Soundtrack");
                field.SetValue(behaviour, value);
                EditorUtility.SetDirty(behaviour);
            }

            EditorSceneManager.MarkSceneDirty(gameManager.scene);
        }

        /// <summary>
        /// The camera the generated GameManager drives — the same lookup it does at runtime
        /// (<c>GetComponentInChildren&lt;Camera&gt;</c>). Null when the scene has not been generated yet.
        /// </summary>
        private static Camera FindSceneCamera()
        {
            GameObject gameManager = FindSceneObjectByName("GameManager");
            if (gameManager == null) return null;

            return gameManager.GetComponentInChildren<Camera>(true);
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
                        name = "Rules Set",
                        id = "g" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                    }
                }
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
                case "Velocity": actor.Velocity = null; break;
                case "AngularVelocity": actor.AngularVelocity = null; break;
                case "Physics":
                    ActorPhysicsUtility.ClearOverrides(actor);
                    break;
            }

            // Sync reset values to scene
            SyncDataToScene(actor);

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

            // Push changes to scene
            SyncDataToScene(context.currentProject.actors[actorIndex]);

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

        // Push data from JSON to GameObject
        public void SyncDataToScene(ActorJson actor)
        {
            if (actor == null) return;
            GameObject obj = FindSceneObjectByName(actor.ActorName);
            if (obj == null)
            {
                Loader.EnsureTagExists(actor.Tag);
                return;
            }

            if (obj.activeSelf != actor.Active)
                obj.SetActive(actor.Active);

            if (!string.IsNullOrEmpty(actor.Tag) && obj.tag != actor.Tag)
            {
                Loader.TryApplyTag(obj, actor.Tag);
            }

            if (actor.Position != null && actor.Position.Length >= 3)
                obj.transform.position = new Vector3(actor.Position[0], actor.Position[1], actor.Position[2]);

            if (actor.Rotation != null && actor.Rotation.Length >= 3)
                obj.transform.eulerAngles = new Vector3(actor.Rotation[0], actor.Rotation[1], actor.Rotation[2]);

            if (actor.Scale != null && actor.Scale.Length >= 3)
                obj.transform.localScale = new Vector3(actor.Scale[0], actor.Scale[1], actor.Scale[2]);

            GameObject prefab = ActorPhysicsUtility.LoadPrefab(actor);
            if (ActorPhysicsUtility.ApplyTo(obj, actor, prefab, true) && obj.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(obj.scene);
        }

        // Pull data from GameObject to JSON if changed
        public bool SyncSceneToData(ActorJson actor)
        {
            if (actor == null) return false;
            GameObject obj = FindSceneObjectByName(actor.ActorName);
            if (obj == null) return false;

            bool changed = false;
            bool Diff(float a, float b) => Mathf.Abs(a - b) > 0.001f;

            // Tag can change without affecting transform.hasChanged.
            if (!string.IsNullOrEmpty(obj.tag) && actor.Tag != obj.tag)
            {
                actor.Tag = obj.tag;
                changed = true;
            }

            if (actor.Active != obj.activeSelf)
            {
                actor.Active = obj.activeSelf;
                changed = true;
            }

            // Do not persist velocities produced by a running simulation. In Edit mode,
            // Inspector changes to root Rigidbody/Collider components are bidirectional.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                GameObject prefab = ActorPhysicsUtility.LoadPrefab(actor);
                if (ActorPhysicsUtility.CaptureFrom(obj, actor, prefab))
                    changed = true;
            }

            if (obj.transform.hasChanged)
            {
                // Position Check
                Vector3 pos = obj.transform.position;
                if (actor.Position == null || actor.Position.Length < 3 || Diff(actor.Position[0], pos.x) || Diff(actor.Position[1], pos.y) || Diff(actor.Position[2], pos.z))
                {
                    if (actor.Position == null || actor.Position.Length < 3) actor.Position = new float[3];
                    actor.Position[0] = pos.x; actor.Position[1] = pos.y; actor.Position[2] = pos.z;
                    changed = true;
                }

                // Rotation Check
                Vector3 rot = obj.transform.eulerAngles;
                if (actor.Rotation == null || actor.Rotation.Length < 3 || Diff(actor.Rotation[0], rot.x) || Diff(actor.Rotation[1], rot.y) || Diff(actor.Rotation[2], rot.z))
                {
                    if (actor.Rotation == null || actor.Rotation.Length < 3) actor.Rotation = new float[3];
                    actor.Rotation[0] = rot.x; actor.Rotation[1] = rot.y; actor.Rotation[2] = rot.z;
                    changed = true;
                }

                // Scale Check
                Vector3 scl = obj.transform.localScale;
                if (actor.Scale == null || actor.Scale.Length < 3 || Diff(actor.Scale[0], scl.x) || Diff(actor.Scale[1], scl.y) || Diff(actor.Scale[2], scl.z))
                {
                    if (actor.Scale == null || actor.Scale.Length < 3) actor.Scale = new float[] { 1, 1, 1 };
                    actor.Scale[0] = scl.x; actor.Scale[1] = scl.y; actor.Scale[2] = scl.z;
                    changed = true;
                }

                obj.transform.hasChanged = false;
            }

            if (changed)
            {
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            }

            return changed;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            var activeOnly = GameObject.Find(objectName);
            if (activeOnly != null)
                return activeOnly;

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go == null || go.name != objectName)
                    continue;

                if (!go.scene.IsValid() || EditorUtility.IsPersistent(go))
                    continue;

                return go;
            }

            return null;
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
                Name = $"Rule {actor.Script.Count + 1}",
                When = hasCondition ? new List<string> { "" } : new List<string>(),
                Do = new List<string> { "" }
            };

            actor.Script.Add(newRule);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();

            context.SelectScript(actor.Script.Count - 1);
        }

        /// <summary>
        /// Adds a new empty rule to an actor's script. When a groupId is given the rule is tagged
        /// before notifying, so every listener (e.g. the Properties panel's rule counts) sees the
        /// final group on the first rebuild instead of a momentary null.
        /// </summary>
        public void AddEmptyRule(int actorIndex, string groupId = null)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Add Empty Rule");

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null)
            {
                actor.Script = new List<SentenceJson>();
            }

            SentenceJson newRule = new SentenceJson
            {
                Name = $"Rule {actor.Script.Count + 1}",
                When = new List<string>(),
                Do = new List<string>(),
                groupId = string.IsNullOrEmpty(groupId) ? null : groupId
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
        /// Moves a rule to a target index
        /// </summary>
        public void MoveRuleToIndex(int actorIndex, int fromIndex, int toIndex)
        {
            if (context.currentProject == null ||
                actorIndex < 0 ||
                actorIndex >= context.currentProject.actors.Count)
            {
                return;
            }

            ActorJson actor = context.currentProject.actors[actorIndex];
            if (actor.Script == null ||
                fromIndex < 0 ||
                fromIndex >= actor.Script.Count ||
                toIndex < 0 ||
                toIndex >= actor.Script.Count ||
                fromIndex == toIndex)
            {
                return;
            }

            Undo.RecordObject(context.currentProject, "Move Rule");

            SentenceJson moved = actor.Script[fromIndex];
            actor.Script.RemoveAt(fromIndex);
            toIndex = Mathf.Clamp(toIndex, 0, actor.Script.Count);
            actor.Script.Insert(toIndex, moved);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            context.SelectScript(toIndex);
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
                Name = string.IsNullOrEmpty(original.Name) ? "Rule (Copy)" : $"{original.Name} (Copy)",
                When = original.When != null ? new List<string>(original.When) : new List<string>(),
                Do = original.Do != null ? new List<string>(original.Do) : new List<string>(),
                groupId = original.groupId
            };

            actor.Script.Insert(ruleIndex + 1, duplicate);

            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            context.SelectScript(ruleIndex + 1);
        }

        /// <summary>
        /// Removes the condition from a rule, converting it to unconditional
        /// </summary>
        public void RemoveRuleCondition(int actorIndex, int ruleIndex)
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

            Undo.RecordObject(context.currentProject, "Remove Rule Condition");
            actor.Script[ruleIndex].When = new List<string>();
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Adds a condition to an unconditional rule, converting it to conditional
        /// </summary>
        public void AddRuleCondition(int actorIndex, int ruleIndex)
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

            Undo.RecordObject(context.currentProject, "Add Rule Condition");
            actor.Script[ruleIndex].When = new List<string> { "" };
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
        }

        /// <summary>
        /// Adds an action to a rule that has no actions yet
        /// </summary>
        public void AddRuleAction(int actorIndex, int ruleIndex)
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

            Undo.RecordObject(context.currentProject, "Add Rule Action");
            actor.Script[ruleIndex].Do = new List<string> { "" };
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
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
