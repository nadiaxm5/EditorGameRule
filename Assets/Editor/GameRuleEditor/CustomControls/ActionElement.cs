using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ActionElement : VisualElement
    {
        private EditorContext context;
        private PopupField<string> typeDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;
        private List<VisualElement> inputElements = new List<VisualElement>();

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
                typeDropdown.SetValueWithoutNotify(result.Name);
                UpdateParameterFields();

                for (int i = 0; i < inputElements.Count && i < result.Params.Count; i++)
                {
                    if (inputElements[i] is TextField tf)
                    {
                        tf.SetValueWithoutNotify(result.Params[i]);
                    }
                }
            }
        }

        private void CreateUI()
        {
            var mainRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart } };
            mainRow.style.flexShrink = 0;

            typeDropdown = new PopupField<string>(availableTypes, 0) { style = { width = 110, marginRight = 5 } };
            typeDropdown.style.flexShrink = 0;
            typeDropdown.RegisterValueChangedCallback(evt => { UpdateParameterFields(); OnChanged?.Invoke(); });
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
            inputElements.Clear();
            string type = typeDropdown.value;

            switch (type)
            {
                case "Edit":
                    AddParameterField("Property", true); AddParameterField("Value", true); break;
                case "Spawn":
                    AddParameterField("Prefab", true, false, true);
                    AddParameterField("Spawner", true, false, true);
                    AddParameterField("Pos X", true); AddParameterField("Pos Y", true); AddParameterField("Pos Z", true);
                    AddParameterField("Rot X", true); AddParameterField("Rot Y", true); AddParameterField("Rot Z", true);
                    break;

                case "Animate": AddResourceField<AnimationClip>("Animation Name"); break;
                case "PlaySound": AddResourceField<AudioClip>("Sound Name"); break;
                case "PlayParticles": AddResourceField<ParticleSystem>("Particle Prefab"); break;

                case "Move": AddParameterField("Speed", true); AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "MoveTo": AddParameterField("Speed", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;
                case "NavigateTo": AddParameterField("Speed", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;

                case "Rotate": AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "RotateTo": AddParameterField("Speed", true); AddParameterField("DX", true); AddParameterField("DY", true); AddParameterField("DZ", true); AddParameterField("PivotX", true); AddParameterField("PivotY", true); AddParameterField("PivotZ", true); break;
                case "Torque": AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;

                case "Push": AddParameterField("Force", true); AddParameterField("RX", true); AddParameterField("RY", true); AddParameterField("RZ", true); break;
                case "PushTo": AddParameterField("Force", true); AddParameterField("X", true); AddParameterField("Y", true); AddParameterField("Z", true); break;
            }
        }

        // Standard Text + Picker Button
        private void AddParameterField(string placeholder, bool showPicker = false, bool boolOnly = false, bool actorsOnly = false)
        {
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 140, alignItems = Align.Center } };
            container.style.flexShrink = 0;
            container.style.marginBottom = 4;

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
                    GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (picked) => { field.value = picked; OnChanged?.Invoke(); }, boolOnly, actorsOnly);
                });
                container.Add(pickBtn);
            }
            parametersContainer.Add(container); inputElements.Add(field);
        }

        // [Updated] Now looks identical to AddParameterField but picks resources
        private void AddResourceField<T>(string placeholder) where T : Object
        {
            var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 3, minWidth = 180, alignItems = Align.Center } };
            container.style.flexShrink = 0;
            container.style.marginBottom = 4;

            container.Add(CreateFieldTag(placeholder));

            var textField = new TextField() { style = { flexGrow = 1, minWidth = 140 } };
            textField.style.flexShrink = 0;
            textField.isReadOnly = false;
            textField.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            container.Add(textField);

            var pickBtn = CreatePickerButton(() =>
            {
                GameRuleEditor.Windows.PropertyPickerDialog.Show(context, (name) =>
                {
                    textField.value = name;
                    OnChanged?.Invoke();
                }, resourceFilter: typeof(T));
            });

            container.Add(pickBtn);
            parametersContainer.Add(container);
            inputElements.Add(textField);
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

        public string GetActionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();
            foreach (var el in inputElements)
            {
                if (el is TextField tf) parameters.Add(tf.value);
            }
            if (parameters.Count == 0) return $"{type}()";
            return $"{type}({string.Join(",", parameters)})";
        }
    }
}