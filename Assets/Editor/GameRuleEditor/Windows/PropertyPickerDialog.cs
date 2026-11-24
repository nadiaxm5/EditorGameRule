using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Animations;

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


        private GUIStyle navigationButtonStyle;
        private GUIStyle propertyButtonStyle;
        private GUIStyle navigationColoredLabelStyle;
        private GUIStyle propertyColoredLabelStyle;
        private GUIStyle pickerSurfaceStyle;
        private GUIStyle pickerColumnStyle;
        private bool stylesAreProSkin;
        private Vector2 mouseScreenPosition;
        private Rect currentColumnScreenRect;

        // Filters
        private bool boolOnly = false;

        private System.Type resourceType = null; // [New] Filter for resources
        private bool includeAllResourceAssets;
        private bool showActorResources = true;
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
            public bool HidePreview;
        }

        internal static bool UsesActorResources(System.Type type) =>
            type == typeof(AnimationClip) || type == typeof(ParticleSystem);

        private static List<PreviewGridItem> GetActorResources(EditorContext ctx, System.Type type, out string warning)
        {
            var result = new List<PreviewGridItem>();
            warning = null;
            var actor = ctx?.SelectedActor;
            GameObject prefab = string.IsNullOrEmpty(actor?.PrefabName) ? null :
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/" + actor.PrefabName + ".prefab");
            if (prefab == null)
            {
                warning = type == typeof(AnimationClip)
                    ? "Assign a prefab to this actor before choosing an Animator state."
                    : "Assign a prefab to this actor before choosing a particle system.";
                return result;
            }

            if (type == typeof(AnimationClip))
            {
                // Match Utils.PlayAnimation: only the Animator on the actor root is used.
                var animator = prefab.GetComponent<Animator>();
                if (animator == null) { warning = "Animate requires an Animator on the actor prefab's root."; return result; }
                RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
                var visited = new HashSet<RuntimeAnimatorController>();
                while (runtime is AnimatorOverrideController overrides && visited.Add(runtime))
                    runtime = overrides.runtimeAnimatorController;
                if (!(runtime is AnimatorController controller))
                { warning = "Assign an Animator Controller with playable states to the actor's Animator."; return result; }

                foreach (var layer in controller.layers)
                {
                    AnimatorStateMachine machine = layer.stateMachine;
                    if (layer.syncedLayerIndex >= 0 && layer.syncedLayerIndex < controller.layers.Length)
                        machine = controller.layers[layer.syncedLayerIndex].stateMachine;
                    AddAnimatorStates(result, machine, layer.name, new HashSet<AnimatorStateMachine>());
                }
                if (!animator.enabled) warning = "Enable the Animator component on the actor prefab to play its states.";
                else if (result.Count == 0) warning = "This actor's Animator Controller has no playable states.";
            }
            else
            {
                // Runtime searches active children; the instantiated root is renamed to ActorName.
                foreach (var system in prefab.GetComponentsInChildren<ParticleSystem>())
                {
                    string name = system.gameObject == prefab ? actor.ActorName : system.gameObject.name;
                    if (result.Any(item => item.Result == name)) continue;
                    result.Add(new PreviewGridItem { Label = name, Result = name,
                        SecondaryName = system.gameObject == prefab ? "Actor root" : AnimationUtility.CalculateTransformPath(system.transform, prefab.transform),
                        PreviewObject = system });
                }
                if (result.Count == 0)
                    warning = "PlayParticles requires a Particle System on the actor prefab or an active child.";
            }
            return result.OrderBy(item => item.Label, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Result, System.StringComparer.Ordinal).ToList();
        }

        private static void AddAnimatorStates(List<PreviewGridItem> items, AnimatorStateMachine machine,
                                             string path, HashSet<AnimatorStateMachine> visited)
        {
            if (machine == null || !visited.Add(machine)) return;
            foreach (var child in machine.states)
            {
                var state = child.state;
                if (state == null) continue;
                items.Add(new PreviewGridItem { Label = state.name, Result = path + "." + state.name,
                    SecondaryName = path + "." + state.name,
                    PreviewObject = state.motion != null ? (Object)state.motion : state });
            }
            foreach (var child in machine.stateMachines)
                if (child.stateMachine != null)
                    AddAnimatorStates(items, child.stateMachine, path + "." + child.stateMachine.name, visited);
        }

        internal static string ActorResourceWarning(EditorContext ctx, System.Type type, string value)
        {
            if (!UsesActorResources(type)) return null;
            var choices = GetActorResources(ctx, type, out string warning);
            if (warning != null) return warning;
            if (string.IsNullOrEmpty(value)) return null;
            if (choices.Any(item => item.Result == value || type == typeof(AnimationClip) && item.Label == value)) return null;
            return type == typeof(AnimationClip)
                ? "The selected name is not a state in this actor's Animator Controller. Choose a state; a clip name alone is not enough."
                : "No active Particle System with this name exists on the actor prefab or its children.";
        }

        private enum PropertyScope { Self, Global, Actor }
        private PropertyScope selectedScope = PropertyScope.Self;
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
            win.showActorResources = true;
            win.includeAllResourceAssets = false;
            win.prefabMode = false;
            win.prefabSearch = string.Empty;
            win.minSize = new Vector2(500, 300);
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
        }

        public static void ShowPrefab(EditorContext ctx, System.Action<string> callback, bool includeActorPrefabs,
                                      Rect? anchorScreenRect = null)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Prefab", true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = false;
            win.resourceType = null;
            win.includeAllResourceAssets = false;
            win.prefabMode = true;
            win.includeActorPrefabs = includeActorPrefabs;
            win.selectedPrefabSource = includeActorPrefabs
                ? PrefabSource.ActorPrefabs
                : PrefabSource.AllPrefabs;
            win.prefabSearch = string.Empty;
            win.scrollCategory = Vector2.zero;
            win.minSize = new Vector2(360, 220);
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
        }

        public static void ShowResource(EditorContext ctx, System.Action<string> callback,
                                        System.Type resourceFilter, string windowTitle,
                                        string allResourcesLabel, Rect? anchorScreenRect = null,
                                        bool includeAllAssets = false)
        {
            var win = GetWindow<PropertyPickerDialog>(true, windowTitle, true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = false;
            win.resourceType = resourceFilter;
            win.includeAllResourceAssets = includeAllAssets;
            win.showActorResources = true;
            win.prefabMode = false;
            win.resourceTabLabel = allResourcesLabel;
            win.resourceSearch = string.Empty;
            win.scrollCategory = Vector2.zero;
            win.minSize = new Vector2(360, 220);
            win.wantsMouseMove = true;
            win.InitData();
            win.ShowUtility();
            PositionNearAnchor(win, anchorScreenRect, new Vector2(500, 300));
        }

        /// <summary>Converts a UI Toolkit element's panel-space bounds to desktop screen coordinates.</summary>
        public static Rect GetScreenRect(VisualElement element)
        {
            if (element?.panel == null) return default;

            // Focus and mouse-over can already point at another window when a picker opens.
            // Resolve the window that actually contains the originating control.
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var root = window.rootVisualElement;
                if (root.panel != element.panel || (root != element && !root.Contains(element)))
                    continue;

                Rect screenRect = element.worldBound;
                screenRect.position += window.position.position - root.worldBound.position;
                return screenRect;
            }
            return default;
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

            if (y + windowSize.y > desktop.yMax)
                y = anchor.yMin - windowSize.y - gap;

            x = Mathf.Clamp(x, desktop.xMin, Mathf.Max(desktop.xMin, desktop.xMax - windowSize.x));
            y = Mathf.Clamp(y, desktop.yMin, Mathf.Max(desktop.yMin, desktop.yMax - windowSize.y));

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

            return Regex.Replace(value, @"(?<![A-Za-z0-9_])this\.", actorName + ".");
        }

        public static string ToStoredReference(EditorContext ctx, string value)
        {
            // Picker results and loaded operands already carry their scope (this., Actor., #).
            // Display text is deliberately ambiguous and must never be used to infer it.
            return value;
        }

        public static bool IsGlobalReference(EditorContext ctx, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            Match match = Regex.Match(value.Trim(),
                @"^#(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\.[xyz])?$");
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
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), GameRuleTheme.Panel);
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
            const float columnGap = outerPadding;
            float availableWidth = Mathf.Max(0f, position.width - (outerPadding * 2f) - (columnGap * 2f));
            float columnWidth = availableWidth / 3f;

            GUILayout.Space(outerPadding);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);

            // Col 1: Category
            DrawColumn(ref scrollCategory, columnWidth, () =>
            {
                string meLabel = currentActor == null ? "Me" : $"Me ({currentActor.ActorName})";
                DrawSelectable(meLabel, "Me", PropertyScope.Self);
                DrawSelectable("Game (global)", "Game", PropertyScope.Global);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
                foreach (var actorName in actorNames) DrawSelectable(actorName, actorName, PropertyScope.Actor);
            }, hideHorizontalScrollbar: true);

            GUILayout.Space(columnGap);

            // Col 2: Group
            DrawColumn(ref scrollGroup, columnWidth, () =>
            {
                if (selectedScope == PropertyScope.Global)
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
                if (selectedScope == PropertyScope.Global) DrawGameProperties();
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

            bool hasActorTab = UsesActorResources(resourceType);
            string actorTabLabel = resourceType == typeof(AnimationClip) ? "Actor Animations" : "Actor Particles";
            if (hasActorTab)
            {
                float actorTabWidth = EditorStyles.miniButton.CalcSize(new GUIContent(actorTabLabel)).x + 18f;
                if (DrawYellowTabButton(actorTabLabel, showActorResources, actorTabWidth))
                {
                    showActorResources = true;
                    scrollCategory = Vector2.zero;
                }
                GUILayout.Space(4f);
            }
            float tabWidth = Mathf.Clamp(EditorStyles.miniButton.CalcSize(
                new GUIContent(resourceTabLabel)).x + 18f, 96f, 140f);
            if (DrawYellowTabButton(resourceTabLabel, !hasActorTab || !showActorResources, tabWidth))
            {
                showActorResources = false;
                scrollCategory = Vector2.zero;
            }

            if (position.width < 420f)
            {
                GUILayout.Space(outerPadding);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(outerPadding);
            }
            GUILayout.FlexibleSpace();
            float searchWidth = Mathf.Clamp(position.width * 0.32f, 115f, 210f);
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
            resourceSearch = EditorGUILayout.TextField(resourceSearch ?? string.Empty, searchStyle,
                GUILayout.Width(searchWidth), GUILayout.Height(22f));

            GUILayout.Space(outerPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);

            List<PreviewGridItem> actorResources = null;
            if (hasActorTab && showActorResources)
            {
                actorResources = GetActorResources(context, resourceType, out string warning);
                string hint = resourceType == typeof(AnimationClip)
                    ? "States from this actor's Animator Controller. Selecting a clip asset alone does not create a state."
                    : "Particle systems already on this actor or its active children. Selecting an asset does not add a system.";
                EditorGUILayout.HelpBox(warning ?? hint, warning == null ? MessageType.Info : MessageType.Warning);
            }
            else if (hasActorTab)
            {
                string hint = resourceType == typeof(AnimationClip)
                    ? "All animation clips. Animate still requires a matching state in this actor's Animator Controller."
                    : "All particle resources. PlayParticles still requires a matching system on this actor or an active child.";
                EditorGUILayout.HelpBox(hint, MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(outerPadding);
            EditorGUILayout.BeginVertical(pickerSurfaceStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory, GUILayout.ExpandHeight(true));

            var assets = actorResources;
            if (assets == null)
            {
                var source = includeAllResourceAssets
                    ? AssetDatabase.FindAssets("t:" + resourceType.Name)
                        .Select(guid => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), resourceType))
                        .Where(asset => asset != null)
                    : Resources.LoadAll("", resourceType);
                assets = source.Select(asset => new PreviewGridItem
                {
                    Label = asset.name, Result = includeAllResourceAssets ? AssetDatabase.GetAssetPath(asset) : asset.name,
                    SecondaryName = resourceType.Name, PreviewObject = asset
                }).ToList();
            }
            assets = assets.OrderBy(item => item.Label, System.StringComparer.OrdinalIgnoreCase)
                .Where(item => MatchesSearch(resourceSearch, item.Label, item.Result)).ToList();

            assets.Insert(0, new PreviewGridItem { Label = "None", Result = string.Empty, HidePreview = true });
            DrawPreviewGrid(assets);
            if (assets.Count == 1 && (actorResources == null || actorResources.Count > 0))
            {
                string tabLabel = hasActorTab && showActorResources ? actorTabLabel : resourceTabLabel;
                EditorGUILayout.HelpBox($"No items in {tabLabel} match the search.", MessageType.Info);
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
                DrawYellowTabButton("Project Prefabs", selectedPrefabSource == PrefabSource.ActorPrefabs, 112f))
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

            if (position.width < 420f)
            {
                GUILayout.Space(outerPadding);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(outerPadding);
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
            EditorGUILayout.BeginVertical(pickerSurfaceStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory, GUILayout.ExpandHeight(true));

            List<GameObject> selectablePrefabs = LoadSelectablePrefabs();
            var gridItems = new List<PreviewGridItem>();
            GameObject emptyPrefab = selectablePrefabs.Find(prefab => prefab.name == "Empty");
            if (emptyPrefab != null)
                gridItems.Add(new PreviewGridItem
                {
                    Label = emptyPrefab.name,
                    Result = emptyPrefab.name,
                    PreviewObject = emptyPrefab
                });
            int pinnedCount = gridItems.Count;

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
                    if (prefab == emptyPrefab) continue;
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

            DrawPreviewGrid(gridItems);
            if (gridItems.Count == pinnedCount)
            {
                string message = selectedPrefabSource == PrefabSource.ActorPrefabs
                    ? "No actor prefabs match the search."
                    : "No prefabs in Resources/Prefabs match the search.";
                EditorGUILayout.HelpBox(message, MessageType.Info);
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
            const float minimumCardWidth = 112f;
            const float maximumCardWidth = 136f;
            const float cardHeight = 138f;

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
            string assetPath = AssetDatabase.GetAssetPath(item.PreviewObject);
            if (!string.IsNullOrEmpty(assetPath)) tooltip += "\n" + assetPath;
            bool clicked = GUI.Button(cardRect, new GUIContent(string.Empty, tooltip), GUIStyle.none);

            GameRuleTheme.DrawPickerButton(cardRect, hovered, pressed);

            float previewSize = Mathf.Min(width - 12f, 76f);
            var previewRect = new Rect(
                cardRect.x + ((width - previewSize) * 0.5f),
                cardRect.y + 6f,
                previewSize,
                previewSize);

            Texture preview = item.HidePreview ? null : GetAssetPreview(item.PreviewObject);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);

            var labelRect = new Rect(cardRect.x + 5f, previewRect.yMax + 3f,
                width - 10f, Mathf.Max(20f, cardRect.yMax - previewRect.yMax - 6f));
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            // Wrap compound asset names visually; the stored reference and tooltip stay intact.
            string displayName = Regex.Replace(item.Label, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[_\-])", "\u200B");
            labelStyle.normal.textColor = labelStyle.hover.textColor =
                labelStyle.active.textColor = labelStyle.focused.textColor = GameRuleTheme.PropertyAccent;

            GUI.Label(labelRect, new GUIContent(displayName, tooltip), labelStyle);

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
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

            GameRuleTheme.DrawPickerButton(rect, hovered, pressed, selected);

            GUIStyle labelStyle = CreateColoredLabelStyle(TextAnchor.MiddleCenter, new RectOffset(2, 2, 2, 2));
            labelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(rect, label, labelStyle);
            return clicked;
        }

        private void DrawColumn(ref Vector2 scroll, float width, System.Action drawContent,
                                bool hideHorizontalScrollbar = false)
        {
            // IMGUI can lay out the contents at the full column width before deciding
            // that a vertical scrollbar is needed. Reserve its gutter explicitly so
            // buttons keep their right edge visible, including on the first frame.
            GUIStyle verticalScrollbar = GUI.skin.verticalScrollbar;
            float scrollbarGutter = Mathf.Max(14f, verticalScrollbar.fixedWidth) +
                verticalScrollbar.margin.left + verticalScrollbar.margin.right + 4f;
            float contentWidth = Mathf.Max(0f, width - pickerSurfaceStyle.padding.horizontal - scrollbarGutter);
            Rect columnRect = EditorGUILayout.BeginVertical(
                pickerColumnStyle, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            // Keep each column visibly separate from the darker, evenly sized gutters.
            Color columnFill = Color.Lerp(GameRuleTheme.Panel, GameRuleTheme.Surface, 0.5f);
            GUI.DrawTexture(columnRect, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                true, 0f, columnFill, 0f, 4f);
            Rect previousColumnScreenRect = currentColumnScreenRect;
            Vector2 columnScreenPosition = GUIUtility.GUIToScreenPoint(columnRect.position);
            currentColumnScreenRect = new Rect(columnScreenPosition, columnRect.size);
            currentColumnScreenRect.width = Mathf.Max(0f, currentColumnScreenRect.width - scrollbarGutter);

            scroll = hideHorizontalScrollbar
                ? EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, verticalScrollbar)
                : EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginVertical(GUILayout.Width(contentWidth), GUILayout.MaxWidth(contentWidth));
            drawContent();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            currentColumnScreenRect = previousColumnScreenRect;
        }

        private void DrawSelectable(string label, string id, PropertyScope scope)
        {
            string defaultGroup = scope == PropertyScope.Global ? "Global" : "Transform";
            if (DrawTintedButton(label, navigationButtonStyle, navigationColoredLabelStyle,
                selectedCategory == id && selectedScope == scope))
            {
                selectedCategory = id;
                selectedScope = scope;
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
            string prefix = selectedScope == PropertyScope.Self ? "this." : selectedCategory + ".";
            ActorJson targetData = selectedScope == PropertyScope.Self ? currentActor : context.currentProject.actors.Find(a => a.ActorName == selectedCategory);

            if (propertyDefinitions.ContainsKey(selectedGroup))
            {
                var props = propertyDefinitions[selectedGroup];
                foreach (var prop in props)
                {
                    if (!boolOnly || prop.IsBool) DrawFinalItem(prop.Label, prefix + prop.Suffix, prop.Tooltip);
                }
            }

            if (selectedGroup == "Custom" && targetData != null)
            {
                foreach (var property in targetData.GetCustomProperties())
                {
                    if (property.type == "vector2" || property.type == "vector3")
                    {
                        if (boolOnly) continue;
                        DrawFinalItem(property.name + ".x", prefix + property.name + ".x");
                        DrawFinalItem(property.name + ".y", prefix + property.name + ".y");
                        if (property.type == "vector3")
                            DrawFinalItem(property.name + ".z", prefix + property.name + ".z");
                    }
                    else
                    {
                        DrawFinalItem(property.name, prefix + property.name);
                    }
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
                pickerSurfaceStyle != null &&
                pickerColumnStyle != null &&
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
            pickerSurfaceStyle = new GUIStyle(EditorStyles.helpBox);
            pickerSurfaceStyle.normal.background = null;
            // Explicit outer padding and column gaps already reserve the margins.
            // Native helpBox margins would add width and clip the rightmost column.
            pickerColumnStyle = new GUIStyle(pickerSurfaceStyle) { margin = new RectOffset() };
        }

        private static GUIStyle CreateButtonStyle(TextAnchor alignment)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = alignment,
                fontStyle = FontStyle.Bold,
                stretchWidth = true
            };
            style.normal.textColor = style.hover.textColor = style.active.textColor =
                style.focused.textColor = GameRuleTheme.PropertyAccent;
            return style;
        }

        private static GUIStyle CreateColoredLabelStyle(TextAnchor alignment, RectOffset padding)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = alignment,
                fontStyle = FontStyle.Bold,
                padding = padding
            };
            Color textColor = GameRuleTheme.PropertyAccent;
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
            // ScrollView's layout may stretch the child beyond the requested width.
            // Bound the actual painted/clickable rectangle to this column's content
            // area, leaving the scrollbar and a small gap completely outside it.
            if (currentColumnScreenRect.width > 0f)
                buttonRect.width = Mathf.Max(0f, Mathf.Min(buttonRect.width,
                    currentColumnScreenRect.xMax - buttonScreenPosition.x - 4f));
            Rect buttonScreenRect = new Rect(buttonScreenPosition, buttonRect.size);
            Rect visibleButtonScreenRect = Intersect(buttonScreenRect, currentColumnScreenRect);
            bool hovered = visibleButtonScreenRect.Contains(mouseScreenPosition);
            bool pressed = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            bool colored = selected || hovered;
            // Register the tooltip only over the visible part of this column's button.
            // The colored button draws its label separately, but still needs GUIContent for hover help.
            var content = new GUIContent(label, hovered ? tooltip : null);
            bool clicked = GUI.Button(buttonRect, new GUIContent(string.Empty, hovered ? tooltip : null), GUIStyle.none);
            GameRuleTheme.DrawPickerButton(buttonRect, hovered, pressed, selected);
            var labelStyle = colored ? coloredLabelStyle : buttonStyle;
            labelStyle.normal.textColor = GameRuleTheme.PropertyAccent;
            GUI.Label(buttonRect, content, labelStyle);

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
