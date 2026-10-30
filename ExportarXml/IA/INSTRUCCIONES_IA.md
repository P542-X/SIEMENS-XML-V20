# Instrucciones para la IA

Este paquete lo ha generado el Add-In **SIEMENS-XML-V20** desde TIA Portal con *XML → Exportar selección*. El usuario te lo pasa para que entiendas su programa de PLC y, si te lo pide, le prepares algo para **importar** con el mismo Add-In (*XML → Importar ficheros*).

Sigue estas instrucciones al pie de la letra. Están sacadas de errores reales de importación en TIA Portal V20.

---

## 1. Qué hay en el paquete

| Fichero / carpeta | Qué es | Para qué te sirve |
|---|---|---|
| `_info.md` | Proyecto, versión de TIA, **CPU y firmware**, lista de objetos (tipo, nombre, número, lenguaje) e **instrucciones con su versión** en esta CPU | Léelo **primero** |
| `SD/` | Bloques en **SIMATIC SD** (texto) | Leer la lógica. Es el formato más legible y no depende de la CPU |
| `FUENTES/` | Bloques **SCL** como fuente `.scl` (SD de V20 no admite SCL) | Leer y modificar el SCL |
| `XML/` | Todo en **SimaticML** (copia exacta) | Detalles que SD no tiene: títulos y comentarios de red, comentarios de variables, valores iniciales, offsets de DB estándar, UDT y tablas de variables |
| `_info.txt` | CPU de origen de cada carpeta | Lo lee el importador; cópialo a tu entrega si usas XML de KOP (punto 4) |
| `_resumen.txt` | Lo que se exportó y lo que falló | Si falta un bloque, mira aquí por qué (protegido, F-Safety, inconsistente…) |

Extensiones SD: `.s7dcl` KOP (y también los DB en esta versión de V20) · `.s7dcf` FUP · `.s7dcs` SCL · `.s7dbd` DB. Cada bloque con su `.s7res` (textos), salvo los DB.

En `_info.md`, la columna **Texto** dice dónde está cada bloque en texto: `SD`, `FUENTE` o **`solo XML`** con el motivo:
- *SD no admite este bloque (varios lenguajes o SCL)*: bloques KOP con alguna red SCL, o SCL que no pudo salir como fuente.
- *protegido con know-how* / *GRAPH*: no se puede leer ni regenerar su lógica; no lo toques.

## 2. Cómo leer el programa

1. `_info.md`: qué CPU es (S7-1500, S7-1200…), qué hay y qué números de bloque están ocupados.
2. `SD/`: la lógica de cada bloque. Los parámetros de las cajas van con `:=` (entradas e InOut) y `=>` (salidas).
3. `XML/`: cuando necesites comentarios, valores iniciales, offsets o el detalle exacto de un UDT o DB.
4. Antes de proponer cambios, explica al usuario lo que has entendido (qué hace cada bloque y cómo se relacionan) si no te ha pedido otra cosa.

## 3. Qué entregar cuando el usuario quiera importar algo

Entrega **un .zip** con esta estructura (solo las carpetas que uses):

```
<nombre>_para_importar.zip
    XML/        UDT, DB globales y tablas de variables (y KOP en XML solo si no hay otra opción)
    SD/         bloques KOP, FUP o SCL en SIMATIC SD (.s7dcl / .s7dcf / .s7dcs + .s7res)
    FUENTES/    fuentes externas .scl / .udt / .db (alternativa para SCL y DB)
    _info.txt   solo si incluyes KOP en XML: copia el del paquete original
    LEEME.md    qué es cada cosa y qué tiene que hacer el usuario después
```

Reglas generales:

- **Un objeto por fichero**, y el **nombre del fichero igual al nombre del objeto** (obligatorio en SD).
- Si **modificas** un bloque que ya existe, parte de su exportación (SD o XML) y cambia solo lo necesario. Mantén nombre y número.
- Si **creas** bloques nuevos, usa números libres (mira la lista de `_info.md`) y dilo en `LEEME.md`.
- No toques bloques F (Safety) ni protegidos: Openness no los importa.
- El usuario importa el .zip tal cual (no hace falta descomprimir). El Add-In importa por orden de dependencias (UDT → tablas → FB/FC → OB/DB), pregunta si sobrescribir, compila, deja un resumen y se puede deshacer entera con *XML → Deshacer última importación* (el Ctrl+Z de TIA no sirve).

## 4. Qué formato usar para cada cosa

| Objeto | Formato | Motivo |
|---|---|---|
| UDT (tipo de datos PLC) | **XML** `SW.Types.PlcStruct` | No lleva versiones: vale para cualquier CPU |
| DB global | **XML** `SW.Blocks.GlobalDB` (o fuente `.db`) | Igual |
| Tabla de variables | **XML** `SW.Tags.PlcTagTable` | Igual |
| FC / FB / OB en **KOP** o **FUP** | **SD** `.s7dcl` / `.s7dcf` | Las cajas van sin versión: TIA pone la de la CPU de destino |
| FC / FB / OB en **SCL** | **Fuente `.scl`** en `FUENTES/` | Texto SCL normal, sin tokenizar |
| Bloque **solo XML** (KOP con redes SCL…) | XML, partiendo del del paquete | Si va a **otra CPU**, avisa al usuario: las cajas llevan la versión de la CPU de origen |
| DB de instancia | Que lo cree TIA al compilar, o XML | |
| KOP en XML | **Evitar** | Cada caja lleva su versión y la ata a una CPU y firmware |

Si de verdad necesitas KOP en XML: copia la versión y las plantillas (`TemplateValue`) de cada caja **de un XML del propio paquete** o de la lista de `_info.md`, y mete `_info.txt` en tu entrega. **Nunca inventes versiones ni plantillas.**

## 5. SIMATIC SD (KOP)

Sintaxis real exportada por TIA V20:

```
{
    S7_Optimized := "TRUE";
    S7_Language := "LAD";
    S7_BlockNumber := "40031"
}
FUNCTION "Nombre_Bloque" : Void
    VAR_TEMP
        Buf : Array[0..1999] of Byte;
        Pos : DInt;
    END_VAR

    {
      S7_Language := "LAD"
    }
    NETWORK
        RUNG wire#powerrail
            Contact( "Mi_DB".Bit )
            Move{
                Card := 1;
                DisableENO := FALSE
            }(
                IN := 0,
                OUT1 => #Pos
            )
            Add{
                Card := 2;
                Type := AUTO;
                DisableENO := TRUE
            }(
                IN1 := "Mi_DB".Contador,
                IN2 := 1,
                OUT => "Mi_DB".Contador
            )
        END_RUNG

    END_NETWORK

END_FUNCTION
```

- Una red = `NETWORK … END_NETWORK` precedida de `{ S7_Language := "LAD" }`. Dentro, `RUNG wire#powerrail … END_RUNG`; las cajas de una rama se encadenan por EN/ENO en el orden escrito.
- Plantillas de la caja entre `{ }` separadas por `;` (`Card`, `DisableENO`, `Type := AUTO`, `src_type := Variant`…). **Sin versión.**
- Pines en mayúsculas (`IN`, `OUT1`, `SRC_VARIABLE`) salvo `Ret_Val`. Entradas e InOut `:=`, salidas `=>`.
- Variables globales entre comillas (`"DB".Miembro`), locales con `#`.
- `S7_BlockNumber` es opcional (sin él, número automático).
- Tipos de usuario (UDT, FB como multiinstancia) se escriben `_.Nombre`: `Msg : _.UDT_CentralAlarmas;`, `C1_TCOMM : _.TCOMM_Allen_Bradley_V10;`.
- Llamada a un FB con DB de instancia: `"Nombre_DB"( Param := valor, Salida => "DB".x, OtraSalida =>  , … )` (salida sin conectar: `=>  ,`).
- DB global en SD (`.s7dcl`): atributos `S7_Optimized`, `S7_StandardRetain`, `S7_BlockNumber` entre `{ }`, y `DATA_BLOCK Nombre  VAR … END_VAR  END_DATA_BLOCK`.
- **Textos**: el título y el comentario de red van como `S7_NetworkTitle := "MLC_x"; S7_NetworkComment := "MLC_y"` en las llaves de la red; el título de bloque como `S7_BlockTitle := "MLC_z"`; el comentario de una variable como `{ S7_MLC := "MLC_w" }` delante de ella. Cada `MLC_…` es un `<Comment Id="MLC_…">` del `.s7res` con un `<MultiLanguageText Lang="es-ES">` por idioma. Usa Id únicos dentro del bloque.
- Fichero en **UTF-8 con BOM** y saltos **CRLF**. Sin textos, el `.s7res` contiene solo `<root />`; con textos, `<root>` y un `<Comment Id=…>` por cada uno.
- **Ramas (comprobado en V20):**
  - *OR*: una rama lleva el punto de unión `wire#w1` **en medio** (delante de la bobina o caja de salida) y las demás ramas terminan en `END_RUNG wire#w1`:
    ```
    RUNG wire#powerrail
        Contact( "DB".A )
        wire#w1
        Coil( "DB".Q )
    END_RUNG
    RUNG wire#powerrail
        Contact( "DB".B )
    END_RUNG wire#w1
    ```
  - *Una condición, varias salidas*: `wire#w1` en medio de la primera rama y las siguientes empiezan con `RUNG wire#w1`.
  - **No** empieces una rama con `RUNG wire#wN` si ese cable solo es destino de otras ramas: TIA rechaza el bloque entero.
- **`Type := AUTO` y `SrcType`** necesitan al menos una entrada con tipo (una variable o una constante con tipo, `INT#6`). Con todas las entradas constantes sin tipo (`IN1 := 6, IN2 := 7`) importa, pero **no compila** (*"Please select a data type" / "At least one constant must be typed"*).
- **Llamada a una FC con valor de retorno**: `"Mi_FC"( A := 2, B := 3, Ret_Val => "DB".x )`.
- **Si una importación SD falla, TIA no dice la línea** (solo *Failure*). Cuando uses algo que no aparece en el paquete, ponlo en un bloque aparte: si falla, solo cae ese.
- **Comprobado en un S7-1500 con V20** (prueba de 49 casos): Contact, I_Contact, Coil, S_Coil, R_Coil, Not, PBox, ramas OR y de salida, Eq/Ne/Gt/Ge/Lt/Le (Int, DInt, Real, Word, String), Move (1 y 2 salidas), Swap, Convert, S_Move, Serialize, Deserialize, Calculate, Add, Sub, Mul, Div, CONCAT, LEN, VAL_STRG, STRG_VAL, MAX_LEN, Strg_TO_Chars, TON (en DB global y como multiinstancia), RD_SYS_T, llamadas a FC (con y sin retorno) y a FB con DB de instancia (en SD `DATA_BLOCK Nombre : FB`), FB en SD con interfaz y multiinstancias.
- **Para la sintaxis de una caja o construcción que no conozcas**, búscala en `SD/` del paquete (hay muchos ejemplos reales). Si no está, pide al usuario que ponga esa caja en un bloque de prueba, lo exporte con *Exportar selección* y te pase el .zip. No la adivines.

## 6. Fuentes `.scl`, `.db`, `.udt`

Los bloques SCL del paquete vienen así en `FUENTES/`: modifícalos sobre esa fuente.

- Texto normal de TIA (`FUNCTION "x" : Void … END_FUNCTION`, `DATA_BLOCK`, `TYPE`), con `{ S7_Optimized_Access := 'TRUE' }` y `VERSION : 0.1`.
- Una fuente puede llevar varios objetos; el Add-In los genera todos.
- Los bloques generados desde fuente quedan en la raíz de *Bloques de programa*.
- Si el objeto ya existe y el usuario elige sobrescribir, el Add-In hace copia en XML, lo borra y lo regenera.

## 7. SimaticML (XML)

Solo para UDT, DB, tablas de variables y, si no queda otra, bloques.

- Raíz `<Document>` con `<Engineering version="V20" />` (o la versión de `_info.md`) y **un solo** objeto `SW.*`.
- Interfaz: namespace `http://www.siemens.com/automation/Openness/SW/Interface/v5`.
- `ID` de `ObjectList` en hexadecimal y **únicos** en todo el documento.
- Textos (`MultilingualText`) en los idiomas del proyecto: copia los de un XML del paquete.
- DB con offsets fijos: `<MemoryLayout>Standard</MemoryLayout>`.
- Tipos de UDT entre comillas escapadas: `Datatype="&quot;Mi_UDT&quot;"`.
- Struct anidado: `<Member Name="x" Datatype="Struct">` con los miembros dentro.
- **SCL en XML** (evitarlo; mejor `.scl`): una sola `CompileUnit` por bloque, código tokenizado con `UId` únicos, índices de array con tokens `[` `]` y **sin** `AccessModifier`.
- **KOP en XML** (evitarlo; mejor SD): un `Access` por cada uso de una variable; cables de entrada e InOut `IdentCon → NameCon`, de salida `NameCon → IdentCon`; en `Serialize` y `Deserialize`, `DEST_ARRAY` y `DEST_VARIABLE` son salidas y el retorno se llama `Ret_Val`.

## 8. La CPU importa

- Mira en `_info.md` la familia (S7-1500, S7-1200, ET 200SP…) y el firmware. Usa solo instrucciones y tipos que existan en esa CPU; si dudas de alguno, dilo y propón comprobarlo en la ayuda de TIA o con una exportación de prueba.
- SD resuelve las versiones de instrucción, pero **no** puede usar una instrucción que la CPU no tenga: eso saldrá como error al importar.
- Si el usuario va a importar en **otra CPU**, entrega la lógica en SD y los datos en XML: el Add-In elegirá SD solo.

## 9. Qué hace el Add-In al importar (para explicárselo al usuario)

- Valida cada XML antes: si incumple una regla conocida sale `NO VÁLIDO` con la línea del problema y no se importa.
- Si el mismo bloque viene en XML y SD: misma CPU que el origen → XML; otra CPU → SD; si uno falla, prueba el otro.
- Pregunta si sobrescribir lo que ya existe (Sí / No / Cancelar).
- Compila y deja `Import_XML\resumen_<fecha>.txt`. **Si algo falla, pide al usuario ese resumen** (las líneas `ERROR`, `NO VÁLIDO` y la compilación) y corrige a partir de él.
- *XML → Deshacer última importación* lo deja todo como estaba: borra lo nuevo y restaura lo sobrescrito (el Ctrl+Z de TIA no deshace lo que hace el Add-In).

## 10. Antes de entregar, comprueba

1. ¿Cada objeto está en el formato del punto 4 y con el nombre de fichero igual al del objeto?
2. ¿Ninguna caja KOP lleva versión inventada? (SD: sin versión; XML: copiada del paquete)
3. ¿Los UDT y DB que usa la lógica van en la entrega o ya existen en el proyecto?
4. ¿Las llamadas apuntan a bloques que existen o que entregas?
5. ¿Los ficheros SD están en UTF-8 con BOM y CRLF, con su `.s7res`?
6. ¿El `LEEME.md` dice qué importar, qué responder a "sobrescribir", qué llamar desde dónde y cómo comprobar que funciona?

## 11. Cómo contestar al usuario

- Primero, en una o dos frases, qué has entendido o qué vas a cambiar.
- Después, el .zip y los pasos para importarlo: *XML → Importar ficheros…*, elegir el .zip y responder Sí o No a sobrescribir.
- Al final, cómo comprobar que funciona y qué hacer si el resumen da error: pasarte el `resumen_*.txt`.
