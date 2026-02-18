using UnityEngine;
using UnityEditor;
using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Unity Editor window that provides a local LLaMA chat interface.
/// Uses the proven async void + Task.Run(async => await foreach) pattern.
/// </summary>
public class LLamaTool : EditorWindow
{
    // ── Model State (persistent across domain reloads) ──
    private static LLamaWeights    _model;
    private static LLamaContext    _context;
    private static InteractiveExecutor _executor;
    private static ModelParams     _parameters;
    private static bool            _isModelLoaded;

    // ── UI State ──
    private string _userInput      = "";
    private string _statusMessage  = "";
    private Vector2 _scrollPos;
    private List<string> _chatHistory = new List<string>();
    private bool _isGenerating;

    // ── Settings ──
    private float  _temperature    = 0.0f;
    private int    _maxTokens      = 4096;
    private string _systemPrompt   = "Below is an instruction that describes a task, paired with an input that provides further context. Write a response that appropriately completes the request.";

    // ── Repetition Penalty Settings ──
    private float _repeatPenalty    = 1.15f;
    private float _frequencyPenalty = 0.1f;
    private float _presencePenalty  = 0.1f;
    private int   _penaltyCount    = 128;

    // ── Loop Detection ──
    private int _loopCheckWindowSize = 12;  // min tokens in a repeated pattern
    private int _loopMaxRepeats      = 3;   // how many repeats before stopping

    // ── Input Context (public so other scripts can set it) ──
    /// <summary>Current input context sent as ### Input: in the prompt.
    /// Editable from the editor window or settable from code via LLamaTool.InputContext.</summary>
    public static string InputContext = "";

    // ── Chain Last Response ──
    private bool   _chainLastResponse = true;
    private string _lastAIResponse    = "";

    // ── Model Selection ──
    private string[] _availableModels = Array.Empty<string>();
    private string[] _availableModelNames = Array.Empty<string>();
    private int      _selectedModelIndex = 0;

    // ── Grammar / GBNF ──
    private bool   _useGrammar;
    private string _grammarPath = "";
    private string _grammarText = "";
    private string _grammarFileName = "(ninguno)";

    // ── Actor Skeleton (injected as context) ──
    private bool   _useSkeleton     = true;
    private bool   _skeletonAlreadySent = false;
    private string _skeletonPath    = "";
    private string _skeletonText    = "";
    private string _skeletonFileName = "(ninguno)";

    // ── Anti-prompts ──
    private static readonly string[] AntiPrompts = { "### Instruction:", "### Input:", "User:" };

    // ── Think / Reasoning Display ──
    private bool   _showThinkReasoning = false;
    private string _lastThinkContent   = "";

    [MenuItem("Tools/LLama Tool")]
    public static void ShowWindow()
    {
        GetWindow<LLamaTool>("LLama Tool");
    }

    private void OnEnable()
    {
        RefreshModelList();
        AutoLoadSkeleton();
        if (_isModelLoaded)
            _statusMessage = "Modelo ya cargado en memoria.";
    }

    private void RefreshModelList()
    {
        string streamingPath = Application.streamingAssetsPath;
        if (Directory.Exists(streamingPath))
        {
            _availableModels = Directory.GetFiles(streamingPath, "*.gguf")
                .OrderBy(f => f)
                .ToArray();
            _availableModelNames = _availableModels
                .Select(Path.GetFileName)
                .ToArray();
        }
        else
        {
            _availableModels = Array.Empty<string>();
            _availableModelNames = Array.Empty<string>();
        }

        if (_selectedModelIndex >= _availableModels.Length)
            _selectedModelIndex = 0;
    }

    // ══════════════════════════════════════════════════════════
    //  MODEL LOADING — same async-void pattern as the original
    // ══════════════════════════════════════════════════════════

    private async void InitModel()
    {
        if (_isModelLoaded)
        {
            _statusMessage = "El modelo ya está cargado.";
            return;
        }

        _statusMessage = "Cargando modelo…";
        Repaint();

        try
        {
            int cpuThreads = Math.Max(1, Environment.ProcessorCount / 2);

            if (_availableModels.Length == 0)
            {
                _statusMessage = "No se encontraron modelos .gguf en StreamingAssets.";
                Repaint();
                return;
            }

            string modelPath = _availableModels[_selectedModelIndex];
            _statusMessage = $"Cargando: {Path.GetFileName(modelPath)}…";
            Repaint();

            _parameters = new ModelParams(modelPath)
            {
                ContextSize   = 8192,
                BatchSize     = 4096,
                UBatchSize    = 512,
                GpuLayerCount = 99,
                MainGpu       = 0,
                Threads       = cpuThreads,
                BatchThreads  = cpuThreads,
                UseMemorymap  = true,
                UseMemoryLock = false,
                FlashAttention = true
            };

            // Load model on background thread
            await Task.Run(() =>
            {
                _model   = LLamaWeights.LoadFromFile(_parameters);
                _context = _model.CreateContext(_parameters);
                _executor = new InteractiveExecutor(_context);
            });

            _isModelLoaded = true;

            // ── Warm-up: one tiny inference to prime GPU caches ──
            _statusMessage = "Calentando modelo…";
            Repaint();

            await Task.Run(async () =>
            {
                var warmParams = new InferenceParams
                {
                    MaxTokens    = 1,
                    AntiPrompts  = AntiPrompts,
                    SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f }
                };

                await foreach (var _ in _executor.InferAsync("Hello\n", warmParams))
                {
                    // consume single token
                }
            });

            // Reset context after warm-up so it starts clean
            _context.Dispose();
            _context  = _model.CreateContext(_parameters);
            _executor = new InteractiveExecutor(_context);

            _statusMessage = "Modelo cargado y listo.";
            _chatHistory.Clear();
            _chatHistory.Add("Sistema: Sistema listo.");
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error al cargar: {ex.Message}";
            Debug.LogError($"[LLamaTool] {ex}");
            _isModelLoaded = false;
        }

        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    //  INFERENCE — proven Task.Run + await foreach pattern
    // ══════════════════════════════════════════════════════════

    private async void SendMessageToAI()
    {
        if (!_isModelLoaded || _isGenerating) return;

        string userMsg = _userInput.Trim();
        if (string.IsNullOrEmpty(userMsg)) return;

        _isGenerating = true;
        _userInput    = "";
        _chatHistory.Add($"User: {userMsg}");
        _chatHistory.Add("AI: …");
        Repaint();

        try
        {
            // Build Alpaca-format prompt
            string prompt = BuildAlpacaPrompt(userMsg);

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
            bool stoppedByLoopDetection = false;

            // Capture settings for background thread
            int loopWindow  = _loopCheckWindowSize;
            int loopRepeats = _loopMaxRepeats;

            // Run inference on background thread — original proven pattern
            await Task.Run(async () =>
            {
                await foreach (var text in _executor.InferAsync(prompt, inferParams))
                {
                    fullResponse += text;

                    // ── Loop detection: stop if output is repeating ──
                    if (DetectRepetitionLoop(fullResponse, loopWindow, loopRepeats))
                    {
                        stoppedByLoopDetection = true;
                        break;
                    }

                    // Snapshot for closure
                    string snapshot = fullResponse;

                    // Update UI via delayCall (works from background thread in Editor)
                    EditorApplication.delayCall += () =>
                    {
                        UpdateLastHistoryMessage($"AI: {snapshot}");
                    };
                }
            });

            if (stoppedByLoopDetection)
                Debug.LogWarning("[LLamaTool] Generación detenida: repetición detectada.");

            // Final update on main thread
            string cleanResponse = CleanResponse(fullResponse);
            UpdateLastHistoryMessage($"AI: {cleanResponse}");

            // Store for chaining
            _lastAIResponse = cleanResponse;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LLamaTool] Inference error: {ex}");
            UpdateLastHistoryMessage($"AI: [Error: {ex.Message}]");
        }
        finally
        {
            _isGenerating = false;
            Repaint();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PROMPT BUILDING
    // ══════════════════════════════════════════════════════════

    private string BuildAlpacaPrompt(string userMessage)
    {
        // System prompt ALWAYS included (must match training format exactly)
        string prompt = $"{_systemPrompt}\n\n";

        // ### Input: is ALWAYS present — combines skeleton + manual input + chained response
        var inputParts = new List<string>();

        // 1) Actor skeleton — only on the FIRST prompt of the session
        if (_useSkeleton && !_skeletonAlreadySent && !string.IsNullOrEmpty(_skeletonText))
        {
            inputParts.Add(
                "JSON SCHEMA REFERENCE (field names and types only):\n"
                + _skeletonText + "\n"
                + "RULES:\n"
                + "- ONLY include fields the user explicitly mentioned or that are necessary for the request.\n"
                + "- Do NOT include fields the user did not ask for.\n"
                + "- Use proper values (not the type placeholders).\n"
                + "- Script rules: each object in the Script array must have \"When\" (list of conditions) and \"Do\" (list of actions), properly indented.\n"
                + "- Output valid JSON with 4-space indentation.");
            _skeletonAlreadySent = true;
        }

        // 2) Manual / external InputContext
        if (!string.IsNullOrEmpty(InputContext))
            inputParts.Add(InputContext);

        // 3) Chained last response — framed as editable previous output
        bool hasChainedResponse = _chainLastResponse
            && !string.IsNullOrEmpty(_lastAIResponse)
            && _lastAIResponse != "Sistema listo."
            && _lastAIResponse != "Memoria limpia. Puedes empezar de nuevo.";

        if (hasChainedResponse)
        {
            inputParts.Add(
                "Previous output (apply the instruction's changes to this, do NOT copy it unchanged):\n"
                + _lastAIResponse);

            // Reinforce the instruction to modify
            prompt += $"### Instruction:\n{userMessage}\nIMPORTANT: You MUST apply the requested changes to the previous output below. Do NOT return it unchanged.\n\n";
        }
        else
        {
            prompt += $"### Instruction:\n{userMessage}\n\n";
        }

        prompt += $"### Input:\n{string.Join("\n\n", inputParts)}\n\n";

        prompt += "### Response:\n";

        return prompt;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private string CleanResponse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        string cleaned = raw;
        foreach (var ap in AntiPrompts)
        {
            int idx = cleaned.IndexOf(ap, StringComparison.Ordinal);
            if (idx >= 0) cleaned = cleaned.Substring(0, idx);
        }

        // Trim trailing repeated fragment (cleanup after loop detection cut)
        cleaned = TrimTrailingRepetition(cleaned);

        // Extract and store think content, then strip tags
        string thinkContent = GameJsonAuditor.ExtractThinkContent(cleaned);
        if (!string.IsNullOrEmpty(thinkContent))
            _lastThinkContent = thinkContent;
        else
            _lastThinkContent = "";
        cleaned = GameJsonAuditor.StripThinkTags(cleaned);

        return cleaned.Trim();
    }

    /// <summary>
    /// Detects if the generated text is stuck in a repetition loop.
    /// Checks whether the last `windowSize` characters repeat `maxRepeats` times.
    /// </summary>
    private static bool DetectRepetitionLoop(string text, int windowSize, int maxRepeats)
    {
        if (string.IsNullOrEmpty(text)) return false;

        int minLen = windowSize * maxRepeats;
        if (text.Length < minLen) return false;

        // Try pattern lengths from windowSize down to 4 characters
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

            if (count >= maxRepeats)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a trailing repeated fragment from the response text.
    /// </summary>
    private static string TrimTrailingRepetition(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 20) return text;

        // Check for repeated suffixes of various lengths
        for (int patLen = 6; patLen <= text.Length / 3; patLen++)
        {
            string pattern = text.Substring(text.Length - patLen);
            int lastGoodEnd = text.Length;

            // Walk backwards and remove repeated copies of the pattern
            int pos = text.Length - patLen;
            int repeats = 0;
            while (pos >= 0 && text.Substring(pos, patLen) == pattern)
            {
                repeats++;
                pos -= patLen;
            }

            if (repeats >= 2)
            {
                // Keep one copy of the pattern
                return text.Substring(0, pos + patLen + patLen).TrimEnd();
            }
        }

        return text;
    }

    private void UpdateLastHistoryMessage(string message)
    {
        if (_chatHistory.Count > 0)
            _chatHistory[_chatHistory.Count - 1] = message;
        Repaint();
    }

    private void ResetChat()
    {
        if (!_isModelLoaded || _isGenerating) return;

        // Dispose and recreate context (simple reset)
        try
        {
            _context?.Dispose();
            _context  = _model.CreateContext(_parameters);
            _executor = new InteractiveExecutor(_context);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LLamaTool] Error resetting context: {ex}");
        }

        _chatHistory.Clear();
        _chatHistory.Add("Sistema: Memoria limpia. Puedes empezar de nuevo.");
        _lastAIResponse = "";
        _skeletonAlreadySent = false;
        _statusMessage  = "Chat reiniciado.";
        Repaint();
    }

    private void UnloadModel()
    {
        _context?.Dispose();
        _model?.Dispose();

        _context       = null;
        _executor      = null;
        _model         = null;
        _parameters    = null;
        _isModelLoaded = false;
        _isGenerating  = false;

        _chatHistory.Clear();
        _lastAIResponse = "";
        _statusMessage  = "Modelo descargado.";
        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    //  SKELETON LOADING
    // ══════════════════════════════════════════════════════════

    /// <summary>Try to auto-load actor_skeleton.json from Assets/Editor/.</summary>
    private void AutoLoadSkeleton()
    {
        if (!string.IsNullOrEmpty(_skeletonText)) return; // already loaded

        string defaultPath = Path.Combine(Application.dataPath, "Editor", "actor_skeleton.json");
        if (File.Exists(defaultPath))
        {
            _skeletonPath = defaultPath;
            LoadSkeleton();
        }
    }

    private void LoadSkeleton()
    {
        if (string.IsNullOrEmpty(_skeletonPath)) return;

        try
        {
            if (File.Exists(_skeletonPath))
            {
                _skeletonText = File.ReadAllText(_skeletonPath);
                _skeletonFileName = Path.GetFileName(_skeletonPath);
                _statusMessage = $"Esqueleto cargado: {_skeletonFileName}";
            }
            else
            {
                _statusMessage = $"Esqueleto no encontrado: {_skeletonPath}";
                _skeletonFileName = "(ninguno)";
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error esqueleto: {ex.Message}";
            _skeletonFileName = "(error)";
        }

        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    //  GRAMMAR LOADING
    // ══════════════════════════════════════════════════════════

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
                _statusMessage = $"Archivo no encontrado: {_grammarPath}";
                _grammarFileName = "(ninguno)";
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error gramática: {ex.Message}";
            _grammarFileName = "(error)";
        }

        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        GUILayout.Label("LLama Tool", EditorStyles.boldLabel);
        GUILayout.Label(_statusMessage, EditorStyles.helpBox);

        // ── Model selector ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Modelo", GUILayout.Width(50));
        GUI.enabled = !_isModelLoaded && !_isGenerating;
        if (_availableModelNames.Length > 0)
            _selectedModelIndex = EditorGUILayout.Popup(_selectedModelIndex, _availableModelNames);
        else
            EditorGUILayout.LabelField("(no hay modelos .gguf)");
        GUI.enabled = true;
        if (GUILayout.Button("↻", GUILayout.Width(25), GUILayout.Height(18)))
            RefreshModelList();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // ── Model controls ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Cargar Modelo", GUILayout.Height(30)))
            InitModel();
        if (GUILayout.Button("Descargar Modelo", GUILayout.Height(30)))
            UnloadModel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // ── Settings ──
        _temperature = EditorGUILayout.Slider("Temperatura", _temperature, 0f, 2f);
        _maxTokens   = EditorGUILayout.IntSlider("Max Tokens", _maxTokens, 64, 4096);

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Penalización por Repetición", EditorStyles.boldLabel);
        _repeatPenalty    = EditorGUILayout.Slider("Repeat Penalty", _repeatPenalty, 1.0f, 2.0f);
        _frequencyPenalty = EditorGUILayout.Slider("Frequency Penalty", _frequencyPenalty, 0f, 1f);
        _presencePenalty  = EditorGUILayout.Slider("Presence Penalty", _presencePenalty, 0f, 1f);
        _penaltyCount     = EditorGUILayout.IntSlider("Penalty Window", _penaltyCount, 16, 512);

        EditorGUILayout.Space(3);
        _chainLastResponse = EditorGUILayout.Toggle("Encadenar última respuesta", _chainLastResponse);

        // ── Input Context (editable manually, also settable from code) ──
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Input Context (### Input)");
        InputContext = EditorGUILayout.TextArea(InputContext ?? "", GUILayout.Height(45));

        EditorGUILayout.Space(3);
        _systemPrompt = EditorGUILayout.TextField("System Prompt", _systemPrompt);

        // ── Actor Skeleton ──
        EditorGUILayout.Space(5);
        _useSkeleton = EditorGUILayout.Toggle("Inyectar Esqueleto Actor", _useSkeleton);
        if (_useSkeleton)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Esqueleto:", _skeletonFileName, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Seleccionar…", GUILayout.Width(90)))
            {
                string startDir = string.IsNullOrEmpty(_skeletonPath)
                    ? Application.dataPath
                    : Path.GetDirectoryName(_skeletonPath);
                string picked = EditorUtility.OpenFilePanel("Seleccionar esqueleto JSON", startDir, "json");
                if (!string.IsNullOrEmpty(picked))
                {
                    _skeletonPath = picked;
                    LoadSkeleton();
                }
            }
            if (!string.IsNullOrEmpty(_skeletonText) && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                _skeletonText = "";
                _skeletonPath = "";
                _skeletonFileName = "(ninguno)";
                _statusMessage = "Esqueleto eliminado.";
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Grammar ──
        EditorGUILayout.Space(5);
        _useGrammar = EditorGUILayout.Toggle("Usar Gramática GBNF", _useGrammar);
        if (_useGrammar)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gramática:", _grammarFileName, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Seleccionar…", GUILayout.Width(90)))
            {
                string startDir = string.IsNullOrEmpty(_grammarPath)
                    ? Application.dataPath
                    : Path.GetDirectoryName(_grammarPath);
                string picked = EditorUtility.OpenFilePanel("Seleccionar archivo GBNF", startDir, "gbnf");
                if (!string.IsNullOrEmpty(picked))
                {
                    _grammarPath = picked;
                    LoadGrammar();
                }
            }
            if (!string.IsNullOrEmpty(_grammarText) && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                _grammarText = "";
                _grammarPath = "";
                _grammarFileName = "(ninguno)";
                _statusMessage = "Gramática eliminada.";
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // ── Think Reasoning Toggle ──
        EditorGUILayout.BeginHorizontal();
        Color defaultBg = GUI.backgroundColor;
        GUI.backgroundColor = _showThinkReasoning ? new Color(0.6f, 0.4f, 1f) : defaultBg;
        string thinkLabel = _showThinkReasoning ? "▼ Razonamiento" : "▶ Razonamiento";
        if (GUILayout.Button(thinkLabel, EditorStyles.miniButton, GUILayout.Width(110)))
            _showThinkReasoning = !_showThinkReasoning;
        GUI.backgroundColor = defaultBg;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ── Think reasoning panel ──
        if (_showThinkReasoning)
        {
            if (string.IsNullOrEmpty(_lastThinkContent))
            {
                EditorGUILayout.HelpBox(
                    "El modelo no generó razonamiento (<think>) en la última respuesta, " +
                    "o el modelo utilizado no soporta esta función.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUIStyle headerStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold
                };
                headerStyle.normal.textColor = new Color(0.8f, 0.6f, 1f);
                GUILayout.Label("💡 Razonamiento del modelo:", headerStyle);

                GUIStyle thinkStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize  = 10,
                    fontStyle = FontStyle.Italic,
                    padding   = new RectOffset(6, 6, 4, 4)
                };
                thinkStyle.normal.textColor = new Color(0.7f, 0.55f, 1f);
                GUILayout.Label(_lastThinkContent, thinkStyle);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(5);

        // ── Chat history ──
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
        foreach (var msg in _chatHistory)
        {
            GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
            if (msg.StartsWith("User:"))
                style.normal.textColor = new Color(0.3f, 0.7f, 1f);
            else if (msg.StartsWith("AI:"))
                style.normal.textColor = new Color(0.4f, 1f, 0.4f);
            else
                style.normal.textColor = Color.yellow;

            GUILayout.Label(msg, style);
        }
        EditorGUILayout.EndScrollView();

        // ── Input area ──
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        _userInput = EditorGUILayout.TextField(_userInput, GUILayout.Height(25));

        GUI.enabled = _isModelLoaded && !_isGenerating;
        if (GUILayout.Button("Enviar", GUILayout.Width(70), GUILayout.Height(25)))
            SendMessageToAI();

        if (GUILayout.Button("Reset", GUILayout.Width(55), GUILayout.Height(25)))
            ResetChat();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        // Send on Enter key
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && _isModelLoaded && !_isGenerating && !string.IsNullOrEmpty(_userInput))
        {
            SendMessageToAI();
            Event.current.Use();
        }
    }

    private void OnDestroy()
    {
        // Don't unload model on window close — keep it in memory for quick reopen
    }
}
