using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.Panels;

namespace GameRuleEditor.Windows
{
    public class GameRuleSceneWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;
        private Label titleLabel;

        public static GameRuleSceneWindow EnsureVisible(EditorContext ctx, ProjectController ctrl)
        {
            var window = GetWindow<GameRuleSceneWindow>("Scene Settings", false);
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
            if (context != null) BuildUI();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f); // panelBackground

            // Header
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.height = 32;
            headerContainer.style.paddingLeft = 10;
            headerContainer.style.paddingRight = 6;
            headerContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f); // headerBackground
            headerContainer.style.borderBottomWidth = 1;
            headerContainer.style.borderBottomColor = new Color(0.102f, 0.102f, 0.102f);
            headerContainer.style.flexShrink = 0;

            titleLabel = new Label("Scene Settings");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.898f, 0.906f, 0.922f); // textPrimary
            headerContainer.Add(titleLabel);

            var closeBtn = new Button(() => this.Close());
            closeBtn.text = "\u2715"; // ✕
            closeBtn.tooltip = "Close Window";
            closeBtn.style.width = 24;
            closeBtn.style.height = 24;
            closeBtn.style.fontSize = 12;
            closeBtn.style.backgroundColor = Color.clear;
            closeBtn.style.borderLeftWidth = 0;
            closeBtn.style.borderRightWidth = 0;
            closeBtn.style.borderTopWidth = 0;
            closeBtn.style.borderBottomWidth = 0;
            headerContainer.Add(closeBtn);

            root.Add(headerContainer);

            // Content
            var contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            if (context != null)
                contentContainer.Add(new SceneSettingsPanel(context, controller));
        }
    }
}