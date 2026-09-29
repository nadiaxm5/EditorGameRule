using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ActionElement : VisualElement
    {
        private const string SelectActionLabel = "Select action";

        private EditorContext context;
        private PopupField<string> typeDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private readonly List<ParameterBinding> parameterBindings = new List<ParameterBinding>();
        private string selectedActionType;

        public System.Action OnChanged;
        public System.Action OnRemove;
        public System.Action OnMoveUp;
        public System.Action OnMoveDown;

        public ActionElement(EditorContext ctx, List<string> actionTypes)
        {
            context = ctx;
            availableTypes = actionTypes;
            style.marginBottom = 5;
            style.flexShrink = 0;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            style.paddingTop = 5; style.paddingBottom = 5;
            style.paddingLeft = 5; style.paddingRight = 5;
            CreateUI();
        }

        public void SetFromSource(string actionString)
        {
            var result = GameRuleParser.ParseFunction(actionString);
            if (availableTypes.Contains(result.Name))
            {
                selectedActionType = result.Name;
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateParameterFields();

                foreach (ParameterBinding binding in parameterBindings)
                {
                    string value = binding.SourceIndex < result.Params.Count
                        ? result.Params[binding.SourceIndex]
                        : string.Empty;
                    binding.Control.SetStoredValue(string.IsNullOrWhiteSpace(value)
                        ? binding.DefaultValue ?? string.Empty
                        : value);
                }
            }
        }

        private void CreateUI()
        {
            var mainRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart } };
            mainRow.style.flexShrink = 0;

            selectedActionType = null;
            typeDropdown = new PopupField<string>(availableTypes, 0) { style = { width = 130, marginRight = 5 } };
            typeDropdown.SetValueWithoutNotify(SelectActionLabel);
            typeDropdown.AddToClassList("button-action");
            typeDropdown.AddToClassList("rule-selector-dropdown");
            typeDropdown.style.flexShrink = 0;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (!availableTypes.Contains(evt.newValue)) return;

                selectedActionType = evt.newValue;
                UpdateParameterFields();
                OnChanged?.Invoke();
            });
            mainRow.Add(typeDropdown);

            parametersContainer = new VisualElement() { style = { flexDirection = FlexDirection.Column, flexGrow = 1, flexWrap = Wrap.NoWrap } };
            parametersContainer.style.flexShrink = 0;
            parametersContainer.style.marginRight = 4;
            mainRow.Add(parametersContainer);

            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = string.Empty };
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 28; removeBtn.style.height = 26;

            var trashImage = new Image();
            trashImage.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
            trashImage.style.width = 16;
            trashImage.style.height = 16;
            trashImage.style.alignSelf = Align.Center;
            trashImage.style.unityBackgroundImageTintColor = Color.white;
            removeBtn.Add(trashImage);

            mainRow.Add(removeBtn);

            Add(mainRow);
            UpdateParameterFields();
        }

        private void UpdateParameterFields()
        {
            parametersContainer.Clear();
            parameterBindings.Clear();
            string type = selectedActionType;

            switch (type)
            {
                case "Edit":
                    AddPropertyParameter("Property", 0);
                    AddExpressionParameter("Value", 1);
                    break;
                case "Spawn":
                    AddPrefabParameter("Prefab", 0);
                    AddScalarParameter("Offset X", 2);
                    AddScalarParameter("Offset Y", 3);
                    AddScalarParameter("Offset Z", 4);
                    AddScalarParameter("Rotation X", 5, true);
                    AddScalarParameter("Rotation Y", 6, true);
                    AddScalarParameter("Rotation Z", 7, true);
                    break;

                case "Animate": AddResourceParameter("Animation", 0, typeof(AnimationClip), "Pick Animation", "All Animations"); break;
                case "PlaySound": AddResourceParameter("Sound", 0, typeof(AudioClip), "Pick Sound", "All Sounds"); break;
                case "PlayParticles": AddResourceParameter("Particle System", 0, typeof(ParticleSystem), "Pick Particles", "All Particles"); break;

                case "Move": AddScalarParameter("Speed", 0); AddScalarParameter("Rotation X", 1, true); AddScalarParameter("Rotation Y", 2, true); break;
                case "MoveTo": AddScalarParameter("Speed", 0); AddScalarParameter("Target X", 1); AddScalarParameter("Target Y", 2); AddScalarParameter("Target Z", 3); break;
                case "NavigateTo": AddScalarParameter("Speed", 0); AddScalarParameter("Target X", 1); AddScalarParameter("Target Y", 2); AddScalarParameter("Target Z", 3); break;

                case "Rotate": AddScalarParameter("Turn Speed", 0, true); AddScalarParameter("Rotation X", 1); AddScalarParameter("Rotation Y", 2); AddScalarParameter("Rotation Z", 3); break;
                case "RotateTo": AddScalarParameter("Turn Speed", 0, true); AddScalarParameter("Target X", 1); AddScalarParameter("Target Y", 2); AddScalarParameter("Target Z", 3); AddScalarParameter("Pivot X", 4); AddScalarParameter("Pivot Y", 5); AddScalarParameter("Pivot Z", 6); break;
                case "Torque": AddScalarParameter("Torque X", 0); AddScalarParameter("Torque Y", 1); AddScalarParameter("Torque Z", 2); break;

                case "Push": AddScalarParameter("Force", 0); AddScalarParameter("Rotation X", 1, true); AddScalarParameter("Rotation Y", 2, true); break;
                case "PushTo": AddScalarParameter("Force", 0); AddScalarParameter("Target X", 1); AddScalarParameter("Target Y", 2); AddScalarParameter("Target Z", 3); break;
            }
        }

        private void AddPropertyParameter(string label, int sourceIndex)
        {
            AddParameterRow(label,
                new PropertyValueControl(context, () => OnChanged?.Invoke()), sourceIndex);
        }

        private void AddExpressionParameter(string label, int sourceIndex)
        {
            AddParameterRow(label,
                new ExpressionValueControl(context, ActionDefaults.Get(selectedActionType, sourceIndex),
                    () => OnChanged?.Invoke()), sourceIndex);
        }

        private void AddScalarParameter(string label, int sourceIndex, bool showDegrees = false)
        {
            AddParameterRow(label,
                new ScalarValueControl(context, ActionDefaults.Get(selectedActionType, sourceIndex),
                    showDegrees, () => OnChanged?.Invoke()), sourceIndex);
        }

        private void AddPrefabParameter(string label, int sourceIndex)
        {
            AddParameterRow(label,
                new ResourceValueControl(context, typeof(GameObject), "Pick Prefab", "All Prefabs",
                    true, () => OnChanged?.Invoke()), sourceIndex);
        }

        private void AddResourceParameter(string label, int sourceIndex, Type resourceType,
                                          string pickerLabel, string allResourcesLabel)
        {
            AddParameterRow(label,
                new ResourceValueControl(context, resourceType, pickerLabel, allResourcesLabel,
                    false, () => OnChanged?.Invoke()), sourceIndex);
        }

        private void AddParameterRow(string label, IActionParameterControl control, int sourceIndex)
        {
            var container = new VisualElement();
            container.AddToClassList("action-parameter-row");
            container.Add(CreateFieldTag(label));
            container.Add((VisualElement)control);
            parametersContainer.Add(container);
            parameterBindings.Add(new ParameterBinding(control, sourceIndex,
                ActionDefaults.Get(selectedActionType, sourceIndex)));
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

        public string GetActionString()
        {
            string type = selectedActionType;
            if (string.IsNullOrEmpty(type)) return string.Empty;

            List<string> parameters = new List<string>();
            foreach (ParameterBinding binding in parameterBindings)
                parameters.Add(binding.SerializedValue);

            // Move and Push retain their legacy RZ parameter for JSON/runtime compatibility.
            // The runtime does not use it, so the editor keeps it hidden and writes zero.
            if (type == "Move" || type == "Push") parameters.Add("0");

            // Spawn is always relative to the actor executing the rule. Keep "this" in the
            // serialized action so existing JSON and script generation retain their format.
            if (type == "Spawn") parameters.Insert(1, "this");

            if (parameters.Count == 0) return $"{type}()";
            return $"{type}({string.Join(",", parameters)})";
        }

        private interface IActionParameterControl
        {
            string StoredValue { get; }
            void SetStoredValue(string value);
        }

        private sealed class ParameterBinding
        {
            public readonly IActionParameterControl Control;
            public readonly int SourceIndex;
            public readonly string DefaultValue;
            public string SerializedValue => string.IsNullOrWhiteSpace(Control.StoredValue)
                ? DefaultValue ?? string.Empty
                : Control.StoredValue;

            public ParameterBinding(IActionParameterControl control, int sourceIndex, string defaultValue)
            {
                Control = control;
                SourceIndex = sourceIndex;
                DefaultValue = defaultValue;
            }
        }

        private sealed class PropertyValueControl : VisualElement, IActionParameterControl
        {
            private readonly EditorContext context;
            private readonly System.Action changed;
            private readonly Button valueButton;
            private readonly Button pickerButton;
            private string displayValue;

            public string StoredValue => GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(
                context, displayValue ?? string.Empty);

            public PropertyValueControl(EditorContext context, System.Action changed)
            {
                this.context = context;
                this.changed = changed;
                AddToClassList("action-single-picker");
                valueButton = new Button(() => OpenPicker(valueButton));
                valueButton.AddToClassList("action-picker-value-display");
                Add(valueButton);

                pickerButton = new Button(() => OpenPicker(pickerButton)) { text = "Pick Property", tooltip = "Pick Property" };
                pickerButton.AddToClassList("button-property-picker");
                pickerButton.AddToClassList("action-resource-picker");
                Add(pickerButton);
                UpdateButton();
            }

            public void SetStoredValue(string value)
            {
                displayValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, value?.Trim());
                UpdateButton();
            }

            private void OpenPicker(VisualElement anchorElement)
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    displayValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked);
                    UpdateButton();
                    changed?.Invoke();
                }, false, anchorScreenRect: GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(anchorElement));
            }

            private void UpdateButton()
            {
                bool hasValue = !string.IsNullOrWhiteSpace(displayValue);
                valueButton.text = hasValue ? displayValue : "Empty";
                valueButton.tooltip = hasValue ? displayValue : "No property selected";
                valueButton.EnableInClassList("action-empty-value", !hasValue);
            }
        }

        private sealed class ResourceValueControl : VisualElement, IActionParameterControl
        {
            private readonly EditorContext context;
            private readonly Type resourceType;
            private readonly string pickerLabel;
            private readonly string allResourcesLabel;
            private readonly bool prefabMode;
            private readonly System.Action changed;
            private readonly Button valueButton;
            private readonly Button pickerButton;
            private string value;

            public string StoredValue => value ?? string.Empty;

            public ResourceValueControl(EditorContext context, Type resourceType, string pickerLabel,
                                        string allResourcesLabel, bool prefabMode, System.Action changed)
            {
                this.context = context;
                this.resourceType = resourceType;
                this.pickerLabel = pickerLabel;
                this.allResourcesLabel = allResourcesLabel;
                this.prefabMode = prefabMode;
                this.changed = changed;
                AddToClassList("action-single-picker");
                valueButton = new Button(() => OpenPicker(valueButton));
                valueButton.AddToClassList("action-picker-value-display");
                Add(valueButton);

                pickerButton = new Button(() => OpenPicker(pickerButton)) { text = pickerLabel, tooltip = pickerLabel };
                pickerButton.AddToClassList("button-property-picker");
                pickerButton.AddToClassList("action-resource-picker");
                Add(pickerButton);
                UpdateButton();
            }

            public void SetStoredValue(string storedValue)
            {
                value = storedValue?.Trim() ?? string.Empty;
                UpdateButton();
            }

            private void OpenPicker(VisualElement anchorElement)
            {
                Rect anchor = GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(anchorElement);
                if (prefabMode)
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.ShowPrefab(context, picked =>
                    {
                        value = picked;
                        UpdateButton();
                        changed?.Invoke();
                    }, includeActorPrefabs: true, anchorScreenRect: anchor);
                    return;
                }

                GameRuleEditor.Windows.PropertyPickerDialog.ShowResource(context, picked =>
                {
                    value = picked;
                    UpdateButton();
                    changed?.Invoke();
                }, resourceType, pickerLabel, allResourcesLabel, anchor);
            }

            private void UpdateButton()
            {
                bool hasValue = !string.IsNullOrWhiteSpace(value);
                valueButton.text = hasValue ? value : "Empty";
                valueButton.tooltip = hasValue ? value : $"No {pickerLabel.Replace("Pick ", string.Empty).ToLowerInvariant()} selected";
                valueButton.EnableInClassList("action-empty-value", !hasValue);
            }
        }

        private enum ScalarKind { Empty, Property, Number, Legacy }

        private sealed class ScalarValueControl : VisualElement, IActionParameterControl
        {
            private const string PickValueLabel = "Pick Property / Number";
            private readonly EditorContext context;
            private readonly string defaultValue;
            private readonly bool showDegrees;
            private readonly System.Action changed;
            private readonly PopupField<string> picker;
            private readonly VisualElement selectionContainer;
            private ScalarKind kind;
            private string textValue;
            private float numberValue;

            public string StoredValue
            {
                get
                {
                    if (kind == ScalarKind.Number) return FormatNumber(numberValue);
                    if (kind == ScalarKind.Property || kind == ScalarKind.Legacy)
                        return GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(
                            context, textValue ?? string.Empty);
                    return string.Empty;
                }
            }

            public ScalarValueControl(EditorContext context, string defaultValue, bool showDegrees,
                                      System.Action changed)
            {
                this.context = context;
                this.defaultValue = defaultValue;
                this.showDegrees = showDegrees;
                this.changed = changed;
                AddToClassList("action-scalar-control");

                picker = new PopupField<string>(new List<string> { "Pick Property", "Pick Number" }, 0);
                picker.SetValueWithoutNotify(PickValueLabel);
                picker.AddToClassList("button-property-picker");
                picker.AddToClassList("rule-selector-dropdown");
                picker.AddToClassList("action-value-picker");
                picker.RegisterValueChangedCallback(evt =>
                {
                    string choice = evt.newValue;
                    picker.SetValueWithoutNotify(PickValueLabel);
                    if (choice == "Pick Property") OpenPropertyPicker(picker);
                    else SetNumber(0f, true);
                });
                selectionContainer = new VisualElement();
                selectionContainer.AddToClassList("action-value-selection");
                Add(selectionContainer);
                Add(picker);
                SetStoredValue(defaultValue ?? string.Empty);
            }

            public void SetStoredValue(string value)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(normalized))
                {
                    kind = ScalarKind.Empty;
                    textValue = string.Empty;
                }
                else if (TryParseNumber(normalized, out float number))
                {
                    kind = ScalarKind.Number;
                    numberValue = number;
                }
                else
                {
                    textValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, normalized);
                    kind = Regex.IsMatch(textValue,
                        @"^(?:#[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+)$")
                        ? ScalarKind.Property
                        : ScalarKind.Legacy;
                }
                RebuildSelection();
            }

            private void OpenPropertyPicker(VisualElement anchorElement)
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    kind = ScalarKind.Property;
                    textValue = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked);
                    RebuildSelection();
                    changed?.Invoke();
                }, false, anchorScreenRect:
                    GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(anchorElement));
            }

            private void SetNumber(float value, bool notify)
            {
                kind = ScalarKind.Number;
                numberValue = value;
                RebuildSelection();
                if (notify) changed?.Invoke();
            }

            private void RebuildSelection()
            {
                selectionContainer.Clear();
                if (kind == ScalarKind.Empty) return;

                if (kind == ScalarKind.Number)
                {
                    var number = new ActionNumericStepper(numberValue, showDegrees);
                    number.Changed += () =>
                    {
                        numberValue = number.Value;
                        changed?.Invoke();
                    };
                    selectionContainer.Add(number);
                }
                else
                {
                    Button valueButton = null;
                    valueButton = new Button(() => OpenPropertyPicker(valueButton))
                    {
                        text = showDegrees ? textValue + "°" : textValue,
                        tooltip = kind == ScalarKind.Legacy
                            ? "Existing expression. Pick a property or number to replace it."
                            : "Click to replace this property"
                    };
                    valueButton.AddToClassList(kind == ScalarKind.Legacy
                        ? "action-legacy-value"
                        : "action-selected-value");
                    selectionContainer.Add(valueButton);
                }

                var clear = new Button(() =>
                {
                    SetStoredValue(defaultValue ?? string.Empty);
                    changed?.Invoke();
                }) { text = "×", tooltip = defaultValue == null ? "Clear value" : "Reset to default" };
                clear.AddToClassList("button-danger");
                clear.AddToClassList("action-clear-value");
                selectionContainer.Add(clear);
            }
        }

        private sealed class ExpressionValueControl : VisualElement, IActionParameterControl
        {
            private const string PickValueLabel = "Pick Property / Number";
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
            private readonly string defaultValue;
            private readonly System.Action changed;
            private readonly List<ExpressionTerm> terms = new List<ExpressionTerm>();
            private readonly PopupField<string> valuePicker;
            private readonly PopupField<string> operationPicker;
            private readonly VisualElement selectionContainer;
            private string pendingOperator;
            private bool lockedLegacyExpression;

            public string StoredValue
            {
                get
                {
                    var result = new StringBuilder();
                    for (int i = 0; i < terms.Count; i++)
                    {
                        ExpressionTerm term = terms[i];
                        if (i > 0) result.Append(' ').Append(term.OperatorBefore).Append(' ');
                        if (term.Kind == TermKind.Number) result.Append(FormatNumber(term.Number));
                        else result.Append(GameRuleEditor.Windows.PropertyPickerDialog.ToStoredReference(
                            context, term.Text));
                    }
                    return result.ToString();
                }
            }

            public ExpressionValueControl(EditorContext context, string defaultValue, System.Action changed)
            {
                this.context = context;
                this.defaultValue = defaultValue;
                this.changed = changed;
                AddToClassList("action-expression-control");

                valuePicker = new PopupField<string>(new List<string> { "Pick Property", "Pick Number" }, 0);
                valuePicker.SetValueWithoutNotify(PickValueLabel);
                valuePicker.AddToClassList("button-property-picker");
                valuePicker.AddToClassList("rule-selector-dropdown");
                valuePicker.AddToClassList("action-value-picker");
                valuePicker.RegisterValueChangedCallback(evt =>
                {
                    string choice = evt.newValue;
                    valuePicker.SetValueWithoutNotify(PickValueLabel);
                    if (choice == "Pick Property") PickPropertyForToolbar();
                    else ApplyPickedTerm(new ExpressionTerm { Kind = TermKind.Number, Number = 0f });
                });
                operationPicker = new PopupField<string>(new List<string> { "+", "-" }, 0);
                operationPicker.SetValueWithoutNotify(PickOperationLabel);
                operationPicker.AddToClassList("button-number-picker");
                operationPicker.AddToClassList("rule-selector-dropdown");
                operationPicker.AddToClassList("action-operation-picker");
                operationPicker.RegisterValueChangedCallback(evt =>
                {
                    if (terms.Count == 1 && pendingOperator == null && !lockedLegacyExpression)
                    {
                        pendingOperator = evt.newValue;
                        RebuildSelection();
                    }
                    operationPicker.SetValueWithoutNotify(PickOperationLabel);
                });
                selectionContainer = new VisualElement();
                selectionContainer.AddToClassList("action-expression-selection");
                Add(selectionContainer);
                Add(valuePicker);
                Add(operationPicker);
                SetStoredValue(defaultValue ?? string.Empty);
            }

            public void SetStoredValue(string value)
            {
                terms.Clear();
                pendingOperator = null;
                lockedLegacyExpression = false;
                ParseExpression(value);
                RebuildSelection();
            }

            private void PickPropertyForToolbar()
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, picked =>
                {
                    ApplyPickedTerm(new ExpressionTerm
                    {
                        Kind = TermKind.Property,
                        Text = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, picked)
                    });
                }, false, anchorScreenRect:
                    GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(valuePicker));
            }

            private void ReplaceTermWithProperty(int index, VisualElement anchor)
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
                    RebuildSelection();
                    changed?.Invoke();
                }, false, anchorScreenRect:
                    GameRuleEditor.Windows.PropertyPickerDialog.GetScreenRect(anchor));
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
                RebuildSelection();
                changed?.Invoke();
            }

            private void RebuildSelection()
            {
                selectionContainer.Clear();
                bool canAddOperation = terms.Count == 1 && pendingOperator == null && !lockedLegacyExpression;
                operationPicker.SetEnabled(canAddOperation);
                operationPicker.tooltip = canAddOperation
                    ? "Choose + or −, then pick the second property or number"
                    : "Only one + or − operation is allowed";

                if (terms.Count == 0 && pendingOperator == null)
                {
                    var empty = new Label("Choose a property or number");
                    empty.AddToClassList("action-empty-expression");
                    selectionContainer.Add(empty);
                    return;
                }

                for (int i = 0; i < terms.Count; i++)
                {
                    int index = i;
                    ExpressionTerm term = terms[i];
                    var termContainer = new VisualElement();
                    termContainer.AddToClassList("action-expression-term");

                    if (i > 0)
                    {
                        var termOperator = new PopupField<string>(
                            new List<string> { "+", "-" }, term.OperatorBefore == "-" ? 1 : 0);
                        termOperator.AddToClassList("button-number-picker");
                        termOperator.AddToClassList("action-term-operator");
                        termOperator.RegisterValueChangedCallback(evt =>
                        {
                            term.OperatorBefore = evt.newValue;
                            changed?.Invoke();
                        });
                        termContainer.Add(termOperator);
                    }

                    if (term.Kind == TermKind.Number)
                    {
                        var number = new ActionNumericStepper(term.Number);
                        number.Changed += () =>
                        {
                            term.Number = number.Value;
                            changed?.Invoke();
                        };
                        termContainer.Add(number);
                    }
                    else
                    {
                        Button valueButton = null;
                        valueButton = new Button(() => ReplaceTermWithProperty(index, valueButton))
                        {
                            text = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, term.Text),
                            tooltip = term.Kind == TermKind.Legacy
                                ? "Existing expression. Pick a property or number to replace it."
                                : "Click to replace this property"
                        };
                        valueButton.AddToClassList(term.Kind == TermKind.Legacy
                            ? "action-legacy-value"
                            : "action-selected-value");
                        termContainer.Add(valueButton);
                    }

                    selectionContainer.Add(termContainer);
                }

                if (pendingOperator != null)
                {
                    var pending = new Label(pendingOperator);
                    pending.AddToClassList("button-number-picker");
                    pending.AddToClassList("action-pending-operator");
                    selectionContainer.Add(pending);
                }

                if (terms.Count > 0 || pendingOperator != null)
                {
                    var clear = new Button(() =>
                    {
                        SetStoredValue(defaultValue ?? string.Empty);
                        changed?.Invoke();
                    }) { text = "×", tooltip = defaultValue == null ? "Clear expression" : "Reset to default" };
                    clear.AddToClassList("button-danger");
                    clear.AddToClassList("action-clear-value");
                    selectionContainer.Add(clear);
                }
            }

            private void ParseExpression(string expression)
            {
                string normalized = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(
                    context, expression?.Trim());
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
                    if (TryParseNumber(value, out float number))
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
                            @"^(?:#[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+)$");
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
                string pending = null;
                for (int i = 0; i < expression.Length; i++)
                {
                    char character = expression[i];
                    if (character == '(') depth++;
                    else if (character == ')') depth = Math.Max(0, depth - 1);
                    if (depth != 0 || (character != '+' && character != '-')) continue;
                    if (i > 0 && (expression[i - 1] == 'e' || expression[i - 1] == 'E')) continue;
                    if (i == start)
                    {
                        pending = character.ToString();
                        start = i + 1;
                        continue;
                    }
                    if (pending != null && string.IsNullOrWhiteSpace(expression.Substring(start, i - start)))
                    {
                        start = i;
                        continue;
                    }
                    result.Add(new ExpressionPart(pending, expression.Substring(start, i - start)));
                    pending = character.ToString();
                    start = i + 1;
                }
                result.Add(new ExpressionPart(pending, expression.Substring(start)));
                return result;
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

        private sealed class ActionNumericStepper : VisualElement
        {
            private readonly FloatField field;
            public System.Action Changed;
            public float Value => field.value;

            public ActionNumericStepper(float initialValue, bool showDegrees = false)
            {
                AddToClassList("numeric-stepper");
                AddToClassList("action-number-value");
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;

                var decrement = new Button(() => SetValue(field.value - 0.1f)) { text = "▼" };
                decrement.AddToClassList("numeric-stepper-button");
                Add(decrement);

                field = new FloatField { value = initialValue };
                field.AddToClassList("numeric-stepper-field");
                field.RegisterValueChangedCallback(evt =>
                {
                    float sanitized = Sanitize(evt.newValue);
                    if (!Mathf.Approximately(sanitized, evt.newValue)) field.SetValueWithoutNotify(sanitized);
                    Changed?.Invoke();
                });
                Add(field);

                if (showDegrees)
                {
                    var degree = new Label("°") { pickingMode = PickingMode.Ignore };
                    degree.AddToClassList("action-number-degree-suffix");
                    field.Add(degree);
                }

                var increment = new Button(() => SetValue(field.value + 0.1f)) { text = "▲" };
                increment.AddToClassList("numeric-stepper-button");
                Add(increment);
            }

            private void SetValue(float value)
            {
                float sanitized = Sanitize(value);
                if (Mathf.Approximately(field.value, sanitized)) return;
                field.SetValueWithoutNotify(sanitized);
                Changed?.Invoke();
            }

            private static float Sanitize(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
                return (float)Math.Round(value, 4, MidpointRounding.AwayFromZero);
            }
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
}
