using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    /// <summary>
    /// Panel for editing scene settings (GameManager properties)
    /// </summary>
    public class SceneSettingsPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        // Scene Fields
        private TextField gameNameField;

        private Vector2Field screenResolutionField;

        // Camera
        private Vector3Field cameraPosField;

        private Vector3Field cameraRotField;

        // Lighting
        private Vector3Field sunPosField;

        private Vector3Field sunRotField;
        private ColorField sunColorField;
        private ColorField sunAmbientField;

        // Physics / Background
        private ColorField backgroundColorField;

        private Vector3Field gravityField;

        // Variable List Container
        private VisualElement customVariablesContainer;

        // Creation Section References
        private VisualElement valueFieldContainer;

        private Func<object> activeValueGetter;

        // Available types
        private List<string> variableTypes = new List<string> { "Int", "Float", "Bool", "Vector2", "Vector3" };

        public SceneSettingsPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            // Subscribe to events
            context.OnProjectLoaded += UpdateUI;
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            // Header
            var header = new Label("Scene Settings");
            header.AddToClassList("panel-header");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 15;
            scrollView.Add(header);

            // Basic settings section
            var basicSection = CreateSection("Basic Settings");
            scrollView.Add(basicSection);

            gameNameField = CreateTextField(basicSection, "Game Name:");
            gameNameField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.GameName = evt.newValue;
                    context.currentProject.projectName = evt.newValue;
                }, "Change Game Name");
            });

            screenResolutionField = CreateVector2Field(basicSection, "Screen Resolution:");
            screenResolutionField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.ScreenResolution = new float[] { evt.newValue.x, evt.newValue.y };
                }, "Change Screen Resolution");
            });

            backgroundColorField = CreateColorField(basicSection, "Background Color:");
            backgroundColorField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.BackgroundColor = ColorToBytes(evt.newValue);
                }, "Change Background Color");
            });

            // Camera section
            var cameraSection = CreateSection("Camera");
            scrollView.Add(cameraSection);

            cameraPosField = CreateVector3Field(cameraSection, "Position:");
            cameraPosField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.CameraPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Camera Position");
            });

            cameraRotField = CreateVector3Field(cameraSection, "Rotation:");
            cameraRotField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.CameraRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Camera Rotation");
            });

            // Lighting section
            var lightingSection = CreateSection("Lighting (Sun)");
            scrollView.Add(lightingSection);

            sunPosField = CreateVector3Field(lightingSection, "Position:");
            sunPosField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.SunPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Sun Position");
            });

            sunRotField = CreateVector3Field(lightingSection, "Rotation:");
            sunRotField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.SunRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Sun Rotation");
            });

            sunColorField = CreateColorField(lightingSection, "Sun Color:");
            sunColorField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.SunColor = ColorToBytes(evt.newValue);
                }, "Change Sun Color");
            });

            sunAmbientField = CreateColorField(lightingSection, "Ambient Color:");
            sunAmbientField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.SunAmbientColor = ColorToBytes(evt.newValue);
                }, "Change Ambient Color");
            });

            // Physics section
            var physicsSection = CreateSection("Physics");
            scrollView.Add(physicsSection);

            gravityField = CreateVector3Field(physicsSection, "Gravity:");
            gravityField.RegisterValueChangedCallback(evt =>
            {
                controller.UpdateSceneProperty(() =>
                {
                    context.currentProject.sceneData.Gravity = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                }, "Change Gravity");
            });

            // Custom Variables section
            var customVarSection = CreateSection("Custom Global Variables");
            scrollView.Add(customVarSection);

            // Variable creation
            var creationBox = new VisualElement();
            creationBox.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            creationBox.style.paddingTop = 10; creationBox.style.paddingBottom = 10;
            creationBox.style.paddingLeft = 10; creationBox.style.paddingRight = 10;
            creationBox.style.borderTopLeftRadius = 5; creationBox.style.borderTopRightRadius = 5;
            creationBox.style.borderBottomLeftRadius = 5; creationBox.style.borderBottomRightRadius = 5;
            creationBox.style.marginBottom = 10;

            var creationTitle = new Label("Add New Variable");
            creationTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            creationTitle.style.marginBottom = 5;
            creationBox.Add(creationTitle);

            var nameInput = new TextField("Name:");
            nameInput.value = "NewVariable";
            creationBox.Add(nameInput);

            var typeInput = new PopupField<string>("Type:", variableTypes, 0);
            creationBox.Add(typeInput);

            valueFieldContainer = new VisualElement();
            valueFieldContainer.style.marginTop = 5;
            creationBox.Add(valueFieldContainer);

            CreateValueField("Int"); // Default int

            typeInput.RegisterValueChangedCallback(evt => CreateValueField(evt.newValue));

            var addButton = new Button(() =>
            {
                string varName = nameInput.value;
                if (string.IsNullOrEmpty(varName))
                {
                    EditorUtility.DisplayDialog("Error", "Variable name cannot be empty.", "OK");
                    return;
                }

                object value = activeValueGetter?.Invoke();
                string typeStr = typeInput.value.ToLower();

                controller.AddCustomVariable(varName, typeStr, value);
                nameInput.value = "NewVariable";
            });
            addButton.text = "+ Add Variable";
            addButton.AddToClassList("button-success");
            addButton.style.marginTop = 10;
            creationBox.Add(addButton);

            customVarSection.Add(creationBox);

            customVariablesContainer = new VisualElement();
            customVarSection.Add(customVariablesContainer);

            Add(scrollView);
        }

        private void CreateValueField(string type)
        {
            valueFieldContainer.Clear();
            switch (type)
            {
                case "Int":
                    var intField = new IntegerField("Initial Value:");
                    intField.value = 0;
                    valueFieldContainer.Add(intField);
                    activeValueGetter = () => intField.value;
                    break;

                case "Float":
                    var floatField = new FloatField("Initial Value:");
                    floatField.value = 0f;
                    valueFieldContainer.Add(floatField);
                    activeValueGetter = () => floatField.value;
                    break;

                case "Bool":
                    var boolField = new Toggle("Initial Value:");
                    boolField.value = false;
                    valueFieldContainer.Add(boolField);
                    activeValueGetter = () => boolField.value;
                    break;

                case "Vector2":
                    var v2Field = new Vector2Field("Initial Value:");
                    v2Field.value = Vector2.zero;
                    valueFieldContainer.Add(v2Field);
                    activeValueGetter = () => v2Field.value;
                    break;

                case "Vector3":
                    var v3Field = new Vector3Field("Initial Value:");
                    v3Field.value = Vector3.zero;
                    valueFieldContainer.Add(v3Field);
                    activeValueGetter = () => v3Field.value;
                    break;
            }
        }

        #region UI Helpers

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

        private Vector2Field CreateVector2Field(VisualElement parent, string label)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 5;
            var labelElement = new Label(label) { style = { minWidth = 150 } };
            container.Add(labelElement);
            var field = new Vector2Field() { style = { flexGrow = 1 } };
            container.Add(field);
            parent.Add(container);
            return field;
        }

        private Vector3Field CreateVector3Field(VisualElement parent, string label)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 5;
            var labelElement = new Label(label) { style = { minWidth = 150 } };
            container.Add(labelElement);
            var field = new Vector3Field() { style = { flexGrow = 1 } };
            container.Add(field);
            parent.Add(container);
            return field;
        }

        private ColorField CreateColorField(VisualElement parent, string label)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 5;
            var labelElement = new Label(label) { style = { minWidth = 150 } };
            container.Add(labelElement);
            var field = new ColorField() { style = { flexGrow = 1 } };
            container.Add(field);
            parent.Add(container);
            return field;
        }

        #endregion UI Helpers

        // Helper to convert Unity Color to byte array [0-255]
        private byte[] ColorToBytes(Color c)
        {
            return new byte[] {
                (byte)(c.r * 255),
                (byte)(c.g * 255),
                (byte)(c.b * 255)
            };
        }

        // Helper to convert byte array to Unity Color
        private Color BytesToColor(byte[] b)
        {
            if (b == null || b.Length < 3) return Color.white;
            return new Color(b[0] / 255f, b[1] / 255f, b[2] / 255f);
        }

        private void UpdateUI()
        {
            if (context?.currentProject?.sceneData == null) { SetEnabled(false); return; }
            SetEnabled(true);
            var scene = context.currentProject.sceneData;

            if (gameNameField.value != (scene.GameName ?? ""))
                gameNameField.SetValueWithoutNotify(scene.GameName ?? "");

            if (scene.ScreenResolution != null && scene.ScreenResolution.Length >= 2)
            {
                var res = new Vector2(scene.ScreenResolution[0], scene.ScreenResolution[1]);
                if (screenResolutionField.value != res) screenResolutionField.SetValueWithoutNotify(res);
            }

            if (backgroundColorField.value != BytesToColor(scene.BackgroundColor))
                backgroundColorField.SetValueWithoutNotify(BytesToColor(scene.BackgroundColor));

            UpdateVector3Field(cameraPosField, scene.CameraPosition);
            UpdateVector3Field(cameraRotField, scene.CameraRotation);
            UpdateVector3Field(sunPosField, scene.SunPosition);
            UpdateVector3Field(sunRotField, scene.SunRotation);
            UpdateVector3Field(gravityField, scene.Gravity);

            if (sunColorField.value != BytesToColor(scene.SunColor))
                sunColorField.SetValueWithoutNotify(BytesToColor(scene.SunColor));

            if (sunAmbientField.value != BytesToColor(scene.SunAmbientColor))
                sunAmbientField.SetValueWithoutNotify(BytesToColor(scene.SunAmbientColor));

            UpdateCustomVariablesList();
        }

        private void UpdateVector3Field(Vector3Field field, float[] data)
        {
            if (data != null && data.Length >= 3)
            {
                var vec = new Vector3(data[0], data[1], data[2]);
                if (field.value != vec) field.SetValueWithoutNotify(vec);
            }
        }

        private void UpdateCustomVariablesList()
        {
            customVariablesContainer.Clear();
            if (context?.currentProject?.sceneData?.CustomVariable == null) return;

            for (int i = 0; i < context.currentProject.sceneData.CustomVariable.Count; i++)
            {
                int index = i;
                var customVar = context.currentProject.sceneData.CustomVariable[i];
                var item = new VisualElement();
                item.AddToClassList("list-item");
                item.style.marginTop = 5;

                string valueStr = "";
                switch (customVar.type.ToLower())
                {
                    case "int": valueStr = customVar.intValue.ToString(); break;
                    case "float": valueStr = customVar.floatValue.ToString("F2"); break;
                    case "bool": valueStr = customVar.boolValue.ToString(); break;
                    case "vector2":
                        if (customVar.arrayValue?.Length >= 2) valueStr = $"({customVar.arrayValue[0]}, {customVar.arrayValue[1]})"; break;
                    case "vector3":
                        if (customVar.arrayValue?.Length >= 3) valueStr = $"({customVar.arrayValue[0]}, {customVar.arrayValue[1]}, {customVar.arrayValue[2]})"; break;
                }

                var label = new Label($"{customVar.name} ({customVar.type}) = {valueStr}") { style = { flexGrow = 1 } };
                item.Add(label);

                var removeBtn = new Button(() => controller.RemoveCustomVariable(index)) { text = "Remove" };
                removeBtn.AddToClassList("button-danger");
                removeBtn.style.width = 80;
                item.Add(removeBtn);

                customVariablesContainer.Add(item);
            }
        }

        ~SceneSettingsPanel()
        {
            context.OnProjectLoaded -= UpdateUI;
            context.OnProjectChanged -= UpdateUI;
        }
    }
}