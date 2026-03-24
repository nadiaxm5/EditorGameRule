using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            BuildHeader(root);
            BuildContent(root);
        }

        // ── Header ────────────
        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.style.height = 30;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.102f, 0.102f, 0.102f);
            header.style.justifyContent = Justify.Center;
            header.style.paddingLeft = 8;
   /*         
            var label = new Label("Cast");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12;
            label.style.color = new Color(0.898f, 0.906f, 0.922f);
            header.Add(label);
            */
            root.Add(header);
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

            

            
            var addActorBtn = new Button(OnAddActor);
            addActorBtn.text = "+ Add Actor";
            addActorBtn.style.marginTop = 10;
            addActorBtn.style.marginBottom = 10;
            addActorBtn.style.paddingTop = 6;
            addActorBtn.style.paddingBottom = 6;
            addActorBtn.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            addActorBtn.style.color = new Color(0.898f, 0.906f, 0.922f); // textPrimary
            addActorBtn.style.borderTopWidth = 1;
            addActorBtn.style.borderBottomWidth = 1;
            addActorBtn.style.borderLeftWidth = 1;
            addActorBtn.style.borderRightWidth = 1;
            addActorBtn.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            addActorBtn.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            addActorBtn.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            addActorBtn.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            actorListContainer.Add(addActorBtn);

            if (context.selectedActorIndex >= 0 && context.selectedActorIndex < actorItems.Count)
                HighlightItem(context.selectedActorIndex);

        }

        private VisualElement CreateActorItem(ActorJson actor, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("actor-list-item");
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 15;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 4;
            item.style.marginLeft = 2;
            item.style.marginRight = 2;
            item.style.marginTop = 0;
            item.style.marginBottom = 0;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            // Apply opacity based on active state
            UpdateItemOpacity(item, actor);

            // Click → select actor
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
            if (evt.button == 0) // Left click
                context.SelectActor(index);
            });
            item.RegisterCallback<PointerDownEvent>(evt => OnActorDragStart(evt, item, index));
            item.RegisterCallback<PointerMoveEvent>(evt => OnActorDragMove(evt, item));
            item.RegisterCallback<PointerUpEvent>(evt => OnActorDragEnd(evt, item, index));
            item.RegisterCallback<PointerCaptureOutEvent>(evt => OnActorDragEnd(evt, item, index));

            // Icon placeholder (box)
            var icon = new Image();
            icon.image = EditorGUIUtility.IconContent("Prefab Icon").image;
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 4;
              
            Color iconColor = new Color(0.8f, 0.8f, 0.8f);
            if (!string.IsNullOrEmpty(actor.IconColorHex) && UnityEngine.ColorUtility.TryParseHtmlString(actor.IconColorHex, out iconColor)) {
            icon.tintColor = iconColor;
            } else {
            icon.tintColor = new Color(0.8f, 0.8f, 0.8f);
            }
            item.Add(icon);
            
            var nameLabel = new Label(actor.ActorName ?? "Unnamed");
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.color = new Color(0.898f, 0.906f, 0.922f); // textPrimary #e5e7eb
            item.Add(nameLabel);

            // 3-dot menu button (permanently visible)
            var menuBtn = new Button(() => ShowActorContextMenu(index));
            menuBtn.text = "\u22EE"; // ⋮
            menuBtn.AddToClassList("actor-menu-btn");
            menuBtn.style.display = DisplayStyle.Flex;
            menuBtn.style.opacity = 1f;
            menuBtn.style.color = new Color(0.898f, 0.906f, 0.922f); // same as textPrimary
            menuBtn.style.fontSize = 16;
            menuBtn.style.backgroundColor = Color.clear;
            menuBtn.style.borderTopWidth = 0;
            menuBtn.style.borderBottomWidth = 0;
            menuBtn.style.borderLeftWidth = 0;
            menuBtn.style.borderRightWidth = 0;
            item.Add(menuBtn);

            return item;
        }

        private void UpdateItemOpacity(VisualElement item, ActorJson actor)
        {
            item.style.opacity = actor.Active ? 1f : 0.5f;
        }

        private void ShowActorContextMenu(int actorIndex)
        {
            var actor = context.currentProject.actors[actorIndex];
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Properties"), false, () =>
            {
                context.SelectActor(actorIndex);
                GameRulePropertiesWindow.EnsureVisible(context, controller);
            });

            menu.AddItem(new GUIContent("Rules"), false, () =>
            {
                context.SelectActor(actorIndex);
                GameRuleRulesWindow.EnsureVisible(context, controller);
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

        // ──────────────────────────────────
        // DRAG AND DROP REORDERING
        // ──────────────────────────────────
        
        private bool isDraggingActor = false;
        private Vector2 dragStartPos;
        private VisualElement draggedItem;
        private int draggedIndex = -1;

        private void OnActorDragStart(PointerDownEvent evt, VisualElement item, int index)
        {
            if (evt.button != 0) return; 
            isDraggingActor = true;
            dragStartPos = evt.position;
            draggedItem = item;
            draggedIndex = index;
            item.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnActorDragMove(PointerMoveEvent evt, VisualElement item)
        {            
            if (!isDraggingActor || item != draggedItem) return;

            

            float diffY = evt.position.y - dragStartPos.y;
            if (UnityEngine.Mathf.Abs(diffY) > 5f)
            {
                item.transform.position = new Vector3(0f, diffY, 0f);
            }
        }   

        private void OnActorDragEnd(EventBase evt, VisualElement item, int index)
        {
            if (!isDraggingActor || item != draggedItem) return;
            
            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null)
                item.ReleasePointer(pointerEvt.pointerId);
            
            isDraggingActor = false;
            draggedItem = null;
            // Ensure visual offset from drag preview is cleared.
            item.transform.position = Vector3.zero;

            if (pointerEvt == null) return;
            
            float diffY = pointerEvt.position.y - dragStartPos.y;
            if (UnityEngine.Mathf.Abs(diffY) > 15f) 
            {
                int newIndex = draggedIndex + UnityEngine.Mathf.RoundToInt(diffY / 28f);
                newIndex = UnityEngine.Mathf.Clamp(newIndex, 0, context.currentProject.actors.Count - 1);
                
                if (newIndex != draggedIndex)
                {
                    var actorList = context.currentProject.actors;
                    var actorToMove = actorList[draggedIndex];
                    actorList.RemoveAt(draggedIndex);
                    actorList.Insert(newIndex, actorToMove);
                    
                    UnityEditor.EditorUtility.SetDirty(context.currentProject);
                    
                    if (context.selectedActorIndex == draggedIndex)
                        context.SelectActor(newIndex);
                    else if (draggedIndex < context.selectedActorIndex && newIndex >= context.selectedActorIndex)
                        context.SelectActor(context.selectedActorIndex - 1);
                    else if (draggedIndex > context.selectedActorIndex && newIndex <= context.selectedActorIndex)
                        context.SelectActor(context.selectedActorIndex + 1);

                    RefreshList();
                }
            }
            evt.StopPropagation();
        }
        
        private void OnActorSelected(int index)
        {
            for (int i = 0; i < actorItems.Count; i++)
            actorItems[i].RemoveFromClassList("actor-list-item--selected");

            if (index >= 0 && index < actorItems.Count)
            {
            HighlightItem(index);
            
            // Select in scene if exists (including inactive objects)
            if (context?.currentProject?.actors != null && index < context.currentProject.actors.Count)
            {
                var actorName = context.currentProject.actors[index].ActorName;
                var go = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == actorName && !EditorUtility.IsPersistent(g));
                
                if (go != null)
                {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                }
            }
            }
        }

        private void HighlightItem(int index)
        {
            if (index >= 0 && index < actorItems.Count)
                actorItems[index].AddToClassList("actor-list-item--selected");
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


