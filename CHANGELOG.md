# Cambios

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
