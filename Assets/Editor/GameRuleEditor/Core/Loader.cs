using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static PlasticPipe.PlasticProtocol.Messages.Serialization.ItemHandlerMessagesSerialization;

public static class Loader
{
    public static void LoadJson(string fileName)
    {
        string jsonPath = Application.dataPath + "/Resources/Games/" + fileName;
        string json = File.ReadAllText(jsonPath);
        SceneJson scene = JsonUtility.FromJson<SceneJson>(json);

        // Sanitize: the JSON export strips empty arrays, so When/Do can be null after re-import
        if (scene.Cast != null)
        {
            foreach (var actor in scene.Cast)
            {
                if (actor.Script == null) actor.Script = new List<SentenceJson>();
                if (actor.Properties == null) actor.Properties = new List<string>();
                foreach (var sentence in actor.Script)
                {
                    if (sentence.When == null) sentence.When = new List<string>();
                    if (sentence.Do == null) sentence.Do = new List<string>();
                }
            }
        }
        else
        {
            scene.Cast = new List<ActorJson>();
        }

        // Preserve the descriptor order as the canonical actor evaluation order.
        // Scene instantiation reverses Cast below, but scheduler order must not be reversed.
        List<string> declarationOrder = scene.Cast.Select(actor => actor.ActorName).ToList();

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
        LoadScripts(scene, declarationOrder);
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

            if (!string.IsNullOrEmpty(actor.Tag)) obj.tag = actor.Tag;
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

            // Do not deactivate actors here. Generated actor scripts apply Active in Start.
            // This lets every actor run Awake and lets GameManager register the complete
            // declaration-ordered actor set before inactive actors disable themselves.
        }
        AssetDatabase.Refresh();
    }

    private static void LoadScripts(SceneJson scene, List<string> declarationOrder)
    {
        if (Directory.Exists("Assets/Resources/Scripts/"))
            Directory.Delete("Assets/Resources/Scripts/", true);
        Directory.CreateDirectory("Assets/Resources/Scripts/");

        Scripts.CreateGameManager(scene, declarationOrder);
        Scripts.Create(scene.Cast);

        // Save a flag indicating we need to attach scripts after compilation finishes
        EditorPrefs.SetBool("GameRule_PendingScriptAttach", true);
        EditorPrefs.SetInt("GameRule_ScriptAttachRetries", 0);

        // Force synchronous import so scripts compile before Play mode domain reload
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    // Automatically called by Unity when C# compilation finishes
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // Check if this script compilation was triggered by the Generate Scene button
        if (!EditorPrefs.GetBool("GameRule_PendingScriptAttach", false)) return;

        int retries = EditorPrefs.GetInt("GameRule_ScriptAttachRetries", 0);

        var scripts = Resources.LoadAll<MonoScript>("Scripts");
        int attachedCount = 0;
        int pendingCount = 0;

        foreach (var script in scripts)
        {
            System.Type scriptType = script.GetClass();
            if (scriptType == null)
            {
                // Script exists but hasn't been compiled yet — need another reload
                pendingCount++;
                continue;
            }

            GameObject obj = FindGameObjectByName(script.name);
            if (obj != null && obj.GetComponent(scriptType) == null)
            {
                obj.AddComponent(scriptType);
                attachedCount++;
            }
        }

        // Only clear the flag once all scripts resolved or we've retried enough
        if (pendingCount == 0 || retries >= 3)
        {
            EditorPrefs.DeleteKey("GameRule_PendingScriptAttach");
            EditorPrefs.DeleteKey("GameRule_ScriptAttachRetries");

            // If there's a pending auto-play request, enter Play mode now
            if (EditorPrefs.GetBool("GameRule_AutoPlayAfterGenerate", false))
            {
                EditorPrefs.DeleteKey("GameRule_AutoPlayAfterGenerate");
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.isPlaying = true;
                };
            }
        }
        else
        {
            // Keep the flag — scripts haven't compiled yet, wait for next domain reload
            EditorPrefs.SetInt("GameRule_ScriptAttachRetries", retries + 1);
        }
    }

    /// <summary>
    /// Finds a root GameObject by name, including inactive objects.
    /// GameObject.Find() only returns active objects, so we search all root objects manually.
    /// </summary>
    private static GameObject FindGameObjectByName(string name)
    {
        // First try the fast path (active objects)
        GameObject obj = GameObject.Find(name);
        if (obj != null) return obj;

        // Search all root objects in loaded scenes (includes inactive)
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var rootObj in scene.GetRootGameObjects())
            {
                if (rootObj.name == name)
                    return rootObj;
            }
        }
        return null;
    }
}
