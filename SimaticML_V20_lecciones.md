# Generar XML SimaticML para TIA Portal V20: lo aprendido

Notas sacadas de generar a mano los bloques de recepción **Estandar CentralAlarmas** del PLC MES de Pintura 2 e importarlos con el Add-In **SIEMENS-XML-V20** (`P542-X/SIEMENS-XML-V20`).

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
- **Un OB de ciclo con número > 1 se ejecuta en cada ciclo detrás del OB1**, por orden de número. Sirve para añadir lógica sin tocar `Main` (aquí, OB123 detrás de `MES_Rockwell_Llamadas`).
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
  <CallInfo Name="MES_CentralAlarmas_Recibir" BlockType="FC" />
</Call>
<!-- Wire: <Powerrail /> → <NameCon UId="21" Name="en" /> -->
```

Llamada a FB con DB de instancia: `CallInfo` con `<Instance Scope="GlobalVariable">` y un `<Parameter Name=… Section=… Type=…/>` por cada pin (ver `MES_Rockwell_Llamadas`).

### Errores de KOP vistos

| Error de importación | Causa | Solución |
|---|---|---|
| `The node 'TemplateValue' with the name 'var_type' … is missing at the element 'Serialize'` | Puse `Version="2.1"`, que usa otra plantilla | En este V20 es `Version="2.2"` con `src_type` y `dest_type` |
| `… 'var_type' … has an invalid value 'Array[0..999] of Word'` / `'Byte'` | Valores adivinados | No adivinar: exportar una caja de muestra |
| (prevenido) `RET_VAL` | El pin se llama `Ret_Val` | Copiar los nombres de pin de una exportación |

---

## 9. Datos del proyecto MES Pintura 2 que conviene recordar

- La recepción por socket (`MES_Rockwell_Con` → `TCOMM_Allen_Bradley_V10` → `TCOMM_V1`, con TRCV) deja los datos en **`Cx_Rcv : Array[0..999] of Word`** del DB de instancia, que es **optimizado**. No es un `Array of Byte`.
- Puertos: C1 = Estados (x0), C2 = Alarmas (x1). CPF: 2110 y 2111 en `MES_Rockwell_CPF_DB`. RECTIFICADORES: 2120 y 2121 en `MES_Rockwell_RECTIFICADORES_DB`.
- Hay trama nueva cuando `"<DB instancia>".Cx_TCOMM.T_COMM.TRcvNdr` = 1. Es un Static Bool de `TCOMM_V1` y se lee desde fuera sin problema.
- El Rockwell ya manda en orden Siemens (SWPB). Con TRCV sobre `Array of Word`, el byte 0 de la trama es el byte alto de `Rcv[0]`.
- Para pasar `Array of Word` → tipo estructurado: **`Serialize`** (`Array of Word` → `Array of Byte`, en orden big-endian) y después **`Deserialize`** (bytes → UDT). Sin bucles. `Deserialize` no acepta directamente un `Array of Word`.
- `MOVE_BLK_VARIANT` no sirve entre tipos distintos. `BLKMOV`, `PEEK` y `POKE` no sirven con origen optimizado.
- Cabecera CentralAlarmas (64 B): `Socket_DIAG` es **WORD**. El bit de vida del Rockwell es `Socket_DIAG.%X8` (= DBX0.0), no `%X0`. Después vienen `Nombre[0..3]` (Int), `PuntoLectura`, `Spare`, `CodeResponse_MES`, `CodeResponse_Leg`, `Registro_Conexion` (DTL, byte 18) y `Spare_1[0..16]`.
- Tamaños útiles: Alarmas 1664 B (cabecera + `Data_0..19`), Estados 184 B (cabecera + `Estados` + `Datos[0..19]`). El Rockwell manda siempre 2000 B.
- Los DB de destino van con acceso **estándar**, con `Msg` en el offset 0 y detrás `Error` y `Contador`, para que los offsets coincidan con el estándar.

---

## 10. Checklist antes de importar

1. ¿Cada fichero tiene un solo objeto y `Engineering version="V20"`?
2. ¿Los `ID` en hexadecimal son únicos y los `UId` también?
3. Si es SCL: ¿una sola CompileUnit y sin `AccessModifier`?
4. Si es KOP: ¿cada instrucción con versión o plantillas está copiada de una exportación real?
5. ¿Los cables de salida van `NameCon` → `IdentCon`?
6. ¿Pasa el XML por un parser sin errores?
7. Tras importar: leer el `resumen_*.txt` y actualizar las llamadas en rojo.
