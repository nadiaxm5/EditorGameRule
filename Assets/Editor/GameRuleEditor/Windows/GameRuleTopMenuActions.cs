using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameRuleEditor.Controllers;
using GameRuleEditor.Core;

namespace GameRuleEditor.Windows
{
    /// <summary>Shared entry points for the main menu, Cast and Tools.</summary>
    public static class GameRuleTopMenuActions
    {
        [MenuItem("GameRule/Project Actions/New Project", priority = 10)]
        public static void NewProject() => Run(() =>
        {
            if (TryGetContextAndController(out _, out var controller))
                GameRuleProjectDialog.ShowNew(controller);
        });

        [MenuItem("GameRule/Project Actions/Open Project", priority = 11)]
        public static void OpenProject() => Run(() =>
        {
            if (TryGetContextAndController(out _, out var controller))
                GameRuleProjectDialog.ShowOpen(controller);
        });

        [MenuItem("GameRule/Project Actions/Save Project", priority = 12)]
        public static void SaveProject() => Run(() =>
        {
            if (TryGetContextAndController(out _, out var controller)) controller.SaveProject();
        });

        [MenuItem("GameRule/Project Actions/Close Project", priority = 13)]
        public static void CloseProject() => Run(() =>
        {
            if (TryGetContextAndController(out _, out var controller)) controller.CloseProject();
        });

        [MenuItem("GameRule/JSON Actions/Import JSON", priority = 30)]
        public static void ImportJson() => Run(() =>
        {
            if (!TryGetContextAndController(out _, out var controller)) return;
            string path = EditorUtility.OpenFilePanel("Import JSON",
                Path.Combine(Application.dataPath, "Resources/Games"), "json");
            if (!string.IsNullOrEmpty(path)) controller.ImportJsonAsProject(path);
        });

        [MenuItem("GameRule/JSON Actions/Export JSON", priority = 31)]
        public static void ExportJson() => Run(() =>
        {
            if (!TryGetContextAndController(out var context, out var controller) ||
                context.currentProject == null) return;
            string path = EditorUtility.SaveFilePanel("Export JSON",
                Path.Combine(Application.dataPath, "Resources/Games"),
                context.currentProject.projectName + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            // Export is a snapshot, not an implicit Save or a new save destination.
            controller.SaveProjectToJson(path);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export JSON", "Project exported successfully.", "OK");
        });

        [MenuItem("GameRule/Project Actions/New Project", true)]
        [MenuItem("GameRule/Project Actions/Open Project", true)]
        [MenuItem("GameRule/JSON Actions/Import JSON", true)]
        public static bool CanManageProjects() =>
            !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        [MenuItem("GameRule/Project Actions/Save Project", true)]
        [MenuItem("GameRule/Project Actions/Close Project", true)]
        [MenuItem("GameRule/JSON Actions/Export JSON", true)]
        public static bool HasEditableProject() => CanManageProjects() &&
            AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath)?.currentProject != null;

        public static void PopulateProjectMenu(DropdownMenu menu)
        {
            menu.AppendAction("New Project", _ => NewProject(), _ => Status(CanManageProjects()));
            menu.AppendAction("Open Project", _ => OpenProject(), _ => Status(CanManageProjects()));
            menu.AppendAction("Save Project", _ => SaveProject(), _ => Status(HasEditableProject()));
            menu.AppendAction("Close Project", _ => CloseProject(), _ => Status(HasEditableProject()));
        }

        public static void PopulateJsonMenu(DropdownMenu menu)
        {
            menu.AppendAction("Import JSON", _ => ImportJson(), _ => Status(CanManageProjects()));
            menu.AppendAction("Export JSON", _ => ExportJson(), _ => Status(HasEditableProject()));
        }

        private static DropdownMenuAction.Status Status(bool enabled) =>
            enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;

        internal static void Run(System.Action operation)
        {
            if (!CanManageProjects()) return;
            try { operation(); }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("GameRule", error.Message, "OK");
            }
        }

        private static bool TryGetContextAndController(out EditorContext context, out ProjectController controller)
        {
            context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            if (context == null)
            {
                GameRuleLayoutManager.OpenLayout(false);
                context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            }
            controller = context == null ? null : GameRuleLayoutManager.GetOrCreateController(context);
            return controller != null;
        }
    }
}
