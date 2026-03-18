using UnityEngine;
using UnityEditor;
using System.IO;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Handles opening and arranging all GameRule Editor windows in Unity's layout.
    /// Owns the single shared ProjectController instance used by all windows.
    /// </summary>
    public static class GameRuleLayoutManager
    {
        private const string CONTEXT_FOLDER = "Assets/Editor/GameRuleEditor/Projects";
        private const string CONTEXT_PATH = CONTEXT_FOLDER + "/EditorContext.asset";

        /// <summary>
        /// The single shared ProjectController. Both windows must use this same instance.
        /// After domain reload (play mode, recompile) statics are reset to null,
        /// so the first window that calls GetOrCreateController will recreate it.
        /// </summary>
        private static ProjectController _sharedController;

        /// <summary>
        /// Returns the shared ProjectController, creating it if necessary.
        /// This ensures only ONE controller is ever active, preventing duplicate
        /// EditorApplication.update and Undo.undoRedoPerformed subscriptions.
        /// </summary>
        public static ProjectController GetOrCreateController(EditorContext context)
        {
            if (context == null) return null;

            if (_sharedController == null)
            {
                _sharedController = new ProjectController(context);
                _sharedController.Enable();
            }
            return _sharedController;
        }

        /// <summary>
        /// Returns the path to the EditorContext asset.
        /// </summary>
        public static string ContextPath => CONTEXT_PATH;

        /// <summary>
        /// Main entry point: opens all GameRule windows and arranges them.
        /// </summary>
        public static void OpenLayout()
        {
            var context = EnsureEditorContext();
            var controller = GetOrCreateController(context);

            // 0. Open Toolbar window (docked top preferably)
            var toolbarWindow = EditorWindow.GetWindow<GameRuleToolbarWindow>("GR Toolbar", false);
            toolbarWindow.minSize = new Vector2(300, 32);
            toolbarWindow.maxSize = new Vector2(4000, 32);
            toolbarWindow.Init(context, controller);

            // 1. Open Hierarchy window (docked next to Unity's Hierarchy)
            var hierarchyWindow = EditorWindow.GetWindow<GameRuleHierarchyWindow>(
                "GR Hierarchy", false, typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow"));
            hierarchyWindow.minSize = new Vector2(256, 300);
            hierarchyWindow.Init(context, controller);

            // 2. Open Inspector window (docked next to Unity's Inspector)
            var inspectorWindow = EditorWindow.GetWindow<GameRuleInspectorWindow>(
                "GR Inspector", false, typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
            inspectorWindow.minSize = new Vector2(320, 300);
            inspectorWindow.Init(context, controller);

            // If no project loaded, show modal dialog
            if (context.currentProject == null)
            {
                ShowStartDialog(context, controller, hierarchyWindow);
            }
        }

        /// <summary>
        /// Shows a modal start dialog to create or open a project.
        /// </summary>
        private static void ShowStartDialog(EditorContext context, ProjectController controller, GameRuleHierarchyWindow hierarchyWindow)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "GameRule Editor",
                "No project loaded. What would you like to do?",
                "Create New Project",
                "Cancel",
                "Open Existing Project"
            );

            switch (choice)
            {
                case 0: // Create New
                    string newPath = EditorUtility.SaveFilePanelInProject(
                        "Create New GameRule Project", "NewProject", "asset",
                        "Choose where to save the new project");
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        string projectName = Path.GetFileNameWithoutExtension(newPath);
                        controller.CreateNewProject(projectName);
                        AssetDatabase.CreateAsset(context.currentProject, newPath);
                        AssetDatabase.SaveAssets();
                        // Re-init hierarchy
                        hierarchyWindow.Init(context, controller);
                    }
                    break;

                case 2: // Open Existing
                    string openPath = EditorUtility.OpenFilePanel("Open GameRule Project", "Assets", "asset");
                    if (!string.IsNullOrEmpty(openPath))
                    {
                        if (openPath.StartsWith(Application.dataPath))
                            openPath = "Assets" + openPath.Substring(Application.dataPath.Length);

                        var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(openPath);
                        if (project != null)
                        {
                            controller.LoadProject(project);
                            hierarchyWindow.Init(context, controller);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Error", "Selected file is not a GameRule Project.", "OK");
                        }
                    }
                    break;

                case 1: // Cancel
                default:
                    break;
            }
        }

        /// <summary>
        /// Ensures the EditorContext ScriptableObject exists and returns it.
        /// </summary>
        private static EditorContext EnsureEditorContext()
        {
            var context = AssetDatabase.LoadAssetAtPath<EditorContext>(CONTEXT_PATH);

            if (context == null)
            {
                if (!Directory.Exists(CONTEXT_FOLDER))
                {
                    Directory.CreateDirectory(CONTEXT_FOLDER);
                    AssetDatabase.Refresh();
                }

                context = ScriptableObject.CreateInstance<EditorContext>();
                AssetDatabase.CreateAsset(context, CONTEXT_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"Initialized GameRule EditorContext at: {CONTEXT_PATH}");
            }

            return context;
        }
    }
}
