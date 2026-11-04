# Guía para una IA que trabaja con este repositorio

Este repositorio es el Add-In **SIEMENS-XML-V20** para **TIA Portal V20** y todo lo aprendido para que una IA genere programas de PLC (S7-1500) que **se importan, compilan y funcionan a la primera**. Léelo en este orden según lo que te pidan.

## 1. Lo primero, siempre

1. `README.md`: qué es el Add-In, cómo se instala y qué hace cada menú.
2. `ExportarXml/IA/INSTRUCCIONES_IA.md`: **la guía principal**. Reglas de oro, recetas paso a paso por caso, sintaxis **comprobada** de SIMATIC SD, fuentes SCL y SimaticML, lo que no funciona y la tabla de errores con su solución. Todo lo que generes tiene que cumplirla.
3. `ROADMAP.md`: en qué punto está el proyecto y qué falta.

## 2. Según lo que te pidan

| Te piden… | Lee además | Y haz |
|---|---|---|
| **Entender un proyecto exportado** (el usuario te pasa un .zip de *Exportar selección*) | `_info.md` del propio .zip; `INSTRUCCIONES_IA.md` §1–3 | Resume qué hace el programa antes de proponer cambios |
| **Modificar o crear bloques, DB, UDT, OB, tablas** para importar | `INSTRUCCIONES_IA.md` §4–12; `referencia/` (ejemplos reales); `pruebas/*/` (bloques comprobados del mismo tipo) | Sigue la receta del caso (§5). Entrega un .zip con `XML/`, `SD/`, `FUENTES/` y `LEEME.md` |
| **Una instrucción que no está en la guía** | `INSTRUCCIONES_IA.md` §5 caso K | **No la inventes**: pide al usuario una FC hecha a mano con esa caja, compilada y exportada |
| **Corregir tras importar** (te pasan `resumen_*.txt`) | `INSTRUCCIONES_IA.md` §10 (errores) y `SimaticML_V20_lecciones.md` | El resumen dice el bloque y la línea; si TIA se cerró, pide `Import_XML\progreso.txt` |
| **Ampliar las pruebas** (nueva tanda de instrucciones) | `GUIA_PRUEBAS.md`; `herramientas/README.md` | Usa `herramientas/simatic.py` y una tanda existente como plantilla |
| **Cambiar el código del Add-In** | `README.md` (ficheros), `CHANGELOG.md` | C# .NET Framework 4.8 + `Siemens.Engineering.AddIn`. Compila con `dotnet build -c Release` en `ExportarXml/` (necesita TIA V20) |
| **Publicar una versión** | sección 4 de este fichero | |

## 3. Reglas que no se negocian

- **No inventes sintaxis.** Solo lo que está en `INSTRUCCIONES_IA.md`, en `referencia/`, en `pruebas/` o en la exportación del usuario. Algunas cajas mal escritas **cierran TIA Portal**.
- **El nombre de una caja en SD no siempre es el de TIA** (`MoveBlockI` = MOVE_BLK, `InRange` = IN_RANGE, `JumpCoil` = JMP…): consulta la tabla de la guía.
- **Lógica KOP → SD; SCL → fuente `.scl`; UDT, DB, tablas y OB nuevos → XML.** KOP en XML solo si no hay otra opción. RET en KOP solo por XML.
- **Un elemento dudoso por bloque**: si TIA rechaza un `.s7dcl` no dice la línea.
- **El repositorio es público**: nada de datos de una planta o empresa (IP, nombres de instalación, usuarios, rutas). Usa nombres genéricos.
- Responde en el idioma del usuario. Este repositorio está en castellano de España.

## 4. Publicar una versión

1. Cambia `<AddInVersion>` y `<Version>` en `ExportarXml/Config.xml` y añade la entrada en `CHANGELOG.md`.
2. Comprueba que no se cuela nada sensible: `git grep -n -I -E "10\.[0-9]+\.[0-9]+\.|C:\\\\Users|<nombre de la empresa o la planta>"`.
3. Comprueba que compila: `cd ExportarXml && dotnet build -c Release`.
4. `git add -A && git commit -m "vX.Y.Z: …" && git tag vX.Y.Z && git push origin main && git push origin vX.Y.Z`.
5. El usuario instala el `.addin` con TIA cerrado (pasos en `README.md`).

## 5. Mapa del repositorio

| Carpeta / fichero | Qué es |
|---|---|
| `ExportarXml/` | Código del Add-In (C#) |
| `ExportarXml/IA/INSTRUCCIONES_IA.md` | Guía para la IA (va dentro de la DLL y se copia a cada exportación) |
| `SimaticML_V20_lecciones.md` | Historial de lo aprendido, con el porqué de cada regla |
| `referencia/` | Bloques reales exportados de TIA (sintaxis de referencia) |
| `pruebas/` | Baterías de prueba ya generadas, cada una con su `LEEME.md` y su resultado |
| `herramientas/` | Generadores de las baterías (`simatic.py` + `tanda*.py`) |
| `GUIA_PRUEBAS.md` | Método para comprobar instrucciones nuevas |
| `ROADMAP.md`, `CHANGELOG.md` | Hoja de ruta y cambios por versión |
