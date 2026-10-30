# Instrucciones para la IA

Este paquete lo ha generado el Add-In **SIEMENS-XML-V20** desde TIA Portal con *XML → Exportar selección*. El usuario te lo pasa para que entiendas su programa de PLC y, cuando te lo pida, le prepares algo para **importar** con el mismo Add-In (*XML → Importar ficheros*).

Todo lo que pone aquí está **comprobado en TIA Portal V20** (importado, compilado y, cuando se indica, ejecutado en PLCSIM). Lo que no está comprobado se dice. Síguelo al pie de la letra: cada regla viene de un error real.

---

## 0. Reglas de oro (léelas siempre)

1. **Lee primero `_info.md`**: CPU, firmware, versión de TIA, idiomas del proyecto, lista de bloques (con su número y si están en SD, FUENTE o *solo XML*) e instrucciones con versión.
2. **Formato por tipo de objeto** (sección 4): lógica KOP/FUP → **SIMATIC SD**; SCL → **fuente `.scl`**; UDT, DB, tablas de variables, OB → **XML**. KOP en XML solo si no queda otra.
3. **No inventes sintaxis.** Usa solo lo que aparece en la sección 6 o en `SD/` del paquete. Si necesitas una caja que no aparece en ninguno de los dos, **pídele al usuario que la exporte** (caso K). Hay cajas mal escritas que **cierran TIA Portal** (sección 9).
4. **Un objeto por fichero** y **nombre de fichero = nombre del objeto**.
5. **Cambia lo mínimo.** Para modificar un bloque, parte de su fichero del paquete y toca solo lo necesario. Mantén nombre, número e interfaz salvo que el cambio lo exija.
6. **Entrega un único .zip** con la estructura de la sección 11 y un `LEEME.md` con los pasos para el usuario.
7. **Después de cada importación, pide el resumen** (`Import_XML\resumen_<fecha>.txt`) y, si hay error, corrígelo con la sección 10.
8. **Si algo puede hacer daño en una instalación real** (cambiar lógica de seguridad, salidas, comunicaciones de planta), dilo claramente y recomienda probarlo antes en un proyecto de pruebas con PLCSIM.

---

## 1. Qué hay en el paquete

| Fichero / carpeta | Qué es | Para qué te sirve |
|---|---|---|
| `_info.md` | Proyecto, TIA, **CPU y firmware**, **idiomas**, objetos (tipo, nombre, número, lenguaje, columna **Texto**) e instrucciones con su versión | Léelo **primero** |
| `SD/` | Bloques en **SIMATIC SD** (texto). `.s7dcl` KOP (y los DB en V20), `.s7dcf` FUP, `.s7dcs` SCL, `.s7dbd` DB; cada bloque con su `.s7res` (textos) salvo los DB | Leer y modificar la lógica KOP/FUP. No depende de la CPU |
| `FUENTES/` | Bloques **SCL** como fuente `.scl` (el SD de V20 no exporta SCL) | Leer y modificar SCL |
| `XML/` | Todo en **SimaticML** (copia exacta) | UDT, DB, tablas de variables, comentarios, valores iniciales, offsets. Y los bloques *solo XML* |
| `_info.txt` | CPU de origen de cada carpeta | Lo lee el importador para elegir XML o SD |
| `_resumen.txt` | Qué se exportó y qué no | Por qué falta algo |

**Columna Texto de `_info.md`:**
- `SD`: el bloque está en `SD/`.
- `FUENTE`: está en `FUENTES/` (SCL).
- `solo XML: …`: no hay versión de texto. Motivos: *varios lenguajes o SCL* (KOP con alguna red SCL), *protegido con know-how*, *GRAPH*. Los protegidos y GRAPH no se pueden modificar; no los toques.

---

## 2. Flujo de trabajo con el usuario (siempre igual)

1. **Recibes el .zip.** Lee `_info.md` y los bloques que te interesen.
2. **Resume lo que has entendido** en pocas líneas: qué hace el programa, bloques principales (empieza por `Main` y lo que llama), comunicaciones, datos clave. Si el usuario no ha pedido nada concreto, para aquí y pregúntale qué quiere.
3. **Aclara lo imprescindible antes de generar** (una pregunta, no diez): qué hay que cambiar, dónde, con qué nombres, si es para el PLC real o para pruebas.
4. **Elige formato** para cada objeto (sección 4) y **genera** siguiendo la receta del caso (sección 5).
5. **Revisa** con la lista de la sección 12.
6. **Entrega** el .zip (sección 11) y los pasos al usuario:
   1. Guardar el proyecto (el Add-In también lo guarda antes de importar).
   2. *XML → Importar ficheros…* → elegir el .zip **sin descomprimir**. (No usar *Importar carpeta* con varios .zip dentro.)
   3. Si pregunta *¿Sobrescribir?*: **Sí** para sustituir lo que ya existe, **No** para importar solo lo nuevo.
   4. Mirar el resumen y pasártelo si hay `ERROR`, `NO VÁLIDO` o errores de compilación.
   5. Si algo no convence: *XML → Deshacer última importación* (el Ctrl+Z de TIA **no** deshace lo importado).
7. **Corrige** con el resumen (sección 10) y repite hasta *Compilación: Correcta (errores: 0)*.
8. **Verifica en marcha** si procede (caso M): cargar en PLCSIM o en el PLC y comprobar valores online.

---

## 3. Cómo leer el programa

1. `_info.md`: familia de CPU (S7-1500, S7-1200, ET 200SP…), firmware, idiomas, números de bloque ocupados.
2. `SD/` → `Main` (OB1) y los OB de ciclo (`ProgramCycle`, números ≥ 123): ahí se ve qué se llama y en qué orden.
3. Cada bloque llamado, en `SD/` o `FUENTES/`.
4. `XML/` cuando necesites: comentarios de variables, valores iniciales, offsets de DB estándar, títulos de red de bloques *solo XML*, UDT y tablas de variables.

Lectura de SD:
- `Contact( x )` contacto NA; `I_Contact( x )` NC; `Coil( x )`, `S_Coil`, `R_Coil`, `I_Coil` (negada).
- Las cajas de una rama se encadenan por EN/ENO en el orden escrito.
- Parámetros: `:=` entradas e InOut; `=>` salidas; `=>  ,` salida sin conectar.
- `wire#wN` en medio de una rama = punto de unión (OR) o de reparto; `END_RUNG wire#wN` = esa rama termina en ese punto; `RUNG wire#wN` = rama que sale de ese punto.
- Textos: `S7_NetworkTitle := "MLC_x"` → busca `<Comment Id="MLC_x">` en el `.s7res`.

---

## 4. Qué formato usar para cada cosa

| Objeto | Formato | Notas |
|---|---|---|
| UDT (tipo de datos PLC) | **XML** `SW.Types.PlcStruct` | No lleva versiones: vale para cualquier CPU |
| DB global | **XML** `SW.Blocks.GlobalDB` o **fuente `.db`** | En fuente, sin versiones. **R_TRIG/F_TRIG no se admiten dentro de un DB global** |
| DB de instancia | **Fuente `.db`** (`DATA_BLOCK "X" … "FB"`) o **SD** (`DATA_BLOCK X : FB`) | O deja que lo cree TIA |
| Tabla de variables PLC | **XML** `SW.Tags.PlcTagTable` | |
| FC / FB en **KOP** | **SD** `.s7dcl` + `.s7res` | Cajas sin versión: TIA pone la de la CPU de destino |
| FC / FB en **FUP** | **SD** `.s7dcf` | Sin comprobar todavía |
| FC / FB en **SCL** | **Fuente `.scl`** | Texto SCL normal. Varias FC en una fuente se generan todas |
| OB nuevo (de ciclo, etc.) | **XML** `SW.Blocks.OB` | La forma SD de un OB nuevo (número y tipo) no está comprobada |
| Bloque *solo XML* (KOP con redes SCL) | **XML**, partiendo del del paquete | Si va a otra CPU, avisa: sus cajas llevan la versión de la CPU de origen |
| Protegidos, GRAPH, F (Safety) | **No se tocan** | Openness no los importa |

**¿Otra CPU?** Si el usuario va a importar en una CPU distinta de la de `_info.md`: lógica en SD o fuente, datos en XML. No uses KOP en XML. Usa solo instrucciones que existan en esa CPU (S7-1200 tiene menos que S7-1500; si dudas, dilo).

---

## 5. Recetas paso a paso

### A. Modificar un bloque KOP que está en `SD/`
1. Copia `SD/<Bloque>.s7dcl` y su `.s7res`.
2. Cambia solo las redes necesarias. Para redes nuevas, copia la estructura de una red existente (sección 6.3).
3. Si añades variables, decláralas en la interfaz con el mismo estilo que las existentes.
4. Si añades textos (título/comentario de red), añade sus `<Comment Id="MLC_…">` al `.s7res` con un Id nuevo y único en el bloque.
5. Mantén `S7_BlockNumber`.
6. Entrega en `SD/`. El usuario responde **Sí** a sobrescribir.

### B. Modificar un bloque *solo XML* (KOP con redes SCL)
1. Parte de `XML/<Bloque>.xml`.
2. No toques versiones ni `TemplateValue` de las cajas existentes. Si añades cajas, copia su `Part` de otro XML del paquete con la **misma instrucción**; si no hay, pide una exportación (caso K).
3. Respeta las reglas de la sección 8.
4. Incluye `_info.txt` del paquete en la entrega.

### C. Crear una FC o FB nuevo en KOP
1. Número libre (mira `_info.md`) o quita `S7_BlockNumber` para que TIA lo asigne.
2. Escribe el `.s7dcl` con la estructura de la sección 6.1 (FC) o 6.2 (FB).
3. Cada red con `{ S7_Language := "LAD" }` (y título si quieres).
4. Solo cajas de la sección 6.5 o de `SD/`. Las dudosas, en **otro bloque** aparte: si falla, solo cae ese.
5. `.s7res`: `<root />` si no hay textos, o los `<Comment>` usados.
6. Si es un FB y se llama con DB de instancia, entrega también el DB (caso G).
7. Dile al usuario **desde dónde llamarlo** (o entrega el OB, caso I).

### D. Crear o modificar un bloque SCL
1. Fuente `.scl` en `FUENTES/` (sección 7). Si existe en `FUENTES/`, parte de ella.
2. Constantes con tipo en funciones genéricas (`INT#0`, `REAL#0.5`).
3. Temporizadores y contadores IEC: instancias en un DB (`IEC_TIMER`, `IEC_COUNTER`) o en `VAR` de un FB (`TON_TIME`…).
4. Al sobrescribir desde fuente, el Add-In borra el bloque y lo regenera: **queda en la raíz de *Bloques de programa*** (avísalo) y con el número de la fuente si lo indicas.

### E. UDT nuevo o modificado
1. XML con el esqueleto de la sección 8.2.
2. Si lo modificas y lo usan DB o bloques, avisa: tras importar hay que compilar todo y quizá reinicializar los DB al cargar.

### F. DB global nuevo o modificado
1. XML (sección 8.3) o fuente `.db` (sección 7.3).
2. `MemoryLayout`: `Optimized` salvo que necesite offsets fijos (otros sistemas lo leen por dirección) → `Standard`.
3. Valores iniciales en `<StartValue>` (XML) o en la declaración `:= ` (fuente).
4. No metas `R_TRIG`/`F_TRIG` dentro.

### G. DB de instancia
- Fuente `.db`:
  ```
  DATA_BLOCK "Mi_FB_DB"
  { S7_Optimized_Access := 'TRUE' }
  VERSION : 0.1
  NON_RETAIN
  "Mi_FB"

  BEGIN

  END_DATA_BLOCK
  ```
  Para un FB del sistema (R_TRIG, F_TRIG) vale con y sin comillas: `"R_TRIG"` o `R_TRIG`.
- SD: `DATA_BLOCK Mi_FB_DB : Mi_FB` + `END_DATA_BLOCK` (sección 6.4).

### H. Tabla de variables
XML de la sección 8.4. Comprueba que las direcciones (`%M`, `%I`, `%Q`) no chocan con las existentes (están en `XML/` del paquete).

### I. OB nuevo (por ejemplo, de ciclo)
1. XML de la sección 8.5, `SecondaryType` `ProgramCycle`, número ≥ 123 libre.
2. Un OB de ciclo se ejecuta **detrás del OB1**, por orden de número, en cada ciclo. Sirve para añadir lógica sin tocar `Main`.
3. **No llames el mismo bloque desde dos sitios** (por ejemplo, desde `Main` y desde un OB nuevo) salvo que sea intencionado: se ejecutaría dos veces por ciclo.

### J. Llevar el programa (o parte) a otra CPU
1. Lógica KOP en SD, SCL en fuente, datos en XML.
2. Revisa instrucciones que la CPU de destino no tenga.
3. El usuario importa el .zip en el PLC de destino; el Add-In elige SD si la CPU es distinta.

### K. Necesitas una instrucción que no conoces
No la escribas a ciegas. Pídele al usuario:
1. En un proyecto de pruebas, crear una **FC en KOP** con **una caja por red**, conectando todos los pines a variables temporales del tipo real.
2. Si la caja tiene desplegable de tipo, elegir el que se vaya a usar.
3. Compilar y exportarla con *XML → Exportar selección*.
4. Pasarte el .zip.
Copia de ahí la sintaxis exacta. Hasta tenerla, puedes ofrecer la alternativa en SCL.

### L. Corregir tras importar
1. Lee el resumen: `ERROR` (no entró), `NO VÁLIDO` (el Add-In lo frenó antes), `OMITIDO` (ya creado por otro fichero), errores de compilación.
2. Busca el mensaje en la sección 10.
3. Si un `.s7dcl` da *Failure* sin más, TIA no dice la línea: divide el bloque o quita lo dudoso hasta encontrarlo.
4. Si TIA se cerró: `Import_XML\progreso.txt` del proyecto; el último `IMPORTANDO` sin `OK` ni `ERROR` es el culpable.
5. Los errores de compilación se refieren a líneas **del cuerpo** del bloque SCL (contando desde `BEGIN`) o a redes KOP. Si no sabes en qué bloque es, pide al usuario que haga doble clic en el error (pestaña *Info → Compile*) y te diga el bloque.
6. Entrega solo lo corregido (con **Sí** a sobrescribir), o usa *Deshacer última importación* y entrega todo de nuevo.

### M. Comprobar que funciona (pruebas)
Patrón usado y comprobado:
1. Un DB de resultados con un `Bool` por prueba y un contador `Vida` (DInt) que suma 1 en cada ciclo.
2. Cada prueba calcula y compara con el valor esperado; si coincide, pone su `Bool` a TRUE (`Coil` o `S_Coil` si es un pulso).
3. Una FC SCL cuenta los TRUE y compara con el total.
4. Un OB de ciclo (XML) llama a todo.
5. El usuario carga en PLCSIM (CPU igual a la del proyecto, *Support simulation during block compilation* activado), pone RUN y mira el DB con *Monitor all* (columna *Monitor value*).
6. Si todo sale FALSE: la CPU está en STOP o no se cargó. Ver *Diagnostics buffer*; *"Maximum cycle time exceeded"* suele ser del programa de planta en PLCSIM → probar en un proyecto vacío.

---

## 6. Referencia SIMATIC SD (KOP) — comprobada en V20

### 6.1 FC
```
{
    S7_Optimized := "TRUE";
    S7_Language := "LAD";
    S7_BlockNumber := "41001";
    S7_BlockTitle := "MLC_T0"
}
FUNCTION "Mi_FC" : Void
    VAR_INPUT 
        Entrada : Int;
    END_VAR
    VAR_OUTPUT 
        Salida : Int;
    END_VAR
    VAR_TEMP 
        Aux : DInt;
    END_VAR

    {
      S7_Language := "LAD";
      S7_NetworkTitle := "MLC_T1"
    }
    NETWORK
        RUNG wire#powerrail
            Contact( "Mi_DB".Marcha )
            Coil( "Mi_DB".Motor )
        END_RUNG

    END_NETWORK

END_FUNCTION
```
- Fichero **UTF-8 con BOM** y saltos **CRLF**. La sangría no importa.
- `S7_BlockNumber` y `S7_BlockTitle` son opcionales.
- `.s7res` con textos:
  ```
  <root>
    <Comment Id="MLC_T0">
      <MultiLanguageText Lang="es-ES">Título del bloque</MultiLanguageText>
    </Comment>
    <Comment Id="MLC_T1">
      <MultiLanguageText Lang="es-ES">Título de la red 1</MultiLanguageText>
    </Comment>
  </root>
  ```
  Sin textos: `<root />`. Usa los idiomas que dice `_info.md`.
- Comentario de una variable: `{ S7_MLC := "MLC_v1" }` delante de ella.

### 6.2 FB con multiinstancias
```
FUNCTION_BLOCK "Mi_FB"
    VAR_INPUT 
        Enable : Bool;
    END_VAR
    VAR_OUTPUT 
        Q : Bool;
    END_VAR
    VAR 
        {
             InstructionName := "IEC_TIMER";
             LibVersion := "1.0"
        }
        Tmr : IEC_TIMER;
        Llamadas : DInt;
    END_VAR

    {
      S7_Language := "LAD"
    }
    NETWORK
        RUNG wire#powerrail
            Contact( #Enable )
            #Tmr.TON{ time_type := Time }(
                PT := T#100MS, 
                ET =>  
            )
            Coil( #Q )
        END_RUNG

    END_NETWORK

END_FUNCTION_BLOCK
```
Multiinstancia de un FB de usuario: `C1 : _.Otro_FB;` y se llama `#C1( Param := … )`.

### 6.3 Redes y ramas
- Serie (AND): cajas/contactos uno detrás de otro en la misma `RUNG`.
- **OR** (comprobado):
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
- **Una condición, varias salidas** (comprobado):
  ```
  RUNG wire#powerrail
      Contact( True )
      wire#w1
      Coil( "DB".Salida_1 )
  END_RUNG
  RUNG wire#w1
      Coil( "DB".Salida_2 )
  END_RUNG
  ```
- **Nunca** empieces una rama con `RUNG wire#wN` si ese cable solo es destino de otras ramas: TIA rechaza el bloque entero.
- Varias `RUNG wire#powerrail` en una `NETWORK` = varias ramas independientes.

### 6.4 DB en SD
```
        {
           S7_BlockNumber := "2111";
           S7_Optimized := "FALSE";
           S7_StandardRetain := "FALSE"
        }
    DATA_BLOCK Mi_DB
        VAR
            { S7_Setpoint := "False" }
            Msg : _.Mi_UDT;
            Error : Int;
        END_VAR
    END_DATA_BLOCK
```
DB de instancia: `DATA_BLOCK Mi_FB_DB : Mi_FB` + `END_DATA_BLOCK`. Tipos de usuario: `_.Nombre`.

### 6.5 Cajas comprobadas (importan, compilan y funcionan en S7-1500 V4.0)

| Familia | Sintaxis |
|---|---|
| Bits | `Contact( x )`, `I_Contact( x )`, `Coil( x )`, `S_Coil( x )`, `R_Coil( x )`, `I_Coil( x )`, `Not()`, `PBox( mem )`, `NBox( mem )` |
| Flancos con operando | `P_Contact( Top := señal, Bottom := mem )`, `N_Contact( Top := …, Bottom := … )`, `P_Coil( Top := bit_salida, Bottom := mem )` |
| Biestable SR | `S_SR( Top := mem, R1 := reset )` seguido de `Coil( Q )` |
| Temporizadores | `"DB".t.TP{ time_type := Time }( PT := T#100MS, ET =>  )` · igual `TON`, `TOF`; `TONR`: `( R := …, PT := …, ET =>  )`. Instancia `IEC_TIMER` |
| Contadores | `"DB".c.CTU{ value_type := Int }( R := …, PV := 3, CV =>  )` · `CTD( LD := …, PV := …, CV =>  )` · `CTUD( CD := …, R := …, LD := …, PV := …, QD =>  , CV =>  )`. Instancia `IEC_COUNTER`. Cuenta por flanco de la entrada de la rama |
| Comparar | `Eq{ SrcType := Int }( IN1 := …, IN2 := … )` · `Ne`, `Gt`, `Ge`, `Lt`, `Le`; tipos Int, DInt, Real, Word, UInt, String |
| Sumar/restar/mult./dividir | `Add{ Card := 2; Type := AUTO; DisableENO := TRUE }( IN1 := …, IN2 := …, OUT => … )` · `Sub{ Type := AUTO; … }` · `Mul{ Card := 2; Type := AUTO; … }` · `Div{ Type := AUTO; … }` (al menos una entrada con tipo) |
| Funciones con tipo | `Sqrt{ SrcType := Real; DisableENO := TRUE }( IN := …, OUT => … )` · igual `Sqr`, `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Exp`, `Ln`, `Frac` (Real); `Mod`, `Neg`, `Abs` (Int) |
| Lógica de palabras | `And{ Card := 2; SrcType := Word; DisableENO := TRUE }( IN1, IN2, OUT => )` · `Or`, `Xor`; `Inv{ SrcType := Word; … }( IN, OUT => )` |
| Desplazar/rotar | `Shr{ SrcType := Word; DisableENO := TRUE }( IN := …, N := 4, OUT => … )` · `Shl`, `Ror`, `Rol` |
| Selección | `Mux{ Card := 2; SrcType := Int; DisableENO := TRUE }( K := 1, IN0 := …, IN1 := …, ELSE := 0, OUT => … )` · `Demux{ Card := 2; SrcType := Int; … }( K := …, IN := …, OUT0 => …, OUT1 => …, ELSE =>  )` |
| Redondeo | `Round{ SrcType := Real; DestType := DInt; DisableENO := TRUE }( IN := …, OUT => … )` · `Ceil`, `Floor`, `Trunc` |
| Mover | `Move{ Card := 1; DisableENO := TRUE }( IN := …, OUT1 => … )` (Card 2 → `OUT2`) · `Swap{ SrcType := Word; … }` · `Convert{ SrcType := Int; DestType := Real; … }` · `S_Move{ DisableENO := TRUE }( IN := 'x', OUT => … )` · `Calculate{ Card := 3; SrcType := Int; DisableENO := TRUE; Equation := (IN1 * IN2) + IN3 }( … )` |
| Serializar | `Serialize{ src_type := Variant; dest_type := Variant }( SRC_VARIABLE := …, Ret_Val => …, DEST_ARRAY => …, POS := … )` · `Deserialize{ … }( SRC_ARRAY := …, Ret_Val => …, DEST_VARIABLE => …, POS := … )` |
| Texto | `CONCAT{ str_type := String; card := 2; DisableENO := TRUE }( IN1, IN2, OUT => )` · `LEN{ str_type := String; return_type := Int; … }` · `MAX_LEN{ SrcType := String }( IN, OUT => )` · `VAL_STRG{ value_type := Int; str_type := String }( IN, SIZE, PREC, FORMAT, P := var, OUT => )` · `STRG_VAL{ src_type := String; dest_type := Int }( IN, FORMAT, P := var, OUT => )` · `Strg_TO_Chars{ ptr_type := Variant; strg_type := String }( Strg, pChars := 0, Cnt =>, Chars := )` · `Chars_TO_Strg{ ptr_type := Variant; str_type := String }( … )` |
| Arrays | `Upper_Bound{ src_type := Variant; DisableENO := FALSE }( ARR := …, DIM := 1, OUT => … )` · `Lower_Bound{ … }` · `BLKMOV{ blk_type := Variant }( SRCBLK := …, RET_VAL => …, DSTBLK => … )` |
| Reloj | `RD_SYS_T{ date_type := DTL }( RET_VAL => …, OUT => … )` |
| Llamadas | FC: `"Mi_FC"( Entrada := …, Salida => … )` · FC con retorno: `"Mi_FC"( A := 2, B := 3, Ret_Val => … )` · FB: `"Mi_FB_DB"( Param := … )` · multiinstancia: `#Inst( … )` · sin parámetros: `"Mi_FC"()` |

**Más cajas comprobadas en marcha (24 pruebas, todas TRUE; sintaxis sacada de una exportación real).** Ojo: **el nombre SD no siempre es el de TIA**:

| En TIA | En SD |
|---|---|
| INC / DEC | `Inc{ DestType := Int; DisableENO := TRUE }( "DB".x )` (un solo parámetro, sin nombre) · `Dec{ … }( … )` |
| MIN / MAX | `MIN{ value_type := Int; card := 2; DisableENO := TRUE }( IN1 := …, IN2 := …, OUT => … )` · `MAX{ … }` |
| LIMIT | `LIMIT{ value_type := Int; DisableENO := TRUE }( MN := …, IN := …, MX := …, OUT => … )` |
| EXPT | `Expt{ DestType := Real; SrcType := Real; DisableENO := TRUE }( IN1 := …, IN2 := …, OUT => … )` |
| DECO / ENCO | `DECO{ value_type := UInt; return_type := DWord; DisableENO := TRUE }( IN := …, OUT => … )` · `ENCO{ src_type := DWord; DisableENO := TRUE }( IN := …, OUT => … )` |
| SEL | `SEL{ value_type := Int }( G := …, IN0 := …, IN1 := …, OUT => … )` |
| IN_RANGE / OUT_RANGE | `InRange{ SrcType := Real }( MIN := …, IN := …, MAX := … )` · `OutRange{ … }` (contactos: llevan algo detrás) |
| OK / NOT_OK | `OK( "DB".r )` · `NOK( "DB".r )` (contactos) |
| R_TRIG / F_TRIG | `"Mi_Rtrig_DB".R_TRIG( CLK := …, Q => … )` · `"Mi_Ftrig_DB".F_TRIG( … )` (DB de instancia propio) |
| RS | `S_RS( Top := mem, S1 := … )` (con un contacto delante: la entrada R no puede ir directa a la barra) |
| SET_BF | `S_BitfieldCoil( Top => "DB".Bits[0], N := 3 )` |
| MOVE_BLK / UMOVE_BLK | `MoveBlockI{ DisableENO := TRUE }( IN := "DB".A[0], COUNT := …, OUT => "DB".B[0] )` · `MoveBlockU{ … }` |
| FILL_BLK | `FillBlockI{ DisableENO := TRUE }( IN := …, COUNT := …, OUT => "DB".A[0] )` |
| MOVE_BLK_VARIANT | `MOVE_BLK_VARIANT( SRC := …, COUNT := …, SRC_INDEX := …, DEST_INDEX := …, Ret_Val => …, DEST => … )` (sin plantillas) |
| NORM_X / SCALE_X | `Normalize{ SrcType := Int; DestType := Real; DisableENO := TRUE }( MIN, VALUE, MAX, OUT => )` · `Scale_X{ SrcType := Real; DestType := Int; DisableENO := TRUE }( … )` |

También comprobadas: RESET_BF → `R_BitfieldCoil( Top => "DB".Bits[1], N := 2 )` y UFILL_BLK → `FillBlockU{ DisableENO := TRUE }( IN, COUNT, OUT => )`.

Reglas de las plantillas `{ }`:
- Una sola plantilla va en línea: `Eq{ SrcType := Int }`. Varias, en bloque separadas por `;`.
- `Type := AUTO` **solo** en Add, Sub, Mul, Div. En el resto, el tipo con **`SrcType := …`** (`Type := Real` deja el tipo en *Undef*).
- `DisableENO := TRUE` quita la salida ENO; `FALSE` la deja para encadenar.
- Con todas las entradas constantes sin tipo (`IN1 := 6, IN2 := 7`), TIA no sabe el tipo: usa al menos una variable o `INT#6`.

**Sintaxis desconocida** (pide exportación, caso K): CountOfElements (no existe en KOP) y todo lo de instrucciones avanzadas, comunicación y tecnología que no aparezca en `SD/` ni en esta guía.

---

## 7. Referencia de fuentes (`.scl`, `.db`, `.udt`) — comprobada

### 7.1 FC y FB
```
FUNCTION "Mi_FC" : Int
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
   VAR_INPUT
      A : Int;
      B : Int;
   END_VAR
   VAR_TEMP
      i : Int;
   END_VAR

BEGIN
	#Mi_FC := #A + #B;
END_FUNCTION

FUNCTION_BLOCK "Mi_FB"
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
   VAR_INPUT
      N : Int;
   END_VAR
   VAR_OUTPUT
      Suma : Int;
   END_VAR
   VAR
      Tmr : TON_TIME;
   END_VAR

BEGIN
	#Tmr(IN := TRUE, PT := T#100MS);
END_FUNCTION_BLOCK
```

### 7.2 Lo comprobado en SCL
IF/ELSIF/ELSE, CASE, FOR, WHILE, REPEAT/UNTIL, EXIT, CONTINUE, GOTO con etiqueta, REGION, CONCAT, LEN, LEFT, SQRT/SIN/…/EXP/LN, `**`, ABS, MIN, MAX, LIMIT, MOD, conversiones (`INT_TO_DINT`, `DINT_TO_REAL`, `REAL_TO_INT`), ROUND/CEIL/FLOOR/TRUNC, SCALE_X, NORM_X, AND/OR/XOR/NOT de Word, DECO, ENCO, SEL, MUX, SHR/SHL/ROR/ROL, SWAP, MOVE_BLK, FILL_BLK, MOVE_BLK_VARIANT, `.%X2` (bit de un Word), arrays de UDT, FC con retorno, RUNTIME, GET_ERR_ID.

Temporizadores y contadores IEC en un DB: `"DB".t.TON(IN := …, PT := T#100MS);` y `"DB".t.Q` · `"DB".c.CTU(CU := …, R := FALSE, PV := 3);` y **`"DB".c.QU`** (CTD: `.QD`; no hay `.Q`).
R_TRIG/F_TRIG con DB de instancia: `"Mi_Rtrig_DB"(CLK := x);` y `"Mi_Rtrig_DB".Q`.

Cuidado:
- Constantes con tipo en funciones genéricas: `SCALE_X(MIN := INT#0, VALUE := REAL#0.5, MAX := INT#100)`, `LIMIT(MN := INT#0, IN := x, MX := INT#4)`.
- Guarda el resultado de funciones genéricas (MIN, MAX, SEL, MUX, DECO…) en una variable con tipo antes de comparar.
- `x := -"DB".a = -5` → *Invalid assignment*: usa una variable intermedia.
- `CountOfElements` solo con un parámetro **Variant** del propio bloque (`VAR_IN_OUT Arr : Variant;` y `CountOfElements(#Arr)`).

### 7.3 DB global en fuente
```
DATA_BLOCK "Mi_DB"
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
NON_RETAIN
   VAR 
      Tmr : IEC_TIMER;
      Cnt : IEC_COUNTER;
      Valor : Int;
   END_VAR

BEGIN

END_DATA_BLOCK
```
`{ S7_Optimized_Access := 'FALSE' }` para acceso estándar. **No** `R_TRIG`/`F_TRIG` aquí (*"Data type R_TRIG is not allowed here"*).

---

## 8. Referencia SimaticML (XML)

### 8.1 Reglas
- `<Document>` con `<Engineering version="V20" />` y **un solo** objeto `SW.*`.
- Interfaz: `xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5"`.
- `ID` de `ObjectList` en hexadecimal y **únicos** en todo el documento.
- Textos en los **idiomas del proyecto** (`_info.md`). Si sobra alguno, el Add-In lo adapta, pero mejor generarlo bien.
- Tipos de UDT: `Datatype="&quot;Mi_UDT&quot;"`. Arrays: `Array[0..9] of Int`. Struct: `Datatype="Struct"` con los miembros dentro.
- Instrucciones con versión en la interfaz: `Datatype="IEC_TIMER" Version="1.0"`.
- **SCL en XML** (evitar): una sola `CompileUnit`, tokens con `UId` únicos, índices con `<Token Text="["/>`/`"]"` y **sin** `AccessModifier`.
- **KOP en XML** (evitar): un `Access` por uso; cables de entrada e InOut `IdentCon → NameCon`, de salida `NameCon → IdentCon`; versión y `TemplateValue` copiados de un XML real de la misma CPU (Serialize/Deserialize 2.2: `src_type`/`dest_type` = `Variant`, pin `Ret_Val`).

### 8.2 UDT
```xml
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <Engineering version="V20" />
  <SW.Types.PlcStruct ID="0">
    <AttributeList>
      <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
        <Section Name="None">
          <Member Name="Valor" Datatype="Int"><Comment><MultiLanguageText Lang="es-ES">Valor</MultiLanguageText></Comment></Member>
          <Member Name="Texto" Datatype="String[10]" />
        </Section>
      </Sections></Interface>
      <Name>Mi_UDT</Name>
      <Namespace />
    </AttributeList>
    <ObjectList>
      <MultilingualText ID="1" CompositionName="Comment"><ObjectList>
        <MultilingualTextItem ID="2" CompositionName="Items"><AttributeList><Culture>es-ES</Culture><Text>Comentario</Text></AttributeList></MultilingualTextItem>
      </ObjectList></MultilingualText>
      <MultilingualText ID="3" CompositionName="Title"><ObjectList>
        <MultilingualTextItem ID="4" CompositionName="Items"><AttributeList><Culture>es-ES</Culture><Text /></AttributeList></MultilingualTextItem>
      </ObjectList></MultilingualText>
    </ObjectList>
  </SW.Types.PlcStruct>
</Document>
```

### 8.3 DB global
Como el UDT, pero `SW.Blocks.GlobalDB`, `Section Name="Static"` y en `AttributeList`:
`<AutoNumber>false</AutoNumber>`, `<MemoryLayout>Optimized</MemoryLayout>` (o `Standard`), `<Name>…</Name>`, `<Namespace />`, `<Number>…</Number>`, `<ProgrammingLanguage>DB</ProgrammingLanguage>`.
Miembros: `<Member Name="X" Datatype="Int" Remanence="NonRetain" Accessibility="Public">` y valor inicial con `<StartValue>5</StartValue>` (cadenas `'XYZ'`, Word `16#1234`, Bool `true`).

### 8.4 Tabla de variables
```xml
<SW.Tags.PlcTagTable ID="0">
  <AttributeList><Name>Mis_Variables</Name></AttributeList>
  <ObjectList>
    <SW.Tags.PlcTag ID="1" CompositionName="Tags">
      <AttributeList>
        <DataTypeName>Bool</DataTypeName>
        <ExternalAccessible>true</ExternalAccessible>
        <ExternalVisible>true</ExternalVisible>
        <ExternalWritable>true</ExternalWritable>
        <LogicalAddress>%M950.0</LogicalAddress>
        <Name>Mi_Bit</Name>
      </AttributeList>
      <ObjectList>(MultilingualText Comment con ID 2, 3…)</ObjectList>
    </SW.Tags.PlcTag>
  </ObjectList>
</SW.Tags.PlcTagTable>
```

### 8.5 OB de ciclo que llama a FC sin parámetros
`SW.Blocks.OB` con `<SecondaryType>ProgramCycle</SecondaryType>`, `<ProgrammingLanguage>LAD</ProgrammingLanguage>`, `<Number>123+</Number>`, interfaz con `Section Name="Input"` (`Initial_Call` y `Remanence`, `Datatype="Bool" Informative="true"`), `Temp`, `Constant`. Cada red:
```xml
<SW.Blocks.CompileUnit ID="4" CompositionName="CompileUnits">
  <AttributeList>
    <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
      <Parts><Call UId="21"><CallInfo Name="Mi_FC" BlockType="FC" /></Call></Parts>
      <Wires><Wire UId="22"><Powerrail /><NameCon UId="21" Name="en" /></Wire></Wires>
    </FlgNet></NetworkSource>
    <ProgrammingLanguage>LAD</ProgrammingLanguage>
  </AttributeList>
  <ObjectList>(MultilingualText Comment y Title con ID siguientes)</ObjectList>
</SW.Blocks.CompileUnit>
```

---

## 9. Lo que NO hay que hacer (cierra TIA o falla siempre)

| No hagas | Qué pasa |
|---|---|
| `FILL_BLK{…}`, `MOVE_BLK{…}`, `NORM_X{ … out_type … }` en SD (los nombres buenos son `FillBlockI`, `MoveBlockI`, `Normalize`) | **TIA Portal se cierra** al importar. El Add-In bloquea esas formas (NO VÁLIDO) |
| Nombre de caja inventado (`R_RS`, `SET_BF`, `IN_Range`…): el nombre SD suele ser otro (`S_RS`, `S_BitfieldCoil`, `InRange`) | Importa como llamada a `<Nombre>_Callee` y no compila, o no importa |
| `RUNG wire#wN` que solo es destino | El bloque entero no importa |
| `Type := AUTO` fuera de Add/Sub/Mul/Div | No compila (*IsAuto not supported*) |
| `Type := Real` en vez de `SrcType := Real` | No compila (*Undef*) |
| R_TRIG/F_TRIG dentro de un DB global | No genera el DB |
| KOP en XML con versiones inventadas | No importa (*TemplateValue … missing / invalid value*) |
| SCL en XML con varias CompileUnit o `AccessModifier` | No importa |
| XML con un idioma que el proyecto no tiene | No importa (el Add-In lo adapta) |
| Llamar el mismo bloque desde dos OB | Se ejecuta dos veces por ciclo |
| Tocar bloques protegidos, GRAPH o F | Openness no los importa |

---

## 10. Mensajes de error y solución

| Mensaje | Causa | Solución |
|---|---|---|
| `ImportFromDocuments terminó con estado Failure` | Sintaxis SD que TIA no acepta (TIA no dice la línea) | Revisa ramas, nombres de cajas y plantillas contra la sección 6; divide el bloque para localizarlo |
| `…estado null` y TIA se cierra | Caja SD que rompe TIA | Quítala; ver sección 9 y `progreso.txt` |
| `The 'ImportFromDocuments' method only supports the 'Override' option` | Versión antigua del Add-In | Actualizar el Add-In |
| `the specified culture does not exist within the current project` | Idioma de más en el XML | El Add-In ≥ 2.3.3 lo adapta; usa los idiomas de `_info.md` |
| `AccessModifier is not supported` | SCL en XML con `AccessModifier` | Quitarlo, o mejor usar fuente `.scl` |
| `does not support the use of multiple compilation units` | SCL en XML con varias redes | Una sola CompileUnit, o fuente `.scl` |
| `TemplateValue … missing` / `has an invalid value` | KOP en XML con versión/plantillas inventadas | Copiar de un XML real o usar SD |
| `Please select a data type` / `At least one constant must be typed` | Todas las entradas son constantes sin tipo | Una variable o `INT#…` |
| `The property 'IsAuto' is not supported` | `Type := AUTO` donde no vale | `SrcType := <tipo>` |
| `…does not match the data type Undef` + `Data type Void is not permitted` | Tipo mal indicado (`Type := Real`) | `SrcType := Real` |
| `The referenced block "X_Callee" no longer exists` | Nombre de caja desconocido | Pedir exportación (caso K) |
| `Data type R_TRIG is not allowed here` | R_TRIG en DB global | DB de instancia propio |
| `Tag "DB".x.Q not defined` (contador) | `IEC_COUNTER` no tiene `.Q` | `.QU` / `.QD` |
| `Invalid assignment` (SCL) | Constante sin tipo, negativo comparado, `CountOfElements` con array directo | Ver sección 7.2 |
| `No non-typed constants can be used at this location` | Función genérica con constantes sin tipo | `INT#`, `REAL#` |
| `Tag "X" not defined` / `Missing instance DB` | Falta un DB/variable o no se importó | Entregar el objeto que falta o revisar el resumen |
| `Inconsistent blocks … cannot be exported` | Bloque inconsistente al hacer copia | Compilar; el Add-In ≥ 2.3.2 sigue igualmente |
| `The composition already contains an object with the same … Name` | Ya existe | Responder **Sí** a sobrescribir |
| Compila pero todo FALSE en PLCSIM | CPU en STOP o no cargado | *Diagnostics buffer*; cargar *Software (all)*; CPU igual a la del proyecto |
| `Maximum program cycle time exceeded` | Ciclo demasiado largo (PLCSIM) | Proyecto de pruebas limpio, o subir el tiempo de ciclo máximo |

---

## 11. Cómo entregar

```
<nombre>_para_importar.zip
    XML/        UDT, DB, tablas de variables, OB (y KOP en XML solo si no hay otra)
    SD/         FC/FB KOP en SIMATIC SD (.s7dcl + .s7res), DB en SD si procede
    FUENTES/    .scl / .db / .udt
    _info.txt   solo si entregas KOP en XML: copia el del paquete original
    LEEME.md    qué es, qué importar, qué responder a "sobrescribir", qué llamar desde dónde, cómo comprobarlo
```

Plantilla de `LEEME.md`:
```
# <Qué es>

| Fichero | Objeto | Nº | Qué hace |
|---|---|---|---|

## Importar
1. Guardar el proyecto.
2. XML → Importar ficheros… → este .zip (sin descomprimir). Sobrescribir: Sí / No.
3. Resumen: compilación con 0 errores. Si no, pasar el resumen.

## Después
- Llamar a "<bloque>" desde <dónde> (si no va incluido).
- Cargar en el PLC (o PLCSIM) y comprobar <qué>.

## Deshacer
XML → Deshacer última importación.
```

Mensaje al usuario: una o dos frases de qué has hecho, el .zip, los pasos de importación y cómo comprobarlo.

---

## 12. Comprobación final antes de entregar

1. ¿Cada objeto en el formato de la sección 4, un objeto por fichero, nombre de fichero = nombre del objeto?
2. ¿Solo cajas de la sección 6.5 o de `SD/`? ¿Ninguna de la sección 9?
3. ¿`SrcType` donde toca y `Type := AUTO` solo en Add/Sub/Mul/Div? ¿Ninguna caja con todas las entradas constantes sin tipo?
4. ¿Los UDT, DB, instancias y FC que usa la lógica van en la entrega o ya existen?
5. ¿Números de bloque libres? ¿Ningún bloque llamado dos veces por error?
6. ¿SD en UTF-8 con BOM y CRLF, con su `.s7res` y los `MLC_` coincidiendo?
7. ¿XML con un solo objeto, `ID` únicos, idiomas del proyecto?
8. ¿SCL con constantes tipadas en funciones genéricas y contadores con `.QU`/`.QD`?
9. ¿Lo dudoso va en bloques aparte?
10. ¿`LEEME.md` completo y pasos claros para el usuario?
