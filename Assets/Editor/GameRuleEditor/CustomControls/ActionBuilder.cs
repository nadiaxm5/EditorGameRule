using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using GameRuleEditor.Core;

namespace GameRuleEditor.CustomControls
{
    public class ActionBuilder : VisualElement
    {
        private List<string> actionTypes;

        private List<ActionElement> actions = new List<ActionElement>();
        private VisualElement actionsContainer;
        private Label previewLabel;
        private EditorContext context;

        public System.Action<List<string>> OnActionsChanged;

        public ActionBuilder(EditorContext editorContext)
        {
            context = editorContext;

            actionTypes = new List<string>();
            MethodInfo[] methods = typeof(global::Action).GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.ReturnType == typeof(void))
                {
                    actionTypes.Add(method.Name);
                }
            }

            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderTopLeftRadius = 5; style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5; style.borderBottomRightRadius = 5;
            style.borderLeftWidth = 1; style.borderRightWidth = 1;
            style.borderTopWidth = 1; style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);
            style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            style.paddingTop = 10; style.paddingBottom = 10;
            style.paddingLeft = 10; style.paddingRight = 10;

            CreateUI();
        }

        private void CreateUI()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 10;

            var label = new Label("DO (Actions)");
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            var addButton = new Button(() => AddAction(null));
            addButton.text = "+ Add Action";
            addButton.AddToClassList("button-primary");
            addButton.style.height = 25;
            header.Add(addButton);

            Add(header);

            actionsContainer = new VisualElement();
            Add(actionsContainer);
/*
            var previewContainer = new VisualElement();
            previewContainer.style.marginTop = 10;
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            previewContainer.style.paddingTop = 8; previewContainer.style.paddingBottom = 8;
            previewContainer.style.paddingLeft = 8; previewContainer.style.paddingRight = 8;

            var previewTitle = new Label("Preview:");
            previewTitle.style.fontSize = 10;
            previewTitle.style.color = new Color(0.7f, 0.7f, 0.7f);
            previewContainer.Add(previewTitle);

            previewLabel = new Label("");
            previewLabel.style.fontSize = 11;
            previewLabel.style.color = new Color(0.8f, 0.9f, 1f);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewContainer.Add(previewLabel);

            Add(previewContainer); */
        }

        private void AddAction(string actionString = null)
        {
            var actionElement = new ActionElement(context, actionTypes);

            if (!string.IsNullOrEmpty(actionString))
            {
                actionElement.SetFromSource(actionString);
            }

            //actionElement.OnChanged += UpdatePreview;
            actionElement.OnRemove += () => RemoveAction(actionElement);
            actionElement.OnMoveUp += () => MoveActionUp(actionElement);
            actionElement.OnMoveDown += () => MoveActionDown(actionElement);

            actions.Add(actionElement);
            actionsContainer.Add(actionElement);

            //UpdatePreview();
        }

        private void RemoveAction(ActionElement element)
        { actions.Remove(element); actionsContainer.Remove(element); //UpdatePreview();
        }

        private void MoveActionUp(ActionElement element)
        {
            int index = actions.IndexOf(element);
            if (index <= 0) return;
            actions.RemoveAt(index); actions.Insert(index - 1, element);
            actionsContainer.Remove(element); actionsContainer.Insert(index - 1, element);
            //UpdatePreview();
        }

        private void MoveActionDown(ActionElement element)
        {
            int index = actions.IndexOf(element);
            if (index < 0 || index >= actions.Count - 1) return;
            actions.RemoveAt(index); actions.Insert(index + 1, element);
            actionsContainer.Remove(element); actionsContainer.Insert(index + 1, element);
            //UpdatePreview();
        }

        private void UpdatePreview()
        {
            List<string> actionStrings = BuildActionStrings();
            previewLabel.text = actionStrings.Count == 0 ? "(no actions)" : string.Join(", ", actionStrings);
            OnActionsChanged?.Invoke(actionStrings);
        }

        private List<string> BuildActionStrings()
        {
            List<string> result = new List<string>();
            foreach (var action in actions)
            {
                string actionStr = action.GetActionString();
                if (!string.IsNullOrEmpty(actionStr)) result.Add(actionStr);
            }
            return result;
        }

        public void SetActions(List<string> actionStrings)
        {
            foreach (var action in actions) actionsContainer.Remove(action);
            actions.Clear();

            if (actionStrings == null || actionStrings.Count == 0)
            {
                //UpdatePreview();
                return;
            }

            foreach (var actionStr in actionStrings)
            {
                AddAction(actionStr);
            }
        }
    }
}