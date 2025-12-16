using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace GameRuleEditor.CustomControls
{
    /// <summary>
    /// Visual control for building GameRule conditions
    /// </summary>
    public class ConditionBuilder : VisualElement
    {
        private List<string> conditionTypes = new List<string>
        {
            "Compare",
            "Check",
            "Collision",
            "Timer",
            "Touch",
            "Keyboard"
        };

        private List<ConditionElement> conditions = new List<ConditionElement>();
        private VisualElement conditionsContainer;
        private Label previewLabel;

        public System.Action<string> OnConditionChanged;

        public ConditionBuilder()
        {
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderTopLeftRadius = 5;
            style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5;
            style.borderBottomRightRadius = 5;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            CreateUI();
        }

        private void CreateUI()
        {
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 10;

            var label = new Label("WHEN (Conditions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            var addButton = new Button(() => AddCondition());
            addButton.text = "+ Add Condition";
            addButton.AddToClassList("button-primary");
            addButton.style.height = 25;
            header.Add(addButton);

            Add(header);

            // Conditions container
            conditionsContainer = new VisualElement();
            Add(conditionsContainer);

            // Preview
            var previewContainer = new VisualElement();
            previewContainer.style.marginTop = 10;
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            previewContainer.style.borderTopLeftRadius = 3;
            previewContainer.style.borderTopRightRadius = 3;
            previewContainer.style.borderBottomLeftRadius = 3;
            previewContainer.style.borderBottomRightRadius = 3;
            previewContainer.style.paddingTop = 8;
            previewContainer.style.paddingBottom = 8;
            previewContainer.style.paddingLeft = 8;
            previewContainer.style.paddingRight = 8;

            var previewTitle = new Label("Preview:");
            previewTitle.style.fontSize = 10;
            previewTitle.style.color = new Color(0.7f, 0.7f, 0.7f);
            previewContainer.Add(previewTitle);

            previewLabel = new Label("");
            previewLabel.style.fontSize = 11;
            previewLabel.style.color = new Color(0.8f, 0.9f, 1f);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewContainer.Add(previewLabel);

            Add(previewContainer);
        }

        private void AddCondition()
        {
            var conditionElement = new ConditionElement(conditionTypes);
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
            if (conditions.Count == 0)
                return "";

            List<string> parts = new List<string>();

            for (int i = 0; i < conditions.Count; i++)
            {
                string conditionStr = conditions[i].GetConditionString();
                if (string.IsNullOrEmpty(conditionStr))
                    continue;

                if (i > 0)
                {
                    string logicalOp = conditions[i].GetLogicalOperator();
                    parts.Add($" {logicalOp} ");
                }

                parts.Add(conditionStr);
            }

            return string.Join("", parts);
        }

        public void SetCondition(string conditionString)
        {
            // Clear existing
            foreach (var cond in conditions)
            {
                conditionsContainer.Remove(cond);
            }
            conditions.Clear();

            if (string.IsNullOrEmpty(conditionString))
            {
                UpdatePreview();
                return;
            }

            // Parse the condition string (simple parsing)
            // For now, just add one condition with the full string
            // TODO: Implement proper parsing
            var conditionElement = new ConditionElement(conditionTypes);
            conditionElement.OnChanged += UpdatePreview;
            conditionElement.OnRemove += () => RemoveCondition(conditionElement);

            conditions.Add(conditionElement);
            conditionsContainer.Add(conditionElement);

            UpdatePreview();
        }
    }

    /// <summary>
    /// Individual condition element
    /// </summary>
    public class ConditionElement : VisualElement
    {
        private PopupField<string> typeDropdown;
        private PopupField<string> logicalOpDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;

        public System.Action OnChanged;
        public System.Action OnRemove;

        private List<TextField> parameterFields = new List<TextField>();

        public ConditionElement(List<string> conditionTypes)
        {
            availableTypes = conditionTypes;

            style.flexDirection = FlexDirection.Row;
            style.marginBottom = 5;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;
            style.paddingTop = 5;
            style.paddingBottom = 5;
            style.paddingLeft = 5;
            style.paddingRight = 5;

            CreateUI();
        }

        private void CreateUI()
        {
            // Logical operator (AND, OR, NOT)
            logicalOpDropdown = new PopupField<string>(
                new List<string> { "AND", "OR", "NOT" },
                0
            );
            logicalOpDropdown.style.width = 60;
            logicalOpDropdown.style.marginRight = 5;
            logicalOpDropdown.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            Add(logicalOpDropdown);

            // Condition type dropdown
            typeDropdown = new PopupField<string>(availableTypes, 0);
            typeDropdown.style.width = 120;
            typeDropdown.style.marginRight = 5;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                UpdateParameterFields();
                OnChanged?.Invoke();
            });
            Add(typeDropdown);

            // Parameters container
            parametersContainer = new VisualElement();
            parametersContainer.style.flexDirection = FlexDirection.Row;
            parametersContainer.style.flexGrow = 1;
            Add(parametersContainer);

            // Remove button
            var removeBtn = new Button(() => OnRemove?.Invoke());
            removeBtn.text = "X";
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 25;
            removeBtn.style.height = 25;
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
                case "Compare":
                    AddParameterField("Expression (e.g., Health > 0)");
                    break;

                case "Check":
                    AddParameterField("Variable (e.g., Active)");
                    break;

                case "Collision":
                    AddParameterField("Tag (e.g., Enemy)");
                    break;

                case "Timer":
                    AddParameterField("Seconds");
                    break;

                case "Touch":
                    AddParameterField("Mode (press/down/up/tap)");
                    AddParameterField("On Actor? (true/false)");
                    break;

                case "Keyboard":
                    AddParameterField("Key (e.g., W)");
                    AddParameterField("Mode (press/down/up)");
                    break;
            }
        }

        private void AddParameterField(string placeholder)
        {
            var field = new TextField();
            field.style.flexGrow = 1;
            field.style.marginRight = 3;
            field.SetValueWithoutNotify("");

            // Use placeholder as label
            var label = new Label(placeholder);
            label.style.fontSize = 9;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.position = Position.Absolute;
            label.style.left = 5;
            label.style.top = 3;
            field.Add(label);

            field.RegisterValueChangedCallback(evt =>
            {
                label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                OnChanged?.Invoke();
            });

            parametersContainer.Add(field);
            parameterFields.Add(field);
        }

        public string GetLogicalOperator()
        {
            return logicalOpDropdown.value;
        }

        public string GetConditionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();

            foreach (var field in parameterFields)
            {
                parameters.Add(field.value);
            }

            // Build condition string
            if (parameters.Count == 0 || string.IsNullOrEmpty(parameters[0]))
                return "";

            string paramsStr = string.Join(",", parameters);
            return $"{type}({paramsStr})";
        }
    }
}