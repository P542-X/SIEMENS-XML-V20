# SIEMENS XML

Add-In para **TIA Portal V20** que permite exportar e importar bloques en XML (SimaticML) desde el menú contextual del árbol del proyecto.

## Funciones

Clic derecho en el árbol del proyecto → **XML**:

- **Exportar selección a XML**: bloques (OB, FB, FC, DB), carpetas, *Bloques de programa*, tipos de datos PLC (UDT), tablas de variables o el PLC completo. Se guarda en `<carpeta del proyecto>\Export_XML\<fecha_hora>\`, respetando la estructura de carpetas, con un `_resumen.txt`.
- **Importar ficheros XML…**: selección múltiple de ficheros.
- **Importar carpeta XML (con subcarpetas)…**: importa todos los `.xml` de una carpeta.

La importación clasifica cada fichero (UDT, tabla de variables, FB, FC, OB, DB), lo importa en orden de dependencias (UDT → tablas → FB/FC → OB/DB global → DB de instancia), pregunta si sobrescribir los objetos existentes, compila el PLC y deja un resumen en `<carpeta del proyecto>\Import_XML\`.

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
| `Importador.cs` | Lógica de importación y compilación |
| `Dialogos.cs` | Diálogos de Windows (ficheros, carpeta, mensajes) |
| `Config.xml` | Configuración del Add-In Publisher (permisos, versión) |

Las DLL de Siemens no se incluyen en el repositorio; se referencian desde la instalación de TIA Portal.
