using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameRuleEditor.Windows
{
    public class PropertyPickerDialog : EditorWindow
    {
        // --- CONFIGURATION ---
        private struct PropDef
        { public string Label; public string Suffix; public bool IsBool; public string Tooltip; }

        private static readonly HashSet<string> BuiltInGlobalNames = new HashSet<string>
        {
            "GameName", "ScreenResolution", "CameraPosition", "CameraRotation",
            "SunPosition", "SunRotation", "SunColor", "SunAmbientColor",
            "BackgroundColor", "Gravity", "SoundTrack", "FPS", "Time",
            "DeltaTime", "Mouse", "MouseWorld"
        };

        private readonly Dictionary<string, List<PropDef>> propertyDefinitions = new Dictionary<string, List<PropDef>>
        {
            { "Transform", new List<PropDef> {
                new PropDef { Label = "Position X", Suffix = "x", Tooltip = "Actor's world position on the X axis." },
                new PropDef { Label = "Position Y", Suffix = "y", Tooltip = "Actor's world position on the Y axis." },
                new PropDef { Label = "Position Z", Suffix = "z", Tooltip = "Actor's world position on the Z axis." },
                new PropDef { Label = "Rotation X", Suffix = "rx", Tooltip = "Actor's rotation around the X axis, in degrees." },
                new PropDef { Label = "Rotation Y", Suffix = "ry", Tooltip = "Actor's rotation around the Y axis, in degrees." },
                new PropDef { Label = "Rotation Z", Suffix = "rz", Tooltip = "Actor's rotation around the Z axis, in degrees." },
                new PropDef { Label = "Scale X", Suffix = "sx", Tooltip = "Actor's scale on the X axis." },
                new PropDef { Label = "Scale Y", Suffix = "sy", Tooltip = "Actor's scale on the Y axis." },
                new PropDef { Label = "Scale Z", Suffix = "sz", Tooltip = "Actor's scale on the Z axis." }
            }},
            { "Physics", new List<PropDef> {
                new PropDef { Label = "Velocity X", Suffix = "Velocity.x", Tooltip = "Actor's movement speed on the X axis." },
                new PropDef { Label = "Velocity Y", Suffix = "Velocity.y", Tooltip = "Actor's movement speed on the Y axis." },
                new PropDef { Label = "Velocity Z", Suffix = "Velocity.z", Tooltip = "Actor's movement speed on the Z axis." },
                new PropDef { Label = "Ang.Vel X", Suffix = "AngularVelocity.x", Tooltip = "Actor's rotation speed around the X axis." },
                new PropDef { Label = "Ang.Vel Y", Suffix = "AngularVelocity.y", Tooltip = "Actor's rotation speed around the Y axis." },
                new PropDef { Label = "Ang.Vel Z", Suffix = "AngularVelocity.z", Tooltip = "Actor's rotation speed around the Z axis." },
                new PropDef { Label = "Density", Suffix = "Density", Tooltip = "Actor's physics mass (shown as Density)." },
                new PropDef { Label = "Friction", Suffix = "Friction", Tooltip = "How strongly the actor resists sliding on surfaces." },
                new PropDef { Label = "Bounciness", Suffix = "Bounciness", Tooltip = "How much the actor rebounds from collisions." },
                new PropDef { Label = "Drag", Suffix = "Drag", Tooltip = "How strongly the actor's motion is slowed." }
            }},
            { "State", new List<PropDef> {
                new PropDef { Label = "Active", Suffix = "Active", IsBool = true, Tooltip = "Whether this actor is active in the scene." }
            }},
            { "UI", new List<PropDef> {
                new PropDef { Label = "Slider Value", Suffix = "sliderValue", Tooltip = "Current value of the actor's UI slider." },
                new PropDef { Label = "Text Content", Suffix = "text", Tooltip = "Text shown by the actor's UI text element." }
            }}
        };

        // ---------------------

        private System.Action<string> onPick;
        private EditorContext context;

        private static readonly Color PickerYellow = new Color32(255, 174, 3, 255);
        private static readonly Color PickerYellowHover = new Color32(255, 193, 61, 255);
        private static readonly Color PickerYellowActive = new Color32(217, 146, 0, 255);

        private GUIStyle navigationButtonStyle;
        private GUIStyle propertyButtonStyle;
        private GUIStyle navigationColoredLabelStyle;
        private GUIStyle propertyColoredLabelStyle;
        private bool stylesAreProSkin;
        private Vector2 mouseScreenPosition;
        private Rect currentColumnScreenRect;

        // Filters
        private bool boolOnly = false;

        private System.Type resourceType = null; // [New] Filter for resources
        private bool prefabMode = false;
        private bool includeActorPrefabs = false;
        private PrefabSource selectedPrefabSource = PrefabSource.ActorPrefabs;
        private string prefabSearch = string.Empty;
        private string resourceTabLabel = "All Resources";
        private string resourceSearch = string.Empty;

        private const string PrefabsFolder = "Assets/Resources/Prefabs";

        private enum PrefabSource
        {
            ActorPrefabs,
            AllPrefabs
        }

        private struct PreviewGridItem
        {
            public string Label;
            public string Result;
            public string SecondaryName;
            public Object PreviewObject;
        }

        private string selectedCategory = "Me";
        private string selectedGroup = "Transform";

        private Vector2 scrollCategory;
        private Vector2 scrollGroup;
        private Vector2 scrollProps;

        private List<string> actorNames;
        private ActorJson currentActor;

        // [Updated] Added resourceFilter parameter
        public static void Show(EditorContext ctx, System.Action<string> callback, bool onlyBooleans = false,
                                System.Type resourceFilter = null, Rect? anchorScreenRect = null)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Property", true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = onlyBooleans;
            win.resourceType = resourceFilter;
            win.prefabMode = false;
            win.prefabSearch = string.Empty;
            win.minSize = new Vector2(500, 300);
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
        }

        public static void ShowPrefab(EditorContext ctx, System.Action<string> callback, bool includeActorPrefabs,
                                      Rect? anchorScreenRect = null)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Prefab", true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = false;
            win.resourceType = null;
            win.prefabMode = true;
            win.includeActorPrefabs = includeActorPrefabs;
            win.selectedPrefabSource = includeActorPrefabs
                ? PrefabSource.ActorPrefabs
                : PrefabSource.AllPrefabs;
            win.prefabSearch = string.Empty;
            win.scrollCategory = Vector2.zero;
            win.minSize = new Vector2(360, 220);
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
        }

        public static void ShowResource(EditorContext ctx, System.Action<string> callback,
                                        System.Type resourceFilter, string windowTitle,
                                        string allResourcesLabel, Rect? anchorScreenRect = null)
        {
            var win = GetWindow<PropertyPickerDialog>(true, windowTitle, true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = false;
            win.resourceType = resourceFilter;
            win.prefabMode = false;
            win.resourceTabLabel = allResourcesLabel;
            win.resourceSearch = string.Empty;
            win.scrollCategory = Vector2.zero;
            win.minSize = new Vector2(360, 220);
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
        }

        /// <summary>Converts a UI Toolkit element's panel-space bounds to desktop screen coordinates.</summary>
        public static Rect GetScreenRect(VisualElement element)
        {
            if (element == null) return default;

            Rect screenRect = element.worldBound;
            EditorWindow hostWindow = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            if (hostWindow != null)
                screenRect.position += hostWindow.position.position;

            return screenRect;
        }

        private static void PositionNearAnchor(EditorWindow window, Rect? anchorScreenRect, Vector2 windowSize)
        {
            if (!anchorScreenRect.HasValue || anchorScreenRect.Value.width <= 0f)
            {
                window.position = new Rect(window.position.position, windowSize);
                return;
            }

            Rect anchor = anchorScreenRect.Value;
            Rect desktop = UnityEditorInternal.InternalEditorUtility.GetBoundsOfDesktopAtPoint(anchor.center);
            const float gap = 4f;

            float x = anchor.xMin;
            float y = anchor.yMax + gap;

            if (x + windowSize.x > desktop.xMax)
                x = desktop.xMax - windowSize.x;
            if (x < desktop.xMin)
                x = desktop.xMin;

            if (y + windowSize.y > desktop.yMax)
                y = anchor.yMin - windowSize.y - gap;
            if (y < desktop.yMin)
                y = desktop.yMin;

            window.position = new Rect(new Vector2(x, y), windowSize);
        }

        private void InitData()
        {
            if (context?.currentProject == null) return;
            currentActor = context.SelectedActor;
            actorNames = context.currentProject.actors
                .Select(a => a.ActorName)
                .Where(n => currentActor == null || n != currentActor.ActorName)
                .ToList();
        }

        /// <summary>
        /// Shows self references with the selected actor's name while keeping "this" as the
        /// persisted GameRule syntax.
        /// </summary>
        public static string ToDisplayReference(EditorContext ctx, string value)
        {
            string actorName = ctx?.SelectedActor?.ActorName;
            if (string.IsNullOrEmpty(value)) return value;

            // Keep the global marker in the serialized rule, but not in editor controls.
            value = Regex.Replace(value, @"(?<![A-Za-z0-9_])#(?<name>[A-Za-z_][A-Za-z0-9_]*)",
                match => IsGlobalName(ctx, match.Groups["name"].Value)
                    ? match.Groups["name"].Value : match.Value);
            if (string.IsNullOrEmpty(actorName)) return value;

            return Regex.Replace(value, @"(?<![A-Za-z0-9_])this\.", actorName + ".",
                RegexOptions.IgnoreCase);
        }

        public static string ToStoredReference(EditorContext ctx, string value)
        {
            string actorName = ctx?.SelectedActor?.ActorName;
            if (string.IsNullOrEmpty(value)) return value;

            if (!string.IsNullOrEmpty(actorName))
            {
                string actorReference = @"(?<![A-Za-z0-9_])" + Regex.Escape(actorName) + @"\.";
                value = Regex.Replace(value, actorReference, "this.", RegexOptions.IgnoreCase);
            }

            return Regex.Replace(value,
                @"(?<![A-Za-z0-9_.#])(?<name>[A-Za-z_][A-Za-z0-9_]*)",
                match => IsGlobalName(ctx, match.Groups["name"].Value)
                    ? "#" + match.Value : match.Value);
        }

        public static bool IsGlobalReference(EditorContext ctx, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            Match match = Regex.Match(value.Trim(),
                @"^#?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\.[xyz])?$");
            return match.Success && IsGlobalName(ctx, match.Groups["name"].Value);
        }

        private static bool IsGlobalName(EditorContext ctx, string name)
        {
            return BuiltInGlobalNames.Contains(name) ||
                ctx?.currentProject?.sceneData?.CustomVariables?.Any(variable => variable.name == name) == true;
        }

        private void OnGUI()
        {
            // Capture this before entering any ScrollView. Event.current.mousePosition is
            // interpreted in the active GUI clip, so converting it from inside each column
            // can offset it by that column's origin and make a neighbouring button hover.
            mouseScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            EnsureStyles();
            if (Event.current.type == EventType.MouseMove) Repaint();

            if (prefabMode)
            {
                DrawPrefabMode();
                return;
            }

            // 1. Resource Mode (New)
            if (resourceType != null)
            {
                DrawResourceMode();
                return;
            }

            // 2. Standard Property Mode
            const float outerPadding = 8f;
            const float columnGap = 6f;
            float availableWidth = Mathf.Max(450f, position.width - (outerPadding * 2f) - (columnGap * 2f));
            float columnWidth = availableWidth / 3f;

            GUILayout.Space(outerPadding);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);

            // Col 1: Category
            DrawColumn(ref scrollCategory, columnWidth, () =>
            {
                string meLabel = currentActor == null ? "Me" : $"Me ({currentActor.ActorName})";
                DrawSelectable(meLabel, "Me");
                DrawSelectable("Game (global)", "Game");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
                foreach (var actorName in actorNames) DrawSelectable(actorName, actorName);
            }, hideHorizontalScrollbar: true);

            GUILayout.Space(columnGap);

            // Col 2: Group
            DrawColumn(ref scrollGroup, columnWidth, () =>
            {
                if (selectedCategory == "Game")
                {
                    DrawGroupSelectable("Global Variables", "Global");
                    if (!boolOnly)
                    {
                        DrawGroupSelectable("Camera", "Camera");
                        DrawGroupSelectable("Mouse", "Mouse");
                        DrawGroupSelectable("Sun", "Sun");
                        DrawGroupSelectable("Physics", "Physics");
                    }
                }
                else
                {
                    foreach (var groupName in propertyDefinitions.Keys)
                    {
                        bool hasValidProps = !boolOnly || propertyDefinitions[groupName].Any(p => p.IsBool);
                        if (hasValidProps) DrawGroupSelectable(groupName, groupName);
                    }
                    DrawGroupSelectable("Custom Properties", "Custom");
                }
            });

            GUILayout.Space(columnGap);

            // Col 3: Properties
            DrawColumn(ref scrollProps, columnWidth, () =>
            {
                if (selectedCategory == "Game") DrawGameProperties();
                else DrawActorProperties();
            });

            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(outerPadding);
        }

        private void DrawResourceMode()
        {
            const float outerPadding = 8f;
            GUILayout.Space(outerPadding);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);

            float tabWidth = Mathf.Clamp(EditorStyles.miniButton.CalcSize(
                new GUIContent(resourceTabLabel)).x + 18f, 96f, 140f);
            DrawYellowTabButton(resourceTabLabel, true, tabWidth);

            GUILayout.FlexibleSpace();
            float searchWidth = Mathf.Clamp(position.width * 0.32f, 115f, 210f);
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
            resourceSearch = EditorGUILayout.TextField(resourceSearch ?? string.Empty, searchStyle,
                GUILayout.Width(searchWidth), GUILayout.Height(22f));

            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory, GUILayout.ExpandHeight(true));

            var assets = Resources.LoadAll("", resourceType)
                .OrderBy(asset => asset.name, System.StringComparer.OrdinalIgnoreCase)
                .Where(asset => MatchesSearch(resourceSearch, asset.name))
                .Select(asset => new PreviewGridItem
                {
                    Label = asset.name,
                    Result = asset.name,
                    SecondaryName = resourceType.Name,
                    PreviewObject = asset
                })
                .ToList();

            if (assets.Count == 0)
            {
                EditorGUILayout.HelpBox($"No items in {resourceTabLabel} match the search.", MessageType.Info);
            }
            else
            {
                DrawPreviewGrid(assets);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(outerPadding);
        }

        private void DrawPrefabMode()
        {
            const float outerPadding = 8f;
            GUILayout.Space(outerPadding);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);

            if (includeActorPrefabs &&
                DrawYellowTabButton("Actor Prefabs", selectedPrefabSource == PrefabSource.ActorPrefabs, 112f))
            {
                selectedPrefabSource = PrefabSource.ActorPrefabs;
                scrollCategory = Vector2.zero;
            }

            if (includeActorPrefabs) GUILayout.Space(4f);

            if (DrawYellowTabButton("All Prefabs", selectedPrefabSource == PrefabSource.AllPrefabs, 96f))
            {
                selectedPrefabSource = PrefabSource.AllPrefabs;
                scrollCategory = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            float searchWidth = Mathf.Clamp(position.width * 0.32f, 115f, 210f);
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
            prefabSearch = EditorGUILayout.TextField(prefabSearch ?? string.Empty, searchStyle,
                GUILayout.Width(searchWidth), GUILayout.Height(22f));

            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory, GUILayout.ExpandHeight(true));

            List<GameObject> selectablePrefabs = LoadSelectablePrefabs();
            var gridItems = new List<PreviewGridItem>();

            if (selectedPrefabSource == PrefabSource.ActorPrefabs)
            {
                foreach (string actorName in actorNames)
                {
                    ActorJson actor = context.currentProject.actors.Find(item => item.ActorName == actorName);
                    string prefabName = actor?.PrefabName ?? string.Empty;
                    if (!MatchesPrefabSearch(actorName, prefabName)) continue;

                    GameObject prefab = selectablePrefabs.Find(item => item.name == prefabName);
                    gridItems.Add(new PreviewGridItem
                    {
                        Label = string.IsNullOrEmpty(prefabName)
                            ? actorName
                            : $"{actorName} ({prefabName})",
                        Result = prefabName,
                        SecondaryName = null,
                        PreviewObject = prefab
                    });
                }
            }
            else
            {
                foreach (GameObject prefab in selectablePrefabs)
                {
                    if (!MatchesPrefabSearch(prefab.name)) continue;

                    gridItems.Add(new PreviewGridItem
                    {
                        Label = prefab.name,
                        Result = prefab.name,
                        SecondaryName = prefab.name,
                        PreviewObject = prefab
                    });
                }
            }

            if (gridItems.Count == 0)
            {
                string message = selectedPrefabSource == PrefabSource.ActorPrefabs
                    ? "No actor prefabs match the search."
                    : "No prefabs in Resources/Prefabs match the search.";
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            else
            {
                DrawPreviewGrid(gridItems);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(outerPadding);
        }

        private void DrawPreviewGrid(List<PreviewGridItem> items)
        {
            const float gap = 8f;
            const float minimumCardWidth = 96f;
            const float maximumCardWidth = 114f;
            const float cardHeight = 116f;

            // Account for outer padding, help-box padding and the vertical scrollbar.
            float availableWidth = Mathf.Max(minimumCardWidth, position.width - 42f);
            int columns = Mathf.Max(1,
                Mathf.FloorToInt((availableWidth + gap) / (minimumCardWidth + gap)));
            float cardWidth = Mathf.Min(maximumCardWidth,
                (availableWidth - (gap * (columns - 1))) / columns);

            for (int start = 0; start < items.Count; start += columns)
            {
                EditorGUILayout.BeginHorizontal();

                int end = Mathf.Min(start + columns, items.Count);
                for (int index = start; index < end; index++)
                {
                    DrawPreviewCard(items[index], cardWidth, cardHeight);
                    if (index < end - 1) GUILayout.Space(gap);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(gap);
            }
        }

        private void DrawPreviewCard(PreviewGridItem item, float width, float height)
        {
            Rect cardRect = GUILayoutUtility.GetRect(width, height,
                GUILayout.Width(width), GUILayout.Height(height));
            bool hovered = cardRect.Contains(Event.current.mousePosition);
            bool pressed = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;

            string tooltip = string.IsNullOrEmpty(item.SecondaryName) || item.SecondaryName == item.Label
                ? item.Label
                : $"{item.Label} — {item.SecondaryName}";
            bool clicked = GUI.Button(cardRect, new GUIContent(string.Empty, tooltip), GUIStyle.none);

            Color cardColor = pressed
                ? PickerYellowActive
                : hovered ? PickerYellowHover : GameRuleTheme.Header;
            EditorGUI.DrawRect(cardRect, cardColor);

            float previewSize = Mathf.Min(width - 12f, 76f);
            var previewRect = new Rect(
                cardRect.x + ((width - previewSize) * 0.5f),
                cardRect.y + 6f,
                previewSize,
                previewSize);

            Texture preview = GetAssetPreview(item.PreviewObject);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);

            var labelRect = new Rect(cardRect.x + 5f, previewRect.yMax + 3f,
                width - 10f, Mathf.Max(20f, cardRect.yMax - previewRect.yMax - 6f));
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            if (hovered || pressed)
            {
                Color hoveredTextColor = new Color32(30, 30, 30, 255);
                labelStyle.normal.textColor = hoveredTextColor;
                labelStyle.hover.textColor = hoveredTextColor;
                labelStyle.active.textColor = hoveredTextColor;
                labelStyle.focused.textColor = hoveredTextColor;
            }

            GUI.Label(labelRect, new GUIContent(item.Label, tooltip), labelStyle);

            if (clicked)
            {
                onPick?.Invoke(item.Result);
                Close();
            }
        }

        private Texture GetAssetPreview(Object asset)
        {
            Object previewTarget = asset is Component component ? component.gameObject : asset;
            if (previewTarget == null)
                return EditorGUIUtility.IconContent("Prefab Icon").image;

            Texture preview = AssetPreview.GetAssetPreview(previewTarget);
            if (preview != null) return preview;

            if (AssetPreview.IsLoadingAssetPreview(previewTarget.GetInstanceID()))
                Repaint();

            return AssetPreview.GetMiniThumbnail(previewTarget) ??
                   EditorGUIUtility.ObjectContent(null, previewTarget.GetType()).image;
        }

        private bool MatchesPrefabSearch(params string[] names)
        {
            return MatchesSearch(prefabSearch, names);
        }

        private static bool MatchesSearch(string query, params string[] names)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            string search = query.Trim();
            return names.Any(name => !string.IsNullOrEmpty(name) &&
                name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<GameObject> LoadSelectablePrefabs()
        {
            var prefabs = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(PrefabsFolder)) return prefabs;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;

                string folder = System.IO.Path.GetDirectoryName(path);
                if (folder == null || folder.Replace('\\', '/') != PrefabsFolder) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) prefabs.Add(prefab);
            }

            prefabs.Sort((left, right) => EditorUtility.NaturalCompare(left.name, right.name));
            return prefabs;
        }

        private static bool DrawYellowTabButton(string label, bool selected, float width)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), style,
                GUILayout.Width(width), GUILayout.Height(22f));
            bool hovered = rect.Contains(Event.current.mousePosition);
            bool pressed = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            bool clicked = GUI.Button(rect, GUIContent.none, style);

            Color fillColor = pressed || selected
                ? PickerYellowActive
                : hovered ? PickerYellowHover : PickerYellow;
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 2f,
                Mathf.Max(0f, rect.width - 4f), Mathf.Max(0f, rect.height - 4f)), fillColor);

            GUIStyle labelStyle = CreateColoredLabelStyle(TextAnchor.MiddleCenter, new RectOffset(2, 2, 2, 2));
            labelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(rect, label, labelStyle);
            return clicked;
        }

        private void DrawColumn(ref Vector2 scroll, float width, System.Action drawContent,
                                bool hideHorizontalScrollbar = false)
        {
            Rect columnRect = EditorGUILayout.BeginVertical(
                EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            Rect previousColumnScreenRect = currentColumnScreenRect;
            Vector2 columnScreenPosition = GUIUtility.GUIToScreenPoint(columnRect.position);
            currentColumnScreenRect = new Rect(columnScreenPosition, columnRect.size);

            scroll = hideHorizontalScrollbar
                ? EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar)
                : EditorGUILayout.BeginScrollView(scroll);
            drawContent();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            currentColumnScreenRect = previousColumnScreenRect;
        }

        private void DrawSelectable(string label, string id)
        {
            string defaultGroup = (id == "Game") ? "Global" : "Transform";
            if (DrawTintedButton(label, navigationButtonStyle, navigationColoredLabelStyle, selectedCategory == id))
            {
                selectedCategory = id;
                selectedGroup = defaultGroup;
            }
        }

        private void DrawGroupSelectable(string label, string id)
        {
            if (DrawTintedButton(label, navigationButtonStyle, navigationColoredLabelStyle, selectedGroup == id)) selectedGroup = id;
        }

        private void DrawGameProperties()
        {
            string prefix = "#";
            if (selectedGroup == "Global" && context.currentProject.sceneData.CustomVariables != null)
            {
                foreach (var v in context.currentProject.sceneData.CustomVariables)
                {
                    if (!boolOnly || v.type == "bool")
                    {
                        if (!boolOnly && (v.type == "vector2" || v.type == "vector3"))
                        {
                            DrawFinalItem(v.name + ".x", prefix + v.name + ".x");
                            DrawFinalItem(v.name + ".y", prefix + v.name + ".y");
                            if (v.type == "vector3") DrawFinalItem(v.name + ".z", prefix + v.name + ".z");
                        }
                        else DrawFinalItem(v.name, prefix + v.name);
                    }
                }
            }

            if (!boolOnly)
            {
                if (selectedGroup == "Camera") { DrawVector3Group("CameraPosition", prefix + "CameraPosition", "Camera position in world space"); DrawVector3Group("CameraRotation", prefix + "CameraRotation", "Camera rotation in degrees"); }
                else if (selectedGroup == "Mouse")
                {
                    // GameManager.Mouse is the screen-space vector; MouseWorld is world-space.
                    DrawVector3Group("Mouse Screen", prefix + "Mouse", "Mouse position in screen coordinates");
                    DrawVector3Group("Mouse World", prefix + "MouseWorld", "Mouse position in world space");
                }
                else if (selectedGroup == "Sun") { DrawVector3Group("SunPosition", prefix + "SunPosition", "Sun position in world space"); DrawVector3Group("SunRotation", prefix + "SunRotation", "Sun rotation in degrees"); }
                else if (selectedGroup == "Physics") { DrawVector3Group("Gravity", prefix + "Gravity", "Gravity applied to the scene"); }
            }
        }

        private void DrawActorProperties()
        {
            string prefix = (selectedCategory == "Me") ? "this." : selectedCategory + ".";
            ActorJson targetData = (selectedCategory == "Me") ? currentActor : context.currentProject.actors.Find(a => a.ActorName == selectedCategory);

            if (propertyDefinitions.ContainsKey(selectedGroup))
            {
                var props = propertyDefinitions[selectedGroup];
                foreach (var prop in props)
                {
                    if (!boolOnly || prop.IsBool) DrawFinalItem(prop.Label, prefix + prop.Suffix, prop.Tooltip);
                }
            }

            if (selectedGroup == "Custom" && targetData?.Properties != null)
            {
                foreach (var prop in targetData.Properties)
                {
                    string propName = prop.Contains("=") ? prop.Split('=')[0] : prop;
                    DrawFinalItem(propName, prefix + propName);
                }
            }
        }

        private void DrawVector3Group(string name, string fullPrefix, string description)
        {
            DrawFinalItem(name + " X", fullPrefix + ".x", description + " on the X axis.");
            DrawFinalItem(name + " Y", fullPrefix + ".y", description + " on the Y axis.");
            DrawFinalItem(name + " Z", fullPrefix + ".z", description + " on the Z axis.");
        }

        private void DrawFinalItem(string label, string result, string tooltip = null)
        {
            if (DrawTintedButton(label, propertyButtonStyle, propertyColoredLabelStyle, false, tooltip))
            {
                onPick?.Invoke(result);
                Close();
            }
        }

        private void EnsureStyles()
        {
            if (navigationButtonStyle != null &&
                propertyButtonStyle != null &&
                navigationColoredLabelStyle != null &&
                propertyColoredLabelStyle != null &&
                stylesAreProSkin == EditorGUIUtility.isProSkin)
            {
                return;
            }

            navigationButtonStyle = CreateButtonStyle(TextAnchor.MiddleLeft);
            navigationButtonStyle.clipping = TextClipping.Clip;
            propertyButtonStyle = CreateButtonStyle(TextAnchor.MiddleLeft);
            propertyButtonStyle.padding = new RectOffset(8, 8, 2, 2);
            navigationButtonStyle.padding = new RectOffset(8, 8, 2, 2);
            navigationColoredLabelStyle = CreateColoredLabelStyle(TextAnchor.MiddleLeft, new RectOffset(8, 8, 2, 2));
            propertyColoredLabelStyle = CreateColoredLabelStyle(TextAnchor.MiddleLeft, new RectOffset(8, 8, 2, 2));
            stylesAreProSkin = EditorGUIUtility.isProSkin;
        }

        private static GUIStyle CreateButtonStyle(TextAnchor alignment)
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                alignment = alignment,
                stretchWidth = true
            };
        }

        private static GUIStyle CreateColoredLabelStyle(TextAnchor alignment, RectOffset padding)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = alignment,
                padding = padding
            };
            Color textColor = new Color32(30, 30, 30, 255);
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
            return style;
        }

        private bool DrawTintedButton(string label, GUIStyle buttonStyle, GUIStyle coloredLabelStyle, bool selected, string tooltip = null)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                buttonStyle,
                GUILayout.Height(22),
                GUILayout.ExpandWidth(true));

            Vector2 buttonScreenPosition = GUIUtility.GUIToScreenPoint(buttonRect.position);
            Rect buttonScreenRect = new Rect(buttonScreenPosition, buttonRect.size);
            Rect visibleButtonScreenRect = Intersect(buttonScreenRect, currentColumnScreenRect);
            bool hovered = visibleButtonScreenRect.Contains(mouseScreenPosition);
            bool pressed = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            bool colored = selected || hovered;
            // Register the tooltip only over the visible part of this column's button.
            // The colored button draws its label separately, but still needs GUIContent for hover help.
            var content = new GUIContent(label, hovered ? tooltip : null);
            bool clicked = GUI.Button(buttonRect,
                colored ? new GUIContent(string.Empty, hovered ? tooltip : null) : content,
                buttonStyle);

            if (colored)
            {
                Color fillColor = pressed
                    ? PickerYellowActive
                    : hovered ? PickerYellowHover : PickerYellow;
                var fillRect = new Rect(
                    buttonRect.x + 2f,
                    buttonRect.y + 2f,
                    Mathf.Max(0f, buttonRect.width - 4f),
                    Mathf.Max(0f, buttonRect.height - 4f));
                EditorGUI.DrawRect(fillRect, fillColor);
                GUI.Label(buttonRect, content, coloredLabelStyle);
            }

            return clicked;
        }

        private static Rect Intersect(Rect first, Rect second)
        {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            return xMax > xMin && yMax > yMin
                ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                : Rect.zero;
        }
    }
}
