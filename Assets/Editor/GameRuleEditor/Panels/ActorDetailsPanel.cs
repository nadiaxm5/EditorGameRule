using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    /// <summary>
    /// Panel for editing the selected actor's properties
    /// </summary>
    public class ActorDetailsPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        private TextField actorNameField;
        private TextField prefabNameField;
        private TextField tagField;
        private Toggle activeToggle;

        // Transform
        private Vector3Field positionField;

        private Vector3Field rotationField;
        private Vector3Field scaleField;
        private Vector3Field sizeField; // New

        // Physics
        private Vector3Field velocityField;        // New

        private Vector3Field angularVelocityField; // New
        private FloatField densityField;
        private FloatField frictionField;
        private FloatField bouncinessField;
        private FloatField dragField;

        private VisualElement propertiesContainer;
        private VisualElement noSelectionContainer;

        public ActorDetailsPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            // Subscribe to events
            context.OnActorSelected += _ => UpdateUI();
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            // No selection message
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;

            var noSelectionLabel = new Label("Select an actor to edit its properties");
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            noSelectionContainer.Add(noSelectionLabel);

            Add(noSelectionContainer);

            // Properties container (initially hidden)
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.display = DisplayStyle.None;

            // Header
            var header = new Label("Actor Properties");
            header.AddToClassList("panel-header");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 15;
            scrollView.Add(header);

            // --- Basic Properties Section ---
            var basicSection = CreateSection("Basic Properties");
            scrollView.Add(basicSection);

            actorNameField = CreateTextField(basicSection, "Actor Name:");
            actorNameField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.ActorName = evt.newValue;
                }, "Change Actor Name");
            });

            prefabNameField = CreateTextField(basicSection, "Prefab Name:");
            prefabNameField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.PrefabName = evt.newValue;
                }, "Change Prefab Name");
            });

            tagField = CreateTextField(basicSection, "Tag:");
            tagField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Tag = evt.newValue;
                }, "Change Tag");
            });

            activeToggle = CreateToggle(basicSection, "Active:");
            activeToggle.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Active = evt.newValue;
                }, "Change Active State");
            });

            // --- Transform Section ---
            var transformSection = CreateSection("Transform");
            scrollView.Add(transformSection);

            positionField = CreateVector3Field(transformSection, "Position:");
            positionField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Position = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Position");
            });

            rotationField = CreateVector3Field(transformSection, "Rotation:");
            rotationField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Rotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Rotation");
            });

            scaleField = CreateVector3Field(transformSection, "Scale:");
            scaleField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Scale = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Scale");
            });

            sizeField = CreateVector3Field(transformSection, "Size (Target):");
            sizeField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Size = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Size");
            });

            // --- Physics Section ---
            var physicsSection = CreateSection("Physics");
            scrollView.Add(physicsSection);

            velocityField = CreateVector3Field(physicsSection, "Linear Velocity:");
            velocityField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Velocity = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Velocity");
            });

            angularVelocityField = CreateVector3Field(physicsSection, "Angular Velocity:");
            angularVelocityField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.AngularVelocity = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Angular Velocity");
            });

            densityField = CreateFloatField(physicsSection, "Density (Mass):");
            densityField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Density = evt.newValue;
                }, "Change Density");
            });

            frictionField = CreateFloatField(physicsSection, "Friction:");
            frictionField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Friction = evt.newValue;
                }, "Change Friction");
            });

            bouncinessField = CreateFloatField(physicsSection, "Bounciness:");
            bouncinessField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Bounciness = evt.newValue;
                }, "Change Bounciness");
            });

            dragField = CreateFloatField(physicsSection, "Drag (Damping):");
            dragField.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;
                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    context.SelectedActor.Drag = evt.newValue;
                }, "Change Drag");
            });

            // --- Custom Properties Section ---
            var propsSection = CreateSection("Custom Properties");
            scrollView.Add(propsSection);

            var addPropButton = new Button(() => ShowAddPropertyDialog());
            addPropButton.text = "+ Add Property";
            addPropButton.AddToClassList("button-primary");
            propsSection.Add(addPropButton);

            propertiesContainer = new VisualElement();
            propsSection.Add(propertiesContainer);

            Add(scrollView);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("panel-section");

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            section.Add(titleLabel);

            return section;
        }

        private TextField CreateTextField(VisualElement parent, string label)
        {
            var field = new TextField(label);
            field.style.marginBottom = 5;
            parent.Add(field);
            return field;
        }

        private Toggle CreateToggle(VisualElement parent, string label)
        {
            var toggle = new Toggle(label);
            toggle.style.marginBottom = 5;
            parent.Add(toggle);
            return toggle;
        }

        private FloatField CreateFloatField(VisualElement parent, string label)
        {
            var field = new FloatField(label);
            field.style.marginBottom = 5;
            parent.Add(field);
            return field;
        }

        private Vector3Field CreateVector3Field(VisualElement parent, string label)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 5;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 150;
            container.Add(labelElement);

            var field = new Vector3Field();
            field.style.flexGrow = 1;
            container.Add(field);

            parent.Add(container);
            return field;
        }

        private void ShowAddPropertyDialog()
        {
            if (context.selectedActorIndex < 0) return;

            // Simple property format: "propertyName=value"
            var dialog = ScriptableObject.CreateInstance<AddPropertyDialog>();
            dialog.ShowModal(propertyDef =>
            {
                if (!string.IsNullOrEmpty(propertyDef))
                {
                    controller.AddActorProperty(context.selectedActorIndex, propertyDef);
                }
            });
        }

        private void UpdateUI()
        {
            var actor = context.SelectedActor;

            if (actor == null)
            {
                // Show "no selection" message
                noSelectionContainer.style.display = DisplayStyle.Flex;
                this.Q<ScrollView>().style.display = DisplayStyle.None;
                return;
            }

            // Show properties
            noSelectionContainer.style.display = DisplayStyle.None;
            this.Q<ScrollView>().style.display = DisplayStyle.Flex;

            // Update fields without triggering callbacks
            if (actorNameField.value != (actor.ActorName ?? ""))
                actorNameField.SetValueWithoutNotify(actor.ActorName ?? "");

            if (prefabNameField.value != (actor.PrefabName ?? ""))
                prefabNameField.SetValueWithoutNotify(actor.PrefabName ?? "");

            if (tagField.value != (actor.Tag ?? ""))
                tagField.SetValueWithoutNotify(actor.Tag ?? "");

            activeToggle.SetValueWithoutNotify(actor.Active);

            // Transform
            if (actor.Position != null && actor.Position.Length >= 3)
                positionField.SetValueWithoutNotify(new Vector3(actor.Position[0], actor.Position[1], actor.Position[2]));
            else
                positionField.SetValueWithoutNotify(Vector3.zero);

            if (actor.Rotation != null && actor.Rotation.Length >= 3)
                rotationField.SetValueWithoutNotify(new Vector3(actor.Rotation[0], actor.Rotation[1], actor.Rotation[2]));
            else
                rotationField.SetValueWithoutNotify(Vector3.zero);

            if (actor.Scale != null && actor.Scale.Length >= 3)
                scaleField.SetValueWithoutNotify(new Vector3(actor.Scale[0], actor.Scale[1], actor.Scale[2]));
            else
                scaleField.SetValueWithoutNotify(Vector3.one);

            if (actor.Size != null && actor.Size.Length >= 3)
                sizeField.SetValueWithoutNotify(new Vector3(actor.Size[0], actor.Size[1], actor.Size[2]));
            else
                sizeField.SetValueWithoutNotify(Vector3.zero);

            // Physics
            if (actor.Velocity != null && actor.Velocity.Length >= 3)
                velocityField.SetValueWithoutNotify(new Vector3(actor.Velocity[0], actor.Velocity[1], actor.Velocity[2]));
            else
                velocityField.SetValueWithoutNotify(Vector3.zero);

            if (actor.AngularVelocity != null && actor.AngularVelocity.Length >= 3)
                angularVelocityField.SetValueWithoutNotify(new Vector3(actor.AngularVelocity[0], actor.AngularVelocity[1], actor.AngularVelocity[2]));
            else
                angularVelocityField.SetValueWithoutNotify(Vector3.zero);

            densityField.SetValueWithoutNotify(actor.Density);
            frictionField.SetValueWithoutNotify(actor.Friction);
            bouncinessField.SetValueWithoutNotify(actor.Bounciness);
            dragField.SetValueWithoutNotify(actor.Drag);

            UpdatePropertiesList();
        }

        private void UpdatePropertiesList()
        {
            propertiesContainer.Clear();

            var actor = context.SelectedActor;
            if (actor?.Properties == null || actor.Properties.Count == 0)
            {
                var emptyLabel = new Label("No custom properties");
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                emptyLabel.style.fontSize = 10;
                emptyLabel.style.marginTop = 5;
                propertiesContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < actor.Properties.Count; i++)
            {
                int index = i; // Capture for closure
                string property = actor.Properties[i];

                var item = new VisualElement();
                item.AddToClassList("list-item");
                item.style.marginTop = 5;

                var label = new Label(property);
                label.style.flexGrow = 1;
                label.style.fontSize = 11;
                item.Add(label);

                var removeBtn = new Button(() =>
                    controller.RemoveActorProperty(context.selectedActorIndex, index));
                removeBtn.text = "Remove";
                removeBtn.AddToClassList("button-danger");
                removeBtn.style.width = 80;
                removeBtn.style.height = 20;
                removeBtn.style.fontSize = 9;
                item.Add(removeBtn);

                propertiesContainer.Add(item);
            }
        }

        ~ActorDetailsPanel()
        {
            context.OnActorSelected -= _ => UpdateUI();
            context.OnProjectChanged -= UpdateUI;
        }
    }

    // Simple dialog for adding properties
    public class AddPropertyDialog : EditorWindow
    {
        private System.Action<string> callback;
        private string propertyName = "";
        private float propertyValue = 0f;

        public void ShowModal(System.Action<string> onComplete)
        {
            callback = onComplete;
            titleContent = new GUIContent("Add Property");
            minSize = new Vector2(300, 120);
            maxSize = new Vector2(300, 120);

            ShowModal();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Property Definition", EditorStyles.boldLabel);
            GUILayout.Space(5);

            propertyName = EditorGUILayout.TextField("Name:", propertyName);
            propertyValue = EditorGUILayout.FloatField("Value:", propertyValue);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("Add", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(propertyName))
                {
                    callback?.Invoke($"{propertyName}={propertyValue}");
                    Close();
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}