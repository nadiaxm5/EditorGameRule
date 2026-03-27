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
        private bool suppressActorNameCallback;

        private ObjectField prefabPicker;
        private DropdownField tagField;
        private Toggle activeToggle;
        private bool suppressActiveToggleCallback;

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


            // Active (Toggle)
            activeToggle = new Toggle();
            activeToggle.tooltip = "Active";
            activeToggle.style.marginRight = 5;
            activeToggle.RegisterValueChangedCallback(OnActiveToggleValueChanged);
            topRow.Add(activeToggle);

            

            // Actor Name (No Label)
            actorNameField = new TextField();
            actorNameField.style.flexGrow = 1;
            actorNameField.style.marginRight = 10;
            actorNameField.RegisterValueChangedCallback(OnActorNameFieldValueChanged);
            actorNameField.RegisterCallback<FocusOutEvent>(OnActorNameFieldFocusOut);
            topRow.Add(actorNameField);

            // Tag container for dropdown and Add logic
            var tagContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            
            tagContainer.Add(new Label("Tag") { style = { width = 20, unityTextAlign = TextAnchor.MiddleCenter } });
            tagField = new DropdownField();
            tagField.style.width = 120;
            tagContainer.Add(tagField);
            
            topRow.Add(tagContainer);

            scrollView.Add(topRow);

            // ─── Prefab Selection ───
            var prefabRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 15, paddingLeft = 4 } };
            prefabRow.Add(new Label("Prefab") { style = { width = 40, unityTextAlign = TextAnchor.MiddleCenter } });
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
            
            // Add Transform icon
            var transformIcon = new Image { image = EditorGUIUtility.IconContent("Transform Icon").image, style = { width = 16, height = 16, marginRight = 4 } };
            transformSection.Insert(0, transformIcon);

            var headerLabel = transformSection.Q<Label>();

            // 2. Insertamos el icono en el contenedor padre del Label, justo en la misma posición 
            // que ocupa el texto actualmente. Esto desplazará el texto hacia la derecha.
            if (headerLabel != null)
            {
                headerLabel.parent.Insert(headerLabel.parent.IndexOf(headerLabel), transformIcon);
            }

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
/*
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
*/
            row.Add(field);
         //   row.Add(btn);
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

            suppressActorNameCallback = true;
            actorNameField.SetValueWithoutNotify(actor.ActorName ?? "Unnamed");
            suppressActorNameCallback = false;
            suppressActiveToggleCallback = true;
            activeToggle.SetValueWithoutNotify(actor.Active);
            suppressActiveToggleCallback = false;

            // Refresh TagField directly
            tagField.UnregisterValueChangedCallback(OnTagFieldValueChanged);
            var choicesList = new List<string>(UnityEditorInternal.InternalEditorUtility.tags);
            choicesList.Add("Add Custom Tag...");
            tagField.choices = choicesList;
            tagField.SetValueWithoutNotify(actor.Tag ?? "Untagged");
            tagField.RegisterValueChangedCallback(OnTagFieldValueChanged);

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

        private void OnActiveToggleValueChanged(ChangeEvent<bool> evt)
        {
            if (suppressActiveToggleCallback || context.selectedActorIndex < 0 || context.SelectedActor == null)
                return;

            controller.UpdateActorProperty(
                context.selectedActorIndex,
                () => context.SelectedActor.Active = evt.newValue,
                "Toggle Actor Active");
        }

        private void OnActorNameFieldValueChanged(ChangeEvent<string> evt)
        {
            if (suppressActorNameCallback || context.selectedActorIndex < 0 || context.SelectedActor == null)
                return;

            string requestedName = (evt.newValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(requestedName))
            {
                // Allow temporary empty text while editing; normalize on focus out.
                return;
            }

            string oldName = context.SelectedActor.ActorName ?? "Unnamed";
            string uniqueName = GetUniqueActorName(requestedName, context.selectedActorIndex);
            if (uniqueName == oldName)
                return;

            controller.UpdateActorProperty(
                context.selectedActorIndex,
                () =>
                {
                    context.SelectedActor.ActorName = uniqueName;
                    RenameSceneObject(oldName, uniqueName);
                },
                "Rename Actor");

            context.NotifyActorListChanged();

            if (uniqueName != requestedName)
            {
                suppressActorNameCallback = true;
                actorNameField.SetValueWithoutNotify(uniqueName);
                suppressActorNameCallback = false;
            }
        }

        private void OnActorNameFieldFocusOut(FocusOutEvent evt)
        {
            if (context.selectedActorIndex < 0 || context.SelectedActor == null)
                return;

            string currentText = (actorNameField.value ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(currentText))
                return;

            suppressActorNameCallback = true;
            actorNameField.SetValueWithoutNotify(context.SelectedActor.ActorName ?? "Unnamed");
            suppressActorNameCallback = false;
        }

        private string GetUniqueActorName(string baseName, int currentActorIndex)
        {
            string candidate = baseName;
            int suffix = 1;

            while (context.currentProject.actors.Exists(a => a != context.currentProject.actors[currentActorIndex] && a.ActorName == candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }

            return candidate;
        }

        private void RenameSceneObject(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || oldName == newName)
                return;

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go == null || go.name != oldName)
                    continue;

                if (!go.scene.IsValid() || EditorUtility.IsPersistent(go))
                    continue;

                go.name = newName;
                return;
            }
        }

        private void OnTagFieldValueChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue == "Add Custom Tag...")
            {
                var tagContainer = tagField.parent;
                var textField = new TextField { value = "", style = { width = 120 } };
                tagContainer.Remove(tagField);
                tagContainer.Add(textField);
                
                textField.schedule.Execute(() => textField.Focus()).StartingIn(10);
                
                void FinishTagAddition() {
                    string newTag = textField.value.Trim();
                    if (!string.IsNullOrEmpty(newTag) && newTag != "Add Custom Tag...") {
                        bool tagExists = false;
                        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags) {
                            if (t == newTag) { tagExists = true; break; }
                        }
                        if (!tagExists) {
                            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                            SerializedProperty tagsProp = tagManager.FindProperty("tags");
                            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = newTag;
                            tagManager.ApplyModifiedProperties();
                        }
                        if (context.SelectedActor != null) {
                            Undo.RecordObject(context.currentProject, "Change Tag");
                            context.SelectedActor.Tag = newTag;
                            EditorUtility.SetDirty(context.currentProject);
                            controller.SyncDataToScene(context.SelectedActor);
                            context.NotifyProjectChanged();
                        }
                    }
                    
                    if (tagContainer.Contains(textField)) tagContainer.Remove(textField);
                    if (!tagContainer.Contains(tagField)) tagContainer.Add(tagField);
                    
                    UpdateUI();
                }

                textField.RegisterCallback<BlurEvent>(e => FinishTagAddition());
                textField.RegisterCallback<KeyDownEvent>(e => {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape) {
                        if (e.keyCode == KeyCode.Escape) textField.value = "";
                        FinishTagAddition();
                    }
                });
                return;
            }

            if (context.SelectedActor != null)
            {
                Undo.RecordObject(context.currentProject, "Change Tag");
                context.SelectedActor.Tag = evt.newValue;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                context.NotifyProjectChanged();
            }
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
                    }) { text = string.Empty };
                    btn.AddToClassList("button-danger");
                    btn.style.width = 22;
                    btn.style.height = 20;

                    var trashImage = new Image();
                    trashImage.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
                    trashImage.style.width = 12;
                    trashImage.style.height = 12;
                    trashImage.style.alignSelf = Align.Center;
                    trashImage.style.unityBackgroundImageTintColor = Color.white;
                    btn.Add(trashImage);

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

        private VisualElement dragSpacer;



        private void OnPropDragStart(PointerDownEvent evt, VisualElement item, int index)
        {
            if (evt.button != 0) return;
            // Only drag if clicking the handle area (left side)
            if (evt.localPosition.x > 20) return; 
            
            isDraggingProp = true;
            dragStartPosProp = evt.position;
            draggedPropItem = item;
            draggedPropIndex = index;

            dragSpacer = new VisualElement();
            dragSpacer.style.height = item.layout.height; // Copia la altura (aprox 24)
            item.parent.Insert(index, dragSpacer); // Mantiene el hueco abierto

            // Sacamos el item del layout normal y lo traemos al frente
            item.style.position = Position.Absolute;
            item.style.top = item.layout.y;   // Lo mantenemos donde estaba visualmente
            item.style.left = item.layout.x;
            item.style.width = item.layout.width; // Evita que se encoja
            item.BringToFront(); 

            item.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            

        }

        private void OnPropDragMove(PointerMoveEvent evt, VisualElement item)
        {
            if (!isDraggingProp || item != draggedPropItem) return;

            float diffY = evt.position.y - dragStartPosProp.y;
            if (UnityEngine.Mathf.Abs(diffY) > 5f)
            {
                item.transform.position = new Vector3(0f, diffY, 0f);
            }
        }

        private void OnPropDragEnd(EventBase evt, VisualElement item, int index)
        {
            if (!isDraggingProp || item != draggedPropItem) return;
            
            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null)
                item.ReleasePointer(pointerEvt.pointerId);
            
            isDraggingProp = false;
            draggedPropItem = null;
            item.transform.position = Vector3.zero;
            item.style.position = StyleKeyword.Null; // Revierte el Absolute
            item.style.top = StyleKeyword.Null;
            item.style.left = StyleKeyword.Null;
            item.style.width = StyleKeyword.Null;

            // Borramos el espaciador
            if (dragSpacer != null && dragSpacer.parent != null)
            {
                dragSpacer.parent.Remove(dragSpacer);
                dragSpacer = null;
            }
            // ----------------------------------------------------

            if (pointerEvt == null) return;
            
            float diffY = pointerEvt.position.y - dragStartPosProp.y;
            if (UnityEngine.Mathf.Abs(diffY) > 15f) 
            {
                var actor = context.SelectedActor;
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
                else
                {
                    // Si el índice no cambió, lo reinsertamos visualmente en su sitio original
                    item.parent.Insert(draggedPropIndex, item);
                }
            }
            else
            {
                // Si no se arrastró lo suficiente (>15f), lo devolvemos a su lugar
                item.parent.Insert(draggedPropIndex, item);
            }
            
            evt.StopPropagation();
        }
    }
}
