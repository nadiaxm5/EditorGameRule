using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using System.IO;

namespace GameRuleEditor.Windows
{
    public class GameRuleToolbarWindow : EditorWindow
    {
        private EditorContext context;
        private ProjectController controller;
        private Label projectNameLabel;
        private ToolbarButton playBtn;

        public void Init(EditorContext editorContext, ProjectController projectController)
        {
            UnsubscribeEvents();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            context = editorContext;
            controller = projectController;
            SubscribeEvents();
            BuildUI();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                context = AssetDatabase.LoadAssetAtPath<EditorContext>(GameRuleLayoutManager.ContextPath);
            }
            if (context != null && controller == null)
            {
                controller = GameRuleLayoutManager.GetOrCreateController(context);
            }
            if (context != null)
            {
                SubscribeEvents();
                BuildUI();
            }
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void SubscribeEvents()
        {
            if (context == null) return;
            context.OnProjectLoaded += Rebuild;
            context.OnProjectChanged += Rebuild;
        }

        private void UnsubscribeEvents()
        {
            if (context == null) return;
            context.OnProjectLoaded -= Rebuild;
            context.OnProjectChanged -= Rebuild;
        }

        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            UpdatePlayButtonUI();
        }

        private void UpdatePlayButtonUI()
        {
            if (playBtn != null)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying)
                {
                    playBtn.text = "■"; // ■ Stop
                    playBtn.tooltip = "Stop Playing";
                }
                else
                {
                    playBtn.text = "▶"; // ▶ Play
                    playBtn.tooltip = "Generate Scene and Play";
                }
            }
        }

        // ...existing code...

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/GameRuleEditor/UI/USS/Common.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            root.style.flexGrow = 1;

            var toolbar = new Toolbar();
            toolbar.style.height = 32;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f); // #333333
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.102f, 0.102f, 0.102f); // #1a1a1a
            toolbar.style.flexShrink = 0;

            // Grupo izquierda (se queda igual)
            var leftGroup = new VisualElement();
            leftGroup.style.flexDirection = FlexDirection.Row;
            leftGroup.style.alignItems = Align.Center;

            var projectMenu = new ToolbarMenu();
            projectMenu.text = "Project";
            projectMenu.menu.AppendAction("New Project", a => OnNewProject());
            projectMenu.menu.AppendAction("Open Project", a => OnOpenProject());
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Import JSON", a => OnImportJson());
            projectMenu.menu.AppendAction("Export to JSON", a => OnExportJson());
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("Close Project", a => OnCloseProject());
            leftGroup.Add(projectMenu);

            // Grupo centro (antes estaba a la derecha)
            var middleGroup = new VisualElement();
            middleGroup.style.flexDirection = FlexDirection.Row;
            middleGroup.style.alignItems = Align.Center;
            middleGroup.style.justifyContent = Justify.Center;
            middleGroup.style.flexGrow = 1;

            var rightSpacer = new VisualElement();
            rightSpacer.style.flexShrink = 0;
            rightSpacer.style.width = 0;

            projectNameLabel = new Label(context?.currentProject?.projectName ?? "No Project");
            projectNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            projectNameLabel.style.overflow = Overflow.Hidden;
            projectNameLabel.style.textOverflow = TextOverflow.Ellipsis;
            projectNameLabel.style.fontSize = 11;
            projectNameLabel.style.color = new Color(0.61f, 0.64f, 0.69f); // #9ca3af
            projectNameLabel.style.maxWidth = 220;
            projectNameLabel.style.marginRight = 8;
            leftGroup.Add(projectNameLabel);

            var undoBtn = new ToolbarButton(() => Undo.PerformUndo()) { text = "\u21A9" };
            undoBtn.tooltip = "Undo (Ctrl+Z)";
            undoBtn.style.fontSize = 14;
            undoBtn.style.width = 35;
            middleGroup.Add(undoBtn);

            var redoBtn = new ToolbarButton(() => Undo.PerformRedo()) { text = "\u21AA" };
            redoBtn.tooltip = "Redo (Ctrl+Shift+Z)";
            redoBtn.style.fontSize = 14;
            redoBtn.style.width = 35;
            middleGroup.Add(redoBtn);

            playBtn = new ToolbarButton(OnPlayButton);
            playBtn.style.fontSize = 20;
            playBtn.style.width = 35;
            playBtn.style.marginLeft = 6;
            UpdatePlayButtonUI();
            middleGroup.Add(playBtn);

            var sceneSettingsBtn = new ToolbarButton(() =>
            {
                if (context != null)
                {
                    GameRuleSceneWindow.EnsureVisible(context, controller);
                }
            })
            { text = "\u2699" };
            sceneSettingsBtn.tooltip = "Scene Settings";
            sceneSettingsBtn.style.fontSize = 14;
            sceneSettingsBtn.style.width = 35;
            sceneSettingsBtn.style.marginLeft = 6;
            middleGroup.Add(sceneSettingsBtn);

            var saveBtn = new ToolbarButton(OnSaveProject) { text = "Save" };
            saveBtn.style.fontSize = 11;
            saveBtn.style.marginLeft = 6;
            middleGroup.Add(saveBtn);

            toolbar.Add(leftGroup);
            toolbar.Add(middleGroup);
            toolbar.Add(rightSpacer);

            leftGroup.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                rightSpacer.style.width = leftGroup.resolvedStyle.width;
            });

            root.Add(toolbar);
        }

// ...existing code...

        private void Rebuild()
        {
            if (projectNameLabel != null)
            {
                projectNameLabel.text = context?.currentProject?.projectName ?? "No Project";
            }
        }

        private void OnNewProject()
        {
            string newPath = EditorUtility.SaveFilePanelInProject("Create New GameRule Project", "NewProject", "asset", "Choose where to save the new project");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (controller == null)
                    controller = GameRuleLayoutManager.GetOrCreateController(context);
                
                string projectName = Path.GetFileNameWithoutExtension(newPath);
                controller.CreateNewProject(projectName);
                AssetDatabase.CreateAsset(context.currentProject, newPath);
                AssetDatabase.SaveAssets();
            }
        }

        private void OnOpenProject()
        {
            string path = EditorUtility.OpenFilePanel("Open Project", Application.dataPath, "asset");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                var project = AssetDatabase.LoadAssetAtPath<GameRuleProject>(path);
                if (project != null)
                {
                    if (controller == null)
                        controller = GameRuleLayoutManager.GetOrCreateController(context);
                    controller.LoadProject(project);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Selected file is not a GameRule Project.", "OK");
                }
            }
        }

        private void OnImportJson()
        {
            if (controller == null)
                controller = GameRuleLayoutManager.GetOrCreateController(context);

            string jsonPath = EditorUtility.OpenFilePanel("Import JSON", Application.dataPath + "/Resources/Games", "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            controller.ImportJsonAsProject(jsonPath);
            Rebuild();
        }

        private void OnExportJson()
        {
            if (context?.currentProject == null) return;
            string path = EditorUtility.SaveFilePanel("Export to JSON", Application.dataPath + "/Resources/Games", context.currentProject.projectName + ".json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                if (controller == null)
                    controller = GameRuleLayoutManager.GetOrCreateController(context);
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

                string gamesFolder = Path.Combine(Application.dataPath, "Resources/Games");
                if (!Directory.Exists(gamesFolder))
                    Directory.CreateDirectory(gamesFolder);

                string jsonPath = Path.Combine(gamesFolder, context.currentProject.projectName + ".json");
                if (controller == null)
                    controller = GameRuleLayoutManager.GetOrCreateController(context);

                controller.SaveProjectToJson(jsonPath);
                AssetDatabase.Refresh();
                Debug.Log($"Project saved and JSON exported: {jsonPath}");
            }
        }

                private void OnPlayButton()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            else
            {
                OnGenerateScene();
            }
        }

        private void OnGenerateScene()
        {
            if (context?.currentProject == null || controller == null) return;
            
            // Set flag so that after scripts compile, we auto-play
            EditorPrefs.SetBool("GameRule_AutoPlayAfterGenerate", true);
            controller.GenerateScene();
        }
    }
}