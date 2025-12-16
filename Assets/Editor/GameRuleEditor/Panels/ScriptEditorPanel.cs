using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;
using GameRuleEditor.CustomControls;

namespace GameRuleEditor.Panels
{
    /// <summary>
    /// Panel for visually editing actor scripts (when-do rules)
    /// </summary>
    public class ScriptEditorPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        private VisualElement rulesContainer;
        private VisualElement noSelectionContainer;
        private Label actorNameLabel;

        public ScriptEditorPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            // Subscribe to events - SIN paréntesis
            context.OnActorSelected += OnActorSelected;
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            // No selection message
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;

            var noSelectionLabel = new Label("Select an actor to edit its script rules");
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            noSelectionContainer.Add(noSelectionLabel);

            Add(noSelectionContainer);

            // Script editor container (initially hidden)
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.display = DisplayStyle.None;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 15;

            actorNameLabel = new Label();
            actorNameLabel.style.fontSize = 18;
            actorNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(actorNameLabel);

            var addRuleButton = new Button(() => AddRule(true));
            addRuleButton.text = "+ Add Rule (when-do)";
            addRuleButton.AddToClassList("button-primary");
            header.Add(addRuleButton);

            var addUnconditionalButton = new Button(() => AddRule(false));
            addUnconditionalButton.text = "+ Add Unconditional (do)";
            addUnconditionalButton.AddToClassList("button-success");
            header.Add(addUnconditionalButton);

            scrollView.Add(header);

            // Info box
            var infoBox = new VisualElement();
            infoBox.AddToClassList("help-box");
            infoBox.style.marginBottom = 15;

            var infoText = new Label(
                "Rules are evaluated every frame. Conditional rules (when-do) execute only when their conditions are met. " +
                "Unconditional rules (do) execute every frame."
            );
            infoText.style.whiteSpace = WhiteSpace.Normal;
            infoBox.Add(infoText);

            scrollView.Add(infoBox);

            // Rules container
            rulesContainer = new VisualElement();
            scrollView.Add(rulesContainer);

            Add(scrollView);
        }

        // Método helper para manejar la selección de actor
        private void OnActorSelected(int index)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var actor = context.SelectedActor;

            if (actor == null)
            {
                // Show "no selection" message
                noSelectionContainer.style.display = DisplayStyle.Flex;
                this.Q<ScrollView>().style.display = DisplayStyle.None;
                return;
            }

            // Show script editor
            noSelectionContainer.style.display = DisplayStyle.None;
            this.Q<ScrollView>().style.display = DisplayStyle.Flex;

            actorNameLabel.text = $"Script Rules for: {actor.ActorName}";

            UpdateRulesList();
        }

        private void UpdateRulesList()
        {
            rulesContainer.Clear();

            var actor = context.SelectedActor;
            if (actor?.Script == null || actor.Script.Count == 0)
            {
                var emptyLabel = new Label("No rules defined. Add a rule to get started.");
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                emptyLabel.style.fontSize = 12;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                rulesContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < actor.Script.Count; i++)
            {
                int ruleIndex = i; // Capture for closure
                var rule = actor.Script[i];

                var ruleElement = CreateRuleElement(rule, ruleIndex);
                rulesContainer.Add(ruleElement);
            }
        }

        private VisualElement CreateRuleElement(SentenceJson rule, int ruleIndex)
        {
            var container = new VisualElement();
            container.AddToClassList("panel-section");
            container.style.marginBottom = 15;

            // Header with rule number and actions
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;

            bool hasCondition = rule.When != null && rule.When.Count > 0 && !string.IsNullOrEmpty(rule.When[0]);

            var titleLabel = new Label(hasCondition ? $"Rule {ruleIndex + 1}" : $"Unconditional Rule {ruleIndex + 1}");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLabel);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;

            // Move up button
            if (ruleIndex > 0)
            {
                var upBtn = new Button(() => controller.MoveRuleUp(context.selectedActorIndex, ruleIndex));
                upBtn.text = "Up";
                upBtn.style.width = 40;
                upBtn.style.marginRight = 3;
                buttonContainer.Add(upBtn);
            }

            // Move down button
            if (ruleIndex < context.SelectedActor.Script.Count - 1)
            {
                var downBtn = new Button(() => controller.MoveRuleDown(context.selectedActorIndex, ruleIndex));
                downBtn.text = "Down";
                downBtn.style.width = 40;
                downBtn.style.marginRight = 3;
                buttonContainer.Add(downBtn);
            }

            // Duplicate button
            var duplicateBtn = new Button(() => controller.DuplicateRule(context.selectedActorIndex, ruleIndex));
            duplicateBtn.text = "Duplicate";
            duplicateBtn.style.marginRight = 3;
            buttonContainer.Add(duplicateBtn);

            // Remove button
            var removeBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Rule",
                    "Are you sure you want to remove this rule?",
                    "Remove",
                    "Cancel"))
                {
                    controller.RemoveRule(context.selectedActorIndex, ruleIndex);
                }
            });
            removeBtn.text = "Remove";
            removeBtn.AddToClassList("button-danger");
            buttonContainer.Add(removeBtn);

            header.Add(buttonContainer);
            container.Add(header);

            // Condition builder (if has condition)
            if (hasCondition)
            {
                var conditionBuilder = new ConditionBuilder();
                if (rule.When.Count > 0)
                {
                    conditionBuilder.SetCondition(rule.When[0]);
                }
                conditionBuilder.OnConditionChanged += conditionStr =>
                {
                    List<string> conditions = string.IsNullOrEmpty(conditionStr)
                        ? new List<string>()
                        : new List<string> { conditionStr };
                    controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex, conditions);
                };
                container.Add(conditionBuilder);
            }

            // Action builder
            var actionBuilder = new ActionBuilder();
            if (rule.Do != null && rule.Do.Count > 0)
            {
                actionBuilder.SetActions(rule.Do);
            }
            actionBuilder.OnActionsChanged += actions =>
            {
                controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions);
            };
            actionBuilder.style.marginTop = 10;
            container.Add(actionBuilder);

            return container;
        }

        private void AddRule(bool hasCondition)
        {
            if (context.selectedActorIndex < 0)
                return;

            controller.AddRule(context.selectedActorIndex, hasCondition);
        }

        ~ScriptEditorPanel()
        {
            // Desuscribirse - SIN paréntesis
            context.OnActorSelected -= OnActorSelected;
            context.OnProjectChanged -= UpdateUI;
        }
    }
}