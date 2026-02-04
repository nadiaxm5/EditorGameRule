using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Text.RegularExpressions;

namespace GameRuleEditor.CustomControls
{
    public class ConditionElement : VisualElement
    {
        private EditorContext context;
        private PopupField<string> typeDropdown;
        private Label operatorLabel;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<VisualElement> inputElements = new List<VisualElement>();
        public System.Action OnChanged;
        public System.Action OnRemove;
        private bool isOperator = false;

        public ConditionElement(EditorContext ctx, List<string> conditionTypes, string specificType = null)
        {
            context = ctx;
            availableTypes = conditionTypes;
            style.flexDirection = FlexDirection.Row; style.marginBottom = 5;
            style.borderTopLeftRadius = 3; style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3; style.borderBottomRightRadius = 3;
            style.paddingTop = 5; style.paddingBottom = 5; style.paddingLeft = 5; style.paddingRight = 5;
            style.alignItems = Align.Center;
            CreateUI(specificType);
        }

        private void CreateUI(string specificType)
        {
            isOperator = (specificType == "AND" || specificType == "OR" || specificType == "NOT");
            if (isOperator)
            {
                style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                style.borderLeftColor = new Color(0.8f, 0.6f, 0.2f); style.borderLeftWidth = 3;
                operatorLabel = new Label(specificType) { style = { unityFontStyleAndWeight = FontStyle.Bold, width = 50, unityTextAlign = TextAnchor.MiddleCenter, color = new Color(1f, 0.9f, 0.7f) } };
                Add(operatorLabel);
            }
            else
            {
                style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                typeDropdown = new PopupField<string>(availableTypes, 0) { style = { width = 90 } };
                if (!string.IsNullOrEmpty(specificType) && availableTypes.Contains(specificType)) typeDropdown.SetValueWithoutNotify(specificType);
                typeDropdown.RegisterValueChangedCallback(evt => { UpdateParameterFields(true); OnChanged?.Invoke(); });
                Add(typeDropdown);
                parametersContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, alignItems = Align.Center } };
                Add(parametersContainer);
                UpdateParameterFields(false);
            }
            Add(new VisualElement() { style = { flexGrow = 1 } });
            var removeBtn = new Button(() => OnRemove?.Invoke()) { text = "X" };
            removeBtn.AddToClassList("button-danger"); removeBtn.style.width = 20;
            Add(removeBtn);
        }

        public void SetFromSource(string token)
        {
            if (isOperator) return;
            var result = GameRuleParser.ParseFunction(token);
            if (availableTypes.Contains(result.Name))
            {
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateParameterFields(false);
                FillFieldsFromParams(result.Name, result.Params);
            }
        }

        private void UpdateParameterFields(bool notifyChange = true)
        {
            if (isOperator) return;
            parametersContainer.Clear();
            inputElements.Clear();
            string type = typeDropdown.value;

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
                    var tags = new List<string>(UnityEditorInternal.InternalEditorUtility.tags);
                    var tagDrop = new PopupField<string>(tags, 0) { style = { flexGrow = 1 } };
                    tagDrop.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
                    parametersContainer.Add(tagDrop); inputElements.Add(tagDrop);
                    break;

                case "Timer": AddParameterField("Seconds"); break;

                case "Touch":
                    var touchModes = new List<string> { "press", "down", "up", "tap" };
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
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 40 } };
            var field = new TextField() { style = { flexGrow = 1 } };
            var label = new Label(placeholder) { style = { fontSize = 8, color = new Color(0.6f, 0.6f, 0.6f), position = Position.Absolute, left = 2, top = 2 }, pickingMode = PickingMode.Ignore };
            field.Add(label);
            field.RegisterValueChangedCallback(evt => { label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None; OnChanged?.Invoke(); });
            container.Add(field);

            if (showPicker)
            {
                var pickBtn = new Button(() =>
                {
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) => { field.value = picked; OnChanged?.Invoke(); }, boolOnly);
                })
                { text = "°", style = { width = 18, height = 18, fontSize = 10, marginLeft = 0 } };
                container.Add(pickBtn);
            }
            parametersContainer.Add(container); inputElements.Add(field);
        }

        private void FillFieldsFromParams(string type, List<string> p)
        {
            if (p == null || p.Count == 0) return;
            if (type == "Compare")
            {
                string fullExpr = p[0];
                var match = Regex.Match(fullExpr, @"(.+?)\s*(<=|>=|==|!=|<|>)\s*(.+)");
                if (match.Success) { ((TextField)inputElements[0]).value = match.Groups[1].Value.Trim(); ((PopupField<string>)inputElements[1]).value = match.Groups[2].Value.Trim(); ((TextField)inputElements[2]).value = match.Groups[3].Value.Trim(); }
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
            if (isOperator) return operatorLabel.text;
            string type = typeDropdown.value;
            List<string> parts = new List<string>();

            if (type == "Compare")
            {
                string v1 = ((TextField)inputElements[0]).value; string op = ((PopupField<string>)inputElements[1]).value; string v2 = ((TextField)inputElements[2]).value;
                return $"Compare({v1} {op} {v2})";
            }
            foreach (var el in inputElements)
            {
                if (el is TextField tf) parts.Add(tf.value);
                else if (el is PopupField<string> pf) parts.Add(pf.value);
                else if (el is Toggle tg) parts.Add(tg.value.ToString().ToLower());
            }
            return $"{type}({string.Join(",", parts)})";
        }
    }
}