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
        /// <summary>Only prefabs directly inside this folder can be assigned to an actor.</summary>
        private const string PrefabsFolder = "Assets/Resources/Prefabs";

        private EditorContext context;
        private ProjectController controller;

        private VisualElement mainContainer;
        private VisualElement noSelectionContainer;

        private TextField actorNameField;
        private Label actorPropertiesTitle;
        private bool suppressActorNameCallback;

        private ObjectField prefabPicker;
        private DropdownField tagField;
        private Toggle activeToggle;
        private bool suppressActiveToggleCallback;

        private Vector3Field velocityField;
        private Vector3Field angularVelocityField;
        private FloatField densityField;
        private FloatField dragField;
        private FloatField frictionField;
        private FloatField bouncinessField;
        private Label physicsStatusLabel;
        private Button revertPhysicsButton;
        private readonly Dictionary<string, Label> physicsLabels = new Dictionary<string, Label>();

        private VisualElement ruleSetsContainer;
        private VisualElement customPropertiesContainer;

        private class VectorRow
        {
            public VisualElement container;
            public VisualElement field;
            public Label label;
        }
        private Dictionary<string, VectorRow> rows = new Dictionary<string, VectorRow>();

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

            // Subscribe on attach (not in the constructor): a docked window can detach and reattach
            // this panel, and a constructor-only subscription would be dropped on the first detach
            // and never restored, leaving the panel frozen on whatever actor it last showed.
            // Refreshing on attach also catches up on any selection missed while detached.
            RegisterCallback<AttachToPanelEvent>(evt =>
            {
                context.OnActorSelected += OnActorSelected;
                context.OnProjectChanged += UpdateUI;
                UpdateUI();
            });

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
            noSelectionContainer.Add(new Label("Select an actor to edit") { style = { color = GameRuleTheme.SubtleText } });
            Add(noSelectionContainer);

            mainContainer = new VisualElement();
            mainContainer.style.flexGrow = 1;
            mainContainer.style.display = DisplayStyle.None;
            Add(mainContainer);

            var scrollView = new ScrollView();
            mainContainer.Add(scrollView);

            actorPropertiesTitle = new Label();
            actorPropertiesTitle.AddToClassList("studio-panel-title");
            scrollView.Add(actorPropertiesTitle);

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
            actorNameField.AddToClassList("actor-properties-name");
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
            prefabPicker = new ObjectField { objectType = typeof(GameObject), allowSceneObjects = false, style = { flexGrow = 1, minWidth = 0 } };
            prefabPicker.tooltip = "Only prefabs inside " + PrefabsFolder;

            // The whole field opens our custom prefab picker, so Unity's separate circular
            // object-selector button is unnecessary.
            var prefabSelectorButton = prefabPicker.Q<VisualElement>(className: ObjectField.selectorUssClassName);
            if (prefabSelectorButton != null)
                prefabSelectorButton.style.display = DisplayStyle.None;

            prefabPicker.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;

                // Safety net: never accept anything that is not a prefab from PrefabsFolder (e.g. a model dropped in).
                if (evt.newValue != null && !IsSelectablePrefab(evt.newValue as GameObject))
                {
                    prefabPicker.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                Undo.RecordObject(context.currentProject, "Change Prefab");
                context.SelectedActor.PrefabName = evt.newValue == null ? "Empty" : evt.newValue.name;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                context.NotifyProjectChanged();
            });

            // Replace Unity's object picker (which also lists models) with a menu of prefabs only,
            // and reject drags of anything outside PrefabsFolder.
            prefabPicker.RegisterCallback<PointerDownEvent>(OnPrefabPickerPointerDown, TrickleDown.TrickleDown);
            prefabPicker.RegisterCallback<MouseDownEvent>(OnPrefabPickerMouseDown, TrickleDown.TrickleDown);
            prefabPicker.RegisterCallback<DragUpdatedEvent>(OnPrefabPickerDragUpdated, TrickleDown.TrickleDown);
            prefabPicker.RegisterCallback<DragPerformEvent>(OnPrefabPickerDragPerform, TrickleDown.TrickleDown);
            prefabRow.Add(prefabPicker);

            // Tag controls occupy 140 px and the actor-name field has a 10 px right margin.
            // Reserving both makes the two editable fields end at exactly the same position.
            prefabRow.Add(new VisualElement { style = { width = 150, flexShrink = 0 } });
            scrollView.Add(prefabRow);

            // ─── Transform (always-on) ───
            var transformSection = new Foldout { text = "Transform", value = true };
            GameRuleTheme.StylePropertySection(transformSection);

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

            // ─── Physics (inherited from the prefab until edited) ───
            var physicsSection = new Foldout { text = "Physics", value = true };
            GameRuleTheme.StylePropertySection(physicsSection);

            var physicsIcon = new Image { image = EditorGUIUtility.IconContent("Rigidbody Icon").image, style = { width = 16, height = 16, marginRight = 4 } };
            var physicsHeaderLabel = physicsSection.Q<Label>();
            if (physicsHeaderLabel != null)
                physicsHeaderLabel.parent.Insert(physicsHeaderLabel.parent.IndexOf(physicsHeaderLabel), physicsIcon);

            scrollView.Add(physicsSection);

            var physicsContainer = new VisualElement { style = { paddingLeft = 15, marginTop = 5 } };
            physicsSection.Add(physicsContainer);

            velocityField = CreatePhysicsVector3(physicsContainer, "Velocity", "Velocity");
            velocityField.tooltip = "Initial linear velocity.";
            angularVelocityField = CreatePhysicsVector3(physicsContainer, "Angular Velocity", "AngularVelocity");
            angularVelocityField.tooltip = "Initial angular velocity in radians per second.";
            densityField = CreatePhysicsFloat(physicsContainer, "Density", "Density");
            densityField.tooltip = "Applied to Rigidbody.mass for compatibility with the existing GameRule format.";
            dragField = CreatePhysicsFloat(physicsContainer, "Drag", "Drag");
            frictionField = CreatePhysicsFloat(physicsContainer, "Friction", "Friction");
            bouncinessField = CreatePhysicsFloat(physicsContainer, "Bounciness", "Bounciness");

            physicsStatusLabel = new Label();
            physicsStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            physicsStatusLabel.style.color = GameRuleTheme.MutedText;
            physicsStatusLabel.style.marginTop = 4;
            physicsStatusLabel.style.marginBottom = 4;
            physicsContainer.Add(physicsStatusLabel);

            revertPhysicsButton = new Button(() =>
            {
                if (context.selectedActorIndex >= 0)
                    controller.RevertActorProperty(context.selectedActorIndex, "Physics");
            }) { text = "Revert Physics to Prefab" };
            revertPhysicsButton.style.alignSelf = Align.FlexEnd;
            revertPhysicsButton.style.marginTop = 3;
            physicsContainer.Add(revertPhysicsButton);

            customPropertiesContainer = new VisualElement();
            scrollView.Add(customPropertiesContainer);

            var ruleSetsSection = new Foldout { text = "Rule Sets", value = true };
            GameRuleTheme.StylePropertySection(ruleSetsSection);
            var ruleSetsContent = new VisualElement();
            ruleSetsContent.AddToClassList("rule-overview-column");
            ruleSetsContent.AddToClassList("rule-sets-body");
            ruleSetsContainer = new VisualElement();
            ruleSetsContent.Add(ruleSetsContainer);

            var addRulesSetButton = new Button(AddRulesComponent) { text = "+ Add Rule Set" };
            addRulesSetButton.AddToClassList("button-primary");
            addRulesSetButton.style.alignSelf = Align.Center;
            ruleSetsContent.Add(addRulesSetButton);

            ruleSetsSection.Add(ruleSetsContent);
            scrollView.Add(ruleSetsSection);
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
            actorPropertiesTitle.text = $"{actor.ActorName} Properties";

            if (actor.Components == null)
                actor.Components = new List<ActorComponentMeta>();

            // Auto-migrate legacy data
            bool dirty = false;

            if (ActorPhysicsUtility.NormalizeLegacyOverrides(actor))
                dirty = true;

            if (actor.CustomProperties == null || (actor.Properties?.Count ?? 0) > 0)
            {
                actor.GetCustomProperties();
                dirty = true;
            }

            // 1. Ensure every Rules component has an id and use the current default name.
            int ruleSetNumber = 0;
            foreach (var comp in actor.Components)
            {
                if (comp.type != "Rules") continue;
                ruleSetNumber++;

                if (string.IsNullOrEmpty(comp.id))
                {
                    comp.id = NewGroupId();
                    dirty = true;
                }

                if (IsLegacyRuleSetName(comp.name))
                {
                    comp.name = NextRuleSetName(actor, ruleSetNumber);
                    dirty = true;
                }
            }

            // 2. Custom Properties is permanent and always appears before every Rules Set.
            int propertiesIndex = actor.Components.FindIndex(c => c.type == "Properties");
            if (propertiesIndex < 0)
            {
                actor.Components.Insert(0, new ActorComponentMeta { type = "Properties", name = "Custom Properties" });
                dirty = true;
            }
            else
            {
                var propertiesComponent = actor.Components[propertiesIndex];
                if (propertiesComponent.name != "Custom Properties")
                {
                    propertiesComponent.name = "Custom Properties";
                    dirty = true;
                }

                if (propertiesIndex != 0)
                {
                    actor.Components.RemoveAt(propertiesIndex);
                    actor.Components.Insert(0, propertiesComponent);
                    dirty = true;
                }
            }

            for (int i = actor.Components.Count - 1; i > 0; i--)
            {
                if (actor.Components[i].type == "Properties")
                {
                    actor.Components.RemoveAt(i);
                    dirty = true;
                }
            }

            // 3. Add default Rules component if needed, then assign groupIds to ungrouped rules
            if (actor.Script != null && actor.Script.Count > 0 && !HasComponentOfType(actor, "Rules"))
            {
                var defaultComp = new ActorComponentMeta { type = "Rules", name = "Rule Set", id = NewGroupId() };
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
            var choices = Loader.GetProjectTags(context.currentProject?.actors);
            choices.Add("Add Custom Tag...");
            tagField.choices = choices;
            tagField.SetValueWithoutNotify(actor.Tag ?? "Untagged");
            tagField.RegisterValueChangedCallback(OnTagFieldValueChanged);

            var iconImage = this.Q<Image>("PrefabColorImage");
            if (iconImage != null)
            {
                Color c = GameRuleTheme.PrefabIconTint;
                if (!string.IsNullOrEmpty(actor.IconColorHex))
                    ColorUtility.TryParseHtmlString(actor.IconColorHex, out c);
                iconImage.tintColor = c;
            }

            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + (actor.PrefabName ?? ""));
            prefabPicker.SetValueWithoutNotify(prefab);

            UpdateVectorRow("Position", actor.Position, prefab?.transform.position ?? Vector3.zero);
            UpdateVectorRow("Rotation", actor.Rotation, prefab?.transform.eulerAngles ?? Vector3.zero);
            UpdateVectorRow("Scale", actor.Scale, prefab?.transform.localScale ?? Vector3.one);
            UpdatePhysicsUI(actor, prefab);

            RebuildComponents();
        }

        // ──────────────────────────────────
        //  COMPONENT RENDERING
        // ──────────────────────────────────
        private void RebuildComponents()
        {
            customPropertiesContainer.Clear();
            ruleSetsContainer.Clear();
            var actor = context.SelectedActor;
            if (actor?.Components == null) return;

            for (int i = 0; i < actor.Components.Count; i++)
            {
                int compIndex = i;
                var comp = actor.Components[i];
                if (comp.type == "Properties")
                {
                    customPropertiesContainer.Add(BuildPropertiesComponent(comp, compIndex));
                    continue;
                }
                if (comp.type != "Rules") continue;

                var card = BuildRulesComponent(comp, compIndex);

                card.RegisterCallback<PointerMoveEvent>(evt => OnCompDragMove(evt, card));
                card.RegisterCallback<PointerUpEvent>(evt => OnCompDragEnd(evt, card));
                card.RegisterCallback<PointerCaptureOutEvent>(evt => OnCompDragEnd(evt, card));

                ruleSetsContainer.Add(card);
            }

        }

        /// <summary>
        /// Builds the foldout shell: drag handle + name (editable if renameable) + remove button.
        /// </summary>
        private Foldout BuildComponentShell(
            ActorComponentMeta comp,
            int compIndex,
            System.Action onRemove,
            bool renameable = false,
            bool draggable = true)
        {
            if (!initializedComponents.Contains(comp))
            {
                initializedComponents.Add(comp);
                if (comp.type != "Rules" && comp.type != "Properties")
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
            GameRuleTheme.StylePropertySection(foldout, useSectionSpacing: comp.type != "Rules");

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

            if (draggable)
            {
                // Drag handle — initiates component reorder
                var dragHandle = new Label("\u2261");
                dragHandle.tooltip = "Drag to reorder";
                dragHandle.pickingMode = PickingMode.Position;
                dragHandle.style.width = 16;
                dragHandle.style.unityTextAlign = TextAnchor.MiddleCenter;
                dragHandle.style.color = GameRuleTheme.SubtleText;
                dragHandle.style.marginLeft = 2;
                dragHandle.style.marginRight = 4;
                dragHandle.RegisterCallback<PointerDownEvent>(evt =>
                    OnCompDragStart(evt, foldout, compIndex));
                toggleInput.Insert(1, dragHandle);
            }

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

            if (onRemove != null)
            {
                var removeBtn = new Button(onRemove) { text = "×" };
                removeBtn.tooltip = "Remove Component";
                removeBtn.AddToClassList("button-danger");
                removeBtn.AddToClassList("button-danger-icon");
                removeBtn.style.marginLeft = 4;
                removeBtn.style.paddingLeft = 0;
                removeBtn.style.paddingRight = 0;

                removeBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                toggle.Add(removeBtn);
            }

            return foldout;
        }

        private VisualElement BuildPropertiesComponent(ActorComponentMeta comp, int compIndex)
        {
            var foldout = BuildComponentShell(comp, compIndex, null, draggable: false);
            var actor = context.SelectedActor;
            foldout.Add(new CustomPropertyList(() => context.currentProject, () => actor.GetCustomProperties()));
            return foldout;
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
            foldout.AddToClassList("rule-set-card");
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
            infoLabel.style.color = GameRuleTheme.MutedText;
            infoLabel.style.fontSize = 11;
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);

            string capturedId = comp.id;
            var openBtn = new Button(() => GameRuleRulesWindow.EnsureVisible(context, controller, capturedId, comp.name));
            openBtn.text = "Open Rules Window";
            openBtn.AddToClassList("button-primary");
            openBtn.style.alignSelf = Align.Center;
            content.Add(openBtn);

            return foldout;
        }

        // ──────────────────────────────────
        //  PROPERTY DRAG (spacer-based)
        // ──────────────────────────────────
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
            compDragSpacer.style.backgroundColor = GameRuleTheme.Field;
            compDragSpacer.style.borderTopWidth = 2;
            compDragSpacer.style.borderBottomWidth = 2;
            compDragSpacer.style.borderLeftWidth = 2;
            compDragSpacer.style.borderRightWidth = 2;
            compDragSpacer.style.borderTopColor = GameRuleTheme.AccentText;
            compDragSpacer.style.borderBottomColor = GameRuleTheme.AccentText;
            compDragSpacer.style.borderLeftColor = GameRuleTheme.AccentText;
            compDragSpacer.style.borderRightColor = GameRuleTheme.AccentText;
            compDragSpacer.style.borderTopLeftRadius = 5;
            compDragSpacer.style.borderTopRightRadius = 5;
            compDragSpacer.style.borderBottomLeftRadius = 5;
            compDragSpacer.style.borderBottomRightRadius = 5;

            int insertAt = ruleSetsContainer.IndexOf(card);
            if (insertAt < 0) return;
            ruleSetsContainer.Insert(insertAt, compDragSpacer);

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
            card.style.translate = new Translate(0f, diffY, 0f);

            float draggedCenterY = card.layout.y + diffY + card.layout.height / 2f;

            int newTargetIndex = 0;
            foreach (var child in ruleSetsContainer.Children())
            {
                if (child == card || child == compDragSpacer) continue;
                if (draggedCenterY < child.layout.y + child.layout.height / 2f) break;
                newTargetIndex++;
            }

            int currentSpacerIndex = 0;
            foreach (var child in ruleSetsContainer.Children())
            {
                if (child == compDragSpacer) break;
                if (child == card) continue;
                currentSpacerIndex++;
            }

            if (newTargetIndex != currentSpacerIndex)
            {
                ruleSetsContainer.Remove(compDragSpacer);
                int physicalInsert = 0;
                int logical = 0;
                foreach (var child in ruleSetsContainer.Children())
                {
                    if (logical == newTargetIndex) break;
                    if (child != card) logical++;
                    physicalInsert++;
                }
                ruleSetsContainer.Insert(physicalInsert, compDragSpacer);
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

            card.style.translate = new Translate(0f, 0f, 0f);
            card.style.opacity = StyleKeyword.Null;
            card.style.position = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.left = StyleKeyword.Null;
            card.style.width = StyleKeyword.Null;

            int targetRuleSetIndex = -1;
            if (compDragSpacer != null && compDragSpacer.parent != null)
            {
                targetRuleSetIndex = 0;
                foreach (var child in ruleSetsContainer.Children())
                {
                    if (child == compDragSpacer) break;
                    if (child == card) continue;
                    targetRuleSetIndex++;
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
            int newIndex = -1;
            if (actor?.Components != null && targetRuleSetIndex >= 0)
            {
                for (int i = 0; i < actor.Components.Count; i++)
                {
                    if (actor.Components[i].type != "Rules") continue;
                    if (targetRuleSetIndex-- == 0)
                    {
                        newIndex = i;
                        break;
                    }
                }
            }
            if (newIndex >= 0 && originalIndex >= 0 && originalIndex < actor.Components.Count &&
                originalIndex != newIndex)
            {
                Undo.RecordObject(context.currentProject, "Reorder Components");
                var compToMove = actor.Components[originalIndex];
                actor.Components.RemoveAt(originalIndex);
                actor.Components.Insert(newIndex, compToMove);
                EditorUtility.SetDirty(context.currentProject);
            }

            RebuildComponents();
            evt.StopPropagation();
        }

        // ──────────────────────────────────
        //  PREFAB PICKER (prefabs only)
        // ──────────────────────────────────
        private void OnPrefabPickerPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            evt.StopImmediatePropagation();
            evt.StopPropagation();
            ShowPrefabPicker();
        }

        private void OnPrefabPickerMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return;
            evt.StopImmediatePropagation();
            evt.StopPropagation();
        }

        private void OnPrefabPickerDragUpdated(DragUpdatedEvent evt)
        {
            if (IsValidPrefabDrag()) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            evt.StopImmediatePropagation();
            evt.StopPropagation();
        }

        private void OnPrefabPickerDragPerform(DragPerformEvent evt)
        {
            if (IsValidPrefabDrag()) return;
            evt.StopImmediatePropagation();
            evt.StopPropagation();
        }

        private void ShowPrefabPicker()
        {
            if (context.selectedActorIndex < 0) return;

            PropertyPickerDialog.ShowPrefab(context, prefabName =>
            {
                GameObject prefab = LoadSelectablePrefabs().Find(item => item.name == prefabName);
                if (prefab != null) prefabPicker.value = prefab;
            }, includeActorPrefabs: false,
                anchorScreenRect: PropertyPickerDialog.GetScreenRect(prefabPicker));
        }

        private static List<GameObject> LoadSelectablePrefabs()
        {
            var prefabs = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(PrefabsFolder)) return prefabs;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (IsSelectablePrefab(prefab)) prefabs.Add(prefab);
            }

            prefabs.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return prefabs;
        }

        /// <summary>A .prefab asset stored directly in PrefabsFolder — models (.fbx) and nested folders are excluded.</summary>
        private static bool IsSelectablePrefab(GameObject go)
        {
            if (go == null) return false;

            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) return false;

            string folder = System.IO.Path.GetDirectoryName(path);
            return folder != null && folder.Replace('\\', '/') == PrefabsFolder;
        }

        private static bool IsValidPrefabDrag()
        {
            var dragged = DragAndDrop.objectReferences;
            return dragged.Length == 1 && IsSelectablePrefab(dragged[0] as GameObject);
        }

        // ──────────────────────────────────
        //  ADD / REMOVE RULES SET
        // ──────────────────────────────────
        private void AddRulesComponent()
        {
            var actor = context.SelectedActor;
            if (actor == null) return;
            if (actor.Components == null)
                actor.Components = new List<ActorComponentMeta>();
            int nextNumber = actor.Components.FindAll(component => component.type == "Rules").Count + 1;

            Undo.RecordObject(context.currentProject, "Add Rule Set");
            actor.Components.Add(new ActorComponentMeta
            {
                type = "Rules",
                name = NextRuleSetName(actor, nextNumber),
                id = NewGroupId()
            });
            EditorUtility.SetDirty(context.currentProject);
            context.NotifyProjectChanged();
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
                ? (actor.GetCustomProperties().Count > 0)
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
                    actor.GetCustomProperties().Clear();
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

        private static bool IsLegacyRuleSetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "Rules" || name == "Rules Set")
                return true;
            foreach (string prefix in new[] { "Rules ", "Rules Set " })
                if (name.StartsWith(prefix, System.StringComparison.Ordinal) &&
                    int.TryParse(name.Substring(prefix.Length), out _))
                    return true;
            return false;
        }

        private static string NextRuleSetName(ActorJson actor, int number)
        {
            string name;
            do
            {
                name = number == 1 ? "Rule Set" : $"Rule Set {number}";
                number++;
            }
            while (actor.Components.Exists(component => component.type == "Rules" &&
                string.Equals(component.name, name, System.StringComparison.OrdinalIgnoreCase)));

            return name;
        }

        // ──────────────────────────────────
        //  VECTOR OVERRIDES
        // ──────────────────────────────────
        private void CreateOverrideableVector3(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var label = new Label(labelText) { style = { minWidth = 140, unityFontStyleAndWeight = FontStyle.Normal } };
            row.Add(label);

            var field = new Vector3Field { style = { flexGrow = 1 } };
            field.RegisterRoundedChange(valueFromField =>
            {
                if (context.selectedActorIndex < 0) return;
                Undo.RecordObject(context.currentProject, "Change " + propertyKey);
                float[] arr = new float[] { valueFromField.x, valueFromField.y, valueFromField.z };
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

        private Vector3Field CreatePhysicsVector3(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var label = new Label(labelText) { style = { minWidth = 140, unityFontStyleAndWeight = FontStyle.Normal } };
            row.Add(label);

            var field = new Vector3Field { style = { flexGrow = 1 } };
            field.RegisterRoundedChange(valueFromField =>
            {
                ActorJson actor = context.SelectedActor;
                if (actor == null) return;

                Undo.RecordObject(context.currentProject, "Change " + labelText);
                float[] value = { valueFromField.x, valueFromField.y, valueFromField.z };
                if (propertyKey == "Velocity") actor.Velocity = value;
                else actor.AngularVelocity = value;

                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(actor);
                if (revertPhysicsButton != null) revertPhysicsButton.SetEnabled(true);
            });

            row.Add(field);
            parent.Add(row);
            physicsLabels[propertyKey] = label;
            return field;
        }

        private FloatField CreatePhysicsFloat(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var label = new Label(labelText) { style = { minWidth = 140, unityFontStyleAndWeight = FontStyle.Normal } };
            row.Add(label);

            var field = new FloatField { style = { flexGrow = 1 } };
            field.RegisterRoundedChange(valueFromField =>
            {
                ActorJson actor = context.SelectedActor;
                if (actor == null) return;

                float value = valueFromField;
                if (propertyKey == "Density") value = Mathf.Max(0.01f, value);
                else if (propertyKey == "Drag") value = Mathf.Max(0f, value);
                else value = Mathf.Clamp01(value);

                Undo.RecordObject(context.currentProject, "Change " + labelText);
                switch (propertyKey)
                {
                    case "Density": actor.Density = value; actor.OverrideDensity = true; break;
                    case "Drag": actor.Drag = value; actor.OverrideDrag = true; break;
                    case "Friction": actor.Friction = value; actor.OverrideFriction = true; break;
                    case "Bounciness": actor.Bounciness = value; actor.OverrideBounciness = true; break;
                }

                if (value != valueFromField)
                    field.SetValueWithoutNotify(value);

                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(actor);
                if (revertPhysicsButton != null) revertPhysicsButton.SetEnabled(true);
            });

            row.Add(field);
            parent.Add(row);
            physicsLabels[propertyKey] = label;
            return field;
        }

        private void UpdatePhysicsUI(ActorJson actor, GameObject prefab)
        {
            Rigidbody body = prefab != null ? prefab.GetComponent<Rigidbody>() : null;
            Collider collider = prefab != null ? prefab.GetComponent<Collider>() : null;

            velocityField.SetEnabled(body != null);
            angularVelocityField.SetEnabled(body != null);
            densityField.SetEnabled(body != null);
            dragField.SetEnabled(body != null);
            frictionField.SetEnabled(collider != null);
            bouncinessField.SetEnabled(collider != null);

            velocityField.SetValueWithoutNotify(ActorPhysicsUtility.ToVector3(actor.Velocity, body != null ? body.linearVelocity : Vector3.zero));
            angularVelocityField.SetValueWithoutNotify(ActorPhysicsUtility.ToVector3(actor.AngularVelocity, body != null ? body.angularVelocity : Vector3.zero));
            densityField.SetValueWithoutNotify(actor.OverrideDensity ? actor.Density : body != null ? body.mass : 0f);
            dragField.SetValueWithoutNotify(actor.OverrideDrag ? actor.Drag : body != null ? body.linearDamping : 0f);
            frictionField.SetValueWithoutNotify(actor.OverrideFriction ? actor.Friction : ActorPhysicsUtility.GetFriction(collider));
            bouncinessField.SetValueWithoutNotify(actor.OverrideBounciness ? actor.Bounciness : ActorPhysicsUtility.GetBounciness(collider));

            SetPhysicsLabelState("Velocity", actor.Velocity != null && actor.Velocity.Length >= 3);
            SetPhysicsLabelState("AngularVelocity", actor.AngularVelocity != null && actor.AngularVelocity.Length >= 3);
            SetPhysicsLabelState("Density", actor.OverrideDensity);
            SetPhysicsLabelState("Drag", actor.OverrideDrag);
            SetPhysicsLabelState("Friction", actor.OverrideFriction);
            SetPhysicsLabelState("Bounciness", actor.OverrideBounciness);

            if (body != null && collider != null)
                physicsStatusLabel.text = "Root Rigidbody and Collider found.";
            else if (body == null && collider == null)
                physicsStatusLabel.text = "The prefab root has no Rigidbody or Collider. Physics fields are disabled.";
            else if (body == null)
                physicsStatusLabel.text = "The prefab root has no Rigidbody. Rigidbody fields are disabled.";
            else
                physicsStatusLabel.text = "The prefab root has no Collider. Collider fields are disabled.";

            revertPhysicsButton.SetEnabled(ActorPhysicsUtility.HasOverrides(actor));
        }

        private void SetPhysicsLabelState(string key, bool overridden)
        {
            if (physicsLabels.TryGetValue(key, out Label label))
                label.style.unityFontStyleAndWeight = overridden ? FontStyle.Bold : FontStyle.Normal;
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
