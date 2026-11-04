# -*- coding: utf-8 -*-
"""Tanda 2: fecha/hora y texto. Sintaxis SD de la exportación real Aprender_T2 (TIA V20)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda2_fecha_texto"))
MEM = m
D, R = '"T6_Datos"', '"T6_Resultado"'
VARS, RES, FCS, SCLT = [], [], [], []
num = [43001]
def v(n, t, start=None, com=""):
    VARS.append((n, t, start, com)); return f"{D}.{n}"
def okb(n, d): RES.append((n, d)); return f"{R}.{n}"
def fc(nombre, desc, *redes):
    b = Bloque(f"T6_L_{nombre}", num[0], f"Tanda 2 KOP: {desc}"); num[0] += 1
    for t, rs in redes: b.add(t, *rs)
    b.sd(); FCS.append(b.nombre)
def ok_s(n, d, codigo): SCLT.append((okb(f"S_{n}", "[SCL] " + d), codigo))
def bloque(nombre, plantillas, params): return caja(nombre, plantillas, params)
eqs = lambda t, a, b: cmp("Eq", t, a, b)

# Datos base (los DTL los pone T6_S_Base en SCL, con literal DTL#)
D1, D2, D3, DN = v("Dtl_1", "DTL"), v("Dtl_2", "DTL"), v("Dtl_3", "DTL"), v("Dtl_Ahora", "DTL")
T1500, T1, DI1 = v("T_1500ms", "Time", "T#1500MS"), v("T_Out", "Time"), v("DI_Out", "DInt")
DI2000, TH = v("DInt_2000", "DInt", "2000"), v("T_1h", "Time", "T#1H")
FECHA, TOD = v("Fecha", "Date", "D#2026-03-04"), v("Hora", "Time_Of_Day", "TOD#12:30:00")
RET, RET2 = v("Ret_Leer", "Int"), v("Ret_Escribir", "Int")
F = v("Falso", "Bool", "false")
ABC = v("S_ABCDEF", "String", "'ABCDEF'"); S_OUT = v("S_Out", "String")
I123 = v("Int_123", "Int", "123"); S456 = v("S_456", "String", "'456'")
L3, P2, L2, P1, P3 = v("Int_3", "Int", "3"), v("Int_2", "Int", "2"), v("Int_2b", "Int", "2"), v("Int_1", "Int", "1"), v("Int_3b", "Int", "3")
ABEF, CD, XY = v("S_ABEF", "String", "'ABEF'"), v("S_CD", "String", "'CD'"), v("S_XY", "String", "'XY'")
HEX = v("S_1A2B", "String", "'1A2B'"); N4, U2 = v("Int_4", "Int", "4"), v("UInt_2", "UInt", "2")
BYTES = v("Bytes", "Array[0..9] of Byte"); WRET = v("W_Ret", "Word")
CA, CB = v("Char_A", "Char", "'A'"), v("Char_A2", "Char", "'A'")

# ---- Fecha y hora (KOP)
fc("T_CONV_T_DI", "T_CONV Time -> DInt", ("T_CONV", [rung(C("True"), bloque("T_CONV", ["src_type := Time", "dest_type := DInt"], [f"IN := {T1500}", f"OUT => {DI1}"]))]),
   ("Comprobar", [rung(eqs("DInt", DI1, "1500"), Q(okb("L_T_CONV_T_DI", "T_CONV Time→DInt")))]))
x = v("T_desde_DInt", "Time")
fc("T_CONV_DI_T", "T_CONV DInt -> Time", ("T_CONV", [rung(C("True"), bloque("T_CONV", ["src_type := DInt", "dest_type := Time"], [f"IN := {DI2000}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(eqs("Time", x, "T#2S"), Q(okb("L_T_CONV_DI_T", "T_CONV DInt→Time")))]))
fc("T_ADD", "T_ADD DTL + Time", ("T_ADD", [rung(C("True"), bloque("T_ADD", ["date_type := DTL", "time_type := Time"], [f"IN1 := {D1}", f"IN2 := {TH}", f"OUT => {D2}"]))]),
   ("Comprobar", [rung(eqs("USInt", f"{D2}.HOUR", "11"), Q(okb("L_T_ADD", "T_ADD DTL + Time (10:00 + 1h)")))]))
fc("T_SUB", "T_SUB DTL - Time", ("T_SUB", [rung(C("True"), bloque("T_SUB", ["date_type := DTL", "time_type := Time"], [f"IN1 := {D1}", f"IN2 := {TH}", f"OUT => {D3}"]))]),
   ("Comprobar", [rung(eqs("USInt", f"{D3}.HOUR", "9"), Q(okb("L_T_SUB", "T_SUB DTL − Time (10:00 − 1h)")))]))
x = v("T_Diff", "Time")
fc("T_DIFF", "T_DIFF DTL", ("T_DIFF", [rung(C("True"), bloque("T_DIFF", ["date_type := DTL", "time_type := Time"], [f"IN1 := {D2}", f"IN2 := {D1}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(eqs("Time", x, "T#1H"), Q(okb("L_T_DIFF", "T_DIFF DTL (11:00 − 10:00)")))]))
x = v("D_Comb", "DTL")
fc("T_COMBINE", "T_COMBINE Date + TOD", ("T_COMBINE", [rung(C("True"), bloque("T_COMBINE", ["time_type := Time_Of_Day", "date_type := DTL"], [f"IN1 := {FECHA}", f"IN2 := {TOD}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(eqs("USInt", f"{x}.MONTH", "3"), eqs("USInt", f"{x}.HOUR", "12"), Q(okb("L_T_COMBINE", "T_COMBINE Date + Time_Of_Day")))]))
fc("RD_LOC_T", "RD_LOC_T", ("RD_LOC_T", [rung(C("True"), bloque("RD_LOC_T", ["date_type := DTL"], [f"RET_VAL => {RET}", f"OUT => {DN}"]))]),
   ("Comprobar", [rung(eqs("Int", RET, "0"), cmp("Gt", "UInt", f"{DN}.YEAR", "2000"), Q(okb("L_RD_LOC_T", "RD_LOC_T (hora local)")))]))
m1, r1 = v("WrLoc_Mem", "Bool"), v("Ret_WrLoc", "Int", "-1")
fc("WR_LOC_T", "WR_LOC_T (una vez, con la hora leída)", ("Escribir una vez", [rung(C("True"), f"PBox( {m1} )", bloque("WR_LOC_T", ["loctime_type := DTL"], [f"LOCTIME := {DN}", f"DST := {F}", f"Ret_Val => {r1}"]))]),
   ("Comprobar", [rung(eqs("Int", r1, "0"), S(okb("L_WR_LOC_T", "WR_LOC_T")))]))
m2, r2 = v("WrSys_Mem", "Bool"), v("Ret_WrSys", "Int", "-1"); DS = v("D_Sys", "DTL")
fc("WR_SYS_T", "WR_SYS_T (una vez, con la hora leída)", ("Leer", [rung(C("True"), bloque("RD_SYS_T", ["date_type := DTL"], [f"RET_VAL => {RET2}", f"OUT => {DS}"]))]),
   ("Escribir una vez", [rung(C("True"), f"PBox( {m2} )", bloque("WR_SYS_T", ["date_type := DTL"], [f"IN := {DS}", f"RET_VAL => {r2}"]))]),
   ("Comprobar", [rung(eqs("Int", r2, "0"), S(okb("L_WR_SYS_T", "WR_SYS_T")))]))
D1b = v("Dtl_1_Copia", "DTL")
fc("Eq_DTL", "comparar dos DTL", ("Eq DTL", [rung(eqs("DTL", D1, D1b), Q(okb("L_Eq_DTL", "Eq con SrcType DTL")))]))

# ---- Texto (KOP)
x = v("S_desde_Int", "String"); y = v("Int_vuelta", "Int")
# Int -> String deja delante el signo (espacio si es positivo): se comprueba con la vuelta String -> Int
fc("S_CONV_I_S", "S_CONV Int -> String (y vuelta)", ("S_CONV", [rung(C("True"), bloque("S_CONV", ["src_type := Int", "dest_type := String", "DisableENO := TRUE"], [f"IN := {I123}", f"OUT => {x}"]))]),
   ("Vuelta", [rung(C("True"), bloque("S_CONV", ["src_type := String", "dest_type := Int", "DisableENO := TRUE"], [f"IN := {x}", f"OUT => {y}"]))]),
   ("Comprobar", [rung(eqs("Int", y, "123"), Q(okb("L_S_CONV_I_S", "S_CONV Int→String (comprobado con la vuelta)")))]))
x = v("I_desde_S", "Int")
fc("S_CONV_S_I", "S_CONV String -> Int", ("S_CONV", [rung(C("True"), bloque("S_CONV", ["src_type := String", "dest_type := Int", "DisableENO := TRUE"], [f"IN := {S456}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(eqs("Int", x, "456"), Q(okb("L_S_CONV_S_I", "S_CONV String→Int")))]))
def txt(nombre, plantilla, params, esperado, tipo_res="String", desc=None):
    out = v(f"{nombre}_Out", tipo_res)
    fc(nombre, desc or nombre, (nombre, [rung(C("True"), bloque(nombre, [plantilla, "DisableENO := TRUE"], params + [f"OUT => {out}"]))]),
       ("Comprobar", [rung(eqs(tipo_res, out, esperado), Q(okb(f"L_{nombre}", desc or nombre)))]))
txt("LEFT", "str_type := String", [f"IN := {ABC}", f"L := {L3}"], "'ABC'")
txt("RIGHT", "str_type := String", [f"IN := {ABC}", f"L := {L3}"], "'DEF'")
txt("MID", "value_type := String", [f"IN := {ABC}", f"L := {L2}", f"P := {P2}"], "'BC'")
txt("DELETE", "str_type := String", [f"IN := {ABC}", f"L := {L2}", f"P := {P1}"], "'CDEF'")
txt("INSERT", "str_type := String", [f"IN1 := {ABEF}", f"IN2 := {CD}", f"P := {P2}"], "'ABCDEF'")
txt("REPLACE", "str_type := String", [f"IN1 := {ABC}", f"IN2 := {XY}", f"L := {L2}", f"P := {P3}"], "'ABXYEF'")
txt("FIND", "str_type := String", [f"IN1 := {ABC}", f"IN2 := {CD}"], "3", "Int")
fc("ATH", "ATH (texto hexadecimal a bytes)", ("ATH", [rung(C("True"), bloque("ATH", ["n_type := Int", "in_out_type := Variant"], [f"IN := {HEX}", f"N := {N4}", f"RET_VAL => {WRET}", f"OUT => {BYTES}"]))]),
   ("Comprobar", [rung(eqs("Byte", f"{BYTES}[0]", "16#1A"), eqs("Byte", f"{BYTES}[1]", "16#2B"), Q(okb("L_ATH", "ATH '1A2B' → 16#1A, 16#2B")))]))
x = v("HTA_Out", "String"); w2 = v("W_Ret2", "Word"); B2 = v("Bytes_HTA", "Array[0..9] of Byte")
fc("HTA", "HTA (bytes a texto hexadecimal)", ("Preparar", [rung(C("True"), move("16#1A", f"{B2}[0]")), rung(C("True"), move("16#2B", f"{B2}[1]"))]),
   ("HTA", [rung(C("True"), bloque("HTA", ["n_type := UInt", "in_out_type := Variant", "card := 1"], [f"IN := {B2}", f"N := {U2}", f"RET_VAL => {w2}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(eqs("String", x, "'1A2B'"), Q(okb("L_HTA", "HTA 16#1A, 16#2B → '1A2B'")))]))
fc("Eq_Char", "comparar dos Char", ("Eq Char", [rung(eqs("Char", CA, CB), Q(okb("L_Eq_Char", "Eq con SrcType Char")))]))

# ---- SCL
ok_s("Fecha", "TIME_TO_DINT, DINT_TO_TIME, T_ADD, T_SUB, T_DIFF, CONCAT_DATE_TOD (T_COMBINE en KOP)",
     f'''#di := TIME_TO_DINT({T1500});
	#t := DINT_TO_TIME(2000);
	#dtA := T_ADD(IN1 := {D1}, IN2 := T#1H);
	#dtB := T_SUB(IN1 := {D1}, IN2 := T#1H);
	#t2 := T_DIFF(IN1 := #dtA, IN2 := {D1});
	#dtC := CONCAT_DATE_TOD(IN1 := {FECHA}, IN2 := {TOD});
	{R}.S_Fecha := #di = 1500 AND #t = T#2S AND #dtA.HOUR = 11 AND #dtB.HOUR = 9 AND #t2 = T#1H AND #dtC.MONTH = 3 AND #dtC.HOUR = 12;''')
ok_s("Reloj", "RD_LOC_T, RD_SYS_T, WR_LOC_T y WR_SYS_T (una vez)",
     f'''#r := RD_LOC_T(OUT => #dtA);
	#r2 := RD_SYS_T(OUT => #dtB);
	IF NOT {D}.Escrito_S THEN
	    #r3 := WR_LOC_T(LOCTIME := #dtA, DST := FALSE);
	    #r4 := WR_SYS_T(IN := #dtB);
	    {D}.Escrito_S := TRUE;
	    {R}.S_Reloj := #r = 0 AND #r2 = 0 AND #r3 = 0 AND #r4 = 0 AND #dtA.YEAR > 2000;
	END_IF;''')
v("Escrito_S", "Bool")
ok_s("Texto", "INT_TO_STRING, STRING_TO_INT, LEFT, RIGHT, MID, DELETE, INSERT, REPLACE, FIND",
     f'''#s := INT_TO_STRING(123);
	#i := STRING_TO_INT('456');
	#ok := STRING_TO_INT(#s) = 123 AND #i = 456;   // INT_TO_STRING deja el signo delante (' 123')
	#ok := #ok AND LEFT(IN := {ABC}, L := 3) = 'ABC' AND RIGHT(IN := {ABC}, L := 3) = 'DEF';
	#ok := #ok AND MID(IN := {ABC}, L := 2, P := 2) = 'BC' AND DELETE(IN := {ABC}, L := 2, P := 1) = 'CDEF';
	#ok := #ok AND INSERT(IN1 := 'ABEF', IN2 := 'CD', P := 2) = 'ABCDEF' AND REPLACE(IN1 := {ABC}, IN2 := 'XY', L := 2, P := 3) = 'ABXYEF';
	#ok := #ok AND FIND(IN1 := {ABC}, IN2 := 'CD') = 3;
	{R}.S_Texto := #ok;''')
ok_s("Hex", "ATH y HTA",
     f'''#w := ATH(IN := {HEX}, N := 4, OUT => #b);
	#w2 := HTA(IN := #b, N := 2, OUT => #s);
	{R}.S_Hex := #b[0] = 16#1A AND #b[1] = 16#2B AND #s = '1A2B';''')
ok_s("Comparar", "comparar DTL y Char", f'''{R}.S_Comparar := {D1} = {D1b} AND {CA} = {CB};''')

# ---- Base: DTL de partida y Vida
w("FUENTES/T6_S_Base.scl", f'''FUNCTION "T6_S_Base" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
BEGIN
	// DTL de partida (literal DTL# en SCL)
	{D1} := DTL#2026-01-01-10:00:00;
	{D1b} := DTL#2026-01-01-10:00:00;
	{R}.Vida := {R}.Vida + 1;
END_FUNCTION
''')
cuerpo = "\n\n".join(f"\t// {d}\n\t{c}" for (n, c), (_, d) in zip(SCLT, [r for r in RES if r[0].startswith("S_")]))
w("FUENTES/T6_S_Pruebas.scl", f'''FUNCTION "T6_S_Pruebas" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_TEMP
      di : DInt;
      t : Time;
      t2 : Time;
      dtA : DTL;
      dtB : DTL;
      dtC : DTL;
      r : Int;
      r2 : Int;
      r3 : Int;
      r4 : Int;
      s : String;
      i : Int;
      ok : Bool;
      w : Word;
      w2 : Word;
      b : Array[0..9] of Byte;
   END_VAR

BEGIN
{cuerpo}
END_FUNCTION
''')
lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T6_Contar.scl", f'''FUNCTION "T6_Contar" : Void
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
w("XML/T6_Datos.xml", gdb("T6_Datos", 43900, "".join(MEM(n, t, c, s) for n, t, s, c in VARS), "Tanda 2: datos"))
w("XML/T6_Resultado.xml", gdb("T6_Resultado", 43901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo") +
                              MEM("OK", "Int", "Correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"), "Tanda 2: un Bool por prueba"))
w("XML/T6_Ciclo.xml", ob("T6_Ciclo", 129, ["T6_S_Base"] + FCS + ["T6_S_Pruebas", "T6_Contar"], "Tanda 2: llama a todas las pruebas"))
filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 2: fecha/hora y texto (TIA V20)

{len(RES)} pruebas ({len(FCS)} FC KOP con la sintaxis SD de la exportación real `Aprender_T2` + {len(SCLT)} grupos SCL). FC 43001..{num[0]-1}, DB 43900/43901, OB 129.

**Ojo:** `WR_LOC_T` y `WR_SYS_T` escriben la hora del PLC (una sola vez, con la misma hora que acaban de leer). Hacerlo solo en el PLC de pruebas / PLCSIM.

1. XML → Importar ficheros… → este .zip → Sí a sobrescribir.
2. Compilar (0 errores), cargar en PLCSIM, RUN.
3. `T6_Resultado`: `Vida` sube, `OK` = `Total` ({len(RES)}), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print(len(RES), len(FCS), z)
