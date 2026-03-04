using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.Panels;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Right panel — Conditional inspector that shows SceneSettings, ActorProperties, or ActorRules
    /// based on the EditorContext.activeInspectorMode state machine.
    /// </summary>
    public class GameRuleInspectorWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;

        private VisualElement contentContainer;
        private Label titleLabel;
        private VisualElement headerContainer;

        /// <summary>
        /// Opens or focuses the inspector window, initializing it with the given context.
        /// </summary>
        public static GameRuleInspectorWindow EnsureVisible(EditorContext ctx, ProjectController ctrl)
        {
            if (ctx.activeInspectorMode == GRInspectorMode.None) return null;

            var window = GetWindow<GameRuleInspectorWindow>("Inspector", false);
            window.minSize = new Vector2(320, 300);
            window.Init(ctx, ctrl);
            window.Show();
            return window;
        }

        public void Init(EditorContext editorContext, ProjectController projectController)
        {
            bool needsRebuild = (context != editorContext);
            UnsubscribeEvents();
            context = editorContext;
            controller = projectController;

            if (needsRebuild)
            {
                SubscribeEvents();
                BuildUI();
            }
            else
            {
                SubscribeEvents();
                RefreshContent();
            }
        }

        /// <summary>
        /// Called by Unity after domain reload (Play mode, recompile).
        /// Re-acquires the shared EditorContext and ProjectController.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (context == null)
            {
                context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            }
            // Use the shared controller — never create a private one
            if (context != null && controller == null)
            {
                controller = GameRuleLayoutManager.GetOrCreateController(context);
            }
            if (context != null)
            {
                SubscribeEvents();
                BuildUI();
            }
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnsubscribeEvents();
            // Don't disable the shared controller — other windows may still use it
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (context != null)
                    BuildUI();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            // Don't disable the shared controller here
        }

        private void SubscribeEvents()
        {
            if (context == null) return;
            context.OnInspectorModeChanged += OnInspectorModeChanged;
            context.OnProjectChanged += OnProjectChanged;
            context.OnActorSelected += OnActorSelected;
            context.OnProjectLoaded += OnProjectLoaded;
        }

        private void UnsubscribeEvents()
        {
            if (context == null) return;
            context.OnInspectorModeChanged -= OnInspectorModeChanged;
            context.OnProjectChanged -= OnProjectChanged;
            context.OnActorSelected -= OnActorSelected;
            context.OnProjectLoaded -= OnProjectLoaded;
        }

        // ──────────────────────────────────
        //  EVENT HANDLERS
        // ──────────────────────────────────
        private void OnInspectorModeChanged(GRInspectorMode mode)
        {
            if (mode == GRInspectorMode.None)
            {
                // Optionally close or show empty state
                RefreshContent();
                return;
            }
            RefreshContent();
        }

        private void OnProjectChanged()
        {
            // Pass through to embedded panels (they handle their own refresh)
        }

        private void OnActorSelected(int index)
        {
            // If we're in actor mode and actor changed, refresh
            if (context.activeInspectorMode == GRInspectorMode.ActorProps ||
                context.activeInspectorMode == GRInspectorMode.ActorRules)
            {
                RefreshContent();
            }
        }

        private void OnProjectLoaded()
        {
            RefreshContent();
        }

        // ──────────────────────────────────
        //  UI BUILDING
        // ──────────────────────────────────
        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f); // panelBackground

            // Header
            headerContainer = new VisualElement();
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

            titleLabel = new Label("Inspector");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.898f, 0.906f, 0.922f); // textPrimary
            headerContainer.Add(titleLabel);

            var closeBtn = new Button(() =>
            {
                context.CloseInspector();
                RefreshContent();
            });
            closeBtn.text = "\u2715"; // ✕
            closeBtn.tooltip = "Close Inspector";
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
            contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            RefreshContent();
        }

        // ──────────────────────────────────
        //  CONTENT SWITCHING
        // ──────────────────────────────────
        private void RefreshContent()
        {
            if (contentContainer == null || context == null) return;

            contentContainer.Clear();

            switch (context.activeInspectorMode)
            {
                case GRInspectorMode.SceneProps:
                    titleLabel.text = "Scene Settings";
                    var scenePanel = new SceneSettingsPanel(context, controller);
                    contentContainer.Add(scenePanel);
                    break;

                case GRInspectorMode.ActorProps:
                    string actorName = context.SelectedActor?.ActorName ?? "Actor";
                    titleLabel.text = $"Properties — {actorName}";
                    var detailsPanel = new ActorDetailsPanel(context, controller);
                    contentContainer.Add(detailsPanel);
                    break;

                case GRInspectorMode.ActorRules:
                    string ruleName = context.SelectedActor?.ActorName ?? "Actor";
                    titleLabel.text = $"Rules — {ruleName}";
                    var scriptPanel = new ScriptEditorPanel(context, controller);
                    contentContainer.Add(scriptPanel);
                    break;

                case GRInspectorMode.None:
                default:
                    titleLabel.text = "Inspector";
                    DrawEmptyState();
                    break;
            }
        }

        private void DrawEmptyState()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;

            var label = new Label("Nothing selected");
            label.style.color = new Color(0.42f, 0.44f, 0.50f); // textMuted
            label.style.fontSize = 12;
            container.Add(label);

            var hint = new Label("Use the hierarchy or context menu\nto open an inspector");
            hint.style.color = new Color(0.42f, 0.44f, 0.50f);
            hint.style.fontSize = 10;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 6;
            hint.style.whiteSpace = WhiteSpace.Normal;
            container.Add(hint);

            contentContainer.Add(container);
        }
    }
}
