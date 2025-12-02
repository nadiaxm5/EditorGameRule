using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class ActorEditorWindow : EditorWindow
{
    private static SceneJson parentScene;
    private static ActorJson currentActor;

    public static void Open(SceneJson scene, ActorJson actor)
    {
        parentScene = scene;
        currentActor = actor;

        var wnd = GetWindow<ActorEditorWindow>();
        wnd.titleContent = new GUIContent("Actor Editor");
    }

    public void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Editor/UI/ActorEditorWindow.uxml"
        );

        var root = visualTree.Instantiate();
        rootVisualElement.Add(root);

        RegisterCallbacks(root);
    }

    private void RegisterCallbacks(VisualElement root)
    {
        var nameField = root.Q<TextField>("ActorNameField");
        var openScriptButton = root.Q<Button>("OpenScriptButton");

        nameField.value = currentActor.ActorName;
        nameField.RegisterValueChangedCallback(ev =>
        {
            currentActor.ActorName = ev.newValue;
        });

        openScriptButton.clicked += () =>
        {
            ActorScriptEditorWindow.Open(currentActor);
        };
    }
}