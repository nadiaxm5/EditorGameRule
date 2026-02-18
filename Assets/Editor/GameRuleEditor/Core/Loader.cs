using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Loader
{
    public static void LoadJson(string fileName)
    {
        string jsonPath = Application.dataPath + "/Resources/Games/" + fileName;
        string json = File.ReadAllText(jsonPath);
        SceneJson scene = JsonUtility.FromJson<SceneJson>(json);
        scene.Cast.Reverse();
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        newScene.name = scene.GameName;

        // Delete default camera and light
        GameObject defaultCamera = GameObject.Find("Main Camera");
        if (defaultCamera != null) Object.DestroyImmediate(defaultCamera);
        GameObject defaultLight = GameObject.Find("Directional Light");
        if (defaultLight != null) Object.DestroyImmediate(defaultLight);

        // Instanciate GameManager from Prefab
        GameObject gameManagerPrefab = Resources.Load<GameObject>("Prefabs/GameManager");
        if (gameManagerPrefab == null)
        {
            Debug.LogError("Prefab GameManager no encontrado en Resources/Prefabs.");
            return;
        }

        GameObject gmInstance = Object.Instantiate(gameManagerPrefab);
        gmInstance.name = "GameManager";

        // Load
        CreateTags(scene.Cast);
        LoadPrefabs(scene.Cast);
        LoadScripts(scene);
    }

    public static void CreateTags(List<ActorJson> actorList)
    {
        // Load TagManager asset
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProperty = tagManager.FindProperty("tags");

        // Get existing tags to avoid duplicates
        HashSet<string> existingTags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);

        foreach (ActorJson actor in actorList)
        {
            string tagToCheck = actor.Tag;

            // Skip if invalid or already exists
            if (string.IsNullOrEmpty(tagToCheck) || existingTags.Contains(tagToCheck))
            {
                continue;
            }

            // Add new tag
            int index = tagsProperty.arraySize;
            tagsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty newTag = tagsProperty.GetArrayElementAtIndex(index);
            newTag.stringValue = tagToCheck;

            existingTags.Add(tagToCheck);
        }

        // Save changes
        tagManager.ApplyModifiedProperties();
        tagManager.Update();
    }

    private static void LoadPrefabs(List<ActorJson> actorList)
    {
        foreach (ActorJson actor in actorList)
        {
            Object prefab = AssetDatabase.LoadAssetAtPath("Assets/Resources/Prefabs/" + actor.PrefabName + ".prefab", typeof(GameObject));
            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab((GameObject)prefab);
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            Collider col = obj.GetComponent<Collider>();

            obj.name = actor.ActorName;

            if (actor.Tag != null) obj.tag = actor.Tag;
            if (actor.Position != null) obj.transform.position = new Vector3(actor.Position[0], actor.Position[1], actor.Position[2]);
            if (actor.Rotation != null) obj.transform.eulerAngles = new Vector3(actor.Rotation[0], actor.Rotation[1], actor.Rotation[2]);
            if (actor.Scale != null) obj.transform.localScale = new Vector3(actor.Scale[0], actor.Scale[1], actor.Scale[2]);
            if (rb != null)
            {
                if (actor.Velocity != null) rb.linearVelocity = new Vector3(actor.Velocity[0], actor.Velocity[1], actor.Velocity[2]);
                if (actor.AngularVelocity != null) rb.angularVelocity = new Vector3(actor.AngularVelocity[0], actor.AngularVelocity[1], actor.AngularVelocity[2]);
                if (actor.Density != 0) rb.mass = actor.Density;
                if (actor.Drag != 0) rb.linearDamping = actor.Drag;
            }
            if (col != null)
            {
                if (col.material == null) col.material = new PhysicsMaterial();
                if (actor.Friction != 0)
                {
                    col.material.dynamicFriction = actor.Friction;
                    col.material.staticFriction = actor.Friction;
                }
                if (actor.Bounciness != 0) col.material.bounciness = actor.Bounciness;
            }

            if (actor.Size != null)
            {
                Renderer rend = obj.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    Vector3 originalSize = rend.bounds.size;
                    originalSize.x = originalSize.x == 0 ? 1 : originalSize.x;
                    originalSize.y = originalSize.y == 0 ? 1 : originalSize.y;
                    originalSize.z = originalSize.z == 0 ? 1 : originalSize.z;

                    Vector3 desired = new Vector3(actor.Size[0], actor.Size[1], actor.Size[2]);
                    Vector3 newScale = new Vector3(desired.x / originalSize.x, desired.y / originalSize.y, desired.z / originalSize.z);
                    obj.transform.localScale = newScale;
                }
            }
        }
        AssetDatabase.Refresh();
    }

    private static void LoadScripts(SceneJson scene)
    {
        if (Directory.Exists("Assets/Resources/Scripts/"))
            Directory.Delete("Assets/Resources/Scripts/", true);
        Directory.CreateDirectory("Assets/Resources/Scripts/");

        Scripts.CreateGameManager(scene);
        Scripts.Create(scene.Cast);
        AssetDatabase.Refresh();

        // Add scripts to gameObjects
        var scripts = Resources.LoadAll<MonoScript>("Scripts");
        foreach (var script in scripts)
        {
            GameObject obj = GameObject.Find(script.name);
            obj.AddComponent(script.GetClass());
        }
    }
}