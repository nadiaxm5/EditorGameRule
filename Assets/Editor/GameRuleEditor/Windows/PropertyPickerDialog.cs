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

        // Navigation State
        private string selectedCategory = "Me"; // Me, Game, specific Actor

        private string selectedGroup = "Transform"; // Transform, Physics, Custom, etc.

        // Scroll Positions
        private Vector2 scrollCategory;

        private Vector2 scrollGroup;
        private Vector2 scrollProps;

        // Data Cache
        private List<string> actorNames;

        private ActorJson currentActor;

        public static void Show(EditorContext ctx, System.Action<string> callback)
        {
            var win = GetWindow<PropertyPickerDialog>(true, "Pick Property", true);
            win.context = ctx;
            win.onPick = callback;
            win.minSize = new Vector2(500, 300);
            win.InitData();
            win.ShowUtility(); // Modal-like behavior
        }

        private void InitData()
        {
            if (context?.currentProject == null) return;

            // Prepare actor list excluding "Me" (current selected) if possible
            currentActor = context.SelectedActor;
            actorNames = context.currentProject.actors
                .Select(a => a.ActorName)
                .Where(n => currentActor == null || n != currentActor.ActorName)
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // --- COLUMN 1: TARGET (Me, Game, Other Actors) ---
            DrawColumn(ref scrollCategory, 150, () =>
            {
                DrawSelectable("Me (this)", "Me");
                DrawSelectable("Game (#)", "Game");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
                foreach (var actorName in actorNames)
                {
                    DrawSelectable(actorName, actorName);
                }
            });

            // --- COLUMN 2: PROPERTY GROUPS ---
            DrawColumn(ref scrollGroup, 150, () =>
            {
                if (selectedCategory == "Game")
                {
                    DrawGroupSelectable("Global Variables", "Global");
                    DrawGroupSelectable("Camera", "Camera");
                    DrawGroupSelectable("Sun", "Sun");
                    DrawGroupSelectable("Physics", "Physics");
                }
                else // Me or Actor
                {
                    DrawGroupSelectable("Transform", "Transform");
                    DrawGroupSelectable("Physics", "Physics");
                    DrawGroupSelectable("State", "State");
                    DrawGroupSelectable("Custom Properties", "Custom");
                    DrawGroupSelectable("UI", "UI");
                }
            });

            // --- COLUMN 3: PROPERTIES ---
            DrawColumn(ref scrollProps, 200, () =>
            {
                if (selectedCategory == "Game") DrawGameProperties();
                else DrawActorProperties();
            });

            EditorGUILayout.EndHorizontal();
        }

        private void DrawColumn(ref Vector2 scroll, float width, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            drawContent();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectable(string label, string id)
        {
            GUI.backgroundColor = (selectedCategory == id) ? Color.cyan : Color.white;
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20)))
            {
                selectedCategory = id;
                // Reset group default when changing category
                if (id == "Game") selectedGroup = "Global";
                else selectedGroup = "Transform";
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawGroupSelectable(string label, string id)
        {
            GUI.backgroundColor = (selectedGroup == id) ? Color.cyan : Color.white;
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20)))
            {
                selectedGroup = id;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawGameProperties()
        {
            string prefix = "#";

            if (selectedGroup == "Global")
            {
                if (context.currentProject.sceneData.CustomVariables != null)
                {
                    foreach (var v in context.currentProject.sceneData.CustomVariables)
                    {
                        if (v.type == "vector2" || v.type == "vector3")
                        {
                            DrawFinalItem(v.name + ".x", prefix + v.name + ".x");
                            DrawFinalItem(v.name + ".y", prefix + v.name + ".y");
                            if (v.type == "vector3") DrawFinalItem(v.name + ".z", prefix + v.name + ".z");
                        }
                        else
                        {
                            DrawFinalItem(v.name, prefix + v.name);
                        }
                    }
                }
            }
            else if (selectedGroup == "Camera")
            {
                DrawVector3Group("CameraPosition", prefix + "CameraPosition");
                DrawVector3Group("CameraRotation", prefix + "CameraRotation");
            }
            else if (selectedGroup == "Sun")
            {
                DrawVector3Group("SunPosition", prefix + "SunPosition");
                DrawVector3Group("SunRotation", prefix + "SunRotation");
            }
            else if (selectedGroup == "Physics")
            {
                DrawVector3Group("Gravity", prefix + "Gravity");
            }
        }

        private void DrawActorProperties()
        {
            string prefix = (selectedCategory == "Me") ? "this." : selectedCategory + ".";

            // Try to find the target actor to show its specific custom properties
            ActorJson targetData = (selectedCategory == "Me") ? currentActor :
                                   context.currentProject.actors.Find(a => a.ActorName == selectedCategory);

            if (selectedGroup == "Transform")
            {
                DrawFinalItem("Position X", prefix + "x");
                DrawFinalItem("Position Y", prefix + "y");
                DrawFinalItem("Position Z", prefix + "z");
                DrawFinalItem("Rotation X", prefix + "rx");
                DrawFinalItem("Rotation Y", prefix + "ry");
                DrawFinalItem("Rotation Z", prefix + "rz");
                DrawFinalItem("Scale X", prefix + "sx");
                DrawFinalItem("Scale Y", prefix + "sy");
                DrawFinalItem("Scale Z", prefix + "sz");
            }
            else if (selectedGroup == "Physics")
            {
                // Mapped according to Utils.cs support logic
                DrawFinalItem("Velocity X", prefix + "Velocity.x");
                DrawFinalItem("Velocity Y", prefix + "Velocity.y");
                DrawFinalItem("Velocity Z", prefix + "Velocity.z");
                DrawFinalItem("Ang.Vel X", prefix + "AngularVelocity.x");
                DrawFinalItem("Ang.Vel Y", prefix + "AngularVelocity.y");
                DrawFinalItem("Ang.Vel Z", prefix + "AngularVelocity.z");
                DrawFinalItem("Density", prefix + "Density");
                DrawFinalItem("Friction", prefix + "Friction");
                DrawFinalItem("Bounciness", prefix + "Bounciness");
                DrawFinalItem("Drag", prefix + "Drag");
            }
            else if (selectedGroup == "State")
            {
                DrawFinalItem("Active", prefix + "Active");
            }
            else if (selectedGroup == "UI")
            {
                DrawFinalItem("Slider Value", prefix + "sliderValue");
                DrawFinalItem("Text Content", prefix + "text");
            }
            else if (selectedGroup == "Custom")
            {
                if (targetData != null && targetData.Properties != null)
                {
                    if (targetData.Properties.Count == 0)
                    {
                        EditorGUILayout.LabelField("No custom properties defined.");
                    }
                    foreach (var prop in targetData.Properties)
                    {
                        // prop string is "name=value". We extract "name".
                        string propName = prop.Contains("=") ? prop.Split('=')[0] : prop;
                        DrawFinalItem(propName, prefix + propName);
                    }
                }
            }
        }

        private void DrawVector3Group(string name, string fullPrefix)
        {
            DrawFinalItem(name + " X", fullPrefix + ".x");
            DrawFinalItem(name + " Y", fullPrefix + ".y");
            DrawFinalItem(name + " Z", fullPrefix + ".z");
        }

        private void DrawFinalItem(string label, string resultString)
        {
            if (GUILayout.Button(label, EditorStyles.label))
            {
                onPick?.Invoke(resultString);
                Close();
            }
        }
    }
}