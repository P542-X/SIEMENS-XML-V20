# SIEMENS XML

Add-In para **TIA Portal V20** que permite exportar e importar bloques en XML (SimaticML) , fuentes externas (.scl, .udt, .db, .awl) y documentos SIMATIC SD (.s7dcl, .s7dcf, .s7dcs, .s7dbd) desde el menú contextual del árbol del proyecto.

## Funciones

Clic derecho en el árbol del proyecto → **XML**:

- **Exportar selección a XML**: bloques (OB, FB, FC, DB), carpetas, *Bloques de programa*, tipos de datos PLC (UDT), tablas de variables o el PLC completo. Se guarda en `<carpeta del proyecto>\Export_XML\<fecha_hora>\`, respetando la estructura de carpetas, con un `_resumen.txt`.
- **Exportar selección a SIMATIC SD (texto)**: exporta bloques a `<carpeta del proyecto>\Export_SD\<fecha_hora>\` en formato SIMATIC SD.
- **Importar ficheros XML / fuentes…**: selección múltiple de `.xml`, `.scl`, `.udt`, `.db`, `.awl`, `.s7dcl`, `.s7dcf`, `.s7dcs` y `.s7dbd`.
- **Importar carpeta XML / fuentes (con subcarpetas)…**: importa todos los ficheros de esos tipos de una carpeta.

La importación clasifica cada fichero (UDT, tabla de variables, FB, FC, OB, DB, fuente externa), lo importa en orden de dependencias (UDT → tablas → FB/FC → OB/DB global → DB de instancia), pregunta si sobrescribir los objetos existentes, compila el PLC y deja un resumen en `<carpeta del proyecto>\Import_XML\`.

### Deshacer la importación

Toda la importación va dentro de una transacción de TIA, así que **Ctrl+Z en TIA la deshace entera de una vez** (aparece como *Importar XML (N ficheros)* en el historial de deshacer). Si algún fichero no se deja importar dentro de la transacción, se reintenta fuera de ella y sale en el resumen con la nota *fuera de la transacción: no se deshace con Ctrl+Z*. La última línea del resumen dice si la importación se puede deshacer y qué queda fuera. La compilación va siempre fuera de la transacción.

### Validación previa de los XML

Antes de importar, cada XML pasa por `Validador.cs`:

- **Reglas propias** (bloquean la importación del fichero, que sale como `NO VÁLIDO` en el resumen con la línea del problema). Son errores reales que TIA V20 rechaza y el XSD no detecta: SimaticML de una versión mayor que V20, `ID` repetidos, `UId` repetidos en una red, bloque SCL con más de una CompileUnit, `AccessModifier` en una red SCL y cables (`IdentCon` / `NameCon`) que apuntan a un `UId` que no existe en `Parts`.
- **XSD de TIA Openness** (`PublicAPI\V20\Schemas`, si está instalado): sus mensajes salen como `aviso XSD` con línea y columna, pero **no bloquean**, para que un falso positivo no impida importar un fichero que TIA sí acepta. La primera línea del resumen indica si se han usado los XSD.

### Fuentes externas

Los `.scl`, `.udt`, `.db` y `.awl` se añaden a *Fuentes externas*, se generan sus bloques (`GenerateBlocksFromSource`) y se borra la fuente. `GenerateBlocksFromSource` **no sobrescribe** un bloque que ya existe (lo deja con el código viejo), así que al elegir sobrescribir el Add-In exporta antes cada objeto de la fuente a XML en `Import_XML\copia_<fecha>\`, lo borra y lo regenera. Si la copia no se puede hacer, no borra nada y lo marca como error. El Add-In lee las cabeceras (`TYPE`, `FUNCTION_BLOCK`, `FUNCTION`, `DATA_BLOCK`, `ORGANIZATION_BLOCK`) para saber qué objetos contiene cada fuente y si ya existen. Los bloques generados quedan en la raíz de *Bloques de programa* (Openness no deja elegir carpeta).

Si en la misma importación un objeto viene en un XML y también en una fuente (por ejemplo, una carpeta con `fuentes_respaldo/`), se importan primero los XML y las fuentes solo crean lo que falte. Lo repetido sale como `OMITIDO` en el resumen, no como error.

### SIMATIC SD (V20)

Formato de texto de TIA V20 para bloques: `.s7dcl` (KOP), `.s7dcf` (FUP, desde V20 Update 4), `.s7dcs` (SCL) y `.s7dbd` (DB), con un `.s7res` al lado para los textos. Se exporta con `ExportAsDocuments` y se importa con `ImportFromDocuments` (sobrescribe si eliges *Sí*). Un documento contiene un solo bloque, y su nombre de fichero tiene que ser el del bloque.

Es la forma recomendada de **generar KOP**: las instrucciones se escriben por nombre, sin `Version` ni `UId`, y TIA pone la versión al importar. Solo algunas cajas llevan `{ S7_Templates := "SrcType := Int" }`. Para aprender la sintaxis de cualquier instrucción, exporta a SD un bloque que la contenga.

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
| `ExportarXmlAddIn.cs` | Menú contextual y exportación |
| `Importador.cs` | Lógica de importación (XML y fuentes externas) y compilación |
| `Validador.cs` | Comprobación previa de los XML: reglas propias y XSD de TIA |
| `ExportadorSd.cs` | Exportación a SIMATIC SD |
| `Dialogos.cs` | Diálogos de Windows (ficheros, carpeta, mensajes) |
| `Config.xml` | Configuración del Add-In Publisher (permisos, versión) |

Las DLL de Siemens no se incluyen en el repositorio; se referencian desde la instalación de TIA Portal.
