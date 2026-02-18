using UnityEngine;
using UnityEditor;
using System; // <--- FALTABA ESTO
using System.Runtime.InteropServices;
using System.IO;

public class DllDebug : EditorWindow
{
    [MenuItem("Tools/Debug Llama DLL")]
    public static void CheckDll()
    {
        // 1. Ruta a tu DLL (Asegúrate de que la carpeta sea Plugins/x86_64)
        string path = Path.Combine(Application.dataPath, "Plugins/x86_64/libllama.dll");
        path = path.Replace("/", "\\"); 

        if (!File.Exists(path))
        {
            Debug.LogError($"[Debug] El archivo no existe en: {path}");
            return;
        }

        Debug.Log($"[Debug] Intentando cargar DLL desde: {path}");

        // 2. Intentamos cargarla manualmente
        IntPtr handle = LoadLibrary(path);

        if (handle == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Debug.LogError($"[CRÍTICO] Falló la carga. Código de error Windows: {errorCode}");
            
            if (errorCode == 126) 
            {
                Debug.LogError("Error 126 (Module not found): TE FALTAN DEPENDENCIAS.");
                Debug.LogError("Solución: Ve a la carpeta bin de CUDA 13 y copia 'cudart64_13.dll' y 'cublas64_13.dll' a Assets/Plugins/x86_64.");
            }
            else if (errorCode == 193) 
            {
                Debug.LogError("Error 193 (Bad Image): Mezcla de 32 y 64 bits. Unity Editor es 64 bits, tu DLL debe ser x64.");
            }
        }
        else
        {
            // Si carga, liberamos la memoria inmediatamente
            Debug.Log($"<color=green>[ÉXITO] La DLL 'libllama.dll' y sus dependencias (CUDA) se cargaron correctamente.</color>");
            FreeLibrary(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr hModule);
}