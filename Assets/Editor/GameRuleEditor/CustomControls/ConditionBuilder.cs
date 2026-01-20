using UnityEngine;
using UnityEngine.UIElements;
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

        private List<ConditionElement> elements = new List<ConditionElement>();
        private VisualElement conditionsContainer;
        private Label previewLabel;
        public System.Action<string> OnConditionChanged;

        public ConditionBuilder()
        {
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderTopLeftRadius = 5; style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5; style.borderBottomRightRadius = 5;
            // Fixed padding syntax
            style.paddingTop = 10; style.paddingBottom = 10;
            style.paddingLeft = 10; style.paddingRight = 10;

            CreateUI();
        }

        private void CreateUI()
        {
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;

            var label = new Label("WHEN (Conditions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            // Buttons Container
            var btnContainer = new VisualElement();
            btnContainer.style.flexDirection = FlexDirection.Row;

            // Logic Buttons
            CreateHeaderButton(btnContainer, "+ NOT", "button-primary", () => AddElement("NOT"));
            CreateHeaderButton(btnContainer, "+ AND", "button-primary", () => AddElement("AND"));
            CreateHeaderButton(btnContainer, "+ OR", "button-primary", () => AddElement("OR"));

            var spacer = new VisualElement();
            spacer.style.width = 10;
            btnContainer.Add(spacer);

            // Condition Button
            CreateHeaderButton(btnContainer, "+ Condition", "button-primary", () => AddElement(null));

            header.Add(btnContainer);
            Add(header);

            // Container
            conditionsContainer = new VisualElement();
            Add(conditionsContainer);

            // Preview
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
            Add(previewContainer);
        }

        private void CreateHeaderButton(VisualElement container, string text, string className, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList(className);
            btn.style.height = 20;
            btn.style.fontSize = 10;
            btn.style.marginRight = 2;
            container.Add(btn);
        }

        private void AddElement(string specificType, string sourceValue = null)
        {
            var element = new ConditionElement(conditionTypes, specificType);

            if (!string.IsNullOrEmpty(sourceValue))
            {
                element.SetFromSource(sourceValue);
            }

            element.OnChanged += UpdatePreview;
            element.OnRemove += () => RemoveElement(element);

            elements.Add(element);
            conditionsContainer.Add(element);

            UpdatePreview();
        }

        private void RemoveElement(ConditionElement element)
        {
            elements.Remove(element);
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
            if (elements.Count == 0) return "";

            List<string> parts = new List<string>();
            foreach (var elem in elements)
            {
                string str = elem.GetString();
                if (!string.IsNullOrEmpty(str)) parts.Add(str);
            }

            return string.Join(" ", parts);
        }

        public void SetCondition(string fullConditionString)
        {
            foreach (var el in elements) conditionsContainer.Remove(el);
            elements.Clear();

            // FIX: If string is empty, we add a default empty condition row
            // This happens when you create a new rule or manually clear the conditions.
            if (string.IsNullOrEmpty(fullConditionString))
            {
                AddElement(null, "");
                UpdatePreview();
                return;
            }

            // Tokenize and build
            List<string> tokens = GameRuleParser.TokenizeCondition(fullConditionString);

            foreach (var token in tokens)
            {
                if (token == "AND" || token == "OR" || token == "NOT")
                {
                    AddElement(token);
                }
                else
                {
                    AddElement(null, token);
                }
            }

            UpdatePreview();
        }
    }
}