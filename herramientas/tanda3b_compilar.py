# -*- coding: utf-8 -*-
"""Tanda 3B: instrucciones que necesitan hardware real o tarjeta de memoria. SOLO importar y compilar
(ningún OB las llama). Una FC por instrucción. Sintaxis SD de la exportación real Aprender_T3."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda3b_compilar"))
NUM = {}   # número fijo por prueba
def fc(nombre, desc, temp, *rungs):
    n = f"T8_L_{nombre}"
    NUM.setdefault(n, 45001 + len(NUM))
    b = Bloque(n, NUM[n], f"Tanda 3B (solo compilar): {desc}")
    b.add(desc, *rungs)
    b.sd("FUNCTION", "    VAR_TEMP \n" + temp + "    END_VAR\n")
    return n
def raw(t): return "            " + t.strip().replace("\n", "\n            ") + "\n"
T = lambda *xs: "".join(f"        {x}\n" for x in xs)
SYS = lambda nombre, tipo, inst, ver="1.0": f"        {{\n             InstructionName := \"{inst}\";\n             LibVersion := \"{ver}\"\n        }}\n        {nombre} : {tipo};\n"
LISTA = []
def add(nombre, desc, temp, cuerpo):
    LISTA.append((fc(nombre, desc, temp, rung(C("#req"), raw(cuerpo))), desc))
B = "req : Bool;", "busy : Bool;", "ret : Int;"

add("INIT_RD", "INIT_RD", T(*B), "INIT_RD(\n    REQ := #req, \n    Ret_Val => #ret\n)")
add("CREATE_DB", "CREATE_DB", T(*B, "lo : DB_ANY;", "hi : DB_ANY;", "cnt : UDInt;", "attr : Byte;", "src : Array[0..9] of Byte;", "dbn : DB_ANY;"),
    "CREATE_DB(\n    REQ := #req, \n    LOW_LIMIT := #lo, \n    UP_LIMIT := #hi, \n    COUNT := #cnt, \n    ATTRIB := #attr, \n    SRCBLK := #src, \n    RET_VAL => #ret, \n    BUSY => #busy, \n    DB_NUM => #dbn\n)")
add("DELETE_DB", "DELETE_DB", T(*B, "dbn : DB_ANY;"), "DELETE_DB(\n    REQ := #req, \n    DB_NUMBER := #dbn, \n    Ret_Val => #ret, \n    BUSY => #busy\n)")
add("READ_DBL", "READ_DBL", T(*B, "a : Array[0..9] of Byte;", "b : Array[0..9] of Byte;"),
    "READ_DBL{\n    src_type := Variant; \n    dest_type := Variant\n}(\n    REQ := #req, \n    SRCBLK := #a, \n    RET_VAL => #ret, \n    BUSY => #busy, \n    DSTBLK => #b\n)")
add("WRIT_DBL", "WRIT_DBL", T(*B, "a : Array[0..9] of Byte;", "b : Array[0..9] of Byte;"),
    "WRIT_DBL{\n    src_type := Variant; \n    dest_type := Variant\n}(\n    REQ := #req, \n    SRCBLK := #a, \n    RET_VAL => #ret, \n    BUSY => #busy, \n    DSTBLK => #b\n)")
add("ATTACH", "ATTACH", T(*B, "ob : OB_ATT;", "ev : EVENT_ATT;"), "ATTACH(\n    OB_NR := #ob, \n    EVENT := #ev, \n    ADD := #req, \n    RET_VAL => #ret\n)")
add("DETACH", "DETACH", T(*B, "ob : OB_ATT;", "ev : EVENT_ATT;"), "DETACH(\n    OB_NR := #ob, \n    EVENT := #ev, \n    RET_VAL => #ret\n)")
add("SET_CINT", "SET_CINT", T(*B, "ob : OB_CYCLIC;", "cyc : UDInt;", "ph : UDInt;"), "SET_CINT(\n    OB_NR := #ob, \n    CYCLE := #cyc, \n    PHASE := #ph, \n    RET_VAL => #ret\n)")
add("CAN_DINT", "CAN_DINT", T(*B, "ob : OB_DELAY;"), "CAN_DINT{ ob_type := OB_DELAY }(\n    OB_NR := #ob, \n    RET_VAL => #ret\n)")
add("SET_TINT", "SET_TINT", T(*B, "ob : OB_TOD;", "sdt : Date_And_Time;", "per : Word;"),
    "SET_TINT{ obnr_type := OB_TOD }(\n    OB_NR := #ob, \n    SDT := #sdt, \n    PERIOD := #per, \n    RET_VAL => #ret\n)")
add("CAN_TINT", "CAN_TINT", T(*B, "ob : OB_TOD;"), "CAN_TINT{ ob_type := OB_TOD }(\n    OB_NR := #ob, \n    Ret_Val => #ret\n)")
add("Gen_UsrMsg", "Gen_UsrMsg", T(*B, "mode : UInt;", "txt : UInt;", "lst : UInt;") + SYS("av", "AssocValues", "AssocValues"),
    "Gen_UsrMsg(\n    Mode := #mode, \n    TextID := #txt, \n    TextListID := #lst, \n    Ret_Val => #ret, \n    AssocValues := #av\n)")
add("LED", "LED", T(*B, "hw : HW_IO;", "led : UInt;"), "LED(\n    LADDR := #hw, \n    LED := #led, \n    Ret_Val => #ret\n)")
add("RD_SINFO", "RD_SINFO", T(*B, "top : Array[0..99] of Byte;", "st : Array[0..99] of Byte;"),
    "RD_SINFO{ si_type := Variant }(\n    RET_VAL => #ret, \n    TOP_SI => #top, \n    START_UP_SI => #st\n)")
add("DeviceStates", "DeviceStates", T(*B, "hw : HW_IOSYSTEM;", "mode : UInt;", "states : Array[0..1023] of Bool;"),
    "DeviceStates(\n    LADDR := #hw, \n    MODE := #mode, \n    Ret_Val => #ret, \n    STATE := #states\n)")
add("ModuleStates", "ModuleStates", T(*B, "hw : HW_DEVICE;", "mode : UInt;", "states : Array[0..127] of Bool;"),
    "ModuleStates(\n    LADDR := #hw, \n    MODE := #mode, \n    Ret_Val => #ret, \n    STATE := #states\n)")
add("GET_DIAG", "GET_DIAG", T(*B, "hw : HW_ANY;", "mode : UInt;", "cnt : UInt;", "diag : Array[0..99] of Byte;", "det : Array[0..99] of Byte;"),
    "GET_DIAG(\n    MODE := #mode, \n    LADDR := #hw, \n    RET_VAL => #ret, \n    CNT_DIAG => #cnt, \n    DIAG := #diag, \n    DETAIL := #det\n)")
add("GEO2LOG", "GEO2LOG", T(*B, "hw : HW_ANY;") + SYS("geo", "GEOADDR", "GEOADDR"), "GEO2LOG(\n    GEOADDR := #geo, \n    RET_VAL => #ret, \n    LADDR => #hw\n)")
add("LOG2GEO", "LOG2GEO", T(*B, "hw : HW_ANY;") + SYS("geo", "GEOADDR", "GEOADDR"), "LOG2GEO(\n    LADDR := #hw, \n    Ret_Val => #ret, \n    GEOADDR := #geo\n)")

# ---- FB del sistema con DB de instancia: en SCL (el DB de instancia en la misma fuente)
SCL = [
 ("RDREC", "RDREC", "hw : HW_IO;\n      buf : Array[0..9] of Byte;", '"T8_RDREC_DB"(REQ := FALSE, ID := #hw, INDEX := 1, MLEN := 10, RECORD := #buf);'),
 ("WRREC", "WRREC", "hw : HW_IO;\n      buf : Array[0..9] of Byte;", '"T8_WRREC_DB"(REQ := FALSE, ID := #hw, INDEX := 1, LEN := 10, RECORD := #buf);'),
 ("GETIO", "GETIO", "sub : HW_SUBMODULE;\n      buf : Array[0..9] of Byte;", '"T8_GETIO_DB"(ID := #sub, INPUTS := #buf);'),
 ("SETIO", "SETIO", "sub : HW_SUBMODULE;\n      buf : Array[0..9] of Byte;", '"T8_SETIO_DB"(ID := #sub, OUTPUTS := #buf);'),
 ("Get_IM_Data", "Get_IM_Data", "hw : HW_IO;\n      buf : Array[0..99] of Byte;", '"T8_Get_IM_Data_DB"(LADDR := #hw, IM_TYPE := 0, DATA := #buf);'),
 ("Get_Name", "Get_Name", "hw : HW_IOSYSTEM;\n      s : String;", '"T8_Get_Name_DB"(LADDR := #hw, STATION_NR := 1, DATA := #s);'),
 ("DataLogCreate", "DataLogCreate", "id : DWord;\n      nom : String;\n      cab : String;\n      dat : Array[0..9] of Int;", '"T8_DataLogCreate_DB"(REQ := FALSE, RECORDS := 10, FORMAT := 1, TIMESTAMP := 1, NAME := #nom, ID := #id, HEADER := #cab, DATA := #dat);'),
 ("RecipeExport", "RecipeExport", "x : Int;", '"T8_RecipeExport_DB"(REQ := FALSE, RECIPE_DB := "T8_Receta");'),
]
for nombre, fb, temps, llamada in SCL:
    w(f"FUENTES/T8_S_{nombre}.scl", f'''DATA_BLOCK "T8_{nombre}_DB"
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
NON_RETAIN
{fb}

BEGIN

END_DATA_BLOCK

FUNCTION "T8_S_{nombre}" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_TEMP
      {temps}
   END_VAR

BEGIN
	// Solo compilar: necesita hardware real o tarjeta de memoria
	{llamada}
END_FUNCTION
''')
w("FUENTES/T8_Receta.db", 'DATA_BLOCK "T8_Receta"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n   ARRAY[0..2] OF Int\n\nBEGIN\n\nEND_DATA_BLOCK\n')

filas = "\n".join(f"| `{n}` | {d} |" for n, d in LISTA) + "\n" + "\n".join(f"| `T8_S_{n}` + `T8_{n}_DB` | {f} (SCL, FB del sistema con DB de instancia) |" for n, f, _, _ in SCL)
w("LEEME.md", f'''# Tanda 3B: hardware, diagnóstico, avisos, DataLog, recetas — SOLO importar y compilar

Estas instrucciones necesitan hardware real, tarjeta de memoria o avisos configurados: **no se ejecutan** (ningún OB las llama). La prueba es que **importen y compilen**.

1. Guardar. XML → Importar ficheros… → este .zip.
2. Pasar el resumen (con la 2.4.2 dice en qué bloque está cada error).
3. Después: XML → Deshacer última importación (no hace falta dejarlas en el proyecto).

FC 45001..{max(NUM.values())} (KOP) y fuentes `T8_S_*` (SCL), DB `T8_*_DB`, `T8_Receta`.

| Bloque | Instrucción |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print(len(LISTA), "KOP +", len(SCL), "SCL", z)
