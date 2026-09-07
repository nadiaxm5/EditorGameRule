# Evidencia manual complementaria

Ejecución automática de referencia: `20260907T123851573Z-9a66e3`.

**Procedencia:** observaciones manuales comunicadas por el usuario, acompañadas de cinco capturas y de `manual-a1-r100-export.json`. No son una nueva ejecución de la suite por el asistente. Las imágenes se inspeccionaron al incorporarlas; la exportación se comprobó programáticamente. Los tiempos «menos de un segundo» e «inmediatamente» son apreciaciones manuales, no medidas de Stopwatch, y no se incorporan a las estadísticas automáticas.

La fecha y hora de las observaciones manuales **no se registraron**. Tampoco se consignó un commit durante la inspección manual. Al iniciar esta incorporación, el checkout estaba en `178115633699465214584d1502e3d025633c1904`, rama `major-revision/robustness-scale-evaluation`; la ejecución automática evaluó `5a463cc0a2af8b9131524aca5bf813e327fe32f3`. La fecha del verificador, si se registra en sus resultados, corresponde exclusivamente a la verificación del archivo.

## Entorno manual

- Unity **6000.3.1f1**.
- Escala de pantalla de Windows: **150 %**.
- Ventana Rules no maximizada, ajustada manualmente a un tamaño cómodo.
- Dimensiones exactas de ventana: **no registradas**. No se deducen de los recortes de las capturas ni de las dimensiones de las ventanas del ensayo automático.
- Todas las pruebas se realizaron en **Edit mode**, sin entrar en Play mode.
- No se ejecutaron generación de escena ni runtime. Los cinco casos inválidos aceptados se describen únicamente como **aceptados e importados sin diagnóstico**, no como importaciones correctas.

## Panel con un actor y 100 reglas

Observaciones comunicadas:

- `a1_r100.json` apareció en menos de un segundo.
- `EvalActor000` se seleccionó inmediatamente.
- La ventana Rules se abrió con un retraso inferior a un segundo.
- `Rule000` se expandió correctamente y mostró `Compare(this.Health > 0)` y `Edit(EvalActor000.Health,1)`.
- Se cambió temporalmente el nombre a `Rule000_test` y se restauró a `Rule000`.
- Ratón y teclado respondieron correcta e inmediatamente.
- Se pudo recorrer el panel hasta `Rule099`.
- Las reglas aparecieron consecutivamente, sin huecos, duplicados, recortes ni solapamientos evidentes.
- `Rule099` mostró `Compare(this.Health > 0)` y `Edit(EvalActor000.Health,100)`; su título se mostró completo.
- Se recorrió una segunda vez el panel desde `Rule000` hasta `Rule099`.
- Plegar y desplegar las reglas funcionó correctamente.
- No hubo bloqueos, atascos, pérdida de foco, desaparición de elementos, superposiciones, recortes ni retrasos perceptibles.
- La Console no mostró errores ni advertencias.
- Se exportó [manual-a1-r100-export.json](manual-a1-r100-export.json).

Las capturas [Rule000](manual-a1-rule000.png) y [Rule099](manual-a1-rule099.png) muestran los títulos, condición y acción descritos. Las capturas estáticas corroboran ese contenido visible; la respuesta a ratón/teclado y los tiempos proceden del relato manual.

### Verificación programática de la exportación

Procedimiento reproducible, desde la raíz del repositorio:

```powershell
python Evaluation/RobustnessScale/test-code/verify_manual.py 20260907T123851573Z-9a66e3
python Evaluation/RobustnessScale/test-code/audit.py 20260907T123851573Z-9a66e3
```

El primer comando comprueba el JSON y actualiza únicamente el manifiesto de evidencia manual y su resultado de verificación. El segundo reutiliza la auditoría de hashes de entradas y resultados automáticos, ampliada para los artefactos manuales. Ninguno sobrescribe resultados automáticos brutos.

Resultado: [manual-export-verification.json](manual-export-verification.json).

| Comprobación | Resultado observado en el archivo |
|---|---|
| GameName | `GR_Eval_a1_r100` |
| Actores | Exactamente uno: `EvalActor000` |
| Reglas | Exactamente 100 |
| Nombres y orden | `Rule000` a `Rule099`, consecutivos, sin omisiones ni duplicados |
| Primera acción | `Edit(EvalActor000.Health,1)` en `Rule000` |
| Última acción | `Edit(EvalActor000.Health,100)` en `Rule099` |
| Condiciones | `Compare(this.Health > 0)` en las 100 reglas |
| Orden y contenido de todas las reglas | Coinciden con el descriptor `a1_r100.json`, incluidos `groupId`, When y Do |
| Comparación adicional | El JSON completo coincide estructuralmente con `scale-a1_r100-5-export.json`, preservando el orden de arrays; no se exige igualdad de espacios o saltos de línea |

## Importaciones inválidas desde el menú de producción

Ruta utilizada: **GameRule > Actions > Import version**.

### 1. malformed_json.json

- Apareció un error rojo en Console. Mensaje y traza íntegra proporcionados por el usuario:

```text
ArgumentException: JSON parse error: Missing a comma or '}' after an object member.

UnityEngine.JsonUtility.FromJsonInternal (System.String json, System.Object objectToOverwrite, System.Type type) (at <4ecfcf9250334024b05302773cbe44f3>:0)
UnityEngine.JsonUtility.FromJson (System.String json, System.Type type) (at <4ecfcf9250334024b05302773cbe44f3>:0)
UnityEngine.JsonUtility.FromJson[T] (System.String json) (at <4ecfcf9250334024b05302773cbe44f3>:0)
GameRuleEditor.Core.GameRuleProject.ImportFromJson (System.String jsonPath) (at Assets/Editor/GameRuleEditor/Core/GameRuleProject.cs:194)
GameRuleEditor.Controllers.ProjectController.ImportJsonAsProject (System.String jsonPath) (at Assets/Editor/GameRuleEditor/Controllers/ProjectController.cs:122)
GameRuleEditor.Windows.GameRuleTopMenuActions.ImportJson () (at Assets/Editor/GameRuleEditor/Windows/GameRuleTopMenuActions.cs:63)
```

- La traza pasó por `GameRuleProject.ImportFromJson` (línea 194), `ProjectController.ImportJsonAsProject` (línea 122) y `GameRuleTopMenuActions.ImportJson` (línea 63), según la copia anterior.
- No apareció un diálogo.
- La importación no se instaló.
- `GR_Eval_Baseline` permaneció como proyecto activo.
- `EvalActor000` y `EvalActor001` continuaron disponibles.
- El panel de reglas siguió abriéndose.
- GameRule y Unity continuaron respondiendo.

Esto completa la observación manual de presentación en Console que no ejercitaba el runner al capturar la excepción. La disponibilidad visual comunicada no sustituye la comparación de estado completo conservada en los resultados automáticos.

### 2. missing_required.json

- No apareció mensaje en Console, ventana ni diálogo.
- El descriptor fue **aceptado e importado sin diagnóstico**.
- El campo Game Name quedó vacío.
- Los dos actores continuaron disponibles.
- El panel de reglas pudo abrirse.
- GameRule siguió respondiendo.

### 3. missing_actor.json

- Fue **aceptado e importado sin diagnóstico**.
- No apareció mensaje en Console, ventana ni diálogo.
- GameRule siguió respondiendo.
- Los dos actores y el panel de reglas permanecieron accesibles.
- No se evaluó generación ni runtime.

### 4. missing_property.json

- Fue **aceptado e importado sin diagnóstico**.
- No apareció mensaje en Console, ventana ni diálogo.
- GameRule siguió respondiendo.
- Los dos actores y el panel de reglas permanecieron accesibles.
- No se evaluó generación ni runtime.

### 5. unsupported_action.json

- Fue **aceptado e importado sin diagnóstico**.
- No apareció mensaje en Console, ventana ni diálogo.
- GameRule siguió respondiendo.
- En `Rule000` de `EvalActor000`, la interfaz mostró **Edit con Property y Value vacíos**, tal como muestra [la captura](manual-unsupported-action.png).
- Esto confirma la discrepancia visual observada automáticamente respecto al JSON, que contiene `NoSuchAction(EvalActor000.Health,1)`.
- No se evaluó generación ni runtime.

### 6. invalid_parameter.json

- Fue **aceptado e importado sin diagnóstico**.
- No apareció mensaje en Console, ventana ni diálogo.
- GameRule siguió respondiendo.
- Los dos actores y el panel de reglas permanecieron accesibles.
- No se evaluó generación ni runtime.

## Comprobación de 200 actores y 20 reglas por actor

Observaciones comunicadas:

- `a200_r20.json` apareció en menos de un segundo.
- Unity y GameRule continuaron respondiendo.
- `EvalActor000`, `EvalActor099` y `EvalActor199` pudieron seleccionarse inmediatamente.
- Sus paneles Rules se abrieron correctamente.
- Cada uno de esos paneles mostró desde `Rule000` hasta `Rule019`.
- Se pudo llegar y expandir `Rule019`.
- Cambiar entre los tres actores fue inmediato, sin retraso perceptible.
- `EvalActor199`, `Rule019`, mostró `Compare(this.Health > 0)` y `Edit(EvalActor199.Health,20)`.
- No se observaron errores gráficos ni problemas de interacción.
- La Console no mostró errores.

La [captura de EvalActor199/Rule019](manual-a200-rule019.png) muestra simultáneamente la regla expandida y el tramo final de la jerarquía, con `EvalActor199` seleccionado. Se conserva también la [captura independiente de la jerarquía](manual-a200-hierarchy.png), que muestra su tramo final y `EvalActor199` seleccionado. La inspección manual de esta configuración comprende los tres actores indicados; no se afirma haber inspeccionado manualmente los 200 paneles.

## Capturas y trazabilidad

Se incorporaron cinco PNG originales, copiados byte a byte, sin recortes, reescalado ni edición. Los nombres de origen son etiquetas de archivo, sin rutas privadas:

| Archivo de evidencia | Origen | Alcance visible |
|---|---|---|
| `manual-a1-rule000.png` | `CapturaRule000` | Rule000, Compare y Edit con valor 1 |
| `manual-a1-rule099.png` | `CapturaRule099` | Rule099, título completo, Compare y Edit con valor 100 |
| `manual-unsupported-action.png` | `CapturaUnsupportedAction` | Rule000, Edit con Property y Value vacíos |
| `manual-a200-rule019.png` | `CapturaEvalActor199Rule019` | Rule019 de EvalActor199 y tramo final de la jerarquía |
| `manual-a200-hierarchy.png` | `CapturaEvalActor199Jerarquia` | Captura independiente del tramo final de la jerarquía con EvalActor199 seleccionado |

El apartado `manual_evidence` de [inputs.json](inputs.json) conserva los hashes SHA-256 y la relación entre archivos y fuentes. Los hashes y descriptores automáticos originales permanecen iguales. Los resúmenes automáticos que indicaban observaciones manuales pendientes describen su estado histórico al ejecutar la suite; este documento los complementa sin reclasificar sus resultados.

## Cambios locales de las importaciones, excluidos del commit

Al comenzar la incorporación existían cambios ajenos a esta documentación: `Assets/Editor/GameRuleEditor/Projects/EditorContext.asset` apuntaba a `GR_Eval_Baseline 9.asset`, con `selectedActorIndex: 0`; había 14 proyectos `.asset` nuevos y sus 14 `.meta` (`GR_Eval_Baseline` y sufijos 1–9, `GR_Eval_a1_r100` y sufijo 1, `GR_Eval_a200_r20`, `ImportedProject`), además de `Assets/Resources/Games/GR_Eval_a1_r100.json` y su `.meta`.

Se conservaron en el checkout y se excluyeron del commit inicial de evidencia manual (`905dde4`). La corrección final incluye, por petición expresa del usuario y después del commit correctivo, restaurar únicamente `EditorContext.asset` desde la rama y eliminar únicamente los 30 archivos no versionados enumerados (14 proyectos y sus `.meta`, más el JSON de Resources/Games y su `.meta`), previa verificación de cada ruta. La exportación manual situada en esta carpeta sí forma parte de la entrega y se conserva sin modificar sus bytes. No se elimina ningún archivo de Evaluation/RobustnessScale ni se modifica código de producción o el manuscrito.
