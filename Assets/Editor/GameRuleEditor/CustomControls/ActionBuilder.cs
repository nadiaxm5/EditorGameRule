using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace GameRuleEditor.CustomControls
{
    /// <summary>
    /// Visual control for building GameRule actions
    /// </summary>
    public class ActionBuilder : VisualElement
    {
        private List<string> actionTypes = new List<string>
        {
            "Edit",
            "Delete",
            "Spawn",
            "Animate",
            "PlaySound",
            "PlayParticles",
            "Move",
            "MoveTo",
            "NavigateTo",
            "Rotate",
            "RotateTo",
            "Push",
            "PushTo",
            "Torque",
            "LoadScene",
            "QuitGame"
        };

        private List<ActionElement> actions = new List<ActionElement>();
        private VisualElement actionsContainer;
        private Label previewLabel;

        public System.Action<List<string>> OnActionsChanged;

        public ActionBuilder()
        {
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderTopLeftRadius = 5;
            style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5;
            style.borderBottomRightRadius = 5;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            CreateUI();
        }

        private void CreateUI()
        {
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 10;

            var label = new Label("DO (Actions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            var addButton = new Button(() => AddAction());
            addButton.text = "+ Add Action";
            addButton.AddToClassList("button-primary");
            addButton.style.height = 25;
            header.Add(addButton);

            Add(header);

            // Actions container
            actionsContainer = new VisualElement();
            Add(actionsContainer);

            // Preview
            var previewContainer = new VisualElement();
            previewContainer.style.marginTop = 10;
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            previewContainer.style.borderTopLeftRadius = 3;
            previewContainer.style.borderTopRightRadius = 3;
            previewContainer.style.borderBottomLeftRadius = 3;
            previewContainer.style.borderBottomRightRadius = 3;
            previewContainer.style.paddingTop = 8;
            previewContainer.style.paddingBottom = 8;
            previewContainer.style.paddingLeft = 8;
            previewContainer.style.paddingRight = 8;

            var previewTitle = new Label("Preview:");
            previewTitle.style.fontSize = 10;
            previewTitle.style.color = new Color(0.7f, 0.7f, 0.7f);
            previewContainer.Add(previewTitle);

            previewLabel = new Label("");
            previewLabel.style.fontSize = 11;
            previewLabel.style.color = new Color(0.8f, 0.9f, 1f);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewContainer.Add(previewLabel);

            Add(previewContainer);
        }

        private void AddAction()
        {
            var actionElement = new ActionElement(actionTypes);
            actionElement.OnChanged += UpdatePreview;
            actionElement.OnRemove += () => RemoveAction(actionElement);
            actionElement.OnMoveUp += () => MoveActionUp(actionElement);
            actionElement.OnMoveDown += () => MoveActionDown(actionElement);

            actions.Add(actionElement);
            actionsContainer.Add(actionElement);

            UpdatePreview();
        }

        private void RemoveAction(ActionElement element)
        {
            actions.Remove(element);
            actionsContainer.Remove(element);
            UpdatePreview();
        }

        private void MoveActionUp(ActionElement element)
        {
            int index = actions.IndexOf(element);
            if (index <= 0) return;

            actions.RemoveAt(index);
            actions.Insert(index - 1, element);

            actionsContainer.Remove(element);
            actionsContainer.Insert(index - 1, element);

            UpdatePreview();
        }

        private void MoveActionDown(ActionElement element)
        {
            int index = actions.IndexOf(element);
            if (index < 0 || index >= actions.Count - 1) return;

            actions.RemoveAt(index);
            actions.Insert(index + 1, element);

            actionsContainer.Remove(element);
            actionsContainer.Insert(index + 1, element);

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            List<string> actionStrings = BuildActionStrings();

            if (actionStrings.Count == 0)
            {
                previewLabel.text = "(no actions)";
            }
            else
            {
                previewLabel.text = string.Join(", ", actionStrings);
            }

            OnActionsChanged?.Invoke(actionStrings);
        }

        private List<string> BuildActionStrings()
        {
            List<string> result = new List<string>();

            foreach (var action in actions)
            {
                string actionStr = action.GetActionString();
                if (!string.IsNullOrEmpty(actionStr))
                {
                    result.Add(actionStr);
                }
            }

            return result;
        }

        public void SetActions(List<string> actionStrings)
        {
            // Clear existing
            foreach (var action in actions)
            {
                actionsContainer.Remove(action);
            }
            actions.Clear();

            if (actionStrings == null || actionStrings.Count == 0)
            {
                UpdatePreview();
                return;
            }

            // Add actions from list
            foreach (var actionStr in actionStrings)
            {
                var actionElement = new ActionElement(actionTypes);
                actionElement.OnChanged += UpdatePreview;
                actionElement.OnRemove += () => RemoveAction(actionElement);
                actionElement.OnMoveUp += () => MoveActionUp(actionElement);
                actionElement.OnMoveDown += () => MoveActionDown(actionElement);

                // TODO: Parse actionStr and set fields

                actions.Add(actionElement);
                actionsContainer.Add(actionElement);
            }

            UpdatePreview();
        }
    }

    /// <summary>
    /// Individual action element
    /// </summary>
    public class ActionElement : VisualElement
    {
        private PopupField<string> typeDropdown;
        private VisualElement parametersContainer;
        private List<string> availableTypes;

        public System.Action OnChanged;
        public System.Action OnRemove;
        public System.Action OnMoveUp;
        public System.Action OnMoveDown;

        private List<TextField> parameterFields = new List<TextField>();

        public ActionElement(List<string> actionTypes)
        {
            availableTypes = actionTypes;

            style.marginBottom = 5;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;
            style.paddingTop = 5;
            style.paddingBottom = 5;
            style.paddingLeft = 5;
            style.paddingRight = 5;

            CreateUI();
        }

        private void CreateUI()
        {
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;

            // Move buttons
            var moveButtons = new VisualElement();
            moveButtons.style.flexDirection = FlexDirection.Column;
            moveButtons.style.marginRight = 5;

            var upBtn = new Button(() => OnMoveUp?.Invoke());
            upBtn.text = "^";
            upBtn.style.width = 20;
            upBtn.style.height = 15;
            upBtn.style.fontSize = 8;
            upBtn.style.paddingTop = 0;
            upBtn.style.paddingBottom = 0;
            moveButtons.Add(upBtn);

            var downBtn = new Button(() => OnMoveDown?.Invoke());
            downBtn.text = "v";
            downBtn.style.width = 20;
            downBtn.style.height = 15;
            downBtn.style.fontSize = 8;
            downBtn.style.paddingTop = 0;
            downBtn.style.paddingBottom = 0;
            moveButtons.Add(downBtn);

            mainRow.Add(moveButtons);

            // Action type dropdown
            typeDropdown = new PopupField<string>(availableTypes, 0);
            typeDropdown.style.width = 120;
            typeDropdown.style.marginRight = 5;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                UpdateParameterFields();
                OnChanged?.Invoke();
            });
            mainRow.Add(typeDropdown);

            // Parameters container
            parametersContainer = new VisualElement();
            parametersContainer.style.flexDirection = FlexDirection.Row;
            parametersContainer.style.flexGrow = 1;
            mainRow.Add(parametersContainer);

            // Remove button
            var removeBtn = new Button(() => OnRemove?.Invoke());
            removeBtn.text = "X";
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.width = 25;
            removeBtn.style.height = 25;
            mainRow.Add(removeBtn);

            Add(mainRow);

            UpdateParameterFields();
        }

        private void UpdateParameterFields()
        {
            parametersContainer.Clear();
            parameterFields.Clear();

            string type = typeDropdown.value;

            switch (type)
            {
                case "Edit":
                    AddParameterField("Property (e.g., this.x)");
                    AddParameterField("Value");
                    break;

                case "Delete":
                    // No parameters
                    break;

                case "Spawn":
                    AddParameterField("Prefab Name");
                    AddParameterField("Spawner (this/ActorName)");
                    AddParameterField("Offset X");
                    AddParameterField("Offset Y");
                    AddParameterField("Offset Z");
                    break;

                case "Animate":
                case "PlaySound":
                case "PlayParticles":
                    AddParameterField("Name");
                    break;

                case "Move":
                    AddParameterField("Speed");
                    AddParameterField("Angle RX");
                    AddParameterField("Angle RY");
                    AddParameterField("Angle RZ");
                    break;

                case "MoveTo":
                case "NavigateTo":
                    AddParameterField("Speed");
                    AddParameterField("Target X");
                    AddParameterField("Target Y");
                    AddParameterField("Target Z");
                    break;

                case "Rotate":
                    AddParameterField("Angular Speed");
                    AddParameterField("Axis RX");
                    AddParameterField("Axis RY");
                    AddParameterField("Axis RZ");
                    break;

                case "RotateTo":
                    AddParameterField("Angular Speed");
                    AddParameterField("Dir X");
                    AddParameterField("Dir Y");
                    AddParameterField("Dir Z");
                    AddParameterField("Pivot X");
                    AddParameterField("Pivot Y");
                    AddParameterField("Pivot Z");
                    break;

                case "Push":
                    AddParameterField("Force");
                    AddParameterField("Dir RX");
                    AddParameterField("Dir RY");
                    AddParameterField("Dir RZ");
                    break;

                case "PushTo":
                    AddParameterField("Force");
                    AddParameterField("Target X");
                    AddParameterField("Target Y");
                    AddParameterField("Target Z");
                    break;

                case "Torque":
                    AddParameterField("Torque RX");
                    AddParameterField("Torque RY");
                    AddParameterField("Torque RZ");
                    break;

                case "LoadScene":
                case "QuitGame":
                    // No parameters
                    break;
            }
        }

        private void AddParameterField(string placeholder)
        {
            var field = new TextField();
            field.style.flexGrow = 1;
            field.style.marginRight = 3;
            field.style.minWidth = 80;
            field.SetValueWithoutNotify("");

            // Placeholder label
            var label = new Label(placeholder);
            label.style.fontSize = 9;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.position = Position.Absolute;
            label.style.left = 5;
            label.style.top = 3;
            field.Add(label);

            field.RegisterValueChangedCallback(evt =>
            {
                label.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                OnChanged?.Invoke();
            });

            parametersContainer.Add(field);
            parameterFields.Add(field);
        }

        public string GetActionString()
        {
            string type = typeDropdown.value;
            List<string> parameters = new List<string>();

            foreach (var field in parameterFields)
            {
                parameters.Add(field.value);
            }

            // Build action string
            if (parameterFields.Count > 0 && string.IsNullOrEmpty(parameterFields[0].value))
                return "";

            if (parameters.Count == 0)
                return $"{type}(this)";

            string paramsStr = string.Join(",", parameters);
            return $"{type}({paramsStr})";
        }
    }
}