using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    public class ActorDetailsPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        private VisualElement mainContainer;
        private VisualElement noSelectionContainer;

        private TextField actorNameField;

        private ObjectField prefabPicker;
        private UnityEditor.UIElements.TagField tagField;
        private Toggle activeToggle;

        private VisualElement propertiesContainer;

        // Helper to manage Override UI state
        private class PropertyRow
        {
            public VisualElement container;
            public VisualElement field;
            public Label label;
        }

        private Dictionary<string, PropertyRow> rows = new Dictionary<string, PropertyRow>();

        public ActorDetailsPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            context.OnActorSelected += OnActorSelected;
            context.OnProjectChanged += UpdateUI;

            // Cleanup when removed from visual tree
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                context.OnActorSelected -= OnActorSelected;
                context.OnProjectChanged -= UpdateUI;
            });
        }

        private void OnActorSelected(int _) => UpdateUI();

        
        private void CreateUI()
        {
            // No Selection View
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;
            noSelectionContainer.Add(new Label("Select an actor to edit") { style = { color = Color.gray } });
            Add(noSelectionContainer);

            // Content View
            mainContainer = new VisualElement();
            mainContainer.style.flexGrow = 1;
            mainContainer.style.display = DisplayStyle.None;
            Add(mainContainer);

            var scrollView = new ScrollView();
            mainContainer.Add(scrollView);

            var header = new Label("Actor Properties");
            header.AddToClassList("panel-header");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 15;
            scrollView.Add(header);

            // ─── Top Row ───
            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 10, paddingLeft = 4 } };

            // Active (Toggle)
            activeToggle = new Toggle();
            activeToggle.tooltip = "Active";
            activeToggle.style.marginRight = 5;
            topRow.Add(activeToggle);

            // Prefab Icon color picker
            var iconContainer = new VisualElement { style = { width = 24, height = 24, marginRight = 5 } };
            var iconImage = new Image { image = EditorGUIUtility.IconContent("Prefab Icon").image, style = { width = 20, height = 20, alignSelf = Align.Center, marginTop = 2 } };
            iconImage.name = "PrefabColorImage";
            iconContainer.Add(iconImage);

            var iconColorField = new ColorField { style = { position = Position.Absolute, top = 0, left = 0, right = 0, bottom = 0, opacity = 0.01f } };
            iconColorField.showAlpha = false;
            iconColorField.showEyeDropper = false;
            iconColorField.RegisterValueChangedCallback(evt => {
                if (context.SelectedActor == null) return;
                string hex = "#" + ColorUtility.ToHtmlStringRGB(evt.newValue);
                context.SelectedActor.IconColorHex = hex;
                iconImage.tintColor = evt.newValue;
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged(); 
                controller.SyncDataToScene(context.SelectedActor);
            });
            iconContainer.Add(iconColorField);
            topRow.Add(iconContainer);

            // Actor Name (No Label)
            actorNameField = new TextField();
            actorNameField.style.flexGrow = 1;
            actorNameField.style.marginRight = 10;
            topRow.Add(actorNameField);

            // Tag Dropdown (TagField provides native tag drop down including 'Add Tag...')
            tagField = new TagField("");
            tagField.style.width = 120;
            topRow.Add(tagField);

            scrollView.Add(topRow);

            // ─── Prefab Selection ───
            var prefabRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 15, paddingLeft = 4 } };
            prefabRow.Add(new Label("Prefab:") { style = { width = 80, unityTextAlign = TextAnchor.MiddleLeft } });
            prefabPicker = new ObjectField { objectType = typeof(GameObject), allowSceneObjects = false, style = { flexGrow = 1 } };
            prefabPicker.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                Undo.RecordObject(context.currentProject, "Change Prefab");
                context.SelectedActor.PrefabName = evt.newValue == null ? "Empty" : evt.newValue.name;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                context.NotifyProjectChanged();
            });
            prefabRow.Add(prefabPicker);
            scrollView.Add(prefabRow);

            // ─── Transform (Collapsible) ───
            var transformSection = new Foldout { text = "Transform", value = true };
            transformSection.style.unityFontStyleAndWeight = FontStyle.Bold;
            transformSection.style.marginBottom = 10;
            scrollView.Add(transformSection);

            var transformContainer = new VisualElement { style = { paddingLeft = 15, marginTop = 5 } };
            transformSection.Add(transformContainer);

            CreateOverrideableVector3(transformContainer, "Position", "Position");
            CreateOverrideableVector3(transformContainer, "Rotation", "Rotation");
            CreateOverrideableVector3(transformContainer, "Scale", "Scale");
            // Size removed as per instruction

            // Custom Properties (Collapsible)
            var propsSection = new Foldout { text = "Properties", value = true };
            propsSection.style.unityFontStyleAndWeight = FontStyle.Bold;
            propsSection.style.marginBottom = 10;
            scrollView.Add(propsSection);

            propertiesContainer = new VisualElement { style = { paddingLeft = 15, marginTop = 5 } };
            propsSection.Add(propertiesContainer);
        }

        #region UI Construction Helpers

        private void CreateOverrideableVector3(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var label = new Label(labelText) { style = { minWidth = 140, unityFontStyleAndWeight = FontStyle.Normal } }; // Not bold by default
            row.Add(label);

            var field = new Vector3Field { style = { flexGrow = 1 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                Undo.RecordObject(context.currentProject, "Change " + propertyKey);
                float[] arr = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                
                var actor = context.SelectedActor;
                switch (propertyKey) {
                    case "Position": actor.Position = arr; break;
                    case "Rotation": actor.Rotation = arr; break;
                    case "Scale": actor.Scale = arr; break;
                }
                
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            });

            var btn = new Button(() =>
            {
                if (context.selectedActorIndex < 0) return;
                Undo.RecordObject(context.currentProject, "Revert " + propertyKey);
                var actor = context.SelectedActor;
                switch (propertyKey) {
                    case "Position": actor.Position = null; break;
                    case "Rotation": actor.Rotation = null; break;
                    case "Scale": actor.Scale = null; break;
                }
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                UpdateUI(); 
            }) { text = "Revert" };
            btn.style.width = 50;

            row.Add(field);
            row.Add(btn);
            parent.Add(row);

            rows[propertyKey] = new PropertyRow { container = row, field = field, label = label };
        }

        #endregion

        private void UpdateUI()
        {
            var actor = context.SelectedActor;
            if (actor == null)
            {
                noSelectionContainer.style.display = DisplayStyle.Flex;
                mainContainer.style.display = DisplayStyle.None;
                actorNameField.Unbind();
                activeToggle.Unbind();
                return;
            }

            noSelectionContainer.style.display = DisplayStyle.None;
            mainContainer.style.display = DisplayStyle.Flex;

            var so = new SerializedObject(context.currentProject);
            var actorProp = so.FindProperty("actors").GetArrayElementAtIndex(context.selectedActorIndex);

            actorNameField.BindProperty(actorProp.FindPropertyRelative("ActorName"));
            activeToggle.BindProperty(actorProp.FindPropertyRelative("Active"));

            // Refresh TagField directly
            tagField.SetValueWithoutNotify(actor.Tag ?? "Untagged");
            tagField.RegisterValueChangedCallback(evt => {
                actor.Tag = evt.newValue;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
            });

            // Set Icon color
            var iconImage = this.Q<Image>("PrefabColorImage");
            if (iconImage != null) {
                Color c = new Color(0.8f, 0.8f, 0.8f);
                if (!string.IsNullOrEmpty(actor.IconColorHex) && ColorUtility.TryParseHtmlString(actor.IconColorHex, out c)) {
                    iconImage.tintColor = c;
                } else {
                    iconImage.tintColor = new Color(0.8f, 0.8f, 0.8f);
                }
            }

            // Prefab Loading
            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + (actor.PrefabName ?? ""));
            prefabPicker.SetValueWithoutNotify(prefab);

            // Vector overrides
            UpdateVectorRow("Position", actor.Position, prefab?.transform.position ?? Vector3.zero);
            UpdateVectorRow("Rotation", actor.Rotation, prefab?.transform.eulerAngles ?? Vector3.zero);
            UpdateVectorRow("Scale", actor.Scale, prefab?.transform.localScale ?? Vector3.one);

            UpdatePropertiesList();
        }

        private void UpdateVectorRow(string key, float[] actorData, Vector3 prefabDefault)
        {
            if (!rows.ContainsKey(key)) return;
            var row = rows[key];
            var vecField = (Vector3Field)row.field;

            bool isOverridden = (actorData != null && actorData.Length >= 3);
            if (isOverridden)
            {
                vecField.SetValueWithoutNotify(new Vector3(actorData[0], actorData[1], actorData[2]));
                row.label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                vecField.SetValueWithoutNotify(prefabDefault);
                row.label.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        private void UpdatePropertiesList()
        {
            propertiesContainer.Clear();
            var actor = context.SelectedActor;
            
            if (actor != null && actor.Properties != null)
            {
                for (int i = 0; i < actor.Properties.Count; i++)
                {
                    int idx = i;
                    string p = actor.Properties[i];
                    string[] parts = p.Split('=');
                    string pName = parts[0];
                    string pVal = parts.Length > 1 ? parts[1] : "";

                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 0, marginTop = 0, alignItems = Align.Center } };
                    
                    // Drag setup
                    row.RegisterCallback<PointerDownEvent>(evt => OnPropDragStart(evt, row, idx));
                    row.RegisterCallback<PointerMoveEvent>(evt => OnPropDragMove(evt, row));
                    row.RegisterCallback<PointerUpEvent>(evt => OnPropDragEnd(evt, row, idx));
                    row.RegisterCallback<PointerCaptureOutEvent>(evt => OnPropDragEnd(evt, row, idx));

                    // Drag Handle (Icon or Label)
                    var handle = new Label("≡") { style = { fontSize = 16, width = 16, unityTextAlign = TextAnchor.MiddleCenter, color = Color.gray, cursor = new UnityEngine.UIElements.Cursor() } };
                    row.Add(handle);

                    // Name Field
                    var nameField = new TextField { value = pName, style = { flexGrow = 1, marginRight = 5 } };
                    nameField.RegisterValueChangedCallback(evt => {
                        UpdateActorProp(idx, evt.newValue, pVal); 
                        pName = evt.newValue;
                    });
                    row.Add(nameField);

                    // Value Field
                    var valField = new TextField { value = pVal, style = { width = 60, marginRight = 5 } };
                    valField.RegisterValueChangedCallback(evt => {
                        UpdateActorProp(idx, pName, evt.newValue);
                        pVal = evt.newValue;
                    });
                    row.Add(valField);

                    var btn = new Button(() => {
                        controller.RemoveActorProperty(context.selectedActorIndex, idx);
                        UpdatePropertiesList();
                    }) { text = "X" };
                    btn.AddToClassList("button-danger");
                    btn.style.width = 20;
                    row.Add(btn);

                    propertiesContainer.Add(row);
                }
            }

            // ADD PROPERTY BUTTON at the bottom
            var addPropBtn = new Button(() => {
                if (actor.Properties == null) actor.Properties = new List<string>();
                actor.Properties.Add("NewProp=0");
                EditorUtility.SetDirty(context.currentProject);
                UpdatePropertiesList();
            }) { text = "+ Add Property" };
            addPropBtn.style.marginTop = 10;
            addPropBtn.style.paddingTop = 4;
            addPropBtn.style.paddingBottom = 4;
            addPropBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            propertiesContainer.Add(addPropBtn);
        }

        private void UpdateActorProp(int index, string newName, string newVal)
        {
            if (context.SelectedActor != null && context.SelectedActor.Properties != null && index < context.SelectedActor.Properties.Count)
            {
                context.SelectedActor.Properties[index] = $"{newName}={newVal}";
                EditorUtility.SetDirty(context.currentProject);
            }
        }

        // ─── Drag and Drop Logic for Properties ───
        private bool isDraggingProp = false;
        private Vector2 dragStartPosProp;
        private VisualElement draggedPropItem;
        private int draggedPropIndex = -1;

        private void OnPropDragStart(PointerDownEvent evt, VisualElement item, int index)
        {
            if (evt.button != 0) return;
            // Only drag if clicking the handle area (left side)
            if (evt.localPosition.x > 20) return; 
            
            isDraggingProp = true;
            dragStartPosProp = evt.position;
            draggedPropItem = item;
            draggedPropIndex = index;
            item.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPropDragMove(PointerMoveEvent evt, VisualElement item)
        {
        }

        private void OnPropDragEnd(EventBase evt, VisualElement item, int index)
        {
            if (!isDraggingProp || item != draggedPropItem) return;
            
            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null)
                item.ReleasePointer(pointerEvt.pointerId);
            
            isDraggingProp = false;
            draggedPropItem = null;

            if (pointerEvt == null) return;
            
            float diffY = pointerEvt.position.y - dragStartPosProp.y;
            if (UnityEngine.Mathf.Abs(diffY) > 15f) 
            {
                var actor = context.SelectedActor;
                // row height is roughly 24
                int newIndex = draggedPropIndex + UnityEngine.Mathf.RoundToInt(diffY / 24f);
                newIndex = UnityEngine.Mathf.Clamp(newIndex, 0, actor.Properties.Count - 1);
                
                if (newIndex != draggedPropIndex)
                {
                    var propToMove = actor.Properties[draggedPropIndex];
                    actor.Properties.RemoveAt(draggedPropIndex);
                    actor.Properties.Insert(newIndex, propToMove);
                    
                    UnityEditor.EditorUtility.SetDirty(context.currentProject);
                    UpdatePropertiesList();
                }
            }
            evt.StopPropagation();
        }
    }
}
