using System.IO;
using UnityEditor;
using UnityEngine;
using GameRuleEditor.Controllers;
using GameRuleEditor.Core;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Unity top menu integration for GameRule actions.
    /// Mirrors the Project toolbar menu so actions are available from the main menu bar.
    /// </summary>
    public static class GameRuleTopMenuActions
    {
        [MenuItem("GameRule/Actions/New Project", priority = 20)]
        public static void NewProject()
        {
            if (!TryGetContextAndController(out var context, out var controller)) return;

            string newPath = EditorUtility.SaveFilePanelInProject(
                "Create New GameRule Project", "NewProject", "asset", "Choose where to save the new project");
            if (string.IsNullOrEmpty(newPath)) return;

            string projectName = Path.GetFileNameWithoutExtension(newPath);
            controller.CreateNewProject(projectName);
            AssetDatabase.CreateAsset(context.currentProject, newPath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("GameRule/Actions/Open Project", priority = 21)]
        public static void OpenProject()
        {
            if (!TryGetContextAndController(out _, out var controller)) return;

            string path = EditorUtility.OpenFilePanel("Open Project", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(path);
            if (project != null)
            {
                controller.LoadProject(project);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a GameRule Project.", "OK");
            }
        }

        [MenuItem("GameRule/Actions/Import version", priority = 23)]
        public static void ImportJson()
        {
            if (!TryGetContextAndController(out _, out var controller)) return;

            string jsonPath = EditorUtility.OpenFilePanel(
                "Import version", Application.dataPath + "/Resources/Games", "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            controller.ImportJsonAsProject(jsonPath);
        }

        [MenuItem("GameRule/Actions/Export version", priority = 24)]
        public static void ExportJson()
        {
            if (!TryGetContextAndController(out var context, out var controller)) return;
            if (context.currentProject == null)
            {
                EditorUtility.DisplayDialog("No Project", "Load or create a project first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export version",
                Application.dataPath + "/Resources/Games",
                context.currentProject.projectName + ".json",
                "json");
            if (string.IsNullOrEmpty(path)) return;

            controller.SaveProjectToJson(path);
            SaveProject();
            EditorUtility.DisplayDialog("Success", "Project exported successfully", "OK");
        }

        [MenuItem("GameRule/Actions/Save Project", priority = 25)]
        public static void SaveProject()
        {
            if (!TryGetContextAndController(out var context, out var controller)) return;
            if (context.currentProject == null)
            {
                EditorUtility.DisplayDialog("No Project", "Load or create a project first.", "OK");
                return;
            }

            EditorUtility.SetDirty(context.currentProject);
            AssetDatabase.SaveAssets();

            string gamesFolder = Path.Combine(Application.dataPath, "Resources/Games");
            if (!Directory.Exists(gamesFolder))
            {
                Directory.CreateDirectory(gamesFolder);
            }

            string jsonPath = Path.Combine(gamesFolder, context.currentProject.projectName + ".json");
            controller.SaveProjectToJson(jsonPath);
            AssetDatabase.Refresh();
            Debug.Log($"Project saved and JSON exported: {jsonPath}");
        }

        [MenuItem("GameRule/Actions/Close Project", priority = 27)]
        public static void CloseProject()
        {
            if (!TryGetContextAndController(out var context, out _)) return;
            context.currentProject = null;
            context.CloseInspector();
            EditorUtility.SetDirty(context);
            AssetDatabase.SaveAssets();
        }

        private static bool TryGetContextAndController(out EditorContext context, out ProjectController controller)
        {
            context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            controller = null;

            if (context == null)
            {
                GameRuleLayoutManager.OpenLayout();
                context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            }

            if (context == null)
            {
                EditorUtility.DisplayDialog("GameRule", "Could not initialize EditorContext.", "OK");
                return false;
            }

            controller = GameRuleLayoutManager.GetOrCreateController(context);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("GameRule", "Could not initialize ProjectController.", "OK");
                return false;
            }

            return true;
        }
    }
}