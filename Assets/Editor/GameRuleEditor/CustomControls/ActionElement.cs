using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ActionElement : VisualElement
    {
        private EditorContext context; // New Context
        private PopupField<string> typeDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<TextField> parameterFields = new List<TextField>();

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

                for (int i = 0; i < parameterFields.Count && i < result.Params.Count; i++)
                {
                    parameterFields[i].SetValueWithoutNotify(result.Params[i]);
                }
            }
        }

        private void CreateUI()
        {
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;

            // Move buttons
            var moveButtons = new VisualElement();
            moveButtons.style.flexDirection = FlexDirection.Column;
            moveButtons.style.marginRight = 5;
            var upBtn = new Button(() => OnMoveUp?.Invoke()) { text = "^" };
            var downBtn = new Button(() => OnMoveDown?.Invoke()) { text = "v" };
            upBtn.style.width = 20; upBtn.style.height = 15; upBtn.style.fontSize = 8;
            downBtn.style.width = 20; downBtn.style.height = 15; downBtn.style.fontSize = 8;
            moveButtons.Add(upBtn); moveButtons.Add(downBtn);
            mainRow.Add(moveButtons);

            typeDropdown = new PopupField<string>(availableTypes, 0);
            typeDropdown.style.width = 120;
            typeDropdown.style.marginRight = 5;
            typeDropdown.RegisterValueChangedCallback(evt => { UpdateParameterFields(); OnChanged?.Invoke(); });
            mainRow.Add(typeDropdown);

            parametersContainer = new VisualElement();
            parametersContainer.style.flexDirection = FlexDirection.Row;
            parametersContainer.style.flexGrow = 1;
            mainRow.Add(parametersContainer);

            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = "X" };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 25; removeBtn.style.height = 25;
            mainRow.Add(removeBtn);

            Add(mainRow);
            UpdateParameterFields();
        }

        private void UpdateParameterFields()
        {
            parametersContainer.Clear();
            parameterFields.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Edit":
                    AddParameterField("Property", true); // Picker!
                    AddParameterField("Value", true);    // Picker (read values)
                    break;

                case "Spawn":
                    AddParameterField("Prefab");
                    AddParameterField("Spawner");
                    AddParameterField("Pos X"); AddParameterField("Pos Y"); AddParameterField("Pos Z");
                    AddParameterField("Rot X"); AddParameterField("Rot Y"); AddParameterField("Rot Z");
                    break;

                case "Animate":
                case "PlaySound":
                case "PlayParticles":
                    AddParameterField("Name"); break;
                case "Move":
                case "Push":
                    AddParameterField("Value", true);
                    AddParameterField("RX"); AddParameterField("RY"); AddParameterField("RZ"); break;
                case "MoveTo":
                case "NavigateTo":
                case "PushTo":
                    AddParameterField("Value", true);
                    AddParameterField("X", true); // Picker target
                    AddParameterField("Y", true);
                    AddParameterField("Z", true);
                    break;

                case "Rotate":
                case "Torque":
                    AddParameterField("RX"); AddParameterField("RY"); AddParameterField("RZ"); break;
                case "RotateTo":
                    AddParameterField("Speed"); AddParameterField("DX"); AddParameterField("DY"); AddParameterField("DZ");
                    AddParameterField("PivotX"); AddParameterField("PivotY"); AddParameterField("PivotZ"); break;
            }
        }

        private void AddParameterField(string placeholder, bool showPicker = false)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            container.style.marginRight = 3;
            container.style.minWidth = 50;

            var field = new TextField();
            field.style.flexGrow = 1;

            var label = new Label(placeholder);
            label.style.fontSize = 8;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.position = Position.Absolute;
            label.style.left = 2; label.style.top = 2;
            field.Add(label);

            field.RegisterValueChangedCallback(evt =>
            {
                label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                OnChanged?.Invoke();
            });

            container.Add(field);

            if (showPicker)
            {
                var pickBtn = new Button(() =>
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) =>
                    {
                        field.value = picked;
                        OnChanged?.Invoke();
                    });
                });
                pickBtn.text = "°"; // Small icon-like button
                pickBtn.style.width = 18;
                pickBtn.style.height = 18;
                pickBtn.style.marginLeft = 1;
                pickBtn.style.fontSize = 10;
                container.Add(pickBtn);
            }

            parametersContainer.Add(container);
            parameterFields.Add(field);
        }

        public string GetActionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();
            foreach (var field in parameterFields) parameters.Add(field.value);

            if (parameters.Count == 0) return $"{type}()";
            return $"{type}({string.Join(",", parameters)})";
        }
    }
}