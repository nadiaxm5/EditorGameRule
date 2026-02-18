using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Linq;

namespace GameRuleEditor.Windows
{
    public class PropertyPickerDialog : EditorWindow
    {
        // --- CONFIGURATION ---
        private struct PropDef
        { public string Label; public string Suffix; public bool IsBool; }

        private readonly Dictionary<string, List<PropDef>> propertyDefinitions = new Dictionary<string, List<PropDef>>
        {
            { "Transform", new List<PropDef> {
                new PropDef { Label = "Pos X", Suffix = "x", IsBool = false },
                new PropDef { Label = "Pos Y", Suffix = "y", IsBool = false },
                new PropDef { Label = "Pos Z", Suffix = "z", IsBool = false },
                new PropDef { Label = "Rot X", Suffix = "rx", IsBool = false },
                new PropDef { Label = "Rot Y", Suffix = "ry", IsBool = false },
                new PropDef { Label = "Rot Z", Suffix = "rz", IsBool = false },
                new PropDef { Label = "Scale X", Suffix = "sx", IsBool = false },
                new PropDef { Label = "Scale Y", Suffix = "sy", IsBool = false },
                new PropDef { Label = "Scale Z", Suffix = "sz", IsBool = false }
            }},
            { "Physics", new List<PropDef> {
                new PropDef { Label = "Velocity X", Suffix = "Velocity.x", IsBool = false },
                new PropDef { Label = "Velocity Y", Suffix = "Velocity.y", IsBool = false },
                new PropDef { Label = "Velocity Z", Suffix = "Velocity.z", IsBool = false },
                new PropDef { Label = "Ang.Vel X", Suffix = "AngularVelocity.x", IsBool = false },
                new PropDef { Label = "Ang.Vel Y", Suffix = "AngularVelocity.y", IsBool = false },
                new PropDef { Label = "Ang.Vel Z", Suffix = "AngularVelocity.z", IsBool = false },
                new PropDef { Label = "Density", Suffix = "Density", IsBool = false },
                new PropDef { Label = "Friction", Suffix = "Friction", IsBool = false },
                new PropDef { Label = "Bounciness", Suffix = "Bounciness", IsBool = false },
                new PropDef { Label = "Drag", Suffix = "Drag", IsBool = false }
            }},
            { "State", new List<PropDef> {
                new PropDef { Label = "Active", Suffix = "Active", IsBool = true }
            }},
            { "UI", new List<PropDef> {
                new PropDef { Label = "Slider Value", Suffix = "sliderValue", IsBool = false },
                new PropDef { Label = "Text Content", Suffix = "text", IsBool = false }
            }}
        };

        // ---------------------

        private System.Action<string> onPick;
        private EditorContext context;

        // Filters
        private bool boolOnly = false;

        private bool actorsOnly = false;
        private System.Type resourceType = null; // [New] Filter for resources

        private string selectedCategory = "Me";
        private string selectedGroup = "Transform";

        private Vector2 scrollCategory;
        private Vector2 scrollGroup;
        private Vector2 scrollProps;

        private List<string> actorNames;
        private ActorJson currentActor;

        // [Updated] Added resourceFilter parameter
        public static void Show(EditorContext ctx, System.Action<string> callback, bool onlyBooleans = false, bool onlyActors = false, System.Type resourceFilter = null)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Property", true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = onlyBooleans;
            win.actorsOnly = onlyActors;
            win.resourceType = resourceFilter;
            win.minSize = new Vector2(500, 300);
            win.InitData();
            win.ShowUtility();
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

        private void OnGUI()
        {
            // 1. Resource Mode (New)
            if (resourceType != null)
            {
                DrawResourceMode();
                return;
            }

            // 2. Actors Only Mode
            if (actorsOnly)
            {
                DrawActorsOnlyMode();
                return;
            }

            // 3. Standard Property Mode
            EditorGUILayout.BeginHorizontal();

            // Col 1: Category
            DrawColumn(ref scrollCategory, 150, () =>
            {
                DrawSelectable("Me (this)", "Me");
                DrawSelectable("Game (#)", "Game");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
                foreach (var actorName in actorNames) DrawSelectable(actorName, actorName);
            });

            // Col 2: Group
            DrawColumn(ref scrollGroup, 150, () =>
            {
                if (selectedCategory == "Game")
                {
                    DrawGroupSelectable("Global Variables", "Global");
                    if (!boolOnly)
                    {
                        DrawGroupSelectable("Camera", "Camera");
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

            // Col 3: Properties
            DrawColumn(ref scrollProps, 200, () =>
            {
                if (selectedCategory == "Game") DrawGameProperties();
                else DrawActorProperties();
            });

            EditorGUILayout.EndHorizontal();
        }

        // [New] Draws list of files in Resources folder matching the type
        private void DrawResourceMode()
        {
            EditorGUILayout.LabelField($"Select {resourceType.Name}:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory);

            // Find all assets of type in Resources folder
            var assets = Resources.LoadAll("", resourceType);

            if (assets.Length == 0)
            {
                EditorGUILayout.HelpBox($"No {resourceType.Name} found in Resources folder.", MessageType.Info);
            }

            foreach (var asset in assets)
            {
                if (GUILayout.Button(asset.name, EditorStyles.miniButton))
                {
                    onPick?.Invoke(asset.name);
                    Close();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActorsOnlyMode()
        {
            EditorGUILayout.LabelField("Select Actor / Prefab:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollCategory = EditorGUILayout.BeginScrollView(scrollCategory);

            foreach (var actorName in actorNames)
            {
                if (GUILayout.Button(actorName, EditorStyles.miniButton))
                {
                    onPick?.Invoke(actorName);
                    Close();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resources/Prefabs:", EditorStyles.boldLabel);

            var prefabs = Resources.LoadAll<GameObject>("Prefabs");
            foreach (var p in prefabs)
            {
                if (GUILayout.Button(p.name, EditorStyles.miniButton))
                {
                    onPick?.Invoke(p.name);
                    Close();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawColumn(ref Vector2 scroll, float width, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            scroll = EditorGUILayout.BeginScrollView(scroll); drawContent(); EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
        }

        private void DrawSelectable(string label, string id)
        {
            GUI.backgroundColor = (selectedCategory == id) ? Color.cyan : Color.white;
            string defaultGroup = (id == "Game") ? "Global" : "Transform";
            if (GUILayout.Button(label, EditorStyles.miniButton)) { selectedCategory = id; selectedGroup = defaultGroup; }
            GUI.backgroundColor = Color.white;
        }

        private void DrawGroupSelectable(string label, string id)
        {
            GUI.backgroundColor = (selectedGroup == id) ? Color.cyan : Color.white;
            if (GUILayout.Button(label, EditorStyles.miniButton)) selectedGroup = id;
            GUI.backgroundColor = Color.white;
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
                if (selectedGroup == "Camera") { DrawVector3Group("CameraPosition", prefix + "CameraPosition"); DrawVector3Group("CameraRotation", prefix + "CameraRotation"); }
                else if (selectedGroup == "Sun") { DrawVector3Group("SunPosition", prefix + "SunPosition"); DrawVector3Group("SunRotation", prefix + "SunRotation"); }
                else if (selectedGroup == "Physics") { DrawVector3Group("Gravity", prefix + "Gravity"); }
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
                    if (!boolOnly || prop.IsBool) DrawFinalItem(prop.Label, prefix + prop.Suffix);
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

        private void DrawVector3Group(string name, string fullPrefix)
        {
            DrawFinalItem(name + " X", fullPrefix + ".x"); DrawFinalItem(name + " Y", fullPrefix + ".y"); DrawFinalItem(name + " Z", fullPrefix + ".z");
        }

        private void DrawFinalItem(string label, string result)
        {
            if (GUILayout.Button(label, EditorStyles.label)) { onPick?.Invoke(result); Close(); }
        }
    }
}