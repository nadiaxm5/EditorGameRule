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

        public static Color Background => IsLight ? Rgb(242, 242, 242) : Rgb(37, 37, 39);
        public static Color Header => IsLight ? Rgb(226, 226, 226) : Rgb(56, 56, 56);
        public static Color Surface => IsLight ? Rgb(250, 250, 250) : Rgb(46, 46, 46);
        public static Color InsetSurface => IsLight ? Rgb(250, 250, 250) : Rgb(51, 51, 51);
        public static Color RaisedSurface => IsLight ? Rgb(250, 250, 250) : Rgb(64, 64, 64);
        public static Color BrightHeader => IsLight ? Rgb(226, 226, 226) : Rgb(77, 77, 77);
        public static Color Border => IsLight ? Rgb(182, 182, 182) : Rgb(26, 26, 26);
        public static Color SoftBorder => IsLight ? Rgb(182, 182, 182) : Rgb(38, 38, 38);
        public static Color Text => IsLight ? Rgb(35, 35, 35) : Rgb(229, 231, 235);
        public static Color MutedText => IsLight ? Rgb(92, 92, 92) : Rgb(156, 163, 175);
        public static Color SubtleText => IsLight ? Rgb(107, 107, 107) : Rgb(145, 145, 145);
        public static Color PreviewText => IsLight ? Rgb(34, 67, 97) : Rgb(204, 230, 255);
        public static Color AccentText => IsLight ? Rgb(59, 39, 107) : Rgb(208, 221, 255);
        public static Color IconTint => IsLight ? Rgb(72, 110, 160) : Rgb(184, 209, 255);
        public static Color PrefabIconTint => Color.white;

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

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
