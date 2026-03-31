using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.Windows;

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

        private VisualElement componentsContainer;

        private class VectorRow
        {
            public VisualElement container;
            public VisualElement field;
            public Label label;
        }
        private Dictionary<string, VectorRow> rows = new Dictionary<string, VectorRow>();

        // ── Property drag (spacer-based) ──
        private bool isDraggingProp;
        private Vector2 dragStartPosProp;
        private VisualElement draggedPropItem;
        private int draggedPropIndex = -1;
        private VisualElement propDragSpacer;

        // ── Component drag (spacer-based) ──
        private bool isDraggingComp;
        private Vector2 dragStartPosComp;
        private VisualElement draggedCompItem;
        private int draggedCompIndex = -1;
        private VisualElement compDragSpacer;

        // ── Component foldout state ──
        private HashSet<ActorComponentMeta> collapsedComponents = new HashSet<ActorComponentMeta>();
        private HashSet<ActorComponentMeta> initializedComponents = new HashSet<ActorComponentMeta>();

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

            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                context.OnActorSelected -= OnActorSelected;
                context.OnProjectChanged -= UpdateUI;
            });
        }

        private void OnActorSelected(int _) => UpdateUI();

        // ──────────────────────────────────
        //  SKELETON UI
        // ──────────────────────────────────
        private void CreateUI()
        {
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;
            noSelectionContainer.Add(new Label("Select an actor to edit") { style = { color = Color.gray } });
            Add(noSelectionContainer);

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

            var iconContainer = new VisualElement { style = { width = 24, height = 24, marginRight = 5 } };
            var iconImage = new Image { image = EditorGUIUtility.IconContent("Prefab Icon").image, style = { width = 20, height = 20, alignSelf = Align.Center, marginTop = 2 } };
            iconImage.name = "PrefabColorImage";
            iconContainer.Add(iconImage);

            var iconColorField = new ColorField { style = { position = Position.Absolute, top = 0, left = 0, right = 0, bottom = 0, opacity = 0.01f } };
            iconColorField.showAlpha = false;
            iconColorField.showEyeDropper = false;
            iconColorField.RegisterValueChangedCallback(evt =>
            {
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

            activeToggle = new Toggle();
            activeToggle.tooltip = "Active";
            activeToggle.style.marginRight = 5;
            activeToggle.RegisterValueChangedCallback(OnActiveToggleValueChanged);
            topRow.Add(activeToggle);

            actorNameField = new TextField();
            actorNameField.style.flexGrow = 1;
            actorNameField.style.marginRight = 10;
            actorNameField.RegisterValueChangedCallback(OnActorNameFieldValueChanged);
            actorNameField.RegisterCallback<FocusOutEvent>(OnActorNameFieldFocusOut);
            topRow.Add(actorNameField);

            var tagContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            tagContainer.Add(new Label("Tag") { style = { width = 20, unityTextAlign = TextAnchor.MiddleCenter } });
            tagField = new DropdownField();
            tagField.style.width = 120;
            tagContainer.Add(tagField);
            topRow.Add(tagContainer);

            scrollView.Add(topRow);

            // ─── Prefab ───
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

            // ─── Transform (always-on) ───
            var transformSection = new Foldout { text = "Transform", value = true };
            transformSection.style.unityFontStyleAndWeight = FontStyle.Bold;
            transformSection.style.marginBottom = 10;

            var transformIcon = new Image { image = EditorGUIUtility.IconContent("Transform Icon").image, style = { width = 16, height = 16, marginRight = 4 } };
            var headerLabel = transformSection.Q<Label>();
            if (headerLabel != null)
                headerLabel.parent.Insert(headerLabel.parent.IndexOf(headerLabel), transformIcon);

            scrollView.Add(transformSection);

            var transformContainer = new VisualElement { style = { paddingLeft = 15, marginTop = 5 } };
            transformSection.Add(transformContainer);

            CreateOverrideableVector3(transformContainer, "Position", "Position");
            CreateOverrideableVector3(transformContainer, "Rotation", "Rotation");
            CreateOverrideableVector3(transformContainer, "Scale", "Scale");

            // ─── Dynamic components ───
            componentsContainer = new VisualElement();
            componentsContainer.style.marginTop = 5;
            scrollView.Add(componentsContainer);

            // ─── Add Component button ───
            var addComponentBtn = new Button(() => ShowAddComponentMenu());
            addComponentBtn.text = "Add Component";
            addComponentBtn.style.marginTop = 14;
            addComponentBtn.style.marginBottom = 8;
            addComponentBtn.style.paddingTop = 5;
            addComponentBtn.style.paddingBottom = 5;
            addComponentBtn.style.alignSelf = Align.Center;
            addComponentBtn.style.width = 200;
            addComponentBtn.style.backgroundColor = new Color(0.22f, 0.22f, 0.25f);
            addComponentBtn.style.borderTopLeftRadius = 4;
            addComponentBtn.style.borderTopRightRadius = 4;
            addComponentBtn.style.borderBottomLeftRadius = 4;
            addComponentBtn.style.borderBottomRightRadius = 4;
            scrollView.Add(addComponentBtn);
        }

        // ──────────────────────────────────
        //  UPDATE
        // ──────────────────────────────────
        private void UpdateUI()
        {
            var actor = context.SelectedActor;
            if (actor == null)
            {
                noSelectionContainer.style.display = DisplayStyle.Flex;
                mainContainer.style.display = DisplayStyle.None;
                return;
            }

            noSelectionContainer.style.display = DisplayStyle.None;
            mainContainer.style.display = DisplayStyle.Flex;

            if (actor.Components == null)
                actor.Components = new List<ActorComponentMeta>();

            // Auto-migrate legacy data
            bool dirty = false;

            // 1. Ensure every Rules component has an id
            foreach (var comp in actor.Components)
            {
                if (comp.type == "Rules" && string.IsNullOrEmpty(comp.id))
                {
                    comp.id = NewGroupId();
                    dirty = true;
                }
            }

            // 2. Add default Properties component if needed
            if (actor.Properties != null && actor.Properties.Count > 0 && !HasComponentOfType(actor, "Properties"))
            {
                actor.Components.Insert(0, new ActorComponentMeta { type = "Properties", name = "Properties" });
                dirty = true;
            }

            // 3. Add default Rules component if needed, then assign groupIds to ungrouped rules
            if (actor.Script != null && actor.Script.Count > 0 && !HasComponentOfType(actor, "Rules"))
            {
                var defaultComp = new ActorComponentMeta { type = "Rules", name = "Rules", id = NewGroupId() };
                actor.Components.Add(defaultComp);
                dirty = true;
            }

            // 4. Assign groupId to rules that have none (use first Rules component)
            if (actor.Script != null)
            {
                string firstRulesId = null;
                foreach (var c in actor.Components)
                    if (c.type == "Rules") { firstRulesId = c.id; break; }

                if (firstRulesId != null)
                {
                    foreach (var rule in actor.Script)
                    {
                        if (string.IsNullOrEmpty(rule.groupId))
                        {
                            rule.groupId = firstRulesId;
                            dirty = true;
                        }
                    }
                }
            }

            if (dirty) EditorUtility.SetDirty(context.currentProject);

            suppressActorNameCallback = true;
            actorNameField.SetValueWithoutNotify(actor.ActorName ?? "Unnamed");
            suppressActorNameCallback = false;

            suppressActiveToggleCallback = true;
            activeToggle.SetValueWithoutNotify(actor.Active);
            suppressActiveToggleCallback = false;

            tagField.UnregisterValueChangedCallback(OnTagFieldValueChanged);
            var choices = new List<string>(UnityEditorInternal.InternalEditorUtility.tags);
            choices.Add("Add Custom Tag...");
            tagField.choices = choices;
            tagField.SetValueWithoutNotify(actor.Tag ?? "Untagged");
            tagField.RegisterValueChangedCallback(OnTagFieldValueChanged);

            var iconImage = this.Q<Image>("PrefabColorImage");
            if (iconImage != null)
            {
                Color c = new Color(0.8f, 0.8f, 0.8f);
                if (!string.IsNullOrEmpty(actor.IconColorHex))
                    ColorUtility.TryParseHtmlString(actor.IconColorHex, out c);
                iconImage.tintColor = c;
            }

            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + (actor.PrefabName ?? ""));
            prefabPicker.SetValueWithoutNotify(prefab);

            UpdateVectorRow("Position", actor.Position, prefab?.transform.position ?? Vector3.zero);
            UpdateVectorRow("Rotation", actor.Rotation, prefab?.transform.eulerAngles ?? Vector3.zero);
            UpdateVectorRow("Scale", actor.Scale, prefab?.transform.localScale ?? Vector3.one);

            RebuildComponents();
        }

        // ──────────────────────────────────
        //  COMPONENT RENDERING
        // ──────────────────────────────────
        private void RebuildComponents()
        {
            componentsContainer.Clear();
            var actor = context.SelectedActor;
            if (actor?.Components == null) return;

            for (int i = 0; i < actor.Components.Count; i++)
            {
                int compIndex = i;
                var comp = actor.Components[i];
                var card = comp.type == "Properties"
                    ? BuildPropertiesComponent(comp, compIndex)
                    : BuildRulesComponent(comp, compIndex);

                card.RegisterCallback<PointerMoveEvent>(evt => OnCompDragMove(evt, card));
                card.RegisterCallback<PointerUpEvent>(evt => OnCompDragEnd(evt, card));
                card.RegisterCallback<PointerCaptureOutEvent>(evt => OnCompDragEnd(evt, card));

                componentsContainer.Add(card);
            }
        }

        /// <summary>
        /// Builds the foldout shell: drag handle + name (editable if renameable) + remove button.
        /// </summary>
        private Foldout BuildComponentShell(ActorComponentMeta comp, int compIndex, System.Action onRemove, bool renameable = false)
        {
            if (!initializedComponents.Contains(comp))
            {
                initializedComponents.Add(comp);
                collapsedComponents.Add(comp);
            }
            bool isCollapsed = collapsedComponents.Contains(comp);
            var foldout = new Foldout { value = !isCollapsed };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    collapsedComponents.Remove(comp);
                else
                    collapsedComponents.Add(comp);
            });
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.marginBottom = 10;
            foldout.style.backgroundColor = new Color(0.18f, 0.18f, 0.20f);
            foldout.style.borderTopLeftRadius = 4;
            foldout.style.borderTopRightRadius = 4;
            foldout.style.borderBottomLeftRadius = 4;
            foldout.style.borderBottomRightRadius = 4;

            var toggle = foldout.Q<Toggle>();
            if (toggle == null) return foldout;

            toggle.text = "";
            var toggleInput = toggle.Q(className: Toggle.inputUssClassName);
            if (toggleInput == null) return foldout;

            // Hide default text element
            var toggleText = toggleInput.Q(className: Toggle.textUssClassName);
            if (toggleText != null)
            {
                toggleText.style.display = DisplayStyle.None;
                toggleText.style.width = 0;
                toggleText.style.minWidth = 0;
            }

            // Drag handle — initiates component reorder
            var dragHandle = new Label("\u2261");
            dragHandle.tooltip = "Drag to reorder";
            dragHandle.pickingMode = PickingMode.Position;
            dragHandle.style.width = 16;
            dragHandle.style.unityTextAlign = TextAnchor.MiddleCenter;
            dragHandle.style.color = new Color(0.65f, 0.65f, 0.65f);
            dragHandle.style.marginLeft = 2;
            dragHandle.style.marginRight = 4;
            dragHandle.RegisterCallback<PointerDownEvent>(evt =>
                OnCompDragStart(evt, foldout, compIndex));
            toggleInput.Insert(1, dragHandle);

            // Component name — editable TextField or static Label
            if (renameable)
            {
                var nameField = new TextField { value = comp.name };
                nameField.style.flexGrow = 1;
                nameField.style.marginLeft = 0;
                nameField.style.marginRight = 4;
                nameField.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameField.style.backgroundColor = Color.clear;
                nameField.style.borderTopWidth = 0;
                nameField.style.borderBottomWidth = 0;
                nameField.style.borderLeftWidth = 0;
                nameField.style.borderRightWidth = 0;
                nameField.style.unityTextAlign = TextAnchor.MiddleLeft;
                HideTextFieldLabel(nameField);

                nameField.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    // Buscamos cualquier elemento de texto interno real (TextElement)
                    var innerTexts = nameField.Query<TextElement>().ToList();
                    foreach (var txt in innerTexts)
                    {
                        // Ignoramos la etiqueta oculta (Label) y nos quedamos con el Input real
                        if (!txt.ClassListContains("unity-label"))
                        {
                            txt.style.paddingLeft = 4; // Damos aire por la izquierda
                            txt.style.overflow = Overflow.Visible; // EVITA QUE SE RECORTE LA NEGRITA
                        }
                    }
                });

                
                nameField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(context.currentProject, "Rename Component");
                    comp.name = evt.newValue;
                    EditorUtility.SetDirty(context.currentProject);
                });
                nameField.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                toggleInput.Add(nameField);
            }
            else
            {
                var nameLabel = new Label(comp.name);
                nameLabel.style.flexGrow = 1;
                nameLabel.style.marginLeft = 4;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                toggleInput.Add(nameLabel);
            }

            // Remove button
            var removeBtn = new Button(() => onRemove?.Invoke());
            removeBtn.text = "\u2715";
            removeBtn.tooltip = "Remove Component";
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 22;
            removeBtn.style.height = 20;
            removeBtn.style.marginLeft = 4;
            removeBtn.style.paddingLeft = 0;
            removeBtn.style.paddingRight = 0;
            removeBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            toggle.Add(removeBtn);

            return foldout;
        }

        private VisualElement BuildPropertiesComponent(ActorComponentMeta comp, int compIndex)
        {
            var foldout = BuildComponentShell(comp, compIndex, () => RemoveComponent(compIndex, "Properties"));
            var content = new VisualElement { style = { paddingLeft = 15, marginTop = 5 } };
            foldout.Add(content);
            RebuildPropertiesList(content);
            return foldout;
        }

        private void RebuildPropertiesList(VisualElement content)
        {
            content.Clear();
            var actor = context.SelectedActor;
            if (actor == null) return;

            if (actor.Properties != null)
            {
                for (int i = 0; i < actor.Properties.Count; i++)
                {
                    int idx = i;
                    string p = actor.Properties[i];
                    string[] parts = p.Split('=');
                    string pName = parts[0];
                    string pVal = parts.Length > 1 ? parts[1] : "";

                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2, alignItems = Align.Center } };

                    // Drag handle
                    var handle = new Label("\u2261");
                    handle.tooltip = "Drag to reorder";
                    handle.pickingMode = PickingMode.Position;
                    handle.style.fontSize = 16;
                    handle.style.width = 16;
                    handle.style.unityTextAlign = TextAnchor.MiddleCenter;
                    handle.style.color = Color.gray;
                    handle.style.marginRight = 4;
                    row.Add(handle);

                    // Name field
                    var nameField = new TextField { value = pName };
                    nameField.style.flexGrow = 1;
                    nameField.style.marginRight = 4;
                    HideTextFieldLabel(nameField);
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        UpdateActorProp(idx, evt.newValue, pVal);
                        pName = evt.newValue;
                    });
                    row.Add(nameField);

                    // Value field
                    var valField = new TextField { value = pVal };
                    valField.style.width = 60;
                    valField.style.marginRight = 4;
                    HideTextFieldLabel(valField);
                    valField.RegisterValueChangedCallback(evt =>
                    {
                        UpdateActorProp(idx, pName, evt.newValue);
                        pVal = evt.newValue;
                    });
                    row.Add(valField);

                    // Delete button
                    var delBtn = new Button(() =>
                    {
                        controller.RemoveActorProperty(context.selectedActorIndex, idx);
                        RebuildPropertiesList(content);
                    }) { text = string.Empty };
                    delBtn.AddToClassList("button-danger");
                    delBtn.style.width = 28;
                    delBtn.style.height = 26;
                    var trashImg = new Image();
                    trashImg.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
                    trashImg.style.width = 16;
                    trashImg.style.height = 16;
                    trashImg.style.alignSelf = Align.Center;
                    trashImg.style.unityBackgroundImageTintColor = Color.white;
                    delBtn.Add(trashImg);
                    row.Add(delBtn);

                    // Spacer-based drag registration
                    handle.RegisterCallback<PointerDownEvent>(evt => OnPropDragStart(evt, row, idx, content));
                    row.RegisterCallback<PointerMoveEvent>(evt => OnPropDragMove(evt, row));
                    row.RegisterCallback<PointerUpEvent>(evt => OnPropDragEnd(evt, row, content));
                    row.RegisterCallback<PointerCaptureOutEvent>(evt => OnPropDragEnd(evt, row, content));

                    content.Add(row);
                }
            }

            var addBtn = new Button(() =>
            {
                if (actor.Properties == null) actor.Properties = new List<string>();
                actor.Properties.Add("NewProp=0");
                EditorUtility.SetDirty(context.currentProject);
                RebuildPropertiesList(content);
            }) { text = "+ Add Property" };
            addBtn.style.marginTop = 10;
            addBtn.style.paddingTop = 4;
            addBtn.style.paddingBottom = 4;
            addBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            content.Add(addBtn);
        }

        private VisualElement BuildRulesComponent(ActorComponentMeta comp, int compIndex)
        {
            // Ensure the component always has a valid id
            if (string.IsNullOrEmpty(comp.id))
            {
                comp.id = NewGroupId();
                EditorUtility.SetDirty(context.currentProject);
            }

            var foldout = BuildComponentShell(comp, compIndex, () => RemoveComponent(compIndex, comp.id), renameable: true);
            var content = new VisualElement { style = { paddingLeft = 15, marginTop = 5, marginBottom = 8 } };
            foldout.Add(content);

            int ruleCount = 0;
            var actor = context.SelectedActor;
            if (actor?.Script != null)
                foreach (var r in actor.Script)
                    if (r.groupId == comp.id) ruleCount++;

            var infoLabel = new Label(ruleCount == 0
                ? "No rules defined yet."
                : $"{ruleCount} rule{(ruleCount == 1 ? "" : "s")} defined.");
            infoLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            infoLabel.style.fontSize = 11;
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);

            string capturedId = comp.id;
            var openBtn = new Button(() => GameRuleRulesWindow.EnsureVisible(context, controller, capturedId, comp.name));
            openBtn.text = "Open Rules Window";
            openBtn.AddToClassList("button-primary");
            openBtn.style.alignSelf = Align.FlexStart;
            content.Add(openBtn);

            return foldout;
        }

        // ──────────────────────────────────
        //  PROPERTY DRAG (spacer-based)
        // ──────────────────────────────────
        private void OnPropDragStart(PointerDownEvent evt, VisualElement row, int index, VisualElement content)
        {
            if (evt.button != 0) return;

            isDraggingProp = true;
            dragStartPosProp = evt.position;
            draggedPropItem = row;
            draggedPropIndex = index;

            propDragSpacer = new VisualElement();
            propDragSpacer.style.height = Mathf.Max(24f, row.layout.height);
            propDragSpacer.style.marginBottom = row.resolvedStyle.marginBottom;
            propDragSpacer.style.marginTop = row.resolvedStyle.marginTop;
            propDragSpacer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.2f);
            propDragSpacer.style.borderTopWidth = 2;
            propDragSpacer.style.borderBottomWidth = 2;
            propDragSpacer.style.borderLeftWidth = 2;
            propDragSpacer.style.borderRightWidth = 2;
            propDragSpacer.style.borderTopColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderBottomColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderLeftColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderRightColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderTopLeftRadius = 3;
            propDragSpacer.style.borderTopRightRadius = 3;
            propDragSpacer.style.borderBottomLeftRadius = 3;
            propDragSpacer.style.borderBottomRightRadius = 3;

            int insertAt = content.IndexOf(row);
            if (insertAt < 0) return;
            content.Insert(insertAt, propDragSpacer);

            row.style.position = Position.Absolute;
            row.style.top = row.layout.y;
            row.style.left = row.layout.x;
            row.style.width = row.layout.width;
            row.style.opacity = 0.8f;
            row.BringToFront();

            row.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPropDragMove(PointerMoveEvent evt, VisualElement row)
        {
            if (!isDraggingProp || row != draggedPropItem) return;

            float diffY = evt.position.y - dragStartPosProp.y;
            row.transform.position = new Vector3(0f, diffY, 0f);

            var content = propDragSpacer?.parent;
            if (content == null) return;

            float draggedCenterY = row.layout.y + diffY + row.layout.height / 2f;

            // Determine target logical index (skip row, spacer, and the Add button)
            int newTargetIndex = 0;
            foreach (var child in content.Children())
            {
                if (child == row || child == propDragSpacer || child is Button) continue;
                if (draggedCenterY < child.layout.y + child.layout.height / 2f) break;
                newTargetIndex++;
            }

            int currentSpacerIndex = 0;
            foreach (var child in content.Children())
            {
                if (child == propDragSpacer) break;
                if (child == row || child is Button) continue;
                currentSpacerIndex++;
            }

            if (newTargetIndex != currentSpacerIndex)
            {
                content.Remove(propDragSpacer);
                int physicalInsert = 0;
                int logical = 0;
                foreach (var child in content.Children())
                {
                    if (logical == newTargetIndex) break;
                    if (child != row && !(child is Button)) logical++;
                    physicalInsert++;
                }
                content.Insert(physicalInsert, propDragSpacer);
            }

            evt.StopPropagation();
        }

        private void OnPropDragEnd(EventBase evt, VisualElement row, VisualElement content)
        {
            if (!isDraggingProp || row != draggedPropItem) return;

            int originalIndex = draggedPropIndex;
            isDraggingProp = false;
            draggedPropItem = null;
            draggedPropIndex = -1;

            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null) row.ReleasePointer(pointerEvt.pointerId);

            row.transform.position = Vector3.zero;
            row.style.opacity = StyleKeyword.Null;
            row.style.position = StyleKeyword.Null;
            row.style.top = StyleKeyword.Null;
            row.style.left = StyleKeyword.Null;
            row.style.width = StyleKeyword.Null;

            // Read new index from spacer position before removing it
            int newIndex = originalIndex;
            if (propDragSpacer != null && propDragSpacer.parent != null)
            {
                newIndex = 0;
                foreach (var child in content.Children())
                {
                    if (child == propDragSpacer) break;
                    if (child == row || child is Button) continue;
                    newIndex++;
                }
                propDragSpacer.parent.Remove(propDragSpacer);
                propDragSpacer = null;
            }

            if (!(evt is PointerUpEvent))
            {
                RebuildPropertiesList(content);
                return;
            }

            var actor = context.SelectedActor;
            int clamped = Mathf.Clamp(newIndex, 0, (actor?.Properties?.Count ?? 1) - 1);
            if (actor?.Properties != null && actor.Properties.Count > 1 &&
                originalIndex >= 0 && originalIndex < actor.Properties.Count &&
                originalIndex != clamped)
            {
                var focused = this.focusController?.focusedElement;
                if (focused != null) focused.Blur();

                var propToMove = actor.Properties[originalIndex];
                actor.Properties.RemoveAt(originalIndex);
                actor.Properties.Insert(clamped, propToMove);
                EditorUtility.SetDirty(context.currentProject);
            }

            RebuildPropertiesList(content);
            evt.StopPropagation();
        }

        // ──────────────────────────────────
        //  COMPONENT DRAG (spacer-based)
        // ──────────────────────────────────
        private void OnCompDragStart(PointerDownEvent evt, VisualElement card, int index)
        {
            if (evt.button != 0 || context.SelectedActor?.Components == null) return;

            isDraggingComp = true;
            dragStartPosComp = evt.position;
            draggedCompItem = card;
            draggedCompIndex = index;

            compDragSpacer = new VisualElement();
            compDragSpacer.style.height = Mathf.Max(30f, card.layout.height);
            compDragSpacer.style.marginBottom = card.resolvedStyle.marginBottom;
            compDragSpacer.style.marginTop = card.resolvedStyle.marginTop;
            compDragSpacer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.2f);
            compDragSpacer.style.borderTopWidth = 2;
            compDragSpacer.style.borderBottomWidth = 2;
            compDragSpacer.style.borderLeftWidth = 2;
            compDragSpacer.style.borderRightWidth = 2;
            compDragSpacer.style.borderTopColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            compDragSpacer.style.borderBottomColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            compDragSpacer.style.borderLeftColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            compDragSpacer.style.borderRightColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            compDragSpacer.style.borderTopLeftRadius = 5;
            compDragSpacer.style.borderTopRightRadius = 5;
            compDragSpacer.style.borderBottomLeftRadius = 5;
            compDragSpacer.style.borderBottomRightRadius = 5;

            int insertAt = componentsContainer.IndexOf(card);
            if (insertAt < 0) return;
            componentsContainer.Insert(insertAt, compDragSpacer);

            card.style.position = Position.Absolute;
            card.style.top = card.layout.y;
            card.style.left = card.layout.x;
            card.style.width = card.layout.width;
            card.style.opacity = 0.8f;
            card.BringToFront();

            card.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCompDragMove(PointerMoveEvent evt, VisualElement card)
        {
            if (!isDraggingComp || card != draggedCompItem) return;

            float diffY = evt.position.y - dragStartPosComp.y;
            card.transform.position = new Vector3(0f, diffY, 0f);

            float draggedCenterY = card.layout.y + diffY + card.layout.height / 2f;

            int newTargetIndex = 0;
            foreach (var child in componentsContainer.Children())
            {
                if (child == card || child == compDragSpacer) continue;
                if (draggedCenterY < child.layout.y + child.layout.height / 2f) break;
                newTargetIndex++;
            }

            int currentSpacerIndex = 0;
            foreach (var child in componentsContainer.Children())
            {
                if (child == compDragSpacer) break;
                if (child == card) continue;
                currentSpacerIndex++;
            }

            if (newTargetIndex != currentSpacerIndex)
            {
                componentsContainer.Remove(compDragSpacer);
                int physicalInsert = 0;
                int logical = 0;
                foreach (var child in componentsContainer.Children())
                {
                    if (logical == newTargetIndex) break;
                    if (child != card) logical++;
                    physicalInsert++;
                }
                componentsContainer.Insert(physicalInsert, compDragSpacer);
            }

            evt.StopPropagation();
        }

        private void OnCompDragEnd(EventBase evt, VisualElement card)
        {
            if (!isDraggingComp || card != draggedCompItem) return;

            int originalIndex = draggedCompIndex;
            isDraggingComp = false;
            draggedCompItem = null;
            draggedCompIndex = -1;

            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null) card.ReleasePointer(pointerEvt.pointerId);

            card.transform.position = Vector3.zero;
            card.style.opacity = StyleKeyword.Null;
            card.style.position = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.left = StyleKeyword.Null;
            card.style.width = StyleKeyword.Null;

            int newIndex = originalIndex;
            if (compDragSpacer != null && compDragSpacer.parent != null)
            {
                newIndex = 0;
                foreach (var child in componentsContainer.Children())
                {
                    if (child == compDragSpacer) break;
                    if (child == card) continue;
                    newIndex++;
                }
                compDragSpacer.parent.Remove(compDragSpacer);
                compDragSpacer = null;
            }

            if (!(evt is PointerUpEvent))
            {
                RebuildComponents();
                return;
            }

            var actor = context.SelectedActor;
            int clamped = Mathf.Clamp(newIndex, 0, (actor?.Components?.Count ?? 1) - 1);
            if (actor?.Components != null && actor.Components.Count > 1 &&
                originalIndex >= 0 && originalIndex < actor.Components.Count &&
                originalIndex != clamped)
            {
                Undo.RecordObject(context.currentProject, "Reorder Components");
                var compToMove = actor.Components[originalIndex];
                actor.Components.RemoveAt(originalIndex);
                actor.Components.Insert(clamped, compToMove);
                EditorUtility.SetDirty(context.currentProject);
            }

            RebuildComponents();
            evt.StopPropagation();
        }

        // ──────────────────────────────────
        //  ADD / REMOVE COMPONENT
        // ──────────────────────────────────
        private void ShowAddComponentMenu()
        {
            var actor = context.SelectedActor;
            if (actor == null) return;

            var menu = new GenericMenu();

            // Properties is limited to one per actor
            if (HasComponentOfType(actor, "Properties"))
                menu.AddDisabledItem(new GUIContent("Properties"));
            else
                menu.AddItem(new GUIContent("Properties"), false, () => AddPropertiesComponent());

            // Rules allows multiple
            menu.AddItem(new GUIContent("Rules"), false, () => AddRulesComponent());

            menu.ShowAsContext();
        }

        private void AddPropertiesComponent()
        {
            var actor = context.SelectedActor;
            if (actor == null) return;
            Undo.RecordObject(context.currentProject, "Add Component");
            actor.Components.Add(new ActorComponentMeta { type = "Properties", name = "Properties" });
            EditorUtility.SetDirty(context.currentProject);
            RebuildComponents();
        }

        private void AddRulesComponent()
        {
            var actor = context.SelectedActor;
            if (actor == null) return;
            Undo.RecordObject(context.currentProject, "Add Component");
            int rulesCount = 0;
            foreach (var c in actor.Components) if (c.type == "Rules") rulesCount++;
            actor.Components.Add(new ActorComponentMeta
            {
                type = "Rules",
                name = rulesCount == 0 ? "Rules" : $"Rules {rulesCount + 1}",
                id = NewGroupId()
            });
            EditorUtility.SetDirty(context.currentProject);
            RebuildComponents();
        }

        /// <param name="groupId">For Rules components: the id of the component being removed (clears only its rules).</param>
        private void RemoveComponent(int compIndex, string groupId)
        {
            var actor = context.SelectedActor;
            if (actor == null || compIndex < 0 || compIndex >= actor.Components.Count) return;

            var comp = actor.Components[compIndex];
            bool isProperties = comp.type == "Properties";

            bool hasData = isProperties
                ? (actor.Properties != null && actor.Properties.Count > 0)
                : (actor.Script != null && actor.Script.Exists(r => r.groupId == groupId));

            if (hasData)
            {
                string dataLabel = isProperties ? "properties" : "rules";
                if (!EditorUtility.DisplayDialog("Remove Component",
                    $"This will also delete all {dataLabel} in this component. Are you sure?",
                    "Remove", "Cancel"))
                    return;

                Undo.RecordObject(context.currentProject, "Remove Component");
                if (isProperties)
                    actor.Properties.Clear();
                else
                    actor.Script.RemoveAll(r => r.groupId == groupId);
            }
            else
            {
                Undo.RecordObject(context.currentProject, "Remove Component");
            }

            actor.Components.RemoveAt(compIndex);
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
            RebuildComponents();
        }

        private static bool HasComponentOfType(ActorJson actor, string type)
        {
            if (actor.Components == null) return false;
            foreach (var c in actor.Components)
                if (c.type == type) return true;
            return false;
        }

        private static string NewGroupId() =>
            "g" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

        // ──────────────────────────────────
        //  VECTOR OVERRIDES
        // ──────────────────────────────────
        private void CreateOverrideableVector3(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var label = new Label(labelText) { style = { minWidth = 140, unityFontStyleAndWeight = FontStyle.Normal } };
            row.Add(label);

            var field = new Vector3Field { style = { flexGrow = 1 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                Undo.RecordObject(context.currentProject, "Change " + propertyKey);
                float[] arr = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                var actor = context.SelectedActor;
                switch (propertyKey)
                {
                    case "Position": actor.Position = arr; break;
                    case "Rotation": actor.Rotation = arr; break;
                    case "Scale": actor.Scale = arr; break;
                }
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            });

            row.Add(field);
            parent.Add(row);
            rows[propertyKey] = new VectorRow { container = row, field = field, label = label };
        }

        private void UpdateVectorRow(string key, float[] actorData, Vector3 prefabDefault)
        {
            if (!rows.ContainsKey(key)) return;
            var row = rows[key];
            var vecField = (Vector3Field)row.field;
            bool isOverridden = actorData != null && actorData.Length >= 3;
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

        // ──────────────────────────────────
        //  HELPERS
        // ──────────────────────────────────

        /// <summary>Hides the label element of a TextField so it doesn't push the input area left.</summary>
        private static void HideTextFieldLabel(TextField field)
        {
            if (field.labelElement == null) return;
            var s = field.labelElement.style;
            s.display = DisplayStyle.None;
            s.width = 0;
            s.minWidth = 0;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            s.paddingLeft = 0; s.paddingRight = 0; s.paddingTop = 0; s.paddingBottom = 0;
        }

        private void UpdateActorProp(int index, string newName, string newVal)
        {
            if (context.SelectedActor?.Properties == null || index >= context.SelectedActor.Properties.Count) return;
            context.SelectedActor.Properties[index] = $"{newName}={newVal}";
            EditorUtility.SetDirty(context.currentProject);
        }

        private string GetUniqueActorName(string baseName, int currentActorIndex)
        {
            string candidate = baseName;
            int suffix = 1;
            while (context.currentProject.actors.Exists(
                a => a != context.currentProject.actors[currentActorIndex] && a.ActorName == candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }
            return candidate;
        }

        private void RenameSceneObject(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || oldName == newName) return;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != oldName || !go.scene.IsValid() || EditorUtility.IsPersistent(go)) continue;
                go.name = newName;
                return;
            }
        }

        // ──────────────────────────────────
        //  ACTOR HEADER CALLBACKS
        // ──────────────────────────────────
        private void OnActiveToggleValueChanged(ChangeEvent<bool> evt)
        {
            if (suppressActiveToggleCallback || context.selectedActorIndex < 0 || context.SelectedActor == null) return;
            controller.UpdateActorProperty(context.selectedActorIndex,
                () => context.SelectedActor.Active = evt.newValue, "Toggle Actor Active");
        }

        private void OnActorNameFieldValueChanged(ChangeEvent<string> evt)
        {
            if (suppressActorNameCallback || context.selectedActorIndex < 0 || context.SelectedActor == null) return;

            string requested = (evt.newValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(requested)) return;

            string oldName = context.SelectedActor.ActorName ?? "Unnamed";
            string uniqueName = GetUniqueActorName(requested, context.selectedActorIndex);
            if (uniqueName == oldName) return;

            controller.UpdateActorProperty(context.selectedActorIndex, () =>
            {
                context.SelectedActor.ActorName = uniqueName;
                RenameSceneObject(oldName, uniqueName);
            }, "Rename Actor");

            context.NotifyActorListChanged();

            if (uniqueName != requested)
            {
                suppressActorNameCallback = true;
                actorNameField.SetValueWithoutNotify(uniqueName);
                suppressActorNameCallback = false;
            }
        }

        private void OnActorNameFieldFocusOut(FocusOutEvent evt)
        {
            if (context.selectedActorIndex < 0 || context.SelectedActor == null) return;
            string currentText = (actorNameField.value ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(currentText)) return;
            suppressActorNameCallback = true;
            actorNameField.SetValueWithoutNotify(context.SelectedActor.ActorName ?? "Unnamed");
            suppressActorNameCallback = false;
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

                void FinishTagAddition()
                {
                    string newTag = textField.value.Trim();
                    if (!string.IsNullOrEmpty(newTag) && newTag != "Add Custom Tag...")
                    {
                        bool exists = false;
                        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
                            if (t == newTag) { exists = true; break; }
                        if (!exists)
                        {
                            var tagMgr = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                            var tagsProp = tagMgr.FindProperty("tags");
                            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = newTag;
                            tagMgr.ApplyModifiedProperties();
                        }
                        if (context.SelectedActor != null)
                        {
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
                textField.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
                    {
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
    }
}
