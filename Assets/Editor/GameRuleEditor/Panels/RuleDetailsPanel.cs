using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameRuleEditor.Controllers;
using GameRuleEditor.Core;
using GameRuleEditor.CustomControls;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameRuleEditor.Panels
{
    /// <summary>Edits one condition or action selected in the Rules overview.</summary>
    internal sealed class RuleDetailsPanel : VisualElement
    {
        private readonly EditorContext context;
        private readonly ProjectController controller;
        private readonly VisualElement content;
        private bool applyingChange;
        private bool subscribed;

        public RuleDetailsPanel(EditorContext context, ProjectController controller)
        {
            this.context = context;
            this.controller = controller;
            style.flexGrow = 1;

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.contentViewport.style.height = Length.Percent(100);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            content = new VisualElement();
            content.style.minWidth = 300;
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 12;
            scroll.Add(content);
            Add(scroll);

            RegisterCallback<AttachToPanelEvent>(_ => { Subscribe(); Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            Refresh();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            context.OnActorSelected += OnActorSelected;
            context.OnProjectLoaded += Refresh;
            context.OnProjectChanged += OnProjectChanged;
            context.OnRuleElementSelected += Refresh;
            context.OnRulesTabVisibilityChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            context.OnActorSelected -= OnActorSelected;
            context.OnProjectLoaded -= Refresh;
            context.OnProjectChanged -= OnProjectChanged;
            context.OnRuleElementSelected -= Refresh;
            context.OnRulesTabVisibilityChanged -= Refresh;
        }

        private void OnActorSelected(int _) => Refresh();

        private void OnProjectChanged()
        {
            if (!applyingChange) Refresh();
        }

        private void ShowEmpty(string message)
        {
            content.Clear();
            var label = new Label(message);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = GameRuleTheme.SubtleText;
            label.style.marginTop = 18;
            content.Add(label);
        }

        private void Refresh()
        {
            if (!GameRuleEditor.Windows.GameRuleRulesWindow.IsRulesTabActive())
            {
                ShowEmpty("Open Rules to select a condition or action.");
                return;
            }

            var rule = context.SelectedScript;
            int index = context.SelectedRuleElementIndex;
            RuleElementKind kind = context.SelectedRuleElementKind;
            if (rule == null || kind == RuleElementKind.None || index < 0)
            {
                ShowEmpty("Select a condition or action in Rules to edit its details.");
                return;
            }

            if (kind == RuleElementKind.Condition)
            {
                if (rule.When == null || rule.When.Count == 0)
                {
                    ShowEmpty("Select a condition in Rules.");
                    return;
                }
                List<RuleConditionTerm> terms = RuleConditionSequence.Parse(rule.When[0]);
                if (index >= terms.Count || string.IsNullOrWhiteSpace(terms[index].Source))
                {
                    context.ClearRuleElementSelection();
                    ShowEmpty("Select a condition or action in Rules to edit its details.");
                    return;
                }
                BuildConditionDetails(rule, index, terms);
            }
            else
            {
                int count = rule.Do?.Count ?? 0;
                if (index >= count || string.IsNullOrWhiteSpace(rule.Do[index]))
                {
                    context.ClearRuleElementSelection();
                    ShowEmpty("Select a condition or action in Rules to edit its details.");
                    return;
                }
                BuildActionDetails(rule, index);
            }
        }

        private VisualElement CreateHeading(string breadcrumb)
        {
            content.Clear();
            var heading = new Label(breadcrumb);
            heading.style.color = GameRuleTheme.MutedText;
            heading.style.fontSize = 12;
            heading.style.marginBottom = 12;
            content.Add(heading);

            var typeLabel = new Label("TYPE");
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeLabel.style.fontSize = 11;
            typeLabel.style.marginBottom = 5;
            content.Add(typeLabel);

            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.marginBottom = 14;
            content.Add(selectorRow);
            return selectorRow;
        }

        private void BuildConditionDetails(SentenceJson rule, int index, List<RuleConditionTerm> terms)
        {
            string source = index < terms.Count ? terms[index].Source : string.Empty;
            var names = typeof(global::Condition).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(bool)).Select(method => method.Name).ToList();
            var element = new ConditionElement(context, names, detailsMode: true);
            if (!string.IsNullOrWhiteSpace(source))
            {
                element.SetFromSource(RuleConditionSequence.WithoutNot(source));
                element.SetNegated(RuleConditionSequence.IsNegated(source));
            }

            int ruleIndex = context.selectedScriptIndex;
            element.BeforeTypeChange = (oldType, _) => ConfirmReplacement(source, oldType, true);
            element.OnChanged += () =>
            {
                if (context.SelectedScript != rule || context.selectedScriptIndex != ruleIndex) return;
                string replacement = element.GetString();
                if (string.IsNullOrEmpty(replacement)) return;

                var latest = RuleConditionSequence.Parse(rule.When?[0]);
                if (index < latest.Count)
                    latest[index].Source = replacement;
                else if (context.PendingRuleElement && index == latest.Count)
                    latest.Add(new RuleConditionTerm(context.PendingConditionJoin, replacement));
                else return;

                context.CommitPendingRuleElementSelection();
                applyingChange = true;
                try
                {
                    controller.UpdateRuleCondition(context.selectedActorIndex, ruleIndex,
                        new List<string> { RuleConditionSequence.Build(latest) });
                }
                finally { applyingChange = false; }
                source = replacement;
            };

            var selectorRow = CreateHeading($"{rule.Name}  /  WHEN  /  Condition {index + 1}");
            element.TypeSelector.style.width = 190;
            selectorRow.Add(element.TypeSelector);
            content.Add(element);
            AddTypeHint();
        }

        private void BuildActionDetails(SentenceJson rule, int index)
        {
            string source = index < (rule.Do?.Count ?? 0) ? rule.Do[index] : string.Empty;
            var names = typeof(global::Action).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(void)).Select(method => method.Name).ToList();
            var element = new ActionElement(context, names, detailsMode: true);
            if (!string.IsNullOrWhiteSpace(source)) element.SetFromSource(source);

            int ruleIndex = context.selectedScriptIndex;
            element.BeforeTypeChange = (oldType, _) => ConfirmReplacement(source, oldType, false);
            element.OnChanged += () =>
            {
                if (context.SelectedScript != rule || context.selectedScriptIndex != ruleIndex) return;
                string replacement = element.GetActionString();
                if (string.IsNullOrEmpty(replacement)) return;

                var actions = rule.Do != null ? new List<string>(rule.Do) : new List<string>();
                if (index < actions.Count)
                    actions[index] = replacement;
                else if (context.PendingRuleElement && index == actions.Count)
                    actions.Add(replacement);
                else return;

                context.CommitPendingRuleElementSelection();
                applyingChange = true;
                try { controller.UpdateRuleActions(context.selectedActorIndex, ruleIndex, actions); }
                finally { applyingChange = false; }
                source = replacement;
            };

            var selectorRow = CreateHeading($"{rule.Name}  /  DO  /  Action {index + 1}");
            element.TypeSelector.style.width = 190;
            selectorRow.Add(element.TypeSelector);
            content.Add(element);
            AddTypeHint();
        }

        private void AddTypeHint()
        {
            var hint = new Label("Use the type menu above to replace this condition or action.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.color = GameRuleTheme.SubtleText;
            hint.style.marginTop = 12;
            content.Add(hint);
        }

        private bool ConfirmReplacement(string source, string oldType, bool condition)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrEmpty(oldType)) return true;

            string defaultSource;
            if (condition)
            {
                var names = new List<string> { oldType };
                var defaults = new ConditionElement(context, names, detailsMode: true);
                defaults.SetFromSource(oldType + "()");
                defaultSource = defaults.GetString();
                source = RuleConditionSequence.WithoutNot(source);
            }
            else
            {
                var names = new List<string> { oldType };
                var defaults = new ActionElement(context, names, detailsMode: true);
                defaults.SetFromSource(oldType + "()");
                defaultSource = defaults.GetActionString();
            }

            if (string.Equals(source, defaultSource, System.StringComparison.Ordinal)) return true;
            return EditorUtility.DisplayDialog("Change Type",
                "Changing the type will discard the current parameter values. Continue?",
                "Change Type", "Cancel");
        }
    }
}
