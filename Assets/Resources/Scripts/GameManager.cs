using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Camera mainCamera;
    private Light sunLight;
    private AudioSource audioSource;

    public string GameName = "UserStudy";
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

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
        
        mainCamera = GetComponentInChildren<Camera>();
        sunLight = GetComponentInChildren<Light>();
        audioSource = GetComponent<AudioSource>();
        
        ApplyCameraSettings();
        ApplySunSettings();
        ApplyGlobalSettings();
    }

    void Update()
    {
        UpdateRuntimeVariables();
    }

    void FixedUpdate()
    {
        UpdateMousePosition();
        ApplySunSettings();
    }

    void LateUpdate()
    {
        ApplyCameraSettings();
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
