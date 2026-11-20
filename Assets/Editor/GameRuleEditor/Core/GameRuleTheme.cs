using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameRuleEditor.Core
{
    /// <summary>Shared neutral palette for GameRule's UI Toolkit windows.</summary>
    internal static class GameRuleTheme
    {
        private sealed class ThemeState
        {
            public bool ProSkin;
            public System.Action Rebuild;
        }

        public static bool IsLight => !EditorGUIUtility.isProSkin;

        public static Color Background => IsLight ? Rgb(242, 242, 242) : Rgb(30, 30, 30);
        public static Color Panel => IsLight ? Rgb(242, 242, 242) : Rgb(37, 37, 37);
        public static Color Surface => IsLight ? Rgb(250, 250, 250) : Rgb(51, 51, 51);
        public static Color Field => IsLight ? Rgb(255, 255, 255) : Rgb(56, 56, 56);
        public static Color Header => IsLight ? Rgb(226, 226, 226) : Surface;
        public static Color InsetSurface => IsLight ? Surface : Panel;
        public static Color RaisedSurface => Surface;
        public static Color BrightHeader => IsLight ? Header : Field;
        public static Color Border => IsLight ? Rgb(182, 182, 182) : Rgb(66, 66, 66);
        public static Color SoftBorder => Border;
        public static Color Text => IsLight ? Rgb(35, 35, 35) : Rgb(230, 230, 230);
        public static Color MutedText => IsLight ? Rgb(92, 92, 92) : Rgb(181, 181, 181);
        public static Color SubtleText => IsLight ? Rgb(107, 107, 107) : MutedText;
        public static Color PreviewText => MutedText;
        public static Color AccentText => IsLight ? Rgb(88, 64, 150) : Rgb(161, 138, 224);
        public static Color PropertyAccent => IsLight ? Rgb(121, 88, 18) : Rgb(217, 182, 92);
        public static Color IconTint => AccentText;
        public static Color PrefabIconTint => Color.white;

        public static Label CreateNameError()
        {
            var label = new Label();
            label.AddToClassList("name-validation-error");
            label.style.color = (Color)new Color32(222, 112, 120, 255);
            label.style.fontSize = 10;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.display = DisplayStyle.None;
            return label;
        }

        public static void ShowNameError(Label label, string error)
        {
            label.text = error ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        // IMGUI pickers use the same neutral field and subtle one-pixel frame as UI Toolkit.
        public static void DrawPickerField(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1,
                Mathf.Max(0, rect.width - 2), Mathf.Max(0, rect.height - 2)), fill);
        }

        public static void StylePropertySection(Foldout section, bool useSectionSpacing = true)
        {
            section.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (useSectionSpacing) section.style.marginBottom = 10;
            section.style.backgroundColor = Surface;
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;
        }

        public static void Configure(VisualElement root, System.Action rebuild)
        {
            root.EnableInClassList("gamerule-light", IsLight);

            if (!(root.userData is ThemeState state))
            {
                state = new ThemeState();
                root.userData = state;
                root.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    root.EnableInClassList("gamerule-compact", evt.newRect.width < 420f);
                    root.EnableInClassList("gamerule-narrow", evt.newRect.width < 300f);
                });
                // The root survives Clear(), so this check is installed only once per window.
                root.schedule.Execute(() =>
                {
                    if (state.ProSkin == EditorGUIUtility.isProSkin) return;
                    state.ProSkin = EditorGUIUtility.isProSkin;
                    state.Rebuild?.Invoke();
                }).Every(250);
            }

            state.ProSkin = EditorGUIUtility.isProSkin;
            state.Rebuild = rebuild;
            root.EnableInClassList("gamerule-compact", root.resolvedStyle.width < 420f);
            root.EnableInClassList("gamerule-narrow", root.resolvedStyle.width < 300f);
        }

        /// <summary>Wrap first; scroll horizontally only below the content's readable minimum.</summary>
        public static void ConfigureAdaptiveScroll(ScrollView scroll)
        {
            scroll.mode = ScrollViewMode.VerticalAndHorizontal;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            var content = scroll.contentContainer;
            content.style.flexShrink = 0;

            void UpdateWidth()
            {
                if (scroll.panel == null || scroll.contentViewport.layout.width <= 0) return;
                float viewportWidth = scroll.contentViewport.layout.width;
                float minimum = ReadableMinimumWidth(content);
                float scale = EditorGUIUtility.pixelsPerPoint;
                float width = minimum <= viewportWidth + 0.5f ? viewportWidth
                    : Mathf.Ceil(minimum * scale) / scale;
                if (Mathf.Abs(content.layout.width - width) > 0.1f)
                    content.style.width = width;
            }

            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateWidth());
            // Also cover changing a value/type without changing the containing row's geometry.
            content.schedule.Execute(UpdateWidth).Every(250);
        }

        private static float ReadableMinimumWidth(VisualElement element)
        {
            var style = element.resolvedStyle;
            if (style.display == DisplayStyle.None || style.position == Position.Absolute) return 0;

            float inner = 0;
            if (element.childCount == 0 && !(element is TextElement)
                && (element is Image || style.flexShrink == 0))
                inner = Mathf.Max(0, element.layout.width - style.paddingLeft - style.paddingRight
                    - style.borderLeftWidth - style.borderRightWidth);
            if (element is TextElement text && !string.IsNullOrEmpty(text.text))
            {
                if (style.whiteSpace == WhiteSpace.Normal)
                {
                    foreach (string word in text.text.Split(new[] { ' ', '\t', '\r', '\n' },
                                 System.StringSplitOptions.RemoveEmptyEntries))
                        inner = Mathf.Max(inner, text.MeasureTextSize(word, 0,
                            VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x);
                }
                else
                    inner = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined,
                        0, VisualElement.MeasureMode.Undefined).x;
            }

            float children = 0;
            bool unbrokenRow = style.flexDirection == FlexDirection.Row && style.flexWrap == Wrap.NoWrap;
            foreach (var child in element.Children())
            {
                float childWidth = ReadableMinimumWidth(child);
                children = unbrokenRow ? children + childWidth : Mathf.Max(children, childWidth);
            }
            inner = Mathf.Max(inner, children) + style.paddingLeft + style.paddingRight
                + style.borderLeftWidth + style.borderRightWidth;
            if (style.minWidth.keyword == StyleKeyword.Undefined)
                inner = Mathf.Max(inner, style.minWidth.value);
            return inner + style.marginLeft + style.marginRight;
        }
    }
}
