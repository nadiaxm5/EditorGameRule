using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ConditionBuilder : VisualElement
    {
        private List<string> conditionTypes;

        private List<ConditionElement> elements = new List<ConditionElement>();
        private VisualElement conditionsContainer;
        private VisualElement addConditionRow;
        private PopupField<string> nextOperatorDropdown;
        private Label previewLabel;
        private EditorContext context;
        private bool suppressConditionChanged;

        public System.Action<string> OnConditionChanged;
        public System.Action OnRemoveCondition;

        public ConditionBuilder(EditorContext editorContext)
        {
            context = editorContext;

            conditionTypes = new List<string>();
            MethodInfo[] methods = typeof(global::Condition).GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.ReturnType == typeof(bool))
                {
                    conditionTypes.Add(method.Name);
                }
            }

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
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;

            var label = new Label("WHEN (Conditions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);
            Add(header);

            conditionsContainer = new VisualElement();
            Add(conditionsContainer);

            addConditionRow = new VisualElement();
            addConditionRow.style.flexDirection = FlexDirection.Row;
            addConditionRow.style.justifyContent = Justify.Center;
            addConditionRow.style.alignItems = Align.Center;
            addConditionRow.style.marginTop = 6;

            const string addConditionLabel = "+ Add Condition";
            var addOperators = new List<string> { "AND", "OR" };
            nextOperatorDropdown = new PopupField<string>(addOperators, 0);
            nextOperatorDropdown.SetValueWithoutNotify(addConditionLabel);
            nextOperatorDropdown.AddToClassList("button-condition");
            nextOperatorDropdown.AddToClassList("rule-selector-dropdown");
            nextOperatorDropdown.style.width = 150;
            nextOperatorDropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "AND" || evt.newValue == "OR")
                {
                    string joinOperator = elements.Count > 0 ? evt.newValue : null;
                    AddElement(joinOperator);
                }

                nextOperatorDropdown.SetValueWithoutNotify(addConditionLabel);
            });
            addConditionRow.Add(nextOperatorDropdown);

            Add(addConditionRow);
/*
            var previewContainer = new VisualElement();
            previewContainer.style.marginTop = 10;
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            previewContainer.style.paddingTop = 5;
            previewContainer.style.paddingBottom = 5;
            previewContainer.style.paddingLeft = 5;
            previewContainer.style.paddingRight = 5;

            previewLabel = new Label("");
            previewLabel.style.fontSize = 11;
            previewLabel.style.color = new Color(0.8f, 0.9f, 1f);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewContainer.Add(previewLabel);
            Add(previewContainer); */
        }

        private void AddElement(string joinOperatorBefore = null, string sourceValue = null, bool isNegated = false)
        {
            var element = new ConditionElement(context, conditionTypes, joinOperatorBefore);

            if (!string.IsNullOrEmpty(sourceValue))
            {
                element.SetFromSource(sourceValue);
            }

            element.SetNegated(isNegated);

            element.OnChanged += UpdatePreview;
            element.OnRemove += () => RemoveElement(element);

            elements.Add(element);
            RebuildConditionsLayout();

            // A newly added empty row is only a local selector until the user chooses
            // a condition. Persisting here would immediately rebuild the UI and remove it.
            if (sourceValue != null) UpdatePreview();
        }

        private void RemoveElement(ConditionElement element)
        {
            elements.Remove(element);

            if (elements.Count == 0)
            {
                conditionsContainer.Clear();
                addConditionRow.style.display = DisplayStyle.None;
                OnRemoveCondition?.Invoke();
                return;
            }

            elements[0].SetJoinOperator(null);
            RebuildConditionsLayout();
            UpdatePreview();
        }

        private void RebuildConditionsLayout()
        {
            conditionsContainer.Clear();

            for (int i = 0; i < elements.Count; i++)
            {
                if (i > 0)
                {
                    int idx = i;
                    var connectorRow = new VisualElement();
                    connectorRow.style.flexDirection = FlexDirection.Row;
                    connectorRow.style.justifyContent = Justify.Center;
                    connectorRow.style.alignItems = Align.Center;
                    connectorRow.style.marginTop = 2;
                    connectorRow.style.marginBottom = 2;

                    var connectorOptions = new List<string> { "AND", "OR" };
                    int defaultOp = elements[idx].JoinOperatorBefore == "OR" ? 1 : 0;
                    var connectorDropdown = new PopupField<string>(connectorOptions, defaultOp);
                    connectorDropdown.AddToClassList("button-primary");
                    connectorDropdown.AddToClassList("rule-selector-dropdown");
                    connectorDropdown.style.width = 70;
                    connectorDropdown.RegisterValueChangedCallback(evt =>
                    {
                        elements[idx].SetJoinOperator(evt.newValue);
                        UpdatePreview();
                    });

                    connectorRow.Add(connectorDropdown);
                    conditionsContainer.Add(connectorRow);
                }

                conditionsContainer.Add(elements[i]);
            }

            addConditionRow.style.display = elements.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdatePreview()
        {
            string preview = BuildConditionString();
            if (previewLabel != null)
            {
                previewLabel.text = string.IsNullOrEmpty(preview) ? "(no conditions)" : preview;
            }

            if (suppressConditionChanged)
            {
                return;
            }

            OnConditionChanged?.Invoke(preview);
        }

        private string BuildConditionString()
        {
            if (elements.Count == 0) return "";
            List<string> parts = new List<string>();
            for (int i = 0; i < elements.Count; i++)
            {
                var elem = elements[i];
                string str = elem.GetString();
                if (string.IsNullOrEmpty(str)) continue;

                if (parts.Count > 0)
                {
                    string joinOperator = elem.JoinOperatorBefore;
                    parts.Add(string.IsNullOrEmpty(joinOperator) ? "AND" : joinOperator);
                }

                parts.Add(str);
            }

            return string.Join(" ", parts);
        }

        public void SetCondition(string fullConditionString)
        {
            suppressConditionChanged = true;

            foreach (var el in elements) conditionsContainer.Remove(el);
            elements.Clear();

            if (string.IsNullOrEmpty(fullConditionString))
            {
                AddElement(null, "", false);
                suppressConditionChanged = false;
                return;
            }

            List<string> tokens = GameRuleParser.TokenizeCondition(fullConditionString);
            string pendingJoinOperator = null;
            bool pendingNegation = false;

            foreach (var token in tokens)
            {
                if (token == "AND" || token == "OR")
                {
                    pendingJoinOperator = token;
                    continue;
                }

                if (token == "NOT")
                {
                    pendingNegation = !pendingNegation;
                    continue;
                }

                AddElement(elements.Count > 0 ? pendingJoinOperator : null, token, pendingNegation);
                pendingJoinOperator = null;
                pendingNegation = false;
            }

            if (elements.Count == 0)
            {
                AddElement(null, "", false);
            }

            suppressConditionChanged = false;
            RebuildConditionsLayout();
            UpdatePreview();
        }
    }
}
