# -*- coding: utf-8 -*-
"""Tanda 3A: control del programa, Variant, Array DB, DB, interrupciones, avisos, RTM (lo que se puede probar en PLCSIM).
Sintaxis SD de la exportación real Aprender_T3 (TIA V20)."""
import re
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda3a_programa"))
MEM = m
D, R = '"T7_Datos"', '"T7_Resultado"'
VARS, RES, FCS = [], [], []
num = [44001]
def v(n, t, start=None, com=""):
    VARS.append((n, t, start, com)); return f"{D}.{n}"
def okb(n, d): RES.append((n, d)); return f"{R}.{n}"
def fc(nombre, desc, *redes, cab="FUNCTION", extra="", llamar=True):
    b = Bloque(f"T7_L_{nombre}", num[0], f"Tanda 3 KOP: {desc}"); num[0] += 1
    for t, rs in redes: b.add(t, *rs)
    b.sd(cab, extra)
    if llamar: FCS.append(b.nombre)
    return b
def raw(t): return t + ("\n" if not t.endswith("\n") else "")
def con_label(etq, *rungs):
    """Red que empieza con una etiqueta de salto: Label(X) antes de los RUNG."""
    return (f"        Label({etq})\n",) + rungs
eqs = lambda t, a, b: cmp("Eq", t, a, b)
F = v("Falso", "Bool", "false"); VERD = v("Verdadero", "Bool", "true")

# ---------- Saltos
f1, l1 = v("Jmp_Fallo", "Bool"), v("Jmp_Llega", "Bool")
fc("JMP", "JMP y LABEL", ("Saltar", [rung(C("True"), raw("            JumpCoil( SALTO )"))]),
   ("No se ejecuta", [rung(C("True"), S(f1))]),
   ("Destino", con_label("SALTO", rung(C("True"), Q(l1)))),
   ("Comprobar", [rung(C(l1), NC(f1), Q(okb("L_JMP", "JMP + LABEL (JumpCoil, Label)")))]))
f2, l2 = v("Jmpn_Fallo", "Bool"), v("Jmpn_Llega", "Bool")
fc("JMPN", "JMPN", ("Saltar si 0", [rung(C(F), raw("            I_JumpCoil( SALTO2 )"))]),
   ("No se ejecuta", [rung(C("True"), S(f2))]),
   ("Destino", con_label("SALTO2", rung(C("True"), Q(l2)))),
   ("Comprobar", [rung(C(l2), NC(f2), Q(okb("L_JMPN", "JMPN (I_JumpCoil)")))]))
k1 = v("UInt_1", "UInt", "1"); f3, l3 = v("JL_Fallo", "Bool"), v("JL_Llega", "Bool")
fc("JMP_LIST", "JMP_LIST", ("Lista", [rung(raw(f"            JumpList{{ Card := 2 }}(\n                K := {k1}, \n                DEST0 => LA, \n                DEST1 => LB\n            )"))]),
   ("Destino 0 (no)", con_label("LA", rung(C("True"), S(f3)))),
   ("Ir al final", [rung(C("True"), raw("            JumpCoil( LFIN )"))]),
   ("Destino 1 (sí)", con_label("LB", rung(C("True"), Q(l3)))),
   ("Comprobar", con_label("LFIN", rung(C(l3), NC(f3), Q(okb("L_JMP_LIST", "JMP_LIST (JumpList), K = 1"))))))
i5 = v("Int_5", "Int", "5"); i3 = v("Int_3", "Int", "3"); f4, l4 = v("SW_Fallo", "Bool"), v("SW_Llega", "Bool")
fc("SWITCH", "SWITCH", ("Switch", [rung(raw(f'''            Switch{{
                Card := 2; 
                CASE0 := "=="; 
                CASE1 := "=="; 
                SrcType := Int
            }}(
                K := {i5}, 
                CASE0 := {i3}, 
                CASE1 := {i5}, 
                ELSE => SE, 
                DEST0 => S0, 
                DEST1 => S1
            )'''))]),
   ("ELSE (no)", con_label("SE", rung(C("True"), S(f4)))),
   ("Caso 0 (no)", con_label("S0", rung(C("True"), S(f4)))),
   ("Ir a caso 1", [rung(C("True"), raw("            JumpCoil( S1 )"))]),
   ("Caso 1 (sí)", con_label("S1", rung(C("True"), Q(l4)))),
   ("Comprobar", [rung(C(l4), NC(f4), Q(okb("L_SWITCH", "SWITCH (Switch), K = 5 → caso 1")))]))
# RETURN: pendiente (la sintaxis SD de RET hay que sacarla de una exportación compilada)
fc("RE_TRIGR", "RE_TRIGR", ("RE_TRIGR", [rung(C("True"), raw("            RE_TRIGR()"), Q(okb("L_RE_TRIGR", "RE_TRIGR")))]))
fc("GET_ERROR", "GET_ERROR y GET_ERR_ID",
   ("GetError", [rung(C("True"), raw("            GetError(ERROR => #err)"))]),
   ("GetErrorID", [rung(C("True"), raw("            GetErrorID(ID => #idErr)"))]),
   ("Comprobar", [rung(eqs("Word", "#err.ERROR_ID", "16#0"), eqs("Word", "#idErr", "16#0"), Q(okb("L_GET_ERROR", "GET_ERROR (GetError) y GET_ERR_ID (GetErrorID) sin error")))]),
   extra='''    VAR_TEMP 
        {
             InstructionName := "ErrorStruct";
             LibVersion := "1.0"
        }
        err : ErrorStruct;
        idErr : Word;
    END_VAR
''')
wt = v("Int_10", "Int", "10")
fc("WAIT", "WAIT", ("WAIT 10 µs", [rung(C("True"), raw(f"            WAIT(WT := {wt})"), Q(okb("L_WAIT", "WAIT")))]))

# ---------- Variant
fc("VarGet_Sub", "VariantGet dentro de una FC con Variant", ("VariantGet", [rung(C("True"), caja("VariantGet", ["src_type := Variant", "dst_type := Variant"], ["SRC := #v", "DST => #o"]))]),
   extra='''    VAR_OUTPUT 
        o : Int;
    END_VAR
    VAR_IN_OUT 
        v : Variant;
    END_VAR
''', llamar=False)
vg = v("VarGet_Out", "Int")
fc("VariantGet", "VariantGet", ("Llamar", [rung(raw(f'            "T7_L_VarGet_Sub"(\n                o => {vg}, \n                v := {i5}\n            )'))]),
   ("Comprobar", [rung(eqs("Int", vg, "5"), Q(okb("L_VariantGet", "VariantGet (Variant → Int)")))]))
fc("VarPut_Sub", "VariantPut dentro de una FC con Variant", ("VariantPut", [rung(C("True"), caja("VariantPut", ["src_type := Variant", "dst_type := Variant"], ["SRC := #i", "DST := #v"]))]),
   extra='''    VAR_INPUT 
        i : Int;
    END_VAR
    VAR_IN_OUT 
        v : Variant;
    END_VAR
''', llamar=False)
vp, i7 = v("VarPut_Dst", "Int"), v("Int_7", "Int", "7")
fc("VariantPut", "VariantPut", ("Llamar", [rung(raw(f'            "T7_L_VarPut_Sub"(\n                i := {i7}, \n                v := {vp}\n            )'))]),
   ("Comprobar", [rung(eqs("Int", vp, "7"), Q(okb("L_VariantPut", "VariantPut (Int → Variant)")))]))

# ---------- DB de array y ATTR_DB
d3, i42, r1, r2, ar = v("DInt_3", "DInt", "3"), v("Int_42", "Int", "42"), v("Ret_Write", "Int", "-1"), v("Ret_Read", "Int", "-1"), v("ArrDB_Leido", "Int")
fc("ArrayDB", "WriteToArrayDB y ReadFromArrayDB",
   ("Escribir", [rung(C("True"), raw(f'            WriteToArrayDB(\n                db := "T7_ArrayDB", \n                index := {d3}, \n                value := {i42}, \n                Ret_Val => {r1}\n            )'))]),
   ("Leer", [rung(C("True"), raw(f'            ReadFromArrayDB(\n                db := "T7_ArrayDB", \n                index := {d3}, \n                Ret_Val => {r2}, \n                value => {ar}\n            )'))]),
   ("Comprobar", [rung(eqs("Int", r1, "0"), eqs("Int", r2, "0"), eqs("Int", ar, "42"), Q(okb("L_ArrayDB", "WriteToArrayDB + ReadFromArrayDB")))]))
ra, ln, at = v("Ret_Attr", "Int", "-1"), v("DB_Longitud", "UDInt"), v("DB_Atributos", "Byte")
fc("ATTR_DB", "ATTR_DB", ("ATTR_DB", [rung(C("True"), raw(f'            ATTR_DB(\n                REQ := True, \n                DB_NUMBER := "T7_Std", \n                RET_VAL => {ra}, \n                DB_LENGTH => {ln}, \n                ATTRIB => {at}\n            )'))]),
   ("Comprobar", [rung(eqs("Int", ra, "0"), cmp("Gt", "UDInt", ln, "0"), Q(okb("L_ATTR_DB", "ATTR_DB sobre T7_Std (longitud > 0)")))]))

# ---------- Interrupciones
r3, r4 = v("Ret_DIS_AIRT", "Int", "-1"), v("Ret_EN_AIRT", "Int", "-1")
fc("AIRT", "DIS_AIRT y EN_AIRT", ("DIS/EN", [rung(C("True"), raw(f"            DIS_AIRT(RET_VAL => {r3})"), raw(f"            EN_AIRT(RET_VAL => {r4})"))]),
   ("Comprobar", [rung(cmp("Ge", "Int", r4, "0"), Q(okb("L_AIRT", "DIS_AIRT + EN_AIRT")))]))
m1, r5, tdl = v("SrtDint_Mem", "Bool"), v("Ret_SRT_DINT", "Int", "-1"), v("T_100ms", "Time", "T#100MS")
w0, ob20 = v("Word_0", "Word", "16#0"), v("OB20_Cuenta", "DInt", None, "Lo incrementa el OB20 (alarma de retardo)")
r6, st6 = v("Ret_QRY_DINT", "Int", "-1"), v("St_QRY_DINT", "Word")
fc("SRT_DINT", "SRT_DINT y QRY_DINT con el OB20",
   ("Arrancar una vez", [rung(C("True"), f"PBox( {m1} )", raw(f"            SRT_DINT{{ obnr_type := OB_DELAY }}(\n                OB_NR := 20, \n                DTIME := {tdl}, \n                SIGN := {w0}, \n                RET_VAL => {r5}\n            )"))]),
   ("Consultar", [rung(C("True"), raw(f"            QRY_DINT{{ obnr_type := OB_DELAY }}(\n                OB_NR := 20, \n                RET_VAL => {r6}, \n                STATUS => {st6}\n            )"))]),
   ("Comprobar", [rung(eqs("Int", r6, "0"), cmp("Gt", "DInt", ob20, "0"), S(okb("L_SRT_DINT", "SRT_DINT + QRY_DINT: el OB20 se ha ejecutado")))]))
r7, cy, ph, st7, ob30 = v("Ret_QRY_CINT", "Int", "-1"), v("Ciclo_OB30", "UDInt"), v("Fase_OB30", "UDInt"), v("St_QRY_CINT", "Word"), v("OB30_Cuenta", "DInt", None, "Lo incrementa el OB30 (alarma cíclica)")
fc("QRY_CINT", "QRY_CINT con el OB30",
   ("Consultar", [rung(C("True"), raw(f"            QRY_CINT(\n                OB_NR := 30, \n                RET_VAL => {r7}, \n                CYCLE => {cy}, \n                PHASE => {ph}, \n                STATUS => {st7}\n            )"))]),
   ("Comprobar", [rung(eqs("Int", r7, "0"), cmp("Gt", "UDInt", cy, "0"), cmp("Gt", "DInt", ob30, "0"), Q(okb("L_QRY_CINT", "QRY_CINT y el OB30 se ejecuta")))]))

# ---------- Avisos: Program_Alarm (FB con multiinstancia)
fc("Alarm", "Program_Alarm (FB)", ("Program_Alarm", [rung(raw('''            #PA.Program_Alarm(
                SIG := "T7_Datos".Verdadero, 
                TIMESTAMP :=  , 
                SD_1 :=  , 
                SD_2 :=  , 
                SD_3 :=  , 
                SD_4 :=  , 
                SD_5 :=  , 
                SD_6 :=  , 
                SD_7 :=  , 
                SD_8 :=  , 
                SD_9 :=  , 
                SD_10 :=  , 
                Error => #Error, 
                Status => #Status
            )'''))]),
   ("Comprobar", [rung(NC("#Error"), cmp("Eq", "Word", "#Status", "16#0"), Q(okb("L_Program_Alarm", "Program_Alarm sin error")))]),
   cab="FUNCTION_BLOCK", extra='''    VAR 
        {
             InstructionName := "Program_Alarm";
             LibVersion := "1.0"
        }
        PA : Program_Alarm;
        Error : Bool;
        Status : Word;
    END_VAR
''', llamar=False)
w("SD/T7_L_Alarm_DB.s7dcl", '''        {
           S7_BlockNumber := "44090";
           S7_Optimized := "TRUE";
           S7_StandardRetain := "FALSE"
        }
    DATA_BLOCK T7_L_Alarm_DB : T7_L_Alarm
    END_DATA_BLOCK
''')
fc("Alarm_Llamar", "llamar al FB de Program_Alarm", ("Llamar", [rung(raw('            "T7_L_Alarm_DB"()'))]))

# ---------- RTM
r8, cq, cv, mode0 = v("Ret_RTM", "Int", "-1"), v("RTM_CQ", "Bool"), v("RTM_CV", "DInt"), v("Byte_0", "Byte", "16#0")
pv0 = v("DInt_0", "DInt", "0")
fc("RTM", "RTM (consultar el contador de horas 1)", ("RTM", [rung(C("True"), raw(f"            RTM{{ nr_type := RTM }}(\n                NR := 1, \n                MODE := {mode0}, \n                PV := {pv0}, \n                RET_VAL => {r8}, \n                CQ => {cq}, \n                CV => {cv}\n            )"))]),
   ("Comprobar", [rung(eqs("Int", r8, "0"), Q(okb("L_RTM", "RTM modo 0 (leer)")))]))

TOG = v("Tog", "Bool")
# ---------- Base
b = Bloque("T7_Base", 44000, "Tanda 3: Tog y Vida")
b.add("Tog", rung(C(TOG), "Not()", Q(TOG)))
b.add("Vida", rung(C("True"), caja("Add", ["Card := 2", "Type := AUTO", "DisableENO := TRUE"], [f"IN1 := {R}.Vida", "IN2 := 1", f"OUT => {R}.Vida"])))
b.sd()

# ---------- SCL: PEEK/POKE y READ/WRITE_BIG/LITTLE (no están en KOP), saltos y RET en SCL
SCL_RES = [okb("S_PEEK_POKE", "[SCL] POKE + PEEK en un DB de acceso estándar"), okb("S_BIG_LITTLE", "[SCL] WRITE_BIG/READ_BIG y WRITE_LITTLE/READ_LITTLE"),
           okb("S_ArrayDB", "[SCL] WriteToArrayDB / ReadFromArrayDB"), okb("S_Variant", "[SCL] VariantGet / VariantPut, TypeOf")]
CAB = """{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
   VAR_TEMP
      w : Word;
      i : Int;
      r : Int;
      buf : Array[0..9] of Byte;
      pos : DInt;
   END_VAR

BEGIN
"""
w("FUENTES/T7_S_Memoria.scl", 'FUNCTION "T7_S_Memoria" : Void\n' + CAB + f'''	// POKE / PEEK sobre el DB estándar T7_Std (número 44902), byte 0
	POKE(AREA := 16#84, DBNUMBER := 44902, BYTEOFFSET := 0, VALUE := WORD#16#1234);
	#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 44902, BYTEOFFSET := 0);
	{R}.S_PEEK_POKE := #w = WORD#16#1234 AND "T7_Std".W = WORD#16#1234;
END_FUNCTION
''')
w("FUENTES/T7_S_BigLittle.scl", 'FUNCTION "T7_S_BigLittle" : Void\n' + CAB + f'''	#pos := 0;
	#r := WRITE_BIG(SRC_VARIABLE := INT#258, DEST_ARRAY := #buf, POS := #pos);
	#pos := 0;
	#r := READ_BIG(SRC_ARRAY := #buf, POS := #pos, DEST_VARIABLE => #i);
	#r := #i;
	#pos := 0;
	#w := INT_TO_WORD(WRITE_LITTLE(SRC_VARIABLE := INT#258, DEST_ARRAY := #buf, POS := #pos));
	#pos := 0;
	#w := INT_TO_WORD(READ_LITTLE(SRC_ARRAY := #buf, POS := #pos, DEST_VARIABLE => #i));
	{R}.S_BIG_LITTLE := #r = 258 AND #i = 258 AND #buf[0] = 16#02 AND #buf[1] = 16#01;
END_FUNCTION
''')
w("FUENTES/T7_S_ArrayDB.scl", 'FUNCTION "T7_S_ArrayDB" : Void\n' + CAB + f'''	// value es Variant: una variable, no una constante
	#i := 77;
	#pos := 5;
	#r := WriteToArrayDB(db := "T7_ArrayDB", index := #pos, value := #i);
	#i := 0;
	#r := ReadFromArrayDB(db := "T7_ArrayDB", index := #pos, value => #i);
	{R}.S_ArrayDB := #i = 77;
END_FUNCTION
''')
w("FUENTES/T7_S_Variant.scl", f'''FUNCTION "T7_S_Variant_Sub" : Int
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_IN_OUT
      v : Variant;
   END_VAR
   VAR_TEMP
      x : Int;
   END_VAR

BEGIN
	IF TypeOf(#v) = Int THEN
	    VariantGet(SRC := #v, DST => #x);
	    #T7_S_Variant_Sub := #x;
	ELSE
	    #T7_S_Variant_Sub := -1;
	END_IF;
END_FUNCTION

FUNCTION "T7_S_Variant" : Void
''' + CAB + f'''	{R}.S_Variant := "T7_S_Variant_Sub"(v := {D}.Int_5) = 5;
END_FUNCTION
''')
w("FUENTES/T7_ArrayDB.db", 'DATA_BLOCK "T7_ArrayDB"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n   ARRAY[0..9] OF Int\n\nBEGIN\n\nEND_DATA_BLOCK\n')
w("XML/T7_Std.xml", gdb("T7_Std", 44902, MEM("W", "Word", "Destino de POKE (byte 0)") + MEM("Relleno", "Array[0..9] of Byte"), "Tanda 3: DB de acceso estándar para PEEK/POKE (número fijo 44902)", layout="Standard"))

lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T7_Contar.scl", f'''FUNCTION "T7_Contar" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_TEMP
      n : Int;
   END_VAR

BEGIN
	#n := 0;
{lineas}
	{R}.OK := #n;
	{R}.Todo_OK := #n = {R}.Total;
END_FUNCTION
''')
w("XML/T7_Datos.xml", gdb("T7_Datos", 44900, "".join(MEM(n, t, c, s) for n, t, s, c in VARS), "Tanda 3: datos"))
w("XML/T7_Resultado.xml", gdb("T7_Resultado", 44901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo") +
                              MEM("OK", "Int", "Correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"), "Tanda 3: un Bool por prueba"))
w("XML/T7_Ciclo.xml", ob("T7_Ciclo", 130, ["T7_Base"] + FCS + ["T7_S_Memoria", "T7_S_BigLittle", "T7_S_ArrayDB", "T7_S_Variant", "T7_Contar"], "Tanda 3: llama a todas las pruebas"))

# ---------- OB20 (alarma de retardo) y OB30 (alarma cíclica): solo cuentan
def ob_int(nombre, numero, tipo, entradas, extra_attr, miembro):
    x = ob(nombre, numero, [], f"Tanda 3: {tipo}, incrementa {miembro}")
    x = x.replace("<SecondaryType>ProgramCycle</SecondaryType>", f"<SecondaryType>{tipo}</SecondaryType>")
    if extra_attr:   # atributos en orden alfabético, como los exporta TIA
        x = x.replace("      <AutoNumber>false</AutoNumber>\n", "      <AutoNumber>false</AutoNumber>\n      <CyclicTime ReadOnly=\"false\">100000</CyclicTime>\n")
        x = x.replace("      <ProgrammingLanguage>LAD</ProgrammingLanguage>\n      <SecondaryType>", "      <PhaseShift ReadOnly=\"false\">0</PhaseShift>\n      <ProgrammingLanguage>LAD</ProgrammingLanguage>\n      <SecondaryType>")
    ent = "".join(f'          <Member Name="{n}" Datatype="{t}" Informative="true" />\n' for n, t in entradas)
    x = re.sub(r'<Section Name="Input">.*?</Section>', f'<Section Name="Input">\n{ent}        </Section>', x, flags=re.S)
    i = x.index('    <ObjectList>') + len('    <ObjectList>\n')
    # tras el comentario de bloque, la red que suma 1
    j = x.index('</MultilingualText>\n', i) + len('</MultilingualText>\n')
    return x[:j] + red_vida("T7_Datos", miembro).replace("\r\n", "\n") + x[j:]
w("XML/T7_OB20.xml", ob_int("T7_OB20", 20, "TimeDelayInterrupt", [("Sign", "Word")], "", "OB20_Cuenta"))
w("XML/T7_OB30.xml", ob_int("T7_OB30", 30, "CyclicInterrupt", [("Initial_Call", "Bool"), ("Event_Count", "Int")], "", "OB30_Cuenta"))

filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 3A: control del programa, Variant, DB, interrupciones y avisos (TIA V20)

{len(RES)} pruebas en marcha. Sintaxis SD de la exportación real `Aprender_T3`. FC 44000..{num[0]-1}, FB `T7_L_Alarm` + DB 44090, DB 44900/44901, `T7_ArrayDB` (DB de array), `T7_Std` (DB estándar, número **44902** — PEEK/POKE lo usan por número), OB 130 (ciclo), **OB20** (alarma de retardo) y **OB30** (alarma cíclica, 100 ms).

Si el OB20 o el OB30 ya existen en el proyecto, borrarlos antes (o responder Sí a sobrescribir).

1. XML → Importar ficheros… → este .zip → Sí a sobrescribir.
2. Compilar (0 errores). 
3. Cargar en PLCSIM (DB: reinicializar), RUN.
4. `T7_Resultado`: `Vida` sube, `OK` = `Total` ({len(RES)}), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print(len(RES), len(FCS), z)
