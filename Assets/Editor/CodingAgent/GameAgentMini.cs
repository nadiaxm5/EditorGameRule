using UnityEngine;
using UnityEditor;
using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Unity Editor window — Game Coding Agent.
/// Uses a local LLaMA model to create and modify actors in game.json.
/// Includes an integrated auditor layer that validates and fixes output
/// before applying changes.
/// </summary>
public class GameAgentMini : EditorWindow
{
    // ══════════════════════════════════════════════════════════
    #region  MODEL STATE (persistent across domain reloads)
    // ══════════════════════════════════════════════════════════

    private static LLamaWeights       _model;
    private static LLamaContext       _context;
    private static InteractiveExecutor _executor;
    private static ModelParams        _parameters;
    private static bool               _isModelLoaded;

    private static bool               _isLoadingModels;

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  GAME JSON STATE
    // ══════════════════════════════════════════════════════════

    private SceneJson _gameData;
    private string   _gameJsonPath;
    private string   _currentJsonText  = "";
    private string   _previousJsonText = "";
    private string[] _currentJsonLines = Array.Empty<string>();
    private HashSet<int> _changedLines = new HashSet<int>();

    // ── Undo / Redo history ──
    private List<string> _undoStack = new List<string>();
    private List<string> _redoStack = new List<string>();
    private const int MaxUndoHistory = 30;

    // ── Editor Context integration (shared with GameRuleEditorWindow) ──
    private GameRuleEditor.Core.EditorContext _editorContext;
    private const string EDITOR_CONTEXT_PATH = "Assets/Editor/GameRuleEditor/Projects/EditorContext.asset";
    private bool _syncingFromEditor = false; // guard to prevent recursive sync

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  UI STATE
    // ══════════════════════════════════════════════════════════

    private string       _userInput     = "";
    private string       _statusMessage = "";
    private string       _gameName      = "NEW_GAME";
    private Vector2      _scrollChat;
    private Vector2      _scrollJson;
    private Vector2      _scrollAudit;
    private List<string> _chatHistory   = new List<string>();
    private List<string> _auditLog      = new List<string>();
    private bool         _isGenerating;

    // ── Operation mode ──
    private enum AgentMode { Create, Modify, Delete }
    private AgentMode _mode = AgentMode.Create;
    private int       _selectedActorIndex = 0;

    // ── Settings ──
    private float  _temperature    = 0.1f;
    private int    _maxTokens      = 8192;
    private float  _repeatPenalty  = 1.15f;
    private float  _frequencyPenalty = 0.1f;
    private float  _presencePenalty  = 0.1f;
    private int    _penaltyCount   = 128;

    // ── Loop Detection ──
    private int _loopCheckWindowSize = 12;
    private int _loopMaxRepeats      = 3;

    // ── Model Selection ──
    private string[] _availableModels     = Array.Empty<string>();
    private string[] _availableModelNames = Array.Empty<string>();
    private int      _selectedModelIndex  = 0;

    // ── Grammar ──
    private bool   _useGrammar;
    private string _grammarPath     = "";
    private string _grammarText     = "";
    private string _grammarFileName = "(ninguno)";

    // ── Chain / Context ──
    private string _lastAIResponse = "";
    private string _skeletonText   = "";

    // ── UI Layout ──
    private bool    _showSettings  = false;
    private bool    _showJsonView  = true;
    private bool    _showAuditLog  = true;
    private Vector2 _scrollMain;

    // ── Think / Reasoning Display ──
    private bool   _showThinkReasoning = false;
    private string _lastThinkContent   = "";

    // ── Pending Global Variables (awaiting user type selection) ──
    private static readonly string[] GlobalVarTypes = { "float", "bool", "int" };
    private List<string> _pendingGlobalNames = new List<string>();
    private List<int>    _pendingGlobalTypes = new List<int>(); // index into GlobalVarTypes

    // ── Anti-prompts ──
    private static readonly string[] AntiPrompts = { "### Instruction:", "### Input:", "User:" };

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  SYSTEM PROMPT — teaches the agent the game engine format
    // ══════════════════════════════════════════════════════════

    private const string AGENT_SYSTEM_PROMPT =
@"You are a game coding agent. You create and modify actors for a JSON-based game engine.
OUTPUT RULES:
1. Output ONLY a valid JSON object for a single actor. No extra text, no explanations, no markdown.
2. Follow this actor format exactly:
{
    ""ActorName"": ""string"",
    ""PrefabName"": ""string"",
    ""Tag"": ""string"",
    ""Active"": true/false,
    ""Position"": [x, y, z],
    ""Rotation"": [rx, ry, rz],
    ""Scale"": [sx, sy, sz],
    ""Properties"": [""name=value"", ""name2=value2""],
    ""Script"": [
        { ""Do"": [""action1"", ""action2""] },
        { ""When"": [""condition""], ""Do"": [""action""] }
    ]
}
3. Only include fields relevant to the actor. Omit unused optional fields.
4. Properties are strings in format ""name=value"". DECLARE ALL custom properties used in Script.
5. Built-in properties (no declaration needed): x, y, z, rx, ry, rz, Active, value, text.
6. If unsure about a value, use reasonable defaults (e.g. Position [0,0,0], Active true, empty Properties).

AVAILABLE CONDITIONS (When):
- Keyboard(key,mode) — key: W,A,S,D,Space,Enter,UpArrow,DownArrow,LeftArrow,RightArrow. mode: press,down,up.
    Example: Keyboard(Space,press)  Keyboard(W,down)  Keyboard(A,up)
- Collision(tag) — Detected collision with actor/tag.  Example: Collision(Enemy)  Collision(Player)
- Timer(seconds) — Repeating interval.  Example: Timer(5)  Timer(0.5)
- Compare(boolExpr) — Relational check.  Example: Compare(this.health>0)  Compare(#Time<60)
- Check(var) — Boolean/truthy check.  Example: Check(this.moving)  Check(Player.Active)
- Touch(mode,onActor) — Touch/mouse. mode: press,down,up,isOver,tap. onActor: true/false.
    Example: Touch(press,true)  Touch(tap,false)
- Combine with: NOT cond, cond1 AND cond2, cond1 OR cond2.
    Example: Compare(this.health<=0) OR Collision(Enemy)

AVAILABLE ACTIONS (Do) — STRICT SIGNATURES, follow parameter count and order exactly:

  Move(speed, rx, ry, rz)  [4 params]
    Continuous displacement at velocity 'speed' in direction angles (rx,ry,rz).
    rx,ry,rz = euler angles defining movement direction. Use this.ry for forward movement.
    Example: Move(this.speed, 0, this.ry, 0)
    Example: Move(this.speed, 0, 90, 0)

  MoveTo(speed, x, y, z)  [4 params]
    Move toward absolute world position (x,y,z) at velocity 'speed'. Stops on arrival.
    Example: MoveTo(this.speed, 5, 0, 10)
    Example: MoveTo(2, Player.x, Player.y, Player.z)

  NavigateTo(speed, x, y, z)  [4 params]
    Pathfinding navigation toward world position (x,y,z) at velocity 'speed'. Uses navmesh.
    Example: NavigateTo(this.speed, Player.x, Player.y, Player.z)
    Example: NavigateTo(this.speed, this.xTarget, this.y, this.zTarget)
    ⚠ NEVER use this.x,this.y,this.z as target — that navigates to self (no-op).

  Rotate(angSpeed, rx, ry, rz)  [4 params]
    Continuous rotation at angular velocity 'angSpeed' around current axes (rx,ry,rz).
    Example: Rotate(this.angularSpeed, this.rx, this.ry, this.rz)
    Example: Rotate(90, 0, 1, 0)

  RotateTo(speed, targetX, targetY, targetZ, pivotX, pivotY, pivotZ)  [ALWAYS 7 params]
    Interpolate orientation to face toward point (targetX,targetY,targetZ),
    using (pivotX,pivotY,pivotZ) as the pivot/origin of the rotation (usually the actor itself).
    'speed' = angular interpolation speed.
    Example: RotateTo(this.rotSpeed, #MouseWorld.x, #MouseWorld.y, #MouseWorld.z, this.x, this.y, this.z)
    Example: RotateTo(5, Player.x, Player.y, Player.z, this.x, this.y, this.z)
    ⚠ NEVER use only 4 params — RotateTo ALWAYS requires 7.
    ⚠ NEVER put this.rx,this.ry,this.rz as target — that rotates toward self (no-op).
    ⚠ The last 3 params (pivot) are usually this.x, this.y, this.z (the actor's own position).

  Push(force, rx, ry, rz)  [4 params]
    Apply linear force of magnitude 'force' in direction (rx,ry,rz).
    Example: Push(10, 0, 0, 1)
    Example: Push(this.pushForce, 0, this.ry, 0)

  PushTo(force, x, y, z)  [4 params]
    Apply linear force of magnitude 'force' toward/away from point (x,y,z).
    Negative force = push away from point. Positive = pull toward point.
    Example: PushTo(-300, RedTank.x, RedTank.y, RedTank.z)
    Example: PushTo(50, Player.x, Player.y, Player.z)
    ⚠ NEVER use this.x,this.y,this.z as target — that pushes toward self (no-op).

  Torque(rx, ry, rz)  [3 params]
    Apply rotational force (torque) around axes (rx,ry,rz).
    Example: Torque(0, 10, 0)

  Edit(property, value)  [2 params]
    Assign value to a property of this actor, another actor, or a global variable.
    Example: Edit(this.speed, 5)  Edit(this.moving, 1)
    Example: Edit(OtherActor.Active, 0)  Edit(#Score, #Score+10)
    Example: Edit(#CameraPosition.x, this.x)

  Spawn(prefab, source)  [2 params]  OR  Spawn(prefab, source, offX, offY, offZ)  [5 params]
    Instantiate prefab at source actor's position, optionally with offset.
    Example: Spawn(Bullet, this, this.offsetX, this.offsetY, this.offsetZ)
    Example: Spawn(Explosion, this)

  Delete(this)  [1 param]
    Remove this actor from the scene.
    Example: Delete(this)

  Animate(name)  [1 param]
    Play animation clip.  Example: Animate(Walk)  Animate(Death)

  PlaySound(name)  [1 param]
    Play sound.  Example: PlaySound(Explosion)  PlaySound(Footsteps)

  PlayParticles(name)  [1 param]
    Activate particle system.  Example: PlayParticles(DustTrail)

  LoadScene()  [0 params]
    Reload/restart the current scene.

  QuitGame()  [0 params]
    Quit the game.
    
DO NOT USE COMMENTS OR EXPLANATIONS IN THE ACTOR JSON — ONLY THE EXACT JSON OBJECT FOR THE ACTOR. NO MARKDOWN, NO TEXT, NO EXTRA FIELDS.

REFERENCES IN EXPRESSIONS:
- this.property — Current actor's property (this.x, this.y, this.z, this.rx, this.ry, this.rz, this.speed, etc.)
- ActorName.property — Another actor's property. USE THIS for chase/follow/interact!
- #GlobalVar — Global variable (#CameraPosition.x, #MouseWorld.x, #Score, #Time, etc.)

CROSS-ACTOR REFERENCES — CRITICAL:
To make an actor chase/follow/face another actor, use THE OTHER ACTOR'S NAME, not 'this':
  CORRECT:   NavigateTo(this.speed, Player.x, Player.y, Player.z)
  WRONG:     NavigateTo(this.speed, this.x, this.y, this.z)  ← navigates to SELF = no-op!
  CORRECT:   RotateTo(5, Player.x, Player.y, Player.z, this.x, this.y, this.z)
  WRONG:     RotateTo(5, this.rx, this.ry, this.rz, ...)  ← faces own rotation = no-op!

EXAMPLE ACTORS (follow these patterns):

Example 1 — Player with WASD movement:
{
  ""ActorName"": ""player"", ""PrefabName"": ""Player"",
  ""Properties"": [""speed=5"", ""moving=0""],
  ""Script"": [
    { ""When"": [""Keyboard(W,down)""], ""Do"": [""Move(this.speed,0,0,0)"", ""Animate(Walk)""] },
    { ""When"": [""Keyboard(S,down)""], ""Do"": [""Move(this.speed,0,180,0)""] },
    { ""When"": [""NOT Keyboard(W,down) AND NOT Keyboard(S,down)""], ""Do"": [""Animate(Idle)""] }
  ]
}

Example 2 — Enemy that chases Player and faces them:
{
  ""ActorName"": ""enemy"", ""PrefabName"": ""ZomBear"", ""Tag"": ""Enemy"",
  ""Properties"": [""speed=3"", ""rotSpeed=5"", ""health=100"", ""damage=10""],
  ""Script"": [
    { ""Do"": [""NavigateTo(this.speed,Player.x,Player.y,Player.z)"",""RotateTo(this.rotSpeed,Player.x,Player.y,Player.z,this.x,this.y,this.z)""] },
    { ""When"": [""Collision(Player)""], ""Do"": [""PlaySound(EnemyBite)""] },
    { ""When"": [""Compare(this.health<=0)""], ""Do"": [""Animate(Death)"",""PlaySound(EnemyDeath)"",""Delete(this)""] }
  ]
}

Example 3 — Projectile spawned and auto-deleted:
{
  ""ActorName"": ""bullet"", ""PrefabName"": ""Bullet"",
  ""Properties"": [""speed=20"", ""damage=25""],
  ""Script"": [
    { ""Do"": [""Move(this.speed,0,this.ry,0)""] },
    { ""When"": [""Timer(3)""], ""Do"": [""Delete(this)""] },
    { ""When"": [""Collision(Enemy)""], ""Do"": [""PlayParticles(HitParticles)"",""Delete(this)""] }
  ]
}";

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  MENU ENTRY
    // ══════════════════════════════════════════════════════════

    // ── Singleton-like access for embedding in other windows ──
    private static GameAgentMini _instance;

    /// <summary>
    /// Returns the current GameAgentMini instance, creating one if needed.
    /// Does NOT open a standalone window — use ShowWindow() for that.
    /// </summary>
    public static GameAgentMini GetOrCreateInstance()
    {
        if (_instance == null)
        {
            // Try to find an existing instance (hidden or docked)
            var existing = Resources.FindObjectsOfTypeAll<GameAgentMini>();
            if (existing.Length > 0)
                _instance = existing[0];
            else
            {
                _instance = CreateInstance<GameAgentMini>();
                _instance.hideFlags = HideFlags.DontSave;
                _instance.OnEnable();
            }
        }
        return _instance;
    }

    [MenuItem("Tools/Game Agent Mini")]
    public static void ShowWindow()
    {
        _instance = GetWindow<GameAgentMini>("Game Agent Mini");
    }

    /// <summary>True while a model is loading or inference is running. Used by host UI to request repaints.</summary>
    public bool IsBusy => _isGenerating || _isLoadingModels;

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        RefreshModelList();
        LoadSkeletonAuto();
        FindEditorContext();
        TryLoadDefaultGameJson();
        AutoLoadGrammar();

        if (_isModelLoaded)
            _statusMessage = "Modelo ya cargado en memoria.";
        else if (!_isLoadingModels)
            InitModels();
    }

    private void OnDestroy()
    {
        UnsubscribeEditorContext();
        UnloadModel();
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  MODEL MANAGEMENT
    // ══════════════════════════════════════════════════════════

    private void RefreshModelList()
    {
        string streamingPath = Application.streamingAssetsPath;

        if (Directory.Exists(streamingPath))
        {
            _availableModels = Directory.GetFiles(streamingPath, "*.gguf")
                .OrderBy(f => f).ToArray();
            _availableModelNames = _availableModels
                .Select(Path.GetFileName).ToArray();
        }
        else
        {
            _availableModels = Array.Empty<string>();
            _availableModelNames = Array.Empty<string>();
        }
        if (_selectedModelIndex >= _availableModels.Length)
            _selectedModelIndex = 0;
    }

    private async void InitModels()
    {
        if (_isLoadingModels) return;
        if (_isModelLoaded)
        {
            _statusMessage = "Modelo ya cargado.";
            return;
        }

        if (_availableModels.Length == 0)
        {
            _statusMessage = "No se encontraron modelos .gguf en StreamingAssets.";
            Repaint();
            return;
        }

        _isLoadingModels = true;
        _statusMessage = "Cargando modelo…";
        Repaint();

        try
        {
            int cpuThreads = Math.Max(1, SystemInfo.processorCount / 2);

            string modelPath = _availableModels[_selectedModelIndex];
            _statusMessage = $"Cargando agente: {Path.GetFileName(modelPath)}…";
            Repaint();
            await LoadAgentModelAsync(cpuThreads, modelPath);

            _statusMessage = BuildLoadedStatusMessage();
            _chatHistory.Clear();
            _chatHistory.Add("Sistema: Agente listo.");
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error al cargar: {ex.Message}";
            Debug.LogError($"[GameAgent] {ex}");
        }
        finally
        {
            _isLoadingModels = false;
            Repaint();
        }
    }

    private async Task LoadAgentModelAsync(int cpuThreads, string modelPath)
    {
        // Purge any prior model instance (GPU/CPU KV cache, weights) before loading new one.
        DisposeModelNative();

        _parameters = new ModelParams(modelPath)
        {
            ContextSize    = 16384,
            BatchSize      = 8192,
            UBatchSize     = 512,
            GpuLayerCount  = 99,
            MainGpu        = 0,
            Threads        = cpuThreads,
            BatchThreads   = cpuThreads,
            UseMemorymap   = true,
            UseMemoryLock  = false,
            FlashAttention = true
        };

        await Task.Run(() =>
        {
            _model    = LLamaWeights.LoadFromFile(_parameters);
            _context  = _model.CreateContext(_parameters);
            _executor = new InteractiveExecutor(_context);
        });

        _isModelLoaded = true;

        // Warm-up
        _statusMessage = "Calentando modelo agente…";
        Repaint();

        await Task.Run(async () =>
        {
            var warmParams = new InferenceParams
            {
                MaxTokens    = 1,
                AntiPrompts  = AntiPrompts,
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f }
            };
            await foreach (var _ in _executor.InferAsync("Hello\n", warmParams)) { }
        });

        _context.Dispose();
        _context  = _model.CreateContext(_parameters);
        _executor = new InteractiveExecutor(_context);
    }

    private string BuildLoadedStatusMessage()
    {
        return _isModelLoaded ? "Modelo agente cargado. Listo." : "No se cargó el modelo.";
    }

    /// <summary>
    /// Disposes native LLama resources (weights + context) and flushes any orphan
    /// native handles from prior domain reloads via forced GC. Safe to call repeatedly.
    /// </summary>
    private static void DisposeModelNative()
    {
        try { _context?.Dispose(); } catch { }
        try { _model?.Dispose();   } catch { }
        _context    = null;
        _executor   = null;
        _model      = null;
        _parameters = null;
        _isModelLoaded = false;

        // Flush orphan LLamaSharp SafeHandle finalizers (e.g. from domain reload leaks).
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void UnloadModel()
    {
        DisposeModelNative();

        _isLoadingModels = false;
        _isGenerating    = false;
        _chatHistory.Clear();
        _lastAIResponse = "";
        _statusMessage  = "Modelos descargados.";
        Repaint();
    }

    private void ResetContext()
    {
        if (!_isModelLoaded || _isGenerating) return;
        try
        {
            _context?.Dispose();
            _context  = _model.CreateContext(_parameters);
            _executor = new InteractiveExecutor(_context);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameAgent] Error resetting: {ex}");
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  EDITOR CONTEXT INTEGRATION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Locate the shared EditorContext asset and subscribe to its events.
    /// </summary>
    private void FindEditorContext()
    {
        _editorContext = AssetDatabase.LoadAssetAtPath<GameRuleEditor.Core.EditorContext>(EDITOR_CONTEXT_PATH);
        if (_editorContext != null)
        {
            _editorContext.OnProjectLoaded  -= OnEditorProjectLoaded;
            _editorContext.OnProjectChanged -= OnEditorProjectChanged;
            _editorContext.OnActorListChanged -= OnEditorProjectChanged;
            _editorContext.OnProjectLoaded  += OnEditorProjectLoaded;
            _editorContext.OnProjectChanged += OnEditorProjectChanged;
            _editorContext.OnActorListChanged += OnEditorProjectChanged;
        }
    }

    private void UnsubscribeEditorContext()
    {
        if (_editorContext != null)
        {
            _editorContext.OnProjectLoaded  -= OnEditorProjectLoaded;
            _editorContext.OnProjectChanged -= OnEditorProjectChanged;
            _editorContext.OnActorListChanged -= OnEditorProjectChanged;
        }
    }

    /// <summary>
    /// Called when the editor loads a different project.
    /// </summary>
    private void OnEditorProjectLoaded()
    {
        SyncFromEditorProject();
        UpdateJsonDisplay();
        _changedLines.Clear();
        _auditLog.Add("↻ Proyecto actualizado desde el Editor.");
        Repaint();
    }

    /// <summary>
    /// Called when the editor modifies the current project.
    /// </summary>
    private void OnEditorProjectChanged()
    {
        if (_syncingFromEditor) return; // avoid re-entrant sync
        SyncFromEditorProject();
        UpdateJsonDisplay();
        Repaint();
    }

    /// <summary>
    /// Pull data FROM the EditorContext project INTO the agent's _gameData.
    /// Uses deep copy (serialize/deserialize) so the agent works on its own
    /// instance — mutations won't affect the project until SyncToEditorProject.
    /// This ensures Undo.RecordObject captures the correct pre-mutation state.
    /// </summary>
    private void SyncFromEditorProject()
    {
        if (_editorContext?.currentProject == null) return;
        var project = _editorContext.currentProject;

        // Deep copy via serialization to get an independent SceneJson
        try
        {
            string json = GameJsonAuditor.SerializeGame(project.sceneData);
            _gameData = GameJsonAuditor.DeserializeGame(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameAgent] SyncFromEditorProject deep copy failed, using shallow: {ex.Message}");
            if (_gameData == null)
                _gameData = new SceneJson();

            _gameData.GameName         = project.sceneData.GameName;
            _gameData.ScreenResolution = project.sceneData.ScreenResolution;
            _gameData.CameraPosition   = project.sceneData.CameraPosition;
            _gameData.CameraRotation   = project.sceneData.CameraRotation;
            _gameData.SunPosition      = project.sceneData.SunPosition;
            _gameData.SunRotation      = project.sceneData.SunRotation;
            _gameData.SunColor         = project.sceneData.SunColor;
            _gameData.SunAmbientColor  = project.sceneData.SunAmbientColor;
            _gameData.BackgroundColor  = project.sceneData.BackgroundColor;
            _gameData.Gravity          = project.sceneData.Gravity;
            _gameData.SoundTrack       = project.sceneData.SoundTrack;
            _gameData.Cast             = new List<ActorJson>(project.actors ?? new List<ActorJson>());
            _gameData.CustomVariables   = project.sceneData.CustomVariables != null
                ? new List<CustomVariable>(project.sceneData.CustomVariables)
                : new List<CustomVariable>();
        }

        _gameName = project.projectName ?? _gameName;
    }

    /// <summary>
    /// Push data FROM the agent's _gameData INTO the EditorContext project.
    /// Fires change notifications so all editor panels refresh.
    /// </summary>
    private void SyncToEditorProject()
    {
        if (_editorContext?.currentProject == null || _gameData == null) return;

        _syncingFromEditor = true; // guard against re-entrant events
        try
        {
            var project = _editorContext.currentProject;

            UnityEditor.Undo.RecordObject(project, "Agent: Modify Game");

            // Sync scene-level fields
            project.sceneData.GameName         = _gameData.GameName;
            project.sceneData.ScreenResolution = _gameData.ScreenResolution;
            project.sceneData.CameraPosition   = _gameData.CameraPosition;
            project.sceneData.CameraRotation   = _gameData.CameraRotation;
            project.sceneData.SunPosition      = _gameData.SunPosition;
            project.sceneData.SunRotation      = _gameData.SunRotation;
            project.sceneData.SunColor         = _gameData.SunColor;
            project.sceneData.SunAmbientColor  = _gameData.SunAmbientColor;
            project.sceneData.BackgroundColor  = _gameData.BackgroundColor;
            project.sceneData.Gravity          = _gameData.Gravity;
            project.sceneData.SoundTrack       = _gameData.SoundTrack;

            // Sync actors — replace the project's list
            project.actors = _gameData.Cast ?? new List<ActorJson>();
            project.sceneData.Cast = project.actors;

            // Sync custom variables
            project.sceneData.CustomVariables = _gameData.CustomVariables;

            EditorUtility.SetDirty(project);
            EditorUtility.SetDirty(_editorContext);

            // Notify all editor panels
            _editorContext.NotifyActorListChanged();
            _editorContext.NotifyProjectChanged();
        }
        finally
        {
            _syncingFromEditor = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  GAME JSON MANAGEMENT
    // ══════════════════════════════════════════════════════════

    private string DefaultGameJsonPath =>
        Path.Combine(Application.dataPath, "Helpers", "game.json");

    private void TryLoadDefaultGameJson()
    {
        if (_gameData != null) return; // already loaded

        // If the editor has a project loaded, use that as initial data
        if (_editorContext?.currentProject != null)
        {
            SyncFromEditorProject();
            _gameJsonPath = DefaultGameJsonPath;
            UpdateJsonDisplay();
            _changedLines.Clear();
            _statusMessage = $"Proyecto del editor cargado: {_gameData.Cast?.Count ?? 0} actores.";
            return;
        }

        _gameJsonPath = DefaultGameJsonPath;
        if (File.Exists(_gameJsonPath))
        {
            LoadGameJsonFromDisk();
        }
        else
        {
            CreateNewGame();
        }
    }

    private void CreateNewGame()
    {
        _gameData = GameJsonAuditor.CreateDefaultGame(_gameName);
        _gameJsonPath = DefaultGameJsonPath;
        UpdateJsonDisplay();
        _changedLines.Clear();
        _auditLog.Clear();
        _auditLog.Add("✓ Juego nuevo creado con plantilla por defecto.");
        _statusMessage = "Nuevo game.json creado (en memoria).";
        SyncToEditorProject();
        Repaint();
    }

    private void LoadGameJsonFromDisk()
    {
        try
        {
            string json = File.ReadAllText(_gameJsonPath);
            _gameData = GameJsonAuditor.DeserializeGame(json);

            if (_gameData == null)
            {
                _statusMessage = "Error: no se pudo parsear game.json.";
                return;
            }

            // Run initial audit
            var audit = GameJsonAuditor.AuditSceneJson(_gameData);
            if (audit.FixedGame != null)
                _gameData = audit.FixedGame;

            _auditLog.Clear();
            if (audit.Fixes.Count > 0 || audit.Warnings.Count > 0)
            {
                _auditLog.AddRange(audit.Fixes.Select(f => $"✓ {f}"));
                _auditLog.AddRange(audit.Warnings.Select(w => $"⚠ {w}"));
            }
            else
            {
                _auditLog.Add("✓ game.json cargado sin problemas.");
            }

            UpdateJsonDisplay();
            _changedLines.Clear();
            _statusMessage = $"game.json cargado: {_gameData.Cast?.Count ?? 0} actores.";
            SyncToEditorProject();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error cargando: {ex.Message}";
            Debug.LogError($"[GameAgent] {ex}");
        }
        Repaint();
    }

    private void SaveGameJsonToDisk()
    {
        if (_gameData == null) return;

        try
        {
            _auditLog.Clear();

            // Run full audit before saving (non-blocking: save even if audit fails)
            try
            {
                var audit = GameJsonAuditor.AuditSceneJson(_gameData);
                if (audit.FixedGame != null)
                    _gameData = audit.FixedGame;

                _auditLog.AddRange(audit.Fixes.Select(f => $"✓ {f}"));
                _auditLog.AddRange(audit.Warnings.Select(w => $"⚠ {w}"));
            }
            catch (Exception ex)
            {
                _auditLog.Add($"⚠ Auditoría pre-guardado falló (se guarda igualmente): {ex.Message}");
                Debug.LogWarning($"[GameAgent] Pre-save audit failed: {ex}");
            }

            string json = GameJsonAuditor.SerializeGame(_gameData);

            string dir = Path.GetDirectoryName(_gameJsonPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_gameJsonPath, json);
            UpdateJsonDisplay();
            _changedLines.Clear();

            _auditLog.Add($"Guardado: {_gameJsonPath}");

            _statusMessage = "game.json guardado.";
            SyncToEditorProject();
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error guardando: {ex.Message}";
            Debug.LogError($"[GameAgent] {ex}");
        }
        Repaint();
    }

    private void LoadExternalGameJson()
    {
        string picked = EditorUtility.OpenFilePanel("Cargar game JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(picked)) return;

        _gameJsonPath = picked;
        LoadGameJsonFromDisk();
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  JSON DISPLAY & CHANGE TRACKING
    // ══════════════════════════════════════════════════════════

    private void UpdateJsonDisplay()
    {
        _previousJsonText = _currentJsonText;
        _currentJsonText = GameJsonAuditor.SerializeGame(_gameData);
        _currentJsonLines = _currentJsonText.Split('\n');
    }

    private void ComputeChangedLines()
    {
        try
        {
            _changedLines.Clear();
            if (string.IsNullOrEmpty(_previousJsonText) || _currentJsonLines == null)
            {
                // Everything is new
                if (_currentJsonLines != null)
                    for (int i = 0; i < _currentJsonLines.Length; i++)
                        _changedLines.Add(i);
                return;
            }

            string[] oldLines = _previousJsonText.Split('\n');
            string[] newLines = _currentJsonLines;

            // Guard: if both are identical, nothing changed
            if (_previousJsonText == _currentJsonText)
                return;

            // ── LCS-based diff: only mark lines that truly changed ──
            int m = oldLines.Length, n = newLines.Length;

            // Safety cap: for very large files fall back to simple comparison
            if ((long)m * n > 2_000_000)
            {
                for (int i = 0; i < newLines.Length; i++)
                {
                    if (i >= oldLines.Length || newLines[i].TrimEnd() != oldLines[i].TrimEnd())
                        _changedLines.Add(i);
                }
                return;
            }

            int[,] dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (oldLines[i - 1].TrimEnd() == newLines[j - 1].TrimEnd())
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            var unchangedNew = new HashSet<int>();
            int ii = m, jj = n;
            while (ii > 0 && jj > 0)
            {
                if (oldLines[ii - 1].TrimEnd() == newLines[jj - 1].TrimEnd())
                {
                    unchangedNew.Add(jj - 1);
                    ii--; jj--;
                }
                else if (dp[ii - 1, jj] > dp[ii, jj - 1])
                    ii--;
                else
                    jj--;
            }

            for (int i = 0; i < newLines.Length; i++)
            {
                if (!unchangedNew.Contains(i))
                    _changedLines.Add(i);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameAgent] ComputeChangedLines error: {ex.Message}");
            // Non-blocking: just mark nothing as changed
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  UNDO / REDO
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Save current game state to undo stack before any mutation.
    /// Clears the redo stack (new branch).
    /// </summary>
    private void PushUndoState()
    {
        if (_gameData == null) return;
        try
        {
            string snapshot = GameJsonAuditor.SerializeGame(_gameData);
            _undoStack.Add(snapshot);
            if (_undoStack.Count > MaxUndoHistory)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameAgent] PushUndoState error: {ex.Message}");
        }
    }

    private void Undo()
    {
        if (_undoStack.Count == 0 || _gameData == null) return;
        try
        {
            // Save current state to redo
            string current = GameJsonAuditor.SerializeGame(_gameData);
            _redoStack.Add(current);

            // Restore last undo state
            string prev = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            _gameData = GameJsonAuditor.DeserializeGame(prev);
            UpdateJsonDisplay();
            ComputeChangedLines();
            SyncToEditorProject();
            _auditLog.Add("↩ Deshacer aplicado.");
            _statusMessage = $"Deshacer ({_undoStack.Count} restantes)";
            Repaint();
        }
        catch (Exception ex)
        {
            _auditLog.Add($"⚠ Error deshaciendo: {ex.Message}");
            Debug.LogWarning($"[GameAgent] Undo error: {ex}");
        }
    }

    private void Redo()
    {
        if (_redoStack.Count == 0 || _gameData == null) return;
        try
        {
            // Save current state to undo
            string current = GameJsonAuditor.SerializeGame(_gameData);
            _undoStack.Add(current);

            // Restore last redo state
            string next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            _gameData = GameJsonAuditor.DeserializeGame(next);
            UpdateJsonDisplay();
            ComputeChangedLines();
            SyncToEditorProject();
            _auditLog.Add("↪ Rehacer aplicado.");
            _statusMessage = $"Rehacer ({_redoStack.Count} restantes)";
            Repaint();
        }
        catch (Exception ex)
        {
            _auditLog.Add($"⚠ Error rehaciendo: {ex.Message}");
            Debug.LogWarning($"[GameAgent] Redo error: {ex}");
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  ACTOR OPERATIONS
    // ══════════════════════════════════════════════════════════

    private string[] GetActorNames()
    {
        if (_gameData?.Cast == null || _gameData.Cast.Count == 0)
            return new string[] { "(ningún actor)" };
        return _gameData.Cast.Select(a => a.ActorName ?? "(sin nombre)").ToArray();
    }

    private void AddActorToGame(ActorJson actor)
    {
        if (_gameData == null) return;
        if (_gameData.Cast == null)
            _gameData.Cast = new List<ActorJson>();

        // Check if actor already exists
        int existing = _gameData.Cast.FindIndex(
            a => a.ActorName != null &&
                 a.ActorName.Equals(actor.ActorName, StringComparison.OrdinalIgnoreCase));

        if (existing >= 0)
        {
            _gameData.Cast[existing] = actor;
            _auditLog.Add($"♻ Actor '{actor.ActorName}' reemplazado en Cast.");
        }
        else
        {
            _gameData.Cast.Add(actor);
            _auditLog.Add($"+ Actor '{actor.ActorName}' añadido al Cast.");
        }
    }

    private void ModifyActorInGame(ActorJson actor)
    {
        if (_gameData?.Cast == null) return;

        int idx = _gameData.Cast.FindIndex(
            a => a.ActorName != null &&
                 a.ActorName.Equals(actor.ActorName, StringComparison.OrdinalIgnoreCase));

        if (idx >= 0)
        {
            _gameData.Cast[idx] = actor;
            _auditLog.Add($"✏ Actor '{actor.ActorName}' modificado.");
        }
        else
        {
            _gameData.Cast.Add(actor);
            _auditLog.Add($"+ Actor '{actor.ActorName}' no existía, añadido.");
        }
    }

    private void DeleteActorFromGame(int index)
    {
        if (_gameData?.Cast == null || index < 0 || index >= _gameData.Cast.Count) return;

        PushUndoState();

        string name = _gameData.Cast[index].ActorName;
        _gameData.Cast.RemoveAt(index);
        _auditLog.Add($"✕ Actor '{name}' eliminado del Cast.");

        UpdateJsonDisplay();
        ComputeChangedLines();
        SyncToEditorProject();
        _statusMessage = $"Actor '{name}' eliminado.";
        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  INFERENCE — sends instruction to LLM, audits output
    // ══════════════════════════════════════════════════════════

    private async void SendMessageToAgent()
    {
        if (!_isModelLoaded || _isGenerating || _isLoadingModels) return;

        string userMsg = _userInput.Trim();
        if (string.IsNullOrEmpty(userMsg)) return;

        _isGenerating = true;
        _userInput    = "";
        _chatHistory.Add($"User: {userMsg}");
        _chatHistory.Add("Agent: …");
        _auditLog.Clear();
        Repaint();

        try
        {
            string prompt = BuildAgentPrompt(userMsg);

            var inferParams = new InferenceParams
            {
                MaxTokens        = _maxTokens,
                AntiPrompts      = AntiPrompts,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature      = _temperature,
                    RepeatPenalty    = _repeatPenalty,
                    FrequencyPenalty = _frequencyPenalty,
                    PresencePenalty  = _presencePenalty,
                    PenaltyCount     = _penaltyCount,
                    PenalizeNewline  = false
                }
            };

            string fullResponse = "";
            bool stoppedByLoop = false;
            int loopWindow  = _loopCheckWindowSize;
            int loopRepeats = _loopMaxRepeats;

            await Task.Run(async () =>
            {
                await foreach (var text in _executor.InferAsync(prompt, inferParams))
                {
                    fullResponse += text;

                    if (DetectRepetitionLoop(fullResponse, loopWindow, loopRepeats))
                    {
                        stoppedByLoop = true;
                        break;
                    }

                    string snapshot = fullResponse;
                    EditorApplication.delayCall += () =>
                    {
                        UpdateLastChatMessage($"Agent: {snapshot}");
                    };
                }
            });

            if (stoppedByLoop)
                Debug.LogWarning("[GameAgent] Generación detenida: repetición detectada.");

            // Extract think content from raw response BEFORE cleaning
            string thinkContent = GameJsonAuditor.ExtractThinkContent(fullResponse);
            if (!string.IsNullOrEmpty(thinkContent))
                _lastThinkContent = thinkContent;
            else
                _lastThinkContent = "";

            // Clean response (strips think tags among other things)
            string cleanResponse = CleanResponse(fullResponse);
            _lastAIResponse = cleanResponse;

            // ── AUDITOR LAYER ── extract JSON, audit, apply ──
            ProcessAgentResponse(cleanResponse, userMsg);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameAgent] Inference error: {ex}");
            UpdateLastChatMessage($"Agent: [Error: {ex.Message}]");
            _auditLog.Add($"✕ Error: {ex.Message}");
        }
        finally
        {
            _isGenerating = false;
            ResetContext();
            Repaint();
        }
    }

    /// <summary>
    /// Process the agent's raw response: extract JSON, audit, merge into game.
    /// This is the core auditor integration layer.
    /// </summary>
    // Keywords/patterns that indicate the user explicitly mentioned position/coordinates
    private static readonly Regex PositionKeywordsRegex = new Regex(
        @"(?:" +
            // Explicit keywords (EN + ES)
            @"\b(position|place|locate|spawn\s*at|move\s*to|put\s*at|set\s*at|coord|posici[oó]n|ubicaci[oó]n|colocar|situar|poner)\b" +
            @"|" +
            // Axis assignments: x=5, y = 3, z:10
            @"\b([xyz])\s*[=:]\s*-?\d" +
            @"|" +
            // Tuple-like coordinate patterns: (1,2,3)  [1, 2, 3]  1,2,3
            @"[\[\(]?\s*-?\d+\.?\d*\s*,\s*-?\d+\.?\d*\s*,\s*-?\d+\.?\d*\s*[\]\)]?" +
            @"|" +
            // Natural language coordinates: at 0 5 0, at 1, 2, 3
            @"\bat\s+-?\d+[\s,]+-?\d+[\s,]+-?\d+" +
        @")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the user's message mentions position/coordinates.
    /// </summary>
    private static bool UserMentionedPosition(string userMessage)
    {
        if (string.IsNullOrEmpty(userMessage)) return false;
        return PositionKeywordsRegex.IsMatch(userMessage);
    }

    // Keywords/patterns that indicate the user explicitly mentioned rotation/orientation

    /*
    private static readonly Regex RotationKeywordsRegex = new Regex(
        @"(?:" +
            // Explicit keywords (EN + ES)
            @"\b(rotat|orient|facing|look\s*at|turn|angle|degree|gir[ao]|rotaci[oó]n|orientaci[oó]n|mirar|apuntar|ángulo|grados)\b" +
            @"|" +
            // Axis rotation assignments: rx=5, ry = 90
            @"\b(r[xyz])\s*[=:]\s*-?\d" +
        @")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the user's message mentions rotation/orientation.
    /// </summary>
    private static bool UserMentionedRotation(string userMessage)
    {
        if (string.IsNullOrEmpty(userMessage)) return false;
        return RotationKeywordsRegex.IsMatch(userMessage);
    }
    */

    // ── Self-referencing target fix ──────────────────────────

    // Detects NavigateTo/PushTo where target coords are this.x, this.y, this.z (always a no-op)
    private static readonly Regex SelfPosTargetRegex = new Regex(
        @"^(NavigateTo|PushTo)\(([^,]+),\s*this\.x\s*,\s*this\.y\s*,\s*this\.z\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detects RotateTo where target coords are this.rx, this.ry, this.rz (always a no-op)
    // Matches: RotateTo(speed,this.rx,this.ry,this.rz) — 4 params
    // Also:    RotateTo(speed,this.rx,this.ry,this.rz,ox,oy,oz) — 7 params (origin variant)
    private static readonly Regex SelfRotTargetRegex = new Regex(
        @"^(RotateTo)\(([^,]+),\s*this\.rx\s*,\s*this\.ry\s*,\s*this\.rz\s*(,.+)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Fix actions where the target is the actor itself.
    /// NavigateTo/PushTo to this.x/y/z and RotateTo to this.rx/ry/rz are always no-ops.
    /// Resolves the intended target from: 1) user message, 2) When conditions.
    /// </summary>
    private void FixSelfReferencingTargets(ActorJson actor, string userMessage)
    {
        if (actor?.Script == null) return;

        // Check if there are any self-referencing targets to fix
        bool hasPosSelfRef = false;
        bool hasRotSelfRef = false;
        foreach (var rule in actor.Script)
        {
            if (rule.Do == null) continue;
            foreach (var action in rule.Do)
            {
                if (SelfPosTargetRegex.IsMatch(action)) hasPosSelfRef = true;
                if (SelfRotTargetRegex.IsMatch(action)) hasRotSelfRef = true;
                if (hasPosSelfRef && hasRotSelfRef) break;
            }
            if (hasPosSelfRef && hasRotSelfRef) break;
        }
        if (!hasPosSelfRef && !hasRotSelfRef) return;

        // Resolve intended target actor
        string targetActor = ResolveTargetActor(actor, userMessage);
        if (targetActor == null)
        {
            string actions = hasPosSelfRef && hasRotSelfRef ? "NavigateTo/PushTo/RotateTo"
                : hasPosSelfRef ? "NavigateTo/PushTo" : "RotateTo";
            _auditLog.Add($"⚠ [{actor.ActorName}] {actions} apunta a this.x/y/z o this.rx/ry/rz (no-op), " +
                          "pero no se pudo determinar el actor objetivo. Revisa manualmente.");
            return;
        }

        // Apply fixes
        foreach (var rule in actor.Script)
        {
            if (rule.Do == null) continue;
            for (int i = 0; i < rule.Do.Count; i++)
            {
                // Fix position self-references: NavigateTo/PushTo
                if (hasPosSelfRef)
                {
                    var mp = SelfPosTargetRegex.Match(rule.Do[i]);
                    if (mp.Success)
                    {
                        string actionName = mp.Groups[1].Value;
                        string firstParam = mp.Groups[2].Value;
                        rule.Do[i] = $"{actionName}({firstParam},{targetActor}.x,{targetActor}.y,{targetActor}.z)";
                        _auditLog.Add($"✓ [{actor.ActorName}] {actionName}(…this.x/y/z) → {targetActor}.x/y/z");
                        continue;
                    }
                }

                // Fix rotation self-references: RotateTo
                if (hasRotSelfRef)
                {
                    var mr = SelfRotTargetRegex.Match(rule.Do[i]);
                    if (mr.Success)
                    {
                        string actionName = mr.Groups[1].Value;
                        string firstParam = mr.Groups[2].Value;
                        string originPart = mr.Groups[3].Success ? mr.Groups[3].Value : "";
                        rule.Do[i] = $"{actionName}({firstParam},{targetActor}.rx,{targetActor}.ry,{targetActor}.rz{originPart})";
                        _auditLog.Add($"✓ [{actor.ActorName}] {actionName}(…this.rx/ry/rz) → {targetActor}.rx/ry/rz");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Try to determine which actor the user intended as a target.
    /// Sources (in priority order):
    ///   1. User message mentions an existing actor name
    ///   2. Collision(ActorName/Tag) in When conditions
    ///   3. Cross-actor .x/.y/.z references in When conditions
    /// </summary>
    private string ResolveTargetActor(ActorJson actor, string userMessage)
    {
        string selfName = actor.ActorName ?? "";

        // Source 1: User message mentions an existing actor name
        if (!string.IsNullOrEmpty(userMessage) && _gameData?.Cast != null)
        {
            string msgLower = userMessage.ToLowerInvariant();
            foreach (var a in _gameData.Cast)
            {
                if (string.IsNullOrEmpty(a.ActorName)) continue;
                if (a.ActorName.Equals(selfName, StringComparison.OrdinalIgnoreCase)) continue;
                if (msgLower.Contains(a.ActorName.ToLowerInvariant()))
                    return a.ActorName;
            }
        }

        // Source 2: Collision(Tag/ActorName) in When conditions
        if (actor.Script != null && _gameData?.Cast != null)
        {
            var collisionRegex = new Regex(@"Collision\((\w+)\)", RegexOptions.IgnoreCase);
            foreach (var rule in actor.Script)
            {
                if (rule.When == null) continue;
                foreach (var when in rule.When)
                {
                    var cm = collisionRegex.Match(when);
                    if (!cm.Success) continue;
                    string tag = cm.Groups[1].Value;

                    // Try exact actor name match first
                    var byName = _gameData.Cast.Find(a =>
                        a.ActorName != null &&
                        a.ActorName.Equals(tag, StringComparison.OrdinalIgnoreCase) &&
                        !a.ActorName.Equals(selfName, StringComparison.OrdinalIgnoreCase));
                    if (byName != null) return byName.ActorName;

                    // Then try tag match
                    var byTag = _gameData.Cast.Find(a =>
                        a.Tag != null &&
                        a.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(a.ActorName) &&
                        !a.ActorName.Equals(selfName, StringComparison.OrdinalIgnoreCase));
                    if (byTag != null) return byTag.ActorName;
                }
            }

            // Source 3: Cross-actor .x/.y/.z/.rx/.ry/.rz references in When
            var crossRefRegex = new Regex(@"(?<!\w)([A-Z]\w*)\.(?:r?[xyz])\b", RegexOptions.Compiled);
            foreach (var rule in actor.Script)
            {
                if (rule.When == null) continue;
                foreach (var when in rule.When)
                {
                    var crm = crossRefRegex.Match(when);
                    if (crm.Success)
                    {
                        string refName = crm.Groups[1].Value;
                        if (!refName.Equals("this", StringComparison.OrdinalIgnoreCase) &&
                            !refName.Equals(selfName, StringComparison.OrdinalIgnoreCase))
                            return refName;
                    }
                }
            }
        }

        return null;
    }

    private void ProcessAgentResponse(string response, string userMessage = null)
    {
        // Note: think content is already extracted and stored in _lastThinkContent
        // by SendMessageToAgent before this method is called.
        // The response received here is already cleaned (think tags stripped).

        // 1. Extract JSON from response (think tags are stripped automatically)
        string jsonBlock = GameJsonAuditor.ExtractJsonFromResponse(response);

        if (string.IsNullOrEmpty(jsonBlock))
        {
            UpdateLastChatMessage($"Agent: {response}\n[No se detectó JSON válido]");
            _auditLog.Add("✕ No se pudo extraer JSON de la respuesta del agente.");
            return;
        }

        // 2. Audit the actor JSON (non-blocking: even with errors, try to apply)
        GameJsonAuditor.AuditResult audit = null;
        try
        {
            audit = GameJsonAuditor.AuditActor(jsonBlock);
        }
        catch (Exception ex)
        {
            _auditLog.Add($"✕ Excepción en auditoría: {ex.Message}");
        }

        // 3. Show audit results
        if (audit != null)
        {
            foreach (var fix in audit.Fixes)
                _auditLog.Add($"✓ {fix}");
            foreach (var warn in audit.Warnings)
                _auditLog.Add($"⚠ {warn}");

            if (!audit.IsValid)
                _auditLog.Add($"⚠ Auditoría con errores: {audit.Error}");
            else if (audit.Fixes.Count == 0 && audit.Warnings.Count == 0)
                _auditLog.Add("✓ Actor válido, sin correcciones necesarias.");
        }

        // 4. Determine the actor to apply: prefer audited, fallback to raw parse
        ActorJson actor = audit?.FixedActor;
        if (actor == null)
        {
            // Audit couldn't produce a fixed actor — try raw deserialization as fallback
            try
            {
                actor = GameJsonAuditor.DeserializeActor(jsonBlock);
                if (actor != null)
                    _auditLog.Add("⚠ Usando actor sin auditar (fallback).");
            }
            catch (Exception ex)
            {
                _auditLog.Add($"✕ No se pudo parsear el actor: {ex.Message}");
            }
        }

        if (actor == null)
        {
            UpdateLastChatMessage($"Agent: {response}\n[No se pudo obtener un actor válido]");
            _auditLog.Add("✕ No se pudo obtener ningún actor de la respuesta.");
            return;
        }

        // 4b. If user did NOT mention position/rotation, reset to default [0,0,0]
        //     This prevents the LLM from inventing random values.
        if (_mode == AgentMode.Create)
        {
            if (!UserMentionedPosition(userMessage) && actor.Position != null)
            {
                _auditLog.Add($"✓ [{actor.ActorName}] Posición reseteada a [0,0,0] (usuario no especificó posición).");
                actor.Position = null;
            }
            /*if (!UserMentionedRotation(userMessage) && actor.Rotation != null)
            {
                _auditLog.Add($"✓ [{actor.ActorName}] Rotación reseteada a [0,0,0] (usuario no especificó rotación).");
                actor.Rotation = null;
            }*/
        }

        // 4c. Fix self-referencing targets (NavigateTo/PushTo/RotateTo to self is always a no-op)
        try { FixSelfReferencingTargets(actor, userMessage); }
        catch (Exception ex) { _auditLog.Add($"⚠ Error corrigiendo self-ref targets: {ex.Message}"); }

        // 5. Apply to game.json based on mode
        PushUndoState();
        try
        {
            switch (_mode)
            {
                case AgentMode.Create:
                    AddActorToGame(actor);
                    break;

                case AgentMode.Modify:
                    ModifyActorInGame(actor);
                    break;
            }
        }
        catch (Exception ex)
        {
            _auditLog.Add($"✕ Error aplicando actor: {ex.Message}");
        }

        // 6. Run full game audit after merge (non-blocking)
        try
        {
            var gameAudit = GameJsonAuditor.AuditSceneJson(_gameData);
            if (gameAudit.FixedGame != null)
                _gameData = gameAudit.FixedGame;

            foreach (var fix in gameAudit.Fixes)
                _auditLog.Add($"✓ [Game] {fix}");
            foreach (var warn in gameAudit.Warnings)
                _auditLog.Add($"⚠ [Game] {warn}");

            // Auto-apply pending globals as float (mini mode — no UI for type selection)
            if (gameAudit.PendingGlobals.Count > 0)
            {
                if (_gameData.CustomVariables == null)
                    _gameData.CustomVariables = new List<CustomVariable>();

                foreach (var name in gameAudit.PendingGlobals)
                {
                    bool exists = _gameData.CustomVariables.Any(
                        cv => cv.name != null && cv.name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        _gameData.CustomVariables.Add(new CustomVariable { name = name, type = "float" });
                        _auditLog.Add($"+ Variable global '#{name}' (float) añadida automáticamente.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _auditLog.Add($"⚠ Error en auditoría global (no bloquea): {ex.Message}");
            Debug.LogWarning($"[GameAgent] AuditSceneJson fallo: {ex}");
        }

        // 7. Update display (non-blocking)
        try
        {
            UpdateJsonDisplay();
            ComputeChangedLines();
        }
        catch (Exception ex)
        {
            _auditLog.Add($"⚠ Error actualizando vista: {ex.Message}");
            Debug.LogWarning($"[GameAgent] Display update fallo: {ex}");
        }

        // 8. Sync to editor project
        SyncToEditorProject();

        // Show clean JSON in chat
        string displayJson = audit?.FixedJson ?? jsonBlock;
        UpdateLastChatMessage($"Agent:\n{displayJson}");
        _statusMessage = $"Actor '{actor.ActorName}' procesado y aplicado.";
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  PROMPT BUILDING
    // ══════════════════════════════════════════════════════════

    private string BuildAgentPrompt(string userMessage)
    {
        var prompt = $"{AGENT_SYSTEM_PROMPT}\n\n";

        var inputParts = new List<string>();

        // Actor skeleton (schema reference)
        if (!string.IsNullOrEmpty(_skeletonText))
        {
            inputParts.Add($"ACTOR SCHEMA REFERENCE:\n{_skeletonText}");
        }

        // ── Context: Actors ──
        inputParts.Add(BuildActorContext());

        // ── Context: Global Variables ──
        inputParts.Add(BuildGlobalVariablesContext());

        // ── Context: Available Prefabs ──
        string prefabCtx = BuildPrefabContext();
        if (prefabCtx != null) inputParts.Add(prefabCtx);

        // ── Context: Available Animations ──
        string animCtx = BuildAnimationContext();
        if (animCtx != null) inputParts.Add(animCtx);

        // ── Context: Available Sounds ──
        string soundCtx = BuildSoundContext();
        if (soundCtx != null) inputParts.Add(soundCtx);

        // If modifying, include the target actor's current JSON
        if (_mode == AgentMode.Modify && _gameData?.Cast != null && _gameData.Cast.Count > 0)
        {
            int idx = Mathf.Clamp(_selectedActorIndex, 0, _gameData.Cast.Count - 1);
            var targetActor = _gameData.Cast[idx];
            string targetJson = GameJsonAuditor.FormatGameJson(
                JsonSerializer.Serialize(targetActor, GameJsonAuditor.JsonOptions));
            inputParts.Add(
                $"ACTOR TO MODIFY (apply changes to this):\n{targetJson}\n" +
                "IMPORTANT: Output the COMPLETE modified actor JSON with ALL fields.");
        }

        // Build instruction section
        string modeHint = _mode switch
        {
            AgentMode.Create => "CREATE a new actor:",
            AgentMode.Modify => "MODIFY the specified actor:",
            _ => ""
        };

        prompt += $"### Instruction:\n{modeHint}: {userMessage}\n\n";
        prompt += $"### Input:\n{string.Join("\n\n", inputParts)}\n\n";
        prompt += "### Response:\n";

        return prompt;
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  Context builders for prompt injection
    // ══════════════════════════════════════════════════════════

    private string BuildActorContext()
    {
        if (_gameData?.Cast == null || _gameData.Cast.Count == 0)
            return "EXISTING ACTORS: (none)";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("EXISTING ACTORS:");
        foreach (var actor in _gameData.Cast)
        {
            string tag = !string.IsNullOrEmpty(actor.Tag) ? $" Tag={actor.Tag}" : "";
            sb.Append($"  - {actor.ActorName ?? "?"} (Prefab={actor.PrefabName ?? "?"}{tag})");

            // List custom properties
            if (actor.Properties != null && actor.Properties.Count > 0)
            {
                sb.Append($"  Props: [{string.Join(", ", actor.Properties)}]");
            }

            // Summarize script rule count
            if (actor.Script != null && actor.Script.Count > 0)
            {
                sb.Append($"  Script: {actor.Script.Count} rules");
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private string BuildGlobalVariablesContext()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("GLOBAL VARIABLES: Built-in: #CameraPosition, #CameraRotation, #MouseWorld");

        if (_gameData?.CustomVariables != null && _gameData.CustomVariables.Count > 0)
        {
            sb.Append(". Custom: ");
            var cvParts = new List<string>();
            foreach (var cv in _gameData.CustomVariables)
            {
                if (string.IsNullOrEmpty(cv.name)) continue;
                string val;
                switch (cv.type?.ToLower())
                {
                    case "bool":  val = cv.boolValue.ToString(); break;
                    case "int":   val = cv.intValue.ToString(); break;
                    default:      val = cv.floatValue.ToString(); break;
                }
                cvParts.Add($"#{cv.name}({cv.type}={val})");
            }
            sb.Append(string.Join(", ", cvParts));
        }
        else
        {
            sb.Append(". Custom: (none)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Build list of available prefabs from the project.
    /// TODO: scan project for prefab assets when prefab library is ready.
    /// </summary>
    private string BuildPrefabContext()
    {
        // Future: scan Assets for .prefab files and list them
        // string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Prefabs"});
        return null; // Not yet implemented
    }

    /// <summary>
    /// Build list of available animations.
    /// TODO: scan project for animation clips when library is ready.
    /// </summary>
    private string BuildAnimationContext()
    {
        // Future: scan Assets for AnimationClip assets
        // string[] anims = AssetDatabase.FindAssets("t:AnimationClip");
        return null; // Not yet implemented
    }

    /// <summary>
    /// Build list of available sounds.
    /// TODO: scan project for audio clips when library is ready.
    /// </summary>
    private string BuildSoundContext()
    {
        // Future: scan Assets for AudioClip assets
        // string[] sounds = AssetDatabase.FindAssets("t:AudioClip");
        return null; // Not yet implemented
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  HELPERS
    // ══════════════════════════════════════════════════════════

    private void LoadSkeletonAuto()
    {
        if (!string.IsNullOrEmpty(_skeletonText)) return;
        string path = Path.Combine(Application.dataPath, "Editor", "actor_skeleton.json");
        if (File.Exists(path))
        {
            _skeletonText = File.ReadAllText(path);
        }
    }

    private string CleanResponse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        string cleaned = raw;
        foreach (var ap in AntiPrompts)
        {
            int idx = cleaned.IndexOf(ap, StringComparison.Ordinal);
            if (idx >= 0) cleaned = cleaned.Substring(0, idx);
        }
        cleaned = TrimTrailingRepetition(cleaned);
        // Strip <think> tags from cleaned output — the content is stored separately
        cleaned = GameJsonAuditor.StripThinkTags(cleaned);
        return cleaned.Trim();
    }

    private static bool DetectRepetitionLoop(string text, int windowSize, int maxRepeats)
    {
        if (string.IsNullOrEmpty(text)) return false;
        int minLen = windowSize * maxRepeats;
        if (text.Length < minLen) return false;

        for (int patLen = windowSize; patLen >= 4; patLen--)
        {
            string tail = text.Substring(text.Length - patLen);
            int count = 0;
            int pos = text.Length - patLen;
            while (pos >= 0)
            {
                if (text.Substring(pos, patLen) == tail)
                    count++;
                else
                    break;
                pos -= patLen;
            }
            if (count >= maxRepeats) return true;
        }
        return false;
    }

    private static string TrimTrailingRepetition(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 20) return text;
        for (int patLen = 6; patLen <= text.Length / 3; patLen++)
        {
            string pattern = text.Substring(text.Length - patLen);
            int pos = text.Length - patLen;
            int repeats = 0;
            while (pos >= 0 && text.Substring(pos, patLen) == pattern)
            {
                repeats++;
                pos -= patLen;
            }
            if (repeats >= 2)
                return text.Substring(0, pos + patLen + patLen).TrimEnd();
        }
        return text;
    }

    private void UpdateLastChatMessage(string message)
    {
        if (_chatHistory.Count > 0)
            _chatHistory[_chatHistory.Count - 1] = message;
        Repaint();
    }

    private void LoadGrammar()
    {
        if (string.IsNullOrEmpty(_grammarPath)) return;
        try
        {
            if (File.Exists(_grammarPath))
            {
                _grammarText = File.ReadAllText(_grammarPath);
                _grammarFileName = Path.GetFileName(_grammarPath);
                _statusMessage = $"Gramática cargada: {_grammarFileName}";
            }
            else
            {
                _grammarFileName = "(no encontrado)";
            }
        }
        catch (Exception ex)
        {
            _grammarFileName = "(error)";
            _statusMessage = $"Error gramática: {ex.Message}";
        }
        Repaint();
    }

    /// <summary>
    /// Auto-load the first .gbnf grammar found in StreamingAssets.
    /// </summary>
    private void AutoLoadGrammar()
    {
        if (_useGrammar && !string.IsNullOrEmpty(_grammarText)) return;

        string streamingPath = Application.streamingAssetsPath;
        if (!Directory.Exists(streamingPath)) return;

        string[] gbnfFiles = Directory.GetFiles(streamingPath, "*.gbnf");
        if (gbnfFiles.Length == 0) return;

        _grammarPath = gbnfFiles[0];
        _useGrammar = true;
        LoadGrammar();
    }

    // ══════════════════════════════════════════════════════════
    #endregion
    #region  GUI
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawAgentGUI();
    }

    /// <summary>
    /// Public GUI drawing method. Can be called from an IMGUIContainer in another window.
    /// </summary>
    public void DrawAgentGUI()
    {
        EditorGUILayout.Space(4);

        // ── Mode selector + Actor dropdown (one line) ──
        EditorGUILayout.BeginHorizontal();

        Color defaultBg = GUI.backgroundColor;

        GUI.backgroundColor = _mode == AgentMode.Create ? new Color(0.3f, 0.8f, 0.3f) : defaultBg;
        if (GUILayout.Button("Crear", EditorStyles.miniButtonLeft, GUILayout.Height(22)))
            _mode = AgentMode.Create;

        GUI.backgroundColor = _mode == AgentMode.Modify ? new Color(0.9f, 0.8f, 0.2f) : defaultBg;
        if (GUILayout.Button("Modificar", EditorStyles.miniButtonRight, GUILayout.Height(22)))
            _mode = AgentMode.Modify;

        GUI.backgroundColor = defaultBg;

        // Actor selector (visible in Modify mode)
        if (_mode == AgentMode.Modify)
        {
            string[] actorNames = GetActorNames();
            _selectedActorIndex = EditorGUILayout.Popup(
                _selectedActorIndex, actorNames, GUILayout.MinWidth(80));
            if (_selectedActorIndex >= actorNames.Length)
                _selectedActorIndex = 0;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // ── Input field (with status hint) + Send button ──
        EditorGUILayout.BeginHorizontal();

        string controlName = "MiniAgentInput";
        GUI.SetNextControlName(controlName);
        _userInput = EditorGUILayout.TextArea(_userInput,
            GUILayout.Height(32), GUILayout.ExpandWidth(true));

        // Draw status as placeholder hint when input is empty and unfocused
        if (string.IsNullOrEmpty(_userInput) && GUI.GetNameOfFocusedControl() != controlName)
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            var hintStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Italic,
                fontSize  = 11,
                padding   = new RectOffset(4, 4, 6, 0)
            };
            hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            GUI.Label(lastRect, _statusMessage, hintStyle);
        }

        GUI.enabled = _isModelLoaded && !_isGenerating && !_isLoadingModels;
        if (GUILayout.Button("Enviar", GUILayout.Height(32), GUILayout.Width(55)))
            SendMessageToAgent();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        // Loading indicator
        if (_isLoadingModels)
        {
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
        }

        // Send on Ctrl+Enter
        if (Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Return
            && Event.current.control
            && _isModelLoaded && !_isGenerating && !_isLoadingModels
            && !string.IsNullOrEmpty(_userInput))
        {
            SendMessageToAgent();
            Event.current.Use();
        }
    }

    // ══════════════════════════════════════════════════════════
    #endregion
}
