using GameRuleEditor.Controllers;
using GameRuleEditor.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Main editor window for GameRule.
    /// Handles Auto-Initialization of the Context and switches between Start Screen and Editor Interface.
    /// </summary>
    public class GameRuleEditorWindow : EditorWindow
    {
        private const string CONTEXT_FOLDER = "Assets/Editor/GameRuleEditor/Projects";
        private const string CONTEXT_PATH = CONTEXT_FOLDER + "/EditorContext.asset";

        private EditorContext editorContext;
        private ProjectController projectController;

        // UI Elements
        private VisualElement root;

        private VisualElement contentContainer;

        // Navigation
        private enum EditorTab
        { Scene, Actors, Preview }

        private EditorTab currentTab = EditorTab.Scene;
        private Button sceneTabButton;
        private Button actorsTabButton;
        private Button previewTabButton;

        // Toolbar references
        private Label projectNameLabel;

        private ToolbarButton saveButton;
        private ToolbarButton generateButton;

        [MenuItem("GameRule/Editor Window")]
        public static void ShowWindow()
        {
            GameRuleEditorWindow window = GetWindow<GameRuleEditorWindow>();
            window.titleContent = new GUIContent("GameRule Editor");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            EnsureEditorContextExists();
            InitializeControllers();

            if (editorContext != null)
            {
                editorContext.OnProjectLoaded += OnProjectLoaded;
                editorContext.OnProjectChanged += UpdateToolbarUI;
            }

            // Enable scene sync
            projectController?.Enable();
        }

        private void OnDisable()
        {
            if (editorContext != null)
            {
                editorContext.OnProjectLoaded -= OnProjectLoaded;
                editorContext.OnProjectChanged -= UpdateToolbarUI;
            }

            // Disable scene sync
            projectController?.Disable();
        }

        #region Initialization (The "Invisible" Plumbing)

        private void EnsureEditorContextExists()
        {
            editorContext = AssetDatabase.LoadAssetAtPath<EditorContext>(CONTEXT_PATH);

            if (editorContext == null)
            {
                if (!Directory.Exists(CONTEXT_FOLDER))
                {
                    Directory.CreateDirectory(CONTEXT_FOLDER);
                    AssetDatabase.Refresh();
                }

                editorContext = ScriptableObject.CreateInstance<EditorContext>();
                AssetDatabase.CreateAsset(editorContext, CONTEXT_PATH);
                AssetDatabase.SaveAssets();

                Debug.Log($"Initialized GameRule EditorContext at: {CONTEXT_PATH}");
            }
        }

        private void InitializeControllers()
        {
            if (editorContext != null)
            {
                projectController = new ProjectController(editorContext);
            }
        }

        #endregion Initialization (The "Invisible" Plumbing)

        #region Main GUI Logic

        private void CreateGUI()
        {
            root = rootVisualElement;
            root.style.flexGrow = 1;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            RefreshInterface();
        }

        /// <summary>
        /// Decides whether to show the "Start Screen" or the "Main Editor"
        /// </summary>
        private void RefreshInterface()
        {
            root.Clear();

            if (editorContext == null)
            {
                root.Add(new Label("Error: Could not initialize Editor Context."));
                return;
            }

            if (editorContext.currentProject == null)
            {
                DrawStartScreen();
            }
            else
            {
                DrawMainEditorInterface();
            }
        }

        #endregion Main GUI Logic

        #region Start Screen

        private void DrawStartScreen()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            var title = new Label("GameRule Editor");
            title.style.fontSize = 32;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            title.style.color = new Color(0.3f, 0.6f, 0.9f);
            container.Add(title);

            var subtitle = new Label("Select an action to begin");
            subtitle.style.fontSize = 14;
            subtitle.style.marginBottom = 30;
            subtitle.style.color = Color.gray;
            container.Add(subtitle);

            var buttonsBox = new VisualElement();
            buttonsBox.style.flexDirection = FlexDirection.Row;

            var btnNew = new Button(OnNewProject);
            btnNew.text = "Create New Project";
            btnNew.style.width = 150;
            btnNew.style.height = 40;
            btnNew.style.fontSize = 12;
            btnNew.AddToClassList("button-primary");
            btnNew.style.marginRight = 10;
            buttonsBox.Add(btnNew);

            var btnOpen = new Button(OnOpenProject);
            btnOpen.text = "Open Project";
            btnOpen.style.width = 150;
            btnOpen.style.height = 40;
            btnOpen.style.fontSize = 12;
            buttonsBox.Add(btnOpen);

            container.Add(buttonsBox);
            root.Add(container);
        }

        #endregion Start Screen

        #region Main Editor Interface

        private void DrawMainEditorInterface()
        {
            CreateToolbar(root);
            CreateTabBar(root);

            contentContainer = new VisualElement();
            contentContainer.name = "content-container";
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            // Default to Scene tab
            ShowTab(EditorTab.Scene);
            UpdateToolbarUI();
        }

        private void CreateToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 30;

            var projectMenu = new ToolbarMenu();
            projectMenu.text = "Project";
            projectMenu.menu.AppendAction("Close Project", a =>
            {
                editorContext.currentProject = null;
                RefreshInterface();
            });
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Import JSON", a => OnImportJson());
            projectMenu.menu.AppendAction("Export to JSON", a => OnExportJson());
            toolbar.Add(projectMenu);

            toolbar.Add(new ToolbarSpacer());

            projectNameLabel = new Label("No Project");
            projectNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            projectNameLabel.style.paddingLeft = 10;
            projectNameLabel.style.paddingRight = 10;
            toolbar.Add(projectNameLabel);

            var flexSpace = new VisualElement();
            flexSpace.style.flexGrow = 1;
            toolbar.Add(flexSpace);

            saveButton = new ToolbarButton(OnSaveProject) { text = "Save" };
            toolbar.Add(saveButton);

            generateButton = new ToolbarButton(OnGenerateScene) { text = "Generate Scene" };
            toolbar.Add(generateButton);

            root.Add(toolbar);
        }

        private void CreateTabBar(VisualElement root)
        {
            var tabBar = new VisualElement();
            tabBar.name = "tab-bar";
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.height = 35;
            tabBar.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

            sceneTabButton = CreateTabButton("Scene", EditorTab.Scene);
            tabBar.Add(sceneTabButton);

            actorsTabButton = CreateTabButton("Actors", EditorTab.Actors);
            tabBar.Add(actorsTabButton);

            previewTabButton = CreateTabButton("Preview", EditorTab.Preview);
            tabBar.Add(previewTabButton);

            root.Add(tabBar);
        }

        private Button CreateTabButton(string label, EditorTab tab)
        {
            var button = new Button(() => ShowTab(tab));
            button.text = label;
            button.AddToClassList("tab-button");
            button.style.height = 35;
            button.style.minWidth = 100;
            button.style.borderTopWidth = 0;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 2;
            button.style.borderTopColor = Color.clear;
            button.style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f);
            button.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            button.style.borderBottomColor = Color.clear;
            button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            return button;
        }

        #endregion Main Editor Interface

        #region Actions (New, Open, Save)

        private void OnNewProject()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New GameRule Project",
                "NewProject",
                "asset",
                "Choose where to save the new project"
            );

            if (string.IsNullOrEmpty(path)) return;

            string projectName = Path.GetFileNameWithoutExtension(path);

            projectController.CreateNewProject(projectName);

            AssetDatabase.CreateAsset(editorContext.currentProject, path);
            AssetDatabase.SaveAssets();

            RefreshInterface();
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
                projectController.LoadProject(project);
                RefreshInterface(); // Switch to Main Editor
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a GameRule Project.", "OK");
            }
        }

        private void OnImportJson()
        {
            string jsonPath = EditorUtility.OpenFilePanel("Import JSON", Application.dataPath + "/Resources/Games", "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            string savePath = EditorUtility.SaveFilePanelInProject("Save Imported Project", "ImportedProject", "asset", "Choose location");
            if (string.IsNullOrEmpty(savePath)) return;

            projectController.ImportJsonAsProject(jsonPath, savePath);
            RefreshInterface();
        }

        private void OnExportJson()
        {
            string path = EditorUtility.SaveFilePanel("Export to JSON", Application.dataPath + "/Resources/Games", editorContext.currentProject.projectName + ".json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                projectController.SaveProjectToJson(path);
                EditorUtility.DisplayDialog("Success", "Project exported successfully", "OK");
            }
        }

        private void OnSaveProject()
        {
            if (editorContext?.currentProject != null)
            {
                EditorUtility.SetDirty(editorContext.currentProject);
                AssetDatabase.SaveAssets();
                Debug.Log("Project saved");
            }
        }

        private void OnGenerateScene()
        {
            if (editorContext?.currentProject == null) return;

            var errors = projectController.ValidateProject();
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Validation Errors", string.Join("\n", errors), "OK");
                return;
            }

            // Export temp JSON
            string tempPath = Application.dataPath + "/Resources/Games/_temp_editor_export.json";
            projectController.SaveProjectToJson(tempPath);

            // Load Scene
            Loader.LoadJson("_temp_editor_export.json");
            EditorUtility.DisplayDialog("Success", "Scene generated successfully!", "OK");
        }

        #endregion Actions (New, Open, Save)

        #region Tab Management

        private void ShowTab(EditorTab tab)
        {
            currentTab = tab;
            UpdateTabButtonStyles();
            contentContainer.Clear();

            switch (tab)
            {
                case EditorTab.Scene:
                    contentContainer.Add(new GameRuleEditor.Panels.SceneSettingsPanel(editorContext, projectController));
                    break;

                case EditorTab.Actors:
                    ShowActorsPanel();
                    break;

                case EditorTab.Preview:
                    ShowPreviewPanel();
                    break;
            }
        }

        private void UpdateTabButtonStyles()
        {
            // Reset
            sceneTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            sceneTabButton.style.borderBottomColor = Color.clear;
            actorsTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            actorsTabButton.style.borderBottomColor = Color.clear;
            previewTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            previewTabButton.style.borderBottomColor = Color.clear;

            // Highlight Active
            Button active = null;
            switch (currentTab)
            {
                case EditorTab.Scene: active = sceneTabButton; break;
                case EditorTab.Actors: active = actorsTabButton; break;
                case EditorTab.Preview: active = previewTabButton; break;
            }
            if (active != null)
            {
                active.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
                active.style.borderBottomColor = new Color(0.3f, 0.5f, 0.8f);
            }
        }

        // Panel Constructors

        private void ShowActorsPanel()
        {
            var splitView = new VisualElement();
            splitView.style.flexDirection = FlexDirection.Row;
            splitView.style.flexGrow = 1;

            var actorListPanel = new GameRuleEditor.Panels.ActorListPanel(editorContext, projectController);
            splitView.Add(actorListPanel);

            var rightSide = new VisualElement();
            rightSide.style.flexGrow = 1;
            rightSide.style.flexDirection = FlexDirection.Column;

            // Sub-tabs
            var subTabBar = new VisualElement();
            subTabBar.style.flexDirection = FlexDirection.Row;
            subTabBar.style.height = 30;
            subTabBar.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            subTabBar.style.borderBottomWidth = 1;
            subTabBar.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);

            var detailsTab = new Button() { text = "Properties", style = { flexGrow = 1 } };
            var scriptTab = new Button() { text = "Script Rules", style = { flexGrow = 1 } };
            subTabBar.Add(detailsTab);
            subTabBar.Add(scriptTab);
            rightSide.Add(subTabBar);

            var rightContent = new VisualElement();
            rightContent.style.flexGrow = 1;

            var detailsPanel = new GameRuleEditor.Panels.ActorDetailsPanel(editorContext, projectController);
            var scriptPanel = new GameRuleEditor.Panels.ScriptEditorPanel(editorContext, projectController);

            detailsPanel.style.display = DisplayStyle.Flex;
            scriptPanel.style.display = DisplayStyle.None;

            detailsTab.clicked += () =>
            {
                detailsPanel.style.display = DisplayStyle.Flex;
                scriptPanel.style.display = DisplayStyle.None;
                detailsTab.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
                scriptTab.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            };

            scriptTab.clicked += () =>
            {
                detailsPanel.style.display = DisplayStyle.None;
                scriptPanel.style.display = DisplayStyle.Flex;
                detailsTab.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
                scriptTab.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            };

            // Init styles
            detailsTab.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            scriptTab.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);

            rightContent.Add(detailsPanel);
            rightContent.Add(scriptPanel);
            rightSide.Add(rightContent);

            splitView.Add(rightSide);
            contentContainer.Add(splitView);
        }

        private void ShowPreviewPanel()
        {
            var panel = new ScrollView();
            panel.style.flexGrow = 1;
            var label = new Label("JSON PREVIEW")
            {
                style = {
                    fontSize = 20, unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 20, color = Color.gray
                }
            };
            panel.Add(label);

            if (editorContext?.currentProject != null)
            {
                string json = editorContext.currentProject.ExportToJson();
                var jsonField = new TextField();
                jsonField.multiline = true;
                jsonField.value = json;
                jsonField.isReadOnly = true;
                jsonField.style.flexGrow = 1;
                jsonField.style.minHeight = 400;
                jsonField.style.marginLeft = 20;
                jsonField.style.marginRight = 20;
                jsonField.style.marginTop = 20;
                panel.Add(jsonField);
            }
            contentContainer.Add(panel);
        }

        #endregion Tab Management

        private void OnProjectLoaded() => RefreshInterface();

        private void UpdateToolbarUI()
        {
            if (projectNameLabel != null && editorContext?.currentProject != null)
                projectNameLabel.text = editorContext.currentProject.projectName;

            if (saveButton != null) saveButton.SetEnabled(editorContext?.currentProject != null);
            if (generateButton != null) generateButton.SetEnabled(editorContext?.currentProject != null);
        }
    }
}