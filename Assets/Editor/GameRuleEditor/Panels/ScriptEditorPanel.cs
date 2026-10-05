using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private string groupId;
        private string groupName;

        private VisualElement rulesContainer;
        private VisualElement noSelectionContainer;
        private Label actorNameLabel;

        private HashSet<SentenceJson> collapsedRules = new HashSet<SentenceJson>();

        private bool isDraggingRule;
        private Vector2 dragStartPosRule;
        private VisualElement draggedRuleItem;
        private int draggedRuleIndex = -1;
        private VisualElement ruleDragSpacer;

        private VisualElement draggedElementItem;
        private VisualElement elementDragSpacer;
        private VisualElement elementDragColumn;
        private Vector2 dragStartPosElement;
        private int draggedElementIndex = -1;
        private int draggedElementRuleIndex = -1;
        private RuleElementKind draggedElementKind;

        // Abs indices of rules currently visible (respects groupId filter)
        private List<int> visibleAbsIndices = new List<int>();
        private bool subscribed;

        public ScriptEditorPanel(EditorContext editorContext, ProjectController projectController, string groupId = null, string groupName = null)
        {
            context = editorContext;
            controller = projectController;

            // Unity serializes a null string as "" when it saves the owning window across a domain
            // reload (entering Play). Normalizing here keeps "no group filter" as a single value:
            // otherwise an empty groupId is treated as a real filter and matches no rule at all.
            this.groupId = string.IsNullOrEmpty(groupId) ? null : groupId;
            this.groupName = string.IsNullOrEmpty(groupName) ? null : groupName;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            RegisterCallback<AttachToPanelEvent>(_ => { Subscribe(); UpdateUI(); });
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            context.OnActorSelected += OnActorSelected;
            context.OnProjectChanged += UpdateUI;
            context.OnRuleElementSelected += UpdateRulesList;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            context.OnActorSelected -= OnActorSelected;
            context.OnProjectChanged -= UpdateUI;
            context.OnRuleElementSelected -= UpdateRulesList;
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
            noSelectionLabel.style.color = GameRuleTheme.SubtleText;
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

            actorNameLabel.text = groupName != null
                ? $"Rules [{groupName}] for: {actor.ActorName}"
                : $"Script Rules for: {actor.ActorName}";

            UpdateRulesList();
        }

        private void UpdateRulesList()
        {
            rulesContainer.Clear();
            visibleAbsIndices.Clear();

            var actor = context.SelectedActor;
            if (actor?.Script != null)
            {
                for (int i = 0; i < actor.Script.Count; i++)
                {
                    var rule = actor.Script[i];
                    if (groupId != null && rule.groupId != groupId)
                        continue;
                    visibleAbsIndices.Add(i);
                }
            }

            if (visibleAbsIndices.Count == 0)
            {
                var emptyLabel = new Label("No rules defined. Add a rule to get started.");
                emptyLabel.style.color = GameRuleTheme.SubtleText;
                emptyLabel.style.fontSize = 12;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                rulesContainer.Add(emptyLabel);
                return;
            }

            foreach (int absIdx in visibleAbsIndices)
            {
                var ruleElement = CreateRuleElement(actor.Script[absIdx], absIdx);
                rulesContainer.Add(ruleElement);
            }
        }

        private VisualElement CreateRuleElement(SentenceJson rule, int ruleIndex)
        {
            var container = new VisualElement();
            container.AddToClassList("panel-section");
            container.style.marginBottom = 15;
            container.style.flexShrink = 0;

            if (string.IsNullOrEmpty(rule.Name))
            {
                rule.Name = $"Rule {ruleIndex + 1}";
                EditorUtility.SetDirty(context.currentProject);
            }

            string currentName = rule.Name;

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

                titleField.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    // Buscamos cualquier elemento de texto interno real (TextElement)
                    var innerTexts = titleField.Query<TextElement>().ToList();
                    foreach (var txt in innerTexts)
                    {
                        // Ignoramos la etiqueta oculta (Label) y nos quedamos con el Input real
                        if (!txt.ClassListContains("unity-label"))
                        {
                            txt.style.paddingLeft = 4; // Damos aire por la izquierda
                            txt.style.overflow = Overflow.Visible; // EVITA QUE SE RECORTE LA NEGRITA
                        }
                    }
                });

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
                    dragHandle.style.color = GameRuleTheme.SubtleText;
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

                var duplicateBtn = new Button(() =>
                {
                    controller.DuplicateRule(context.selectedActorIndex, ruleIndex);

                    var actor = context.SelectedActor;
                    int duplicateIndex = ruleIndex + 1;
                    if (actor?.Script != null && duplicateIndex < actor.Script.Count)
                    {
                        SentenceJson duplicatedRule = actor.Script[duplicateIndex];
                        collapsedRules.Remove(duplicatedRule);
                    }

                    UpdateRulesList();
                }) { text = "Duplicate" };
                duplicateBtn.tooltip = "Duplicate Rule";
                duplicateBtn.AddToClassList("button-primary");
                duplicateBtn.style.height = 26;
                duplicateBtn.style.marginLeft = 4;
                duplicateBtn.style.paddingLeft = 10;
                duplicateBtn.style.paddingRight = 10;
                duplicateBtn.style.flexShrink = 0;
                duplicateBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                foldoutToggle.Add(duplicateBtn);

                var removeIconBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog(
                        "Remove Rule",
                        "Are you sure you want to remove this rule?",
                        "Remove",
                        "Cancel"))
                    {
                        controller.RemoveRule(context.selectedActorIndex, ruleIndex);
                        UpdateRulesList();
                    }
                });
                removeIconBtn.text = "×";
                removeIconBtn.tooltip = "Remove Rule";
                removeIconBtn.AddToClassList("button-danger");
                removeIconBtn.AddToClassList("button-danger-icon");
                removeIconBtn.style.width = 28;
                removeIconBtn.style.height = 26;
                removeIconBtn.style.marginLeft = 4;
                removeIconBtn.style.paddingLeft = 0;
                removeIconBtn.style.paddingRight = 0;
                removeIconBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

                foldoutToggle.Add(removeIconBtn);
            }

            // Move/Up/CaptureOut on the CONTAINER — same element as CapturePointer target
            container.RegisterCallback<PointerMoveEvent>(evt => OnRuleDragMove(evt, container));
            container.RegisterCallback<PointerUpEvent>(evt => OnRuleDragEnd(evt, container));
            container.RegisterCallback<PointerCaptureOutEvent>(evt => OnRuleDragEnd(evt, container));

            var overview = new VisualElement();
            overview.AddToClassList("rule-overview");
            overview.style.flexDirection = FlexDirection.Row;
            overview.style.alignItems = Align.FlexStart;
            overview.style.minWidth = 730;
            overview.Add(CreateConditionColumn(rule, ruleIndex));
            overview.Add(CreateActionColumn(rule, ruleIndex));
            contentContainer.Add(overview);

            return container;
        }

        private VisualElement CreateConditionColumn(SentenceJson rule, int ruleIndex)
        {
            var column = CreateOverviewColumn("WHEN", "condition-overview");
            string expression = rule.When != null && rule.When.Count > 0 ? rule.When[0] : null;
            var terms = RuleConditionSequence.Parse(expression);
            for (int i = 0; i < terms.Count; i++)
            {
                string source = terms[i].Source;
                if (string.IsNullOrWhiteSpace(source)) continue;
                int index = i;
                var item = CreateDraggableElementItem();
                if (i > 0)
                {
                    string join = terms[i].JoinBefore;
                    var connector = new TooltipPopupField(RuleTooltips.LogicalOptions(),
                        join == "OR" ? "OR" : "AND", RuleTooltips.Logical(join),
                        RuleDropdownPalette.Condition);
                    connector.AddToClassList("button-condition");
                    connector.style.width = 75;
                    connector.style.alignSelf = Align.Center;
                    connector.style.marginTop = 3;
                    connector.style.marginBottom = 3;
                    connector.RegisterValueChangedCallback(value =>
                    {
                        var latest = RuleConditionSequence.Parse(rule.When[0]);
                        if (index >= latest.Count) return;
                        latest[index].JoinBefore = value;
                        controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                            new List<string> { RuleConditionSequence.Build(latest) });
                    });
                    item.Add(connector);
                }

                var row = new VisualElement();
                row.AddToClassList("rule-summary-row");
                // The connector already has 3px above and below; the generic row's
                // bottom margin would otherwise make the gap above it larger.
                row.style.marginBottom = 0;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.EnableInClassList("condition-negated",
                    RuleConditionSequence.IsNegated(source));
                row.Add(CreateElementDragHandle(item, ruleIndex,
                    RuleElementKind.Condition, index));
                row.Add(CreateSummaryButton(source, ruleIndex, RuleElementKind.Condition, index));

                var notButton = new Button(() =>
                {
                    var latest = RuleConditionSequence.Parse(rule.When[0]);
                    if (index >= latest.Count) return;
                    string body = RuleConditionSequence.WithoutNot(latest[index].Source);
                    latest[index].Source = RuleConditionSequence.IsNegated(latest[index].Source)
                        ? body : "NOT " + body;
                    controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                        new List<string> { RuleConditionSequence.Build(latest) });
                }) { text = "NOT", tooltip = "Invert this condition" };
                notButton.AddToClassList("button-negation");
                notButton.EnableInClassList("button-negation--active",
                    RuleConditionSequence.IsNegated(source));
                notButton.style.marginLeft = 5;
                row.Add(notButton);

                var remove = CreateSmallButton("×", "Remove Condition", () =>
                    RemoveCondition(rule, ruleIndex, index));
                row.Add(remove);
                item.Add(row);
                column.Add(item);
            }

            var types = ConditionTypes();
            var add = new TooltipPopupField(RuleTooltips.ConditionOptions(types),
                "+ Add Condition", "Choose a condition to add to this rule.",
                RuleDropdownPalette.Condition);
            add.AddToClassList("button-condition");
            add.style.alignSelf = Align.Center;
            add.style.marginTop = 8;
            add.RegisterValueChangedCallback(type => AddConditionOfType(rule, ruleIndex, type, types));
            column.Add(add);
            return column;
        }

        private VisualElement CreateActionColumn(SentenceJson rule, int ruleIndex)
        {
            var column = CreateOverviewColumn("DO", "action-overview");
            int storedCount = rule.Do?.Count ?? 0;
            for (int i = 0; i < storedCount; i++)
            {
                int index = i;
                string source = rule.Do[i];
                if (string.IsNullOrWhiteSpace(source)) continue;
                var item = CreateDraggableElementItem();
                var row = new VisualElement();
                row.AddToClassList("rule-summary-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.Add(CreateElementDragHandle(item, ruleIndex,
                    RuleElementKind.Action, index));

                row.Add(CreateSummaryButton(source, ruleIndex, RuleElementKind.Action, index));

                row.Add(CreateSmallButton("×", "Remove Action", () =>
                    RemoveAction(rule, ruleIndex, index)));
                item.Add(row);
                column.Add(item);
            }

            var types = ActionTypes();
            var addButton = new TooltipPopupField(RuleTooltips.ActionOptions(types),
                "+ Add Action", "Choose an action to add to this rule.",
                RuleDropdownPalette.Action);
            addButton.AddToClassList("button-action");
            addButton.style.alignSelf = Align.Center;
            addButton.style.marginTop = 8;
            addButton.RegisterValueChangedCallback(type => AddActionOfType(rule, ruleIndex, type, types));
            column.Add(addButton);
            return column;
        }

        private static VisualElement CreateOverviewColumn(string heading, string className)
        {
            var column = new VisualElement();
            column.AddToClassList("rule-overview-column");
            column.AddToClassList(className);
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            column.style.minWidth = 350;
            var label = new Label(heading);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 8;
            column.Add(label);
            return column;
        }

        private static Button CreateSmallButton(string text, string tooltip, System.Action clicked,
                                                bool destructive = true)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList(destructive ? "button-danger" : "button-primary");
            if (destructive) button.AddToClassList("button-danger-icon");
            button.style.width = 27;
            button.style.height = 27;
            button.style.flexShrink = 0;
            button.style.marginLeft = 4;
            return button;
        }

        private Button CreateSummaryButton(string source, int ruleIndex, RuleElementKind kind, int index)
        {
            bool isCondition = kind == RuleElementKind.Condition;
            var summary = Summarize(source);
            var button = new Button(() => context.SelectRuleElement(ruleIndex, kind, index))
            {
                tooltip = isCondition
                    ? RuleTooltips.Condition(GameRuleParser.ParseFunction(
                        RuleConditionSequence.WithoutNot(source)).Name)
                    : RuleTooltips.Action(GameRuleParser.ParseFunction(source).Name)
            };
            button.AddToClassList("rule-summary-card");
            button.AddToClassList(isCondition ? "button-condition" : "button-action");

            var name = new Label(summary.Name);
            name.AddToClassList("rule-summary-name");
            name.pickingMode = PickingMode.Ignore;
            button.Add(name);

            if (!string.IsNullOrEmpty(summary.Details))
            {
                var details = new Label(summary.Details);
                details.AddToClassList("rule-summary-detail");
                details.pickingMode = PickingMode.Ignore;
                button.Add(details);
            }

            MarkSelected(button, ruleIndex, kind, index);
            return button;
        }

        private void MarkSelected(VisualElement element, int ruleIndex, RuleElementKind kind, int index)
        {
            element.EnableInClassList("rule-summary-selected",
                context.selectedScriptIndex == ruleIndex &&
                context.SelectedRuleElementKind == kind &&
                context.SelectedRuleElementIndex == index);
        }

        private static List<string> ConditionTypes() => typeof(global::Condition)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(bool)).Select(method => method.Name).ToList();

        private static List<string> ActionTypes() => typeof(global::Action)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(void)).Select(method => method.Name).ToList();

        private (string Name, string Details) Summarize(string source)
        {
            bool negated = RuleConditionSequence.IsNegated(source);
            var parsed = GameRuleParser.ParseFunction(RuleConditionSequence.WithoutNot(source));
            string details;
            if (parsed.Name == "Spawn")
            {
                string prefab = parsed.Params != null && parsed.Params.Count > 0
                    ? parsed.Params[0] : string.Empty;
                details = string.IsNullOrWhiteSpace(prefab) ? "No prefab selected" : prefab;
            }
            else if (parsed.Name == "Touch")
            {
                // The second parameter controls the actor target; it is not useful in
                // the short rule overview, but remains unchanged in the stored rule.
                details = parsed.Params != null && parsed.Params.Count > 0
                    ? ConditionElement.FormatEventName(parsed.Params[0]) : string.Empty;
            }
            else if (parsed.Name == "Keyboard")
            {
                details = parsed.Params != null && parsed.Params.Count > 0 &&
                    !string.IsNullOrWhiteSpace(parsed.Params[0])
                    ? ConditionElement.FormatKeyName(parsed.Params[0]) : string.Empty;
            }
            else if (parsed.Name == "Compare")
            {
                string expression = parsed.Params != null && parsed.Params.Count > 0
                    ? parsed.Params[0] : string.Empty;
                var comparison = System.Text.RegularExpressions.Regex.Match(
                    expression, @"^(.*?)\s*(<=|>=|==|!=|<|>)\s*(.*)$");
                details = comparison.Success ? comparison.Groups[1].Value.Trim() : expression.Trim();
            }
            else if (parsed.Name == "Edit")
            {
                details = parsed.Params != null && parsed.Params.Count > 0
                    ? parsed.Params[0] : string.Empty;
            }
            else if (parsed.Name == "Move" || parsed.Name == "Rotate" ||
                     parsed.Name == "Push" || parsed.Name == "Torque")
            {
                details = string.Empty;
            }
            else if (parsed.Name == "MoveTo" || parsed.Name == "NavigateTo" ||
                     parsed.Name == "RotateTo" || parsed.Name == "PushTo")
            {
                details = SummarizeTarget(parsed.Name, parsed.Params);
            }
            else
            {
                details = parsed.Params == null ? string.Empty : string.Join(" · ",
                    parsed.Params.Where(value => !string.IsNullOrWhiteSpace(value)).Take(2));
            }
            details = GameRuleEditor.Windows.PropertyPickerDialog.ToDisplayReference(context, details);
            if (details.Length > 54) details = details.Substring(0, 51) + "…";
            return ((negated ? "NOT " : string.Empty) + parsed.Name, details);
        }

        private string SummarizeTarget(string actionName, List<string> parameters)
        {
            var coordinates = Enumerable.Range(1, 3)
                .Select(index => ActionDefaults.Fill(actionName, index,
                    parameters != null && index < parameters.Count ? parameters[index] : string.Empty))
                .ToList();
            var targetNames = coordinates.Select(TargetReferenceName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();

            // Any of the three target fields can refer to an actor's x, y or z.
            // If different actors are mixed, show the actual fields rather than naming
            // just one of them as the destination.
            return targetNames.Count == 1
                ? targetNames[0]
                : string.Join(",", coordinates);
        }

        private string TargetReferenceName(string coordinate)
        {
            if (string.IsNullOrWhiteSpace(coordinate)) return null;
            string reference = coordinate.Trim();
            int dot = reference.LastIndexOf('.');
            if (dot <= 0 || dot == reference.Length - 1) return null;
            string property = reference.Substring(dot + 1);
            if (property != "x" && property != "y" && property != "z") return null;

            string name = reference.Substring(0, dot);
            if (name.StartsWith("#", System.StringComparison.Ordinal))
                return name.Substring(1);
            if (string.Equals(name, "this", System.StringComparison.OrdinalIgnoreCase))
                return context.SelectedActor?.ActorName;
            return context.currentProject?.actors?.FirstOrDefault(actor =>
                string.Equals(actor.ActorName, name, System.StringComparison.OrdinalIgnoreCase))?.ActorName;
        }

        private void AddConditionOfType(SentenceJson rule, int ruleIndex, string type, List<string> types)
        {
            if (!types.Contains(type)) return;
            var element = new ConditionElement(context, types);
            element.SetFromSource(type + "()");
            string expression = rule.When != null && rule.When.Count > 0 ? rule.When[0] : null;
            var terms = RuleConditionSequence.Parse(expression)
                .Where(term => !string.IsNullOrWhiteSpace(term.Source)).ToList();
            int index = terms.Count;
            terms.Add(new RuleConditionTerm(index == 0 ? null : "AND", element.GetString()));
            controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                new List<string> { RuleConditionSequence.Build(terms) });
            context.SelectRuleElement(ruleIndex, RuleElementKind.Condition, index);
            UpdateRulesList();
        }

        private void AddActionOfType(SentenceJson rule, int ruleIndex, string type, List<string> types)
        {
            if (!types.Contains(type)) return;
            var element = new ActionElement(context, types);
            element.SetFromSource(type + "()");
            var actions = rule.Do != null
                ? rule.Do.Where(action => !string.IsNullOrWhiteSpace(action)).ToList()
                : new List<string>();
            int index = actions.Count;
            actions.Add(element.GetActionString());
            controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions);
            context.SelectRuleElement(ruleIndex, RuleElementKind.Action, index);
            UpdateRulesList();
        }

        private void RemoveCondition(SentenceJson rule, int ruleIndex, int index)
        {
            var terms = RuleConditionSequence.Parse(rule.When[0]);
            if (index >= terms.Count) return;
            terms.RemoveAt(index);
            context.ClearRuleElementSelection();
            if (terms.Count == 0) controller.RemoveRuleCondition(context.selectedActorIndex, ruleIndex);
            else
            {
                terms[0].JoinBefore = null;
                controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                    new List<string> { RuleConditionSequence.Build(terms) });
            }
            UpdateRulesList();
        }

        private void RemoveAction(SentenceJson rule, int ruleIndex, int index)
        {
            var actions = rule.Do != null ? new List<string>(rule.Do) : new List<string>();
            if (index >= actions.Count) return;
            actions.RemoveAt(index);
            context.ClearRuleElementSelection();
            controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions);
            UpdateRulesList();
        }

        private void AddEmptyRule()
        {
            if (context.selectedActorIndex < 0)
                return;

            // Tagging happens inside the controller (before it notifies) so the Properties panel's
            // rule counts rebuild with the correct group instead of a momentary null.
            controller.AddEmptyRule(context.selectedActorIndex, groupId);

            var actor = context.SelectedActor;
            if (actor?.Script != null && actor.Script.Count > 0)
            {
                var newRule = actor.Script[actor.Script.Count - 1];
                // Keep the newly added rule open without changing any rule the user folded.
                collapsedRules.Remove(newRule);
            }

            UpdateRulesList();
        }

        // ──────────────────────────────────
        //  DRAG AND DROP
        // ──────────────────────────────────

        private VisualElement CreateDraggableElementItem()
        {
            var item = new VisualElement();
            item.AddToClassList("rule-overview-item");
            item.style.flexShrink = 0;
            item.RegisterCallback<PointerMoveEvent>(evt => OnElementDragMove(evt, item));
            item.RegisterCallback<PointerUpEvent>(evt => OnElementDragEnd(evt, item));
            item.RegisterCallback<PointerCaptureOutEvent>(evt => OnElementDragEnd(evt, item));
            return item;
        }

        private VisualElement CreateElementDragHandle(VisualElement item, int ruleIndex,
            RuleElementKind kind, int index)
        {
            var handle = new Label("\u2261");
            handle.AddToClassList("rule-element-drag-handle");
            handle.tooltip = "Drag to reorder";
            handle.pickingMode = PickingMode.Position;
            handle.style.width = 16;
            handle.style.flexShrink = 0;
            handle.style.unityTextAlign = TextAnchor.MiddleCenter;
            handle.style.color = GameRuleTheme.SubtleText;
            handle.style.marginRight = 5;
            handle.RegisterCallback<PointerDownEvent>(evt =>
                OnElementDragStart(evt, item, ruleIndex, kind, index));
            return handle;
        }

        private void OnElementDragStart(PointerDownEvent evt, VisualElement item,
            int ruleIndex, RuleElementKind kind, int index)
        {
            if (evt.button != 0 || item.parent == null ||
                context.SelectedActor?.Script == null ||
                ruleIndex < 0 || ruleIndex >= context.SelectedActor.Script.Count ||
                draggedElementItem != null)
                return;

            var column = item.parent;
            if (column.Children().Count(child => child.ClassListContains("rule-overview-item")) < 2)
                return;

            draggedElementItem = item;
            elementDragColumn = column;
            draggedElementRuleIndex = ruleIndex;
            draggedElementKind = kind;
            draggedElementIndex = index;
            dragStartPosElement = evt.position;

            elementDragSpacer = new VisualElement();
            elementDragSpacer.style.height = Mathf.Max(30f, item.layout.height);
            elementDragSpacer.style.marginTop = item.resolvedStyle.marginTop;
            elementDragSpacer.style.marginBottom = item.resolvedStyle.marginBottom;
            elementDragSpacer.style.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.14f);
            elementDragSpacer.style.borderTopWidth = 2;
            elementDragSpacer.style.borderBottomWidth = 2;
            elementDragSpacer.style.borderTopColor = new Color(96f / 255f, 68f / 255f, 165f / 255f);
            elementDragSpacer.style.borderBottomColor = new Color(96f / 255f, 68f / 255f, 165f / 255f);
            column.Insert(column.IndexOf(item), elementDragSpacer);

            item.style.position = Position.Absolute;
            item.style.top = item.layout.y;
            item.style.left = item.layout.x;
            item.style.width = item.layout.width;
            item.style.opacity = 0.85f;
            item.BringToFront();
            item.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnElementDragMove(PointerMoveEvent evt, VisualElement item)
        {
            if (item != draggedElementItem || elementDragColumn == null) return;

            float diffY = evt.position.y - dragStartPosElement.y;
            item.style.translate = new Translate(0f, diffY, 0f);
            float centerY = item.layout.y + diffY + item.layout.height / 2f;
            var otherItems = elementDragColumn.Children()
                .Where(child => child != item && child.ClassListContains("rule-overview-item"))
                .ToList();
            int target = otherItems.FindIndex(child =>
                centerY < child.layout.y + child.layout.height / 2f);
            if (target < 0) target = otherItems.Count;

            int current = elementDragColumn.Children().TakeWhile(child => child != elementDragSpacer)
                .Count(child => child != item && child.ClassListContains("rule-overview-item"));
            if (target == current)
            {
                evt.StopPropagation();
                return;
            }

            elementDragColumn.Remove(elementDragSpacer);
            int insertAt = target < otherItems.Count
                ? elementDragColumn.IndexOf(otherItems[target])
                : otherItems.Count > 0
                    ? elementDragColumn.IndexOf(otherItems[otherItems.Count - 1]) + 1
                    : elementDragColumn.IndexOf(item);
            elementDragColumn.Insert(insertAt, elementDragSpacer);
            evt.StopPropagation();
        }

        private void OnElementDragEnd(EventBase evt, VisualElement item)
        {
            if (item != draggedElementItem) return;

            int from = draggedElementIndex;
            int ruleIndex = draggedElementRuleIndex;
            RuleElementKind kind = draggedElementKind;
            var column = elementDragColumn;
            var spacer = elementDragSpacer;
            draggedElementItem = null;
            elementDragSpacer = null;
            elementDragColumn = null;
            draggedElementIndex = -1;
            draggedElementRuleIndex = -1;
            draggedElementKind = RuleElementKind.None;

            if (evt is IPointerEvent pointerEvt)
                item.ReleasePointer(pointerEvt.pointerId);

            int to = -1;
            if (spacer?.parent == column)
            {
                to = column.Children().TakeWhile(child => child != spacer)
                    .Count(child => child != item && child.ClassListContains("rule-overview-item"));
                column.Remove(spacer);
            }

            item.style.translate = new Translate(0f, 0f, 0f);
            item.style.opacity = StyleKeyword.Null;
            item.style.position = StyleKeyword.Null;
            item.style.top = StyleKeyword.Null;
            item.style.left = StyleKeyword.Null;
            item.style.width = StyleKeyword.Null;

            if (evt is PointerUpEvent && to >= 0 && from != to)
                MoveRuleElement(ruleIndex, kind, from, to);
            else
                UpdateRulesList();
            evt.StopPropagation();
        }

        private void MoveRuleElement(int ruleIndex, RuleElementKind kind, int from, int to)
        {
            var actor = context.SelectedActor;
            if (actor?.Script == null || ruleIndex < 0 || ruleIndex >= actor.Script.Count)
            {
                UpdateRulesList();
                return;
            }
            var rule = actor.Script[ruleIndex];

            if (kind == RuleElementKind.Condition)
            {
                var terms = RuleConditionSequence.Parse(rule.When?.FirstOrDefault())
                    .Where(term => !string.IsNullOrWhiteSpace(term.Source)).ToList();
                if (from < 0 || to < 0 || from >= terms.Count || to >= terms.Count)
                {
                    UpdateRulesList();
                    return;
                }
                RuleConditionSequence.Move(terms, from, to);
                controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                    new List<string> { RuleConditionSequence.Build(terms) });
            }
            else if (kind == RuleElementKind.Action)
            {
                var actions = rule.Do != null ? new List<string>(rule.Do) : new List<string>();
                if (from < 0 || to < 0 || from >= actions.Count || to >= actions.Count)
                {
                    UpdateRulesList();
                    return;
                }
                string moved = actions[from];
                actions.RemoveAt(from);
                actions.Insert(to, moved);
                controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions);
            }
            else
            {
                UpdateRulesList();
                return;
            }

            if (context.selectedScriptIndex == ruleIndex &&
                context.SelectedRuleElementKind == kind)
            {
                int selected = context.SelectedRuleElementIndex;
                int next = selected == from ? to
                    : from < to && selected > from && selected <= to ? selected - 1
                    : from > to && selected >= to && selected < from ? selected + 1
                    : selected;
                context.SelectRuleElement(ruleIndex, kind, next);
            }
            UpdateRulesList();
        }

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
            ruleContainer.style.translate = new Translate(0f, diffY, 0f);

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
            ruleContainer.style.translate = new Translate(0f, 0f, 0f);
            ruleContainer.style.opacity = StyleKeyword.Null;
            ruleContainer.style.position = StyleKeyword.Null;
            ruleContainer.style.top = StyleKeyword.Null;
            ruleContainer.style.left = StyleKeyword.Null;
            ruleContainer.style.width = StyleKeyword.Null;

            // Read spacer position to determine target visual index
            int newVisualIndex = -1;
            if (ruleDragSpacer != null && ruleDragSpacer.parent != null)
            {
                newVisualIndex = 0;
                foreach (var child in rulesContainer.Children())
                {
                    if (child == ruleDragSpacer) break;
                    if (child == ruleContainer) continue;
                    newVisualIndex++;
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

            var actor = context.SelectedActor;
            if (actor?.Script == null || actor.Script.Count <= 1 || newVisualIndex < 0)
            {
                UpdateRulesList();
                evt.StopPropagation();
                return;
            }

            int toAbsIndex;
            if (groupId == null)
            {
                // No filter — visual index == abs post-removal index
                toAbsIndex = Mathf.Clamp(newVisualIndex, 0, actor.Script.Count - 1);
            }
            else
            {
                // Build visible abs indices excluding the dragged item (post-removal visible list)
                var postRemovalVisible = new List<int>();
                foreach (int absIdx in visibleAbsIndices)
                {
                    if (absIdx != originalIndex)
                        postRemovalVisible.Add(absIdx);
                }

                if (postRemovalVisible.Count == 0)
                {
                    UpdateRulesList();
                    evt.StopPropagation();
                    return;
                }

                int clampedVisual = Mathf.Clamp(newVisualIndex, 0, postRemovalVisible.Count);
                if (clampedVisual < postRemovalVisible.Count)
                {
                    // Insert before postRemovalVisible[clampedVisual] in the post-removal abs list
                    int absRef = postRemovalVisible[clampedVisual];
                    toAbsIndex = absRef > originalIndex ? absRef - 1 : absRef;
                }
                else
                {
                    // Insert after the last visible item in the group
                    int absRef = postRemovalVisible[postRemovalVisible.Count - 1];
                    int postRemovalAbsOfLast = absRef > originalIndex ? absRef - 1 : absRef;
                    toAbsIndex = postRemovalAbsOfLast + 1;
                }
                toAbsIndex = Mathf.Clamp(toAbsIndex, 0, actor.Script.Count - 1);
            }

            if (originalIndex >= 0 && originalIndex < actor.Script.Count &&
                originalIndex != toAbsIndex)
            {
                // Force blur to prevent focus guard in UpdateUI from cancelling the redraw
                var focused = this.focusController?.focusedElement;
                if (focused != null) focused.Blur();

                controller.MoveRuleToIndex(context.selectedActorIndex, originalIndex, toAbsIndex);

                // Manually force an update in case FocusGuard killed the controller's update
                UpdateRulesList();
            }
            else
            {
                UpdateRulesList();
            }

            evt.StopPropagation();
        }

    }
}
