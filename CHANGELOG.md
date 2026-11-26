# Cambios

## 2.6.3
- Importar Unified: si un `Create` de una composición crea un objeto de otro tipo que el `$tipo` del JSON, se borra y se prueba el siguiente. En las tablas de valores con `ConditionType` *Bitmask* salía `MappingTableEntryBitmaskList` en vez de `MappingTableEntryBitmask`: las entradas quedaban vacías y TIA daba *Duplicate entries are not allowed for property dynamization with a tag* al compilar.
- `"Formula": ""` ya no se escribe (TIA no deja escribir la fórmula si está desactivada y daba un aviso por cada dinamización).
- Capas: las capas de la pantalla (`$capas`) se escriben bien; la capa de cada objeto todavía no (`Layer` no es propiedad ni atributo de los objetos en V20).

## 2.6.2
- **Capas en Unified (en prueba)**: al importar, `"Layer": n` de cada objeto y `"$capas"` de la pantalla (nombre de cada capa, por posición) se escriben con la API. Si una clave del JSON no es una propiedad .NET del objeto, se prueba como atributo de TIA (`SetAttribute`) antes de dar el aviso.
- Exportar Unified: añade `Layer` de cada objeto y `$capas` de la pantalla, y deja `_atributos_unified.txt` con los atributos de escritura de cada tipo que no salen como propiedad, para ver qué más se puede exportar.
- Guía 6.8: separar por capas lo que se genera.

## 2.6.1
- Variables HMI: `Connection` se escribe la primera y en el momento. Tal como sale de *Exportar selección* (`{"$ref": "HMI_Connection_1"}`) se resolvía al final de la importación, después de `PlcTag`. Vale `"*"`, el nombre o `{"$ref": …}`; si la propiedad es un objeto se busca la conexión del HMI por nombre. `"<Internal tag>"` o vacío (variables internas) no se tocan.
- Al sobrescribir, las tablas de variables existentes se copian antes en `Import_XML\copia_unified_<fecha>\Variables\`, como las pantallas.
- README: la importación Unified ya admite tablas de variables.

## 2.6.0
- **Importar variables HMI Unified (U2, en prueba)**: *Importar ficheros* sobre el panel Unified acepta también tablas de variables en JSON (`"$tipo": "HmiTagTable"`, con la lista `Tags`). Crea la tabla si no existe y en ella cada variable; las propiedades se escriben en el orden del JSON (`Connection` antes que `PlcTag`). `"Connection": "*"` = la única conexión del HMI. Si una variable ya existe, se actualiza solo si se elige sobrescribir (la misma pregunta que para las pantallas). Las tablas se importan antes que las pantallas, porque sus dinamizaciones y scripts las nombran. El resumen cuenta variables nuevas, actualizadas y con error.

## 2.5.15
- El mensaje final de *Exportar* ya no bloquea TIA: el Add-In devuelve el control en cuanto termina y el mensaje sale aparte. Si quedaba detrás de la ventana de TIA, TIA parecía colgado esperándolo.

## 2.5.14
- Exportar Unified más rápido y sin que TIA se quede "pensando" al terminar: no se leen las propiedades simples de solo lectura (no se exportan), y después de cada pantalla se sueltan los objetos de TIA leídos (antes se liberaban de golpe, después de exportar). El resumen dice cuánto tarda cada pantalla.
- *Exportar selección* sobre el panel Unified exporta también las tablas de variables HMI completas (`Variables/<tabla>.json`), para preparar su importación (U2). Al importar se ignoran.

## 2.5.13
- Importar Unified: compila el HMI aunque el software no dé el servicio de compilación (lo busca en el contenedor de software o en el módulo del panel seleccionado). Antes salía *Compilación HMI: no realizada*.
- Guía 6.8 y hoja de ruta: importación de pantallas comprobada en MTP1500 (U3); las pantallas se exportan e importan desde el panel.

## 2.5.12
- Importar Unified: en propiedades de tipo `object` (el `Value` de las tablas de valores de las dinamizaciones) un `"#AARRGGBB"` se escribe como color.
- El Unified Comfort Panel se reconoce también por su referencia (`6AV2 128…`): desaparece el aviso falso con el MTP1500.

## 2.5.11
- **Causa del TiaPortal liberado en el panel Unified**: leer el software de un panel WinCC Unified (`SoftwareContainer.Software`) dentro de una función de estado del menú libera el `TiaPortal` del Add-In. Ahora, al abrir el menú, un panel se reconoce solo por su referencia (`TypeIdentifier` `6AV…`, `Hmi.EsPanel`) y su software se lee al pulsar la opción. `ObtenerPlc` ya no lee el software de los módulos de panel.

## 2.5.10
- **Arreglo de *Access to a disposed object of type TiaPortal***: el menú vuelve a ser el de la v2.4.10, con lo mínimo para Unified (*Exportar pantallas Unified…* sobre el panel, exportar e importar pantallas en JSON). Desde la 2.5.1 las funciones de estado del menú (las que TIA llama al abrir el submenú) hacían trabajo de más (buscar el panel por todo el proyecto, subir por `Parent` hasta el `TiaPortal`, comprobar el `TiaPortal`) y TIA acababa liberando el `TiaPortal` del Add-In: fallaba todo, también en el PLC. Regla nueva: en `OnEstado*` solo comprobaciones de tipo y del propio objeto, como en la v2.4.10.
- Se quitan el *Diagnóstico de la selección* y la traza del menú (2.5.1–2.5.9).

## 2.5.9
- Traza del menú *XML*: cada opción que TIA consulta o se pulsa queda apuntada con el tipo de lo seleccionado y si el `TiaPortal` del Add-In sigue vivo, en `Documentos\ExportarXml_menu.log` (antes `%TEMP%`, que en el proceso del Add-In puede ser otro) y en los mensajes de error inesperado (últimos pasos).

## 2.5.8
- Si el `TiaPortal` que recibe el Add-In al cargarse está liberado (la sesión sigue viva: el PLC se lee bien, pero `TiaPortal.Projects` da *disposed*), cada acción usa el del proyecto de lo seleccionado (`Project.Parent`), guardado en un campo. El proyecto a exportar se toma de lo seleccionado. Queda apuntado en `%TEMP%\ExportarXml_menu.log`.

## 2.5.7
- **Causa real de *Access to a disposed object of type TiaPortal***: los recorridos hacia arriba por `Parent` (buscar el HMI, el PLC o el dispositivo de lo seleccionado) llegaban hasta `Project.Parent`, que devuelve un `TiaPortal` nuevo; al finalizarlo el recolector de basura se cerraba la sesión del Add-In con TIA y todo el menú dejaba de funcionar (también en el PLC). Ahora todos los recorridos se paran en el proyecto (`Hmi.Padre`).
- Se quita la búsqueda del `TiaPortal` por `Parent` de la 2.5.6.

## 2.5.6
- *Access to a disposed object of type TiaPortal*: el `TiaPortal` que recibe el Add-In al cargarse puede quedar liberado (al reactivar el Add-In o reabrir el proyecto). Ahora, en cada clic del menú, se toma el `TiaPortal` vivo subiendo por `Parent` desde lo seleccionado; el del constructor queda solo como respaldo.
- Los errores inesperados muestran el tipo de excepción y las primeras líneas de la pila.

## 2.5.5
- `HmiUnified` ya no guarda el `TiaPortal` en un campo estático (sobrevive entre activaciones del Add-In y daba *Access to a disposed object of type TiaPortal*): se pasa por parámetro.

## 2.5.4
- *Importar ficheros* / *Importar carpeta* vuelven a salir sobre un panel Unified: la búsqueda del PLC fallaba con una excepción en el dispositivo HMI y ocultaba las opciones de importar. Ahora cada comprobación del destino va por separado y `ObtenerPlc` no lanza excepciones.

## 2.5.3
- **XML → Exportar pantallas Unified…** (sobre el panel Unified): lista con casillas y buscador para elegir qué pantallas exportar. TIA V20 no pasa al Add-In las pantallas Unified seleccionadas en el árbol, así que el menú *XML* sale vacío sobre una pantalla.
- `ValueConverter` (y cualquier objeto sin nombre en una propiedad con escritura) se exporta como parte con su contenido, no como `{"$ref": ""}`.
- `Variables_referencia.json`: si `Tags` viene vacío, se leen las variables de cada tabla (`TagTables`).
- El panel Unified se identifica por su dispositivo (modelo y referencia) aunque el `Parent` del software no llegue a él: desaparece el aviso falso de "no parece un Unified Comfort Panel".

## 2.5.2
- Menú *XML*: si al decidir qué opciones se ven salta una excepción (por ejemplo, al subir por `Parent` desde un objeto Unified), ya no se ocultan todas: se apunta en `%TEMP%\ExportarXml_menu.log` y el *Diagnóstico de la selección* la muestra.

## 2.5.1
- WinCC Unified: el menú *XML* sale también al seleccionar una pantalla o un grupo de pantallas aunque su `Parent` no llegue al `HmiSoftware` (se busca el panel en los dispositivos del proyecto).
- Nuevo **XML → Diagnóstico de la selección**: sale solo cuando lo seleccionado no se puede exportar ni importar; muestra el tipo de objeto que da TIA y su cadena de `Parent`.

## 2.5.0
- **WinCC Unified (Unified Comfort, MTP1500), en prueba**: *Exportar selección* sobre un panel Unified, un grupo de pantallas o una pantalla guarda cada pantalla en JSON (`HMI_Unified/<panel>/Pantallas/<grupo>/<pantalla>.json`): objetos, propiedades, textos por idioma, dinamizaciones, eventos y scripts. `Variables_referencia.json` con las variables HMI (solo consulta).
- *Importar ficheros* con un panel Unified como destino recrea las pantallas (crea los grupos que falten; al sobrescribir vacía la pantalla y la rellena), copia lo sobrescrito en `Import_XML\copia_unified_<fecha>\`, compila el HMI y deja `resumen_unified_<fecha>.txt`.
- Nuevos `HmiUnified.cs` (lectura y escritura genérica por reflexión) y `Json.cs`. Guía `INSTRUCCIONES_IA.md` §6.8 con el formato.

## 2.4.10
- Guía `INSTRUCCIONES_IA.md`, nueva sección **6.5e GRAPH en XML** (comprobado en V20): pasos, acciones (`N` con evento `S1`, asignaciones también de estructuras y arrays), transiciones (comparadores, contactos, elementos de array), ramas alternativas, `Direct` frente a `Jump`, interfaz (`G7_StepPlus_V6` / `G7_TransitionPlus_V6`) y DB de instancia en XML.
- Errores nuevos en la tabla: DB ≥ 60000, DB de instancia de GRAPH en SD, orden FC / DB de instancia al importar, nombres reservados en GRAPH (`ERROR`…), `Remanence` en InOut, `OR` entre Bool en una acción.
- Lecciones 25.

## 2.4.9
- **GRAPH**: el validador ya no rechaza los XML de bloques GRAPH (cada transición, supervisión y enclavamiento es una red propia con sus UId desde 21; ahora el ámbito del UId es el FlgNet).
- Al exportar, los GRAPH y los DB de instancia que SD no admite ya no salen como *"protegido con know-how"*.

## 2.4.8
- `CLAUDE.md` / `AGENTS.md`: punto de entrada para cualquier IA (qué leer y qué hacer en cada caso, reglas, cómo publicar).
- `GUIA_PRUEBAS.md`: el método de las baterías de prueba paso a paso.
- `herramientas/`: librería `simatic.py` y los generadores de todas las tandas (regeneran exactamente lo que hay en `pruebas/`).

## 2.4.7
- Tanda 7 (bloques, interfaz, OB) comprobada en marcha: 6/6 (`pruebas/tanda7_bloques/`). OB de número fijo sin `AutoNumber`; `Array[*]` en SD; una caja por red si falla.

## 2.4.6
- Un `.s7dcl` con RET (`Return(`) sale como NO VÁLIDO con explicación: TIA V20 no importa RET en SD (ni el suyo). Usar XML, salto o SCL.
- Guía 6.5d: cómo crear cada tipo de OB en XML (`SecondaryType` y entradas), interfaz especial (`Array[*]`, Variant, constantes), Get_AlarmState, T_RESET, T_CONFIG, TMAIL_C. Lecciones 24.

## 2.4.5
- Comunicación en marcha: TCP (compacta y por partes) y Modbus TCP, 4/4 en PLCSIM (`pruebas/tanda4b_com_marcha/`). Patrón de conexión en la guía. Trampa del temporizador que se reinicia solo en SCL. Lecciones 23.

## 2.4.4
- Tanda 3B (27) y tanda 4, comunicación (17): importan y compilan (`pruebas/tanda3b_compilar/`, `pruebas/tanda4_comunicacion/`). Guía: tipos de `LADDR`, sintaxis de comunicación y la regla de **todos los parámetros** en las llamadas SD a FB del sistema. Lecciones 22.

## 2.4.3
- Tanda 3A comprobada en marcha: 20/20 (`pruebas/tanda3a_programa/`). Guía 6.5c y lecciones 21.

## 2.4.2
- El resumen de importación pone el **bloque** delante de cada error de compilación (`T7_S_Pruebas (FC44003) › 22: …`), no solo la línea.

## 2.4.1
- Tanda 2 (fecha/hora y texto) comprobada en marcha: 27/27 (`pruebas/tanda2_fecha_texto/`). Sintaxis en `INSTRUCCIONES_IA.md` 6.5b, con los nombres a evitar (`D1` en SD, `db` en SCL), `CONCAT_DATE_TOD` en SCL y el signo delante en Int → String.
- El HMI clásico de la 2.4.0 sigue **en prueba**.

## 2.4.0 — HMI clásico (en prueba)
- **Exportar selección** sobre un dispositivo con HMI clásico (Basic/Comfort/Advanced) exporta también el panel a `HMI/<panel>/`: pantallas (con sus carpetas), plantillas, emergentes, deslizantes, tablas de variables HMI, scripts VB, listas de textos y de gráficos, conexiones y ciclos. `_info.md` añade el modelo de panel y el recuento.
- **Importar ficheros / carpeta** con un panel HMI seleccionado importa los XML de HMI (`Hmi.*`) en orden de dependencias (conexiones y ciclos → listas → variables → scripts → plantillas → emergentes → pantallas), con pasadas, copia previa de lo que se sobrescribe (`Import_XML\copia_hmi_<fecha>`), adaptación de idiomas y compilación del HMI. Resumen en `Import_XML\resumen_hmi_<fecha>.txt`.
- WinCC Unified todavía no (fase 4): se avisa.
- Acceso a la API de HMI por reflexión: si algo no existe en un panel o en la versión de TIA, se apunta y se sigue.

## 2.3.9
- `INSTRUCCIONES_IA.md` 6.6: sintaxis vista en una segunda exportación real (GET, T_DIAG, `VAR RETAIN`, `VAR CONSTANT`, `DB_SPECIFIC`, `S7_Access`/`S7_Visibility`, valores iniciales de DB en SD, arrays con límite inferior ≠ 0, `S7_BlockComment`). Las multiinstancias del sistema llevan `LibVersion`.
- Comentarios en **todos** los idiomas del proyecto (si solo van en es-ES y se edita en inglés, la columna *Comment* sale vacía).
- `_info.md`/resumen: motivo propio para un DB de instancia sin interfaz (no sale en SD).
- `ROADMAP.md`: fases (PLC V20, exportación ampliada, HMI clásico, HMI Unified, otras versiones).
- Lecciones 19.

## 2.3.8
- Tanda 1 completa: 120 pruebas de instrucciones básicas, todas TRUE en marcha (`pruebas/tanda1_basicas/` y `pruebas/tanda1b_complemento/`). Tabla de nombres SD ≠ nombres TIA en `INSTRUCCIONES_IA.md`.
- `INSTRUCCIONES_IA.md` reescrito: reglas de oro, flujo de trabajo con el usuario, recetas paso a paso por caso (A–M), referencia completa de SD, fuentes y SimaticML con esqueletos comprobados, lista de lo que cierra TIA y tabla de errores con su solución.
- También se bloquean `NORM_X`/`SCALE_X` escritos con plantilla `out_type` (cierran TIA).
- Tanda 1 de instrucciones básicas comprobada en marcha (96 pruebas): `pruebas/tanda1_basicas/`; sintaxis confirmada en `INSTRUCCIONES_IA.md`.

## 2.3.7
- Los documentos SD con `MOVE_BLK`, `UMOVE_BLK`, `MOVE_BLK_VARIANT`, `FILL_BLK` o `UFILL_BLK` escritos como `{ DisableENO := TRUE }` salen como NO VÁLIDO y no se importan: con esa forma TIA Portal se cierra.

## 2.3.6
- *Importar carpeta* ya no coge los `.zip` que haya dentro: con varios paquetes en una carpeta se importaban todos mezclados. Un `.zip` se importa con *Importar ficheros*.

## 2.3.5
- Guarda el proyecto antes de importar: si TIA se cierra durante la importación (algunas cajas SD mal escritas lo provocan), no se pierde el trabajo anterior.

## 2.3.4
- `Import_XML\progreso.txt`: la importación apunta cada paso en disco antes de hacerlo. Si TIA Portal se cierra de golpe, la última línea `IMPORTANDO …` sin `OK` ni `ERROR` dice qué fichero lo ha provocado.

## 2.3.3
- Los XML se adaptan a los idiomas del proyecto antes de importarlos (`Idiomas.cs`): TIA rechazaba un XML con textos en un idioma no activado en el proyecto. Se importa una copia en `Import_XML\adaptado_<fecha>\`; el original no se toca.
- `_info.md` incluye los idiomas del proyecto.

## 2.3.2
- Corregido: al sobrescribir desde una fuente `.scl`, si un bloque había quedado inconsistente (por ejemplo, porque en la misma importación se reimporta un DB que usa) no se podía hacer la copia extra y la fuente fallaba. Ahora esa copia es opcional: la copia buena ya la hace el deshacer antes de empezar.
- `pruebas/importacion/`: contadores de vida (`Vida_OB125`, `Vida_T2_Bits`, `Vida_OB126`) para saber si el programa se está ejecutando.

## 2.3.1
- `INSTRUCCIONES_IA.md` y lecciones: resultados de las pruebas de importación (49 casos en S7-1500): sintaxis SD de ramas OR y de salida, tipos con `Type := AUTO`, FC con retorno, y lista de cajas comprobadas.
- Mensaje más útil cuando un `.s7dcl` falla (TIA no indica la línea).

## 2.3.0
- El Ctrl+Z de TIA no deshace lo que importa el Add-In (comprobado), así que se quitan las transacciones (y el ensayo doble que necesitaban).
- Deshacer propio: antes de importar se copian a XML los objetos que se van a sobrescribir y se apuntan los nuevos (`Import_XML\deshacer_<fecha>\`). Menú nuevo **XML → Deshacer última importación**: borra lo nuevo, restaura lo sobrescrito en su carpeta y compila.

## 2.2.2
- Corregido: "The 'ImportFromDocuments' method only supports the 'Override' option for blocks". Los documentos SD se importan siempre con `Override`; los objetos que ya existen y no se quieren sobrescribir no llegan a importarse (salen como SALTADO).

## 2.2.1
- Corregido: "Commit of a Transaction is not allowed after an exception". TIA no deja confirmar una transacción si dentro ha fallado cualquier llamada, y al importar es normal que un bloque falle en la primera pasada por depender de otro. Ahora se hace un ensayo en una transacción que se deshace (para saber qué entra y en qué orden) y después la importación real, sin fallos, en otra transacción que sí se confirma. Ctrl+Z sigue deshaciendo toda la importación.

## 2.2.0
- El .zip usa `/` en las rutas (antes `\`, que fuera de Windows no se entiende bien).
- Los bloques SCL, que SD de V20 no exporta, salen como fuente `.scl` en `FUENTES/`.
- `_info.md`: columna **Texto** (SD, FUENTE o *solo XML* con el motivo) y nombres de instrucción limpios (`TCON 4.0` en vez de `Array[..] of TCON 4.0`).
- La importación también empareja XML con una fuente de un solo bloque (misma regla de CPU que con SD).
- `INSTRUCCIONES_IA.md`: sintaxis SD de textos, DB, tipos `_.Nombre` y llamadas a FB, y qué hacer con los bloques *solo XML*.

## 2.1.0
- Cada exportación incluye `INSTRUCCIONES_IA.md` (va dentro de la DLL): cómo leer el paquete, qué formato entregar para cada objeto según la CPU, reglas de SD, fuentes y SimaticML, y comprobaciones antes de entregar.
- `_info.md` remite a esas instrucciones y el aviso final de la exportación lo recuerda.

## 2.0.0
- Menú simplificado: **Exportar selección**, **Importar ficheros** e **Importar carpeta**.
- La exportación deja un paquete con XML + SIMATIC SD + `_info.md` (proyecto, versión de TIA, CPU, firmware, objetos e instrucciones usadas) y un `.zip` listo para compartir.
- La importación admite el `.zip` y, si un bloque viene en XML y en SD, elige según la CPU de destino (misma CPU → XML; otra → SD) con el otro formato de reserva.

## 1.4.0
- La importación va dentro de una transacción: Ctrl+Z en TIA la deshace entera.

## 1.3.x
- Exportar e importar SIMATIC SD (`.s7dcl`, `.s7dcf`, `.s7dcs`, `.s7dbd`).
- Las fuentes externas sobrescriben de verdad (copia en XML antes de borrar).
- XML antes que fuentes; lo repetido sale como OMITIDO; sin el aviso XSD de `Document`.

## 1.2.0
- Importar fuentes externas (`.scl`, `.udt`, `.db`, `.awl`).
- Validación previa de los XML: reglas aprendidas de errores reales y XSD de TIA.
