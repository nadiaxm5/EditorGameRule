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

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

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
        }
    }
}
