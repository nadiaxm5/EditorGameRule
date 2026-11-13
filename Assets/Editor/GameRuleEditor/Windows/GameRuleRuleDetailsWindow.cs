using GameRuleEditor.Controllers;
using GameRuleEditor.Core;
using GameRuleEditor.Panels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameRuleEditor.Windows
{
    public sealed class GameRuleRuleDetailsWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;

        public static GameRuleRuleDetailsWindow EnsureVisible(EditorContext ctx, ProjectController ctrl)
        {
            var window = GetWindow<GameRuleRuleDetailsWindow>("Rule Details", false);
            window.minSize = new Vector2(320, 300);
            window.Init(ctx, ctrl);
            window.Show();
            return window;
        }

        public void Init(EditorContext ctx, ProjectController ctrl)
        {
            context = ctx;
            controller = ctrl;
            BuildUI();
        }

        private void OnEnable()
        {
            if (context == null)
                context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            if (context != null && controller == null)
                controller = GameRuleLayoutManager.GetOrCreateController(context);
            if (context != null) BuildUI();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            GameRuleTheme.Configure(root, BuildUI);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.style.flexGrow = 1;
            root.style.backgroundColor = GameRuleTheme.Background;


            if (context != null && controller != null)
                root.Add(new RuleDetailsPanel(context, controller));
        }
    }
}
