using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Text.RegularExpressions;
using System.Linq; // Required for Sorting/ToList

namespace GameRuleEditor.CustomControls
{
    public class ConditionElement : VisualElement
    {
        private EditorContext context;
        private PopupField<string> typeDropdown;
        private Label negationIcon;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<string> dropdownTypes;
        private List<VisualElement> inputElements = new List<VisualElement>();
        public System.Action OnChanged;
        public System.Action OnRemove;
        private bool isNegated;
        private string selectedConditionType;
        private string joinOperatorBefore;

        public string JoinOperatorBefore => joinOperatorBefore;

        public ConditionElement(EditorContext ctx, List<string> conditionTypes, string joinOperatorBefore = null)
        {
            context = ctx;
            availableTypes = conditionTypes;
            dropdownTypes = new List<string> { "Negate" };
            dropdownTypes.AddRange(conditionTypes);
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

            negationIcon = new Label("!");
            negationIcon.style.width = 16;
            negationIcon.style.marginRight = 4;
            negationIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            negationIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            negationIcon.style.color = new Color(0.95f, 0.2f, 0.2f);
            Add(negationIcon);

            selectedConditionType = availableTypes.Count > 0 ? availableTypes[0] : "Compare";
            typeDropdown = new PopupField<string>(dropdownTypes, selectedConditionType) { style = { width = 110 } };
            typeDropdown.style.flexShrink = 0;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "Negate")
                {
                    isNegated = !isNegated;
                    UpdateNegationIcon();
                    typeDropdown.SetValueWithoutNotify(selectedConditionType);
                    OnChanged?.Invoke();
                    return;
                }

                selectedConditionType = evt.newValue;
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

            UpdateNegationIcon();
            UpdateParameterFields(false);
        }

        public void SetJoinOperator(string joinOperator)
        {
            joinOperatorBefore = string.IsNullOrEmpty(joinOperator) ? null : joinOperator;
        }

        public void SetNegated(bool negated)
        {
            isNegated = negated;
            UpdateNegationIcon();
        }

        private void UpdateNegationIcon()
        {
            negationIcon.style.display = isNegated ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetFromSource(string token)
        {
            var result = GameRuleParser.ParseFunction(token);
            if (availableTypes.Contains(result.Name))
            {
                selectedConditionType = result.Name;
                typeDropdown.SetValueWithoutNotify(result.Name);
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
                    AddParameterField("Value 1", true, false);
                    var operators = new List<string> { "<", "<=", "==", "!=", ">=", ">" };
                    var opDropdown = new PopupField<string>(operators, 0) { style = { width = 45 } };
                    opDropdown.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(opDropdown); inputElements.Add(opDropdown);
                    AddParameterField("Value 2", true, false);
                    break;

                case "Check": AddParameterField("Boolean Var", true, true); break;

                case "Collision":
                    // Start with Unity tags
                    HashSet<string> allTags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);

                    // Add tags from current project actors
                    if (context?.currentProject?.actors != null)
                    {
                        foreach (var actor in context.currentProject.actors)
                        {
                            if (!string.IsNullOrEmpty(actor.Tag)) allTags.Add(actor.Tag);
                        }
                    }

                    var sortedTags = allTags.ToList();
                    sortedTags.Sort();

                    var tagDrop = new PopupField<string>(sortedTags, 0) { style = { flexGrow = 1 } };
                    tagDrop.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tagDrop); inputElements.Add(tagDrop);
                    break;

                case "Timer": AddParameterField("Seconds"); break;

                case "Touch":
                    var touchModes = new List<string> { "press", "down", "up", "tap", "isOver" };
                    var tMode = new PopupField<string>(touchModes, 0) { style = { width = 70 } };
                    tMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tMode); inputElements.Add(tMode);

                    var onActorToggle = new Toggle("On Actor");
                    onActorToggle.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(onActorToggle); inputElements.Add(onActorToggle);
                    break;

                case "Keyboard":
                    AddParameterField("Key (e.g. Space)");
                    var keyModes = new List<string> { "press", "down", "up" };
                    var kMode = new PopupField<string>(keyModes, 0) { style = { width = 70 } };
                    kMode.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(kMode); inputElements.Add(kMode);
                    break;
            }
            if (notifyChange) OnChanged?.Invoke();
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
                var pickBtn = CreatePickerButton(() =>
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) => { field.value = picked; OnChanged?.Invoke(); }, boolOnly);
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

        private Button CreatePickerButton(System.Action onClick)
        {
            var pickBtn = new Button(onClick) { text = "" };
            pickBtn.AddToClassList(ObjectField.selectorUssClassName);
            pickBtn.style.width = 20;
            pickBtn.style.height = 20;
            pickBtn.style.marginLeft = 2;
            pickBtn.style.flexShrink = 0;

            var selectorImage = new VisualElement();
            selectorImage.AddToClassList("unity-object-field__selector-image");
            pickBtn.Add(selectorImage);
            pickBtn.tooltip = "Pick value";

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