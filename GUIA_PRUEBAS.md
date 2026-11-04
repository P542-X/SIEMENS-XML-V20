# Guía de pruebas: cómo comprobar instrucciones nuevas en TIA Portal V20

Todo lo que pone *comprobado* en `ExportarXml/IA/INSTRUCCIONES_IA.md` ha pasado por este método. Sirve para añadir instrucciones, tipos de bloque o versiones de TIA.

Hay tres niveles de comprobación, y conviene saber hasta cuál llega cada prueba:

| Nivel | Qué demuestra |
|---|---|
| **Importa** | La sintaxis (nombre de la caja, plantillas, parámetros) es correcta |
| **Compila** | Los tipos de todos los parámetros son correctos y la instrucción existe en esa CPU |
| **En marcha** | Funciona: el resultado calculado en la CPU (PLCSIM) es el esperado |

## 1. Preparar el entorno

1. Un **proyecto de TIA vacío** solo para pruebas (nunca el de una planta: su programa puede parar PLCSIM por tiempo de ciclo).
2. Una CPU igual a la que se quiere probar (p. ej. CPU 1518-3 PN, FW V4.0).
3. En *Project → Properties → Protection*: **Support simulation during block compilation**.
4. **PLCSIM** arrancado desde TIA con *Start simulation*: así la instancia es la misma CPU que la del proyecto.
5. El Add-In instalado (la última versión: el resumen dice en qué bloque está cada error de compilación y `progreso.txt` dice qué fichero cerró TIA).

## 2. Aprender la sintaxis real (no adivinar)

1. El usuario crea a mano **un bloque `Aprender_…`** en KOP (FC, o FB si hay instancias) con **una caja por red**, los pines conectados a variables temporales del tipo que pida la caja (*Define tag* crea el tipo correcto) o a constantes, y una bobina detrás de las que acaban en `>>`.
2. **Tiene que compilar con 0 errores.** Un bloque sin compilar sale en SD con cajas incompletas (p. ej. `Return()` vacío) y sin XML.
3. Lo exporta con *XML → Exportar selección* y pasa el .zip.
4. De `SD/<bloque>.s7dcl` se copia, para cada caja: **el nombre SD** (puede no ser el de TIA), **las plantillas** `{ … }`, **todos los parámetros** y su orden (en SD las llamadas a FB del sistema llevan todos, los vacíos como `PARAM :=  ,`), y de la interfaz los tipos y atributos (`InstructionName`, `LibVersion`).

## 3. Generar la batería

Con `herramientas/simatic.py` (ver `herramientas/README.md`), tomando una tanda parecida como plantilla:

- **Una FC por instrucción** (si algo no importa, solo cae ese bloque).
- **Autocomprobación**: cada prueba calcula algo y lo compara con el valor esperado; si coincide, pone un `Bool` a TRUE en un **DB de resultados**.
- En el DB de resultados: un `Bool` por prueba, **`Vida`** (sube cada ciclo: demuestra que el programa se ejecuta), **`OK`** (cuántas TRUE), **`Total`** y **`Todo_OK`** (`OK = Total`). Una FC SCL `…_Contar` calcula `OK` y `Todo_OK`.
- **Un OB de ciclo en XML** (número ≥ 123 libre) que llama a todas las FC.
- **Datos de entrada en un DB** (XML) con valores iniciales; nunca todas las entradas de una caja como constantes sin tipo.
- **Números de bloque fijos** por prueba y en un rango libre (p. ej. 41000–41999 para una tanda); al corregir un bloque, **mantener su número**.
- Para lo que **no se puede ejecutar** sin hardware (diagnóstico, periferia, comunicaciones con otros equipos, tarjeta de memoria): FC que **no llama ningún OB**; la prueba es que importen y compilen.
- Un `LEEME.md` con qué prueba cada `Bool`, los números usados y los pasos.

```
cd herramientas
python tanda2_fecha_texto.py            # genera salida/tanda2_fecha_texto/ y salida/tanda2_fecha_texto.zip
```

## 4. Importar y leer el resultado

1. *XML → Importar ficheros…* → el .zip **sin descomprimir** (no usar *Importar carpeta* con varios paquetes dentro).
2. **Resumen** (`Import_XML\resumen_<fecha>.txt`):
   - `ERROR … Failure` en un `.s7dcl` → sintaxis SD (TIA no dice la línea): comparar con la exportación real; separar en una caja por red.
   - `NO VÁLIDO` → el Add-In lo frenó antes (cajas que cierran TIA, RET en SD, reglas del XML).
   - Errores de compilación → `Bloque › línea/red: mensaje`; buscar el mensaje en la tabla de errores de la guía.
3. Si TIA se cierra: `Import_XML\progreso.txt`, la última línea `IMPORTANDO` sin `OK` ni `ERROR` es la culpable.
4. Corregir y volver a importar **solo lo corregido** (con *Sí* a sobrescribir) o *Deshacer última importación* y entregar todo de nuevo.

## 5. Comprobar en marcha

1. Compilar con 0 errores, cargar en PLCSIM (*Software (all)*, DB en *Reinitialize*), CPU en **RUN** (STOP → RUN si hay OB de arranque).
2. Abrir el DB de resultados con *Monitor all*: `Vida` sube, `OK = Total`, `Todo_OK = TRUE`.
3. Si todo sale FALSE: la CPU está en STOP o no se cargó (mirar *Diagnostics buffer*).
4. Si alguna sale FALSE: revisar la lógica de la prueba antes que la instrucción (p. ej. un temporizador `#T(IN := NOT #T.Q)` no da pulso en S7-1500; Program_Alarm con la señal cambiando cada ciclo da error).

## 6. Documentar

1. `INSTRUCCIONES_IA.md`: la sintaxis nueva en su tabla, marcada como **comprobada** (y hasta qué nivel), y cada trampa encontrada en *Lo que NO hay que hacer* o en la tabla de errores.
2. `SimaticML_V20_lecciones.md`: un apartado nuevo con lo aprendido y el porqué.
3. `pruebas/<tanda>/` con la batería generada y su resultado en el título del `LEEME.md`; el generador en `herramientas/`.
4. `ROADMAP.md` y `CHANGELOG.md`.
