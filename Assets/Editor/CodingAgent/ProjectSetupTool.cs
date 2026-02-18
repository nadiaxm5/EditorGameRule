using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ProjectSetupTool : EditorWindow
{
    // ================= CONFIGURACIÓN =================
    // Tu Token (Recuerda no compartirlo públicamente si es un repo open source)
    private const string HF_TOKEN = "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"; 

    // URL Base (Asegúrate que termina en /resolve/main/)
    private const string REPO_BASE_URL = "https://huggingface.co/Jayoru/Qwen2.5_3B_FineGameRule750_InducReasoning/resolve/main/";
    // =================================================

    private struct SetupFile
    {
        public string RemotePath;
        public string LocalPath;
    }

    // LISTA DE ARCHIVOS (Igual que antes)
    private readonly List<SetupFile> files = new List<SetupFile>
    {
        // DLLs
        new SetupFile { RemotePath = "Plugins/x86_64/cublas64_13.dll",   LocalPath = "Assets/Plugins/x86_64/cublas64_13.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/cublasLt64_13.dll", LocalPath = "Assets/Plugins/x86_64/cublasLt64_13.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/cudart64_13.dll",   LocalPath = "Assets/Plugins/x86_64/cudart64_13.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/curand64_10.dll",   LocalPath = "Assets/Plugins/x86_64/curand64_10.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/ggml-base.dll",     LocalPath = "Assets/Plugins/x86_64/ggml-base.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/ggml-cpu.dll",      LocalPath = "Assets/Plugins/x86_64/ggml-cpu.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/ggml-cuda.dll",     LocalPath = "Assets/Plugins/x86_64/ggml-cuda.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/ggml.dll",          LocalPath = "Assets/Plugins/x86_64/ggml.dll" },
        new SetupFile { RemotePath = "Plugins/x86_64/libllama.dll",      LocalPath = "Assets/Plugins/x86_64/libllama.dll" },

        // Modelos
        new SetupFile { RemotePath = "StreamingAssets/gamerule4.gbnf",               LocalPath = "Assets/StreamingAssets/gamerule4.gbnf" },
        new SetupFile { RemotePath = "StreamingAssets/gamerule_model750-q5_k_m.gguf", LocalPath = "Assets/StreamingAssets/gamerule_model750-q5_k_m.gguf" }
    };

    [MenuItem("Tools/Instalar Dependencias del Proyecto")]
    public static void ShowWindow()
    {
        GetWindow<ProjectSetupTool>("Setup IA");
    }

    private void OnGUI()
    {
        GUILayout.Label("Instalador de Dependencias", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        int missing = CountMissingFiles();

        if (missing == 0)
        {
            EditorGUILayout.HelpBox("✅ Todo instalado correctamente.", MessageType.Info);
            GUILayout.Space(5);
            if (GUILayout.Button("Forzar Re-instalación (Reparar)")) StartDownloadProcess();
        }
        else
        {
            EditorGUILayout.HelpBox($"⚠️ Faltan {missing} archivos necesarios.", MessageType.Warning);
            GUILayout.Space(10);
            if (GUILayout.Button($"Descargar e Instalar ({missing} archivos)")) StartDownloadProcess();
        }
    }

    private int CountMissingFiles()
    {
        int count = 0;
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        foreach (var f in files) if (!File.Exists(Path.Combine(root, f.LocalPath))) count++;
        return count;
    }

    private async void StartDownloadProcess()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        
        // Configurar GitIgnore antes de empezar
        UpdateGitIgnore(root);

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                string fullPath = Path.Combine(root, file.LocalPath);
                string fileName = Path.GetFileName(file.LocalPath);

                // Si ya existe, saltar (a menos que quieras forzar siempre)
                if (File.Exists(fullPath)) continue;

                // Crear directorio
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                // Descargar con UI
                string url = REPO_BASE_URL + System.Uri.EscapeUriString(file.RemotePath);
                await DownloadFileWithUI(url, fullPath, fileName, i + 1, files.Count);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error durante la instalación: " + ex.Message);
            EditorUtility.DisplayDialog("Error", "Hubo un fallo en la descarga. Mira la consola.", "Ok");
        }
        finally
        {
            // SIEMPRE quitar la barra de carga al terminar o fallar
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    // Esta es la función mágica que muestra la barra
    private async Task DownloadFileWithUI(string url, string savePath, string fileName, int currentFileIndex, int totalFiles)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("Authorization", $"Bearer {HF_TOKEN}");
            webRequest.downloadHandler = new DownloadHandlerFile(savePath) { removeFileOnAbort = true };
            
            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                // Calculamos porcentaje (0 a 1)
                float progress = operation.progress;
                
                // Formateamos el mensaje: "Descargando modelo.gguf (45%)"
                string title = $"Instalando archivo {currentFileIndex} de {totalFiles}";
                string info = $"Bajando: {fileName}\nProgreso: {progress * 100:F1}%";

                // MOSTRAR BARRA DE PROGRESO (Bloqueante pero actualizable)
                // Si el usuario le da a "Cancelar" en la ventana emergente, paramos.
                if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                {
                    webRequest.Abort();
                    throw new System.Exception("Descarga cancelada por el usuario.");
                }

                // Esperar un frame para que la UI se actualice
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new System.Exception($"Fallo al descargar {fileName}: {webRequest.error}");
            }
        }
    }

    private void UpdateGitIgnore(string projectRoot)
    {
        // ... (Mismo código de gitignore de antes, mantenlo aquí) ...
        // Lo omito para ahorrar espacio, pero usa el UpdateGitIgnore de la respuesta anterior
        string gitIgnorePath = Path.Combine(projectRoot, ".gitignore");
        if (!File.Exists(gitIgnorePath)) File.WriteAllText(gitIgnorePath, "");
        
        var lines = new List<string>(File.ReadAllLines(gitIgnorePath));
        bool modified = false;

        foreach (var file in files)
        {
            string entry = "/" + file.LocalPath.Replace("\\", "/");
            if (!lines.Contains(entry))
            {
                lines.Add(entry);
                modified = true;
            }
        }

        if (modified) File.WriteAllLines(gitIgnorePath, lines.ToArray());
    }
}