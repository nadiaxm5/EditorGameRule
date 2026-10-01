using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using System.Reflection;
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

           /* // 0. Open Toolbar window (docked top preferably)
            var toolbarWindow = EditorWindow.GetWindow<GameRuleToolbarWindow>("Tools", false);
            toolbarWindow.minSize = new Vector2(300, 32);
            toolbarWindow.maxSize = new Vector2(4000, 32);
            toolbarWindow.Init(context, controller);
*/
            // 1. Open Hierarchy window (docked next to Unity's Hierarchy)
            var hierarchyWindow = EditorWindow.GetWindow<GameRuleHierarchyWindow>(
                "Cast", false, typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow"));
            hierarchyWindow.minSize = new Vector2(256, 300);
            hierarchyWindow.Init(context, controller);

            // 2. Open Inspector window (docked next to Unity's Inspector), 
            var sceneWin = EditorWindow.GetWindow<GameRuleSceneWindow>("Scene Settings", false, typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
            sceneWin.minSize = new Vector2(320, 300);
            sceneWin.Init(context, controller);

            var propsWin = EditorWindow.GetWindow<GameRulePropertiesWindow>("Properties", false, typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
            propsWin.minSize = new Vector2(320, 300);
            propsWin.Init(context, controller);

            var rulesWin = EditorWindow.GetWindow<GameRuleRulesWindow>("Rules", false, typeof(Editor).Assembly.GetType("UnityEditor.SceneView"));
            rulesWin.minSize = new Vector2(400, 300);
            rulesWin.Init(context, controller);

            var detailsWin = Resources.FindObjectsOfTypeAll<GameRuleRuleDetailsWindow>()
                .FirstOrDefault() ?? EditorWindow.GetWindow<GameRuleRuleDetailsWindow>(
                    "Rule Details", false, typeof(GameRulePropertiesWindow));
            detailsWin.minSize = new Vector2(320, 300);
            detailsWin.Init(context, controller);

            // Unity's public GetWindow API only creates tabs. Split the existing horizontal
            // dock group so Rule Details remains visible between Rules and Properties.
            TryArrangeStudioDocks(hierarchyWindow, rulesWin, detailsWin, propsWin);
            // Unity may still be attaching a freshly created DockArea on the first
            // delayCall. A second idempotent pass handles that first-open case.
            EditorApplication.delayCall += () =>
            {
                TryArrangeStudioDocks(hierarchyWindow, rulesWin, detailsWin, propsWin);
                EditorApplication.delayCall += () => TryArrangeStudioDocks(
                    hierarchyWindow, rulesWin, detailsWin, propsWin);
            };
            
            // If no project loaded, show modal dialog
            if (context.currentProject == null)
            {
                ShowStartDialog(context, controller, hierarchyWindow);
            }
        }

        private static void TryArrangeStudioDocks(EditorWindow cast, EditorWindow rules,
                                                   EditorWindow details, EditorWindow properties)
        {
            if (cast == null || rules == null || details == null || properties == null) return;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var editorAssembly = typeof(EditorWindow).Assembly;
                Type viewType = editorAssembly.GetType("UnityEditor.View");
                Type dockType = editorAssembly.GetType("UnityEditor.DockArea");
                Type splitType = editorAssembly.GetType("UnityEditor.SplitView");
                FieldInfo parentField = typeof(EditorWindow).GetField("m_Parent", flags);
                if (viewType == null || dockType == null || splitType == null || parentField == null) return;

                object castDock = parentField.GetValue(cast);
                object rulesDock = parentField.GetValue(rules);
                object detailsDock = parentField.GetValue(details);
                object propertiesDock = parentField.GetValue(properties);
                var viewParent = viewType.GetProperty("parent", flags);
                var viewChildren = viewType.GetProperty("children", flags);
                var viewPosition = viewType.GetProperty("position", flags);
                var removeChild = splitType.GetMethod("RemoveChild", flags, null,
                    new[] { viewType }, null);
                var addChild = splitType.GetMethod("AddChild", flags, null,
                    new[] { viewType, typeof(int) }, null);
                object split = viewParent?.GetValue(rulesDock);
                if (split == null || !splitType.IsInstanceOfType(split) ||
                    !ReferenceEquals(viewParent.GetValue(castDock), split) ||
                    !ReferenceEquals(viewParent.GetValue(propertiesDock), split))
                {
                    Debug.LogWarning("GameRule: the existing dock layout is not a horizontal studio group; Rule Details remains available as a tab.");
                    return;
                }

                // Older layouts may contain a stale pane reference from a previous
                // move. Drop only duplicate references to this window, never other tabs.
                FieldInfo panesField = dockType.GetField("m_Panes", flags);
                if (panesField != null && viewChildren != null && removeChild != null)
                {
                    foreach (object child in ((System.Collections.IEnumerable)viewChildren.GetValue(split))
                        .Cast<object>().ToArray())
                    {
                        if (!dockType.IsInstanceOfType(child) || ReferenceEquals(child, detailsDock))
                            continue;
                        var panes = panesField.GetValue(child) as System.Collections.IList;
                        if (panes == null || !panes.Contains(details)) continue;
                        panes.Remove(details);
                        if (panes.Count == 0) removeChild.Invoke(split, new[] { child });
                    }
                }

                if (ReferenceEquals(detailsDock, propertiesDock))
                {
                    MethodInfo removeTab = dockType.GetMethod("RemoveTab", flags, null,
                        new[] { typeof(EditorWindow), typeof(bool), typeof(bool) }, null);
                    object newDock = ScriptableObject.CreateInstance(dockType);
                    MethodInfo addTab = dockType.GetMethod("AddTab", flags, null,
                        new[] { typeof(EditorWindow), typeof(bool) }, null);
                    if (removeTab == null || addTab == null || addChild == null) return;
                    removeTab.Invoke(propertiesDock, new object[] { details, false, true });
                    addTab.Invoke(newDock, new object[] { details, true });
                    if (!ReferenceEquals(parentField.GetValue(details), newDock)) return;
                    detailsDock = newDock;
                    addChild.Invoke(split, new object[] { detailsDock, 2 });
                }

                object[] desired = { castDock, rulesDock, detailsDock, propertiesDock };
                if (removeChild == null || addChild == null || viewChildren == null || viewPosition == null) return;

                for (int i = 0; i < desired.Length; i++)
                {
                    var children = ((Array)viewChildren.GetValue(split)).Cast<object>().ToArray();
                    if (i < children.Length && ReferenceEquals(children[i], desired[i])) continue;
                    removeChild.Invoke(split, new[] { desired[i] });
                    addChild.Invoke(split, new object[] { desired[i], i });
                }

                Rect bounds = (Rect)viewPosition.GetValue(split);
                float[] fractions = { 0.14f, 0.43f, 0.21f, 0.22f };
                float x = 0;
                for (int i = 0; i < desired.Length; i++)
                {
                    float width = i == desired.Length - 1 ? bounds.width - x : bounds.width * fractions[i];
                    viewPosition.SetValue(desired[i], new Rect(x, 0, width, bounds.height));
                    x += width;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("GameRule: could not arrange the four dock groups automatically: " + error.Message);
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
                    ProjectController.EnsureProjectsFolder();
                    string newPath = EditorUtility.SaveFilePanelInProject(
                        "Create New GameRule Project", "NewProject", "asset",
                        "Choose where to save the new project",
                        ProjectController.ProjectsAssetFolder);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        string projectName = Path.GetFileNameWithoutExtension(newPath);
                        controller.CreateAndGenerateProject(projectName, newPath);
                        // Re-init hierarchy
                        hierarchyWindow.Init(context, controller);
                    }
                    break;

                case 2: // Open Existing
                    ProjectController.EnsureProjectsFolder();
                    string openPath = EditorUtility.OpenFilePanel(
                        "Open GameRule Project", ProjectController.ProjectsAbsoluteFolder, "asset");
                    if (!string.IsNullOrEmpty(openPath))
                    {
                        if (openPath.StartsWith(Application.dataPath))
                            openPath = "Assets" + openPath.Substring(Application.dataPath.Length);

                        var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(openPath);
                        if (project != null)
                        {
                            controller.OpenAndGenerateProject(project);
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
