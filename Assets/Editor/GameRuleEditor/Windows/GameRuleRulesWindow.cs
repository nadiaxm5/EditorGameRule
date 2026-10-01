using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.Panels;
using System.Collections;
using System.Reflection;

namespace GameRuleEditor.Windows
{
    public class GameRuleRulesWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;
        private Label titleLabel;

        // [SerializeField] so an open group filter survives entering Play. Unity writes null strings
        // back as "", so both are normalized to null in OnEnable — see NormalizeGroupFilter.
        [SerializeField] private string currentGroupId;
        [SerializeField] private string currentGroupName;

        internal static bool IsRulesTabActive()
        {
            var window = Resources.FindObjectsOfTypeAll<GameRuleRulesWindow>();
            if (window.Length == 0) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var parent = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(window[0]);
            if (parent == null) return false;
            var panes = parent.GetType().GetField("m_Panes", flags)?.GetValue(parent) as IList;
            var selected = parent.GetType().GetProperty("selected", flags)?.GetValue(parent);
            if (panes == null || !(selected is int index)) return true;
            return index >= 0 && index < panes.Count && ReferenceEquals(panes[index], window[0]);
        }

        public static GameRuleRulesWindow EnsureVisible(EditorContext ctx, ProjectController ctrl, string groupId = null, string groupName = null)
        {
            var window = GetWindow<GameRuleRulesWindow>("Rules", false);
            window.minSize = new Vector2(400, 300);
            if (window.currentGroupId != groupId) ctx?.ClearRuleElementSelection();
            window.currentGroupId = groupId;
            window.currentGroupName = groupName;
            window.NormalizeGroupFilter();
            window.Init(ctx, ctrl);
            window.Show();
            window.Focus();
            return window;
        }

        public void Init(EditorContext ctx, ProjectController ctrl)
        {
            context = ctx; controller = ctrl;
            BuildUI();

            // Suscribirse para actualizar el título dinámicamente.
            // Unsubscribe first: Init runs on every EnsureVisible and OnEnable also subscribes,
            // so without this the handler would accumulate and rebuild the UI several times.
            context.OnActorSelected -= OnActorSelected;
            context.OnActorSelected += OnActorSelected;
        }

        /// <summary>
        /// A domain reload turns a null filter into "". Left as-is, the empty id would be taken for a
        /// real group and no rule would match it, so the window looks empty until the actor is reselected.
        /// </summary>
        private void NormalizeGroupFilter()
        {
            if (string.IsNullOrEmpty(currentGroupId)) currentGroupId = null;
            if (string.IsNullOrEmpty(currentGroupName)) currentGroupName = null;
        }

        private void OnEnable()
        {
            NormalizeGroupFilter();

            if (context == null) context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            if (context != null && controller == null) controller = GameRuleLayoutManager.GetOrCreateController(context);
            if (context != null) {
                BuildUI();
                context.OnActorSelected += OnActorSelected;
            }
        }

        private void OnDisable()
        {
            if (context != null) context.OnActorSelected -= OnActorSelected;
        }

        private void OnBecameVisible()
        {
            context?.SetRulesTabVisible(true);
        }

        private void OnBecameInvisible()
        {
            context?.SetRulesTabVisible(false);
        }

        private void OnActorSelected(int index)
        {
            // Clear group filter when the user switches actors
            currentGroupId = null;
            currentGroupName = null;
            BuildUI();
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

            // Header
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.height = 32;
            headerContainer.style.paddingLeft = 10;
            headerContainer.style.paddingRight = 6;
            headerContainer.style.backgroundColor = GameRuleTheme.Header; // headerBackground
            headerContainer.style.borderBottomWidth = 1;
            headerContainer.style.borderBottomColor = GameRuleTheme.Border;
            headerContainer.style.flexShrink = 0;

            string actorName = context?.SelectedActor?.ActorName ?? "Actor";
            string titleText = currentGroupName != null
                ? $"Rules [{currentGroupName}] — {actorName}"
                : $"Rules — {actorName}";
            titleLabel = new Label(titleText);
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = GameRuleTheme.Text; // textPrimary
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
                contentContainer.Add(new ScriptEditorPanel(context, controller, currentGroupId, currentGroupName));
        }
    }
}
