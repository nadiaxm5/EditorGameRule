using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.Panels;

namespace GameRuleEditor.Windows
{
    public class GameRulePropertiesWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;

        public static GameRulePropertiesWindow EnsureVisible(EditorContext ctx, ProjectController ctrl)
        {
            var window = GetWindow<GameRulePropertiesWindow>("Properties", false);
            window.minSize = new Vector2(320, 300);
            window.Init(ctx, ctrl);
            window.Show();
            window.Focus();
            return window;
        }

        public void Init(EditorContext ctx, ProjectController ctrl)
        {
            context = ctx; controller = ctrl;
            BuildUI();
        }

        private void OnEnable()
        {
            if (context == null) context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            if (context != null && controller == null) controller = GameRuleLayoutManager.GetOrCreateController(context);
            if (context != null)
            {
                BuildUI();
            }
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            GameRuleTheme.Configure(root, BuildUI);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            root.style.flexGrow = 1;
            root.style.backgroundColor = GameRuleTheme.Background; // panelBackground

            // Content
            var contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            if (context != null)
                contentContainer.Add(new ActorDetailsPanel(context, controller));
        }
    }
}
