using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameRuleEditor.Controllers;
using GameRuleEditor.Core;

namespace GameRuleEditor.Windows
{
    /// <summary>Creates and opens local projects without exposing their asset files.</summary>
    public sealed class GameRuleProjectDialog : EditorWindow
    {
        [SerializeField] private bool creating;
        [SerializeField] private string projectName = "New Project";
        [SerializeField] private string search = "";
        [SerializeField] private GameRuleProject duplicationSource;
        private ProjectController controller;

        public static void ShowNew(ProjectController controller) => ShowDialog(controller, true);
        public static void ShowOpen(ProjectController controller) => ShowDialog(controller, false);
        public static void ShowDuplicate(ProjectController controller, GameRuleProject source) => ShowDialog(controller, true, source);

        private static void ShowDialog(ProjectController controller, bool creating, GameRuleProject source = null)
        {
            string title = source != null ? "Duplicate Project" : creating ? "New Project" : "Open Project";
            var window = GetWindow<GameRuleProjectDialog>(true,
                title, true);
            window.controller = controller;
            window.creating = creating;
            window.duplicationSource = source;
            if (creating) window.projectName = source != null ? source.projectName + " Copy" : "New Project";
            window.minSize = new Vector2(360, 180);
            window.titleContent = new GUIContent(title);
            window.Build();
            window.ShowUtility();
        }

        private void OnEnable() => Build();

        private void Build()
        {
            if (controller == null)
            {
                var context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
                controller = GameRuleLayoutManager.GetOrCreateController(context);
            }
            var root = rootVisualElement;
            root.Clear();
            GameRuleTheme.Configure(root, Build);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.style.backgroundColor = GameRuleTheme.Background;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            if (controller == null) return;

            if (creating)
            {
                var name = new TextField("Project name") { value = projectName };
                root.Add(name);
                bool duplicating = duplicationSource != null;
                var help = new Label(duplicating
                    ? "The copy includes current edits, is saved locally and opens automatically."
                    : "The project is saved locally and its scene opens automatically.");
                help.style.whiteSpace = WhiteSpace.Normal;
                help.style.marginTop = 10;
                root.Add(help);
                var create = new Button(() => GameRuleTopMenuActions.Run(() =>
                {
                    bool completed = duplicating
                        ? controller.DuplicateProject(duplicationSource, projectName)
                        : controller.CreateAndGenerateProject(projectName);
                    if (completed) Close();
                })) { text = duplicating ? "Duplicate Project" : "Create Project" };
                create.AddToClassList("button-primary");
                create.style.marginTop = 12;
                create.SetEnabled(!string.IsNullOrWhiteSpace(projectName));
                name.RegisterValueChangedCallback(evt =>
                {
                    projectName = evt.newValue;
                    create.SetEnabled(!string.IsNullOrWhiteSpace(projectName));
                });
                root.Add(create);
            }
            else
            {
                var filter = new TextField("Search") { value = search };
                root.Add(filter);
                var list = new ScrollView();
                list.style.flexGrow = 1;
                list.style.marginTop = 8;
                root.Add(list);
                System.Action refresh = () =>
                {
                    list.Clear();
                    foreach (var project in controller.GetProjects())
                    {
                        if ((project.projectName ?? "").IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var entry = new Button(() => GameRuleTopMenuActions.Run(() =>
                        {
                            if (controller.OpenAndGenerateProject(project)) Close();
                        })) { text = project.projectName, tooltip = AssetDatabase.GetAssetPath(project) };
                        entry.AddToClassList("button-primary");
                        entry.style.unityTextAlign = TextAnchor.MiddleLeft;
                        entry.style.minHeight = 30;
                        entry.style.whiteSpace = WhiteSpace.Normal;
                        entry.style.flexShrink = 0;
                        list.Add(entry);
                    }
                    if (list.childCount == 0) list.Add(new Label("No projects found."));
                };
                filter.RegisterValueChangedCallback(evt => { search = evt.newValue; refresh(); });
                refresh();
            }
            var cancel = new Button(Close) { text = "Cancel" };
            cancel.style.alignSelf = Align.FlexEnd;
            cancel.style.marginTop = 8;
            root.Add(cancel);
        }
    }
}
