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

            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems = Align.Center;

            var label = new Label("WHEN (Conditions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelRow.Add(label);

            var removeConditionBtn = new Button(() => OnRemoveCondition?.Invoke()) { text = string.Empty };
            removeConditionBtn.AddToClassList("button-danger");
            removeConditionBtn.style.width = 22;
            removeConditionBtn.style.height = 20;
            removeConditionBtn.style.marginLeft = 8;
            removeConditionBtn.style.fontSize = 10;

            var trashImage = new Image();
            trashImage.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
            trashImage.style.width = 12;
            trashImage.style.height = 12;
            trashImage.style.alignSelf = Align.Center;
            trashImage.style.unityBackgroundImageTintColor = Color.white;
            removeConditionBtn.Add(trashImage);

            labelRow.Add(removeConditionBtn);

            header.Add(labelRow);
            Add(header);

            conditionsContainer = new VisualElement();
            Add(conditionsContainer);

            addConditionRow = new VisualElement();
            addConditionRow.style.flexDirection = FlexDirection.Row;
            addConditionRow.style.justifyContent = Justify.Center;
            addConditionRow.style.alignItems = Align.Center;
            addConditionRow.style.marginTop = 6;

            var addOperators = new List<string> { "+", "AND", "OR" };
            nextOperatorDropdown = new PopupField<string>(addOperators, 0);
            nextOperatorDropdown.style.width = 78;
            nextOperatorDropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "AND" || evt.newValue == "OR")
                {
                    string joinOperator = elements.Count > 0 ? evt.newValue : null;
                    AddElement(joinOperator);
                }

                nextOperatorDropdown.SetValueWithoutNotify(addOperators[0]);
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

            UpdatePreview();
        }

        private void RemoveElement(ConditionElement element)
        {
            elements.Remove(element);
            if (elements.Count > 0)
            {
                elements[0].SetJoinOperator(null);
            }

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
                if (i > 0)
                {
                    string joinOperator = elem.JoinOperatorBefore;
                    parts.Add(string.IsNullOrEmpty(joinOperator) ? "AND" : joinOperator);
                }

                string str = elem.GetString();
                if (!string.IsNullOrEmpty(str)) parts.Add(str);
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