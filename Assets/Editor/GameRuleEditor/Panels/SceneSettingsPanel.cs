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
        private Vector3Field cameraPosField;
        private Vector3Field cameraRotField;
        private Vector3Field sunPosField;
        private Vector3Field sunRotField;
        private Vector3Field gravityField;

        // Variable List Container
        private VisualElement customVariablesContainer;

        // Creation Section References
        private VisualElement valueFieldContainer;
        private Func<object> activeValueGetter; // Delegate to retrieve value from dynamic field

        // Available types matches your backend logic (but capitalized for UI)
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

            // --- Basic Settings Section ---
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

            // --- Camera Section ---
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

            // --- Lighting Section ---
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

            // --- Physics Section ---
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

            // --- Custom Variables Section ---
            var customVarSection = CreateSection("Custom Global Variables");
            scrollView.Add(customVarSection);

            // 1. Variable Creation Area (Integrated)
            var creationBox = new VisualElement();
            creationBox.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            creationBox.style.paddingTop = 10;
            creationBox.style.paddingBottom = 10;
            creationBox.style.paddingLeft = 10;
            creationBox.style.paddingRight = 10;
            creationBox.style.borderTopLeftRadius = 5;
            creationBox.style.borderTopRightRadius = 5;
            creationBox.style.borderBottomLeftRadius = 5;
            creationBox.style.borderBottomRightRadius = 5;
            creationBox.style.marginBottom = 10;

            var creationTitle = new Label("Add New Variable");
            creationTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            creationTitle.style.marginBottom = 5;
            creationBox.Add(creationTitle);

            // Name Field
            var nameInput = new TextField("Name:");
            nameInput.value = "NewVariable";
            creationBox.Add(nameInput);

            // Type Dropdown (Now using PopupField<string> instead of EnumField)
            var typeInput = new PopupField<string>("Type:", variableTypes, 0);
            creationBox.Add(typeInput);

            // Container for dynamic value field (changes based on Type)
            valueFieldContainer = new VisualElement();
            valueFieldContainer.style.marginTop = 5;
            creationBox.Add(valueFieldContainer);

            // Initialize default value field (Int is index 0)
            CreateValueField("Int");

            // Handle Type Change
            typeInput.RegisterValueChangedCallback(evt =>
            {
                CreateValueField(evt.newValue);
            });

            // Add Button
            var addButton = new Button(() =>
            {
                string varName = nameInput.value;
                if (string.IsNullOrEmpty(varName))
                {
                    EditorUtility.DisplayDialog("Error", "Variable name cannot be empty.", "OK");
                    return;
                }

                // Get value from the active field via delegate
                object value = activeValueGetter?.Invoke();

                // Convert UI type (e.g. "Int") to Backend type (e.g. "int")
                string typeStr = typeInput.value.ToLower();

                controller.AddCustomVariable(varName, typeStr, value);

                // Reset Name for convenience
                nameInput.value = "NewVariable";
            });
            addButton.text = "+ Add Variable";
            addButton.AddToClassList("button-success");
            addButton.style.marginTop = 10;
            creationBox.Add(addButton);

            customVarSection.Add(creationBox);

            // 2. Variable List
            customVariablesContainer = new VisualElement();
            customVarSection.Add(customVariablesContainer);

            Add(scrollView);
        }

        /// <summary>
        /// Dynamically creates the input field for the initial value based on the selected Type string.
        /// </summary>
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

            var labelElement = new Label(label);
            labelElement.style.minWidth = 150;
            container.Add(labelElement);

            var field = new Vector2Field();
            field.style.flexGrow = 1;
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

            var labelElement = new Label(label);
            labelElement.style.minWidth = 150;
            container.Add(labelElement);

            var field = new Vector3Field();
            field.style.flexGrow = 1;
            container.Add(field);

            parent.Add(container);
            return field;
        }

        #endregion

        private void UpdateUI()
        {
            if (context?.currentProject?.sceneData == null)
            {
                SetEnabled(false);
                return;
            }

            SetEnabled(true);
            var scene = context.currentProject.sceneData;

            // Update fields without triggering callbacks (safe checks)
            if (gameNameField.value != (scene.GameName ?? ""))
                gameNameField.SetValueWithoutNotify(scene.GameName ?? "");

            if (scene.ScreenResolution != null && scene.ScreenResolution.Length >= 2)
            {
                var currentRes = new Vector2(scene.ScreenResolution[0], scene.ScreenResolution[1]);
                if (screenResolutionField.value != currentRes)
                    screenResolutionField.SetValueWithoutNotify(currentRes);
            }

            if (scene.CameraPosition != null && scene.CameraPosition.Length >= 3)
            {
                var currentPos = new Vector3(scene.CameraPosition[0], scene.CameraPosition[1], scene.CameraPosition[2]);
                if (cameraPosField.value != currentPos)
                    cameraPosField.SetValueWithoutNotify(currentPos);
            }

            if (scene.CameraRotation != null && scene.CameraRotation.Length >= 3)
            {
                var currentRot = new Vector3(scene.CameraRotation[0], scene.CameraRotation[1], scene.CameraRotation[2]);
                if (cameraRotField.value != currentRot)
                    cameraRotField.SetValueWithoutNotify(currentRot);
            }

            if (scene.SunPosition != null && scene.SunPosition.Length >= 3)
            {
                var currentPos = new Vector3(scene.SunPosition[0], scene.SunPosition[1], scene.SunPosition[2]);
                if (sunPosField.value != currentPos)
                    sunPosField.SetValueWithoutNotify(currentPos);
            }

            if (scene.SunRotation != null && scene.SunRotation.Length >= 3)
            {
                var currentRot = new Vector3(scene.SunRotation[0], scene.SunRotation[1], scene.SunRotation[2]);
                if (sunRotField.value != currentRot)
                    sunRotField.SetValueWithoutNotify(currentRot);
            }

            if (scene.Gravity != null && scene.Gravity.Length >= 3)
            {
                var currentGrav = new Vector3(scene.Gravity[0], scene.Gravity[1], scene.Gravity[2]);
                if (gravityField.value != currentGrav)
                    gravityField.SetValueWithoutNotify(currentGrav);
            }

            UpdateCustomVariablesList();
        }

        private void UpdateCustomVariablesList()
        {
            customVariablesContainer.Clear();

            if (context?.currentProject?.sceneData?.CustomVariables == null)
                return;

            for (int i = 0; i < context.currentProject.sceneData.CustomVariables.Count; i++)
            {
                int index = i; // Capture for closure
                var customVar = context.currentProject.sceneData.CustomVariables[i];

                var item = new VisualElement();
                item.AddToClassList("list-item");
                item.style.marginTop = 5;

                // Format the value string based on type
                string valueStr = "";
                switch (customVar.type.ToLower())
                {
                    case "int": valueStr = customVar.intValue.ToString(); break;
                    case "float": valueStr = customVar.floatValue.ToString("F2"); break;
                    case "bool": valueStr = customVar.boolValue.ToString(); break;
                    case "vector2":
                        if (customVar.arrayValue?.Length >= 2)
                            valueStr = $"({customVar.arrayValue[0]}, {customVar.arrayValue[1]})";
                        break;
                    case "vector3":
                        if (customVar.arrayValue?.Length >= 3)
                            valueStr = $"({customVar.arrayValue[0]}, {customVar.arrayValue[1]}, {customVar.arrayValue[2]})";
                        break;
                }

                var label = new Label($"{customVar.name} ({customVar.type}) = {valueStr}");
                label.style.flexGrow = 1;
                item.Add(label);

                var removeBtn = new Button(() => controller.RemoveCustomVariable(index));
                removeBtn.text = "Remove";
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