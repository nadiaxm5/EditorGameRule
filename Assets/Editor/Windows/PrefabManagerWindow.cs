using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PrefabManagerWindow : EditorWindow
{
    [MenuItem("GameRule/Prefabs")]
    public static void Open()
    {
        var wnd = GetWindow<PrefabManagerWindow>();
        wnd.titleContent = new GUIContent("Prefab Manager");
    }

    public void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Editor/UI/PrefabManagerWindow.uxml"
        );
        rootVisualElement.Add(visualTree.Instantiate());
    }
}