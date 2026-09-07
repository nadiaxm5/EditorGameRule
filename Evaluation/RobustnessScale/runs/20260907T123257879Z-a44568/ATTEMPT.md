# Ensayo inicial de instrumentación, conservado

No utilizar para estadísticas de escala ni para clasificar las seis entradas inválidas. Se ejecutó únicamente la base contra producción original, sin modificaciones. Su JSON bruto acredita 2 actores, 4 reglas, exportación y round-trip conservados. `visual_matches_model=false` provino de un error del lector de la suite: `ScriptEditorPanel.CreateRuleElement` vacía el texto del Toggle del Foldout e inserta el título editable en un TextField. El lector inicial capturó dos cadenas vacías en vez de leer esos campos.

Además, al detener el gate, el manejador del runner intentó guardar otra vez la muestra ya guardada y produjo exactamente `IOException: Refusing to overwrite raw result`. El JSON original no se sobrescribió. Aunque el launcher registró salida 0, no existe `completed.json` y faltan las otras muestras; no es una ejecución completa.

Se corrigió **solo la instrumentación**: leer `Foldout.Q<Toggle>().Q<TextField>().value` y no guardar dos veces una muestra terminada. No se cambió el título esperado, los descriptores ni producción. La suite corregida está en `5a463cc`; la ejecución completa posterior tiene otro identificador. El código de instrumentación de este ensayo aún no tenía commit propio; se conserva esta limitación de trazabilidad junto con los hashes de producción y entradas de su environment/manifest.
