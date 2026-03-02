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
        private TextField tagField;
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

            context.OnActorSelected += _ => UpdateUI();
            // Update UI when project data changes (including scene sync)
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            // No Selection View
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;
            noSelectionContainer.Add(new Label("Select an actor to edit") { style = { color = Color.gray } });
            Add(noSelectionContainer);

            // Content View (Usando mainContainer)
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

            var basicSection = CreateSection("Basic Properties");
            scrollView.Add(basicSection);

            // Name (native binding - undo handled automatically via SerializedProperty)
            actorNameField = new TextField("Name:");
            basicSection.Add(actorNameField);

            // Prefab Picker
            var prefabRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            prefabRow.Add(new Label("Prefab:") { style = { minWidth = 150 } });
            prefabPicker = new ObjectField { objectType = typeof(GameObject), allowSceneObjects = false, style = { flexGrow = 1 } };
            prefabPicker.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;

                // Deshacer manual porque ObjectField no se enlaza directamente a un SerializedProperty (string), y necesitamos convertir GameObject -> prefab name (string).
                Undo.RecordObject(context.currentProject, "Change Prefab");
                context.SelectedActor.PrefabName = evt.newValue == null ? "Empty" : evt.newValue.name;
                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(context.SelectedActor);
                context.NotifyProjectChanged();
            });
            prefabRow.Add(prefabPicker);
            basicSection.Add(prefabRow);

            // Tag (native binding - undo handled automatically via SerializedProperty)
            tagField = new TextField("Tag:");
            basicSection.Add(tagField);

            // Active (native binding - undo handled automatically via SerializedProperty)
            activeToggle = new Toggle("Active:");
            basicSection.Add(activeToggle);

            // Transform
            var transformSection = CreateSection("Transform");
            scrollView.Add(transformSection);

            CreateOverrideableVector3(transformSection, "Position", "Position");
            CreateOverrideableVector3(transformSection, "Rotation", "Rotation");
            CreateOverrideableVector3(transformSection, "Scale", "Scale");
            CreateOverrideableVector3(transformSection, "Size (Target)", "Size");

            // Custom Properties
            var propsSection = CreateSection("Custom Properties");
            scrollView.Add(propsSection);

            var addPropBtn = new Button(() => ShowAddPropertyDialog()) { text = "+ Add Property" };
            addPropBtn.AddToClassList("button-primary");
            propsSection.Add(addPropBtn);

            propertiesContainer = new VisualElement();
            propsSection.Add(propertiesContainer);
        }

        #region UI Construction Helpers

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("panel-section");
            section.Add(new Label(title) { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });
            return section;
        }

        private void CreateOverrideableVector3(VisualElement parent, string labelText, string propertyKey)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

            var label = new Label(labelText) { style = { minWidth = 140 } };
            row.Add(label);

            var field = new Vector3Field { style = { flexGrow = 1 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex < 0) return;

                // Deshacer manual porque Vector3Field no se enlaza directamente a un SerializedProperty (float[]), y necesitamos convertir Vector3 -> float[].
                Undo.RecordObject(context.currentProject, $"Change {propertyKey}");

                var vec = evt.newValue;
                float[] arr = new float[] { vec.x, vec.y, vec.z };

                var actor = context.SelectedActor;
                switch (propertyKey)
                {
                    case "Position": actor.Position = arr; break;
                    case "Rotation": actor.Rotation = arr; break;
                    case "Scale": actor.Scale = arr; break;
                    case "Size": actor.Size = arr; break;
                    case "Velocity": actor.Velocity = arr; break;
                    case "AngularVelocity": actor.AngularVelocity = arr; break;
                }

                EditorUtility.SetDirty(context.currentProject);
                controller.SyncDataToScene(actor);
                context.NotifyProjectChanged();
            });
            row.Add(field);

            parent.Add(row);

            // Store references
            rows[propertyKey] = new PropertyRow { container = row, field = field, label = label };
        }

        private void CreateScalarField(VisualElement parent, string labelText, System.Action<float> setter)
        {
            var field = new FloatField(labelText);
            field.style.marginBottom = 5;
            field.RegisterValueChangedCallback(evt =>
            {
                if (context.selectedActorIndex >= 0)
                    controller.UpdateActorProperty(context.selectedActorIndex, () => setter(evt.newValue), "Edit Scalar");
            });
            parent.Add(field);
            field.name = labelText;
        }

        #endregion UI Construction Helpers

        private void UpdateUI()
        {
            var actor = context.SelectedActor;
            if (actor == null)
            {
                noSelectionContainer.style.display = DisplayStyle.Flex;
                mainContainer.style.display = DisplayStyle.None;
                // Desvincular campos para evitar mostrar datos obsoletos si no hay selección
                actorNameField.Unbind();
                tagField.Unbind();
                activeToggle.Unbind();
                return;
            }

            noSelectionContainer.style.display = DisplayStyle.None;
            mainContainer.style.display = DisplayStyle.Flex;

            // --- Native Data Binding for direct 1:1 type fields ---
            // Esto es lo del bindeo pero ha sido un poco curioso de implementar (lo ha hecho el agente) ya que he tenido que idear
            // la arquitectura híbrido de las dos cosas (SerializedProperty + manual) para poder usar el sistema de undo automático 
            // de Unity en la medida de lo posible, pero sin que el hecho de que algunos campos no sean directamente bindables (como 
            // el prefab o los vectores) impida que podamos usarlo para los campos simples. La clave ha sido usar un SerializedObject 
            // temporal para hacer el bind de los campos simples, y luego manejar manualmente los campos complejos con callbacks personalizados 
            // donde hacemos el Undo.RecordObject y marcamos dirty manualmente. De esta forma conseguimos lo mejor de ambos mundos: bindeo directo 
            // con undo automático para lo que se puede, y manejo manual con undo para lo que no se puede bindear directamente.
            var so = new SerializedObject(context.currentProject);
            var actorProp = so.FindProperty("actors").GetArrayElementAtIndex(context.selectedActorIndex);
            actorNameField.BindProperty(actorProp.FindPropertyRelative("ActorName"));
            tagField.BindProperty(actorProp.FindPropertyRelative("Tag"));
            activeToggle.BindProperty(actorProp.FindPropertyRelative("Active"));

            // Prefab Loading
            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + (actor.PrefabName ?? ""));
            prefabPicker.SetValueWithoutNotify(prefab);

            // Check Overrides vs Prefab defaults
            UpdateVectorRow("Position", actor.Position, prefab?.transform.position ?? Vector3.zero);
            UpdateVectorRow("Rotation", actor.Rotation, prefab?.transform.eulerAngles ?? Vector3.zero);
            UpdateVectorRow("Scale", actor.Scale, prefab?.transform.localScale ?? Vector3.one);
            UpdateVectorRow("Size", actor.Size, Vector3.zero);
            UpdateVectorRow("Velocity", actor.Velocity, Vector3.zero);
            UpdateVectorRow("AngularVelocity", actor.AngularVelocity, Vector3.zero);

            // Scalars
            this.Q<FloatField>("Density (Mass)")?.SetValueWithoutNotify(actor.Density);
            this.Q<FloatField>("Friction")?.SetValueWithoutNotify(actor.Friction);
            this.Q<FloatField>("Bounciness")?.SetValueWithoutNotify(actor.Bounciness);
            this.Q<FloatField>("Drag")?.SetValueWithoutNotify(actor.Drag);

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
            if (actor?.Properties == null || actor.Properties.Count == 0)
            {
                propertiesContainer.Add(new Label("No custom properties") { style = { color = Color.gray, fontSize = 10, marginTop = 5 } });
                return;
            }

            for (int i = 0; i < actor.Properties.Count; i++)
            {
                int idx = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
                row.Add(new Label(actor.Properties[i]) { style = { flexGrow = 1, fontSize = 11 } });

                var btn = new Button(() => controller.RemoveActorProperty(context.selectedActorIndex, idx)) { text = "X" };
                btn.AddToClassList("button-danger");
                btn.style.width = 20;
                row.Add(btn);

                propertiesContainer.Add(row);
            }
        }

        private void ShowAddPropertyDialog()
        {
            if (context.selectedActorIndex < 0) return;
            var dialog = ScriptableObject.CreateInstance<AddPropertyDialog>();
            dialog.ShowModal(p => { if (!string.IsNullOrEmpty(p)) controller.AddActorProperty(context.selectedActorIndex, p); });
        }
    }

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

            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = position;
            pos.center = main.center;
            position = pos;

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