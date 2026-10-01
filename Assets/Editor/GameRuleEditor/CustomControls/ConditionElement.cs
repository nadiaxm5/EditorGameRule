using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GameRuleEditor.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GameRuleEditor.CustomControls
{
    public class ConditionElement : VisualElement
    {
        private const string SelectConditionLabel = "Select condition";
        private const string PickKeyLabel = "Pick a Key";

        private readonly EditorContext context;
        private readonly List<string> availableTypes;
        private readonly bool detailsMode;
        private readonly List<VisualElement> inputElements = new List<VisualElement>();
        private readonly HashSet<VisualElement> selfReferenceFields = new HashSet<VisualElement>();
        private TooltipPopupField typeDropdown;
        private Button negationButton;
        private VisualElement parametersContainer;
        private ExpressionOperandControl compareLeftOperand;
        private ExpressionOperandControl compareRightOperand;
        private TooltipPopupField compareOperatorDropdown;
        private Button keyboardKeyButton;
        private Button checkPropertyButton;
        private string selectedKeyboardKey;
        private string checkPropertyValue;
        private bool isNegated;
        private string selectedConditionType;
        private string joinOperatorBefore;

        public System.Action OnChanged;
        public System.Action OnRemove;
        public Func<string, string, bool> BeforeTypeChange;
        public VisualElement TypeSelector => typeDropdown;
        public string JoinOperatorBefore => joinOperatorBefore;

        private static readonly Dictionary<string, string> EventDisplayNames = new Dictionary<string, string>
        {
            { "press", "Held" }, { "down", "Pressed" }, { "up", "Released" },
            { "tap", "Tapped" }, { "isOver", "Pointer Over" }
        };

        public ConditionElement(EditorContext ctx, List<string> conditionTypes, string joinOperatorBefore = null,
                                bool detailsMode = false)
        {
            context = ctx;
            availableTypes = conditionTypes;
            this.detailsMode = detailsMode;
            style.flexDirection = FlexDirection.Row;
            style.marginBottom = 5;
            style.flexShrink = 0;
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;
            style.paddingTop = 5;
            style.paddingBottom = 5;
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.alignItems = Align.Center;
            CreateUI(joinOperatorBefore);
        }

        private void CreateUI(string initialJoinOperator)
        {
            style.backgroundColor = GameRuleTheme.BrightHeader;
            SetJoinOperator(initialJoinOperator);

            negationButton = new Button(() =>
            {
                if (string.IsNullOrEmpty(selectedConditionType)) return;
                isNegated = !isNegated;
                UpdateNegationVisual();
                OnChanged?.Invoke();
            }) { text = "NOT", tooltip = "Invert this condition" };
            negationButton.AddToClassList("button-negation");
            if (detailsMode) negationButton.style.display = DisplayStyle.None;
            Add(negationButton);

            typeDropdown = new TooltipPopupField(
                RuleTooltips.ConditionOptions(availableTypes),
                SelectConditionLabel,
                "Choose a condition that must be true.",
                RuleDropdownPalette.Condition)
            {
                style = { width = 130 }
            };
            typeDropdown.AddToClassList("button-condition");
            typeDropdown.AddToClassList("rule-selector-dropdown");
            typeDropdown.style.flexShrink = 0;
            typeDropdown.RegisterValueChangedCallback(newValue =>
            {
                if (!availableTypes.Contains(newValue)) return;
                if (!string.IsNullOrEmpty(selectedConditionType) &&
                    BeforeTypeChange != null && !BeforeTypeChange(selectedConditionType, newValue))
                {
                    typeDropdown.SetValueWithoutNotify(selectedConditionType);
                    return;
                }
                selectedConditionType = newValue;
                UpdateNegationVisual();
                UpdateParameterFields();
            });
            Add(typeDropdown);

            parametersContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1, alignItems = Align.Center }
            };
            parametersContainer.style.flexShrink = 0;
            if (detailsMode) parametersContainer.style.flexWrap = Wrap.Wrap;
            Add(parametersContainer);
            Add(new VisualElement { style = { flexGrow = 1 } });

            var removeButton = new Button(() => OnRemove?.Invoke())
            {
                text = "×",
                tooltip = "Remove Condition"
            };
            removeButton.AddToClassList("button-danger");
            removeButton.AddToClassList("button-danger-icon");
            removeButton.style.width = 28;
            removeButton.style.height = 26;
            if (detailsMode) removeButton.style.display = DisplayStyle.None;
            Add(removeButton);

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
            if (!availableTypes.Contains(result.Name)) return;
            selectedConditionType = result.Name;
            typeDropdown.SetValueWithoutNotify(result.Name);
            UpdateNegationVisual();
            UpdateParameterFields(false);
            FillFieldsFromParams(result.Name, result.Params);
        }

        private void UpdateParameterFields(bool notifyChange = true)
        {
            parametersContainer.Clear();
            inputElements.Clear();
            selfReferenceFields.Clear();
            compareLeftOperand = null;
            compareRightOperand = null;
            compareOperatorDropdown = null;
            keyboardKeyButton = null;
            checkPropertyButton = null;
            selectedKeyboardKey = null;
            checkPropertyValue = null;

            switch (selectedConditionType)
            {
                case "Compare":
                    AddCompareFields();
                    break;
                case "Check":
                    AddPropertyField("Boolean Property", true);
                    break;
                case "Collision":
                    var projectTags = Loader.GetProjectTags(context?.currentProject?.actors);
                    parametersContainer.Add(CreateFieldTag("Tag"));
                    var tagDropdown = new PopupField<string>(projectTags, 0);
                    tagDropdown.AddToClassList("collision-tag-dropdown");
                    tagDropdown.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tagDropdown);
                    inputElements.Add(tagDropdown);
                    break;
                case "Timer":
                    AddNumericField("Seconds", 0f, 0.1f, true);
                    break;
                case "Touch":
                    var touchModes = new List<string> { "press", "down", "up", "tap", "isOver" };
                    parametersContainer.Add(CreateFieldTag("Event"));
                    var touchMode = CreateEventDropdown(touchModes, 0, 92);
                    touchMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(touchMode);
                    inputElements.Add(touchMode);
                    var onActorToggle = new Toggle("On This Actor");
                    onActorToggle.tooltip = RuleTooltips.Parameter("Touch", "On This Actor");
                    onActorToggle.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(onActorToggle);
                    inputElements.Add(onActorToggle);
                    break;
                case "Keyboard":
                    AddKeyboardFields();
                    break;
            }

            if (notifyChange) OnChanged?.Invoke();
        }

        private void AddCompareFields()
        {
            compareLeftOperand = new ExpressionOperandControl(context, "Property 1");
            compareLeftOperand.Changed += () => OnChanged?.Invoke();
            parametersContainer.Add(compareLeftOperand);

            compareOperatorDropdown = new TooltipPopupField(
                RuleTooltips.ComparisonOptions(),
                "<",
                "Choose how Property 1 is compared with Property 2.",
                RuleDropdownPalette.Number);
            compareOperatorDropdown.AddToClassList("button-number-picker");
            compareOperatorDropdown.AddToClassList("rule-selector-dropdown");
            compareOperatorDropdown.AddToClassList("compare-operator-dropdown");
            compareOperatorDropdown.RegisterValueChangedCallback(_ => OnChanged?.Invoke());
            var operatorContainer = new VisualElement();
            operatorContainer.AddToClassList("compare-operator-container");
            operatorContainer.Add(compareOperatorDropdown);
            parametersContainer.Add(operatorContainer);

            compareRightOperand = new ExpressionOperandControl(context, "Property 2");
            compareRightOperand.Changed += () => OnChanged?.Invoke();
            parametersContainer.Add(compareRightOperand);
        }

        private void AddKeyboardFields()
        {
            var keyContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 3 }
            };
            keyContainer.style.flexShrink = 0;
            keyContainer.Add(CreateFieldTag("Key"));
            keyboardKeyButton = new Button(ShowKeyboardMenu);
            keyboardKeyButton.AddToClassList("keyboard-key-picker");
            UpdateKeyboardButton();
            keyContainer.Add(keyboardKeyButton);
            parametersContainer.Add(keyContainer);

            parametersContainer.Add(CreateFieldTag("Event"));
            var keyMode = CreateEventDropdown(new List<string> { "press", "down", "up" }, 0, 82);
            keyMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            parametersContainer.Add(keyMode);
            inputElements.Add(keyMode);
        }

        private void ShowKeyboardMenu()
        {
            var menu = new GenericMenu();
            string[] groupOrder = { "Letters", "Numbers", "Arrows", "Other" };
            var groupedKeys = GetKeyboardGroups();

            foreach (string groupName in groupOrder)
            {
                foreach (string keyName in groupedKeys[groupName])
                {
                    string capturedKey = keyName;
                    menu.AddItem(
                        new GUIContent($"{groupName}/{FormatKeyName(keyName)}"),
                        string.Equals(selectedKeyboardKey, keyName, StringComparison.OrdinalIgnoreCase),
                        () => SetKeyboardKey(capturedKey));
                }
            }

            menu.ShowAsContext();
        }

        private void SetKeyboardKey(string keyName)
        {
            selectedKeyboardKey = keyName;
            UpdateKeyboardButton();
            OnChanged?.Invoke();
        }

        private void UpdateKeyboardButton()
        {
            if (keyboardKeyButton == null) return;
            keyboardKeyButton.text = string.IsNullOrEmpty(selectedKeyboardKey)
                ? PickKeyLabel
                : FormatKeyName(selectedKeyboardKey);
            keyboardKeyButton.tooltip = string.IsNullOrEmpty(selectedKeyboardKey)
                ? "Choose a keyboard key"
                : FormatKeyName(selectedKeyboardKey);
        }

        private static Dictionary<string, List<string>> GetKeyboardGroups()
        {
            var groups = new Dictionary<string, List<string>>
            {
                { "Letters", new List<string>() },
                { "Numbers", new List<string>() },
                { "Arrows", new List<string>() },
                { "Other", new List<string>() }
            };
            var modifiers = new List<string>();

            foreach (string keyName in Enum.GetNames(typeof(Key)))
            {
                if (keyName == nameof(Key.None) || keyName == "IMESelected") continue;
                string groupName = GetKeyboardGroup(keyName);
                if (groupName == "Modifiers") modifiers.Add(keyName);
                else if (!string.IsNullOrEmpty(groupName)) groups[groupName].Add(keyName);
            }
            groups["Other"].AddRange(modifiers);

            return groups;
        }

        private static string GetKeyboardGroup(string keyName)
        {
            if (Regex.IsMatch(keyName, @"^[A-Z]$")) return "Letters";
            if (Regex.IsMatch(keyName, @"^Digit[0-9]$")) return "Numbers";
            if (keyName == nameof(Key.LeftArrow) || keyName == nameof(Key.RightArrow) ||
                keyName == nameof(Key.UpArrow) || keyName == nameof(Key.DownArrow)) return "Arrows";
            if (keyName == "LeftShift" || keyName == "RightShift" ||
                keyName == "LeftCtrl" || keyName == "RightCtrl" ||
                keyName == "LeftAlt" || keyName == "RightAlt") return "Modifiers";
            if (keyName == "Space" || keyName == "Enter" || keyName == "Escape" ||
                keyName == "Tab" || keyName == "Backspace" || keyName == "Delete") return "Other";
            return null;
        }

        private static string FormatKeyName(string value)
        {
            if (string.IsNullOrEmpty(value) || value == PickKeyLabel) return PickKeyLabel;
            string spaced = Regex.Replace(value, @"([a-z0-9])([A-Z])", "$1 $2");
            return Regex.Replace(spaced, @"^Digit([0-9])$", "Number $1");
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

        private void AddPropertyField(string label, bool boolOnly)
        {
            var container = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginRight = 3, alignItems = Align.Center }
            };
            container.style.flexShrink = 0;
            container.Add(CreateFieldTag(label));

            checkPropertyButton = null;
            checkPropertyButton = new Button(() =>
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    checkPropertyValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked);
                    UpdateCheckPropertyButton();
                    OnChanged?.Invoke();
                }, boolOnly, anchorScreenRect:
                    GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(checkPropertyButton));
            });
            checkPropertyButton.AddToClassList("compare-property-term");
            checkPropertyButton.AddToClassList("check-property-field");
            UpdateCheckPropertyButton();
            container.Add(checkPropertyButton);
            parametersContainer.Add(container);
        }

        private void UpdateCheckPropertyButton()
        {
            if (checkPropertyButton == null) return;
            bool hasValue = !string.IsNullOrWhiteSpace(checkPropertyValue);
            checkPropertyButton.text = hasValue ? checkPropertyValue : "Pick Property";
            checkPropertyButton.tooltip = hasValue ? checkPropertyValue : "Choose a Boolean property";
        }

        private void AddNumericField(string label, float initialValue, float step, bool clampToZero)
        {
            var container = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginRight = 3, minWidth = 190, alignItems = Align.Center }
            };
            container.style.flexShrink = 0;
            VisualElement fieldTag = CreateFieldTag(label);
            fieldTag.tooltip = RuleTooltips.Parameter(selectedConditionType, label) +
                               " Drag horizontally to change the value.";
            container.Add(fieldTag);
            var stepper = new NumericStepper(initialValue, step, clampToZero ? 0f : (float?)null);
            stepper.Changed += () => OnChanged?.Invoke();
            stepper.EnableDragOn(fieldTag);
            container.Add(stepper);
            parametersContainer.Add(container);
            inputElements.Add(stepper);
        }

        private VisualElement CreateFieldTag(string text)
        {
            var tag = new Label(text);
            tag.tooltip = RuleTooltips.Parameter(selectedConditionType, text);
            tag.style.fontSize = 9;
            tag.style.unityFontStyleAndWeight = FontStyle.Bold;
            tag.style.color = GameRuleTheme.Text;
            tag.style.backgroundColor = GameRuleTheme.Surface;
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
            Button button = null;
            button = new Button(() => onClick?.Invoke(
                GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(button))) { text = "Pick Property" };
            button.AddToClassList("button-property-picker");
            button.style.width = 100;
            button.style.minWidth = 100;
            button.style.height = 22;
            button.style.marginLeft = 2;
            button.style.flexShrink = 0;
            button.tooltip = "Pick Property";
            return button;
        }

        private static string StripOperators(string operand)
        {
            return operand.Trim().Trim('<', '>', '=', '!').Trim();
        }

        private void FillFieldsFromParams(string type, List<string> parameters)
        {
            if (parameters == null || parameters.Count == 0) return;

            if (type == "Compare")
            {
                string fullExpression = parameters[0];
                var match = Regex.Match(fullExpression, @"^(.*?)\s*(<=|>=|==|!=|<|>)\s*(.*)$");
                if (match.Success)
                {
                    compareLeftOperand.SetExpression(StripOperators(match.Groups[1].Value));
                    compareOperatorDropdown.SetValueWithoutNotify(match.Groups[2].Value.Trim());
                    compareRightOperand.SetExpression(StripOperators(match.Groups[3].Value));
                }
                else compareLeftOperand.SetExpression(fullExpression);
                return;
            }

            if (type == "Touch")
            {
                if (inputElements.Count >= 2)
                {
                    var eventDropdown = (PopupField<string>)inputElements[0];
                    if (parameters.Count > 0 && eventDropdown.choices.Contains(parameters[0]))
                        eventDropdown.SetValueWithoutNotify(parameters[0]);
                    if (parameters.Count > 1 && bool.TryParse(parameters[1], out bool onActor))
                        ((Toggle)inputElements[1]).SetValueWithoutNotify(onActor);
                }
                return;
            }

            if (type == "Check")
            {
                checkPropertyValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(
                    context, parameters[0]);
                UpdateCheckPropertyButton();
                return;
            }

            if (type == "Keyboard")
            {
                if (parameters.Count > 0)
                {
                    foreach (string keyName in Enum.GetNames(typeof(Key)))
                    {
                        if (!string.Equals(keyName, parameters[0], StringComparison.OrdinalIgnoreCase)) continue;
                        selectedKeyboardKey = keyName;
                        break;
                    }
                    UpdateKeyboardButton();
                }
                if (parameters.Count > 1)
                {
                    var eventDropdown = (PopupField<string>)inputElements[0];
                    if (eventDropdown.choices.Contains(parameters[1]))
                        eventDropdown.SetValueWithoutNotify(parameters[1]);
                }
                return;
            }

            int parameterIndex = 0;
            for (int i = 0; i < inputElements.Count && parameterIndex < parameters.Count; i++)
            {
                if (inputElements[i] is TextField textField)
                {
                    string value = parameters[parameterIndex++];
                    textField.SetValueWithoutNotify(selfReferenceFields.Contains(textField)
                        ? GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, value)
                        : value);
                }
                else if (inputElements[i] is NumericStepper numberField)
                    numberField.SetSerializedValue(parameters[parameterIndex++]);
                else if (inputElements[i] is PopupField<string> popupField)
                {
                    string value = parameters[parameterIndex++];
                    if (popupField.choices.Contains(value)) popupField.SetValueWithoutNotify(value);
                }
                else if (inputElements[i] is Toggle toggle &&
                         bool.TryParse(parameters[parameterIndex++], out bool value))
                    toggle.SetValueWithoutNotify(value);
            }
        }

        public string GetString()
        {
            if (string.IsNullOrEmpty(selectedConditionType)) return string.Empty;
            string conditionText;
            if (selectedConditionType == "Compare")
            {
                conditionText = $"Compare({compareLeftOperand.GetStoredExpression()} " +
                                $"{compareOperatorDropdown.value} {compareRightOperand.GetStoredExpression()})";
                return isNegated ? $"NOT {conditionText}" : conditionText;
            }

            if (selectedConditionType == "Check")
            {
                string storedValue = GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(
                    context, checkPropertyValue ?? string.Empty);
                conditionText = $"Check({storedValue})";
                return isNegated ? $"NOT {conditionText}" : conditionText;
            }

            if (selectedConditionType == "Keyboard")
            {
                string eventName = inputElements.Count > 0 && inputElements[0] is PopupField<string> eventDropdown
                    ? eventDropdown.value
                    : string.Empty;
                conditionText = $"Keyboard({selectedKeyboardKey ?? string.Empty},{eventName})";
                return isNegated ? $"NOT {conditionText}" : conditionText;
            }

            var parts = new List<string>();
            foreach (VisualElement element in inputElements)
            {
                if (element is TextField textField)
                    parts.Add(selfReferenceFields.Contains(textField)
                        ? GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(context, textField.value)
                        : textField.value);
                else if (element is NumericStepper numberField)
                    parts.Add(numberField.SerializedValue);
                else if (element is PopupField<string> popupField)
                    parts.Add(popupField.value);
                else if (element is Toggle toggle)
                    parts.Add(toggle.value.ToString().ToLowerInvariant());
            }

            conditionText = $"{selectedConditionType}({string.Join(",", parts)})";
            return isNegated ? $"NOT {conditionText}" : conditionText;
        }

        private sealed class NumericStepper : VisualElement
        {
            private readonly FloatField field;
            private readonly float step;
            private readonly float? minimum;
            private bool dragging;
            private float dragStartX;
            private float dragStartValue;
            public System.Action Changed;
            public string SerializedValue => FormatNumber(field.value);

            public NumericStepper(float initialValue, float step, float? minimum)
            {
                this.step = step;
                this.minimum = minimum;
                AddToClassList("numeric-stepper");
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.flexShrink = 0;

                var decrement = new Button(() => SetValue(field.value - step, true))
                { text = "▼", tooltip = $"Decrease by {FormatNumber(step)}" };
                decrement.AddToClassList("numeric-stepper-button");
                Add(decrement);

                field = new FloatField { value = Sanitize(initialValue) };
                field.AddToClassList("numeric-stepper-field");
                field.RegisterValueChangedCallback(evt =>
                {
                    float sanitized = Sanitize(evt.newValue);
                    if (!Mathf.Approximately(sanitized, evt.newValue)) field.SetValueWithoutNotify(sanitized);
                    Changed?.Invoke();
                });
                Add(field);

                var increment = new Button(() => SetValue(field.value + step, true))
                { text = "▲", tooltip = $"Increase by {FormatNumber(step)}" };
                increment.AddToClassList("numeric-stepper-button");
                Add(increment);
            }

            public void SetSerializedValue(string serializedValue)
            {
                SetValue(TryParseNumber(serializedValue, out float value) ? value : 0f, false);
            }

            public void EnableDragOn(VisualElement handle)
            {
                handle.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    dragging = true;
                    dragStartX = evt.position.x;
                    dragStartValue = field.value;
                    handle.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!dragging || !handle.HasPointerCapture(evt.pointerId)) return;
                    float pixelDelta = evt.position.x - dragStartX;
                    if (Mathf.Abs(pixelDelta) < 2f) return;
                    SetValue(dragStartValue + pixelDelta / 8f * step, true);
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!dragging) return;
                    dragging = false;
                    if (handle.HasPointerCapture(evt.pointerId)) handle.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                });
            }

            private void SetValue(float value, bool notify)
            {
                float sanitized = Sanitize(value);
                if (Mathf.Approximately(field.value, sanitized)) return;
                field.SetValueWithoutNotify(sanitized);
                if (notify) Changed?.Invoke();
            }

            private float Sanitize(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
                if (minimum.HasValue) value = Mathf.Max(minimum.Value, value);
                return (float)Math.Round(value, 4, MidpointRounding.AwayFromZero);
            }

            private static bool TryParseNumber(string text, out float value)
            {
                return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                       float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
            }

            private static string FormatNumber(float value)
            {
                return value.ToString("0.####", CultureInfo.InvariantCulture);
            }
        }

        private sealed class ExpressionOperandControl : VisualElement
        {
            private const string PickValueLabel = "Pick Property / Number";
            private const string PickPropertyChoice = "Pick Property";
            private const string PickNumberChoice = "Pick Number";
            private const string PickOperationLabel = "+ / −";

            private enum TermKind { Property, Number, Legacy }
            private sealed class ExpressionTerm
            {
                public string OperatorBefore;
                public TermKind Kind;
                public string Text;
                public float Number;
            }

            private readonly EditorContext context;
            private readonly List<ExpressionTerm> terms = new List<ExpressionTerm>();
            private readonly VisualElement termsContainer;
            private readonly TooltipPopupField valuePickerDropdown;
            private readonly TooltipPopupField operationDropdown;
            private string pendingOperator;
            private bool lockedLegacyExpression;
            private bool suppressChanged;
            public System.Action Changed;

            public ExpressionOperandControl(EditorContext context, string label)
            {
                this.context = context;
                AddToClassList("compare-operand");
                style.flexGrow = 1;
                style.minWidth = 300;
                style.flexDirection = FlexDirection.Column;

                var toolbar = new VisualElement();
                toolbar.AddToClassList("compare-operand-toolbar");
                var fieldTag = new Label(label);
                fieldTag.AddToClassList("compare-operand-label");
                fieldTag.tooltip = RuleTooltips.Parameter("Compare", label);
                toolbar.Add(fieldTag);

                valuePickerDropdown = new TooltipPopupField(
                    RuleTooltips.ValueSourceOptions(),
                    PickValueLabel,
                    "Choose a property or a number.",
                    RuleDropdownPalette.Property);
                valuePickerDropdown.AddToClassList("button-property-picker");
                valuePickerDropdown.AddToClassList("rule-selector-dropdown");
                valuePickerDropdown.AddToClassList("compare-value-picker");
                valuePickerDropdown.RegisterValueChangedCallback(choice =>
                {
                    valuePickerDropdown.SetValueWithoutNotify(PickValueLabel);
                    if (choice == PickPropertyChoice)
                    {
                        PickProperty(GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(valuePickerDropdown));
                    }
                    else if (choice == PickNumberChoice)
                    {
                        ApplyPickedTerm(new ExpressionTerm { Kind = TermKind.Number, Number = 0f });
                    }
                });
                toolbar.Add(valuePickerDropdown);

                operationDropdown = new TooltipPopupField(
                    RuleTooltips.ArithmeticOptions(),
                    PickOperationLabel,
                    "Add or subtract one more property or number.",
                    RuleDropdownPalette.Number);
                operationDropdown.AddToClassList("button-number-picker");
                operationDropdown.AddToClassList("rule-selector-dropdown");
                operationDropdown.AddToClassList("compare-operation-dropdown");
                operationDropdown.RegisterValueChangedCallback(newValue =>
                {
                    if (terms.Count == 1 && pendingOperator == null && !lockedLegacyExpression)
                    {
                        pendingOperator = newValue;
                        operationDropdown.SetValueWithoutNotify(PickOperationLabel);
                        RebuildTerms();
                    }
                    else operationDropdown.SetValueWithoutNotify(PickOperationLabel);
                });
                toolbar.Add(operationDropdown);
                Add(toolbar);

                termsContainer = new VisualElement();
                termsContainer.AddToClassList("compare-terms");
                Add(termsContainer);
                RebuildTerms();
            }

            public void SetExpression(string expression)
            {
                suppressChanged = true;
                terms.Clear();
                pendingOperator = null;
                lockedLegacyExpression = false;
                ParseExpression(expression);
                RebuildTerms();
                suppressChanged = false;
            }

            public string GetStoredExpression()
            {
                var result = new StringBuilder();
                for (int i = 0; i < terms.Count; i++)
                {
                    ExpressionTerm term = terms[i];
                    if (i > 0) result.Append(' ').Append(term.OperatorBefore).Append(' ');
                    if (term.Kind == TermKind.Number)
                        result.Append(term.Number.ToString("0.####", CultureInfo.InvariantCulture));
                    else
                        result.Append(GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(context, term.Text));
                }
                return result.ToString();
            }

            private void PickProperty(Rect anchor)
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    ApplyPickedTerm(new ExpressionTerm
                    {
                        Kind = TermKind.Property,
                        Text = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked)
                    });
                }, false, anchorScreenRect: anchor);
            }

            private void ReplaceProperty(int index, Rect anchor)
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    if (index < 0 || index >= terms.Count) return;
                    string operatorBefore = terms[index].OperatorBefore;
                    terms[index] = new ExpressionTerm
                    {
                        OperatorBefore = operatorBefore,
                        Kind = TermKind.Property,
                        Text = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked)
                    };
                    lockedLegacyExpression = false;
                    RebuildTermsAndNotify();
                }, false, anchorScreenRect: anchor);
            }

            private void ApplyPickedTerm(ExpressionTerm pickedTerm)
            {
                lockedLegacyExpression = false;
                if (pendingOperator != null && terms.Count == 1)
                {
                    pickedTerm.OperatorBefore = pendingOperator;
                    terms.Add(pickedTerm);
                    pendingOperator = null;
                }
                else if (terms.Count >= 2)
                {
                    pickedTerm.OperatorBefore = terms[1].OperatorBefore;
                    terms[1] = pickedTerm;
                }
                else
                {
                    terms.Clear();
                    pendingOperator = null;
                    pickedTerm.OperatorBefore = null;
                    terms.Add(pickedTerm);
                }
                RebuildTermsAndNotify();
            }

            private void RebuildTerms()
            {
                termsContainer.Clear();
                var expressionContainer = new VisualElement();
                expressionContainer.AddToClassList("compare-terms-content");
                termsContainer.Add(expressionContainer);
                bool canAddOperation = terms.Count == 1 && pendingOperator == null && !lockedLegacyExpression;
                operationDropdown.SetEnabled(canAddOperation);
                operationDropdown.tooltip = canAddOperation
                    ? "Choose + or −, then pick the second property or number"
                    : "Only one + or − operation is allowed";
                valuePickerDropdown.tooltip = pendingOperator != null || terms.Count >= 2
                    ? "Choose the second property or number"
                    : "Choose or replace the property or number";

                if (terms.Count == 0)
                {
                    var empty = new Label("Choose a property or number");
                    empty.AddToClassList("compare-empty-expression");
                    expressionContainer.Add(empty);
                    return;
                }

                for (int i = 0; i < terms.Count; i++)
                {
                    int index = i;
                    ExpressionTerm term = terms[i];
                    var row = new VisualElement();
                    row.AddToClassList("compare-term");

                    if (i > 0)
                    {
                        var operatorDropdown = new TooltipPopupField(
                            RuleTooltips.ArithmeticOptions(),
                            term.OperatorBefore == "-" ? "-" : "+",
                            "Change this arithmetic operation.",
                            RuleDropdownPalette.Number);
                        operatorDropdown.AddToClassList("button-number-picker");
                        operatorDropdown.AddToClassList("compare-term-operator");
                        operatorDropdown.RegisterValueChangedCallback(newValue =>
                        {
                            term.OperatorBefore = newValue;
                            NotifyChanged();
                        });
                        row.Add(operatorDropdown);
                    }

                    if (term.Kind == TermKind.Number)
                    {
                        var number = new NumericStepper(term.Number, 0.1f, null);
                        number.Changed += () =>
                        {
                            if (float.TryParse(number.SerializedValue, NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out float parsed)) term.Number = parsed;
                            NotifyChanged();
                        };
                        row.Add(number);
                    }
                    else if (term.Kind == TermKind.Property)
                    {
                        Button termButton = null;
                        termButton = new Button(() => ReplaceProperty(index,
                            GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(termButton)))
                        {
                            text = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, term.Text),
                            tooltip = "Click to replace this property"
                        };
                        termButton.AddToClassList("compare-property-term");
                        row.Add(termButton);
                    }
                    else
                    {
                        var legacy = new Label(GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, term.Text));
                        legacy.tooltip = "Existing expression. Replace it to edit it visually.";
                        legacy.AddToClassList("compare-legacy-term");
                        row.Add(legacy);
                    }

                    expressionContainer.Add(row);
                }

                if (pendingOperator != null)
                {
                    var pending = new Label(pendingOperator);
                    pending.AddToClassList("button-number-picker");
                    pending.AddToClassList("compare-term-operator");
                    pending.AddToClassList("compare-pending-operator");
                    pending.tooltip = "Now choose the second property or number";
                    expressionContainer.Add(pending);
                }

                var remove = new Button(ClearExpression) { text = "×", tooltip = "Clear this expression" };
                remove.AddToClassList("button-danger");
                remove.AddToClassList("compare-remove-term");
                termsContainer.Add(remove);
            }

            private void ClearExpression()
            {
                terms.Clear();
                pendingOperator = null;
                lockedLegacyExpression = false;
                RebuildTermsAndNotify();
            }

            private void ParseExpression(string expression)
            {
                string normalized = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, expression?.Trim());
                if (string.IsNullOrEmpty(normalized)) return;
                List<ExpressionPart> parts = SplitAddSubtract(normalized);
                if (parts.Count > 2)
                {
                    lockedLegacyExpression = true;
                    terms.Add(new ExpressionTerm { Kind = TermKind.Legacy, Text = normalized });
                    return;
                }

                foreach (ExpressionPart part in parts)
                {
                    string value = part.Value.Trim();
                    if (string.IsNullOrEmpty(value)) continue;
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                    {
                        if (terms.Count == 0 && part.Operator == "-") number = -number;
                        terms.Add(new ExpressionTerm
                        {
                            OperatorBefore = terms.Count == 0 ? null : part.Operator,
                            Kind = TermKind.Number,
                            Number = number
                        });
                    }
                    else
                    {
                        bool property = Regex.IsMatch(value,
                            @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$");
                        string text = terms.Count == 0 && part.Operator == "-" ? "-" + value : value;
                        terms.Add(new ExpressionTerm
                        {
                            OperatorBefore = terms.Count == 0 ? null : part.Operator,
                            Kind = property && !text.StartsWith("-") ? TermKind.Property : TermKind.Legacy,
                            Text = text
                        });
                    }
                }
            }

            private static List<ExpressionPart> SplitAddSubtract(string expression)
            {
                var result = new List<ExpressionPart>();
                int depth = 0;
                int start = 0;
                string pendingOperator = null;
                for (int i = 0; i < expression.Length; i++)
                {
                    char character = expression[i];
                    if (character == '(') depth++;
                    else if (character == ')') depth = Math.Max(0, depth - 1);
                    if (depth != 0 || (character != '+' && character != '-')) continue;
                    if (i > 0 && (expression[i - 1] == 'e' || expression[i - 1] == 'E')) continue;
                    if (i == start)
                    {
                        pendingOperator = character.ToString();
                        start = i + 1;
                        continue;
                    }

                    // A sign immediately after the binary operator belongs to the next number.
                    // For example, "Health - -0.1" is one subtraction whose second term is -0.1,
                    // not two separate arithmetic operations.
                    if (pendingOperator != null &&
                        string.IsNullOrWhiteSpace(expression.Substring(start, i - start)))
                    {
                        start = i;
                        continue;
                    }

                    result.Add(new ExpressionPart(pendingOperator, expression.Substring(start, i - start)));
                    pendingOperator = character.ToString();
                    start = i + 1;
                }
                result.Add(new ExpressionPart(pendingOperator, expression.Substring(start)));
                return result;
            }

            private void RebuildTermsAndNotify()
            {
                RebuildTerms();
                NotifyChanged();
            }

            private void NotifyChanged()
            {
                if (!suppressChanged) Changed?.Invoke();
            }

            private readonly struct ExpressionPart
            {
                public readonly string Operator;
                public readonly string Value;
                public ExpressionPart(string operation, string value)
                {
                    Operator = operation;
                    Value = value;
                }
            }
        }
    }
}
