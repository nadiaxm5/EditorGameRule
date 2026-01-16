using UnityEngine;
using UnityEngine.UIElements;
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

        private TextField gameNameField;
        private Vector2Field screenResolutionField;
        private Vector3Field cameraPosField;
        private Vector3Field cameraRotField;
        private Vector3Field sunPosField;
        private Vector3Field sunRotField;
        private Vector3Field gravityField;

        private VisualElement customVariablesContainer;

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

            // Basic Settings Section
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

            // Camera Section
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

            // Lighting Section
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

            // Physics Section
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

            // Custom Variables Section
            var customVarSection = CreateSection("Custom Global Variables");
            scrollView.Add(customVarSection);

            var addVarButton = new Button(() => ShowAddVariableDialog());
            addVarButton.text = "+ Add Custom Variable";
            addVarButton.AddToClassList("button-primary");
            customVarSection.Add(addVarButton);

            customVariablesContainer = new VisualElement();
            customVarSection.Add(customVariablesContainer);

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

        private void ShowAddVariableDialog()
        {
            var dialog = new VisualElement();
            dialog.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            dialog.style.paddingTop = 20;
            dialog.style.paddingBottom = 20;
            dialog.style.paddingLeft = 20;
            dialog.style.paddingRight = 20;
            dialog.style.borderTopLeftRadius = 5;
            dialog.style.borderTopRightRadius = 5;
            dialog.style.borderBottomLeftRadius = 5;
            dialog.style.borderBottomRightRadius = 5;
            dialog.style.borderLeftWidth = 1;
            dialog.style.borderRightWidth = 1;
            dialog.style.borderTopWidth = 1;
            dialog.style.borderBottomWidth = 1;
            dialog.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            dialog.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            dialog.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            dialog.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);

            var nameField = new TextField("Variable Name:");
            dialog.Add(nameField);

            var typeField = new UnityEngine.UIElements.EnumField("Type:", CustomVariableType.Float);
            dialog.Add(typeField);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 10;

            var cancelBtn = new Button(() => { /* Close dialog */ });
            cancelBtn.text = "Cancel";
            buttonRow.Add(cancelBtn);

            var addBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(nameField.value))
                {
                    string type = typeField.value.ToString().ToLower();
                    controller.AddCustomVariable(nameField.value, type);
                }
            });
            addBtn.text = "Add";
            addBtn.AddToClassList("button-primary");
            buttonRow.Add(addBtn);

            dialog.Add(buttonRow);

            // For now, let's use a simpler approach with EditorUtility
            UnityEditor.EditorApplication.delayCall += () =>
            {
                string name = UnityEditor.EditorUtility.DisplayDialog(
                    "Add Custom Variable",
                    "Enter variable name in Console",
                    "Float",
                    "Int"
                ) ? "float" : "int";

                string varName = "NewVariable"; // TODO: Add proper dialog
                controller.AddCustomVariable(varName, name);
            };
        }

        private void UpdateUI()
        {
            if (context?.currentProject?.sceneData == null)
            {
                SetEnabled(false);
                return;
            }

            SetEnabled(true);

            if (context?.currentProject?.sceneData == null) { /*...*/ return; }

            SetEnabled(true);
            var scene = context.currentProject.sceneData;

            // Update fields without triggering callbacks
            if (gameNameField.value != (scene.GameName ?? ""))
                gameNameField.SetValueWithoutNotify(scene.GameName ?? "");

            if (scene.ScreenResolution != null && scene.ScreenResolution.Length >= 2)
                screenResolutionField.SetValueWithoutNotify(new Vector2(scene.ScreenResolution[0], scene.ScreenResolution[1]));

            if (scene.CameraPosition != null && scene.CameraPosition.Length >= 3)
                cameraPosField.SetValueWithoutNotify(new Vector3(scene.CameraPosition[0], scene.CameraPosition[1], scene.CameraPosition[2]));

            if (scene.CameraRotation != null && scene.CameraRotation.Length >= 3)
                cameraRotField.SetValueWithoutNotify(new Vector3(scene.CameraRotation[0], scene.CameraRotation[1], scene.CameraRotation[2]));

            if (scene.SunPosition != null && scene.SunPosition.Length >= 3)
                sunPosField.SetValueWithoutNotify(new Vector3(scene.SunPosition[0], scene.SunPosition[1], scene.SunPosition[2]));

            if (scene.SunRotation != null && scene.SunRotation.Length >= 3)
                sunRotField.SetValueWithoutNotify(new Vector3(scene.SunRotation[0], scene.SunRotation[1], scene.SunRotation[2]));

            if (scene.Gravity != null && scene.Gravity.Length >= 3)
                gravityField.SetValueWithoutNotify(new Vector3(scene.Gravity[0], scene.Gravity[1], scene.Gravity[2]));

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

                var label = new Label($"{customVar.name} ({customVar.type})");
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

    // Helper enum for variable type selection
    public enum CustomVariableType
    {
        Int,
        Float,
        Bool,
        Vector2,
        Vector3
    }
}