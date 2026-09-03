using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Camera mainCamera;
    private Light sunLight;
    private AudioSource audioSource;

    private static readonly string[] ActorOrder = new string[] { "FirstActor", "SecondActor" };

    public string GameName = "E08_OrderingAndMultipleWrites";
    public Vector2 ScreenResolution = new Vector2(1920f, 1080f);
    public Vector3 CameraPosition = new Vector3(0f, 1f, -10f);
    public Vector3 CameraRotation = new Vector3(0f, 0f, 0f);
    public Vector3 SunPosition = new Vector3(0f, 3f, 0f);
    public Vector3 SunRotation = new Vector3(50f, -30f, 0f);
    public Color SunColor = new Color32(255, 255, 255, 255);
    public Color SunAmbientColor = new Color32(128, 128, 128, 255);
    public Color BackgroundColor = new Color32(0, 0, 0, 255);
    public Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
    public string SoundTrack
    {
        get
        {
            if (audioSource != null && audioSource.clip != null)
                return audioSource.clip.name;
            return "";
        }
    }
    public float FPS { get; private set; }
    public float Time { get; private set; }
    public float DeltaTime { get; private set; }
    public Vector3 Mouse = Vector3.zero;
    public Vector3 MouseWorld = Vector3.zero;

    // Custom Global Variables
    public float SharedValue = 0f;
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        mainCamera = GetComponentInChildren<Camera>();
        sunLight = GetComponentInChildren<Light>();
        audioSource = GetComponent<AudioSource>();

        ApplyCameraSettings();
        ApplySunSettings();
        ApplyGlobalSettings();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ActorScheduler.Build(ActorOrder);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ActorScheduler.Build(ActorOrder);
    }

    void Update()
    {
        UpdateRuntimeVariables();
        ActorScheduler.RunUpdate();
    }

    void FixedUpdate()
    {
        UpdateMousePosition();
        ActorScheduler.RunFixedUpdate();
        ApplyCameraSettings();
        ApplySunSettings();
    }

    private void UpdateRuntimeVariables()
    {
        Time = UnityEngine.Time.time;
        DeltaTime = UnityEngine.Time.deltaTime;
        FPS = 1.0f / UnityEngine.Time.deltaTime;
    }

    private void UpdateMousePosition()
    {
        if (mainCamera != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                Vector2 m = mouse.position.ReadValue();
                Mouse = new Vector3(m.x, m.y, 0);

                Ray ray = mainCamera.ScreenPointToRay(Mouse);
                Plane plane = new Plane(Vector3.up, Vector3.zero);

                if (plane.Raycast(ray, out float enter))
                    MouseWorld = ray.GetPoint(enter);
            }
        }
    }

    private void ApplyCameraSettings()
    {
        if (mainCamera != null)
        {
            mainCamera.transform.position = CameraPosition;
            mainCamera.transform.eulerAngles = CameraRotation;
            mainCamera.backgroundColor = BackgroundColor;
        }
    }

    private void ApplySunSettings()
    {
        if (sunLight != null)
        {
            sunLight.transform.position = SunPosition;
            sunLight.transform.eulerAngles = SunRotation;
            sunLight.color = SunColor;
            RenderSettings.ambientLight = SunAmbientColor;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    private void ApplyGlobalSettings()
    {
        Physics.gravity = Gravity;
        if (ScreenResolution != Vector2.zero)
            Screen.SetResolution((int)ScreenResolution.x, (int)ScreenResolution.y, true);
    }

}