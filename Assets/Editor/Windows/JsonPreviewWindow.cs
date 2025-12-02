using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

public class JsonPreviewWindow : EditorWindow
{
    private static SceneJson sceneToPreview;

    public static void Open(SceneJson scene)
    {
        sceneToPreview = scene;
        var wnd = GetWindow<JsonPreviewWindow>();
        wnd.titleContent = new GUIContent("JSON Preview");
    }

    public void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Editor/UI/JsonPreviewWindow.uxml"
        );

        var root = visualTree.Instantiate();
        rootVisualElement.Add(root);

        var jsonText = root.Q<TextField>("JsonTextField");
        var saveButton = root.Q<Button>("SaveButton");

        jsonText.multiline = true;
        jsonText.value = JsonUtility.ToJson(sceneToPreview, true);

        saveButton.clicked += () =>
        {
            File.WriteAllText("Assets/Resources/Games/exported.json", jsonText.value);
            AssetDatabase.Refresh();
        };
    }
}