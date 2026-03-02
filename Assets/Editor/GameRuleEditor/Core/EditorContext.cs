using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameRuleEditor.Core
{
    /// <summary>
    /// ScriptableObject that maintains the global state of the GameRule Editor.
    /// This allows different panels to communicate and stay synchronized.
    /// </summary>
    [CreateAssetMenu(fileName = "EditorContext", menuName = "GameRule/Editor Context", order = 2)]
    public class EditorContext : ScriptableObject
    {
        [Header("Current Project")]
        public GameRuleProject currentProject;

        [Header("Selection State")]
        public int selectedActorIndex = -1;
        public int selectedScriptIndex = -1;

        // Events for UI synchronization
        public event System.Action OnProjectLoaded;
        public event System.Action OnProjectChanged;
        public event System.Action<int> OnActorSelected;
        public event System.Action OnActorListChanged;
        public event System.Action<int> OnScriptSelected;

        /// <summary>
        /// Para evitar que durante un undo/redo, algunos paneles ignoren la actualización porque creen que no están enfocados o activos. Al establecer esta bandera, los paneles pueden saber que deben actualizarse incluso si normalmente no lo harían. Después de la actualización, se debe restablecer a false.
        /// </summary>
        [System.NonSerialized]
        public bool isUndoRedoRefresh;

        /// <summary>
        /// Gets the currently selected actor, or null if none selected
        /// </summary>
        public ActorJson SelectedActor
        {
            get
            {
                if (currentProject == null || selectedActorIndex < 0 ||
                    selectedActorIndex >= currentProject.actors.Count)
                {
                    return null;
                }
                return currentProject.actors[selectedActorIndex];
            }
        }

        /// <summary>
        /// Gets the currently selected script (rule), or null if none selected
        /// </summary>
        public SentenceJson SelectedScript
        {
            get
            {
                var actor = SelectedActor;
                if (actor == null || selectedScriptIndex < 0 ||
                    selectedScriptIndex >= actor.Script.Count)
                {
                    return null;
                }
                return actor.Script[selectedScriptIndex];
            }
        }

        /// <summary>
        /// Loads a project and notifies all listeners
        /// </summary>
        public void LoadProject(GameRuleProject project)
        {
            currentProject = project;
            selectedActorIndex = -1;
            selectedScriptIndex = -1;

            OnProjectLoaded?.Invoke();
        }

        /// <summary>
        /// Notifies that the project has been modified
        /// </summary>
        public void NotifyProjectChanged()
        {
            OnProjectChanged?.Invoke();
        }

        /// <summary>
        /// Selects an actor by index
        /// </summary>
        public void SelectActor(int index)
        {
            if (currentProject == null || index < 0 || index >= currentProject.actors.Count)
            {
                selectedActorIndex = -1;
                selectedScriptIndex = -1;
            }
            else
            {
                selectedActorIndex = index;
                selectedScriptIndex = -1; // Reset script selection
            }

            OnActorSelected?.Invoke(selectedActorIndex);
        }

        /// <summary>
        /// Selects a script/rule by index within the current actor
        /// </summary>
        public void SelectScript(int index)
        {
            if (SelectedActor == null || index < 0 || index >= SelectedActor.Script.Count)
            {
                selectedScriptIndex = -1;
            }
            else
            {
                selectedScriptIndex = index;
            }

            OnScriptSelected?.Invoke(selectedScriptIndex);
        }

        /// <summary>
        /// Notifies that the actor list has changed (add/remove/duplicate)
        /// </summary>
        public void NotifyActorListChanged()
        {
            OnActorListChanged?.Invoke();
        }

        /// <summary>
        /// Lo llama todo para forzar que toda la UI se actualice después de un undo/redo, asegurándonos de que todos los paneles vuelvan a leer el estado actual del proyecto y la selección. Sin esto, algunos paneles podrían quedar desincronizados con el estado real después de un undo/redo.
        /// </summary>
        public void NotifyAll()
        {
            OnActorListChanged?.Invoke();
            OnProjectChanged?.Invoke();
            OnActorSelected?.Invoke(selectedActorIndex);
            OnScriptSelected?.Invoke(selectedScriptIndex);
        }

        /// <summary>
        /// Creates a new empty project
        /// </summary>
        public void CreateNewProject(string projectName)
        {
            GameRuleProject newProject = CreateInstance<GameRuleProject>();
            newProject.projectName = projectName;
            newProject.sceneData = new SceneJson
            {
                GameName = projectName,
                ScreenResolution = new float[] { 1920, 1080 },
                CameraPosition = new float[] { 0, 1, -10 },
                CameraRotation = new float[] { 0, 0, 0 },
                SunPosition = new float[] { 0, 3, 0 },
                SunRotation = new float[] { 50, -30, 0 },
                SunColor = new byte[] { 255, 255, 255 },
                SunAmbientColor = new byte[] { 128, 128, 128 },
                BackgroundColor = new byte[] { 0, 0, 0 },
                Gravity = new float[] { 0, -9.81f, 0 },
                CustomVariables = new List<CustomVariable>(),
                Cast = new List<ActorJson>()
            };

            LoadProject(newProject);
        }

        /// <summary>
        /// Clears the current context
        /// </summary>
        public void Clear()
        {
            currentProject = null;
            selectedActorIndex = -1;
            selectedScriptIndex = -1;
        }
    }
}
