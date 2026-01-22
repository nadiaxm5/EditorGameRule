using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements; // Required for ObjectField
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    public class ActorDetailsPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        private TextField actorNameField;
        private ObjectField prefabPicker; // CHANGED: Replaced TextField
        private TextField tagField;
        private Toggle activeToggle;

        // Transform
        private Vector3Field positionField;
        private Vector3Field rotationField;
        private Vector3Field scaleField;
        private Vector3Field sizeField;

        // Physics
        private Vector3Field velocityField;
        private Vector3Field angularVelocityField;
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

            context.OnActorSelected += _ => UpdateUI();
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;
            var noSelectionLabel = new Label("Select an actor to edit its properties");
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            noSelectionContainer.Add(noSelectionLabel);
            Add(noSelectionContainer);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.display = DisplayStyle.None;

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

            // CHANGED: ObjectField for Prefab
            var prefabRow = new VisualElement();
            prefabRow.style.flexDirection = FlexDirection.Row;
            prefabRow.style.alignItems = Align.Center;
            prefabRow.style.marginBottom = 5;
            var prefabLabel = new Label("Prefab:") { style = { minWidth = 150 } };
            prefabRow.Add(prefabLabel);

            prefabPicker = new ObjectField();
            prefabPicker.objectType = typeof(GameObject);
            prefabPicker.allowSceneObjects = false;
            prefabPicker.style.flexGrow = 1;
            prefabPicker.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;

                controller.UpdateActorProperty(context.selectedActorIndex, () =>
                {
                    if (evt.newValue == null)
                    {
                        context.SelectedActor.PrefabName = "Empty"; // Default safety fallback
                    }
                    else
                    {
                        context.SelectedActor.PrefabName = evt.newValue.name;
                    }
                }, "Change Prefab");
            });
            prefabRow.Add(prefabPicker);
            basicSection.Add(prefabRow);

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
                noSelectionContainer.style.display = DisplayStyle.Flex;
                this.Q<ScrollView>().style.display = DisplayStyle.None;
                return;
            }

            noSelectionContainer.style.display = DisplayStyle.None;
            this.Q<ScrollView>().style.display = DisplayStyle.Flex;

            // Updates without callbacks
            if (actorNameField.value != (actor.ActorName ?? ""))
                actorNameField.SetValueWithoutNotify(actor.ActorName ?? "");

            // UPDATE PREFAB FIELD: Try to load from Resources
            GameObject prefabObj = Resources.Load<GameObject>("Prefabs/" + (actor.PrefabName ?? ""));
            prefabPicker.SetValueWithoutNotify(prefabObj);

            if (tagField.value != (actor.Tag ?? ""))
                tagField.SetValueWithoutNotify(actor.Tag ?? "");

            activeToggle.SetValueWithoutNotify(actor.Active);

            UpdateVector3(positionField, actor.Position, Vector3.zero);
            UpdateVector3(rotationField, actor.Rotation, Vector3.zero);
            UpdateVector3(scaleField, actor.Scale, Vector3.one);
            UpdateVector3(sizeField, actor.Size, Vector3.zero);
            UpdateVector3(velocityField, actor.Velocity, Vector3.zero);
            UpdateVector3(angularVelocityField, actor.AngularVelocity, Vector3.zero);

            densityField.SetValueWithoutNotify(actor.Density);
            frictionField.SetValueWithoutNotify(actor.Friction);
            bouncinessField.SetValueWithoutNotify(actor.Bounciness);
            dragField.SetValueWithoutNotify(actor.Drag);

            UpdatePropertiesList();
        }

        private void UpdateVector3(Vector3Field field, float[] data, Vector3 defaultVal)
        {
            if (data != null && data.Length >= 3)
                field.SetValueWithoutNotify(new Vector3(data[0], data[1], data[2]));
            else
                field.SetValueWithoutNotify(defaultVal);
        }

        private void UpdatePropertiesList()
        {
            propertiesContainer.Clear();
            var actor = context.SelectedActor;
            if (actor?.Properties == null || actor.Properties.Count == 0)
            {
                var emptyLabel = new Label("No custom properties") { style = { color = new Color(0.5f, 0.5f, 0.5f), fontSize = 10, marginTop = 5 } };
                propertiesContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < actor.Properties.Count; i++)
            {
                int index = i;
                string property = actor.Properties[i];
                var item = new VisualElement();
                item.AddToClassList("list-item");
                item.style.marginTop = 5;

                var label = new Label(property) { style = { flexGrow = 1, fontSize = 11 } };
                item.Add(label);

                var removeBtn = new Button(() => controller.RemoveActorProperty(context.selectedActorIndex, index));
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

    // Helper Dialog remains the same
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
            if (GUILayout.Button("Cancel", GUILayout.Width(80))) Close();
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