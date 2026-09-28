using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Text.RegularExpressions;

namespace GameRuleEditor.CustomControls
{
    public class ConditionElement : VisualElement
    {
        private const string SelectConditionLabel = "Select condition";

        private EditorContext context;
        private PopupField<string> typeDropdown;
        private Button negationButton;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<VisualElement> inputElements = new List<VisualElement>();
        public System.Action OnChanged;
        public System.Action OnRemove;
        private bool isNegated;
        private string selectedConditionType;
        private string joinOperatorBefore;

        private static readonly Dictionary<string, string> EventDisplayNames = new Dictionary<string, string>
        {
            { "press", "Held" },
            { "down", "Pressed" },
            { "up", "Released" },
            { "tap", "Tapped" },
            { "isOver", "Pointer Over" }
        };

        public string JoinOperatorBefore => joinOperatorBefore;

        public ConditionElement(EditorContext ctx, List<string> conditionTypes, string joinOperatorBefore = null)
        {
            context = ctx;
            availableTypes = conditionTypes;
            style.flexDirection = FlexDirection.Row; style.marginBottom = 5;
            style.flexShrink = 0;
            style.borderTopLeftRadius = 3; style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3; style.borderBottomRightRadius = 3;
            style.paddingTop = 5; style.paddingBottom = 5; style.paddingLeft = 5; style.paddingRight = 5;
            style.alignItems = Align.Center;
            CreateUI(joinOperatorBefore);
        }

        private void CreateUI(string joinOperatorBefore)
        {
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            SetJoinOperator(joinOperatorBefore);

            negationButton = new Button(() =>
            {
                if (string.IsNullOrEmpty(selectedConditionType)) return;

                isNegated = !isNegated;
                UpdateNegationVisual();
                OnChanged?.Invoke();
            })
            {
                text = "NOT",
                tooltip = "Invert this condition"
            };
            negationButton.AddToClassList("button-negation");
            Add(negationButton);

            selectedConditionType = null;
            typeDropdown = new PopupField<string>(availableTypes, 0) { style = { width = 130 } };
            typeDropdown.SetValueWithoutNotify(SelectConditionLabel);
            typeDropdown.AddToClassList("button-condition");
            typeDropdown.AddToClassList("rule-selector-dropdown");
            typeDropdown.style.flexShrink = 0;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (!availableTypes.Contains(evt.newValue)) return;

                selectedConditionType = evt.newValue;
                UpdateNegationVisual();
                UpdateParameterFields(true);
            });
            Add(typeDropdown);

            parametersContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, alignItems = Align.Center } };
            parametersContainer.style.flexShrink = 0;
            Add(parametersContainer);

            Add(new VisualElement() { style = { flexGrow = 1 } });
            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = string.Empty };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 28;
            removeBtn.style.height = 26;

            var trashImage = new Image();
            trashImage.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
            trashImage.style.width = 16;
            trashImage.style.height = 16;
            trashImage.style.alignSelf = Align.Center;
            trashImage.style.unityBackgroundImageTintColor = Color.white;
            removeBtn.Add(trashImage);

            Add(removeBtn);

            UpdateNegationVisual();
            UpdateParameterFields(false);
        }

        public void SetJoinOperator(string joinOperator)
        {
            joinOperatorBefore = string.IsNullOrEmpty(joinOperator) ? null : joinOperator;
        }

        public void SetNegated(bool negated)
        {
            isNegated = negated;
            UpdateNegationVisual();
        }

        private void UpdateNegationVisual()
        {
            bool hasCondition = !string.IsNullOrEmpty(selectedConditionType);
            bool showAsNegated = hasCondition && isNegated;

            negationButton.SetEnabled(hasCondition);
            negationButton.EnableInClassList("button-negation--active", showAsNegated);
            EnableInClassList("condition-negated", showAsNegated);
        }

        public void SetFromSource(string token)
        {
            var result = GameRuleParser.ParseFunction(token);
            if (availableTypes.Contains(result.Name))
            {
                selectedConditionType = result.Name;
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateNegationVisual();
                UpdateParameterFields(false);
                FillFieldsFromParams(result.Name, result.Params);
            }
        }

        private void UpdateParameterFields(bool notifyChange = true)
        {
            parametersContainer.Clear();
            inputElements.Clear();
            string type = selectedConditionType;

            switch (type)
            {
                case "Compare":
                    AddParameterField("Property 1", true, false);
                    var operators = new List<string> { "<", "<=", "==", "!=", ">=", ">" };
                    var opDropdown = new PopupField<string>(operators, 0) { style = { width = 45 } };
                    opDropdown.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(opDropdown); inputElements.Add(opDropdown);
                    AddParameterField("Property 2", true, false);
                    break;

                case "Check": AddParameterField("Boolean Property", true, true); break;

                case "Collision":
                    var projectTags = Loader.GetProjectTags(context?.currentProject?.actors);

                    parametersContainer.Add(CreateFieldTag("Tag"));
                    var tagDrop = new PopupField<string>(projectTags, 0) { style = { flexGrow = 1 } };
                    tagDrop.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tagDrop); inputElements.Add(tagDrop);
                    break;

                case "Timer": AddParameterField("Seconds"); break;

                case "Touch":
                    var touchModes = new List<string> { "press", "down", "up", "tap", "isOver" };
                    parametersContainer.Add(CreateFieldTag("Event"));
                    var tMode = CreateEventDropdown(touchModes, 0, 92);
                    tMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tMode); inputElements.Add(tMode);

                    var onActorToggle = new Toggle("On This Actor");
                    onActorToggle.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(onActorToggle); inputElements.Add(onActorToggle);
                    break;

                case "Keyboard":
                    AddParameterField("Key");
                    var keyModes = new List<string> { "press", "down", "up" };
                    parametersContainer.Add(CreateFieldTag("Event"));
                    var kMode = CreateEventDropdown(keyModes, 0, 82);
                    kMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(kMode); inputElements.Add(kMode);
                    break;
            }
            if (notifyChange) OnChanged?.Invoke();
        }

        private static PopupField<string> CreateEventDropdown(List<string> choices, int defaultIndex, float width)
        {
            var dropdown = new PopupField<string>(choices, defaultIndex) { style = { width = width } };
            dropdown.formatListItemCallback = FormatEventName;
            dropdown.formatSelectedValueCallback = FormatEventName;
            return dropdown;
        }

        private static string FormatEventName(string value)
        {
            return EventDisplayNames.TryGetValue(value, out string displayName) ? displayName : value;
        }

        private void AddParameterField(string placeholder, bool showPicker = false, bool boolOnly = false)
        {
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 140, alignItems = Align.Center } };
            container.style.flexShrink = 0;

            container.Add(CreateFieldTag(placeholder));

            var field = new TextField() { style = { flexGrow = 1, minWidth = 110 } };
            field.style.flexShrink = 0;
            field.isReadOnly = false;
            field.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            container.Add(field);

            if (showPicker)
            {
                var pickBtn = CreatePickerButton(anchorScreenRect =>
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) =>
                    {
                        field.value = picked;
                        OnChanged?.Invoke();
                    }, boolOnly, anchorScreenRect: anchorScreenRect);
                });
                container.Add(pickBtn);
            }
            parametersContainer.Add(container); inputElements.Add(field);
        }

        private VisualElement CreateFieldTag(string text)
        {
            var tag = new Label(text);
            tag.style.fontSize = 9;
            tag.style.unityFontStyleAndWeight = FontStyle.Bold;
            tag.style.color = new Color(0.85f, 0.85f, 0.85f);
            tag.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            tag.style.borderTopLeftRadius = 3;
            tag.style.borderTopRightRadius = 3;
            tag.style.borderBottomLeftRadius = 3;
            tag.style.borderBottomRightRadius = 3;
            tag.style.paddingLeft = 6;
            tag.style.paddingRight = 6;
            tag.style.paddingTop = 1;
            tag.style.paddingBottom = 1;
            tag.style.marginRight = 4;
            tag.style.minWidth = 62;
            tag.style.unityTextAlign = TextAnchor.MiddleCenter;
            return tag;
        }

        private Button CreatePickerButton(System.Action<Rect> onClick)
        {
            Button pickBtn = null;
            pickBtn = new Button(() =>
            {
                onClick?.Invoke(GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(pickBtn));
            }) { text = "Pick Property" };
            pickBtn.AddToClassList("button-property-picker");
            pickBtn.style.width = 100;
            pickBtn.style.minWidth = 100;
            pickBtn.style.height = 22;
            pickBtn.style.marginLeft = 2;
            pickBtn.style.flexShrink = 0;
            pickBtn.tooltip = "Pick Property";

            return pickBtn;
        }

        /// <summary>
        /// Drops comparison operators stuck to the edges of an operand. An operand never legitimately
        /// starts or ends with one; they only appear in rules saved while the split was going wrong.
        /// </summary>
        private static string StripOperators(string operand)
        {
            return operand.Trim().Trim('<', '>', '=', '!').Trim();
        }

        private void FillFieldsFromParams(string type, List<string> p)
        {
            if (p == null || p.Count == 0) return;
            if (type == "Compare")
            {
                string fullExpr = p[0];

                // Both operands are optional: a half-written rule reads "this.Health <" (or just "<"),
                // and requiring text on either side made the whole string fall into Value 1, which
                // then duplicated the operator on every rebuild.
                var match = Regex.Match(fullExpr, @"^(.*?)\s*(<=|>=|==|!=|<|>)\s*(.*)$");
                if (match.Success) { ((TextField)inputElements[0]).value = StripOperators(match.Groups[1].Value); ((PopupField<string>)inputElements[1]).value = match.Groups[2].Value.Trim(); ((TextField)inputElements[2]).value = StripOperators(match.Groups[3].Value); }
                else { ((TextField)inputElements[0]).value = fullExpr; }
            }
            else if (type == "Touch")
            {
                if (inputElements.Count >= 2) { ((PopupField<string>)inputElements[0]).value = p.Count > 0 ? p[0] : "press"; if (p.Count > 1 && bool.TryParse(p[1], out bool b)) ((Toggle)inputElements[1]).value = b; }
            }
            else
            {
                int paramIdx = 0;
                for (int i = 0; i < inputElements.Count && paramIdx < p.Count; i++)
                {
                    if (inputElements[i] is TextField tf) tf.value = p[paramIdx++];
                    else if (inputElements[i] is PopupField<string> pf && pf.choices.Contains(p[paramIdx])) pf.value = p[paramIdx++];
                    else if (inputElements[i] is Toggle tg && bool.TryParse(p[paramIdx], out bool b)) { tg.value = b; paramIdx++; }
                }
            }
        }

        public string GetString()
        {
            string type = selectedConditionType;
            if (string.IsNullOrEmpty(type)) return string.Empty;

            List<string> parts = new List<string>();
            string conditionText;

            if (type == "Compare")
            {
                string v1 = ((TextField)inputElements[0]).value; string op = ((PopupField<string>)inputElements[1]).value; string v2 = ((TextField)inputElements[2]).value;
                conditionText = $"Compare({v1} {op} {v2})";
                return isNegated ? $"NOT {conditionText}" : conditionText;
            }
            foreach (var el in inputElements)
            {
                if (el is TextField tf) parts.Add(tf.value);
                else if (el is PopupField<string> pf) parts.Add(pf.value);
                else if (el is Toggle tg) parts.Add(tg.value.ToString().ToLower());
            }
            conditionText = $"{type}({string.Join(",", parts)})";
            return isNegated ? $"NOT {conditionText}" : conditionText;
        }
    }
}
