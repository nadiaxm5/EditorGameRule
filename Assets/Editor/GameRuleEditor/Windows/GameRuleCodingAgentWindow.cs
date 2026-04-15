using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Bottom-dock editor window that embeds the compact GameAgentMini UI
    /// (the DrawAgentGUI mini interface) inside the new GameRule Editor layout.
    /// The agent itself stays a shared singleton (<see cref="GameAgentMini.GetOrCreateInstance"/>)
    /// so model state and chat history survive open/close of this host window.
    /// </summary>
    public class GameRuleCodingAgentWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;
        private GameAgentMini agent;
        private IMGUIContainer guiContainer;
        private Label titleLabel;

        public static GameRuleCodingAgentWindow EnsureVisible(EditorContext ctx, ProjectController ctrl)
        {
            var dockType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            var window = dockType != null
                ? GetWindow<GameRuleCodingAgentWindow>("Coding Agent", false, dockType)
                : GetWindow<GameRuleCodingAgentWindow>("Coding Agent", false);
            window.minSize = new Vector2(360, 140);
            window.Init(ctx, ctrl);
            window.Show();
            window.Focus();
            return window;
        }

        [MenuItem("GameRule/Coding Agent")]
        public static void OpenMenu()
        {
            var ctx = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            var ctrl = ctx != null ? GameRuleLayoutManager.GetOrCreateController(ctx) : null;
            EnsureVisible(ctx, ctrl);
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

            agent = GameAgentMini.GetOrCreateInstance();
            BuildUI();
        }

        private void OnDisable()
        {
            // Shared singleton — never destroy here.
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f);

            BuildHeader(root);
            BuildBody(root);
        }

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.height = 32;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 6;
            header.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.102f, 0.102f, 0.102f);
            header.style.flexShrink = 0;

            titleLabel = new Label("Coding Agent");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.898f, 0.906f, 0.922f);
            header.Add(titleLabel);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            var popOutBtn = FlatIconButton("\u29C9", "Open Full Window", () => GameAgentMini.ShowWindow());
            rightGroup.Add(popOutBtn);

            var closeBtn = FlatIconButton("\u2715", "Close Window", () => this.Close());
            rightGroup.Add(closeBtn);

            header.Add(rightGroup);
            root.Add(header);
        }

        private static Button FlatIconButton(string text, string tooltip, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text, tooltip = tooltip };
            btn.style.width = 24;
            btn.style.height = 24;
            btn.style.fontSize = 12;
            btn.style.backgroundColor = Color.clear;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.color = new Color(0.898f, 0.906f, 0.922f);
            return btn;
        }

        private void BuildBody(VisualElement root)
        {
            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingLeft = 6;
            body.style.paddingRight = 6;
            body.style.paddingTop = 2;
            body.style.paddingBottom = 4;
            root.Add(body);

            guiContainer = new IMGUIContainer(DrawAgent);
            guiContainer.style.flexGrow = 1;
            body.Add(guiContainer);

            // While the agent is loading/generating, its internal Repaint() targets
            // the hidden singleton window. Pump the host IMGUIContainer so the user
            // actually sees streaming updates and busy state in this docked panel.
            guiContainer.schedule.Execute(() =>
            {
                if (agent != null && agent.IsBusy)
                    guiContainer.MarkDirtyRepaint();
            }).Every(80);
        }

        private void DrawAgent()
        {
            if (agent == null) agent = GameAgentMini.GetOrCreateInstance();
            if (agent == null)
            {
                EditorGUILayout.HelpBox("Could not initialize Coding Agent.", MessageType.Warning);
                return;
            }
            agent.DrawAgentGUI();
        }
    }
}
