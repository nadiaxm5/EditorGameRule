using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameRuleEditor.CustomControls
{
    internal static class CompactRuleValue
    {
        internal static TooltipPopupField CreateMenu(string display, System.Action<string, VisualElement> choose,
            bool alongsideNumber = false)
        {
            var menu = new TooltipPopupField(RuleTooltips.ValueSourceOptions(),
                string.IsNullOrEmpty(display) ? "Select value" : display,
                "Choose a property or number.", RuleDropdownPalette.Property);
            menu.AddToClassList("compact-value-menu");
            menu.AddToClassList("button-property-picker");
            menu.AddToClassList("parameter-selector");
            if (alongsideNumber)
            {
                menu.text = "▾";
                menu.AddToClassList("compact-number-menu");
            }
            menu.RegisterValueChangedCallback(choice =>
            {
                menu.SetValueWithoutNotify(string.IsNullOrEmpty(display) ? "Select value" : display);
                if (alongsideNumber) menu.text = "▾";
                choose(choice, menu);
            });
            return menu;
        }

        internal static VisualElement CreateRow(string className)
        {
            var row = new VisualElement();
            row.AddToClassList(className);
            return row;
        }

        internal static void AlignLabel(Label label)
        {
            label.AddToClassList("detail-parameter-label");
            label.style.marginRight = StyleKeyword.Null;
            label.style.minWidth = StyleKeyword.Null;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        internal static VisualElement WrapParameter(VisualElement control)
        {
            var column = CreateRow("detail-parameter-value");
            column.Add(control);
            return column;
        }

        internal static Button CreateRemove(string tooltip, System.Action remove)
        {
            var button = new Button(remove) { text = "×", tooltip = tooltip };
            button.AddToClassList("button-danger");
            button.AddToClassList("action-clear-value");
            return button;
        }

        internal static void RebuildExpression(VisualElement container, int count, bool canAdd,
            Func<int, VisualElement> createTerm, Func<int, string> getOperator,
            System.Action<int, string> changeOperator, System.Action<string> add, System.Action clear)
        {
            container.Clear();
            var line = CreateRow("compact-expression-line");
            container.Add(line);
            for (int i = 0; i < Math.Max(1, count); i++)
            {
                int index = i;
                var term = CreateRow("compact-expression-term");
                if (index > 0)
                {
                    var operation = new TooltipPopupField(RuleTooltips.ArithmeticOptions(), getOperator(index),
                        "Change this arithmetic operation.", RuleDropdownPalette.Number);
                    operation.AddToClassList("button-number-picker");
                    operation.AddToClassList("parameter-selector");
                    operation.AddToClassList("compact-expression-operator");
                    operation.RegisterValueChangedCallback(value => changeOperator(index, value));
                    term.Add(operation);
                }
                term.Add(createTerm(index));
                if (count > 0 && index == count - 1)
                {
                    if (canAdd)
                    {
                        var addOperation = new TooltipPopupField(RuleTooltips.ArithmeticOptions(), "+ / −",
                            "Add another operand.", RuleDropdownPalette.Number);
                        addOperation.AddToClassList("button-number-picker");
                        addOperation.AddToClassList("parameter-selector");
                        addOperation.AddToClassList("compact-expression-operator");
                        addOperation.RegisterValueChangedCallback(add);
                        term.Add(addOperation);
                    }
                    term.Add(CreateRemove("Clear this expression", clear));
                }
                line.Add(term);
            }
        }

        internal static void FocusNumber(VisualElement stepper)
        {
            var field = stepper.Q<FloatField>();
            if (field != null)
                field.schedule.Execute(() =>
                {
                    var input = field.Q(className: "unity-text-field__input");
                    if (input != null) input.Focus();
                    else field.Focus();
                });
        }
    }
}
