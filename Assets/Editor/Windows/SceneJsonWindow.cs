using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class SceneJsonWindow : EditorWindow
{
    private SceneJson currentScene;

    [MenuItem("GameRule/Scene Editor")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<SceneJsonWindow>();
        wnd.titleContent = new GUIContent("Scene JSON Editor");
    }

    public void CreateGUI()
    {
        // Load UI document
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Editor/UI/SceneJsonWindow.uxml"
        );
        var root = visualTree.Instantiate();
        rootVisualElement.Add(root);

        // Load default or new SceneJson
        currentScene = new SceneJson
        {
            Cast = new System.Collections.Generic.List<ActorJson>(),
            CustomVariables = new System.Collections.Generic.List<CustomVariable>()
        };

        // Hook UI elements here
        RegisterCallbacks(root);
    }

    private void RegisterCallbacks(VisualElement root)
    {
        // Example: Read a textfield
        var gameNameField = root.Q<TextField>("GameNameField");
        var addActorButton = root.Q<Button>("AddActorButton");
        var exportJsonButton = root.Q<Button>("ExportJsonButton");

        Debug.Log($"GN:{gameNameField} | AA:{addActorButton} | EX:{exportJsonButton}");

        gameNameField.RegisterValueChangedCallback(ev =>
        {
            currentScene.GameName = ev.newValue;
        });

        addActorButton.clicked += () =>
        {
            ActorEditorWindow.Open(currentScene, null);
        };

        exportJsonButton.clicked += () =>
        {
            JsonPreviewWindow.Open(currentScene);
        };
    }
}