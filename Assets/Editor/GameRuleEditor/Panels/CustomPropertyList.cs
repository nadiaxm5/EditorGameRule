using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameRuleEditor.Core;

namespace GameRuleEditor.Panels
{
    internal sealed class CustomPropertyList : VisualElement
    {
        private static readonly List<string> Types = new List<string> { "Int", "Float", "Bool", "Vector2", "Vector3" };
        private readonly Func<GameRuleProject> getProject;
        private readonly Func<List<CustomVariable>> getProperties;
        private readonly System.Action onChanged;
        private readonly string addButtonText;
        private List<CustomVariable> displayedProperties;
        private int displayedCount;

        // ── Property drag (spacer-based) ──
        private bool isDraggingProp;
        private Vector2 dragStartPosProp;
        private VisualElement draggedPropItem;
        private int draggedPropIndex = -1;
        private VisualElement propDragSpacer;

        public CustomPropertyList(Func<GameRuleProject> getProject, Func<List<CustomVariable>> getProperties,
            System.Action onChanged = null, string addButtonText = "+ Add Property")
        {
            this.getProject = getProject;
            this.getProperties = getProperties;
            this.onChanged = onChanged;
            this.addButtonText = addButtonText;
            style.paddingLeft = 15;
            style.marginTop = 5;
            style.marginBottom = 8;

            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Rebuild);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Rebuild);
            Rebuild();
        }

        public void Refresh()
        {
            var properties = getProperties();
            if (!ReferenceEquals(properties, displayedProperties) || (properties?.Count ?? 0) != displayedCount)
                Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            var properties = getProperties();
            displayedProperties = properties;
            displayedCount = properties?.Count ?? 0;
            if (properties == null) return;
            var content = this;
            if (properties.Count > 0)
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    int idx = i;
                    var property = properties[i];

                    var row = new VisualElement { style = { marginBottom = 2 } };
                    var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

                    // Drag handle
                    var handle = new Label("\u2261");
                    handle.tooltip = "Drag to reorder";
                    handle.pickingMode = PickingMode.Position;
                    handle.style.fontSize = 16;
                    handle.style.width = 16;
                    handle.style.unityTextAlign = TextAnchor.MiddleCenter;
                    handle.style.color = GameRuleTheme.SubtleText;
                    handle.style.marginRight = 4;
                    header.Add(handle);
                    var nameField = new TextField { value = property.name, isDelayed = true };
                    nameField.style.flexGrow = 1;
                    nameField.style.minWidth = 0;
                    HideTextFieldLabel(nameField);
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        string name = evt.newValue.Trim();
                        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$") ||
                            properties.Exists(other => other != property && other.name == name))
                        {
                            nameField.SetValueWithoutNotify(property.name);
                            return;
                        }
                        UpdateProperty(() => property.name = name);
                    });
                    header.Add(nameField);

                    int typeIndex = Types.FindIndex(type => string.Equals(type, property.type, System.StringComparison.OrdinalIgnoreCase));
                    var typeField = new PopupField<string>(Types, typeIndex >= 0 ? typeIndex : 1);
                    typeField.style.minWidth = 68;
                    var valueContainer = new VisualElement { style = { marginLeft = 20, marginRight = 28 } };
                    void AlignValueWithName(float nameWidth)
                    {
                        bool isNumeric = property.type == "int" || property.type == "float";
                        valueContainer.style.maxWidth = isNumeric && nameWidth > 0
                            ? new StyleLength(nameWidth)
                            : new StyleLength(StyleKeyword.None);
                    }
                    nameField.RegisterCallback<GeometryChangedEvent>(evt => AlignValueWithName(evt.newRect.width));
                    AddValueField(valueContainer, property);
                    typeField.RegisterValueChangedCallback(evt =>
                    {
                        UpdateProperty(() =>
                        {
                            property.type = evt.newValue.ToLowerInvariant();
                            if (property.type == "vector2" && (property.arrayValue == null || property.arrayValue.Length != 2))
                                property.arrayValue = new float[2];
                            if (property.type == "vector3" && (property.arrayValue == null || property.arrayValue.Length != 3))
                                property.arrayValue = new float[3];
                        });
                        AddValueField(valueContainer, property);
                        AlignValueWithName(nameField.layout.width);
                    });
                    header.Add(typeField);

                    // Delete button
                    var delBtn = new Button(() =>
                    {
                        UpdateProperty(() => properties.RemoveAt(idx));
                        Refresh();
                    }) { text = "×", tooltip = "Remove Property" };
                    delBtn.AddToClassList("button-danger");
                    delBtn.AddToClassList("button-danger-icon");
                    delBtn.style.width = 28;
                    delBtn.style.height = 26;
                    header.Add(delBtn);
                    row.Add(header);
                    row.Add(valueContainer);

                    // Spacer-based drag registration
                    handle.RegisterCallback<PointerDownEvent>(evt => OnPropDragStart(evt, row, idx, content));
                    row.RegisterCallback<PointerMoveEvent>(evt => OnPropDragMove(evt, row));
                    row.RegisterCallback<PointerUpEvent>(evt => OnPropDragEnd(evt, row, content));
                    row.RegisterCallback<PointerCaptureOutEvent>(evt => OnPropDragEnd(evt, row, content));

                    content.Add(row);
                }
            }

            var addBtn = new Button(() =>
            {
                string name = "NewProp";
                int suffix = 2;
                while (properties.Exists(property => property.name == name))
                    name = "NewProp" + suffix++;
                UpdateProperty(() => properties.Add(new CustomVariable { name = name, type = "float" }));
                Refresh();
            }) { text = addButtonText };
            addBtn.AddToClassList("button-property-picker");
            addBtn.style.marginTop = 10;
            addBtn.style.paddingTop = 4;
            addBtn.style.paddingBottom = 4;
            addBtn.style.alignSelf = Align.Center;
            content.Add(addBtn);
        }

        private void OnPropDragStart(PointerDownEvent evt, VisualElement row, int index, VisualElement content)
        {
            if (evt.button != 0) return;

            isDraggingProp = true;
            dragStartPosProp = evt.position;
            draggedPropItem = row;
            draggedPropIndex = index;

            propDragSpacer = new VisualElement();
            propDragSpacer.style.height = Mathf.Max(24f, row.layout.height);
            propDragSpacer.style.marginBottom = row.resolvedStyle.marginBottom;
            propDragSpacer.style.marginTop = row.resolvedStyle.marginTop;
            propDragSpacer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.2f);
            propDragSpacer.style.borderTopWidth = 2;
            propDragSpacer.style.borderBottomWidth = 2;
            propDragSpacer.style.borderLeftWidth = 2;
            propDragSpacer.style.borderRightWidth = 2;
            propDragSpacer.style.borderTopColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderBottomColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderLeftColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderRightColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            propDragSpacer.style.borderTopLeftRadius = 3;
            propDragSpacer.style.borderTopRightRadius = 3;
            propDragSpacer.style.borderBottomLeftRadius = 3;
            propDragSpacer.style.borderBottomRightRadius = 3;

            int insertAt = content.IndexOf(row);
            if (insertAt < 0) return;
            content.Insert(insertAt, propDragSpacer);

            row.style.position = Position.Absolute;
            row.style.top = row.layout.y;
            row.style.left = row.layout.x;
            row.style.width = row.layout.width;
            row.style.opacity = 0.8f;
            row.BringToFront();

            row.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPropDragMove(PointerMoveEvent evt, VisualElement row)
        {
            if (!isDraggingProp || row != draggedPropItem) return;

            float diffY = evt.position.y - dragStartPosProp.y;
            row.style.translate = new Translate(0f, diffY, 0f);

            var content = propDragSpacer?.parent;
            if (content == null) return;

            float draggedCenterY = row.layout.y + diffY + row.layout.height / 2f;

            // Determine target logical index (skip row, spacer, and the Add button)
            int newTargetIndex = 0;
            foreach (var child in content.Children())
            {
                if (child == row || child == propDragSpacer || child is Button) continue;
                if (draggedCenterY < child.layout.y + child.layout.height / 2f) break;
                newTargetIndex++;
            }

            int currentSpacerIndex = 0;
            foreach (var child in content.Children())
            {
                if (child == propDragSpacer) break;
                if (child == row || child is Button) continue;
                currentSpacerIndex++;
            }

            if (newTargetIndex != currentSpacerIndex)
            {
                content.Remove(propDragSpacer);
                int physicalInsert = 0;
                int logical = 0;
                foreach (var child in content.Children())
                {
                    if (logical == newTargetIndex) break;
                    if (child != row && !(child is Button)) logical++;
                    physicalInsert++;
                }
                content.Insert(physicalInsert, propDragSpacer);
            }

            evt.StopPropagation();
        }

        private void OnPropDragEnd(EventBase evt, VisualElement row, VisualElement content)
        {
            if (!isDraggingProp || row != draggedPropItem) return;

            int originalIndex = draggedPropIndex;
            isDraggingProp = false;
            draggedPropItem = null;
            draggedPropIndex = -1;

            IPointerEvent pointerEvt = evt as IPointerEvent;
            if (pointerEvt != null) row.ReleasePointer(pointerEvt.pointerId);

            row.style.translate = new Translate(0f, 0f, 0f);
            row.style.opacity = StyleKeyword.Null;
            row.style.position = StyleKeyword.Null;
            row.style.top = StyleKeyword.Null;
            row.style.left = StyleKeyword.Null;
            row.style.width = StyleKeyword.Null;

            // Read new index from spacer position before removing it
            int newIndex = originalIndex;
            if (propDragSpacer != null && propDragSpacer.parent != null)
            {
                newIndex = 0;
                foreach (var child in content.Children())
                {
                    if (child == propDragSpacer) break;
                    if (child == row || child is Button) continue;
                    newIndex++;
                }
                propDragSpacer.parent.Remove(propDragSpacer);
                propDragSpacer = null;
            }

            if (!(evt is PointerUpEvent))
            {
                Rebuild();
                return;
            }

            var properties = getProperties();
            int clamped = Mathf.Clamp(newIndex, 0, (properties?.Count ?? 1) - 1);
            if (properties != null && properties.Count > 1 &&
                originalIndex >= 0 && originalIndex < properties.Count &&
                originalIndex != clamped)
            {
                var focused = this.focusController?.focusedElement;
                if (focused != null) focused.Blur();

                UpdateProperty(() =>
                {
                    var property = properties[originalIndex];
                    properties.RemoveAt(originalIndex);
                    properties.Insert(clamped, property);
                });
            }

            Rebuild();
            evt.StopPropagation();
        }

        /// <summary>Hides the label element of a TextField so it doesn't push the input area left.</summary>
        private static void HideTextFieldLabel(TextField field)
        {
            if (field.labelElement == null) return;
            var s = field.labelElement.style;
            s.display = DisplayStyle.None;
            s.width = 0;
            s.minWidth = 0;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            s.paddingLeft = 0; s.paddingRight = 0; s.paddingTop = 0; s.paddingBottom = 0;
        }

        private void UpdateProperty(System.Action update)
        {
            var project = getProject();
            if (project == null) return;
            Undo.RecordObject(project, "Edit Custom Property");
            update();
            EditorUtility.SetDirty(project);
            onChanged?.Invoke();
        }

        private void AddValueField(VisualElement container, CustomVariable property)
        {
            container.Clear();
            switch (property.type)
            {
                case "int":
                    var integer = new IntegerField { value = property.intValue, style = { marginLeft = 0, marginRight = 0 } };
                    integer.RegisterValueChangedCallback(evt => UpdateProperty(() => property.intValue = evt.newValue));
                    container.Add(integer);
                    break;
                case "bool":
                    var toggle = new Toggle { value = property.boolValue };
                    toggle.RegisterValueChangedCallback(evt => UpdateProperty(() => property.boolValue = evt.newValue));
                    container.Add(toggle);
                    break;
                case "vector2":
                    var vector2 = new Vector2Field { value = (Vector2)property.GetValue() };
                    vector2.RegisterValueChangedCallback(evt => UpdateProperty(() =>
                        property.arrayValue = new[] { evt.newValue.x, evt.newValue.y }));
                    container.Add(vector2);
                    break;
                case "vector3":
                    var vector3 = new Vector3Field { value = (Vector3)property.GetValue() };
                    vector3.RegisterValueChangedCallback(evt => UpdateProperty(() =>
                        property.arrayValue = new[] { evt.newValue.x, evt.newValue.y, evt.newValue.z }));
                    container.Add(vector3);
                    break;
                default:
                    var number = new FloatField { value = property.floatValue, style = { marginLeft = 0, marginRight = 0 } };
                    number.RegisterValueChangedCallback(evt => UpdateProperty(() => property.floatValue = evt.newValue));
                    container.Add(number);
                    break;
            }
        }

    }
}
