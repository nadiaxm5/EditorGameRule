using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Linq;

public static class Utils
{
    // Runtime bridge: Unity access lives here; Action and Condition keep rule semantics.
    // Calls stay synchronous and keep the scheduler's existing execution order.
    public static float DeltaTime => Time.deltaTime;
    public static float ElapsedTime => Time.time;

    public static void SpawnActor(string prefabName, GameObject spawnerObj, Vector3 offsetPos, Vector3 offsetRot)
    {
        var prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
        if (!prefab)
        {
            Debug.LogWarning($"Prefab '{prefabName}' no encontrado en Resources/Prefabs.");
            return;
        }

        var newObj = UnityEngine.Object.Instantiate(prefab);
        newObj.name = prefab.name;
        newObj.SetActive(true);

        // Copy the tag from the original scene instance (including inactive objects set up by Loader)
        string actorTag = FindTagForActor(prefabName);
        if (!string.IsNullOrEmpty(actorTag))
        {
            try { newObj.tag = actorTag; }
            catch (UnityException) { /* Tag not registered */ }
        }

        newObj.transform.position = spawnerObj.transform.TransformPoint(offsetPos);
        newObj.transform.rotation = spawnerObj.transform.rotation * Quaternion.Euler(offsetRot);
        newObj.transform.localScale = prefab.transform.localScale;

        var scriptType = System.Type.GetType(prefabName);
        if (scriptType?.IsSubclassOf(typeof(MonoBehaviour)) == true)
        {
            var script = newObj.AddComponent(scriptType);
            scriptType.GetField("Active")?.SetValue(script, true);

            if (scriptType.GetField("propertyList")?.GetValue(script) is Dictionary<string, float> props)
                foreach (var (prop, val) in props)
                    scriptType.GetField(prop)?.SetValue(script, val);

            // Runtime-spawned actors are appended to the deterministic order after
            // the current scheduler pass, preserving spawn order.
            if (script is IGameRuleActor spawnedActor)
                ActorScheduler.RegisterSpawned(spawnedActor);
        }

        if (newObj.TryGetComponent(out LineRenderer lr))
        {
            Vector3 start = newObj.transform.position;
            Vector3 dir = newObj.transform.forward;
            Vector3 end = Physics.Raycast(start, dir, out RaycastHit hit, 100f)
                ? hit.point
                : start + dir * 100f;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
    }

    public static void PlayAnimation(string animationName, GameObject obj)
    {
        Animator animator = obj.GetComponent<Animator>();
        if (animator != null)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (!currentState.IsName(animationName)) animator.Play(animationName);
        }
    }

    public static void PlaySound(string audioClip, GameObject obj)
    {
        AudioSource[] audios = obj.GetComponents<AudioSource>();
        foreach (AudioSource audio in audios)
        {
            if (audio.clip != null && audio.clip.name == audioClip && !audio.isPlaying)
            {
                audio.Play();
                return;
            }
        }

        AudioSource[] childAudios = obj.GetComponentsInChildren<AudioSource>();
        foreach (AudioSource audio in childAudios)
        {
            if (audio.gameObject == obj) continue;
            if (audio.clip != null && audio.clip.name == audioClip && !audio.isPlaying)
            {
                audio.Play();
                return;
            }
        }
    }

    public static void PlayParticles(string particleSystemName, GameObject obj)
    {
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null && obj.name == particleSystemName && !ps.isPlaying)
        {
            ps.Play();
            return;
        }

        ParticleSystem[] systems = obj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem childPs in systems)
        {
            if (childPs.gameObject == obj) continue;
            if (childPs.gameObject.name == particleSystemName && !childPs.isPlaying)
            {
                childPs.Play();
            }
        }
    }

    public static void DeleteActor(GameObject me)
    {
        RemoveFromCollisions(me);

        // Keep the actor set fixed during the current scheduler pass. The entry
        // is removed when the pass finishes; Unity destroys the object afterwards.
        if (me.TryGetComponent(out IGameRuleActor actor))
            ActorScheduler.Unregister(actor);

        UnityEngine.Object.Destroy(me);
    }

    public static void LoadFirstScene()
    {
        SceneManager.LoadScene(0);
    }

    public static void QuitApplication()
    {
        Application.Quit();
    }

    public static void SetNavigationTarget(GameObject obj, float speed, Vector3 target)
    {
        NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
        if (agent == null) return;

        agent.speed = speed;

        if (agent.isOnNavMesh) agent.SetDestination(target);
        else Debug.LogWarning($"'{obj.name}' is not on the NavMesh, it cannot be navigated");
    }

    public static void ApplyForce(GameObject obj, Vector3 forceVector)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb != null) rb.AddForce(forceVector, ForceMode.Force);
        else obj.transform.position += forceVector * 0.02f;
    }

    public static void ApplyTorque(GameObject obj, Vector3 torqueVector)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.AddTorque(torqueVector, ForceMode.Force);
        else obj.transform.Rotate(torqueVector * Time.deltaTime, Space.Self);

        SetProperty(obj.name + ".rx", obj.transform.eulerAngles.x, obj);
        SetProperty(obj.name + ".ry", obj.transform.eulerAngles.y, obj);
        SetProperty(obj.name + ".rz", obj.transform.eulerAngles.z, obj);
    }

    // Tags are inherited from scene actors, including inactive root objects.
    private static string FindTagForActor(string actorName)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var rootObj in scene.GetRootGameObjects())
            {
                if (rootObj.name == actorName && !rootObj.CompareTag("Untagged"))
                    return rootObj.tag;
            }
        }
        return null;
    }

    public static bool HasCollision(string tag, GameObject obj)
    {
        var script = obj.GetComponent(obj.name);
        var tagCollisionsField = script.GetType().GetField("TagCollisions");
        if (tagCollisionsField == null) return false;

        var tagCollisions = tagCollisionsField.GetValue(script) as Dictionary<string, HashSet<GameObject>>;
        if (tagCollisions == null || !tagCollisions.ContainsKey(tag)) return false;

        tagCollisions[tag].RemoveWhere(o => o == null);

        return tagCollisions[tag].Count > 0;
    }

    public static bool IsPointerOverActor(GameObject obj, Vector2 pointerPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ScreenPointToRay(pointerPosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == obj;
    }

    public static Dictionary<string, float> GetActorTimers(GameObject obj)
    {
        var script = obj.GetComponent(obj.name);
        var field = script.GetType().GetField("timers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(script) as Dictionary<string, float>;
    }

    public static Vector3 GetPosition(GameObject obj)
    {
        if (obj == null) return Vector3.zero;
        Rigidbody body = obj.GetComponent<Rigidbody>();
        return body != null && !body.isKinematic ? body.position : obj.transform.position;
    }

    public static Quaternion GetRotation(GameObject obj)
    {
        if (obj == null) return Quaternion.identity;
        Rigidbody body = obj.GetComponent<Rigidbody>();
        return body != null && !body.isKinematic ? body.rotation : obj.transform.rotation;
    }

    public static void SetPosition(GameObject obj, Vector3 position)
    {
        if (obj == null) return;
        Rigidbody body = obj.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            if (body.interpolation == RigidbodyInterpolation.None)
                body.interpolation = RigidbodyInterpolation.Interpolate;
            body.MovePosition(position);
            return;
        }

        obj.transform.position = position;
    }

    public static void SetRotation(GameObject obj, Quaternion rotation)
    {
        if (obj == null) return;
        Rigidbody body = obj.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            if (body.interpolation == RigidbodyInterpolation.None)
                body.interpolation = RigidbodyInterpolation.Interpolate;
            body.MoveRotation(rotation);
            return;
        }

        obj.transform.rotation = rotation;
    }

    public static float GetProperty(KeyValuePair<string, GameObject> s)
    {
        GameObject obj = s.Value;
        float value = float.NaN;
        if (s.Key.StartsWith("#"))
        {
            return GetGameManagerProperty(s.Key.Substring(1), obj);
        }
        string[] elements = s.Key.Split(new string[] { "." }, StringSplitOptions.None);
        if (elements.Length < 2) return float.NaN;

        if (obj != null)
        {
            switch (elements[1])
            {
                case "x": value = GetPosition(obj).x; break;
                case "y": value = GetPosition(obj).y; break;
                case "z": value = GetPosition(obj).z; break;
                case "rx": value = GetRotation(obj).eulerAngles.x; break;
                case "ry": value = GetRotation(obj).eulerAngles.y; break;
                case "rz": value = GetRotation(obj).eulerAngles.z; break;
                case "sx": value = obj.transform.localScale.x; break;
                case "sy": value = obj.transform.localScale.y; break;
                case "sz": value = obj.transform.localScale.z; break;
                case "Active": value = obj.activeSelf ? 1f : 0f; break;
                case "sliderValue":
                    var slider = obj.GetComponent<UnityEngine.UI.Slider>();
                    if (slider != null) value = slider.value;
                    break;

                case "text":
                    var text = obj.GetComponent<UnityEngine.UI.Text>();
                    if (text != null && float.TryParse(text.text, out float parsed)) value = parsed;
                    break;

                default:
                    {
                        var script = obj.GetComponent(obj.name);
                        value = (float)script.GetType().GetField(elements[1]).GetValue(script);
                        break;
                    }
            }
        }
        return (value);
    }

    private static float GetGameManagerProperty(string propertyPath, GameObject gameManagerObj)
    {
        GameManager gameManager = gameManagerObj.GetComponent<GameManager>();
        if (gameManager == null)
        {
            Debug.Log("GameManager component not found");
            return float.NaN;
        }

        string[] parts = propertyPath.Split('.');

        // Simple property
        if (parts.Length == 1)
        {
            string mainProperty = parts[0];

            var field = typeof(GameManager).GetField(mainProperty);
            var property = typeof(GameManager).GetProperty(mainProperty);

            object value = null;

            if (field != null)
                value = field.GetValue(gameManager);
            else if (property != null)
                value = property.GetValue(gameManager);
            else
            {
                Debug.LogError($"Property '{mainProperty}' not found in GameManager");
                return float.NaN;
            }

            // Float
            if (value is float f) return f;

            // Int
            if (value is int i) return i;

            // Bool
            if (value is bool b) return b ? 1f : 0f;

            Debug.LogError($"Unsupported GameManager property type for '{mainProperty}': {value.GetType()}");
            return float.NaN;
        }

        // Compound property
        if (parts.Length == 2)
        {
            string mainProperty = parts[0];
            string subProperty = parts[1];

            var field = typeof(GameManager).GetField(mainProperty);
            var property = typeof(GameManager).GetProperty(mainProperty);

            object value = null;
            if (field != null)
                value = field.GetValue(gameManager);
            else if (property != null)
                value = property.GetValue(gameManager);
            else
            {
                Debug.LogError($"Property '{mainProperty}' not found in GameManager");
                return float.NaN;
            }

            // Vector3
            if (value is Vector3 vector3)
            {
                if (subProperty == "x") return vector3.x;
                if (subProperty == "y") return vector3.y;
                if (subProperty == "z") return vector3.z;
            }

            // Vector2
            if (value is Vector2 vector2)
            {
                if (subProperty == "x") return vector2.x;
                if (subProperty == "y") return vector2.y;
            }

            // Float
            if (value is float f) return f;

            // Int
            if (value is int i) return i;

            // Bool
            if (value is bool b) return b ? 1f : 0f;

            Debug.LogError($"Unsupported GameManager property type: {value.GetType()}");
            return float.NaN;
        }

        return float.NaN;
    }

    public static void SetProperty(string property, float value, GameObject obj)
    {
        if (property.StartsWith("#"))
        {
            SetGameManagerProperty(property.Substring(1), value, obj);
            return;
        }
        string[] elements = property.Split(new string[] { "." }, StringSplitOptions.None);
        if (obj != null)
        {
            var script = obj.GetComponent(obj.name);
            switch (elements[1])
            {
                case "x": { Vector3 p = GetPosition(obj); p.x = value; SetPosition(obj, p); break; }
                case "y": { Vector3 p = GetPosition(obj); p.y = value; SetPosition(obj, p); break; }
                case "z": { Vector3 p = GetPosition(obj); p.z = value; SetPosition(obj, p); break; }
                case "rx": { Vector3 r = GetRotation(obj).eulerAngles; r.x = value; SetRotation(obj, Quaternion.Euler(r)); break; }
                case "ry": { Vector3 r = GetRotation(obj).eulerAngles; r.y = value; SetRotation(obj, Quaternion.Euler(r)); break; }
                case "rz": { Vector3 r = GetRotation(obj).eulerAngles; r.z = value; SetRotation(obj, Quaternion.Euler(r)); break; }
                case "sx": obj.transform.localScale = new Vector3(value, obj.transform.localScale.y, obj.transform.localScale.z); break;
                case "sy": obj.transform.localScale = new Vector3(obj.transform.localScale.x, value, obj.transform.localScale.z); break;
                case "sz": obj.transform.localScale = new Vector3(obj.transform.localScale.x, obj.transform.localScale.y, value); break;
                case "Active":
                    obj.SetActive(value != 0);
                    if (script != null && script.GetType().GetField("Active") != null)
                        script.GetType().GetField("Active").SetValue(script, value != 0);
                    break;

                case "sliderValue":
                    var slider = obj.GetComponent<UnityEngine.UI.Slider>();
                    if (slider != null) slider.value = value;
                    break;

                case "text":
                    var text = obj.GetComponent<UnityEngine.UI.Text>();
                    if (text != null) text.text = value.ToString();
                    break;

                default:
                    {
                        script.GetType().GetField(elements[1]).SetValue(script, value); break;
                    }
            }
        }
    }

    private static void SetGameManagerProperty(string propertyPath, float value, GameObject gameManagerObj)
    {
        GameManager gameManager = gameManagerObj.GetComponent<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("GameManager component not found");
            return;
        }

        string[] parts = propertyPath.Split('.');

        // Simple property
        if (parts.Length == 1)
        {
            string mainProperty = parts[0];

            var field = typeof(GameManager).GetField(mainProperty);
            var propertyInfo = typeof(GameManager).GetProperty(mainProperty);

            // Float
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(gameManager, value);
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(float))
            {
                propertyInfo.SetValue(gameManager, value);
                return;
            }

            // Int
            if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(gameManager, Mathf.RoundToInt(value));
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(int))
            {
                propertyInfo.SetValue(gameManager, Mathf.RoundToInt(value));
                return;
            }

            // Bool
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(gameManager, value != 0f);
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(bool))
            {
                propertyInfo.SetValue(gameManager, value != 0f);
                return;
            }

            Debug.LogError($"Property '{mainProperty}' not found or unsupported type in GameManager.");
            return;
        }

        // Compound property
        if (parts.Length == 2)
        {
            string mainProperty = parts[0];
            string subProperty = parts[1];

            var field = typeof(GameManager).GetField(mainProperty);
            var propertyInfo = typeof(GameManager).GetProperty(mainProperty);

            object mainValue = null;

            if (field != null)
                mainValue = field.GetValue(gameManager);
            else if (propertyInfo != null)
                mainValue = propertyInfo.GetValue(gameManager);
            else
            {
                Debug.LogError($"Property '{mainProperty}' not found in GameManager");
                return;
            }

            // Vector3
            if (mainValue is Vector3 v3)
            {
                if (subProperty == "x") v3.x = value;
                else if (subProperty == "y") v3.y = value;
                else if (subProperty == "z") v3.z = value;

                if (field != null) field.SetValue(gameManager, v3);
                else propertyInfo.SetValue(gameManager, v3);

                return;
            }

            // Vector2
            if (mainValue is Vector2 v2)
            {
                if (subProperty == "x") v2.x = value;
                else if (subProperty == "y") v2.y = value;

                if (field != null) field.SetValue(gameManager, v2);
                else propertyInfo.SetValue(gameManager, v2);

                return;
            }

            // Float
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(gameManager, value);
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(float))
            {
                propertyInfo.SetValue(gameManager, value);
                return;
            }

            // Int
            if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(gameManager, Mathf.RoundToInt(value));
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(int))
            {
                propertyInfo.SetValue(gameManager, Mathf.RoundToInt(value));
                return;
            }

            // Bool
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(gameManager, value != 0f);
                return;
            }
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(bool))
            {
                propertyInfo.SetValue(gameManager, value != 0f);
                return;
            }

            Debug.LogError($"Unsupported property type for '{mainProperty}'");
        }
    }

    public static Dictionary<string, GameObject> CreateScope(int objID, string scope)
    {
        Dictionary<string, GameObject> scopeList = new Dictionary<string, GameObject>();
        List<string> baseProperties = new List<string> { "x", "y", "z", "rx", "ry", "rz", "sx", "sy", "sz", "Active" };

        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject obj = t.gameObject;
                var script = obj.GetComponent(obj.name);
                List<string> properties = new List<string>(baseProperties);

                if (script != null && script.GetType().GetField("propertyList") != null)
                {
                    var propertyList = (Dictionary<string, float>)script.GetType().GetField("propertyList").GetValue(script);
                    properties.AddRange(propertyList.Keys);
                }

                // Also discover public float fields via reflection (handles inactive objects
                // where Awake never ran and propertyList is still empty)
                if (script != null)
                {
                    foreach (var field in script.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (field.FieldType == typeof(float) && !properties.Contains(field.Name))
                            properties.Add(field.Name);
                    }
                }

                if (obj.GetComponent<UnityEngine.UI.Slider>() != null)
                    properties.Add("sliderValue");

                if (obj.GetComponent<UnityEngine.UI.Text>() != null)
                    properties.Add("text");

                properties = properties.Distinct().ToList();

                foreach (string p in properties)
                {
                    string fullName = obj.name + "." + p;
                    if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                    {
                        scopeList.Add(fullName, obj);
                    }
                }

                if (obj.GetInstanceID() == objID)
                {
                    foreach (string p in properties)
                    {
                        string thisProp = "this." + p;
                        if (!scopeList.ContainsKey(thisProp))
                        {
                            scopeList.Add(thisProp, obj);
                        }
                    }
                }
            }
        }

        if (scope.Contains("#"))
        {
            GameManager gameManager = GameObject.FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                var gameManagerType = typeof(GameManager);
                var fields = gameManagerType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Vector2))
                    {
                        string[] components = { "x", "y" };
                        foreach (string comp in components)
                        {
                            string fullName = "#" + field.Name + "." + comp;
                            if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                            {
                                scopeList.Add(fullName, gameManager.gameObject);
                            }
                        }
                    }
                    else if (field.FieldType == typeof(Vector3))
                    {
                        string[] components = { "x", "y", "z" };
                        foreach (string comp in components)
                        {
                            string fullName = "#" + field.Name + "." + comp;
                            if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                            {
                                scopeList.Add(fullName, gameManager.gameObject);
                            }
                        }
                    }
                    else
                    {
                        string fullName = "#" + field.Name;
                        if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                        {
                            scopeList.Add(fullName, gameManager.gameObject);
                        }
                    }
                }

                var properties = gameManagerType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    if (prop.PropertyType == typeof(Vector2))
                    {
                        string[] components = { "x", "y" };
                        foreach (string comp in components)
                        {
                            string fullName = "#" + prop.Name + "." + comp;
                            if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                            {
                                scopeList.Add(fullName, gameManager.gameObject);
                            }
                        }
                    }
                    else if (prop.PropertyType == typeof(Vector3))
                    {
                        string[] components = { "x", "y", "z" };
                        foreach (string comp in components)
                        {
                            string fullName = "#" + prop.Name + "." + comp;
                            if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                            {
                                scopeList.Add(fullName, gameManager.gameObject);
                            }
                        }
                    }
                    else
                    {
                        string fullName = "#" + prop.Name;
                        if (scope.Contains(fullName) && !scopeList.ContainsKey(fullName))
                        {
                            scopeList.Add(fullName, gameManager.gameObject);
                        }
                    }
                }
            }
        }

        return scopeList;
    }

    public static Dictionary<string, float> CreateProperties(string s)
    {
        Dictionary<string, float> properties = new Dictionary<string, float>();
        if (s != "")
        {
            var asign = s.Split(new string[] { ";" }, StringSplitOptions.None);
            foreach (string a in asign)
            {
                var elements = a.Split(new string[] { "=" }, StringSplitOptions.None);
                if (!properties.ContainsKey(elements[0]))
                {
                    float value = float.Parse(elements[1], System.Globalization.CultureInfo.InvariantCulture);
                    properties.Add(elements[0], value);
                }
            }
        }
        return (properties);
    }

    public static void RemoveFromCollisions(GameObject me)
    {
        var script = me.GetComponent(me.name);
        if (script == null) return;

        var tagDict = script.GetType().GetField("TagCollisions")?.GetValue(script) as Dictionary<string, HashSet<GameObject>>;
        if (tagDict == null) return;

        foreach (var others in tagDict.Values.ToList())
        {
            foreach (var other in others)
            {
                if (other == null) continue;

                var otherScript = other.GetComponent(other.name);
                var otherTagDict = otherScript?.GetType().GetField("TagCollisions")?.GetValue(otherScript) as Dictionary<string, HashSet<GameObject>>;
                if (otherTagDict == null) continue;

                foreach (var set in otherTagDict.Values)
                {
                    set.Remove(me);
                }
            }
        }
    }
}
