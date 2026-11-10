using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using GameRuleEditor.Core;
using GameRuleEditor.Controllers;

namespace GameRuleEditor.Panels
{
    /// <summary>
    /// Panel for editing scene settings (GameManager properties)
    /// </summary>
    public class SceneSettingsPanel : VisualElement
    {
        private EditorContext context;
        private ProjectController controller;

        // Scene Fields
        private TextField gameNameField;

        private Vector2Field screenResolutionField;

        // Camera
        private Vector3Field cameraPosField;

        private Vector3Field cameraRotField;

        // Lighting
        private Vector3Field sunPosField;

        private Vector3Field sunRotField;
        private ColorField sunColorField;
        private ColorField sunAmbientField;

        // Physics / Background
        private ColorField backgroundColorField;

        private Vector3Field gravityField;
        private ObjectField soundTrackField;

        private CustomPropertyList customVariables;

        public SceneSettingsPanel(EditorContext editorContext, ProjectController projectController)
        {
            context = editorContext;
            controller = projectController;

            style.flexGrow = 1;
            AddToClassList("panel-container");

            CreateUI();
            UpdateUI();

            // Subscribe to events
            context.OnProjectLoaded += UpdateUI;
            context.OnProjectChanged += UpdateUI;

            // Cleanup when removed from visual tree
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                context.OnProjectLoaded -= UpdateUI;
                context.OnProjectChanged -= UpdateUI;
            });
        }

        private void CreateUI()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            // Header
            var header = new Label("Scene Settings");
            header.AddToClassList("panel-header");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 15;
            scrollView.Add(header);

            // Basic settings section
            var basicSection = CreateSection("Basic Settings");
            scrollView.Add(basicSection);

            // Vinculación nativa (El binding)
            gameNameField = CreateTextField(basicSection, "Game Name:");
            // Secondary callback to keep projectName in sync (native binding handles GameName + undo)
            gameNameField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject != null)
                    context.currentProject.projectName = evt.newValue;
            });

            screenResolutionField = CreateVector2Field(basicSection, "Screen Resolution:");
            screenResolutionField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector2Field (Vector2) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Screen Resolution");
                context.currentProject.sceneData.ScreenResolution = new float[] { evt.newValue.x, evt.newValue.y };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            backgroundColorField = CreateColorField(basicSection, "Background Color:");
            backgroundColorField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Background Color");
                context.currentProject.sceneData.BackgroundColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Camera section
            var cameraSection = CreateSection("Camera");
            scrollView.Add(cameraSection);

            cameraPosField = CreateVector3Field(cameraSection, "Position:");
            cameraPosField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Camera Position");
                context.currentProject.sceneData.CameraPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                controller.SyncCameraToScene();
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            cameraRotField = CreateVector3Field(cameraSection, "Rotation:");
            cameraRotField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Camera Rotation");
                context.currentProject.sceneData.CameraRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                controller.SyncCameraToScene();
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Lighting section
            var lightingSection = CreateSection("Lighting (Sun)");
            scrollView.Add(lightingSection);

            sunPosField = CreateVector3Field(lightingSection, "Position:");
            sunPosField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Position");
                context.currentProject.sceneData.SunPosition = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunRotField = CreateVector3Field(lightingSection, "Rotation:");
            sunRotField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Rotation");
                context.currentProject.sceneData.SunRotation = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunColorField = CreateColorField(lightingSection, "Sun Color:");
            sunColorField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Sun Color");
                context.currentProject.sceneData.SunColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            sunAmbientField = CreateColorField(lightingSection, "Ambient Color:");
            sunAmbientField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: ColorField (Color) -> byte[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Ambient Color");
                context.currentProject.sceneData.SunAmbientColor = ColorToBytes(evt.newValue);
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Physics section
            var physicsSection = CreateSection("Physics");
            scrollView.Add(physicsSection);

            gravityField = CreateVector3Field(physicsSection, "Gravity:");
            gravityField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;
                // Manual Undo: Vector3Field (Vector3) -> float[] (type mismatch)
                Undo.RecordObject(context.currentProject, "Change Gravity");
                context.currentProject.sceneData.Gravity = new float[] { evt.newValue.x, evt.newValue.y, evt.newValue.z };
                controller.SyncGravityToScene();
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });

            // Audio section
            var audioSection = CreateSection("Audio");
            scrollView.Add(audioSection);

            soundTrackField = new ObjectField("Soundtrack:")
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            soundTrackField.style.marginBottom = 5;
            soundTrackField.RegisterValueChangedCallback(evt =>
            {
                if (context.currentProject == null) return;

                Undo.RecordObject(context.currentProject, "Change Soundtrack");
                context.currentProject.sceneData.SoundTrack =
                    SoundTrackUtility.GetReference(evt.newValue as AudioClip);
                controller.SyncSoundTrackToScene();
                EditorUtility.SetDirty(context.currentProject);
                context.NotifyProjectChanged();
            });
            audioSection.Add(soundTrackField);

            var customVarSection = CreateSection("Custom Global Variables", insetContent: false);
            customVariables = new CustomPropertyList(
                () => context.currentProject,
                () => context.currentProject?.sceneData?.CustomVariables,
                context.NotifyProjectChanged,
                addButtonText: "+ Add Global Variable");
            customVarSection.Add(customVariables);
            scrollView.Add(customVarSection);
            Add(scrollView);
        }

        #region UI Helpers

        private static Foldout CreateSection(string title, bool insetContent = true)
        {
            var section = new Foldout { text = title, value = true };
            GameRuleTheme.StylePropertySection(section);
            if (insetContent)
            {
                section.contentContainer.style.paddingLeft = 15;
                section.contentContainer.style.marginTop = 5;
                section.contentContainer.style.marginBottom = 8;
            }
            return section;
        }

        private static void AddFieldRow(VisualElement parent, string label, VisualElement field, int labelWidth = 150)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            row.Add(new Label(label) { style = { minWidth = labelWidth } });
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            row.Add(field);
            parent.Add(row);
        }

        private TextField CreateTextField(VisualElement parent, string label, int labelWidth = 150)
        {
            var field = new TextField();
            field.style.backgroundColor = Color.clear;
            field.style.borderTopWidth = 0;
            field.style.borderBottomWidth = 0;
            field.style.borderLeftWidth = 0;
            field.style.borderRightWidth = 0;
            field.style.paddingTop = 0;
            field.style.paddingBottom = 0;
            field.style.paddingLeft = 0;
            field.style.paddingRight = 0;
            AddFieldRow(parent, label, field, labelWidth);
            return field;
        }

        private Vector2Field CreateVector2Field(VisualElement parent, string label)
        {
            var field = new Vector2Field();
            AddFieldRow(parent, label, field);
            return field;
        }

        private Vector3Field CreateVector3Field(VisualElement parent, string label)
        {
            var field = new Vector3Field();
            AddFieldRow(parent, label, field);
            return field;
        }

        private ColorField CreateColorField(VisualElement parent, string label)
        {
            var field = new ColorField();
            AddFieldRow(parent, label, field);
            return field;
        }

        #endregion UI Helpers

        // Helper to convert Unity Color to byte array [0-255]
        private byte[] ColorToBytes(Color c)
        {
            return new byte[] {
                (byte)(c.r * 255),
                (byte)(c.g * 255),
                (byte)(c.b * 255)
            };
        }

        // Helper to convert byte array to Unity Color
        private Color BytesToColor(byte[] b)
        {
            if (b == null || b.Length < 3) return Color.white;
            return new Color(b[0] / 255f, b[1] / 255f, b[2] / 255f);
        }

        private void UpdateUI()
        {
            if (context?.currentProject?.sceneData == null)
            {
                SetEnabled(false);
                gameNameField.Unbind();
                return;
            }
            SetEnabled(true);
            var scene = context.currentProject.sceneData;

            // --- Native Data Binding for direct 1:1 type field ---
            // SerializedProperty binding gives automatic Undo/Redo support for GameName.
            var so = new SerializedObject(context.currentProject);
            gameNameField.BindProperty(so.FindProperty("sceneData.GameName"));

            if (scene.ScreenResolution != null && scene.ScreenResolution.Length >= 2)
            {
                var res = new Vector2(scene.ScreenResolution[0], scene.ScreenResolution[1]);
                if (screenResolutionField.value != res) screenResolutionField.SetValueWithoutNotify(res);
            }

            if (backgroundColorField.value != BytesToColor(scene.BackgroundColor))
                backgroundColorField.SetValueWithoutNotify(BytesToColor(scene.BackgroundColor));

            UpdateVector3Field(cameraPosField, scene.CameraPosition);
            UpdateVector3Field(cameraRotField, scene.CameraRotation);
            UpdateVector3Field(sunPosField, scene.SunPosition);
            UpdateVector3Field(sunRotField, scene.SunRotation);
            UpdateVector3Field(gravityField, scene.Gravity);
            controller.SyncGravityToScene();

            AudioClip soundTrack = SoundTrackUtility.Resolve(scene.SoundTrack);
            if (soundTrackField.value != soundTrack)
                soundTrackField.SetValueWithoutNotify(soundTrack);

            if (sunColorField.value != BytesToColor(scene.SunColor))
                sunColorField.SetValueWithoutNotify(BytesToColor(scene.SunColor));

            if (sunAmbientField.value != BytesToColor(scene.SunAmbientColor))
                sunAmbientField.SetValueWithoutNotify(BytesToColor(scene.SunAmbientColor));

            scene.CustomVariables ??= new List<CustomVariable>();
            customVariables.Refresh();
        }

        private void UpdateVector3Field(Vector3Field field, float[] data)
        {
            if (data != null && data.Length >= 3)
            {
                var vec = new Vector3(data[0], data[1], data[2]);
                if (field.value != vec) field.SetValueWithoutNotify(vec);
            }
        }

        ~SceneSettingsPanel()
        {
            context.OnProjectLoaded -= UpdateUI;
            context.OnProjectChanged -= UpdateUI;
        }
    }
}
