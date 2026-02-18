using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    public class ActorListPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        private VisualElement actorListContainer;
        private List<VisualElement> actorItems = new List<VisualElement>();

        public ActorListPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.minWidth = 250;
            style.maxWidth = 350;
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);

            CreateUI();
            UpdateUI();

            context.OnProjectLoaded += UpdateUI;
            context.OnActorListChanged += UpdateUI;
            context.OnActorSelected += OnActorSelected;
            context.OnProjectChanged += UpdateUI;
        }

        private void CreateUI()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);

            var title = new Label("Actors");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            var addButton = new Button(OnAddActor);
            addButton.text = "+";
            addButton.style.width = 30;
            addButton.style.height = 25;
            addButton.AddToClassList("button-primary");
            header.Add(addButton);

            Add(header);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            actorListContainer = new VisualElement();
            actorListContainer.style.paddingTop = 5;
            actorListContainer.style.paddingBottom = 5;
            actorListContainer.style.paddingLeft = 5;
            actorListContainer.style.paddingRight = 5;
            scrollView.Add(actorListContainer);

            Add(scrollView);

            var footer = new VisualElement();
            footer.style.paddingTop = 5;
            footer.style.paddingBottom = 5;
            footer.style.paddingLeft = 5;
            footer.style.paddingRight = 5;
            footer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);

            var countLabel = new Label();
            countLabel.name = "actor-count";
            countLabel.style.fontSize = 10;
            countLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            footer.Add(countLabel);

            Add(footer);
        }

        private void UpdateUI()
        {
            actorListContainer.Clear();
            actorItems.Clear();

            if (context?.currentProject?.actors == null)
            {
                var emptyLabel = new Label("No actors in project");
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                actorListContainer.Add(emptyLabel);
                UpdateCount(0);
                return;
            }

            for (int i = 0; i < context.currentProject.actors.Count; i++)
            {
                int index = i;
                var actor = context.currentProject.actors[i];
                var item = CreateActorItem(actor, index);
                actorListContainer.Add(item);
                actorItems.Add(item);
            }

            UpdateCount(context.currentProject.actors.Count);

            if (context.selectedActorIndex >= 0 && context.selectedActorIndex < actorItems.Count)
            {
                HighlightItem(context.selectedActorIndex);
            }
        }

        private VisualElement CreateActorItem(ActorJson actor, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.marginBottom = 3;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 10;
            item.style.cursor = new UnityEngine.UIElements.Cursor() { texture = null };

            item.RegisterCallback<MouseDownEvent>(evt => context.SelectActor(index));

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;

            var nameLabel = new Label(actor.ActorName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginBottom = 2;
            content.Add(nameLabel);

            var prefabLabel = new Label($"Prefab: {actor.PrefabName}");
            prefabLabel.style.fontSize = 10;
            prefabLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            content.Add(prefabLabel);

            int scriptCount = actor.Script?.Count ?? 0;
            var scriptLabel = new Label($"Rules: {scriptCount}");
            scriptLabel.style.fontSize = 10;
            scriptLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
            content.Add(scriptLabel);

            item.Add(content);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 5;

            var duplicateBtn = new Button(() => controller.DuplicateActor(index));
            duplicateBtn.text = "Duplicate";
            duplicateBtn.style.fontSize = 9;
            duplicateBtn.style.height = 20;
            duplicateBtn.style.flexGrow = 1;
            duplicateBtn.style.marginRight = 2;
            buttonContainer.Add(duplicateBtn);

            var removeBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("Remove Actor", $"Are you sure you want to remove '{actor.ActorName}'?", "Remove", "Cancel"))
                {
                    controller.RemoveActor(index);
                }
            });
            removeBtn.text = "Remove";
            removeBtn.AddToClassList("button-danger");
            removeBtn.style.fontSize = 9;
            removeBtn.style.height = 20;
            removeBtn.style.flexGrow = 1;
            buttonContainer.Add(removeBtn);

            item.Add(buttonContainer);

            return item;
        }

        private void OnActorSelected(int index)
        {
            foreach (var item in actorItems) item.RemoveFromClassList("list-item-selected");
            if (index >= 0 && index < actorItems.Count) HighlightItem(index);
        }

        private void HighlightItem(int index)
        {
            if (index >= 0 && index < actorItems.Count) actorItems[index].AddToClassList("list-item-selected");
        }

        private void OnAddActor()
        {
            string actorName = "NewActor";
            int counter = 1;
            while (context.currentProject.actors.Exists(a => a.ActorName == actorName))
            {
                actorName = $"NewActor_{counter}";
                counter++;
            }

            controller.AddActor(actorName);
        }

        private void UpdateCount(int count)
        {
            var countLabel = this.Q<Label>("actor-count");
            if (countLabel != null)
                countLabel.text = $"{count} actor{(count != 1 ? "s" : "")} in project";
        }

        ~ActorListPanel()
        {
            context.OnProjectLoaded -= UpdateUI;
            context.OnActorListChanged -= UpdateUI;
            context.OnActorSelected -= OnActorSelected;
            context.OnProjectChanged -= UpdateUI;
        }
    }
}