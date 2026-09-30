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
        private TooltipPopupField nextOperatorDropdown;
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
            nextOperatorDropdown = new TooltipPopupField(
                RuleTooltips.LogicalOptions(),
                addConditionLabel,
                "Add another condition and choose how it connects.",
                RuleDropdownPalette.Condition);
            nextOperatorDropdown.AddToClassList("button-condition");
            nextOperatorDropdown.AddToClassList("rule-selector-dropdown");
            nextOperatorDropdown.style.width = 150;
            nextOperatorDropdown.RegisterValueChangedCallback(newValue =>
            {
                if (newValue == "AND" || newValue == "OR")
                {
                    string joinOperator = elements.Count > 0 ? newValue : null;
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

                    string currentOperator = elements[idx].JoinOperatorBefore == "OR" ? "OR" : "AND";
                    var connectorDropdown = new TooltipPopupField(
                        RuleTooltips.LogicalOptions(),
                        currentOperator,
                        RuleTooltips.Logical(currentOperator),
                        RuleDropdownPalette.Condition);
                    connectorDropdown.AddToClassList("button-condition");
                    connectorDropdown.AddToClassList("rule-selector-dropdown");
                    connectorDropdown.style.width = 70;
                    connectorDropdown.RegisterValueChangedCallback(newValue =>
                    {
                        elements[idx].SetJoinOperator(newValue);
                        // An empty condition has no serialized form yet. Persisting now
                        // rebuilds the rule from the previous conditions and drops this row.
                        if (elements.All(element => !string.IsNullOrEmpty(element.GetString())))
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

    internal enum RuleDropdownPalette
    {
        Condition,
        Action,
        Primary,
        Property,
        Number
    }

    internal readonly struct TooltipDropdownOption
    {
        public readonly string Value;
        public readonly string Label;
        public readonly string Tooltip;

        public TooltipDropdownOption(string value, string label, string tooltip)
        {
            Value = value;
            Label = label;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Button-backed dropdown whose popup items can carry real Unity tooltips.
    /// PopupField only accepts strings for its menu entries, so it cannot explain
    /// individual actions, conditions, or logical operators while the menu is open.
    /// </summary>
    internal sealed class TooltipPopupField : Button
    {
        private readonly List<TooltipDropdownOption> options;
        private readonly string placeholderTooltip;
        private readonly RuleDropdownPalette palette;
        private string currentValue;
        private VisualElement openMenuOverlay;

        public string value => currentValue;
        public event System.Action<string> ValueChanged;

        public TooltipPopupField(IEnumerable<TooltipDropdownOption> choices,
                                 string initialValue,
                                 string placeholderTooltip,
                                 RuleDropdownPalette palette)
        {
            options = choices?.ToList() ?? new List<TooltipDropdownOption>();
            this.placeholderTooltip = placeholderTooltip;
            this.palette = palette;
            AddToClassList("tooltip-popup-field");
            clicked += ShowMenu;
            RegisterCallback<DetachFromPanelEvent>(_ => CloseMenu());
            SetValueWithoutNotify(initialValue);
        }

        public void RegisterValueChangedCallback(System.Action<string> callback)
        {
            ValueChanged += callback;
        }

        public void SetValueWithoutNotify(string newValue)
        {
            currentValue = newValue ?? string.Empty;
            UpdatePresentation();
        }

        private void SelectValue(string newValue)
        {
            if (string.Equals(currentValue, newValue, System.StringComparison.Ordinal)) return;
            SetValueWithoutNotify(newValue);
            ValueChanged?.Invoke(newValue);
        }

        private void UpdatePresentation()
        {
            TooltipDropdownOption? selected = null;
            foreach (TooltipDropdownOption option in options)
            {
                if (option.Value != currentValue) continue;
                selected = option;
                break;
            }
            string label = selected?.Label ?? currentValue;
            text = string.IsNullOrWhiteSpace(label) ? "Select" : label + "  ▾";
            tooltip = selected?.Tooltip ?? placeholderTooltip;
        }

        private void ShowMenu()
        {
            CloseMenu();

            VisualElement root = this;
            VisualElement styledRoot = null;
            for (VisualElement current = this; current != null; current = current.parent)
            {
                root = current;
                if (current.styleSheets.count > 0) styledRoot = current;
            }
            if (styledRoot != null) root = styledRoot;

            Vector2 anchorPosition = root.WorldToLocal(worldBound.position);
            Rect anchor = new Rect(anchorPosition, worldBound.size);
            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f) rootWidth = root.worldBound.width;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f) rootHeight = root.worldBound.height;

            int longestLabel = options.Count == 0 ? 0 : options.Max(option => option.Label?.Length ?? 0);
            float menuWidth = Mathf.Min(Mathf.Clamp(longestLabel * 7f + 54f, 190f, 300f),
                                        Mathf.Max(100f, rootWidth - 8f));
            float menuHeight = Mathf.Min(Mathf.Clamp(options.Count * 29f + 8f, 38f, 360f),
                                         Mathf.Max(38f, rootHeight - 8f));
            const float gap = 3f;
            float x = Mathf.Clamp(anchor.xMin, 4f, Mathf.Max(4f, rootWidth - menuWidth - 4f));
            float y = anchor.yMax + gap;
            if (y + menuHeight > rootHeight - 4f)
                y = anchor.yMin - menuHeight - gap;
            y = Mathf.Clamp(y, 4f, Mathf.Max(4f, rootHeight - menuHeight - 4f));

            openMenuOverlay = new VisualElement { focusable = true };
            openMenuOverlay.AddToClassList("tooltip-dropdown-overlay");
            openMenuOverlay.style.position = Position.Absolute;
            openMenuOverlay.style.left = 0;
            openMenuOverlay.style.top = 0;
            openMenuOverlay.style.right = 0;
            openMenuOverlay.style.bottom = 0;
            openMenuOverlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == openMenuOverlay) CloseMenu();
            });
            openMenuOverlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) CloseMenu();
            });

            var menu = new VisualElement();
            menu.AddToClassList("tooltip-dropdown-menu");
            menu.style.position = Position.Absolute;
            menu.style.left = x;
            menu.style.top = y;
            menu.style.width = menuWidth;
            menu.style.height = menuHeight;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            foreach (TooltipDropdownOption option in options)
            {
                TooltipDropdownOption capturedOption = option;
                string prefix = option.Value == currentValue ? "✓  " : string.Empty;
                var optionButton = new Button(() =>
                {
                    CloseMenu();
                    SelectValue(capturedOption.Value);
                })
                {
                    text = prefix + option.Label,
                    tooltip = option.Tooltip
                };
                optionButton.AddToClassList(GetPaletteClass());
                optionButton.AddToClassList("tooltip-dropdown-option");
                scroll.Add(optionButton);
            }

            menu.Add(scroll);
            openMenuOverlay.Add(menu);
            root.Add(openMenuOverlay);
            openMenuOverlay.BringToFront();
            openMenuOverlay.Focus();
        }

        private void CloseMenu()
        {
            openMenuOverlay?.RemoveFromHierarchy();
            openMenuOverlay = null;
        }

        private string GetPaletteClass()
        {
            switch (palette)
            {
                case RuleDropdownPalette.Condition: return "button-condition";
                case RuleDropdownPalette.Action: return "button-action";
                case RuleDropdownPalette.Property: return "button-property-picker";
                case RuleDropdownPalette.Number: return "button-number-picker";
                default: return "button-primary";
            }
        }
    }

    internal static class RuleTooltips
    {
        private static readonly Dictionary<string, string> ActionDescriptions =
            new Dictionary<string, string>
            {
                { "Edit", "Set a property to a value or simple expression." },
                { "Delete", "Remove this actor from the scene." },
                { "Spawn", "Create a prefab relative to this actor." },
                { "Animate", "Play an animation on this actor." },
                { "PlaySound", "Play a sound on this actor." },
                { "PlayParticles", "Play a particle effect on this actor." },
                { "Move", "Move continuously in the chosen direction." },
                { "MoveTo", "Move toward a world position." },
                { "NavigateTo", "Navigate to a position using the NavMesh." },
                { "Rotate", "Rotate continuously around a local axis." },
                { "RotateTo", "Turn toward a target around a pivot." },
                { "Push", "Apply force in the chosen direction." },
                { "PushTo", "Apply force toward a world position." },
                { "Torque", "Apply rotational force to this actor." },
                { "QuitGame", "Close the running game." },
                { "LoadScene", "Load the first game scene again." }
            };

        private static readonly Dictionary<string, string> ConditionDescriptions =
            new Dictionary<string, string>
            {
                { "Compare", "Compare two properties, numbers, or expressions." },
                { "Check", "Test whether a Boolean property is true." },
                { "Collision", "Detect a collision with an actor carrying a tag." },
                { "Keyboard", "Detect a keyboard key event." },
                { "Touch", "Detect a pointer or touch event." },
                { "Timer", "Become true each time the interval elapses." }
            };

        public static List<TooltipDropdownOption> ActionOptions(IEnumerable<string> names)
        {
            return names.Select(name => new TooltipDropdownOption(name, name, Action(name))).ToList();
        }

        public static List<TooltipDropdownOption> ConditionOptions(IEnumerable<string> names)
        {
            return names.Select(name => new TooltipDropdownOption(name, name, Condition(name))).ToList();
        }

        public static List<TooltipDropdownOption> LogicalOptions()
        {
            return new List<TooltipDropdownOption>
            {
                new TooltipDropdownOption("AND", "AND", Logical("AND")),
                new TooltipDropdownOption("OR", "OR", Logical("OR"))
            };
        }

        public static List<TooltipDropdownOption> ComparisonOptions()
        {
            return new List<TooltipDropdownOption>
            {
                new TooltipDropdownOption("<", "<", "Property 1 is less than Property 2."),
                new TooltipDropdownOption("<=", "<=", "Property 1 is less than or equal to Property 2."),
                new TooltipDropdownOption("==", "==", "Both properties have the same value."),
                new TooltipDropdownOption("!=", "!=", "The properties have different values."),
                new TooltipDropdownOption(">=", ">=", "Property 1 is greater than or equal to Property 2."),
                new TooltipDropdownOption(">", ">", "Property 1 is greater than Property 2.")
            };
        }

        public static List<TooltipDropdownOption> ValueSourceOptions()
        {
            return new List<TooltipDropdownOption>
            {
                new TooltipDropdownOption("Pick Property", "Pick Property",
                    "Choose a property from the property picker."),
                new TooltipDropdownOption("Pick Number", "Pick Number",
                    "Use a numeric value.")
            };
        }

        public static List<TooltipDropdownOption> ArithmeticOptions()
        {
            return new List<TooltipDropdownOption>
            {
                new TooltipDropdownOption("+", "+", "Add the following property or number."),
                new TooltipDropdownOption("-", "−", "Subtract the following property or number.")
            };
        }

        public static string Action(string name)
        {
            return ActionDescriptions.TryGetValue(name ?? string.Empty, out string description)
                ? description
                : "Run this action.";
        }

        public static string Condition(string name)
        {
            return ConditionDescriptions.TryGetValue(name ?? string.Empty, out string description)
                ? description
                : "Evaluate this condition.";
        }

        public static string Logical(string value)
        {
            return value == "OR"
                ? "At least one connected condition must be true."
                : "All connected conditions must be true.";
        }

        public static string Parameter(string owner, string label)
        {
            string key = (owner ?? string.Empty) + "." + (label ?? string.Empty);
            switch (key)
            {
                case "Edit.Property": return "Property that will receive the new value.";
                case "Edit.Value": return "New value or one-step addition or subtraction.";
                case "Spawn.Prefab": return "Prefab that will be created.";
                case "Animate.Animation": return "Animation clip to play.";
                case "PlaySound.Sound": return "Audio clip to play.";
                case "PlayParticles.Particle System": return "Particle effect to play.";
                case "Check.Boolean Property": return "Boolean property whose value will be checked.";
                case "Collision.Tag": return "Tag that the other colliding actor must have.";
                case "Timer.Seconds": return "Seconds between each successful timer check.";
                case "Keyboard.Key": return "Keyboard key to monitor.";
                case "Keyboard.Event": return "Key event that must occur: held, pressed, or released.";
                case "Touch.Event": return "Pointer or touch event that must occur.";
                case "Touch.On This Actor": return "Require the pointer or touch to be over this actor.";
                case "Compare.Property 1": return "Left side of the comparison.";
                case "Compare.Property 2": return "Right side of the comparison.";
            }

            if (label == "Speed") return "Movement speed.";
            if (label == "Turn Speed") return "Rotation speed in degrees per second.";
            if (label == "Force") return "Amount of force to apply.";
            if (label == "Prefab") return "Prefab that will be created.";
            if (label != null && label.StartsWith("Offset "))
                return "Spawn offset on the " + label.Substring(label.Length - 1) + " axis.";
            if (label != null && label.StartsWith("Target "))
                return "World-space " + label.Substring(label.Length - 1) + " coordinate of the target.";
            if (label != null && label.StartsWith("Pivot "))
                return "World-space " + label.Substring(label.Length - 1) + " coordinate of the pivot.";
            if (label != null && label.StartsWith("Torque "))
                return "Rotational force around the " + label.Substring(label.Length - 1) + " axis.";
            if (label != null && label.StartsWith("Rotation "))
            {
                string axis = label.Substring(label.Length - 1);
                if (owner == "Rotate") return axis + " component of the local rotation axis.";
                if (owner == "Spawn") return "Spawn rotation offset around the " + axis + " axis, in degrees.";
                return "Movement direction angle around the " + axis + " axis, in degrees.";
            }

            return "Value used by this rule.";
        }
    }
}
