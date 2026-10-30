# Hoja de ruta

Objetivo: que una IA pueda **leer un proyecto de TIA Portal exportado y devolver cambios que se importen a la primera**, en cualquier CPU y versión de TIA. Cada fase se cierra con una batería de pruebas que **importa, compila y funciona** (en PLCSIM o en la simulación de runtime del HMI), y lo aprendido pasa a `ExportarXml/IA/INSTRUCCIONES_IA.md`.

Método en cada tanda:
1. El usuario crea a mano un bloque o una pantalla con lo que toca probar y lo exporta (sintaxis real, sin adivinar).
2. Se generan las pruebas (una por elemento, con autocomprobación).
3. Se importan, se compilan y se comprueban en marcha.
4. Lo que falle se corrige; lo que funcione se documenta como comprobado.

## Fase 1. PLC en TIA V20

| Tanda | Contenido | Estado |
|---|---|---|
| 1 | Instrucciones básicas (bits, tiempo, contadores, comparación, matemáticas, mover, conversión, palabras, desplazamiento, control) | **Hecha: 120/120** |
| 2 | Fecha y hora; String y Char | Pendiente (`Aprender_T2`) |
| 3 | Gestión de datos y de bloques, direccionamiento, interrupciones, diagnóstico, avisos, recetas | Pendiente |
| 4 | Comunicación (OUC, S7, Modbus TCP, OPC UA) | Pendiente (sobre todo importar y compilar) |
| 5 | Tecnología (PID, contaje, Motion) | Pendiente (necesita objetos tecnológicos) |
| 6 | FUP (`.s7dcf`) | Pendiente |
| 7 | Tipos de bloque e interfaz (Variant, Array[*], constantes, remanencia, DB de array, OB de cada tipo) | Pendiente |

## Fase 2. Exportación ampliada

- Tablas de observación y forzado (XML).
- Propiedades de la CPU y de los módulos en `_info.md` (tiempo de ciclo, protección, IP, OPC UA, servidor web…).
- Hardware y red en AML (CAx) para leerlos, y lo que se pueda volver a importar.
- Configuración de objetos tecnológicos.

## Fase 3. HMI clásico (Basic, Comfort, Advanced, Professional)

Openness exporta e importa en XML: variables HMI y tablas, pantallas, plantillas, pantallas emergentes, listas de textos y de gráficos, scripts VB, conexiones y ciclos.

| Tanda | Contenido |
|---|---|
| H1 | Exportar el HMI en el Add-In (junto al PLC, en `HMI/`), `_info.md` con panel, tamaño y conexiones |
| H2 | Variables HMI (internas y conectadas al PLC) y tablas |
| H3 | Pantallas: objetos básicos (campos E/S, botones, textos, rectángulos), eventos y animaciones |
| H4 | Plantillas, pantallas emergentes, navegación, listas de textos y de gráficos |
| H5 | Scripts VB, ciclos, recetas |
| H6 | Avisos: Openness no exporta en XML los avisos de usuario de Basic/Comfort/Advanced → buscar otra vía (listas de textos, avisos de PLC) |

Riesgos conocidos: importar una pantalla con un faceplate que no está en la librería del proyecto cierra TIA → comprobar librerías antes de importar. Verificación: compilar el HMI y abrirlo en la simulación de runtime.

## Fase 4. HMI WinCC Unified

Openness da acceso a variables, avisos, registros, pantallas y sus objetos a través del modelo de objetos, **sin XML de pantallas**. Plan: leer cada objeto con la API y guardarlo en un formato propio (JSON) al exportar; recrearlo con la API al importar.

| Tanda | Contenido |
|---|---|
| U1 | Exportar Unified a JSON (pantallas, objetos con sus propiedades, variables, avisos) |
| U2 | Importar variables y avisos |
| U3 | Importar pantallas y objetos básicos |
| U4 | Dinamizaciones, eventos y scripts JavaScript |
| U5 | Faceplates y librerías |

## Fase 5. Otras versiones de TIA (V18, V19, V21…)

- Proyecto del Add-In por versión (cada una con su `Siemens.Engineering.AddIn.dll`).
- SIMATIC SD solo existe desde V20: en versiones anteriores, lógica en XML o fuentes SCL.
- Pasar las mismas baterías y documentar las diferencias por versión.

## Fuera del alcance de Openness

Bloques F (Safety), bloques protegidos con know-how, contenido de GRAPH en texto.
