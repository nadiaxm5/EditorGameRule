using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class ActorScriptEditorWindow : EditorWindow
{
    private static ActorJson targetActor;

    public static void Open(ActorJson actor)
    {
        targetActor = actor;

        var wnd = GetWindow<ActorScriptEditorWindow>();
        wnd.titleContent = new GUIContent("Script Editor");
    }

    public void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Editor/UI/ActorScriptEditorWindow.uxml"
        );

        var root = visualTree.Instantiate();
        rootVisualElement.Add(root);

        RegisterCallbacks(root);
    }

    private void RegisterCallbacks(VisualElement root)
    {
        var addSentenceButton = root.Q<Button>("AddSentenceButton");
        var previewLabel = root.Q<Label>("PreviewLabel");

        addSentenceButton.clicked += () =>
        {
            var newSentence = new SentenceJson
            {
                When = new System.Collections.Generic.List<string>(),
                Do = new System.Collections.Generic.List<string>()
            };
            targetActor.Script.Add(newSentence);

            previewLabel.text = "Sentence added. Total: " + targetActor.Script.Count;
        };
    }
}