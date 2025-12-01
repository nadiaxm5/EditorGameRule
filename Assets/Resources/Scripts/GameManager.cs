using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Camera mainCamera;
    private Light sunLight;
    private AudioSource audioSource;

    public string GameName = "SURVIVAL_SHOOTER";
    public Vector2 ScreenResolution = new Vector2(1920f, 1080f);
    public Vector3 CameraPosition = new Vector3(0f, 6f, -7f);
    public Vector3 CameraRotation = new Vector3(30f, 0f, 0f);
    public Vector3 SunPosition = new Vector3(3.3899f, 10.902f, -5.8255f);
    public Vector3 SunRotation = new Vector3(22.704f, 65.875f, -175.012f);
    public Color SunColor = new Color32(195, 184, 255, 255);
    public Color SunAmbientColor = new Color32(170, 180, 200, 255);
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
    public int WaveNumber = 1;
    public int TotalEnemies = 10;
    public string GameDifficulty = "Hard";
    public int PlayerLives = 3;
    public bool BonusActive = true;
    public Vector3 RespawnPosition = new Vector3(5f, 0f, 5f);
    public float GameSpeed = 1.5f;
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
            Mouse = Input.mousePosition;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
                MouseWorld = ray.GetPoint(enter);
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
