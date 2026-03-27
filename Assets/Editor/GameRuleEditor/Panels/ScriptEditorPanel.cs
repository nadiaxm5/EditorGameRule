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
        
        private HashSet<SentenceJson> collapsedRules = new HashSet<SentenceJson>();
        private HashSet<SentenceJson> initializedRules = new HashSet<SentenceJson>();

        private bool isDraggingRule;
        private Vector2 dragStartPosRule;
        private VisualElement draggedRuleItem;
        private int draggedRuleIndex = -1;
        private VisualElement ruleDragSpacer;

        public ScriptEditorPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            // Subscribe to events
            context.OnActorSelected += OnActorSelected;
            context.OnProjectChanged += UpdateUI;

            // Cleanup when removed from visual tree
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                context.OnActorSelected -= OnActorSelected;
                context.OnProjectChanged -= UpdateUI;
            });
        }

        private void CreateUI()
        {
            // No selection
            noSelectionContainer = new VisualElement();
            noSelectionContainer.style.flexGrow = 1;
            noSelectionContainer.style.justifyContent = Justify.Center;
            noSelectionContainer.style.alignItems = Align.Center;

            var noSelectionLabel = new Label("Select an actor to edit its script rules");
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            noSelectionContainer.Add(noSelectionLabel);

            Add(noSelectionContainer);

            // Script editor container
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.display = DisplayStyle.None;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.contentContainer.style.minWidth = 760;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.FlexStart;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            actorNameLabel = new Label();
            actorNameLabel.style.fontSize = 18;
            actorNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(actorNameLabel);

            scrollView.Add(header);

            var addRuleButton = new Button(AddEmptyRule);
            addRuleButton.text = "+ Add Rule";
            addRuleButton.AddToClassList("button-primary");

            var addRuleRow = new VisualElement();
            addRuleRow.style.flexDirection = FlexDirection.Row;
            addRuleRow.style.justifyContent = Justify.FlexStart;
            addRuleRow.style.alignItems = Align.Center;
            addRuleRow.style.marginBottom = 10;
            addRuleRow.Add(addRuleButton);
            scrollView.Add(addRuleRow);

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

        // Helper method for actor selection
        private void OnActorSelected(int index)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var actor = context.SelectedActor;

            if (actor == null)
            {
                noSelectionContainer.style.display = DisplayStyle.Flex;
                this.Q<ScrollView>().style.display = DisplayStyle.None;
                return;
            }

            if (!context.isUndoRedoRefresh)
            {
                var focused = this.focusController?.focusedElement as VisualElement;
                if (focused != null && rulesContainer.Contains(focused))
                {
                    if (!(focused is Button))
                    {
                        return;
                    }
                }
            }

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
                int ruleIndex = i;
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
            container.style.flexShrink = 0;

            bool hasCondition = rule.When != null && rule.When.Count > 0;
            bool hasActions = rule.Do != null && rule.Do.Count > 0;

            if (string.IsNullOrEmpty(rule.Name))
            {
                rule.Name = $"Rule {ruleIndex + 1}";
                EditorUtility.SetDirty(context.currentProject);
            }

            string currentName = rule.Name;

            if (!initializedRules.Contains(rule))
            {
                initializedRules.Add(rule);
                collapsedRules.Add(rule);
            }

            bool isCollapsed = collapsedRules.Contains(rule);
            var ruleFoldout = new Foldout();
            ruleFoldout.text = currentName;
            ruleFoldout.value = !isCollapsed;
            ruleFoldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    collapsedRules.Remove(rule);
                }
                else
                {
                    collapsedRules.Add(rule);
                }
            });
            container.Add(ruleFoldout);

            var contentContainer = new VisualElement();
            contentContainer.style.marginTop = 8;
            contentContainer.style.flexShrink = 0;
            ruleFoldout.Add(contentContainer);

            var titleField = new TextField();
            titleField.value = currentName;
            titleField.label = string.Empty;
            titleField.style.fontSize = 13;
            titleField.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleField.style.backgroundColor = Color.clear;
            titleField.style.borderTopWidth = 0;
            titleField.style.borderBottomWidth = 0;
            titleField.style.borderLeftWidth = 0;
            titleField.style.borderRightWidth = 0;
            titleField.style.flexGrow = 1;
            titleField.style.marginRight = 6;
            titleField.style.marginLeft = 0;
            titleField.style.minWidth = 120;
            titleField.style.flexShrink = 0;
            titleField.style.unityTextAlign = TextAnchor.MiddleLeft;

            if (titleField.labelElement != null)
            {
                titleField.labelElement.style.display = DisplayStyle.None;
            }

            titleField.RegisterValueChangedCallback(evt =>
            {
                rule.Name = evt.newValue;
                EditorUtility.SetDirty(context.currentProject);
            });

            titleField.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            var foldoutToggle = ruleFoldout.Q<Toggle>();
            if (foldoutToggle != null)
            {
                foldoutToggle.text = "";
                foldoutToggle.style.justifyContent = Justify.FlexStart;
                foldoutToggle.style.alignItems = Align.Center;

                var toggleInput = foldoutToggle.Q(className: Toggle.inputUssClassName);
                if (toggleInput != null)
                {
                    var toggleText = toggleInput.Q(className: Toggle.textUssClassName);
                    if (toggleText != null)
                    {
                        toggleText.style.display = DisplayStyle.None;
                        toggleText.style.width = 0;
                        toggleText.style.minWidth = 0;
                        toggleText.style.flexGrow = 0;
                        toggleText.style.marginLeft = 0;
                        toggleText.style.marginRight = 0;
                    }

                    var dragHandle = new Label("\u2261");
                    dragHandle.name = "rule-drag-handle";
                    dragHandle.tooltip = "Drag to reorder";
                    dragHandle.pickingMode = PickingMode.Position;
                    dragHandle.style.width = 16;
                    dragHandle.style.unityTextAlign = TextAnchor.MiddleCenter;
                    dragHandle.style.color = new Color(0.65f, 0.65f, 0.65f);
                    dragHandle.style.marginLeft = 2;
                    dragHandle.style.marginRight = 4;

                    // PointerDown on the HANDLE — Toggle would swallow it otherwise
                    dragHandle.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        OnRuleDragStart(evt, container, ruleIndex);
                    });

                    toggleInput.Insert(1, dragHandle);

                    titleField.style.marginLeft = 4;
                    toggleInput.Add(titleField);
                }
                else
                {
                    foldoutToggle.Add(titleField);
                }

                var removeIconBtn = new Button(() =>
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
                removeIconBtn.text = string.Empty;
                removeIconBtn.tooltip = "Remove Rule";
                removeIconBtn.AddToClassList("button-danger");
                removeIconBtn.style.width = 22;
                removeIconBtn.style.height = 20;
                removeIconBtn.style.marginLeft = 4;
                removeIconBtn.style.paddingLeft = 0;
                removeIconBtn.style.paddingRight = 0;
                removeIconBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

                var trashImage = new Image();
                trashImage.image = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
                trashImage.style.width = 12;
                trashImage.style.height = 12;
                trashImage.style.alignSelf = Align.Center;
                trashImage.style.unityBackgroundImageTintColor = Color.white;
                removeIconBtn.Add(trashImage);

                foldoutToggle.Add(removeIconBtn);
            }

            // Move/Up/CaptureOut on the CONTAINER — same element as CapturePointer target
            container.RegisterCallback<PointerMoveEvent>(evt => OnRuleDragMove(evt, container));
            container.RegisterCallback<PointerUpEvent>(evt => OnRuleDragEnd(evt, container));
            container.RegisterCallback<PointerCaptureOutEvent>(evt => OnRuleDragEnd(evt, container));

            // Condition builder
            if (hasCondition)
            {
                var conditionBuilder = new ConditionBuilder(context);
                if (rule.When.Count > 0)
                {
                    conditionBuilder.SetCondition(rule.When[0]);
                }
                conditionBuilder.OnConditionChanged += conditionStr =>
                {
                    List<string> conditions = string.IsNullOrEmpty(conditionStr)
                        ? new List<string> { "" }
                        : new List<string> { conditionStr };

                    controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex, conditions);
                };
                conditionBuilder.OnRemoveCondition += () =>
                {
                    controller.RemoveRuleCondition(context.selectedActorIndex, ruleIndex);
                };
                contentContainer.Add(conditionBuilder);
            }
            else
            {
                var addConditionBtn = new Button(() =>
                {
                    controller.AddRuleCondition(context.selectedActorIndex, ruleIndex);
                });
                addConditionBtn.text = "+ Add Condition";
                addConditionBtn.AddToClassList("button-primary");
                addConditionBtn.style.marginBottom = 10;
                addConditionBtn.style.alignSelf = Align.FlexStart;
                contentContainer.Add(addConditionBtn);
            }

            if (hasActions)
            {
                var actionBuilder = new ActionBuilder(context);
                actionBuilder.SetActions(rule.Do);
                actionBuilder.OnActionsChanged += actions =>
                {
                    controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions);
                };
                actionBuilder.style.marginTop = 10;
                contentContainer.Add(actionBuilder);
            }
            else
            {
                var addActionBtn = new Button(() =>
                {
                    controller.AddRuleAction(context.selectedActorIndex, ruleIndex);
                });
                addActionBtn.text = "+ Add Action";
                addActionBtn.AddToClassList("button-success");
                addActionBtn.style.marginBottom = 10;
                addActionBtn.style.alignSelf = Align.FlexStart;
                contentContainer.Add(addActionBtn);
            }

            return container;
        }

        private void AddEmptyRule()
        {
            if (context.selectedActorIndex < 0)
                return;

            controller.AddEmptyRule(context.selectedActorIndex);

            var actor = context.SelectedActor;
            if (actor?.Script != null && actor.Script.Count > 0)
            {
                collapsedRules.Add(actor.Script[actor.Script.Count - 1]);
            }

            UpdateRulesList();
        }

        // ──────────────────────────────────
        //  DRAG AND DROP
        // ──────────────────────────────────

        private void OnRuleDragStart(PointerDownEvent evt, VisualElement ruleContainer, int index)
        {
            if (evt.button != 0 || context.SelectedActor?.Script == null)
                return;

            isDraggingRule = true;
            dragStartPosRule = evt.position;
            draggedRuleItem = ruleContainer;
            draggedRuleIndex = index;

            ruleDragSpacer = new VisualElement();
            ruleDragSpacer.style.height = Mathf.Max(30f, ruleContainer.layout.height);
            ruleDragSpacer.style.marginBottom = ruleContainer.resolvedStyle.marginBottom;
            ruleDragSpacer.style.marginTop = ruleContainer.resolvedStyle.marginTop;
            ruleDragSpacer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.2f);
            ruleDragSpacer.style.borderTopWidth = 2;
            ruleDragSpacer.style.borderBottomWidth = 2;
            ruleDragSpacer.style.borderLeftWidth = 2;
            ruleDragSpacer.style.borderRightWidth = 2;
            ruleDragSpacer.style.borderTopColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            ruleDragSpacer.style.borderBottomColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            ruleDragSpacer.style.borderLeftColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            ruleDragSpacer.style.borderRightColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            ruleDragSpacer.style.borderTopLeftRadius = 5;
            ruleDragSpacer.style.borderTopRightRadius = 5;
            ruleDragSpacer.style.borderBottomLeftRadius = 5;
            ruleDragSpacer.style.borderBottomRightRadius = 5;

            int insertIndex = rulesContainer.IndexOf(ruleContainer);
            if (insertIndex < 0) return;

            rulesContainer.Insert(insertIndex, ruleDragSpacer);

            ruleContainer.style.position = Position.Absolute;
            ruleContainer.style.top = ruleContainer.layout.y;
            ruleContainer.style.left = ruleContainer.layout.x;
            ruleContainer.style.width = ruleContainer.layout.width;
            ruleContainer.style.opacity = 0.8f;
            ruleContainer.BringToFront();

            // Capture on the CONTAINER — same element where Move/Up/CaptureOut are registered
            ruleContainer.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnRuleDragMove(PointerMoveEvent evt, VisualElement ruleContainer)
        {
            if (!isDraggingRule || ruleContainer != draggedRuleItem)
                return;

            float diffY = evt.position.y - dragStartPosRule.y;
            ruleContainer.transform.position = new Vector3(0f, diffY, 0f);

            float draggedCenterY = ruleContainer.layout.y + diffY + (ruleContainer.layout.height / 2f);

            int newTargetIndex = 0;
            foreach (var child in rulesContainer.Children())
            {
                if (child == draggedRuleItem || child == ruleDragSpacer) continue;
                float childCenter = child.layout.y + (child.layout.height / 2f);
                if (draggedCenterY < childCenter) break;
                newTargetIndex++;
            }

            int currentSpacerLogicIndex = 0;
            foreach (var child in rulesContainer.Children())
            {
                if (child == ruleDragSpacer) break;
                if (child == draggedRuleItem) continue;
                currentSpacerLogicIndex++;
            }

            if (newTargetIndex != currentSpacerLogicIndex)
            {
                rulesContainer.Remove(ruleDragSpacer);

                int physicalInsertIndex = 0;
                int logicalCount = 0;
                foreach (var child in rulesContainer.Children())
                {
                    if (logicalCount == newTargetIndex) break;
                    if (child != draggedRuleItem) logicalCount++;
                    physicalInsertIndex++;
                }

                rulesContainer.Insert(physicalInsertIndex, ruleDragSpacer);
            }

            evt.StopPropagation();
        }

        private void OnRuleDragEnd(EventBase evt, VisualElement ruleContainer)
        {
            if (!isDraggingRule || ruleContainer != draggedRuleItem)
                return;

            // Save state and clear BEFORE ReleasePointer to prevent re-entry
            int originalIndex = draggedRuleIndex;
            isDraggingRule = false;
            draggedRuleItem = null;
            draggedRuleIndex = -1;

            // Release pointer capture
            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null)
                ruleContainer.ReleasePointer(pointerEvt.pointerId);

            // Reset visuals
            ruleContainer.transform.position = Vector3.zero;
            ruleContainer.style.opacity = StyleKeyword.Null;
            ruleContainer.style.position = StyleKeyword.Null;
            ruleContainer.style.top = StyleKeyword.Null;
            ruleContainer.style.left = StyleKeyword.Null;
            ruleContainer.style.width = StyleKeyword.Null;

            // Read spacer position to determine target index
            int newIndex = originalIndex;
            if (ruleDragSpacer != null && ruleDragSpacer.parent != null)
            {
                newIndex = 0;
                foreach (var child in rulesContainer.Children())
                {
                    if (child == ruleDragSpacer) break;
                    if (child == ruleContainer) continue;
                    newIndex++;
                }
                ruleDragSpacer.parent.Remove(ruleDragSpacer);
                ruleDragSpacer = null;
            }

            // Only do the actual move on PointerUp (not on CaptureOut cancel)
            if (!(evt is PointerUpEvent))
            {
                UpdateRulesList();
                return;
            }

            // Perform the move using the controller
            var actor = context.SelectedActor;
            if (actor?.Script == null || actor.Script.Count <= 1)
            {
                UpdateRulesList();
                evt.StopPropagation();
                return;
            }

            // newIndex from the spacer is exactly the index we want to insert at
            // in the POST-REMOVAL list, which matches ProjectController's logic perfectly.
            int clampedNewIndex = Mathf.Clamp(newIndex, 0, actor.Script.Count - 1);

            if (originalIndex >= 0 && originalIndex < actor.Script.Count && 
                originalIndex != clampedNewIndex)
            {
                // Force blur to prevent focus guard in UpdateUI from cancelling the redraw
                var focused = this.focusController?.focusedElement;
                if (focused != null) focused.Blur();

                controller.MoveRuleToIndex(context.selectedActorIndex, originalIndex, clampedNewIndex);
                
                // Manually force an update in case FocusGuard killed the controller's update
                UpdateRulesList();
            }
            else
            {
                // In case it didn't move, just refresh visuals
                UpdateRulesList();
            }

            evt.StopPropagation();
        }

    }
}
