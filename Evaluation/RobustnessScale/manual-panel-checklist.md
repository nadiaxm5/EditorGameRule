# Inspección humana pendiente: 1 actor, 100 reglas

Estado inicial: **NO EJECUTADA**. Las comprobaciones programáticas de selección, callbacks, layout y ScrollTo se guardan en cada muestra `a1_r100`; no completan esta lista.

1. En Unity 6000.3.1f1, abrir `GameRule > Editor`. Importar con `GameRule > Actions > Import version` el archivo `Evaluation/RobustnessScale/scale/descriptors/a1_r100.json`.
2. Seleccionar `EvalActor000` en la jerarquía con el ratón. Registrar si la selección se resalta y cuánto tarda aproximadamente.
3. Botón derecho sobre el actor -> `Rules`. Anotar si abre el conjunto, dimensiones de ventana y escala de pantalla. Las reglas empiezan plegadas.
4. Comprobar `Rule000`, expandirla, editar su nombre temporalmente y restaurarlo; comprobar foco de teclado y que los controles responden. Registrar cualquier excepción de Console.
5. Desplazarse con rueda/barra hasta `Rule099`. Registrar si se alcanza y si se ve el título completo; expandirla y comprobar `Compare(this.Health > 0)` / `Edit(EvalActor000.Health,100)`.
6. Recorrer de principio a fin: deben existir 100 reglas, `Rule000` a `Rule099`, en ese orden, sin duplicados. Comparar con el JSON y el registro automático; anotar recortes, solapamientos, saltos, pérdida de foco/interacción o retrasos perceptibles.
7. Volver al principio, plegar/desplegar y volver a la última regla. Registrar estado final de interacción y exportar a un archivo nuevo para conservar evidencia de cualquier cambio involuntario.
8. Guardar una copia de esta lista en `runs/<run>/manual-panel-result.md`, con UTC, commit, versión Unity, tamaño de ventana, cada observación como sí/no/no comprobado, mensajes exactos y evidencia si procede. No incluir nombres personales ni rutas privadas. No reemplazar resultados automáticos.

| Comprobación | Estado | Observación/evidencia |
|---|---|---|
| Selección con ratón | Pendiente | |
| Apertura del conjunto | Pendiente | |
| Respuesta a ratón y teclado | Pendiente | |
| Desplazamiento hasta Rule099 | Pendiente | |
| 100 reglas visibles al recorrer | Pendiente | |
| Ausencia de errores de dibujo | Pendiente | |
| Retrasos / pérdida de interacción | Pendiente | |
