using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
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
        private VisualElement groupSelectorContainer;
        private VisualElement addRuleRow;
        private const string AllRuleSets = "__all_rule_sets__";
        private readonly HashSet<string> collapsedGroups = new HashSet<string>();
        public event System.Action<string, string> OnGroupFilterChanged;

        private HashSet<SentenceJson> collapsedRules = new HashSet<SentenceJson>();

        private bool isDraggingRule;
        private Vector2 dragStartPosRule;
        private VisualElement draggedRuleItem;
        private int draggedRuleIndex = -1;
        private VisualElement ruleDragSpacer;
        private VisualElement ruleDragList;
        private List<int> ruleDragAbsIndices;

        private VisualElement draggedElementItem;
        private VisualElement elementDragSpacer;
        private VisualElement elementDragColumn;
        private Vector2 dragStartPosElement;
        private int draggedElementIndex = -1;
        private int draggedElementRuleIndex = -1;
        private RuleElementKind draggedElementKind;

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
            GameRuleTheme.ConfigureAdaptiveScroll(scrollView);
            scrollView.style.flexGrow = 1;
            scrollView.style.display = DisplayStyle.None;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.contentContainer.style.minWidth = 0;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.FlexStart;
            header.style.alignItems = Align.Center;

            actorNameLabel = new Label();
            actorNameLabel.AddToClassList("studio-panel-title");
            actorNameLabel.style.flexShrink = 1;
            KeepWordsWhole(actorNameLabel);
            header.Add(actorNameLabel);

            scrollView.Add(header);

            groupSelectorContainer = new VisualElement();
            groupSelectorContainer.style.marginBottom = 10;
            scrollView.Add(groupSelectorContainer);

            var addRuleButton = new Button(AddEmptyRule);
            addRuleButton.text = "+ Add Rule";
            addRuleButton.AddToClassList("button-primary");
            addRuleButton.AddToClassList("rules-command");

            addRuleRow = new VisualElement();
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
            groupId = groupName = null;
            collapsedRules.Clear();
            collapsedGroups.Clear();
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

            actorNameLabel.text = $"{actor.ActorName} Rules";
            var groups = context.GetRuleSets();
            if (groupId != null && !groups.Exists(group => group.id == groupId))
            {
                groupId = groupName = null;
                context.ClearRuleElementSelection();
            }
            groupName = groups.Find(group => group.id == groupId)?.name;
            OnGroupFilterChanged?.Invoke(groupId, groupName);
            groupSelectorContainer.Clear();
            var options = new List<TooltipDropdownOption>
            {
                new TooltipDropdownOption(AllRuleSets, "All Rule Sets", "Show all rule sets for this actor.")
            };
            options.AddRange(groups.Select(group => new TooltipDropdownOption(group.id, group.name,
                "Show the rules in this set.")));
            var selector = new TooltipPopupField(options, groupId ?? AllRuleSets,
                "Choose which rule sets to show.", RuleDropdownPalette.Primary);
            selector.name = "rule-set-filter";
            selector.AddToClassList("button-primary");
            selector.AddToClassList("parameter-selector");
            selector.style.alignSelf = Align.FlexStart;
            selector.SetEnabled(groups.Count > 0);
            selector.RegisterValueChangedCallback(value =>
            {
                groupId = value == AllRuleSets ? null : value;
                groupName = context.GetRuleSets().Find(group => group.id == groupId)?.name;
                OnGroupFilterChanged?.Invoke(groupId, groupName);
                context.ClearRuleElementSelection();
                UpdateUI();
            });
            groupSelectorContainer.Add(selector);
            addRuleRow.style.display = groupId == null ? DisplayStyle.None : DisplayStyle.Flex;

            UpdateRulesList();
        }

        private void UpdateRulesList()
        {
            rulesContainer.Clear();
            var actor = context.SelectedActor;
            if (actor == null) return;
            var groups = context.GetRuleSets();
            if (groupId != null)
            {
                PopulateRuleSet(rulesContainer, groupId);
                return;
            }
            foreach (var group in groups)
            {
                string id = group.id;
                var foldout = new Foldout { text = group.name, value = !collapsedGroups.Contains(id) };
                foldout.AddToClassList("rules-set-overview");
                foldout.style.marginBottom = 12;
                foldout.contentContainer.style.marginLeft = 0;
                foldout.contentContainer.style.paddingTop = 8;
                var toggle = foldout.Q<Toggle>();
                toggle.style.backgroundColor = GameRuleTheme.Header;
                toggle.style.paddingTop = toggle.style.paddingBottom = 4;
                var title = toggle.Q<Label>();
                if (title != null)
                {
                    title.style.color = GameRuleTheme.AccentText;
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target != foldout) return;
                    if (evt.newValue) collapsedGroups.Remove(id); else collapsedGroups.Add(id);
                });
                var add = new Button(() => AddEmptyRule(id)) { text = "+ Add Rule" };
                add.AddToClassList("button-primary");
                add.AddToClassList("rules-command");
                add.style.alignSelf = Align.FlexStart;
                add.style.marginBottom = 8;
                foldout.Add(add);
                var list = new VisualElement { name = "rules-set-list", userData = id };
                PopulateRuleSet(list, id);
                foldout.Add(list);
                rulesContainer.Add(foldout);
            }
            if (groups.Count == 0)
                rulesContainer.Add(new Label("No rule sets defined. Add a Rule Set in Properties."));
        }

        private void PopulateRuleSet(VisualElement list, string id)
        {
            var actor = context.SelectedActor;
            if (actor?.Script != null)
                for (int i = 0; i < actor.Script.Count; i++)
                    if (EditorContext.GetRuleSetId(actor, actor.Script[i]) == id)
                        list.Add(CreateRuleElement(actor.Script[i], i));
            if (list.childCount == 0)
            {
                var empty = new Label("No rules defined. Add a rule to get started.");
                empty.style.color = GameRuleTheme.SubtleText;
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.marginBottom = 10;
                list.Add(empty);
            }
        }

        private VisualElement CreateRuleElement(SentenceJson rule, int ruleIndex)
        {
            var container = new VisualElement();
            container.userData = ruleIndex;
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
            titleField.AddToClassList("studio-rule-name");
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
            titleField.style.minWidth = 50;
            titleField.style.flexShrink = 1;
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
                context.NotifyProjectChanged();
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
                duplicateBtn.AddToClassList("rules-command");
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
                removeIconBtn.AddToClassList("rules-square-command");
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
            overview.Add(CreateConditionColumn(rule, ruleIndex));
            overview.Add(CreateActionColumn(rule, ruleIndex));
            contentContainer.Add(overview);

            return container;
        }

        private VisualElement CreateConditionColumn(SentenceJson rule, int ruleIndex)
        {
            var column = CreateOverviewColumn("WHEN");
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
                    connector.AddToClassList("rule-join-selector");
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
                row.AddToClassList("rule-editor-card");
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
                MarkSelected(row, ruleIndex, RuleElementKind.Condition, index);

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
                notButton.AddToClassList("rules-square-command");
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
                "+ Add Condition", "Add a condition with a logical operator. Conditions joined by AND are treated as a group.",
                RuleDropdownPalette.Condition);
            add.AddToClassList("button-condition");
            add.AddToClassList("rule-add-element");
            add.RegisterValueChangedCallback(type => AddConditionOfType(rule, ruleIndex, type, types));
            column.Add(add);
            return column;
        }

        private VisualElement CreateActionColumn(SentenceJson rule, int ruleIndex)
        {
            var column = CreateOverviewColumn("DO");
            int storedCount = rule.Do?.Count ?? 0;
            for (int i = 0; i < storedCount; i++)
            {
                int index = i;
                string source = rule.Do[i];
                if (string.IsNullOrWhiteSpace(source)) continue;
                var item = CreateDraggableElementItem();
                var row = new VisualElement();
                row.AddToClassList("rule-summary-row");
                row.AddToClassList("rule-editor-card");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.Add(CreateElementDragHandle(item, ruleIndex,
                    RuleElementKind.Action, index));

                row.Add(CreateSummaryButton(source, ruleIndex, RuleElementKind.Action, index));
                MarkSelected(row, ruleIndex, RuleElementKind.Action, index);

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
            addButton.AddToClassList("rule-add-element");
            addButton.RegisterValueChangedCallback(type => AddActionOfType(rule, ruleIndex, type, types));
            column.Add(addButton);
            return column;
        }

        private static VisualElement CreateOverviewColumn(string heading)
        {
            var column = new VisualElement();
            column.AddToClassList("rule-overview-column");
            column.AddToClassList("rule-overview-responsive-column");
            var label = new Label(heading);
            label.AddToClassList("rule-block-heading");
            column.Add(label);
            return column;
        }

        private static Button CreateSmallButton(string text, string tooltip, System.Action clicked,
                                                bool destructive = true)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList(destructive ? "button-danger" : "button-primary");
            if (destructive)
            {
                button.AddToClassList("button-danger-icon");
                button.AddToClassList("rules-square-command");
            }
            else
            {
                button.style.width = 27;
                button.style.height = 27;
                button.style.flexShrink = 0;
            }
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
                KeepWordsWhole(details);
                details.pickingMode = PickingMode.Ignore;
                button.Add(details);
            }

            return button;
        }

        private static void KeepWordsWhole(Label label)
        {
            void UpdateMinimumWidth()
            {
                float width = 0;
                foreach (string word in label.text.Split(new[] { ' ', '\t', '\r', '\n' },
                             System.StringSplitOptions.RemoveEmptyEntries))
                    width = Mathf.Max(width, label.MeasureTextSize(word, 0,
                        VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x);
                float scale = EditorGUIUtility.pixelsPerPoint;
                var style = label.resolvedStyle;
                width += style.paddingLeft + style.paddingRight
                    + style.borderLeftWidth + style.borderRightWidth;
                // Leave a pixel of room for TextCore's glyph rounding at fractional editor scaling.
                label.style.minWidth = (Mathf.Ceil(width * scale) + 1f) / scale;
            }

            label.RegisterCallback<AttachToPanelEvent>(_ => label.schedule.Execute(UpdateMinimumWidth));
            label.RegisterCallback<GeometryChangedEvent>(_ => UpdateMinimumWidth());
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
            var parsed = GameRuleParser.ParseFunction(RuleConditionSequence.WithoutNot(source));
            string details;
            if (parsed.Name == "Spawn")
            {
                string value = parsed.Params != null && parsed.Params.Count > 0
                    ? parsed.Params[0] : string.Empty;
                details = value.Trim();
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
            else if (parsed.Name == "Move" || parsed.Name == "Push")
            {
                details = SummarizeAxisValues(parsed.Name, parsed.Params, 1, "R", "No rotation");
            }
            else if (parsed.Name == "Rotate")
            {
                details = string.Join(", ", Enumerable.Range(1, 3)
                    .Where(index => GetRotationComponent("Rotate", parsed.Params, index) != null)
                    .Select(index => "XYZ"[index - 1]));
            }
            else if (parsed.Name == "Torque")
            {
                details = SummarizeAxisValues(parsed.Name, parsed.Params, 0, "", "");
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
            return (parsed.Name, details);
        }

        private static string SummarizeAxisValues(string actionName, List<string> parameters,
            int firstIndex, string prefix, string emptySummary)
        {
            const string axes = "XYZ";
            var components = new List<string>(axes.Length);
            for (int i = 0; i < axes.Length; i++)
            {
                string value = GetRotationComponent(actionName, parameters, firstIndex + i);
                if (value != null) components.Add($"{prefix}{axes[i]} {value}");
            }
            return components.Count == 0 ? emptySummary : string.Join(" - ", components);
        }

        private static string GetRotationComponent(string actionName, List<string> parameters, int index)
        {
            string value = ActionDefaults.Fill(actionName, index,
                parameters != null && index < parameters.Count ? parameters[index] : null)?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double component)
                && component == 0 ? null : value;
        }

        private string SummarizeTarget(string actionName, List<string> parameters)
        {
            var coordinates = Enumerable.Range(1, 3)
                .Select(index => ActionDefaults.Fill(actionName, index,
                    parameters != null && index < parameters.Count ? parameters[index] : string.Empty))
                .ToList();
            string actorName = coordinates
                .Where(value => !string.IsNullOrWhiteSpace(value) &&
                    !value.TrimStart().StartsWith("#", System.StringComparison.Ordinal))
                .Select(TargetReferenceName)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name));
            return actorName
                ?? coordinates.Select(TargetReferenceName).FirstOrDefault(name => !string.IsNullOrEmpty(name))
                ?? string.Join(",", coordinates);
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
            AddEmptyRule(groupId);
        }

        private void AddEmptyRule(string targetGroup)
        {
            if (context.selectedActorIndex < 0)
                return;

            // Tagging happens inside the controller (before it notifies) so the Properties panel's
            // rule counts rebuild with the correct group instead of a momentary null.
            collapsedGroups.Remove(targetGroup ?? string.Empty);
            controller.AddEmptyRule(context.selectedActorIndex,
                targetGroup == EditorContext.UngroupedRuleSetId ? null : targetGroup);

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
            elementDragSpacer.style.backgroundColor = GameRuleTheme.Field;
            elementDragSpacer.style.borderTopWidth = 2;
            elementDragSpacer.style.borderBottomWidth = 2;
            elementDragSpacer.style.borderTopColor = GameRuleTheme.AccentText;
            elementDragSpacer.style.borderBottomColor = GameRuleTheme.AccentText;
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

            ruleDragList = ruleContainer.parent;
            if (ruleDragList == null || ruleDragList.IndexOf(ruleContainer) < 0) return;
            ruleDragAbsIndices = ruleDragList.Children().Where(child => child.userData is int)
                .Select(child => (int)child.userData).ToList();
            if (!ruleDragAbsIndices.Contains(index)) return;

            isDraggingRule = true;
            dragStartPosRule = evt.position;
            draggedRuleItem = ruleContainer;
            draggedRuleIndex = index;

            ruleDragSpacer = new VisualElement();
            ruleDragSpacer.style.height = Mathf.Max(30f, ruleContainer.layout.height);
            ruleDragSpacer.style.marginBottom = ruleContainer.resolvedStyle.marginBottom;
            ruleDragSpacer.style.marginTop = ruleContainer.resolvedStyle.marginTop;
            ruleDragSpacer.style.backgroundColor = GameRuleTheme.Field;
            ruleDragSpacer.style.borderTopWidth = 2;
            ruleDragSpacer.style.borderBottomWidth = 2;
            ruleDragSpacer.style.borderLeftWidth = 2;
            ruleDragSpacer.style.borderRightWidth = 2;
            ruleDragSpacer.style.borderTopColor = GameRuleTheme.AccentText;
            ruleDragSpacer.style.borderBottomColor = GameRuleTheme.AccentText;
            ruleDragSpacer.style.borderLeftColor = GameRuleTheme.AccentText;
            ruleDragSpacer.style.borderRightColor = GameRuleTheme.AccentText;
            ruleDragSpacer.style.borderTopLeftRadius = 5;
            ruleDragSpacer.style.borderTopRightRadius = 5;
            ruleDragSpacer.style.borderBottomLeftRadius = 5;
            ruleDragSpacer.style.borderBottomRightRadius = 5;

            int insertIndex = ruleDragList.IndexOf(ruleContainer);
            if (insertIndex < 0) return;

            ruleDragList.Insert(insertIndex, ruleDragSpacer);

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
            foreach (var child in ruleDragList.Children())
            {
                if (child == draggedRuleItem || child == ruleDragSpacer) continue;
                float childCenter = child.layout.y + (child.layout.height / 2f);
                if (draggedCenterY < childCenter) break;
                newTargetIndex++;
            }

            int currentSpacerLogicIndex = 0;
            foreach (var child in ruleDragList.Children())
            {
                if (child == ruleDragSpacer) break;
                if (child == draggedRuleItem) continue;
                currentSpacerLogicIndex++;
            }

            if (newTargetIndex != currentSpacerLogicIndex)
            {
                ruleDragList.Remove(ruleDragSpacer);

                int physicalInsertIndex = 0;
                int logicalCount = 0;
                foreach (var child in ruleDragList.Children())
                {
                    if (logicalCount == newTargetIndex) break;
                    if (child != draggedRuleItem) logicalCount++;
                    physicalInsertIndex++;
                }

                ruleDragList.Insert(physicalInsertIndex, ruleDragSpacer);
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
                foreach (var child in ruleDragList.Children())
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

            int toAbsIndex = ruleDragAbsIndices[Mathf.Clamp(newVisualIndex, 0, ruleDragAbsIndices.Count - 1)];

            if (originalIndex >= 0 && originalIndex < actor.Script.Count &&
                originalIndex != toAbsIndex)
            {
                // Force blur to prevent focus guard in UpdateUI from cancelling the redraw
                var focused = this.focusController?.focusedElement;
                if (focused != null) focused.Blur();

                controller.MoveRuleToIndex(context.selectedActorIndex, originalIndex, toAbsIndex, withinGroup: true);

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
