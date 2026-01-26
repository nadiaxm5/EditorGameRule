using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ConditionElement : VisualElement
    {
        private EditorContext context;
        private PopupField<string> typeDropdown;
        private Label operatorLabel;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<TextField> parameterFields = new List<TextField>();

        public System.Action OnChanged;
        public System.Action OnRemove;

        private bool isOperator = false;

        public ConditionElement(EditorContext ctx, List<string> conditionTypes, string specificType = null)
        {
            context = ctx;
            availableTypes = conditionTypes;

            style.flexDirection = FlexDirection.Row;
            style.marginBottom = 5;
            style.borderTopLeftRadius = 3; style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3; style.borderBottomRightRadius = 3;
            style.paddingTop = 5; style.paddingBottom = 5;
            style.paddingLeft = 5; style.paddingRight = 5;
            style.alignItems = Align.Center;

            CreateUI(specificType);
        }

        private void CreateUI(string specificType)
        {
            isOperator = (specificType == "AND" || specificType == "OR" || specificType == "NOT");

            if (isOperator)
            {
                style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                style.borderLeftColor = new Color(0.8f, 0.6f, 0.2f);
                style.borderLeftWidth = 3;

                operatorLabel = new Label(specificType);
                operatorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                operatorLabel.style.fontSize = 12;
                operatorLabel.style.color = new Color(1f, 0.9f, 0.7f);
                operatorLabel.style.width = 100;
                operatorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                Add(operatorLabel);
            }
            else
            {
                style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);

                typeDropdown = new PopupField<string>(availableTypes, 0);
                typeDropdown.style.width = 100;

                if (!string.IsNullOrEmpty(specificType) && availableTypes.Contains(specificType))
                {
                    typeDropdown.SetValueWithoutNotify(specificType);
                }

                typeDropdown.RegisterValueChangedCallback(evt =>
                {
                    UpdateParameterFields(true);
                    OnChanged?.Invoke();
                });
                Add(typeDropdown);

                parametersContainer = new VisualElement();
                parametersContainer.style.flexDirection = FlexDirection.Row;
                parametersContainer.style.flexGrow = 1;
                Add(parametersContainer);

                UpdateParameterFields(false);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = "X" };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 25;
            removeBtn.style.height = 20;
            Add(removeBtn);
        }

        public void SetFromSource(string token)
        {
            if (isOperator) return;

            var result = GameRuleParser.ParseFunction(token);

            if (availableTypes.Contains(result.Name))
            {
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateParameterFields(false);

                for (int i = 0; i < parameterFields.Count && i < result.Params.Count; i++)
                {
                    parameterFields[i].SetValueWithoutNotify(result.Params[i]);
                }
            }
        }

        private void UpdateParameterFields(bool notifyChange = true)
        {
            if (isOperator) return;

            parametersContainer.Clear();
            parameterFields.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Compare": AddParameterField("Expression", true); break; // Can pick properties
                case "Check": AddParameterField("Variable", true); break;     // Can pick properties
                case "Collision": AddParameterField("Tag"); break;            // Just text
                case "Timer": AddParameterField("Seconds"); break;            // Number
                case "Touch": AddParameterField("Mode"); AddParameterField("OnActor (true/false)"); break;
                case "Keyboard": AddParameterField("Key"); AddParameterField("Mode"); break;
            }

            if (notifyChange) OnChanged?.Invoke();
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
            label.style.fontSize = 9;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.position = Position.Absolute;
            label.style.left = 3; label.style.top = 2;
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
                        // Append picking logic for "Compare" could be complex (e.g. inserting at cursor),
                        // but simple replacement is safer for now.
                        // For "Compare", users often want "health > 0", so just inserting "health" might replace everything.
                        // For V1, let's just Append or Replace. Let's Append if not empty to be helpful.
                        if (!string.IsNullOrEmpty(field.value))
                            field.value += picked;
                        else
                            field.value = picked;

                        OnChanged?.Invoke();
                    });
                });
                pickBtn.text = "°";
                pickBtn.style.width = 18;
                pickBtn.style.height = 18;
                pickBtn.style.marginLeft = 1;
                pickBtn.style.fontSize = 10;
                container.Add(pickBtn);
            }

            parametersContainer.Add(container);
            parameterFields.Add(field);
        }

        public string GetString()
        {
            if (isOperator) return operatorLabel.text;

            string type = typeDropdown.value;
            List<string> parameters = new List<string>();
            foreach (var field in parameterFields) parameters.Add(field.value);

            return $"{type}({string.Join(",", parameters)})";
        }
    }
}