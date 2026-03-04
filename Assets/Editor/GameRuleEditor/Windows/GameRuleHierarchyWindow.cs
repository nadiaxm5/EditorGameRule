using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Left panel — Actor hierarchy list with toolbar, context menus, and FAB.
    /// </summary>
    public class GameRuleHierarchyWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;

        private VisualElement actorListContainer;
        private List<VisualElement> actorItems = new List<VisualElement>();
        private Label projectNameLabel;
        private Label actorCountLabel;

        [MenuItem("GameRule/Editor")]
        public static void OpenEditor()
        {
            GameRuleLayoutManager.OpenLayout();
        }

        public void Init(EditorContext editorContext, ProjectController projectController)
        {
            UnsubscribeEvents();
            context = editorContext;
            controller = projectController;
            SubscribeEvents();
            BuildUI();
        }

        /// <summary>
        /// Called by Unity after domain reload (Play mode, recompile).
        /// Re-acquires the shared EditorContext and ProjectController.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Re-acquire context after domain reload
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
            // Auto-generate scene before entering Play mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (context?.currentProject != null && controller != null)
                {
                    // Check if the scene is already generated (same name + GameManager exists)
                    var currentScene = EditorSceneManager.GetActiveScene();
                    bool sceneAlreadyGenerated =
                        currentScene.name == context.currentProject.projectName
                        && GameObject.Find("GameManager") != null;

                    if (sceneAlreadyGenerated)
                    {
                        // Scene already matches — just play, no regeneration needed
                        return;
                    }

                    // Cancel Play mode — we need to generate + compile first
                    EditorApplication.isPlaying = false;

                    // Delay generation to next editor frame so Play mode cancellation takes effect
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            controller.GenerateScene();
                            // Flag: after scripts compile and attach, auto-enter Play mode
                            EditorPrefs.SetBool("GameRule_AutoPlayAfterGenerate", true);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"Failed to generate scene before Play: {ex.Message}");
                        }
                    };
                }
            }
            // Rebuild UI when returning to edit mode
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
            context.OnProjectLoaded += Rebuild;
            context.OnProjectChanged += RefreshList;
            context.OnActorListChanged += RefreshList;
            context.OnActorSelected += OnActorSelected;
        }

        private void UnsubscribeEvents()
        {
            if (context == null) return;
            context.OnProjectLoaded -= Rebuild;
            context.OnProjectChanged -= RefreshList;
            context.OnActorListChanged -= RefreshList;
            context.OnActorSelected -= OnActorSelected;
        }

        // ──────────────────────────────────
        //  UI BUILDING
        // ──────────────────────────────────
        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("hierarchy-root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            // No project loaded → show start dialog
            if (context == null || context.currentProject == null)
            {
                DrawNoProject(root);
                return;
            }

            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f); // #252526

            BuildToolbar(root);
            BuildContent(root);
            BuildFooter(root);
        }

        // ── Toolbar ────────────────────
        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 32;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f); // #333333
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.102f, 0.102f, 0.102f); // #1a1a1a
            toolbar.style.flexShrink = 0;

            // Project menu
            var projectMenu = new ToolbarMenu();
            projectMenu.text = "Project";
            projectMenu.menu.AppendAction("New Project", a => OnNewProject());
            projectMenu.menu.AppendAction("Open Project", a => OnOpenProject());
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Import JSON", a => OnImportJson());
            projectMenu.menu.AppendAction("Export to JSON", a => OnExportJson());
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Close Project", a => OnCloseProject());
            toolbar.Add(projectMenu);

            toolbar.Add(new ToolbarSpacer());

            // Project name
            projectNameLabel = new Label(context?.currentProject?.projectName ?? "No Project");
            projectNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            projectNameLabel.style.flexGrow = 1;
            projectNameLabel.style.overflow = Overflow.Hidden;
            projectNameLabel.style.textOverflow = TextOverflow.Ellipsis;
            projectNameLabel.style.fontSize = 11;
            projectNameLabel.style.color = new Color(0.61f, 0.64f, 0.69f); // #9ca3af
            toolbar.Add(projectNameLabel);

            // Undo / Redo
            var undoBtn = new ToolbarButton(() => Undo.PerformUndo()) { text = "\u21A9" };
            undoBtn.tooltip = "Undo (Ctrl+Z)";
            undoBtn.style.fontSize = 14;
            undoBtn.style.width = 28;
            toolbar.Add(undoBtn);

            var redoBtn = new ToolbarButton(() => Undo.PerformRedo()) { text = "\u21AA" };
            redoBtn.tooltip = "Redo (Ctrl+Shift+Z)";
            redoBtn.style.fontSize = 14;
            redoBtn.style.width = 28;
            toolbar.Add(redoBtn);

            toolbar.Add(new ToolbarSpacer());

            // Scene Settings button
            var sceneSettingsBtn = new ToolbarButton(() =>
            {
                context.ToggleScenePropsInspector();
                GameRuleInspectorWindow.EnsureVisible(context, controller);
            })
            { text = "\u2699" };
            sceneSettingsBtn.tooltip = "Scene Settings";
            sceneSettingsBtn.style.fontSize = 14;
            sceneSettingsBtn.style.width = 28;
            toolbar.Add(sceneSettingsBtn);

            // Generate Scene
            var generateBtn = new ToolbarButton(OnGenerateScene) { text = "\u25B6" }; // ▶
            generateBtn.tooltip = "Generate Scene";
            generateBtn.style.fontSize = 11;
            generateBtn.style.width = 28;
            toolbar.Add(generateBtn);

            // Save
            var saveBtn = new ToolbarButton(OnSaveProject) { text = "Save" };
            saveBtn.style.fontSize = 11;
            toolbar.Add(saveBtn);

            root.Add(toolbar);
        }

        // ── Content (actor list) ────────
        private void BuildContent(VisualElement root)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f); // panelBackground

            actorListContainer = new VisualElement();
            actorListContainer.style.paddingTop = 4;
            actorListContainer.style.paddingBottom = 4;
            actorListContainer.style.paddingLeft = 4;
            actorListContainer.style.paddingRight = 4;
            scrollView.Add(actorListContainer);

            root.Add(scrollView);

            RefreshList();
        }

        // ── Footer with FAB ────────────
        private void BuildFooter(VisualElement root)
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            footer.style.height = 48;
            footer.style.paddingLeft = 10;
            footer.style.paddingRight = 10;
            footer.style.backgroundColor = new Color(0.176f, 0.176f, 0.176f); // #2d2d2d
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.102f, 0.102f, 0.102f); // #1a1a1a
            footer.style.flexShrink = 0;

            actorCountLabel = new Label("0 actors");
            actorCountLabel.style.fontSize = 10;
            actorCountLabel.style.color = new Color(0.42f, 0.44f, 0.50f); // textMuted #6b7280
            footer.Add(actorCountLabel);

            // FAB — Create Actor
            var fab = new Button(OnAddActor);
            fab.text = "+";
            fab.tooltip = "Create new actor";
            fab.AddToClassList("fab-create");
            footer.Add(fab);

            root.Add(footer);
        }

        // ──────────────────────────────────
        //  ACTOR LIST RENDERING
        // ──────────────────────────────────
        private void RefreshList()
        {
            if (actorListContainer == null) return;

            actorListContainer.Clear();
            actorItems.Clear();

            if (context?.currentProject?.actors == null || context.currentProject.actors.Count == 0)
            {
                var empty = new Label("No actors in project");
                empty.AddToClassList("text-muted");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.marginTop = 30;
                actorListContainer.Add(empty);
                UpdateCount(0);
                return;
            }

            for (int i = 0; i < context.currentProject.actors.Count; i++)
            {
                int index = i;
                var actor = context.currentProject.actors[i];
                var item = CreateActorItem(actor, index);
                actorListContainer.Add(item);
                actorItems.Add(item);
            }

            UpdateCount(context.currentProject.actors.Count);

            if (context.selectedActorIndex >= 0 && context.selectedActorIndex < actorItems.Count)
                HighlightItem(context.selectedActorIndex);

            // Update project name label
            if (projectNameLabel != null)
                projectNameLabel.text = context.currentProject?.projectName ?? "No Project";
        }

        private VisualElement CreateActorItem(ActorJson actor, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("actor-list-item");
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 28;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 4;
            item.style.marginLeft = 2;
            item.style.marginRight = 2;
            item.style.marginTop = 1;
            item.style.marginBottom = 1;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            // Click → select actor
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left click
                    context.SelectActor(index);
            });

            // Icon placeholder (box)
            var icon = new Label("\u25A0"); // ■
            icon.style.fontSize = 14;
            icon.style.width = 20;
            icon.style.color = new Color(0.42f, 0.44f, 0.50f); // textMuted
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            item.Add(icon);

            // Actor name
            var nameLabel = new Label(actor.ActorName ?? "Unnamed");
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.color = new Color(0.898f, 0.906f, 0.922f); // textPrimary #e5e7eb
            item.Add(nameLabel);

            // 3-dot menu button (visible on hover via USS)
            var menuBtn = new Button(() => ShowActorContextMenu(index));
            menuBtn.text = "\u22EE"; // ⋮
            menuBtn.AddToClassList("actor-menu-btn");
            item.Add(menuBtn);

            return item;
        }

        private void ShowActorContextMenu(int actorIndex)
        {
            var actor = context.currentProject.actors[actorIndex];
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Properties"), false, () =>
            {
                context.OpenActorPropsInspector(actorIndex);
                GameRuleInspectorWindow.EnsureVisible(context, controller);
            });

            menu.AddItem(new GUIContent("Rules"), false, () =>
            {
                context.OpenActorRulesInspector(actorIndex);
                GameRuleInspectorWindow.EnsureVisible(context, controller);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                controller.DuplicateActor(actorIndex);
            });

            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                // Select the actor and open properties inspector to rename
                context.OpenActorPropsInspector(actorIndex);
                GameRuleInspectorWindow.EnsureVisible(context, controller);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Actor",
                    $"Are you sure you want to delete '{actor.ActorName}'?",
                    "Delete", "Cancel"))
                {
                    controller.RemoveActor(actorIndex);
                }
            });

            menu.ShowAsContext();
        }

        // ──────────────────────────────────
        //  SELECTION HIGHLIGHT
        // ──────────────────────────────────
        private void OnActorSelected(int index)
        {
            for (int i = 0; i < actorItems.Count; i++)
                actorItems[i].RemoveFromClassList("actor-list-item--selected");

            if (index >= 0 && index < actorItems.Count)
                HighlightItem(index);
        }

        private void HighlightItem(int index)
        {
            if (index >= 0 && index < actorItems.Count)
                actorItems[index].AddToClassList("actor-list-item--selected");
        }

        private void UpdateCount(int count)
        {
            if (actorCountLabel != null)
                actorCountLabel.text = $"{count} actor{(count != 1 ? "s" : "")}";
        }

        // ──────────────────────────────────
        //  PROJECT ACTIONS
        // ──────────────────────────────────
        private void OnAddActor()
        {
            if (context?.currentProject == null) return;

            string actorName = "NewActor";
            int counter = 1;
            while (context.currentProject.actors.Exists(a => a.ActorName == actorName))
            {
                actorName = $"NewActor_{counter}";
                counter++;
            }
            controller.AddActor(actorName);
        }

        private void OnNewProject()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New GameRule Project", "NewProject", "asset",
                "Choose where to save the new project");
            if (string.IsNullOrEmpty(path)) return;

            string projectName = Path.GetFileNameWithoutExtension(path);
            controller.CreateNewProject(projectName);
            AssetDatabase.CreateAsset(context.currentProject, path);
            AssetDatabase.SaveAssets();
            Rebuild();
        }

        private void OnOpenProject()
        {
            string path = EditorUtility.OpenFilePanel("Open GameRule Project", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(path);
            if (project != null)
            {
                controller.LoadProject(project);
                Rebuild();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a GameRule Project.", "OK");
            }
        }

        private void OnImportJson()
        {
            string jsonPath = EditorUtility.OpenFilePanel("Import JSON",
                Application.dataPath + "/Resources/Games", "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Save Imported Project", "ImportedProject", "asset", "Choose location");
            if (string.IsNullOrEmpty(savePath)) return;

            controller.ImportJsonAsProject(jsonPath, savePath);
            Rebuild();
        }

        private void OnExportJson()
        {
            if (context?.currentProject == null) return;
            string path = EditorUtility.SaveFilePanel("Export to JSON",
                Application.dataPath + "/Resources/Games",
                context.currentProject.projectName + ".json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                controller.SaveProjectToJson(path);
                EditorUtility.DisplayDialog("Success", "Project exported successfully", "OK");
            }
        }

        private void OnCloseProject()
        {
            if (context == null) return;
            context.currentProject = null;
            context.CloseInspector();
            Rebuild();
        }

        private void OnSaveProject()
        {
            if (context?.currentProject != null)
            {
                EditorUtility.SetDirty(context.currentProject);
                AssetDatabase.SaveAssets();
                Debug.Log("Project saved");
            }
        }

        private void OnGenerateScene()
        {
            if (context?.currentProject == null || controller == null) return;
            controller.GenerateScene();
        }

        private void Rebuild()
        {
            BuildUI();
        }

        // ──────────────────────────────────
        //  NO-PROJECT SCREEN (modal trigger)
        // ──────────────────────────────────
        private void DrawNoProject(VisualElement root)
        {
            root.style.flexGrow = 1;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.backgroundColor = new Color(0.145f, 0.145f, 0.153f);

            var title = new Label("GameRule Editor");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.816f, 0.867f, 1f); // primaryFixed #D0BCFF close
            title.style.marginBottom = 8;
            root.Add(title);

            var subtitle = new Label("No project loaded");
            subtitle.style.fontSize = 12;
            subtitle.style.color = new Color(0.61f, 0.64f, 0.69f);
            subtitle.style.marginBottom = 20;
            root.Add(subtitle);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var btnNew = new Button(OnNewProject) { text = "New Project" };
            btnNew.AddToClassList("button-primary");
            btnNew.style.marginRight = 6;
            btnRow.Add(btnNew);

            var btnOpen = new Button(OnOpenProject) { text = "Open Project" };
            btnRow.Add(btnOpen);

            root.Add(btnRow);
        }
    }
}
