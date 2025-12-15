// Assets/Editor/GameRuleEditor/Core/EditorSetup.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace GameRuleEditor.Core
{
    public static class EditorSetup
    {
        private const string CONTEXT_PATH = "Assets/Editor/GameRuleEditor/EditorContext.asset";

        [MenuItem("GameRule/Setup/Create Editor Context")]
        public static void CreateEditorContext()
        {
            string directory = Path.GetDirectoryName(CONTEXT_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            EditorContext context = ScriptableObject.CreateInstance<EditorContext>();
            AssetDatabase.CreateAsset(context, CONTEXT_PATH);
            AssetDatabase.SaveAssets();

            Debug.Log($"EditorContext created at: {CONTEXT_PATH}");
            Selection.activeObject = context;
        }

        [MenuItem("GameRule/Setup/Create New Project")]
        public static void CreateNewProject()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New GameRule Project",
                "NewGameRuleProject",
                "asset",
                "Choose where to save the new project"
            );

            if (string.IsNullOrEmpty(path))
                return;

            GameRuleProject project = ScriptableObject.CreateInstance<GameRuleProject>();
            project.projectName = Path.GetFileNameWithoutExtension(path);
            project.sceneData = new SceneJson
            {
                GameName = project.projectName,
                ScreenResolution = new float[] { 1920, 1080 },
                CameraPosition = new float[] { 0, 1, -10 },
                CameraRotation = new float[] { 0, 0, 0 },
                SunPosition = new float[] { 0, 3, 0 },
                SunRotation = new float[] { 50, -30, 0 },
                SunColor = new byte[] { 255, 255, 255 },
                SunAmbientColor = new byte[] { 128, 128, 128 },
                BackgroundColor = new byte[] { 0, 0, 0 },
                Gravity = new float[] { 0, -9.81f, 0 },
                CustomVariables = new System.Collections.Generic.List<CustomVariable>(),
                Cast = new System.Collections.Generic.List<ActorJson>()
            };

            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"New project created at: {path}");
            Selection.activeObject = project;
        }
    }
}
