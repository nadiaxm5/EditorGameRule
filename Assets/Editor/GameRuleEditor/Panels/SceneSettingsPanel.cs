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

            // Cleanup when removed from visual tree
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                context.OnProjectLoaded -= UpdateUI;
                context.OnProjectChanged -= UpdateUI;
            });
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

            // Vinculación nativa (El binding)
            gameNameField = CreateTextField(basicSection, "Game Name:");
            // Secondary callback to keep projectName in sync (native binding handles GameName + undo)
            gameNameField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject != null)
                    context.currentProject.projectName = evt.newValue;
            });

            screenResolutionField = CreateVector2Field(basicSection, "Screen Resolution:");
            screenResolutionField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector2Field (Vector2) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Screen Resolution");
                context.currentProject.sceneData.ScreenResolution = new float[] { evt.newValue.x, evt.newValue.y };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            backgroundColorField = CreateColorField(basicSection, "Background Color:");
            backgroundColorField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Background Color");
                context.currentProject.sceneData.BackgroundColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Camera section
            var cameraSection = CreateSection("Camera");
            scrollView.Add(cameraSection);

            cameraPosField = CreateVector3Field(cameraSection, "Position:");
            cameraPosField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Camera Position");
                context.currentProject.sceneData.CameraPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            cameraRotField = CreateVector3Field(cameraSection, "Rotation:");
            cameraRotField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Camera Rotation");
                context.currentProject.sceneData.CameraRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Lighting section
            var lightingSection = CreateSection("Lighting (Sun)");
            scrollView.Add(lightingSection);

            sunPosField = CreateVector3Field(lightingSection, "Position:");
            sunPosField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Position");
                context.currentProject.sceneData.SunPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunRotField = CreateVector3Field(lightingSection, "Rotation:");
            sunRotField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Rotation");
                context.currentProject.sceneData.SunRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunColorField = CreateColorField(lightingSection, "Sun Color:");
            sunColorField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Color");
                context.currentProject.sceneData.SunColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunAmbientField = CreateColorField(lightingSection, "Ambient Color:");
            sunAmbientField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Ambient Color");
                context.currentProject.sceneData.SunAmbientColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Physics section
            var physicsSection = CreateSection("Physics");
            scrollView.Add(physicsSection);

            gravityField = CreateVector3Field(physicsSection, "Gravity:");
            gravityField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Gravity");
                context.currentProject.sceneData.Gravity = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
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
            if (context?.currentProject?.sceneData == null)
            {
                SetEnabled(false);
                gameNameField.Unbind();
                return;
            }
            SetEnabled(true);
            var scene = context.currentProject.sceneData;

            // --- Native Data Binding for direct 1:1 type field ---
            // SerializedProperty binding gives automatic Undo/Redo support for GameName.
            var so = new SerializedObject(context.currentProject);
            gameNameField.BindProperty(so.FindProperty("sceneData.GameName"));

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
            if (context?.currentProject?.sceneData?.CustomVariables == null) return;
            var variables = context.currentProject.sceneData.CustomVariables;

            // Determina si es necesario reconstruir las filas para no perder el foco de escritura
            bool needsRebuild = customVariablesContainer.childCount != variables.Count;
            if (!needsRebuild)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    var row = customVariablesContainer[i];
                    string expectedType = variables[i].type.ToLower();
                    bool hasCorrectField = false;

                    // Comprueba si el campo de valor visual coincide con el tipo de la variable real
                    switch (expectedType)
                    {
                        case "int": hasCorrectField = row.Q<IntegerField>("VarValue") != null; break;
                        case "float": hasCorrectField = row.Q<FloatField>("VarValue") != null; break;
                        case "bool": hasCorrectField = row.Q<Toggle>("VarValue") != null; break;
                        case "vector2": hasCorrectField = row.Q<Vector2Field>("VarValue") != null; break;
                        case "vector3": hasCorrectField = row.Q<Vector3Field>("VarValue") != null; break;
                    }

                    if (!hasCorrectField)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (needsRebuild)
            {
                customVariablesContainer.Clear();
                for (int i = 0; i < variables.Count; i++)
                {
                    int index = i;
                    var customVar = variables[index];

                    var item = new VisualElement();
                    item.AddToClassList("list-item");
                    item.style.marginTop = 5;
                    item.style.flexDirection = FlexDirection.Row;
                    item.style.alignItems = Align.Center;

                    // Campo para el nombre
                    var nameField = new TextField { name = "VarName", value = customVar.name, style = { width = 120, marginRight = 5 } };
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(context.currentProject, "Change Var Name");
                        context.currentProject.sceneData.CustomVariables[index].name = evt.newValue;
                        EditorUtility.SetDirty(context.currentProject);
                        context.NotifyProjectChanged();
                    });
                    item.Add(nameField);

                    // Desplegable de tipo
                    string currentTypeStr = customVar.type.ToLower();
                    string matchType = variableTypes.Find(t => t.ToLower() == currentTypeStr) ?? "Int";

                    var typeDropdown = new PopupField<string>(variableTypes, variableTypes.IndexOf(matchType)) { name = "VarType", style = { width = 80, marginRight = 5 } };
                    typeDropdown.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(context.currentProject, "Change Var Type");
                        var v = context.currentProject.sceneData.CustomVariables[index];
                        v.type = evt.newValue.ToLower();
                        v.intValue = 0; v.floatValue = 0f; v.boolValue = false;
                        if (v.type == "vector2") v.arrayValue = new float[2];
                        if (v.type == "vector3") v.arrayValue = new float[3];
                        EditorUtility.SetDirty(context.currentProject);
                        context.NotifyProjectChanged();
                    });
                    item.Add(typeDropdown);

                    // Contenedor din�mico de valor
                    var valueContainer = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row, marginRight = 5 } };

                    switch (currentTypeStr)
                    {
                        case "int":
                            var intF = new IntegerField { name = "VarValue", value = customVar.intValue, style = { flexGrow = 1 } };
                            intF.RegisterValueChangedCallback(evt =>
                            {
                                Undo.RecordObject(context.currentProject, "Change Var Value");
                                context.currentProject.sceneData.CustomVariables[index].intValue = evt.newValue;
                                EditorUtility.SetDirty(context.currentProject);
                                context.NotifyProjectChanged();
                            });
                            valueContainer.Add(intF);
                            break;

                        case "float":
                            var floatF = new FloatField { name = "VarValue", value = customVar.floatValue, style = { flexGrow = 1 } };
                            floatF.RegisterValueChangedCallback(evt =>
                            {
                                Undo.RecordObject(context.currentProject, "Change Var Value");
                                context.currentProject.sceneData.CustomVariables[index].floatValue = evt.newValue;
                                EditorUtility.SetDirty(context.currentProject);
                                context.NotifyProjectChanged();
                            });
                            valueContainer.Add(floatF);
                            break;

                        case "bool":
                            var boolF = new Toggle { name = "VarValue", value = customVar.boolValue, style = { flexGrow = 1 } };
                            boolF.RegisterValueChangedCallback(evt =>
                            {
                                Undo.RecordObject(context.currentProject, "Change Var Value");
                                context.currentProject.sceneData.CustomVariables[index].boolValue = evt.newValue;
                                EditorUtility.SetDirty(context.currentProject);
                                context.NotifyProjectChanged();
                            });
                            valueContainer.Add(boolF);
                            break;

                        case "vector2":
                            Vector2 v2 = (customVar.arrayValue != null && customVar.arrayValue.Length >= 2) ? new Vector2(customVar.arrayValue[0], customVar.arrayValue[1]) : Vector2.zero;
                            var v2F = new Vector2Field { name = "VarValue", value = v2, style = { flexGrow = 1 } };
                            v2F.RegisterValueChangedCallback(evt =>
                            {
                                // Manual Undo: Vector2Field (Vector2) -> float[] (type mismatch)
                                Undo.RecordObject(context.currentProject, "Change Var Value");
                                context.currentProject.sceneData.CustomVariables[index].arrayValue = new float[] { evt.newValue.x, evt.newValue.y };
                                EditorUtility.SetDirty(context.currentProject);
                                context.NotifyProjectChanged();
                            });
                            valueContainer.Add(v2F);
                            break;

                        case "vector3":
                            Vector3 v3 = (customVar.arrayValue != null && customVar.arrayValue.Length >= 3) ? new Vector3(customVar.arrayValue[0], customVar.arrayValue[1], customVar.arrayValue[2]) : Vector3.zero;
                            var v3F = new Vector3Field { name = "VarValue", value = v3, style = { flexGrow = 1 } };
                            v3F.RegisterValueChangedCallback(evt =>
                            {
                                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                                Undo.RecordObject(context.currentProject, "Change Var Value");
                                context.currentProject.sceneData.CustomVariables[index].arrayValue = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                                EditorUtility.SetDirty(context.currentProject);
                                context.NotifyProjectChanged();
                            });
                            valueContainer.Add(v3F);
                            break;
                    }

                    item.Add(valueContainer);

                    // Bot�n para eliminar la variable
                    var removeBtn = new Button(() => controller.RemoveCustomVariable(index)) { text = "Remove" };
                    removeBtn.AddToClassList("button-danger");
                    removeBtn.style.width = 70;
                    item.Add(removeBtn);

                    customVariablesContainer.Add(item);
                }
            }
            else
            {
                // Solo actualiza los valores de forma silenciosa para no interrumpir si el usuario est� escribiendo
                for (int i = 0; i < variables.Count; i++)
                {
                    var customVar = variables[i];
                    var row = customVariablesContainer[i];

                    var nameField = row.Q<TextField>("VarName");
                    if (nameField != null && nameField.value != customVar.name)
                        nameField.SetValueWithoutNotify(customVar.name);

                    switch (customVar.type.ToLower())
                    {
                        case "int":
                            var intF = row.Q<IntegerField>("VarValue");
                            if (intF != null && intF.value != customVar.intValue) intF.SetValueWithoutNotify(customVar.intValue);
                            break;

                        case "float":
                            var floatF = row.Q<FloatField>("VarValue");
                            if (floatF != null && floatF.value != customVar.floatValue) floatF.SetValueWithoutNotify(customVar.floatValue);
                            break;

                        case "bool":
                            var boolF = row.Q<Toggle>("VarValue");
                            if (boolF != null && boolF.value != customVar.boolValue) boolF.SetValueWithoutNotify(customVar.boolValue);
                            break;

                        case "vector2":
                            var v2F = row.Q<Vector2Field>("VarValue");
                            if (v2F != null)
                            {
                                Vector2 v2 = (customVar.arrayValue != null && customVar.arrayValue.Length >= 2) ? new Vector2(customVar.arrayValue[0], customVar.arrayValue[1]) : Vector2.zero;
                                if (v2F.value != v2) v2F.SetValueWithoutNotify(v2);
                            }
                            break;

                        case "vector3":
                            var v3F = row.Q<Vector3Field>("VarValue");
                            if (v3F != null)
                            {
                                Vector3 v3 = (customVar.arrayValue != null && customVar.arrayValue.Length >= 3) ? new Vector3(customVar.arrayValue[0], customVar.arrayValue[1], customVar.arrayValue[2]) : Vector3.zero;
                                if (v3F.value != v3) v3F.SetValueWithoutNotify(v3);
                            }
                            break;
                    }
                }
            }
        }

        ~SceneSettingsPanel()
        {
            context.OnProjectLoaded -= UpdateUI;
            context.OnProjectChanged -= UpdateUI;
        }
    }
}