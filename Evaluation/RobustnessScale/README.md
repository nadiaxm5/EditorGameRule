# Evaluación reproducible de importación y escala

Código de producción de partida: `c58f222091dbb642797006045ded9c226309fc3f`, rama original `EditorDeterminista`. Trabajo en `major-revision/robustness-scale-evaluation`. No se modifica el manuscrito ni la implementación de producción. Los resultados son observaciones, no pruebas que exijan que los seis descriptores inválidos sean rechazados.

## Inspección del sistema real

- Unity: **6000.3.1f1 (37142da19a94)**, `ProjectSettings/ProjectVersion.txt`.
- UI Toolkit: integrado en esa versión del Editor; `com.unity.modules.uielements` **1.0.0** es la versión del módulo declarado, no una versión independiente de UI Toolkit. No hay paquete UI Toolkit separado.
- Unity Visual Scripting: **1.9.9**, manifest y lock. Esta importación usa clases C# y UI Toolkit, no grafos de Visual Scripting.
- Test Framework **1.6.0**, NUnit **2.0.3** transitivos en `Packages/packages-lock.json`. No hay tests ni asmdefs de pruebas versionados en el commit inicial. Se añade un runner de Editor al ensamblado existente: [RobustnessScaleEvaluation.cs](../../Assets/Editor/Evaluation/RobustnessScaleEvaluation.cs). No se cambia la estructura de ensamblados para acceder a clases de producción.

| Operación | Código de producción y comportamiento |
|---|---|
| Menú de importación | `GameRuleTopMenuActions.ImportJson` elige una ruta y llama `ProjectController.ImportJsonAsProject` sin `try/catch`. El runner omite exclusivamente el selector de archivo. |
| Lectura, JSON, deserialización | `GameRuleProject.ImportFromJson`: `File.ReadAllText`, `JsonUtility.FromJson<SceneJson>`; parsing y deserialización son una sola llamada, sin fases observables independientes. |
| Normalización | El mismo método rellena arrays de escena ausentes/cortos, listas de variables, Cast, When/Do, Properties y Components. No es validación de referencias, acciones o tipos. |
| Persistencia y sustitución del proyecto | `ImportJsonAsProject` crea un asset con ruta única, guarda assets y llama `ProjectController.LoadProject` -> `EditorContext.LoadProject`. Este asigna `currentProject`, restablece índices y emite `OnProjectLoaded`. El proyecto anterior es un `GameRuleProject : ScriptableObject` separado; mantener su asset no equivale a mantenerlo como proyecto activo. |
| Jerarquía visual | `GameRuleHierarchyWindow.Init`, `Rebuild`, `RefreshList`, `CreateActorItem`, suscripciones a `EditorContext`. |
| Reglas visuales | `EditorContext.SelectActor`, `GameRuleRulesWindow.Init` / `OnActorSelected` / `BuildUI`, `ScriptEditorPanel.UpdateRulesList` / `CreateRuleElement`. Reconstrucción por actor; se crean controles de todas sus reglas, inicialmente plegadas. |
| Condiciones/acciones | `ConditionBuilder`, `ConditionElement.SetFromSource`, `ActionBuilder.SetActions`, `ActionElement.SetFromSource`, `GameRuleParser.ParseFunction`. Los tipos de los menús se obtienen por reflexión de `Condition` y `Action`. Los parámetros usan campos de texto; no hay validación semántica general en la importación. |
| Validación explícita posterior | `GameRuleProject.Validate` comprueba GameName obligatorio, nombres de actor duplicados y presencia/existencia de prefab. **Importar no llama a este método.** El runner lo ejecuta después y guarda sus mensajes separados; no los atribuye al diagnóstico natural de importación. |
| Exportación | `ProjectController.SaveProjectToJson` -> `GameRuleProject.SaveToJsonFile` -> `ExportToJson`. Sincroniza `sceneData.Cast` desde `actors`, normaliza When temporalmente, usa JsonUtility, elimina arrays vacíos/ceros mediante regex y escribe campos de escena manualmente. Se guardan snapshots antes y después de exportar para detectar sus efectos. |
| Generación posterior | `ProjectController.GenerateScene` exporta a Resources/Games y llama `Loader.LoadJson`. Loader deserializa, normaliza, conserva el orden de declaración para el planificador, invierte Cast para instanciar, crea escena/tags/prefabs, reemplaza Resources/Scripts y llama `Scripts.CreateGameManager` / `Scripts.Create`. `AssetDatabase.Refresh` recompila; `Loader.OnScriptsReloaded` adjunta componentes (hasta tres reintentos). |
| Evaluación de expresiones en ejecución | `Action.Edit` usa `Parser.ParseNumber`, acceso a scope y `Utils.SetProperty`. `Utils.CreateScope` y `GetProperty` resuelven propiedades de objetos. Son operaciones posteriores, no validadores de importación. |

La generación de escenas y Play **no se ejecutan en esta suite de importación/escala del editor**. Por tanto, no se atribuyen errores de compilación o ejecución a entradas aceptadas sin haberlos observado. Tampoco se sustituye esa generación por un parser aislado. No se miden el guardado de escenas, scripts generados ni rendimiento del juego.

## Entradas y criterios fijados

[inputs.json](inputs.json) contiene ruta, mutación, campo, motivo y SHA-256. Base: dos actores, dos reglas por actor, prefab existente `Empty`, propiedad `Health=100`, `Compare(this.Health > 0)` y `Edit(EvalActorNNN.Health,n)`. Actores y reglas conservan el orden de sus listas; reglas identificadas por `(ActorName, Name)`, agrupadas por `Components.id` / `SentenceJson.groupId`.

| Caso | Única mutación principal respecto a la base |
|---|---|
| malformed_json | Elimina el último `}` raíz. |
| missing_required | Elimina `GameName`, obligatorio según `GameRuleProject.Validate`. Su ausencia no se presupone rechazada por Import. |
| missing_actor | Primer destino de Edit: `EvalActor000.Health` -> `MissingActor.Health`. |
| missing_property | Primer destino: `EvalActor000.Health` -> `EvalActor000.MissingProperty`. |
| unsupported_action | Primera función `Edit` -> `NoSuchAction`, inexistente en `Action`. |
| invalid_parameter | Primera expresión numérica `1` -> `)`, dando `Edit(EvalActor000.Health,))`. Un paréntesis de cierre aislado no es una expresión numérica. |

Antes de cada caso inválido se importa la base con el controlador real y ventanas conectadas. Se captura el objeto activo, actores completos, propiedades, reglas, orden y JSON exportado antes/después. `previous_state_preserved` exige **el mismo proyecto activo y el mismo contenido**, mientras `previous_asset_content_preserved` comprueba por separado el asset anterior. La clasificación distingue instalación completa, instalación con discrepancia visual, no instalación, excepción escapada, mensajes de Console y validación posterior. Una discrepancia detectada por el oráculo no cuenta como diagnóstico de producción.

El gate inicial exige importación, reconstrucción de ambos actores, exportación, conservación del contenido de entrada y round-trip de la base. No se alteran expectativas para que un fallo pase. La igualdad canónica ordena claves de objeto y normaliza números, **nunca reordena arrays**. Se permite añadir defaults; las claves y valores originales deben conservarse, salvo listas vacías que el exportador omite. Se compara además exportación -> importación de producción -> exportación canónica.

## Medición y final de las operaciones

`Stopwatch` de C#, unidades de milisegundos. Una suscripción a `OnProjectLoaded`, anterior a la ventana de jerarquía, marca la frontera real entre importación/persistencia y reconstrucción:

- `read_process_persist_ms`: desde entrada en la operación hasta esa notificación. Incluye lectura, JsonUtility, defaults, CreateAsset, SaveAssets y carga del modelo. No puede separarse lectura/parsing sin instrumentar producción; no se publica una separación ficticia.
- `visual_rebuild_ms`: desde esa notificación hasta terminar jerarquía y **visitar serialmente todos los actores** con la ventana real de reglas. Incluye controles, estilo/layout y esperas de Editor. No significa que producción muestre simultáneamente 4.000 reglas.
- `export_ms`: `SaveProjectToJson`, incluida escritura a disco.
- `total_ms`: desde comienzo de importación hasta fin de exportación. Excluye setup/limpieza, snapshots finales, Validate, round-trip y comprobación adicional del panel largo. Incluye una pequeña sobrecarga del observador y checkpoints de disco; no son tiempos instrumentados con cero sobrecarga. `import_call_ms` y `hierarchy_sync_ms` son diagnósticos adicionales, solapados con esas fases y no deben sumarse otra vez.

Cada espera exige panel adjunto, geometría finita y no nula, callback programado de UI Toolkit y al menos tres actualizaciones consecutivas con geometría estable (raíz y altura de contenido del ScrollView). Se solicita Repaint. Esto acredita final del trabajo síncrono y asentamiento de layout/callbacks, **no mide presentación GPU ni latencia humana**. Se conservan nombres de controles y datos para verificar que no hay omisiones, duplicados o reordenaciones. Tamaños: jerarquía 360x720, reglas 1000x720 puntos; escala DPI registrada.

Configuraciones: 10x10, 50x20, 100x20, 200x20, 1x100. Sin aleatoriedad. Una repetición 0 de calentamiento por configuración (conservada, excluida del resumen), después cinco medidas. Contexto, ventanas y assets importados nuevos en cada repetición, limpieza únicamente de assets creados y cargados por el runner. Cachés de SO/Unity se mantienen; no se fuerza GC, no se activa sincronización escena->datos del controlador porque aquí no se evalúa una escena. Cerrar otros editores/cargas de trabajo y no editar durante el ensayo.

Cuartiles: interpolación lineal **tipo 7**. Con cinco observaciones ordenadas, Q1=x2, mediana=x3, Q3=x4, IQR=x4-x2. No se descartan outliers. Si falta una repetición, hay timeout o error, el grupo conserva los valores disponibles y queda incompleto, sin inventar estadísticas de cinco observaciones.

Timeout de layout: 15 s; límite cooperativo por muestra: 600 s; watchdog externo: 660 s por muestra, 300 s de arranque. El watchdog es necesario porque un bloqueo del hilo principal impide ejecutar callbacks de Editor. El timeout queda en `launcher.json` y en los resúmenes; las muestras no alcanzadas quedan pendientes. El menú solo tiene límites cooperativos, **usar el launcher para disponer de protección frente a bloqueo síncrono**.

## Ejecución exacta

Requisitos: este checkout, Unity **6000.3.1f1** con licencia activa y dependencias restauradas, sesión gráfica Windows y Python 3 (solo biblioteca estándar). Cerrar otras instancias de Unity que usen este proyecto. Desde la raíz del repositorio, PowerShell:

```powershell
python Evaluation/RobustnessScale/scale/generator/generate.py
python Evaluation/RobustnessScale/test-code/test_tools.py
& Evaluation/RobustnessScale/test-code/run.ps1 -Unity 'C:\Program Files\Unity\Hub\Editor\6000.3.1f1\Editor\Unity.exe'
# Copiar el identificador que imprime el launcher:
python Evaluation/RobustnessScale/test-code/summarize.py IDENTIFICADOR_DE_EJECUCION
python Evaluation/RobustnessScale/test-code/audit.py IDENTIFICADOR_DE_EJECUCION
```

El launcher usa gráficos y `-executeMethod GameRuleEvaluation.RobustnessScaleEvaluation.RunCommandLine`, sin `-batchmode`, `-nographics` ni `-quit`. Inicia la ventana de forma oculta; Unity gestiona las ventanas de Editor necesarias. El propio runner sale al acabar. Las ventanas efectivamente adjuntas y su geometría se verifican; una sesión sin layout queda como timeout, no como medición visual válida.

Alternativa interactiva: abrir Unity Hub -> este proyecto -> esperar a compilar -> **GameRule > Evaluation > Run robustness and scale**. No abrir Play. Esperar al mensaje `GameRule evaluation artifacts: ...`, anotar su identificador y ejecutar `summarize.py`. Guardar previamente el trabajo del usuario; el runner emplea un contexto separado, no cambia el proyecto activo guardado, y cierra sus propias ventanas al finalizar.

La suite registra entorno automáticamente. En la ejecución por menú y con cambios sin commit, `git_dirty` y hashes de todos los `.cs` de producción permiten identificar el código. Para resultados citables, ejecutar desde un commit limpio y conservar `environment.json`, `inputs.json`, `launcher.json`, JSON brutos y exportaciones. El runner solo sobrescribe `progress.json`; un ID nuevo es obligatorio, y los JSON brutos jamás se sobrescriben al resumir.

## Artefactos

- `robustness/baseline`, `robustness/invalid-inputs`: siete entradas, incluida base.
- `scale/generator/generate.py`, `scale/descriptors`: generación y cinco entradas versionadas.
- `robustness/raw-results/<run>`, `scale/raw-results/<run>`: observaciones completas, incluyendo calentamientos, snapshots y logs.
- `runs/<run>`: entorno, manifiesto, exportaciones, progreso, salida del launcher y diagnóstico saneado.
- `runs/<run>/summary`: `results.json`, `robustness.csv`, `scale-raw.csv`, `scale-statistics.csv`, `report.md`; resumen unido para evitar duplicación.
- `robustness/summary` y `scale/summary`: índices hacia el resumen unido.
- `.work`: logs privados completos y compilación temporal, ignorados por Git. Los logs Unity pueden contener rutas, hostname e identificadores de licencia; solo se versiona evidencia saneada.
- `environment.json`, `REPORT.md`: entorno e informe de esta entrega.
- [manual-panel-checklist.md](manual-panel-checklist.md): comprobaciones humanas aún sin completar salvo evidencia explícita.

Si se identifica un fallo de producción se conserva la ejecución original. Esta entrega no añade validadores ni corrige automáticamente aceptación silenciosa. Cualquier corrección futura debe ir en commit y ejecución separados.
