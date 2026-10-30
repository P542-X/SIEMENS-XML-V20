# Generar XML SimaticML para TIA Portal V20: lo aprendido

Notas sacadas de generar bloques a mano (y con IA) e importarlos en un S7-1500 con TIA Portal V20 mediante el Add-In **SIEMENS-XML-V20** (`P542-X/SIEMENS-XML-V20`). Todo lo que pone *comprobado* se ha importado y compilado de verdad.

---

## 1. Regla de oro

**Antes de generar un elemento que nunca has generado, crea uno de muestra en TIA, expórtalo con el Add-In y copia su formato.**

Cada instrucción de KOP puede llevar versión y plantillas (`TemplateValue`) propias de la instalación. Si se adivinan, cada intento fallido es otra vuelta de importación. Con Serialize/Deserialize se perdieron tres vueltas adivinando, y una exportación de 30 segundos lo resolvió.

---

## 2. Cómo importa el Add-In

- Un objeto por fichero: un `<Document>` con un único `SW.*` dentro.
- Clasifica por el elemento raíz y lo importa en este orden: `SW.Types.PlcStruct` (UDT) → `SW.Tags.PlcTagTable` → `SW.Blocks.FB` / `FC` → `OB` / `GlobalDB` / `ArrayDB` → `InstanceDB`.
- Hace varias pasadas. Si un bloque falla por depender de otro, lo reintenta.
- Al final compila y deja el detalle en `<carpeta del proyecto>\Import_XML\resumen_<fecha>.txt`. **La línea `ERROR` de ese fichero es la que dice exactamente qué falla.**
- Las exportaciones van a `<carpeta del proyecto>\Export_XML\<fecha_hora>\`.
- Dónde aparece cada cosa al importar: los UDT en *Tipos de datos PLC*; FC, FB, OB y DB en *Bloques de programa*.

---

## 3. Esqueleto común

```xml
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <Engineering version="V20" />
  <SW.Blocks.FC ID="0">
    <AttributeList> ... </AttributeList>
    <ObjectList> ... </ObjectList>
  </SW.Blocks.FC>
</Document>
```

- `DocumentInfo` es opcional; no hace falta.
- Guardar en UTF-8 (con o sin BOM).
- Los `ID` de `ObjectList` (MultilingualText, CompileUnit…) van en **hexadecimal** y no se pueden repetir dentro del bloque.
- Textos: cada `MultilingualText` lleva un `MultilingualTextItem` por idioma del proyecto. En este proyecto son `es-ES` y `en-US`.

Espacios de nombres que funcionan en V20:

| Parte | Namespace |
|---|---|
| Interfaz | `http://www.siemens.com/automation/Openness/SW/Interface/v5` |
| Red KOP/FUP | `http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5` |
| Red SCL | `http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4` |

---

## 4. UDT (`SW.Types.PlcStruct`)

- Un `Section Name="None"` con los `Member`.
- Tipos de otro UDT entre comillas escapadas: `Datatype="&quot;CA_Cabecera&quot;"`.
- Arrays: `Datatype="Array[0..3] of Int"`.
- Comentario de miembro: `<Comment><MultiLanguageText Lang="es-ES">…</MultiLanguageText></Comment>`.
- Atributos mínimos: `Interface`, `Name`, `Namespace`.

## 5. DB global (`SW.Blocks.GlobalDB`)

- `Section Name="Static"`.
- `<MemoryLayout>Standard</MemoryLayout>` para acceso no optimizado (offsets fijos); `Optimized` para optimizado.
- `<Number>` y `<AutoNumber>false</AutoNumber>` para fijar el número.
- `<ProgrammingLanguage>DB</ProgrammingLanguage>`.
- Los miembros admiten `Remanence="NonRetain" Accessibility="Public"` y los `BooleanAttribute` `ExternalAccessible` / `ExternalVisible` / `ExternalWritable`.

## 6. FC y OB

- FC: secciones `Input`, `Output`, `InOut`, `Temp`, `Constant` y `Return` con `<Member Name="Ret_Val" Datatype="Void" />`.
- OB de ciclo: `<SecondaryType>ProgramCycle</SecondaryType>` y en `Input` los miembros `Initial_Call` y `Remanence` (`Datatype="Bool" Informative="true"`).
- **Un OB de ciclo con número > 1 se ejecuta en cada ciclo detrás del OB1**, por orden de número. Sirve para añadir lógica sin tocar `Main`.
- Si al importar una FC el bloque que la llama queda en rojo: clic derecho en la caja → **Actualizar llamada de bloque**.

---

## 7. SCL (`StructuredText`)

### Reglas que han dado error

| Error de importación | Causa | Solución |
|---|---|---|
| `'AccessModifier' is not supported at the object with UID …` | `AccessModifier="Array"` en un `Component` de SCL | En SCL el índice va solo con tokens `[` y `]` dentro del `Component`, sin ese atributo (en KOP es otra cosa) |
| `The programming language of 'SCL' does not support the use of multiple compilation units` | Varias `SW.Blocks.CompileUnit` en un bloque SCL | **Un bloque SCL = una sola CompileUnit.** Separar por comentarios o `REGION` |

### Cómo se escribe cada cosa

El código va tokenizado, todo con `UId` únicos dentro del bloque:

- Espacios y saltos: `<Blank Num="4" UId="…"/>`, `<NewLine Num="1" UId="…"/>`.
- Palabras clave y operadores: `<Token Text="IF" UId="…"/>`, `<Token Text=":=" …/>`, `<Token Text="=&gt;" …/>`.
- Comentario: `<LineComment UId="…"><Text UId="…">texto</Text></LineComment>`.
- Constante: `<Access Scope="LiteralConstant"><Constant><ConstantValue>8</ConstantValue></Constant></Access>`.
- Variable local (`#x.y`): `Access Scope="LocalVariable"` → `Symbol` → `Component`, con `<Token Text="."/>` entre componentes.
- Variable global (`"DB".a.b`): `Access Scope="GlobalVariable"`. El primer `Component` lleva `<BooleanAttribute Name="HasQuotes">true</BooleanAttribute>`.
- Array (`#Buf[2 * #i]`):

```xml
<Component Name="Buf" UId="…">
  <Token Text="[" UId="…" />
  ... expresión del índice ...
  <Token Text="]" UId="…" />
</Component>
```

- Instrucción (`WORD_TO_BYTE(IN := x)`, `Deserialize(...)`):

```xml
<Access Scope="Call" UId="…">
  <Instruction Name="WORD_TO_BYTE" UId="…">
    <Token Text="(" />
    <Parameter Name="IN"><Blank/><Token Text=":="/><Blank/> ...valor... </Parameter>
    <Token Text=")" />
  </Instruction>
</Access>
```

  Parámetros de salida con `=>`. Los separadores `,` y los espacios entre parámetros van fuera de `Parameter`.

### Técnica útil

Escribir el SCL en texto y convertirlo con un script (lexer + emisor de tokens). Para comprobar el resultado, reconstruir el SCL desde el XML y compararlo con el original antes de importarlo.

---

## 8. KOP (`FlgNet`)

### Estructura

- `<Parts>`: primero los `Access` (operandos) y después los `Part` / `Call` (instrucciones).
- `<Wires>`: cada `Wire` une dos extremos.
- **Un `Access` por cada uso**: si la misma variable entra en dos pines, van dos `Access` con `UId` distintos.
- En KOP los componentes de una ruta van seguidos, **sin** tokens `.`: `<Component Name="DB"/><Component Name="Msg"/>`.
- Constantes con tipo: `<ConstantType>DInt</ConstantType><ConstantValue>1</ConstantValue>`.

### Sentido de los cables

| Pin | Cable |
|---|---|
| Entrada (`Input`) | `IdentCon` → `NameCon` |
| `InOut` (p. ej. `POS`) | `IdentCon` → `NameCon` |
| Salida, `Ret_Val`, `out` | `NameCon` → `IdentCon` |
| Cadena de cajas | `NameCon … eno` → `NameCon … en` |
| Primera caja o contacto | `<Powerrail />` → `NameCon … in` / `en` |

En Serialize/Deserialize, `DEST_ARRAY` y `DEST_VARIABLE` se cablean como **salidas**.

### Partes ya probadas en este proyecto

```xml
<Part Name="Contact" UId="…" />                       <!-- pines in, operand, out -->

<Part Name="Move" UId="…">                             <!-- sin DisabledENO: tiene ENO para encadenar -->
  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>   <!-- salidas out1, out2 -->
</Part>

<Part Name="Add" UId="…" DisabledENO="true">
  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>   <!-- in1, in2 -->
  <AutomaticTyped Name="SrcType" />
</Part>

<Part Name="Serialize" Version="2.2" UId="…">          <!-- SRC_VARIABLE, DEST_ARRAY, POS, Ret_Val -->
  <TemplateValue Name="src_type" Type="Type">Variant</TemplateValue>
  <TemplateValue Name="dest_type" Type="Type">Variant</TemplateValue>
</Part>

<Part Name="Deserialize" Version="2.2" UId="…">        <!-- SRC_ARRAY, DEST_VARIABLE, POS, Ret_Val -->
  <TemplateValue Name="src_type" Type="Type">Variant</TemplateValue>
  <TemplateValue Name="dest_type" Type="Type">Variant</TemplateValue>
</Part>
```

Llamada a FC sin parámetros:

```xml
<Call UId="21">
  <CallInfo Name="FC_Recibir" BlockType="FC" />
</Call>
<!-- Wire: <Powerrail /> → <NameCon UId="21" Name="en" /> -->
```

Llamada a FB con DB de instancia: `CallInfo` con `<Instance Scope="GlobalVariable">` y un `<Parameter Name=… Section=… Type=…/>` por cada pin.

### Errores de KOP vistos

| Error de importación | Causa | Solución |
|---|---|---|
| `The node 'TemplateValue' with the name 'var_type' … is missing at the element 'Serialize'` | Puse `Version="2.1"`, que usa otra plantilla | En este V20 es `Version="2.2"` con `src_type` y `dest_type` |
| `… 'var_type' … has an invalid value 'Array[0..999] of Word'` / `'Byte'` | Valores adivinados | No adivinar: exportar una caja de muestra |
| (prevenido) `RET_VAL` | El pin se llama `Ret_Val` | Copiar los nombres de pin de una exportación |

---


## 10. Checklist antes de importar

1. ¿Cada fichero tiene un solo objeto y `Engineering version="V20"`?
2. ¿Los `ID` en hexadecimal son únicos y los `UId` también?
3. Si es SCL: ¿una sola CompileUnit y sin `AccessModifier`?
4. Si es KOP: ¿cada instrucción con versión o plantillas está copiada de una exportación real?
5. ¿Los cables de salida van `NameCon` → `IdentCon`?
6. ¿Pasa el XML por un parser sin errores?
7. Tras importar: leer el `resumen_*.txt` y actualizar las llamadas en rojo.

---

## 11. Para KOP, mejor SIMATIC SD que SimaticML (no depende de la CPU)

TIA V20 tiene un formato de texto para bloques, **SIMATIC SD**: `.s7dcl` (KOP), `.s7dcf` (FUP), `.s7dcs` (SCL) y `.s7dbd` (DB), más un `.s7res` con los textos. Openness lo exporta con `PlcBlock.ExportAsDocuments(carpeta, nombre)` y lo importa con `PlcBlockComposition.ImportFromDocuments(carpeta, nombre, ImportDocumentOptions.Override)`. **Para bloques solo admite `Override`**: con `None` falla (*"only supports the 'Override' option for blocks"*).

**En SD las cajas van por nombre, sin versión.** TIA pone al importar la versión que corresponde a la CPU de destino, así que el mismo fichero vale para cualquier CPU (siempre que la instrucción exista en ese modelo). En SimaticML, en cambio, la versión va escrita (`Serialize` 2.2) y ata el XML a una CPU y firmware.

Sintaxis confirmada con una exportación real de V20 (`referencia/SD/`):

```
{
    S7_Optimized := "TRUE";
    S7_Language := "LAD";
    S7_BlockNumber := "40031"          <- opcional: sin él, número automático
}
FUNCTION "FC_Recibir" : Void
    VAR_TEMP
        Buf : Array[0..1999] of Byte;
        PosS : DInt;
    END_VAR

    {
      S7_Language := "LAD"
    }
    NETWORK
        RUNG wire#powerrail
            Contact( "DB_Origen".Nueva_Trama )
            Move{
                Card := 2;
                DisableENO := FALSE
            }(
                IN := 0,
                OUT1 => #PosS,
                OUT2 => #PosD
            )
            Serialize{
                src_type := Variant;
                dest_type := Variant
            }(
                SRC_VARIABLE := "DB_Origen".Rcv,
                Ret_Val => #RetS,
                DEST_ARRAY => #Buf,
                POS := #PosS
            )
            Add{
                Card := 2;
                Type := AUTO;
                DisableENO := TRUE
            }(
                IN1 := "DB_Destino".Contador,
                IN2 := 1,
                OUT => "DB_Destino".Contador
            )
        END_RUNG

    END_NETWORK

END_FUNCTION
```

Reglas:

- Las cajas de una rama van una detrás de otra dentro de `RUNG wire#powerrail … END_RUNG`; se encadenan por EN/ENO.
- Las plantillas de la caja van entre `{ }` separadas por `;` (`Card`, `DisableENO`, `Type := AUTO`, `src_type`…). Son las mismas que los `TemplateValue` del XML, pero sin versión.
- Parámetros: entradas e InOut con `:=`, salidas con `=>`. Pines en mayúsculas (`IN`, `OUT1`, `SRC_VARIABLE`), salvo `Ret_Val`.
- Fichero en UTF-8 con BOM y saltos CRLF. Sin textos, el `.s7res` es solo `<root />`.
- En esta exportación las redes no llevaban título ni comentario en el `.s7res`, aunque el XML importado sí los tenía: pendiente de ver cómo se escriben los textos en SD.

Para aprender la sintaxis SD de cualquier caja: ponerla en un bloque, exportar a SD y guardar el fichero en `referencia/SD/`.

## 12. `GenerateBlocksFromSource` no sobrescribe

Si el bloque ya existe, al generar desde fuente se queda el código viejo sin dar error. Para sobrescribir hay que borrar antes el bloque (el Add-In v1.3.0 hace copia en XML antes de borrar). `ImportFromDocuments` con `Override` sí sobrescribe.

## 13. Transacciones de Openness: una excepción las invalida

Dentro de `ExclusiveAccess.Transaction(...)`, si **cualquier** llamada de Openness lanza una excepción (aunque se capture), `CommitOnDispose()` falla con *"Commit of a Transaction is not allowed after an exception is thrown due to potential project data corruption"* y todo se deshace. Para importar con reintentos (bloques que dependen de otros) hay que hacer un ensayo en una transacción que se deshace, apuntar el orden que funciona y repetirlo en otra transacción donde ya no falle nada.

Además, en la prueba real **el Ctrl+Z de TIA no deshizo la importación** aunque la transacción se confirmó sin errores. El Add-In ya no usa transacciones: hace su propio deshacer (copia en XML de lo que sobrescribe, lista de lo nuevo y menú *Deshacer última importación*).

## 14. SIMATIC SD: lo aprendido con las pruebas de importación (49 casos)

- **Si un `.s7dcl` falla, TIA solo devuelve `Failure`**, sin línea ni motivo. Con construcciones dudosas: una por bloque.
- **OR**: el punto de unión `wire#wN` va en medio de una rama (delante de la salida) y las otras ramas terminan en `END_RUNG wire#wN`. Empezar una rama con `RUNG wire#wN` cuando ese cable solo es destino rompe el bloque entero.
- **`Type := AUTO` / `SrcType`** con todas las entradas constantes sin tipo: importa pero no compila (*"Please select a data type"*, *"At least one constant must be typed"*). Usar una variable o `INT#6`.
- **FC con retorno** desde KOP: `Ret_Val =>` (TIA acepta también `RET_VAL =>`).
- **DB de instancia** en SD: `DATA_BLOCK Nombre : FB_Nombre` + `END_DATA_BLOCK`, sin `.s7res`.
- **`ImportFromDocuments`** solo admite `Override` en bloques.

## 15. Idiomas del proyecto

Un XML con textos en un idioma que el proyecto no tiene activado no importa: *"the specified culture does not exist within the current project"* (en `MultilingualTextItem` y en los comentarios `MultiLanguageText Lang=…` de la interfaz). En SD el `.s7res` con un idioma de más sí entró. El Add-In (v2.3.3) adapta los XML antes de importarlos: quita los idiomas que faltan y, si un texto se queda sin ninguno, lo pasa al idioma de referencia.

## 16. Algunas cajas SD mal escritas cierran TIA

Importar `FILL_BLK{ DisableENO := TRUE }( IN := 9, COUNT := 10, OUT => "DB".Arr[0] )` o `MOVE_BLK{ DisableENO := TRUE }( IN := "DB".A[0], COUNT := 2, OUT => "DB".B[0] )` en SD hace que `ImportFromDocuments` lance una excepción y **TIA Portal se cierra** justo después, sin guardar. Desde la v2.3.4 el Add-In deja `Import_XML\progreso.txt` para saber qué fichero fue. Cajas dudosas: en un .zip aparte. Desde la v2.3.5 el Add-In guarda el proyecto antes de importar.

## 17. Tanda 1 (instrucciones básicas): lo aprendido

- `Type := AUTO` solo en Add/Sub/Mul/Div. En el resto de cajas con tipo: *"The property 'IsAuto' is not supported for this instruction"*. `Type := Real` tampoco: deja el tipo en *Undef* (*"does not match the data type Undef"*, *"Data type Void is not permitted here"*). La forma a probar es `SrcType := Real`, como en las comparaciones.
- `IEC_COUNTER`: salidas `.QU` / `.QD`, no `.Q`.
- SCL: `x := -"DB".a = -5` → *"Invalid assignment"*; usar una variable intermedia.
- Nombre de caja desconocido en SD → TIA lo importa como llamada a `<Nombre>_Callee` y falla al compilar.
- `R_TRIG`/`F_TRIG` no se admiten como miembros de un DB global (en fuente `.db`).
- `CountOfElements` no está disponible en KOP en S7-1500 V4.0; en SCL solo con un parámetro Variant del bloque (con un array global directo: *"Invalid assignment"*).
- En SCL, `SCALE_X`/`NORM_X` con constantes sin tipo no compilan.
- Entran y compilan en KOP (SD): Abs, Acos, Asin, Atan, Cos, Sin, Tan, Exp, Expt, Ln, Frac, Sqr, Sqrt, Mod, Neg, And, Or, Xor, Inv, Mux, Demux, Shl, Shr, Rol, Ror, Ceil, Floor, Round, Trunc, CTU, CTD, CTUD, TP, TOF, TONR, I_Coil, N_Contact, P_Contact, P_Coil, NBox, SR (`S_SR`) — **comprobadas en marcha (96 pruebas, todas TRUE en PLCSIM)**.
- No entran (sintaxis por aprender): Inc, Dec, Min, Max, Limit, Deco, Enco, Sel, Expt, IN_Range, OUT_Range, OK/NOT_OK, R_TRIG, F_TRIG, RS, SET_BF/RESET_BF. Cierran TIA: MOVE_BLK, FILL_BLK, NORM_X (y se bloquean UMOVE_BLK, MOVE_BLK_VARIANT, SCALE_X).

## 18. El nombre de la caja en SD no es el de TIA

Las 24 cajas que fallaban en la tanda 1 tenían otro nombre interno en SD. Con una FC hecha a mano y exportada se vio: `MoveBlockI` (MOVE_BLK), `MoveBlockU` (UMOVE_BLK), `FillBlockI`/`FillBlockU` (FILL_BLK/UFILL_BLK), `Normalize`/`Scale_X` (NORM_X/SCALE_X), `InRange`/`OutRange` con pin `IN`, `NOK` (NOT_OK), `S_RS` (RS), `S_BitfieldCoil`/`R_BitfieldCoil` (SET_BF/RESET_BF), `Inc{ DestType := Int }( var )`, `MIN{ value_type := Int; card := 2 }`… Con esos nombres, las 24 importan, compilan y funcionan (tanda 1 completa: **120 pruebas, todas TRUE**). Método para cada tanda nueva: **primero una FC con las cajas hecha a mano y exportada**, después las pruebas.
