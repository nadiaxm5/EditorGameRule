using UnityEditor;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Legacy entry point — redirects to the new multi-window layout system.
    /// Kept for backward compatibility with the "GameRule/Editor Window" menu item.
    /// </summary>
    public class GameRuleEditorWindow : EditorWindow
    {
        [MenuItem("GameRule/Editor Window")]
        public static void ShowWindow()
        {
            // Redirect to the new layout manager
            GameRuleLayoutManager.OpenLayout();
        }
    }
}