# GameRule Editor

Un editor visual integrado en Unity para crear, editar y gestionar proyectos de juegos basados en reglas (game rules). Permite a los diseñadores de juegos definir actores, propiedades de escena, comportamientos condicionales (when-do) y variables personalizadas sin escribir código.

**Estado del proyecto:** Desarrollo activo  
**Rama actual:** EditorFinal  
**Versión de Unity:** 2022.3.1f1 (6000.3.1f1)

## Tabla de Contenidos

- [Descripción General](#descripción-general)
- [Arquitectura](#arquitectura)
- [Stack Tecnológico](#stack-tecnológico)
- [Instalación y Configuración](#instalación-y-configuración)
- [Características Principales](#características-principales)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Componentes Clave](#componentes-clave)
- [Flujo de Datos](#flujo-de-datos)
- [Operaciones Disponibles](#operaciones-disponibles)
- [Validación y Exportación](#validación-y-exportación)
- [Sistema de Deshacer/Rehacer](#sistema-de-deshacer-rehacer)
- [Decisiones Arquitectónicas](#decisiones-arquitectónicas)
- [Desarrollo Futuro](#desarrollo-futuro)

## Descripción General

GameRule Editor es una herramienta de edición visual que funciona como una extensión del editor de Unity. Proporciona una interfaz gráfica intuitiva para:

1. **Gestionar Proyectos:** Crear, cargar, guardar e importar/exportar proyectos basados en JSON.
2. **Administrar Actores:** Agregar actores (personajes, objetos), asignar prefabs, configurar propiedades físicas y de transformación.
3. **Definir Reglas de Comportamiento:** Crear reglas condicionales mediante un sistema visual "cuando-entonces" (when-do).
4. **Configurar Escenas:** Editar parámetros globales como resolución, cámara, iluminación, gravedad y variables personalizadas.
5. **Sincronizar Escenas:** Generar escenas de Unity a partir de los datos del proyecto y sincronizar cambios en tiempo real.

## Arquitectura

### Vista General del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                    GAMERULETOOLBAR (Legacy)                 │
│                                                               │
│  GameRuleLayoutManager (Punto de entrada único)              │
│  ├─ Crea/gestiona ProjectController singleton               │
│  └─ Abre ventanas UI con contexto compartido                │
└──────────────┬──────────────────────────────────────────────┘
               │
               ├─────────────────────────────────────────────────┐
               │                                                 │
        ┌──────▼──────┐  ┌──────────────┐  ┌──────────────┐   │
        │  Hierarchy   │  │   Scene      │  │  Properties  │   │
        │   Window     │  │ Settings     │  │   Window     │   │
        │  (Cast)      │  │   Window     │  │              │   │
        └──┬────────┬──┘  └──────┬───────┘  └──────┬───────┘   │
           │        │           │                  │           │
     ┌─────▼─────┐  │     ┌─────▼──────┐    ┌─────▼────────┐   │
     │   Actor   │  │     │   Scene    │    │    Rules     │   │
     │List Panel │  │     │ Settings   │    │   Window     │   │
     │           │  │     │   Panel    │    │              │   │
     └───────────┘  │     └────────────┘    └──────────────┘   │
                    │                                           │
        ┌───────────▼──────────────┐                           │
        │   Actor Details Panel     │                           │
        │  (Propiedades seleccionadas)                          │
        └──────────────────────────┘                           │
               │
               └──────────────────────────────────────────────┐
                                                              │
                         ┌───────────────────────────────────▼┐
                         │     EditorContext (ScriptableObject)│
                         │  (Estado global compartido)         │
                         │  - currentProject                   │
                         │  - selectedActorIndex               │
                         │  - selectedScriptIndex              │
                         │  - activeInspectorMode              │
                         │  - Eventos de sincronización        │
                         └────────────┬──────────────────────┘
                                      │
                         ┌────────────▼──────────────────┐
                         │  ProjectController            │
                         │  (Lógica de negocio)          │
                         │  - Operaciones CRUD           │
                         │  - Undo/Redo                  │
                         │  - Sincronización escena      │
                         └────────────┬──────────────────┘
                                      │
                         ┌────────────▼──────────────────┐
                         │  GameRuleProject              │
                         │  (Modelo de datos)            │
                         │  - SceneJson (configuración)  │
                         │  - List<ActorJson> (actores)  │
                         │  - CustomVariables            │
                         └────────────┬──────────────────┘
                                      │
                    ┌─────────────────▼────────────────┐
                    │   JSON Export/Import              │
                    │   & Loader (Escena del juego)    │
                    └──────────────────────────────────┘
```

### Patrón de Comunicación

El editor utiliza un **patrón de observador (Observer)** centralizado a través de `EditorContext`:

1. **EditorContext** mantiene el estado global y emite eventos cuando cambia el proyecto, se selecciona un actor, etc.
2. **Todas las ventanas y paneles** se suscriben a estos eventos.
3. **ProjectController** ejecuta la lógica de negocio y notifica al contexto cuando se completan operaciones.

Este diseño asegura que múltiples ventanas simultáneamente abiertas siempre están sincronizadas.

## Stack Tecnológico

| Componente | Versión | Propósito |
|------------|---------|----------|
| Unity Editor | 6000.3.1f1 | Plataforma anfitriona |
| C# | .NET 4.7.1 | Lenguaje de programación |
| UIElements (UI Toolkit) | Latest | Interfaz gráfica de las ventanas |
| JsonUtility | Built-in | Serialización de datos |
| Regex | System.Text.RegularExpressions | Parsing de funciones y condiciones |
| Editor Window API | Unity 6000.3+ | Base para ventanas flotantes |

**Dependencias opcionales (no esenciales):**
- LLamaSharp: Herramientas de auditoría de agentes IA (en Assets/Editor/CodingAgent)
- Mobile Dependency Resolver: Soporte para módulos nativos

## Instalación y Configuración

### Requisitos Previos

- **Unity 2022.3 LTS o superior** (probado con 6000.3.1f1)
- **Windows, macOS o Linux** con soporte para editor de Unity
- **.NET Framework 4.7.1 o superior** en el sistema

### Configuración Inicial

1. **Clonar o descargar el proyecto:**
   ```bash
   git clone <https://github.com/nadiaxm5/EditorGameRule/tree/EditorFinal>
   cd EditorGameRule
   ```

2. **Abrir en Unity:**
   - Abre Unity Hub
   - Selecciona o crea un proyecto con Unity 6000.3.1f1 o compatible
   - Abre la carpeta del proyecto

3. **Estructura de Carpetas Requerida:**
   El proyecto espera la siguiente estructura bajo `Assets/Resources/`:
   ```
   Assets/Resources/
   ├── Prefabs/
   │   ├── Empty.prefab          (Se crea automáticamente si no existe)
   │   ├── GameManager.prefab    (Requerido para generar escenas)
   │   └── [Otros prefabs personalizados]
   └── Games/
       └── [Se crean automáticamente con exportaciones JSON]
   ```

4. **Crear EditorContext:**
   - Clic derecho en la carpeta Assets/Editor/GameRuleEditor/Projects/
   - Crear → GameRule → Editor Context
   - Guardar como `EditorContext.asset`

5. **Abrir el Editor:**
   - En el menú de Unity, ir a **GameRule → Editor**
   - O si tienes ventanas antiguas, **GameRule → Editor Window**

### Variables de Entorno

No se requieren variables de entorno. Toda la configuración se almacena en:
- `Assets/Editor/GameRuleEditor/Projects/EditorContext.asset` (estado del editor)
- `.asset` files del proyecto en Assets/ (proyectos guardados)

## Características Principales

### 1. Gestión de Proyectos

- **Crear nuevo proyecto:** Asigna un nombre y comienza a editar.
- **Cargar proyecto existente:** Abre un `.asset` de GameRuleProject guardado previamente.
- **Importar desde JSON:** Convierte archivos JSON (desde juegos ejecutados) en proyectos editables.
- **Exportar a JSON:** Genera archivo JSON limpio con formato específico para el motor de juego.
- **Validación de proyecto:** Verifica nombres únicos de actores, prefabs disponibles, etc.

### 2. Gestión de Actores

**Crear Actores:**
- Clic en el botón "+" en la ventana Cast (Hierarchy).
- El actor se inicializa automáticamente con el prefab "Empty".

**Editar Propiedades:**
- Nombre del actor
- Prefab asignado
- Tag de Unity
- Activo/Inactivo
- Posición, Rotación, Escala
- Tamaño (para ciertos tipos de objetos)
- Propiedades físicas: Densidad, Fricción, Bounciness, Drag
- Propiedades personalizadas (strings arbitrarios)

**Operaciones:**
- Duplicar actor (copia nombre con sufijo automático)
- Eliminar actor
- Revertir propiedades a valores por defecto
- Reordenar en la lista (drag-and-drop en futuras versiones)

### 3. Sistema de Reglas (When-Do)

Cada actor puede tener múltiples reglas (SentenceJson) que definen comportamientos:

**Estructura de una Regla:**
```
CUANDO (When)      : Lista de condiciones evaluadas
ENTONCES (Do)      : Lista de acciones ejecutadas
Nombre (Name)      : Identificador legible para referencia
GroupId (groupId)  : Vinculación a componentes de grupo (opcional)
```

**Condiciones (When):**
- Se evalúan mediante reflexión contra la clase `Condition` (métodos públicos estáticos que retornan bool)
- Soportan negación (prefijo "!")
- Se combinan con operadores: AND, OR
- Ejemplo: `"Compare(Health, >, 50)"` OR `"!IsOnGround()"`

**Acciones (Do):**
- Se ejecutan mediante reflexión contra la clase `Action` (métodos públicos estáticos sin retorno)
- Procesadas secuencialmente cuando la regla se dispara
- Ejemplo: `"DealDamage(10)"`, `"PlayAnimation(\"Jump\")"`, `"SpawnPrefab(\"Explosion\")"`

### 4. Configuración de Escenas

**Parámetros Básicos:**
- Nombre del juego (GameName)
- Resolución de pantalla

**Cámara:**
- Posición (X, Y, Z)
- Rotación (Pitch, Yaw, Roll)

**Iluminación:**
- Posición del sol
- Rotación del sol
- Color del sol (RGB)
- Color ambiental (RGB)

**Física:**
- Gravedad global (Vector3)

**Fondo:**
- Color de fondo (RGB)

**Variables Personalizadas:**
- Tipos soportados: int, float, bool, Vector2, Vector3
- Se almacenan en escala global y se distribuyen al juego
- Edición visual con validación de tipo

### 5. Sincronización Escena-Editor

El editor mantiene sincronización bidireccional con la escena de Unity:

**Editor → Escena:**
- Los cambios en ActorDetails (posición, rotación, escala, activo, tag) se replican al GameObject en la escena.

**Escena → Editor:**
- Si el desarrollador mueve un GameObject en la escena usando el gizmo, el cambio se detecta automáticamente y se actualiza en los datos.
- Se usa `transform.hasChanged` para detectar cambios eficientemente.

**Generación de Escena:**
- Exporta el proyecto a JSON temporal en `Assets/Resources/Games/`.
- Carga el prefab GameManager desde Resources.
- Instancia todos los prefabs de actores desde el directorio Resources/Prefabs/.
- Configura etiquetas (tags) automáticamente.

### 6. Componentes Reutilizables

**ConditionBuilder:**
- Construye UI para editar condiciones when complejas.
- Soporte para AND/OR/NOT operators.
- Dropdown de tipos disponibles basado en reflexión.

**ActionBuilder:**
- Construye UI para editar acciones do.
- Dropdown de tipos disponibles basado en reflexión.
- Validación de parámetros.

**ConditionElement & ActionElement:**
- Elementos individuales editables dentro de builders.
- Soporte para drag-and-drop en futuras versiones.
- Botones de eliminar integrados.

## Estructura del Proyecto

```
J:/Dev/Projects/EditorGameRule/
│
├── Assets/
│   ├── Editor/
│   │   ├── GameRuleEditor/
│   │   │   ├── Core/
│   │   │   │   ├── EditorContext.cs           (Estado global - ScriptableObject)
│   │   │   │   ├── GameRuleProject.cs         (Modelo de datos del proyecto)
│   │   │   │   ├── GameRuleParser.cs          (Parseo de funciones/condiciones)
│   │   │   │   └── Loader.cs                  (Generador de escenas Unity)
│   │   │   │
│   │   │   ├── Controllers/
│   │   │   │   └── ProjectController.cs       (Lógica de negocio, undo/redo)
│   │   │   │
│   │   │   ├── Windows/
│   │   │   │   ├── GameRuleEditorWindow.cs        (Punto de entrada legacy)
│   │   │   │   ├── GameRuleLayoutManager.cs       (Gestor de ventanas)
│   │   │   │   ├── GameRuleHierarchyWindow.cs     (Panel Cast - lista de actores)
│   │   │   │   ├── GameRuleSceneWindow.cs         (Panel Configuración de escena)
│   │   │   │   ├── GameRulePropertiesWindow.cs    (Panel Propiedades de actor)
│   │   │   │   ├── GameRuleRulesWindow.cs         (Panel Reglas/Scripts)
│   │   │   │   ├── GameRuleToolbarWindow.cs       (Barra de herramientas - legacy)
│   │   │   │   ├── GameRuleTopMenuActions.cs      (Menú superior)
│   │   │   │   ├── GameRuleCodingAgentWindow.cs   (Ventana de agentes IA)
│   │   │   │   ├── PropertyPickerDialog.cs        (Diálogo modal)
│   │   │   │   └── GameRuleInspectorWindow.cs     (Inspector visual)
│   │   │   │
│   │   │   ├── Panels/
│   │   │   │   ├── ActorListPanel.cs         (UI lista de actores)
│   │   │   │   ├── ActorDetailsPanel.cs      (UI propiedades de actor)
│   │   │   │   ├── SceneSettingsPanel.cs     (UI configuración escena)
│   │   │   │   └── ScriptEditorPanel.cs      (Editor visual de reglas)
│   │   │   │
│   │   │   ├── CustomControls/
│   │   │   │   ├── ConditionBuilder.cs       (Constructor visual de condiciones)
│   │   │   │   ├── ConditionElement.cs       (Elemento individual de condición)
│   │   │   │   ├── ActionBuilder.cs          (Constructor visual de acciones)
│   │   │   │   └── ActionElement.cs          (Elemento individual de acción)
│   │   │   │
│   │   │   ├── Projects/
│   │   │   │   └── EditorContext.asset       (Archivo de contexto - creado en runtime)
│   │   │   │
│   │   │   └── UI/
│   │   │       └── USS/
│   │   │           └── Common.uss            (Estilos globales del editor)
│   │   │
│   │   ├── Scripts/
│   │   │   ├── SceneJson.cs                  (Definición de estructuras de datos)
│   │   │   ├── Scripts.cs                    (Utilidades de scripts)
│   │   │   └── LoadWindow.cs                 (Diálogo de carga)
│   │   │
│   │   └── CodingAgent/                      (Módulo experimental de IA)
│   │       ├── ProjectSetupTool.cs
│   │       ├── GameAgentTool.cs
│   │       ├── GameAgentMini.cs
│   │       ├── GameJsonAuditor.cs
│   │       ├── LLamaTool.cs
│   │       └── DLLDebug.cs
│   │
│   ├── Resources/
│   │   ├── Prefabs/
│   │   │   ├── Empty.prefab                  (Prefab por defecto creado automáticamente)
│   │   │   ├── GameManager.prefab            (Requerido para generación de escenas)
│   │   │   └── [Otros prefabs del proyecto]
│   │   │
│   │   └── Games/
│   │       └── [Archivos JSON generados automáticamente]
│   │
│   └── [Proyectos guardados como .asset files]
│       ├── MyGame.asset
│       ├── MyGame_01.asset
│       └── [etc...]
│
├── Packages/
│   └── manifest.json                        (Dependencias de Unity)
│
├── ProjectSettings/
│   ├── ProjectVersion.txt                   (6000.3.1f1)
│   ├── TagManager.asset                     (Etiquetas - modificadas dinamicamente)
│   └── [Otros settings de proyecto]
│
└── [Archivos raíz de git, .gitignore, etc.]
```

## Componentes Clave

### EditorContext

**Archivo:** `Assets/Editor/GameRuleEditor/Core/EditorContext.cs`

ScriptableObject singleton que mantiene todo el estado del editor:

```csharp
[CreateAssetMenu(fileName = "EditorContext", menuName = "GameRule/Editor Context")]
public class EditorContext : ScriptableObject
{
    public GameRuleProject currentProject;           // Proyecto actual
    public int selectedActorIndex = -1;              // Índice del actor seleccionado
    public int selectedScriptIndex = -1;             // Índice de la regla seleccionada
    public GRInspectorMode activeInspectorMode;      // Modo de inspector activo
    
    // Eventos para sincronización de UI
    public event Action OnProjectLoaded;
    public event Action OnProjectChanged;
    public event Action<int> OnActorSelected;
    public event Action OnActorListChanged;
    public event Action<int> OnScriptSelected;
    public event Action<GRInspectorMode> OnInspectorModeChanged;
    
    public bool isUndoRedoRefresh;                   // Flag para refresh en undo/redo
}
```

### GameRuleProject

**Archivo:** `Assets/Editor/GameRuleEditor/Core/GameRuleProject.cs`

ScriptableObject que contiene todo el modelo de datos del proyecto:

```csharp
[CreateAssetMenu(fileName = "NewGameRuleProject", menuName = "GameRule/Project")]
public class GameRuleProject : ScriptableObject
{
    public string projectName;
    public SceneJson sceneData;                 // Configuración de escena
    public List<ActorJson> actors;              // Lista de actores
    
    // Métodos principales
    public string ExportToJson();               // Exporta a JSON limpio
    public void SaveToJsonFile(string path);
    public static GameRuleProject ImportFromJson(string jsonPath);
    public void AddActor(string name, string prefab);
    public void RemoveActor(ActorJson actor);
    public ActorJson DuplicateActor(ActorJson original);
    public List<string> Validate();
}
```

### ProjectController

**Archivo:** `Assets/Editor/GameRuleEditor/Controllers/ProjectController.cs`

Controlador que contiene toda la lógica de negocio y manejo de undo/redo:

**Responsabilidades:**
- Operaciones CRUD en actores, reglas y variables
- Sincronización bidireccional escena ↔ datos
- Integración con sistema de undo/redo de Unity
- Validación de operaciones

**Métodos principales:**
- `CreateNewProject()` / `LoadProject()` / `SaveProjectToJson()`
- `AddActor()` / `RemoveActor()` / `DuplicateActor()`
- `AddRule()` / `RemoveRule()` / `DuplicateRule()` / `MoveRuleUp()` / `MoveRuleDown()`
- `UpdateActorProperty()` / `UpdateRuleCondition()` / `UpdateRuleActions()`
- `SyncDataToScene()` / `SyncSceneToData()` / `GenerateScene()`
- `AddCustomVariable()` / `RemoveCustomVariable()`

### GameRuleParser

**Archivo:** `Assets/Editor/GameRuleEditor/Core/GameRuleParser.cs`

Utilidad estática para parsear y tokenizar funciones y condiciones:

```csharp
public static (string Name, List<string> Params) ParseFunction(string input)
{
    // Parsea "FunctionName(param1, param2)" → ("FunctionName", ["param1", "param2"])
}

public static List<string> TokenizeCondition(string fullCondition)
{
    // Tokeniza "Condition1 AND Condition2 OR Condition3"
    // → ["Condition1", "AND", "Condition2", "OR", "Condition3"]
}
```

### Loader

**Archivo:** `Assets/Editor/GameRuleEditor/Core/Loader.cs`

Clase estática que genera escenas de Unity a partir de archivos JSON:

```csharp
public static void LoadJson(string fileName)
{
    // 1. Lee JSON desde Resources/Games/
    // 2. Crea nueva escena
    // 3. Instancia GameManager
    // 4. Crea etiquetas necesarias
    // 5. Instancia todos los prefabs de actores
    // 6. Configura posiciones, rotaciones, físicas, etc.
}
```

## Flujo de Datos

### Flujo de Creación de Proyecto

```
Usuario abre el editor
    ↓
GameRuleLayoutManager.OpenLayout()
    ↓
Carga/crea EditorContext.asset
    ↓
Crea ProjectController compartido
    ↓
Abre todas las ventanas (Hierarchy, Scene, Properties, Rules)
    ↓
Si no hay proyecto: muestra diálogo modal
    ↓
Usuario crea nuevo proyecto o carga existente
    ↓
ProjectController.CreateNewProject() o LoadProject()
    ↓
EditorContext.OnProjectLoaded ← evento
    ↓
Todas las ventanas se suscriben y actualizan UI
```

### Flujo de Edición de Actor

```
Usuario selecciona actor en Hierarchy
    ↓
ActorListPanel.OnActorSelected() ejecutado
    ↓
context.SelectActor(index)
    ↓
EditorContext.OnActorSelected ← evento
    ↓
ActorDetailsPanel actualiza campos
    ↓
Usuario modifica campo (ej: posición)
    ↓
ActorDetailsPanel registra cambio
    ↓
ProjectController.UpdateActorProperty()
    ↓
Undo.RecordObject() → permite undo
    ↓
modifyAction() ejecutada (actualiza datos)
    ↓
SyncDataToScene() → replica a GameObject
    ↓
EditorUtility.SetDirty() → marca para guardar
    ↓
EditorContext.NotifyProjectChanged()
    ↓
Todas las ventanas se actualizan
```

### Flujo de Sincronización Escena-Editor

**Bidireccional automático:**

1. **Editor → Escena:** Los cambios en ActorDetailsPanel se aplican directamente al GameObject.
2. **Escena → Editor:** Si el desarrollador mueve un GameObject con el gizmo:
   - `ProjectController.OnEditorUpdate()` ejecutado cada frame
   - Detecta `transform.hasChanged`
   - Actualiza datos del actor
   - Notifica UI

## Operaciones Disponibles

### Proyecto

| Operación | Método | Undo |
|-----------|--------|------|
| Crear nuevo proyecto | `ProjectController.CreateNewProject()` | No |
| Cargar proyecto | `ProjectController.LoadProject()` | Sí |
| Guardar a JSON | `ProjectController.SaveProjectToJson()` | No |
| Importar JSON | `ProjectController.ImportJsonAsProject()` | Sí |
| Generar escena | `ProjectController.GenerateScene()` | No |
| Validar proyecto | `ProjectController.ValidateProject()` | - |

### Actores

| Operación | Método | Undo |
|-----------|--------|------|
| Agregar actor | `ProjectController.AddActor()` | Sí |
| Eliminar actor | `ProjectController.RemoveActor()` | Sí |
| Duplicar actor | `ProjectController.DuplicateActor()` | Sí |
| Actualizar propiedad | `ProjectController.UpdateActorProperty()` | Sí |
| Agregar propiedad custom | `ProjectController.AddActorProperty()` | Sí |
| Eliminar propiedad | `ProjectController.RemoveActorProperty()` | Sí |
| Revertir propiedad | `ProjectController.RevertActorProperty()` | Sí |

### Reglas (Rules)

| Operación | Método | Undo |
|-----------|--------|------|
| Agregar regla | `ProjectController.AddRule()` | Sí |
| Agregar regla vacía | `ProjectController.AddEmptyRule()` | Sí |
| Eliminar regla | `ProjectController.RemoveRule()` | Sí |
| Duplicar regla | `ProjectController.DuplicateRule()` | Sí |
| Mover regla arriba | `ProjectController.MoveRuleUp()` | Sí |
| Mover regla abajo | `ProjectController.MoveRuleDown()` | Sí |
| Mover regla a índice | `ProjectController.MoveRuleToIndex()` | Sí |
| Actualizar condiciones | `ProjectController.UpdateRuleCondition()` | Sí |
| Actualizar acciones | `ProjectController.UpdateRuleActions()` | Sí |
| Agregar condición | `ProjectController.AddRuleCondition()` | Sí |
| Eliminar condición | `ProjectController.RemoveRuleCondition()` | Sí |
| Agregar acción | `ProjectController.AddRuleAction()` | Sí |

### Escena Global

| Operación | Método | Undo |
|-----------|--------|------|
| Actualizar propiedad | `ProjectController.UpdateSceneProperty()` | Sí |
| Agregar variable | `ProjectController.AddCustomVariable()` | Sí |
| Eliminar variable | `ProjectController.RemoveCustomVariable()` | Sí |

## Validación y Exportación

### Validación de Proyecto

El método `GameRuleProject.Validate()` verifica:

```csharp
public List<string> Validate()
{
    List<string> errors = new List<string>();
    
    // 1. GameName no puede estar vacío
    if (string.IsNullOrEmpty(sceneData.GameName))
        errors.Add("Game name is required");
    
    // 2. Nombres de actores únicos
    HashSet<string> actorNames = new HashSet<string>();
    foreach (var actor in actors) {
        if (actorNames.Contains(actor.ActorName))
            errors.Add($"Duplicate actor name: {actor.ActorName}");
        else actorNames.Add(actor.ActorName);
    }
    
    // 3. Cada actor debe tener prefab asignado
    foreach (var actor in actors) {
        if (string.IsNullOrEmpty(actor.PrefabName))
            errors.Add($"Actor '{actor.ActorName}' has no prefab assigned");
        else {
            // 4. Prefab debe existir en Resources/Prefabs/
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{actor.PrefabName}");
            if (prefab == null)
                errors.Add($"Prefab not found: {actor.PrefabName}");
        }
    }
    
    return errors;
}
```

### Formato de Exportación JSON

Ejemplo de proyecto exportado:

```json
{
    "GameName": "MyGame",
    "ScreenResolution": [1920, 1080],
    "CameraPosition": [0, 1, -10],
    "CameraRotation": [0, 0, 0],
    "SunPosition": [0, 3, 0],
    "SunRotation": [50, -30, 0],
    "SunColor": [255, 255, 255],
    "SunAmbientColor": [128, 128, 128],
    "BackgroundColor": [0, 0, 0],
    "Gravity": [0, -9.81, 0],
    "CustomVariables": [
        {"name": "PlayerHealth", "type": "int", "intValue": 100}
    ],
    "Cast": [
        {
            "ActorName": "Player",
            "PrefabName": "PlayerCharacter",
            "Active": true,
            "Position": [0, 0, 0],
            "Rotation": [0, 0, 0],
            "Scale": [1, 1, 1],
            "Tag": "Player",
            "Properties": ["isProtagonist"],
            "Script": [
                {
                    "Name": "Jump on Space",
                    "When": ["Input.GetKey(\"Space\")"],
                    "Do": ["PlayAnimation(\"Jump\")", "ApplyForce(0, 10, 0)"]
                }
            ]
        }
    ]
}
```

**Características del formato:**
- Arrays vacíos se omiten en exportación limpia
- Flotantes default (0.0) se omiten
- Los campos `When` y `Do` pueden ser null (se tratan como listas vacías)
- Soporta componentes personalizados mediante `Components` array
- Agrupación de reglas mediante `groupId`

## Sistema de Deshacer/Rehacer

### Integración Unity Undo

Todo cambio en el proyecto utiliza `Undo.RecordObject()`:

```csharp
public void UpdateActorProperty(int actorIndex, System.Action modifyAction, string undoName = "Modify Actor")
{
    // Registra el estado antes del cambio
    Undo.RecordObject(context.currentProject, undoName);
    
    // Ejecuta la modificación
    modifyAction?.Invoke();
    
    // Marca para guardar
    EditorUtility.SetDirty(context.currentProject);
    
    // Notifica cambio
    context.NotifyProjectChanged();
}
```

### Manejo de Undo/Redo

Cuando el usuario presiona Ctrl+Z o Ctrl+Y:

1. `Undo.undoRedoPerformed` evento se dispara
2. `ProjectController.OnUndoRedoPerformed()` ejecutado
3. Valida índices de selección (pueden ser inválidos si se eliminó un actor)
4. `context.isUndoRedoRefresh = true`
5. Notifica todas las ventanas para actualizar
6. Las ventanas reciben el evento y se actualizan

**Casos especiales:**
- Si se elimina el actor seleccionado durante undo, se ajusta el `selectedActorIndex`
- Si se elimina una regla seleccionada, se ajusta el `selectedScriptIndex`

## Decisiones Arquitectónicas

### 1. Patrón de Observador Centralizado

**Decisión:** Un único `EditorContext` ScriptableObject como punto central de estado.

**Justificación:**
- Simplifica sincronización entre múltiples ventanas abiertas
- Permite que nueva UI se integre fácilmente suscribiéndose a eventos
- El estado es persistente (se guarda automáticamente en disco)
- Compatible con el flujo de undo/redo de Unity

### 2. ProjectController como Singleton Compartido

**Decisión:** Una única instancia de `ProjectController` creada por `GameRuleLayoutManager` y reutilizada por todas las ventanas.

**Justificación:**
- Evita suscripciones duplicadas a `EditorApplication.update`
- Previene múltiples instancias del sistema de undo/redo
- Facilita persistencia de estado entre domain reloads (play mode, recompilación)

### 3. Reflexión para Condiciones y Acciones

**Decisión:** Usar reflexión para descubrir condiciones (métodos que retornan bool) y acciones (métodos sin retorno) en clases globales `Condition` y `Action`.

**Justificación:**
- Extensible: agregar nuevas condiciones/acciones no requiere cambios en el editor
- Permite que game designers trabajen sin editar código del editor
- Los dropdowns se populan dinámicamente desde el código de juego

### 4. Sincronización Bidireccional Editor ↔ Escena

**Decisión:** Actualizar GameObject en tiempo real cuando cambia el dato, y detectar cambios de GameObject cuando el desarrollador usa gizmos.

**Justificación:**
- Feedback inmediato al usuario
- No requiere botones "Apply" o "Sync"
- Mantiene la verdad única en los datos del proyecto
- Permite desarrollo colaborativo entre desarrolladores y diseñadores

### 5. Diseño Modular de Ventanas

**Decisión:** Múltiples ventanas especializadas (`HierarchyWindow`, `SceneWindow`, `PropertiesWindow`, `RulesWindow`) que comparten `EditorContext` y `ProjectController`.

**Justificación:**
- Flexible: usuarios pueden acomodar ventanas según sus preferencias
- Escalable: agregar nuevas ventanas es simple
- Compatible con el flujo de dock de Unity
- Cada ventana tiene responsabilidad clara

## Desarrollo Futuro

### Características Planeadas

1. **Drag-and-Drop de Actores y Reglas**
   - Reordenar actores en la lista
   - Reordenar reglas dentro de un actor
   - Estado: Parcialmente implementado (infraestructura de drag existe)

2. **Componentes de Grupo (Component System)**
   - Agrupar reglas bajo componentes reutilizables
   - Cada componente puede tener propiedades
   - Estado: Estructura de datos existe, UI pendiente

3. **Panel de Búsqueda y Filtrado**
   - Buscar actores por nombre/etiqueta
   - Filtrar reglas por tipo de condición
   - Estado: No iniciado

4. **Editor de Prefabs Visual**
   - Seleccionar prefabs directamente desde el editor
   - Vista previa de modelos
   - Estado: No iniciado

5. **Validación en Tiempo Real**
   - Mostrar errores/advertencias en tiempo real mientras se edita
   - Sugerencias de autocomplete
   - Estado: No iniciado

6. **Integración con Agentes IA**
   - Las carpetas `/Assets/Editor/CodingAgent/` contienen código experimental
   - Uso de LLamaSharp para auditoría y generación de código
   - Estado: Experimental

7. **Temas de UI Personalizables**
   - Soportar temas light/dark
   - Paletas personalizables
   - Estado: Estructura de USS lista, paleta actualmente morada/gris


### Conocidos Issues

1. **Legacy GameRuleToolbarWindow**: No está completamente integrada con el nuevo sistema de múltiples ventanas. Usar GameRuleHierarchyWindow en su lugar.

2. **GameRuleInspectorWindow**: Ventana de inspector legacy - puede no estar sincronizada. Usar GameRulePropertiesWindow.

3. **PropertyPickerDialog**: Implementación incompleta, usar los campos nativos en PropertyWindow.

4. **LLamaSharp AI Tools**: Requieren configuración manual de modelos y no están completamente testeados.

## Convenciones de Código

### Namespaces

```
GameRuleEditor.Core            → Lógica fundamental
GameRuleEditor.Controllers     → Controladores de negocio
GameRuleEditor.Windows         → Ventanas del editor
GameRuleEditor.Panels          → Paneles reutilizables
GameRuleEditor.CustomControls  → Controles UI personalizados
```

### Archivos de Datos

- **Extensión `.asset`**: Archivos ScriptableObject (proyectos y contexto)
- **Extensión `.json`**: Exportaciones para el motor de juego
- **Ubicación**: Siempre bajo `Assets/` para ser detectados por Unity

### Convenciones de Propiedades

```csharp
// Métodos Get/Set
public int SelectedActorIndex { get; set; }

// Propiedades ComputedI
public ActorJson SelectedActor
{
    get { /* lógica */ }
}

// Arrays serializados siempre con inicialización
public List<ActorJson> actors = new List<ActorJson>();

// Eventos públicos
public event System.Action OnProjectLoaded;
```