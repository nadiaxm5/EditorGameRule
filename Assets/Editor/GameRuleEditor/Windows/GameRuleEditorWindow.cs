using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using UnityEditor.UIElements;

namespace GameRuleEditor.Windows
{
    /// <summary>
    /// Main editor window for GameRule.
    /// Uses UI Toolkit with a tab-based interface.
    /// </summary>
    public class GameRuleEditorWindow : EditorWindow
    {
        // Reference to the editor context (loaded from assets)
        private EditorContext editorContext;

        // Controllers
        private ProjectController projectController;

        // UI Elements
        private ToolbarMenu projectMenu;
        private Label projectNameLabel;
        private Button saveButton;
        private Button generateButton;

        // Tab system
        private enum EditorTab
        {
            Scene,
            Actors,
            Preview
        }

        private EditorTab currentTab = EditorTab.Scene;
        private Button sceneTabButton;
        private Button actorsTabButton;
        private Button previewTabButton;

        private VisualElement contentContainer;

        // Current panel content
        private VisualElement currentPanel;

        [MenuItem("GameRule/Editor Window")]
        public static void ShowWindow()
        {
            GameRuleEditorWindow window = GetWindow<GameRuleEditorWindow>();
            window.titleContent = new GUIContent("GameRule Editor");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            LoadEditorContext();
            InitializeControllers();
        }

        private void CreateGUI()
        {
            // Root container
            var root = rootVisualElement;
            root.style.flexGrow = 1;

            // Load shared styles
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // Create toolbar
            CreateToolbar(root);

            // Create tab bar
            CreateTabBar(root);

            // Create content container
            contentContainer = new VisualElement();
            contentContainer.name = "content-container";
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            // Show initial tab
            ShowTab(EditorTab.Scene);

            // Subscribe to context events
            if (editorContext != null)
            {
                editorContext.OnProjectLoaded += OnProjectLoaded;
                editorContext.OnProjectChanged += OnProjectChanged;
            }

            UpdateUI();
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (editorContext != null)
            {
                editorContext.OnProjectLoaded -= OnProjectLoaded;
                editorContext.OnProjectChanged -= OnProjectChanged;
            }
        }

        #region Initialization

        private void LoadEditorContext()
        {
            const string CONTEXT_PATH = "Assets/Editor/GameRuleEditor/EditorContext.asset";
            editorContext = AssetDatabase.LoadAssetAtPath<EditorContext>(CONTEXT_PATH);

            if (editorContext == null)
            {
                Debug.LogWarning("EditorContext not found. Please create it via GameRule > Setup > Create Editor Context");
            }
        }

        private void InitializeControllers()
        {
            if (editorContext != null)
            {
                projectController = new ProjectController(editorContext);
            }
        }

        #endregion

        #region UI Creation

        private void CreateToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 30;

            // Project menu
            projectMenu = new ToolbarMenu();
            projectMenu.text = "Project";
            projectMenu.menu.AppendAction("New Project", a => OnNewProject());
            projectMenu.menu.AppendAction("Open Project", a => OnOpenProject());
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Import JSON", a => OnImportJson());
            projectMenu.menu.AppendAction("Export to JSON", a => OnExportJson());
            toolbar.Add(projectMenu);

            // Spacer
            toolbar.Add(new ToolbarSpacer());

            // Project name label
            projectNameLabel = new Label("No Project Loaded");
            projectNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            projectNameLabel.style.paddingLeft = 10;
            projectNameLabel.style.paddingRight = 10;
            toolbar.Add(projectNameLabel);

            // Flexible space
            var flexSpace = new VisualElement();
            flexSpace.style.flexGrow = 1;
            toolbar.Add(flexSpace);

            // Save button
            saveButton = new ToolbarButton(OnSaveProject);
            saveButton.text = "Save";
            saveButton.SetEnabled(false);
            toolbar.Add(saveButton);

            // Generate button
            generateButton = new ToolbarButton(OnGenerateScene);
            generateButton.text = "Generate Scene";
            generateButton.SetEnabled(false);
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

            // Scene tab
            sceneTabButton = CreateTabButton("Scene", EditorTab.Scene);
            tabBar.Add(sceneTabButton);

            // Actors tab
            actorsTabButton = CreateTabButton("Actors", EditorTab.Actors);
            tabBar.Add(actorsTabButton);

            // Preview tab
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

        #endregion

        #region Tab Management

        private void ShowTab(EditorTab tab)
        {
            currentTab = tab;

            // Update tab button styles
            UpdateTabButtonStyles();

            // Clear current content
            contentContainer.Clear();

            // Show appropriate panel
            switch (tab)
            {
                case EditorTab.Scene:
                    ShowScenePanel();
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
            // Reset all tabs
            sceneTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            sceneTabButton.style.borderBottomColor = Color.clear;
            actorsTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            actorsTabButton.style.borderBottomColor = Color.clear;
            previewTabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            previewTabButton.style.borderBottomColor = Color.clear;

            // Highlight active tab
            Button activeButton = null;
            switch (currentTab)
            {
                case EditorTab.Scene: activeButton = sceneTabButton; break;
                case EditorTab.Actors: activeButton = actorsTabButton; break;
                case EditorTab.Preview: activeButton = previewTabButton; break;
            }

            if (activeButton != null)
            {
                activeButton.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
                activeButton.style.borderBottomColor = new Color(0.3f, 0.5f, 0.8f);
            }
        }

        private void ShowScenePanel()
        {
            var panel = new ScrollView();
            panel.style.flexGrow = 1;

            var label = new Label("SCENE SETTINGS PANEL");
            label.style.fontSize = 20;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 50;
            label.style.color = Color.gray;

            panel.Add(label);

            if (editorContext?.currentProject != null)
            {
                var info = new Label($"Game: {editorContext.currentProject.sceneData.GameName}");
                info.style.unityTextAlign = TextAnchor.MiddleCenter;
                info.style.paddingTop = 20;
                panel.Add(info);
            }

            contentContainer.Add(panel);
        }

        private void ShowActorsPanel()
        {
            var panel = new ScrollView();
            panel.style.flexGrow = 1;

            var label = new Label("ACTORS PANEL");
            label.style.fontSize = 20;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 50;
            label.style.color = Color.gray;

            panel.Add(label);

            if (editorContext?.currentProject != null)
            {
                var count = new Label($"Actors: {editorContext.currentProject.actors.Count}");
                count.style.unityTextAlign = TextAnchor.MiddleCenter;
                count.style.paddingTop = 20;
                panel.Add(count);

                // Simple actor list
                foreach (var actor in editorContext.currentProject.actors)
                {
                    var actorLabel = new Label($"• {actor.ActorName} ({actor.PrefabName})");
                    actorLabel.style.paddingLeft = 50;
                    actorLabel.style.paddingTop = 5;
                    panel.Add(actorLabel);
                }
            }

            contentContainer.Add(panel);
        }

        private void ShowPreviewPanel()
        {
            var panel = new ScrollView();
            panel.style.flexGrow = 1;

            var label = new Label("JSON PREVIEW");
            label.style.fontSize = 20;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 20;
            label.style.color = Color.gray;

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

        #endregion

        #region Menu Actions

        private void OnNewProject()
        {
            string projectName = "NewGameRuleProject";

            // Simple dialog
            projectName = EditorUtility.SaveFilePanelInProject(
                "Create New Project",
                projectName,
                "asset",
                "Choose location for new project"
            );

            if (string.IsNullOrEmpty(projectName))
                return;

            projectController.CreateNewProject(System.IO.Path.GetFileNameWithoutExtension(projectName));

            // Save the project asset
            AssetDatabase.CreateAsset(editorContext.currentProject, projectName);
            AssetDatabase.SaveAssets();

            UpdateUI();
        }

        private void OnOpenProject()
        {
            string path = EditorUtility.OpenFilePanel(
                "Open GameRule Project",
                "Assets",
                "asset"
            );

            if (string.IsNullOrEmpty(path))
                return;

            // Convert absolute path to relative
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(path);
            if (project != null)
            {
                projectController.LoadProject(project);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Could not load project", "OK");
            }
        }

        private void OnImportJson()
        {
            string jsonPath = EditorUtility.OpenFilePanel(
                "Import JSON",
                Application.dataPath + "/Resources/Games",
                "json"
            );

            if (string.IsNullOrEmpty(jsonPath))
                return;

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Save Imported Project",
                "ImportedProject",
                "asset",
                "Choose where to save the project"
            );

            if (string.IsNullOrEmpty(savePath))
                return;

            projectController.ImportJsonAsProject(jsonPath, savePath);
            UpdateUI();
        }

        private void OnExportJson()
        {
            if (editorContext?.currentProject == null)
            {
                EditorUtility.DisplayDialog("Error", "No project loaded", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export to JSON",
                Application.dataPath + "/Resources/Games",
                editorContext.currentProject.projectName + ".json",
                "json"
            );

            if (string.IsNullOrEmpty(path))
                return;

            projectController.SaveProjectToJson(path);
            EditorUtility.DisplayDialog("Success", "Project exported successfully", "OK");
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
            if (editorContext?.currentProject == null)
            {
                EditorUtility.DisplayDialog("Error", "No project loaded", "OK");
                return;
            }

            // Validate first
            var errors = projectController.ValidateProject();
            if (errors.Count > 0)
            {
                string errorMsg = "Cannot generate scene. Errors found:\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Validation Errors", errorMsg, "OK");
                return;
            }

            // Export to temp JSON and load using existing Loader
            string tempPath = Application.dataPath + "/Resources/Games/_temp_editor_export.json";
            projectController.SaveProjectToJson(tempPath);

            // Use existing Loader
            Loader.LoadJson("_temp_editor_export.json");

            EditorUtility.DisplayDialog("Success", "Scene generated successfully!", "OK");
        }

        #endregion

        #region Event Handlers

        private void OnProjectLoaded()
        {
            UpdateUI();
            ShowTab(currentTab); // Refresh current tab
        }

        private void OnProjectChanged()
        {
            ShowTab(currentTab); // Refresh current tab
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            bool hasProject = editorContext?.currentProject != null;

            if (projectNameLabel != null)
            {
                projectNameLabel.text = hasProject
                    ? editorContext.currentProject.projectName
                    : "No Project Loaded";
            }

            if (saveButton != null)
            {
                saveButton.SetEnabled(hasProject);
            }

            if (generateButton != null)
            {
                generateButton.SetEnabled(hasProject);
            }
        }

        #endregion
    }
}
