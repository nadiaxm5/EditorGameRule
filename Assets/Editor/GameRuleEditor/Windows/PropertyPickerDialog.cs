using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using GameRuleEditor.Core;
using System.Linq;

namespace GameRuleEditor.Windows
{
    public class PropertyPickerDialog : EditorWindow
    {
        private System.Action<string> onPick;
        private EditorContext context;
        private bool boolOnly = false;
        private bool actorsOnly = false;

        private string selectedCategory = "Me";
        private string selectedGroup = "Transform";

        private Vector2 scrollCategory;
        private Vector2 scrollGroup;
        private Vector2 scrollProps;

        private List<string> actorNames;
        private ActorJson currentActor;

        public static void Show(EditorContext ctx, System.Action<string> callback, bool onlyBooleans = false, bool onlyActors = false)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Property", true);
            win.context = ctx;
            win.onPick = callback;
            win.boolOnly = onlyBooleans;
            win.actorsOnly = onlyActors;
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
            // If Actors Only mode, skip the 3-column layout and just show a list
            if (actorsOnly)
            {
                DrawActorsOnlyMode();
                return;
            }

            // Standard 3-column layout
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
                    if (!boolOnly)
                    {
                        DrawGroupSelectable("Transform", "Transform");
                        DrawGroupSelectable("Physics", "Physics");
                    }
                    DrawGroupSelectable("State", "State");
                    DrawGroupSelectable("Custom Properties", "Custom");
                    if (!boolOnly) DrawGroupSelectable("UI", "UI");
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
            if (GUILayout.Button(label, EditorStyles.miniButton)) { selectedCategory = id; selectedGroup = (id == "Game") ? "Global" : "Transform"; }
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
            if (selectedGroup == "Global" && context.currentProject.sceneData.CustomVariable != null)
            {
                foreach (var v in context.currentProject.sceneData.CustomVariable)
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
            if (!boolOnly && selectedGroup == "Camera") { DrawVector3Group("CameraPosition", prefix + "CameraPosition"); DrawVector3Group("CameraRotation", prefix + "CameraRotation"); }
            if (!boolOnly && selectedGroup == "Sun") { DrawVector3Group("SunPosition", prefix + "SunPosition"); DrawVector3Group("SunRotation", prefix + "SunRotation"); }
            if (!boolOnly && selectedGroup == "Physics") { DrawVector3Group("Gravity", prefix + "Gravity"); }
        }

        private void DrawActorProperties()
        {
            string prefix = (selectedCategory == "Me") ? "this." : selectedCategory + ".";
            ActorJson targetData = (selectedCategory == "Me") ? currentActor : context.currentProject.actors.Find(a => a.ActorName == selectedCategory);

            if (selectedGroup == "State") DrawFinalItem("Active", prefix + "Active");

            if (selectedGroup == "Custom" && targetData?.Properties != null)
            {
                foreach (var prop in targetData.Properties)
                {
                    string propName = prop.Contains("=") ? prop.Split('=')[0] : prop;
                    DrawFinalItem(propName, prefix + propName);
                }
            }

            if (boolOnly) return;

            if (selectedGroup == "Transform")
            {
                DrawFinalItem("Pos X", prefix + "x"); DrawFinalItem("Pos Y", prefix + "y"); DrawFinalItem("Pos Z", prefix + "z");
                DrawFinalItem("Rot X", prefix + "rx"); DrawFinalItem("Rot Y", prefix + "ry"); DrawFinalItem("Rot Z", prefix + "rz");
                DrawFinalItem("Scale X", prefix + "sx"); DrawFinalItem("Scale Y", prefix + "sy"); DrawFinalItem("Scale Z", prefix + "sz");
            }
            if (selectedGroup == "Physics")
            {
                DrawFinalItem("Velocity X", prefix + "Velocity.x"); DrawFinalItem("Velocity Y", prefix + "Velocity.y"); DrawFinalItem("Velocity Z", prefix + "Velocity.z");
                DrawFinalItem("Ang.Vel X", prefix + "AngularVelocity.x"); DrawFinalItem("Ang.Vel Y", prefix + "AngularVelocity.y"); DrawFinalItem("Ang.Vel Z", prefix + "AngularVelocity.z");
                DrawFinalItem("Density", prefix + "Density"); DrawFinalItem("Friction", prefix + "Friction"); DrawFinalItem("Bounciness", prefix + "Bounciness"); DrawFinalItem("Drag", prefix + "Drag");
            }
            if (selectedGroup == "UI")
            {
                DrawFinalItem("Slider Value", prefix + "sliderValue");
                DrawFinalItem("Text Content", prefix + "text");
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