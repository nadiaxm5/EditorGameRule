# Inspección humana ejecutada: 1 actor, 100 reglas

Estado: **EJECUTADA**, según las observaciones manuales comunicadas por el usuario para `20260907T123851573Z-9a66e3`. [Resultados, capturas y exportación comprobada](runs/20260907T123851573Z-9a66e3/manual-panel-result.md). Las observaciones humanas son independientes de las comprobaciones programáticas de selección, callbacks, layout y ScrollTo.

Unity 6000.3.1f1, escala de pantalla Windows 150 %, Edit mode sin Play. Ventana no maximizada, ajustada a un tamaño cómodo; dimensiones exactas no registradas. Fecha/hora manuales no registradas. Las expresiones temporales siguientes son percepciones manuales, no cronometraje.

1. En Unity 6000.3.1f1, abrir `GameRule > Editor`. Importar con `GameRule > Actions > Import version` el archivo `Evaluation/RobustnessScale/scale/descriptors/a1_r100.json`.
2. Seleccionar `EvalActor000` en la jerarquía con el ratón. Registrar si la selección se resalta y cuánto tarda aproximadamente.
3. Botón derecho sobre el actor -> `Rules`. Anotar si abre el conjunto, dimensiones de ventana y escala de pantalla. Las reglas empiezan plegadas.
4. Comprobar `Rule000`, expandirla, editar su nombre temporalmente y restaurarlo; comprobar foco de teclado y que los controles responden. Registrar cualquier excepción de Console.
5. Desplazarse con rueda/barra hasta `Rule099`. Registrar si se alcanza y si se ve el título completo; expandirla y comprobar `Compare(this.Health > 0)` / `Edit(EvalActor000.Health,100)`.
6. Recorrer de principio a fin: deben existir 100 reglas, `Rule000` a `Rule099`, en ese orden, sin duplicados. Comparar con el JSON y el registro automático; anotar recortes, solapamientos, saltos, pérdida de foco/interacción o retrasos perceptibles.
7. Volver al principio, plegar/desplegar y volver a la última regla. Registrar estado final de interacción y exportar a un archivo nuevo para conservar evidencia de cualquier cambio involuntario.
8. Guardar una copia de esta lista en `runs/<run>/manual-panel-result.md`, con los datos efectivamente registrados y cada observación como sí/no/no comprobado, mensajes exactos y evidencia si procede. En esta inspección no se registraron UTC, commit durante la inspección ni dimensiones exactas; no se infieren. No incluir nombres personales ni rutas privadas. No reemplazar resultados automáticos.

| Comprobación | Estado | Observación/evidencia |
|---|---|---|
| Selección con ratón | Ejecutada | EvalActor000 se seleccionó inmediatamente. |
| Apertura del conjunto | Ejecutada | Rules abrió con retraso inferior a un segundo; Rule000 se expandió. |
| Respuesta a ratón y teclado | Ejecutada | Correcta e inmediata; Rule000 → Rule000_test → Rule000. |
| Desplazamiento hasta Rule099 | Ejecutada | Dos recorridos; título completo y acción con valor 100. |
| 100 reglas visibles al recorrer | Ejecutada | Consecutivas, sin huecos ni duplicados observados; nombres y orden verificados en el JSON exportado. |
| Ausencia de errores de dibujo | Ejecutada | Sin recortes, solapamientos, superposiciones ni desaparición de elementos observados. |
| Retrasos / pérdida de interacción | Ejecutada | Sin bloqueos, atascos, pérdida de foco ni retrasos perceptibles; plegar/desplegar funcionó. |
| Console | Ejecutada | Sin errores ni advertencias durante esta comprobación de 1×100. |
| Exportación | Verificada programáticamente | manual-a1-r100-export.json: 1 actor, 100 reglas, orden y contenido conservados. |
