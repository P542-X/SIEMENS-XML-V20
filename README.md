# SIEMENS XML

Add-In para **TIA Portal V20** para exportar e importar programas de PLC sin depender del modelo de CPU: XML (SimaticML), SIMATIC SD (.s7dcl, .s7dcf, .s7dcs, .s7dbd), fuentes externas (.scl, .udt, .db, .awl) y paquetes .zip, desde el menú contextual del árbol del proyecto.

## Uso

Clic derecho en el árbol del proyecto → **XML**. Solo hay cuatro opciones:

| Menú | Qué hace |
|---|---|
| **Exportar selección** | Exporta lo seleccionado (bloque, carpeta, *Bloques de programa*, UDT, tablas o el PLC entero) y deja **un .zip** listo para compartir. |
| **Importar ficheros (xml, sd, fuentes, zip)…** | Importa ficheros sueltos o el .zip de una exportación. |
| **Importar carpeta…** | Importa todo lo importable de una carpeta y sus subcarpetas (sin los `.zip`: esos, con *Importar ficheros*). |
| **Deshacer última importación** | Deja el PLC como estaba antes de la última importación. |

### Pensado para trabajar con una IA

1. **Exportar selección** → sale un `.zip`.
2. Se lo pasas a la IA y le dices qué quieres. El paquete lleva `INSTRUCCIONES_IA.md`, que le explica cómo leer el programa y **qué formato entregar para cada cosa** según la CPU, de modo que lo que te devuelva se importe a la primera.
3. La IA te devuelve un `.zip` → **Importar ficheros…** y lo eliges tal cual.
4. Si algo falla, le pasas el `Import_XML\resumen_<fecha>.txt` y lo corrige.

Las instrucciones están en `ExportarXml/IA/INSTRUCCIONES_IA.md`; se compilan dentro de la DLL y se copian a cada exportación. Si aprendes algo nuevo (un error de importación, la sintaxis de una caja), añádelo ahí y recompila.

### Qué hay en una exportación

```
<carpeta del proyecto>\Export\<proyecto>_<fecha>.zip      <- el fichero para compartir
<carpeta del proyecto>\Export\<fecha>\
    INSTRUCCIONES_IA.md   cómo leer el paquete y qué entregar para importar (para la IA)
    XML\...        todo en SimaticML: copia exacta, con títulos y comentarios
    SD\...         los bloques en SIMATIC SD: sin versiones de instrucción, vale para cualquier CPU
    FUENTES\...    los bloques SCL como fuente .scl (SD de V20 no admite SCL)
    _info.md       proyecto, versión de TIA, CPU y firmware, lista de objetos e instrucciones usadas
    _info.txt      CPU de origen de cada carpeta (lo lee el importador)
    _resumen.txt   detalle y errores de la exportación
```

### Importar en cualquier CPU

Si un bloque viene en XML y en SD (como en el .zip de una exportación), el Add-In elige solo:

- **Misma CPU y firmware que el origen** → importa el **XML** (copia exacta).
- **Otra CPU o firmware** → importa el **SD**: TIA pone a cada instrucción la versión que admite esa CPU.
- **Origen desconocido** → XML.

Si el elegido falla, prueba con el otro formato. La primera línea del resumen dice la CPU de destino y cada bloque indica qué formato se usó y por qué. Lo único que no se puede salvar es una instrucción que no existe en la CPU de destino (por ejemplo, algo exclusivo de S7-1500 en una S7-1200): sale como error.

La importación clasifica cada fichero (UDT, tabla de variables, FB, FC, OB, DB, fuente externa), lo importa en orden de dependencias (UDT → tablas → FB/FC → OB/DB global → DB de instancia), pregunta si sobrescribir los objetos existentes, compila el PLC y deja un resumen en `<carpeta del proyecto>\Import_XML\`.

### Deshacer la importación

El Ctrl+Z de TIA no deshace lo que hace un Add-In, así que el Add-In tiene su propio deshacer:

- Antes de importar, exporta a XML cada objeto que se va a sobrescribir y apunta cuáles son nuevos, en `Import_XML\deshacer_<fecha>\`.
- **XML → Deshacer última importación** (clic derecho en el PLC) borra los nuevos, restaura los sobrescritos en su carpeta y compila. Pide confirmación y solo se puede usar una vez por importación.
- Funciona aunque se haya compilado, guardado o cerrado TIA.

### Validación previa de los XML

Antes de importar, cada XML pasa por `Validador.cs`:

- **Reglas propias** (bloquean la importación del fichero, que sale como `NO VÁLIDO` en el resumen con la línea del problema). Son errores reales que TIA V20 rechaza y el XSD no detecta: SimaticML de una versión mayor que V20, `ID` repetidos, `UId` repetidos en una red, bloque SCL con más de una CompileUnit, `AccessModifier` en una red SCL y cables (`IdentCon` / `NameCon`) que apuntan a un `UId` que no existe en `Parts`.
- **XSD de TIA Openness** (`PublicAPI\V20\Schemas`, si está instalado): sus mensajes salen como `aviso XSD` con línea y columna, pero **no bloquean**, para que un falso positivo no impida importar un fichero que TIA sí acepta. La primera línea del resumen indica si se han usado los XSD.

### Fuentes externas

Los `.scl`, `.udt`, `.db` y `.awl` se añaden a *Fuentes externas*, se generan sus bloques (`GenerateBlocksFromSource`) y se borra la fuente. `GenerateBlocksFromSource` **no sobrescribe** un bloque que ya existe (lo deja con el código viejo), así que al elegir sobrescribir el Add-In exporta antes cada objeto de la fuente a XML en `Import_XML\copia_<fecha>\`, lo borra y lo regenera. Si la copia no se puede hacer, no borra nada y lo marca como error. El Add-In lee las cabeceras (`TYPE`, `FUNCTION_BLOCK`, `FUNCTION`, `DATA_BLOCK`, `ORGANIZATION_BLOCK`) para saber qué objetos contiene cada fuente y si ya existen. Los bloques generados quedan en la raíz de *Bloques de programa* (Openness no deja elegir carpeta).

Si en la misma importación un objeto viene en un XML y también en una fuente (por ejemplo, una carpeta con `fuentes_respaldo/`), se importan primero los XML y las fuentes solo crean lo que falte. Lo repetido sale como `OMITIDO` en el resumen, no como error.

### SIMATIC SD (V20)

Formato de texto de TIA V20 para bloques: `.s7dcl` (KOP), `.s7dcf` (FUP), `.s7dcs` (SCL) y `.s7dbd` (DB), con un `.s7res` al lado para los textos. Se exporta con `ExportAsDocuments` y se importa con `ImportFromDocuments` (sobrescribe si eliges *Sí*). Los UDT y las tablas de variables no van en SD: van en XML, que no lleva versiones de instrucción. Un documento contiene un solo bloque, y su nombre de fichero tiene que ser el del bloque.

Es la forma recomendada de **generar KOP**: las instrucciones se escriben por nombre, sin `Version` ni `UId`, y TIA pone la versión al importar. Las plantillas de la caja van entre `{ }` (`Card`, `DisableENO`, `Type := AUTO`…); ver `SimaticML_V20_lecciones.md` y `referencia/SD/`. Para aprender la sintaxis de cualquier instrucción, exporta a SD un bloque que la contenga.

## Carpetas del repositorio

| Carpeta / fichero | Qué es |
|---|---|
| `ExportarXml/` | Código del Add-In |
| `ExportarXml/IA/INSTRUCCIONES_IA.md` | Instrucciones para la IA (van dentro de la DLL y se copian a cada exportación) |
| `SimaticML_V20_lecciones.md` | Lo aprendido generando e importando bloques |
| `referencia/` | Bloques de ejemplo comprobados (SD y XML) |
| `pruebas/importacion/` | Batería de 49 pruebas con autocomprobación en el PLC |
| `CHANGELOG.md` | Cambios por versión |

## Limitaciones

- Solo XML SimaticML de V20 o anteriores.
- Los bloques F (Safety) no se pueden importar por Openness.
- Los bloques con protección de know-how no exportan su código.

## Requisitos

- TIA Portal V20 instalado con la opción **TIA Portal Openness**.
- .NET SDK (para compilar) y .NET Framework 4.8.

## Compilar e instalar

```bat
dotnet build -c Release
"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe" -f Config.xml -l publisher.log -o ExportarXml.addin -c
```

Con TIA Portal cerrado, en un cmd como administrador:

```bat
copy /Y ExportarXml.addin "C:\Program Files\Siemens\Automation\Portal V20\AddIns\"
```

Después, en TIA Portal: task card **Add-Ins** → activar "Exportar e importar XML" y aceptar los permisos.

## Estructura

| Fichero | Contenido |
|---|---|
| `AddInProvider.cs` | Registro del Add-In en el árbol del proyecto |
| `ExportarXmlAddIn.cs` | Menú contextual y exportación (XML + SD + info + zip) |
| `Cpu.cs` | CPU y firmware de un PLC, y `_info.txt` del paquete |
| `Deshacer.cs` | Copias antes de importar y menú *Deshacer última importación* |
| `Idiomas.cs` | Adapta los textos de los XML a los idiomas del proyecto |
| `Progreso.cs` | `Import_XML\progreso.txt`: paso a paso, para localizar un cierre de TIA |
| `Importador.cs` | Importación (XML, SD, fuentes, zip), elección XML/SD según la CPU y compilación |
| `Validador.cs` | Comprobación previa de los XML: reglas propias y XSD de TIA |
| `ExportadorSd.cs` | Parte SIMATIC SD de la exportación |
| `Dialogos.cs` | Diálogos de Windows (ficheros, carpeta, mensajes) |
| `Config.xml` | Configuración del Add-In Publisher (permisos, versión) |

Las DLL de Siemens no se incluyen en el repositorio; se referencian desde la instalación de TIA Portal.
