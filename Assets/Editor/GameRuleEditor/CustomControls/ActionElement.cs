using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ActionElement : VisualElement
    {
        private EditorContext context;
        private PopupField<string> typeDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<VisualElement> inputElements = new List<VisualElement>();

        public System.Action OnChanged;
        public System.Action OnRemove;
        public System.Action OnMoveUp;
        public System.Action OnMoveDown;

        public ActionElement(EditorContext ctx, List<string> actionTypes)
        {
            context = ctx;
            availableTypes = actionTypes;
            style.marginBottom = 5;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            style.paddingTop = 5; style.paddingBottom = 5;
            style.paddingLeft = 5; style.paddingRight = 5;
            CreateUI();
        }

        public void SetFromSource(string actionString)
        {
            var result = GameRuleParser.ParseFunction(actionString);
            if (availableTypes.Contains(result.Name))
            {
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateParameterFields();

                for (int i = 0; i < inputElements.Count && i < result.Params.Count; i++)
                {
                    if (inputElements[i] is TextField tf)
                    {
                        tf.SetValueWithoutNotify(result.Params[i]);
                    }
                }
            }
        }

        private void CreateUI()
        {
            var mainRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            typeDropdown = new PopupField<string>(availableTypes, 0) { style = { width = 110, marginRight = 5 } };
            typeDropdown.RegisterValueChangedCallback(evt => { UpdateParameterFields(); OnChanged?.Invoke(); });
            mainRow.Add(typeDropdown);

            parametersContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, flexWrap = Wrap.Wrap } };
            mainRow.Add(parametersContainer);

            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = "X" };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 20; removeBtn.style.height = 20;
            mainRow.Add(removeBtn);

            Add(mainRow);
            UpdateParameterFields();
        }

        private void UpdateParameterFields()
        {
            parametersContainer.Clear();
            inputElements.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Edit":
                    AddParameterField("Property", true); AddParameterField("Value", true); break;
                case "Spawn":
                    AddParameterField("Prefab", true, false, true);
                    AddParameterField("Spawner", true, false, true);
                    AddParameterField("Pos X", true); AddParameterField("Pos Y", true); AddParameterField("Pos Z", true);
                    AddParameterField("Rot X", true); AddParameterField("Rot Y", true); AddParameterField("Rot Z", true);
                    break;

                case "Animate": AddResourceField<AnimationClip>("Animation Name"); break;
                case "PlaySound": AddResourceField<AudioClip>("Sound Name"); break;
                case "PlayParticles": AddResourceField<ParticleSystem>("Particle Prefab"); break;

                case "Move": AddParameterField("Speed", true); AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "MoveTo": AddParameterField("Speed", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;
                case "NavigateTo": AddParameterField("Speed", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;

                case "Rotate": AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "RotateTo": AddParameterField("Speed", true); AddParameterField("DX", true); AddParameterField("DY", true); AddParameterField("DZ", true); AddParameterField("PivotX", true); AddParameterField("PivotY", true); AddParameterField("PivotZ", true); break;
                case "Torque": AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;

                case "Push": AddParameterField("Force", true); AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "PushTo": AddParameterField("Force", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;
            }
        }

        // Standard Text + Picker Button
        private void AddParameterField(string placeholder, bool showPicker = false, bool boolOnly = false, bool actorsOnly = false)
        {
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 40 } };
            var field = new TextField() { style = { flexGrow = 1 } };
            var label = new Label(placeholder) { style = { fontSize = 8, color = new Color(0.6f, 0.6f, 0.6f), position = Position.Absolute, left = 2, top = 2 }, pickingMode = PickingMode.Ignore };
            field.Add(label);
            field.RegisterValueChangedCallback(evt => { label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None; OnChanged?.Invoke(); });
            container.Add(field);

            if (showPicker)
            {
                var pickBtn = new Button(() =>
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) => { field.value = picked; OnChanged?.Invoke(); }, boolOnly, actorsOnly);
                })
                { text = "°", style = { width = 18, height = 18, fontSize = 10, marginLeft = 0 } };
                container.Add(pickBtn);
            }
            parametersContainer.Add(container); inputElements.Add(field);
        }

        // [Updated] Now looks identical to AddParameterField but picks resources
        private void AddResourceField<T>(string placeholder) where T : Object
        {
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 120 } };

            var textField = new TextField() { style = { flexGrow = 1 } };
            var label = new Label(placeholder) { style = { fontSize = 8, color = new Color(0.6f, 0.6f, 0.6f), position = Position.Absolute, left = 2, top = 2 }, pickingMode = PickingMode.Ignore };
            textField.Add(label);
            textField.RegisterValueChangedCallback(evt =>
            {
                label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                OnChanged?.Invoke();
            });
            container.Add(textField);

            // Use exact same button style as AddParameterField
            var pickBtn = new Button(() =>
            {
                // Call PropertyPickerDialog with resource type filter
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (name) =>
                {
                    textField.value = name;
                    OnChanged?.Invoke();
                }, resourceFilter: typeof(T));
            })
            { text = "°", style = { width = 18, height = 18, fontSize = 10, marginLeft = 0 } };

            container.Add(pickBtn);
            parametersContainer.Add(container);
            inputElements.Add(textField);
        }

        public string GetActionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();
            foreach (var el in inputElements)
            {
                if (el is TextField tf) parameters.Add(tf.value);
            }
            if (parameters.Count == 0) return $"{type}()";
            return $"{type}({string.Join(",", parameters)})";
        }
    }
}