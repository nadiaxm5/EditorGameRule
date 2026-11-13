using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Camera mainCamera;
    private Light sunLight;
    private Vector3 previousCameraPosition;
    private Vector3 currentCameraPosition;
    private Quaternion previousCameraRotation;
    private Quaternion currentCameraRotation;

    private static readonly string[] ActorOrder = new string[] { "LightingReflection", "Environment", "Player", "Hellephant", "ZomBear", "ZomBunny", "HellephantSpawner", "ZomBearSpawner", "ZomBunnySpawner", "HellephantDead", "ZomBearDead", "ZomBunnyDead", "Bullet", "ShotLight", "Laser", "DamageCanvas", "HUDCanvas", "GameOver" };

    public string GameName = "SURVIVAL_SHOOTER";
    public Vector2 ScreenResolution = new Vector2(1920f, 1080f);
    public Vector3 CameraPosition = new Vector3(0f, 5.49f, -7f);
    public Vector3 CameraRotation = new Vector3(30f, 0f, 0f);
    public Vector3 SunPosition = new Vector3(3.3899f, 10.902f, -5.8255f);
    public Vector3 SunRotation = new Vector3(22.704f, 65.875f, -175.012f);
    public Color SunColor = new Color32(195, 184, 255, 255);
    public Color SunAmbientColor = new Color32(170, 180, 200, 255);
    public Color BackgroundColor = new Color32(0, 0, 0, 255);
    public Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
    public string SoundTrack = "Assets/Audio/Music/BackgroundMusic.mp3";
    public float FPS { get; private set; }
    public float Time { get; private set; }
    public float DeltaTime { get; private set; }
    public Vector3 Mouse = Vector3.zero;
    public Vector3 MouseWorld = Vector3.zero;

    // Custom Global Variables
    public float Score = 0f;
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
        
        InitializeCameraState();
        ApplyCameraSettingsImmediate();
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
        GameRuleInput.CaptureFrame();
    }

    void FixedUpdate()
    {
        GameRuleInput.BeginFixedTick();
        try
        {
            UpdateRuntimeVariables();
            UpdateMousePosition();
            BeginCameraFixedTick();
            ActorScheduler.RunFixedUpdate();
            EndCameraFixedTick();
            ApplySunSettings();
        }
        finally
        {
            GameRuleInput.EndFixedTick();
        }
    }

    void LateUpdate()
    {
        ApplyCameraSettingsInterpolated();
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
            Vector2 m = GameRuleInput.PointerPosition();
            Mouse = new Vector3(m.x, m.y, 0);

            Transform cameraTransform = mainCamera.transform;
            Vector3 renderedPosition = cameraTransform.position;
            Quaternion renderedRotation = cameraTransform.rotation;
            Ray ray;
            try
            {
                cameraTransform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraRotation));
                ray = mainCamera.ScreenPointToRay(Mouse);
            }
            finally
            {
                cameraTransform.SetPositionAndRotation(renderedPosition, renderedRotation);
            }
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            if (plane.Raycast(ray, out float enter))
                MouseWorld = ray.GetPoint(enter);
        }
    }

    private void InitializeCameraState()
    {
        previousCameraPosition = CameraPosition;
        currentCameraPosition = CameraPosition;
        previousCameraRotation = Quaternion.Euler(CameraRotation);
        currentCameraRotation = previousCameraRotation;
    }

    private void BeginCameraFixedTick()
    {
        previousCameraPosition = currentCameraPosition;
        previousCameraRotation = currentCameraRotation;
    }

    private void EndCameraFixedTick()
    {
        currentCameraPosition = CameraPosition;
        currentCameraRotation = Quaternion.Euler(CameraRotation);
    }

    private void ApplyCameraSettingsImmediate()
    {
        if (mainCamera != null)
        {
            mainCamera.transform.position = CameraPosition;
            mainCamera.transform.eulerAngles = CameraRotation;
            mainCamera.backgroundColor = BackgroundColor;
        }
    }

    private void ApplyCameraSettingsInterpolated()
    {
        if (mainCamera == null) return;

        float step = UnityEngine.Time.fixedDeltaTime;
        float alpha = step > 0f
            ? Mathf.Clamp01((UnityEngine.Time.time - UnityEngine.Time.fixedTime) / step)
            : 1f;

        mainCamera.transform.position = Vector3.Lerp(previousCameraPosition, currentCameraPosition, alpha);
        mainCamera.transform.rotation = Quaternion.Slerp(previousCameraRotation, currentCameraRotation, alpha);
        mainCamera.backgroundColor = BackgroundColor;
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
