using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ConditionBuilder : VisualElement
    {
        private List<string> conditionTypes = new List<string>
        {
            "Compare", "Check", "Collision", "Timer", "Touch", "Keyboard"
        };

        private List<ConditionElement> conditions = new List<ConditionElement>();
        private VisualElement conditionsContainer;
        private Label previewLabel;
        public System.Action<string> OnConditionChanged;

        public ConditionBuilder()
        {
            // Styling matches original
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderTopLeftRadius = 5; style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5; style.borderBottomRightRadius = 5;
            style.paddingTop = 10; style.paddingBottom = 10;
            style.paddingLeft = 10; style.paddingRight = 10;
            CreateUI();
        }

        private void CreateUI()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 10;

            var label = new Label("WHEN (Conditions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            var addButton = new Button(() => AddCondition(null, ""));
            addButton.text = "+ Add Condition";
            addButton.AddToClassList("button-primary");
            addButton.style.height = 25;
            header.Add(addButton);

            Add(header);
            conditionsContainer = new VisualElement();
            Add(conditionsContainer);

            // Preview
            var previewContainer = new VisualElement();
            previewContainer.style.marginTop = 10;
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            previewLabel = new Label("");
            previewLabel.style.fontSize = 11;
            previewLabel.style.color = new Color(0.8f, 0.9f, 1f);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewContainer.Add(previewLabel);
            Add(previewContainer);
        }

        private void AddCondition(string conditionStr, string operatorStr)
        {
            var conditionElement = new ConditionElement(conditionTypes);

            // Set operator for elements after the first one
            conditionElement.SetOperator(string.IsNullOrEmpty(operatorStr) ? "AND" : operatorStr);

            // Hide operator selector for the very first element
            if (conditions.Count == 0) conditionElement.HideOperator();

            if (!string.IsNullOrEmpty(conditionStr))
            {
                conditionElement.SetFromSource(conditionStr);
            }

            conditionElement.OnChanged += UpdatePreview;
            conditionElement.OnRemove += () => RemoveCondition(conditionElement);

            conditions.Add(conditionElement);
            conditionsContainer.Add(conditionElement);

            UpdatePreview();
        }

        private void RemoveCondition(ConditionElement element)
        {
            conditions.Remove(element);
            conditionsContainer.Remove(element);

            // Ensure first element hides operator
            if (conditions.Count > 0) conditions[0].HideOperator();

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string preview = BuildConditionString();
            previewLabel.text = string.IsNullOrEmpty(preview) ? "(no conditions)" : preview;
            OnConditionChanged?.Invoke(preview);
        }

        private string BuildConditionString()
        {
            if (conditions.Count == 0) return "";
            List<string> parts = new List<string>();

            for (int i = 0; i < conditions.Count; i++)
            {
                string conditionStr = conditions[i].GetConditionString();
                if (string.IsNullOrEmpty(conditionStr)) continue;

                if (i > 0)
                {
                    parts.Add($" {conditions[i].GetLogicalOperator()} ");
                }
                parts.Add(conditionStr);
            }
            return string.Join("", parts);
        }

        public void SetCondition(string fullConditionString)
        {
            foreach (var cond in conditions)
            {
                conditionsContainer.Remove(cond);
            }
            conditions.Clear();

            if (string.IsNullOrEmpty(fullConditionString))
            {
                AddCondition(null, "");
                UpdatePreview();
                return;
            }

            // Split by logical operators using our new parser
            var parts = GameRuleParser.SplitConditions(fullConditionString);

            foreach (var part in parts)
            {
                AddCondition(part.condition, part.op);
            }

            UpdatePreview();
        }
    }

    public class ConditionElement : VisualElement
    {
        private PopupField<string> typeDropdown;
        private PopupField<string> logicalOpDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<TextField> parameterFields = new List<TextField>();

        public System.Action OnChanged;
        public System.Action OnRemove;

        public ConditionElement(List<string> conditionTypes)
        {
            availableTypes = conditionTypes;
            style.flexDirection = FlexDirection.Row;
            style.marginBottom = 5;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            style.paddingTop = 5; style.paddingBottom = 5;
            CreateUI();
        }

        public void SetFromSource(string conditionString)
        {
            var result = GameRuleParser.ParseFunction(conditionString);

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
            parametersContainer.Clear();
            parameterFields.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Compare": AddParameterField("Expression"); break;
                case "Check": AddParameterField("Variable"); break;
                case "Collision": AddParameterField("Tag"); break;
                case "Timer": AddParameterField("Seconds"); break;
                case "Touch": AddParameterField("Mode"); AddParameterField("OnActor (true/false)"); break;
                case "Keyboard": AddParameterField("Key"); AddParameterField("Mode"); break;
            }

            if (notifyChange) OnChanged?.Invoke();
        }

        public void HideOperator()
        { logicalOpDropdown.style.display = DisplayStyle.None; }

        public void SetOperator(string op)
        { logicalOpDropdown.value = op; }

        public string GetLogicalOperator()
        { return logicalOpDropdown.value; }

        private void CreateUI()
        {
            logicalOpDropdown = new PopupField<string>(new List<string> { "AND", "OR" }, 0);
            logicalOpDropdown.style.width = 60;
            logicalOpDropdown.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            Add(logicalOpDropdown);

            typeDropdown = new PopupField<string>(availableTypes, 0);
            typeDropdown.style.width = 100;
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

            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = "X" };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 20;
            Add(removeBtn);

            UpdateParameterFields();
        }

        private void UpdateParameterFields()
        {
            parametersContainer.Clear();
            parameterFields.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Compare": AddParameterField("Expression"); break;
                case "Check": AddParameterField("Variable"); break;
                case "Collision": AddParameterField("Tag"); break;
                case "Timer": AddParameterField("Seconds"); break;
                case "Touch": AddParameterField("Mode"); AddParameterField("OnActor (true/false)"); break;
                case "Keyboard": AddParameterField("Key"); AddParameterField("Mode"); break;
            }
        }

        private void AddParameterField(string placeholder)
        {
            var field = new TextField();
            field.style.flexGrow = 1;
            field.style.marginRight = 3;
            field.RegisterValueChangedCallback(evt => OnChanged?.Invoke());

            // Simple placeholder logic
            var label = new Label(placeholder);
            label.style.fontSize = 8;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.position = Position.Absolute;
            label.style.left = 3; label.style.top = 2;
            field.Add(label);
            field.RegisterValueChangedCallback(evt => label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None);

            parametersContainer.Add(field);
            parameterFields.Add(field);
        }

        public string GetConditionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();
            foreach (var field in parameterFields) parameters.Add(field.value);

            if (parameters.Count == 0 || string.IsNullOrEmpty(parameters[0])) return "";
            return $"{type}({string.Join(",", parameters)})";
        }
    }
}