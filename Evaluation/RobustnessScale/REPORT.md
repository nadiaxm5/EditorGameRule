# Informe de ejecución: robustez de importación y escala

Ejecución completa: `20260907T123851573Z-9a66e3`. Inicio UTC: `2026-09-07T12:39:00.3350127Z`. Fin: `2026-09-07T12:45:46.2698952Z`.
Rama: `major-revision/robustness-scale-evaluation`; partida: `c58f222091dbb642797006045ded9c226309fc3f` (`EditorDeterminista`).
Código evaluado: `5a463cc0a2af8b9131524aca5bf813e327fe32f3`. Producción sin cambios respecto al commit de partida. El commit que contiene este informe incorpora las evidencias; se identifica con `git log -1 -- Evaluation/RobustnessScale/REPORT.md`.

## Entorno y alcance

Windows 11  (10.0.26200) 64bit; CPU AMD Ryzen AI 9 365 w/ Radeon 880M (20 procesadores lógicos); RAM 31763 MB; GPU NVIDIA GeForce RTX 5070 Laptop GPU; Direct3D 11.0 [level 11.1].
Unity 6000.3.1f1 (37142da19a94); UI Toolkit integrado, módulo UIElements 1.0.0; Visual Scripting 1.9.9; Test Framework 1.6.0. Edit mode gráfico, ventanas de producción, sin Play ni generación de escenas. El entorno completo, paquetes, opciones y hashes están en [environment.json](environment.json).

Se usan `ProjectController.ImportJsonAsProject`, `GameRuleProject.ImportFromJson`, `EditorContext.LoadProject/SelectActor`, `GameRuleHierarchyWindow`, `GameRuleRulesWindow`, `ScriptEditorPanel` y sus builders reales; exportación mediante `ProjectController.SaveProjectToJson` y `GameRuleProject.ExportToJson`. `Validate` se observa después, separado de la importación.

## Seis entradas inválidas

| Caso | Detectado al importar/reconstruir | Instalado | Silencioso | Excepción escapada | Proyecto activo anterior intacto | Asset anterior intacto | Estado visual inconsistente |
|---|---|---|---|---|---|---|---|
| malformed_json | Sí | No | No | Sí | Sí | Sí | No |
| missing_required | No | Sí | Sí | No | No | Sí | No |
| missing_actor | No | Sí | Sí | No | No | Sí | No |
| missing_property | No | Sí | Sí | No | No | Sí | No |
| unsupported_action | No | Sí | Sí | No | No | Sí | Sí |
| invalid_parameter | No | Sí | Sí | No | No | Sí | No |

- **malformed_json**: `ArgumentException` en la llamada única de parsing/deserialización `JsonUtility.FromJson`. Mensaje exacto: `JSON parse error: Missing a comma or '}' after an object member.` No se instala otro proyecto; se conservan estado activo y asset anterior. La excepción sale del método de producción y la captura la suite; no se ha reproducido su presentación natural en Console a través del selector de archivo.
- **missing_required**: se instala sin diagnóstico de importación; la llamada explícita posterior a `Validate()` devuelve exactamente `Game name is required`. La importación natural no invoca esa validación.
- **missing_actor**, **missing_property**, **invalid_parameter**: se instalan y reconstruyen sin mensajes de diagnóstico. Su texto se conserva. No se ejecutó la resolución en runtime de estas referencias/expresiones.
- **unsupported_action**: se instala silenciosamente; el JSON conserva `NoSuchAction(EvalActor000.Health,1)`, mientras el primer control visual devuelve `Edit(,)`. Se registra inconsistencia entre datos y control, sin atribuirle un diagnóstico visible.
- Los cinco casos instalados sustituyen el proyecto activo anterior. Sus assets anteriores conservan actores, propiedades, reglas, orden y contenido exportado: sustitución del activo y corrupción de su asset se comprueban por separado.

No hubo bloqueo ni timeout de importación en la ejecución completa. Los mensajes vacíos de cinco casos son ausencia de diagnóstico, no mensajes completados o reconstruidos por la suite. Entradas/mutaciones y sus motivos: [inputs.json](inputs.json). Todas las columnas solicitadas y mensajes están en el [CSV de robustez](runs/20260907T123851573Z-9a66e3/summary/robustness.csv).

## Escala: cinco valores brutos y estadísticas

Milisegundos, redondeados aquí a dos decimales; JSON/CSV conservan precisión completa. Calentamiento 0 excluido y conservado. Importación incluye lectura, deserialización, defaults y persistencia; visual incluye jerarquía y visita de todos los paneles de actor, con esperas de layout. Total incluye la operación hasta exportar. No representa la latencia de una sola selección ni rendimiento del juego.

| Actores × reglas/actor | Operación | R1 | R2 | R3 | R4 | R5 | Mediana | Q1 | Q3 | IQR |
|---|---|---|---|---|---|---|---|---|---|---|
| 10 × 10 | Importación/persistencia | 41.49 | 40.61 | 40.19 | 71.11 | 42.73 | 41.49 | 40.61 | 42.73 | 2.12 |
| 10 × 10 | Reconstrucción visual | 975.14 | 933.64 | 939.65 | 1208.48 | 1032.63 | 975.14 | 939.65 | 1032.63 | 92.98 |
| 10 × 10 | Exportación | 48.89 | 49.20 | 50.62 | 49.84 | 50.60 | 49.84 | 49.20 | 50.60 | 1.40 |
| 10 × 10 | Total | 1066.63 | 1024.85 | 1031.69 | 1330.71 | 1127.03 | 1066.63 | 1031.69 | 1127.03 | 95.35 |
| 50 × 20 | Importación/persistencia | 48.13 | 44.94 | 80.08 | 66.64 | 48.75 | 48.75 | 48.13 | 66.64 | 18.51 |
| 50 × 20 | Reconstrucción visual | 6115.97 | 5437.04 | 5524.74 | 5997.47 | 6454.46 | 5997.47 | 5524.74 | 6115.97 | 591.23 |
| 50 × 20 | Exportación | 480.28 | 608.68 | 390.85 | 387.07 | 422.05 | 422.05 | 390.85 | 480.28 | 89.43 |
| 50 × 20 | Total | 6656.00 | 6104.53 | 5998.20 | 6453.82 | 6928.14 | 6453.82 | 6104.53 | 6656.00 | 551.47 |
| 100 × 20 | Importación/persistencia | 59.96 | 80.63 | 83.05 | 70.89 | 64.40 | 70.89 | 64.40 | 80.63 | 16.23 |
| 100 × 20 | Reconstrucción visual | 14059.12 | 18577.68 | 18479.56 | 15575.64 | 13836.57 | 15575.64 | 14059.12 | 18479.56 | 4420.44 |
| 100 × 20 | Exportación | 1141.07 | 1609.66 | 982.52 | 854.99 | 860.72 | 982.52 | 860.72 | 1141.07 | 280.35 |
| 100 × 20 | Total | 15268.41 | 20276.68 | 19552.01 | 16507.42 | 14768.34 | 16507.42 | 15268.41 | 19552.01 | 4283.60 |
| 200 × 20 | Importación/persistencia | 112.90 | 87.26 | 66.63 | 93.01 | 72.35 | 87.26 | 72.35 | 93.01 | 20.66 |
| 200 × 20 | Reconstrucción visual | 35183.90 | 30362.01 | 25240.76 | 29948.24 | 26891.39 | 29948.24 | 26891.39 | 30362.01 | 3470.61 |
| 200 × 20 | Exportación | 1725.97 | 1790.88 | 1983.43 | 1975.60 | 1721.46 | 1790.88 | 1725.97 | 1975.60 | 249.63 |
| 200 × 20 | Total | 37031.78 | 32268.78 | 27298.21 | 32033.70 | 28693.09 | 32033.70 | 28693.09 | 32268.78 | 3575.68 |
| 1 × 100 | Importación/persistencia | 52.22 | 68.20 | 59.58 | 45.94 | 40.91 | 52.22 | 45.94 | 59.58 | 13.64 |
| 1 × 100 | Reconstrucción visual | 681.09 | 405.61 | 525.90 | 708.86 | 461.90 | 525.90 | 461.90 | 681.09 | 219.19 |
| 1 × 100 | Exportación | 59.94 | 44.57 | 45.92 | 51.11 | 47.19 | 47.19 | 45.92 | 51.11 | 5.19 |
| 1 × 100 | Total | 794.75 | 519.78 | 632.76 | 808.39 | 551.81 | 632.76 | 551.81 | 794.75 | 242.94 |

Cuartiles tipo 7: con cinco valores ordenados, Q1=x2, mediana=x3, Q3=x4, IQR=x4−x2. No se eliminan outliers ni fallos. Límites: 15 s de layout, 600 s cooperativos por muestra, watchdog externo de 660 s por muestra y 300 s de arranque.

## Corrección y panel de 100 reglas

Se conservan 25/25 muestras medidas y 5 calentamientos. Comprobaciones de conteos, identificadores únicos, orden de actores/reglas, contenido de entrada, jerarquía, controles, exportación y round-trip satisfechas en 25/25 muestras medidas.
El descriptor base también superó esas comprobaciones en la ejecución completa. El JSON bruto contiene las listas ordenadas y snapshots; no se deduce conservación a partir de que una ventana permanezca abierta.

| Repetición 1×100 | Actor seleccionado | Reglas | Callback de despliegue | Última regla alcanza viewport | Última regla |
|---|---|---|---|---|---|
| 1 | Sí | 100 | Sí | Sí | Rule099 |
| 2 | Sí | 100 | Sí | Sí | Rule099 |
| 3 | Sí | 100 | Sí | Sí | Rule099 |
| 4 | Sí | 100 | Sí | Sí | Rule099 |
| 5 | Sí | 100 | Sí | Sí | Rule099 |

**Inspección humana ejecutada según las observaciones comunicadas por el usuario:** respuesta inmediata de ratón/teclado, dos recorridos hasta `Rule099`, plegado/desplegado y cambio temporal/restauración de nombre sin incidencias perceptibles; comprobación adicional de tres actores en 200×20. La exportación manual conserva las 100 reglas y su orden. Las importaciones inválidas desde el menú confirmaron el error rojo de parsing y los otros cinco casos aceptados e importados sin diagnóstico; estos no se califican como importaciones correctas. No se evaluó generación ni runtime. [Resultados manuales, cuatro capturas originales y verificación](runs/20260907T123851573Z-9a66e3/manual-panel-result.md); [lista de comprobación completada](manual-panel-checklist.md). Unity 6000.3.1f1, escala Windows 150 %, Edit mode, ventana no maximizada de dimensiones no registradas; fecha/hora manuales no registradas. Los tiempos percibidos no se mezclan con las mediciones automáticas anteriores.

## Ensayos previos, correcciones y limitaciones

- Dos sondeos de inicio se conservaron en [startup-observations.json](runs/preflight/startup-observations.json): reconexiones de licencia y un cierre con código 1, antes de conseguir ejecutar Unity. No se convierten en resultados de importación.
- El [primer ensayo de instrumentación](runs/20260907T123257879Z-a44568/ATTEMPT.md) se conserva completo. Leyó el título desde el elemento equivocado y encontró un segundo fallo al intentar guardar otra vez la muestra. Solo se corrigió la suite; no se alteraron expectativas ni producción. Sus tiempos no se mezclan con los de la ejecución completa.
- **Correcciones de producción: ninguna.** La excepción de parsing se pudo observar sin impedir las muestras independientes; no se añadió validación semántica ni se cambió la aceptación silenciosa.
- Una máquina, un proceso gráfico para la ejecución completa, cinco repeticiones por configuración, cachés calientes tras calentamiento; ventanas y contexto nuevos por muestra. No se mide generación/compilación/runtime, renderizado GPU final ni consumo de memoria por operación.

## Repetición, archivos y verificación

Todos los pasos exactos están en [README.md](README.md#ejecución-exacta). Desde la raíz:

```powershell
python Evaluation/RobustnessScale/scale/generator/generate.py
python Evaluation/RobustnessScale/test-code/test_tools.py
& Evaluation/RobustnessScale/test-code/run.ps1 -Unity 'C:\Program Files\Unity\Hub\Editor\6000.3.1f1\Editor\Unity.exe'
python Evaluation/RobustnessScale/test-code/summarize.py IDENTIFICADOR_DE_EJECUCION
python Evaluation/RobustnessScale/test-code/audit.py IDENTIFICADOR_DE_EJECUCION
```

Añadidos: `Assets/Editor/Evaluation/` y su meta; `Evaluation/RobustnessScale/` con suite auxiliar, siete entradas de robustez, cinco de escala, manifiesto, entorno, resultados, resúmenes, informe y checklist. Los métodos de producción no cambian.
Se ejecutaron los seis tests offline del generador, aislamiento de mutaciones, cuartiles y tratamiento de pendientes/timeouts (todos satisfechos); la compilación del Editor y la ejecución completa en Unity también se realizaron. Advertencias CS0618 de APIs transform obsoletas pertenecen al código previo, sin errores de compilación añadidos.
[Auditoría de trazabilidad](runs/20260907T123851573Z-9a66e3/summary/audit.json): hashes de entradas y JSON brutos, identidad de muestras y completitud. Los resúmenes no sobrescriben los brutos. El `.gitattributes` local usa LF para entradas y herramientas; conserva los bytes originales de las evidencias con `-text`, evitando que Git transforme los CRLF emitidos por Unity y cambie sus hashes.
Se verificaron también 62 hashes directamente sobre un `git archive`, con cero discrepancias: [verificación de los bytes versionados](test-code/archive-verification.json). Los resúmenes usan rutas relativas con `/` para permitir su auditoría en otros sistemas.
[Tablas JSON/CSV e informe procesable](runs/20260907T123851573Z-9a66e3/summary/report.md). Publicación y commit final de entrega: consultar el mensaje de entrega y `git log`; no se crea PR ni se realiza merge.
