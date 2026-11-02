# Traspaso: SIEMENS-XML-V20 (para continuar en otro chat)

Pega este fichero al empezar un chat nuevo. Resume el proyecto, cómo trabajamos, qué está hecho y qué toca ahora.

---

## 1. Qué es

**Add-In de TIA Portal V20** (C#, Openness) para **exportar e importar** programas de PLC pensado para trabajar con una IA:

1. El usuario exporta (*XML → Exportar selección*) → sale un `.zip` con `XML/`, `SD/` (SIMATIC SD), `FUENTES/` (SCL), `_info.md` (CPU, firmware, idiomas, objetos, instrucciones) e `INSTRUCCIONES_IA.md`.
2. Se lo pasa a la IA, que lo lee y le devuelve un `.zip` para importar.
3. El usuario lo importa (*XML → Importar ficheros…*, el .zip sin descomprimir). El Add-In elige formato según la CPU, valida, compila, deja resumen y se puede deshacer (*XML → Deshacer última importación*).

- **Repositorio (público):** https://github.com/P542-X/SIEMENS-XML-V20 — rama `main`, última etiqueta **v2.3.8** (commit `35f52b7`).
- **Documentos clave del repo** (leerlos antes de generar nada):
  - `ExportarXml/IA/INSTRUCCIONES_IA.md` → guía completa para la IA: reglas, recetas por caso, **sintaxis comprobada** de SD, fuentes y XML, errores y soluciones. Va dentro de la DLL y se copia a cada exportación.
  - `SimaticML_V20_lecciones.md` → todo lo aprendido (apartados 1–18).
  - `ROADMAP.md` → fases.
  - `referencia/` → bloques de ejemplo comprobados (`SD/Cajas_basicas_V20` es una exportación real con 24 cajas).
  - `pruebas/` → baterías: `importacion/` (49), `tanda1_basicas/` (96), `tanda1b_complemento/` (24).
  - `CHANGELOG.md`.

## 2. Cómo trabajamos

- Hablar en **castellano de España**. **Comando concreto y listo para copiar primero**, explicación después y breve.
- **No inventar sintaxis.** Para cada tanda nueva, el usuario crea a mano una FC (o pantalla) con las cajas a probar, la exporta y me pasa el .zip; con esa sintaxis real se generan las pruebas.
- Pruebas: **una FC por instrucción** (si falla, solo cae esa), con **autocomprobación**: cada prueba pone un Bool a TRUE en un DB de resultados, un contador `Vida` sube cada ciclo, una FC SCL cuenta los TRUE (`OK` = `Total`, `Todo_OK`), un OB de ciclo (XML) lo llama todo.
- Entregar siempre **un .zip** con `XML/`, `SD/`, `FUENTES/`, `LEEME.md`.
- Tras importar, el usuario pasa el **resumen** (`Import_XML\resumen_*.txt`); si TIA se cierra, `Import_XML\progreso.txt` (el último `IMPORTANDO` sin `OK`/`ERROR` es el culpable).
- Para publicar: zip del repo + prompt para el agente del usuario (o comandos `xcopy` + `git`), y comprobar luego en GitHub (`git ls-remote`).

## 3. Entorno de pruebas del usuario

- Proyecto **TESTXML** (limpio, solo pruebas), idioma del proyecto **en-US**.
- **PLCSIM** con **CPU 1518-3 PN, 6ES7 518-3AT10-0AB0, FW V4.0** (la instancia tiene que ser igual a la del proyecto; arrancar con *Start simulation* desde TIA). *Support simulation during block compilation* activado.
- Números usados: T2/T3 (pruebas iniciales) 40900–40940, OB 125/126; tanda 1: FC 41000–41063, DB 41900/41901, OB 127; tanda 1b: FC 42000–42024, DB 42900/42901, OB 128, DB de instancia `T5_Rt_L`/`T5_Ft_L`.
- Compilar y construir el Add-In:
  ```bat
  cd "<repo>\SIEMENS-XML-V20\ExportarXml" && dotnet build -c Release
  "C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe" -f Config.xml -l publisher.log -o ExportarXml.addin -c
  :: con TIA cerrado, cmd como administrador:
  copy /Y ExportarXml.addin "C:\Program Files\Siemens\Automation\Portal V20\AddIns\"
  ```

## 4. Estado del Add-In (v2.3.8)

Menú *XML*: **Exportar selección** · **Importar ficheros (xml, sd, fuentes, zip)** · **Importar carpeta** (sin coger .zip) · **Deshacer última importación**.

Hace:
- Exporta XML + SD + fuentes SCL (SD de V20 no exporta SCL) + `_info.md`/`_info.txt` + `INSTRUCCIONES_IA.md` + zip con `/`.
- Importa XML, SD (`ImportFromDocuments`, siempre `Override`), fuentes (borra y regenera con copia previa), zip. Empareja XML/SD/fuente del mismo bloque: misma CPU → XML; otra CPU → SD/fuente; si falla, el otro.
- Valida XML (reglas propias + XSD de TIA como aviso). Bloquea SD que cierran TIA (`MOVE_BLK{ DisableENO… }`, `FILL_BLK…`, `NORM_X{ … out_type }`).
- Adapta los idiomas de los XML a los del proyecto. Guarda el proyecto antes de importar. Deshacer propio (copias en `Import_XML\deshacer_<fecha>\`). `progreso.txt`.
- Ficheros: `ExportarXmlAddIn.cs`, `Importador.cs`, `ExportadorSd.cs`, `Validador.cs`, `Cpu.cs`, `Idiomas.cs`, `Deshacer.cs`, `Progreso.cs`, `Dialogos.cs`, `IA/INSTRUCCIONES_IA.md`.

Lo que NO funcionó y se quitó: transacciones de Openness (Ctrl+Z de TIA no deshace lo del Add-In; una excepción invalida la transacción).

## 5. Lo más importante aprendido (detalle en INSTRUCCIONES_IA.md)

- Lógica KOP → **SD** (sin versiones, vale para cualquier CPU). SCL → **fuente**. UDT/DB/tablas/OB → **XML**.
- **El nombre de la caja en SD no siempre es el de TIA**: `MoveBlockI` (MOVE_BLK), `FillBlockI`, `Normalize` (NORM_X), `Scale_X`, `InRange`/`OutRange` (pin `IN`), `NOK`, `S_RS`, `S_BitfieldCoil`/`R_BitfieldCoil`, `Inc{ DestType := Int }( var )`, `MIN{ value_type := Int; card := 2 }`…
- `Type := AUTO` solo en Add/Sub/Mul/Div; el resto `SrcType := <tipo>`. Al menos una entrada con tipo.
- OR: `wire#w1` en medio de una rama y la otra `END_RUNG wire#w1`.
- `IEC_COUNTER`: `.QU`/`.QD`. R_TRIG/F_TRIG no en DB global. SCL: constantes con tipo en funciones genéricas; `CountOfElements` solo con Variant.
- Si una importación SD falla, TIA no dice la línea → un elemento por bloque.

## 6. Fases (ROADMAP.md)

| Fase | Contenido | Estado |
|---|---|---|
| **1. PLC V20** | Tandas 1–7 | **Tanda 1 hecha (120/120)**. Tanda 2 pendiente |
| **2. Exportación ampliada** | Tablas de observación, propiedades CPU/módulos, hardware y red (AML), objetos tecnológicos | Pendiente |
| **3. HMI clásico** (Comfort/Advanced) | H1 exportar HMI en el Add-In → H2 variables → H3 pantallas → H4 plantillas/emergentes/listas → H5 scripts/ciclos/recetas → H6 avisos (sin XML en Openness) | Pendiente; el usuario usa **Comfort y Unified** |
| **4. HMI Unified** | Formato propio (JSON) leído/escrito con la API: U1 exportar → U2 variables/avisos → U3 pantallas → U4 dinamizaciones/JS → U5 faceplates | Pendiente |
| **5. Otras versiones de TIA** | V18, V19, V21 (SD solo desde V20) | Pendiente |

Tandas de la fase 1: 1 básicas ✔ · 2 fecha/hora y String/Char · 3 gestión de datos/bloques, interrupciones, diagnóstico, avisos, recetas · 4 comunicación · 5 tecnología · 6 FUP · 7 tipos de bloque e interfaz.

## 7. Siguiente paso (donde lo dejamos)

**A. Tanda 2 (el usuario la está preparando):** FC `Aprender_T2` en KOP en TESTXML, una caja por red, compilada y exportada con *Exportar selección*:
- Fecha y hora: T_CONV (Time→DInt y DInt→Time), T_ADD y T_SUB (DTL ± Time), T_DIFF (DTL), T_COMBINE, RD_LOC_T, WR_LOC_T, WR_SYS_T, CMP == con dos DTL.
- Texto: S_CONV (Int→String y String→Int), LEFT, RIGHT, MID, DELETE, INSERT, REPLACE, FIND, ATH, HTA, JOIN, SPLIT (si existen), CMP == con dos Char.
Con esa exportación: generar las pruebas de la tanda 2 (como `pruebas/tanda1b_complemento`), importar, compilar, comprobar en PLCSIM y documentar.

**B. HMI clásico, H1:** preparar **Add-In v2.4.0** que exporte el HMI clásico (pantallas, plantillas, variables HMI, listas de textos/gráficos, scripts VB, conexiones) a `HMI/` del paquete y describa el panel en `_info.md`. El usuario añadirá a TESTXML un **panel Comfort** (p. ej. TP700 Comfort) conectado a `PLC_1` con 2–3 pantallas sencillas y unas variables, lo exportará y con eso se aprende el formato real.

**C. Pendiente menor:** en `pruebas/tanda1b_complemento/LEEME.md`, `R_BitfieldCoil` y `FillBlockU` ya no son "deducidas" (24/24 en marcha).

## 8. Cuidado

- El usuario no debe **descomprimir varios zips en la misma carpeta** ni usar *Importar carpeta* sobre una carpeta con paquetes mezclados (se importó lo que no tocaba y TIA se cerró).
- En el proyecto de planta (MES Pintura 2) PLCSIM se para por **tiempo de ciclo**: las pruebas van siempre en TESTXML.
- El repo es **público**: no subir datos de la planta (IP, nombres de la instalación, empresa). Comprobar con `git grep` antes de cada publicación.
