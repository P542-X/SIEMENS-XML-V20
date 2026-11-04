# -*- coding: utf-8 -*-
"""Tanda 1 (complemento): las 24 cajas KOP aprendidas de la exportación real 'palclaude' (TIA V20)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda1b_complemento"))
MEM = m
D, R = '"T5_Datos"', '"T5_Resultado"'
VARS, RES, FCS = [], [], []
num = [42001]
def v(n, t, start=None, com=""):
    VARS.append((n, t, start, com)); return f"{D}.{n}"
def okb(n, d): RES.append((n, d)); return f"{R}.{n}"
def fc(nombre, desc, *redes):
    b = Bloque(f"T5_L_{nombre}", num[0], f"Tanda 1b KOP: {desc}"); num[0] += 1
    for t, rs in redes: b.add(t, *rs)
    b.sd(); FCS.append(b.nombre)
def raw(txt): return txt + ("\n" if not txt.endswith("\n") else "")

TOG = v("Tog", "Bool", None, "Cambia cada ciclo")
F = v("Falso", "Bool", "false")
I0, I1, I3, I4, I5, I9, I10, I20 = (v(f"Int_{k}", "Int", str(k)) for k in (0, 1, 3, 4, 5, 9, 10, 20))
I50, I100 = v("Int_50", "Int", "50"), v("Int_100", "Int", "100")
R2, R05, R16 = v("Real_2", "Real", "2.0"), v("Real_0_5", "Real", "0.5"), v("Real_16", "Real", "16.0")
U2, U3 = v("UInt_2", "UInt", "2"), v("UInt_3", "UInt", "3")
DW = v("DWord_00F0", "DWord", "16#000000F0")
AA, BB, CC, EE = (v(f"Arr_{x}", "Array[0..9] of Int") for x in "ABCE")

# --- Inc / Dec: un único parámetro InOut, posicional
x = v("Inc_Var", "Int")
fc("Inc", "Inc", ("Inc", [rung(C("True"), raw(f"            Inc{{\n                DestType := Int; \n                DisableENO := TRUE\n            }}( {x} )"))]),
   ("Comprobar", [rung(cmp("Gt", "Int", x, "0"), Q(okb("L_Inc", "Inc")))]))
x = v("Dec_Var", "Int")
fc("Dec", "Dec", ("Dec", [rung(C("True"), raw(f"            Dec{{\n                DestType := Int; \n                DisableENO := TRUE\n            }}( {x} )"))]),
   ("Comprobar", [rung(cmp("Lt", "Int", x, "0"), Q(okb("L_Dec", "Dec")))]))
# --- MIN / MAX / LIMIT
for nom, esperado in (("MIN", "3"), ("MAX", "5")):
    x = v(f"{nom}_Out", "Int")
    fc(nom, nom, (nom, [rung(C("True"), caja(nom, ["value_type := Int", "card := 2", "DisableENO := TRUE"], [f"IN1 := {I5}", f"IN2 := {I3}", f"OUT => {x}"]))]),
       ("Comprobar", [rung(cmp("Eq", "Int", x, esperado), Q(okb(f"L_{nom}", nom)))]))
x = v("LIMIT_Out", "Int")
fc("LIMIT", "LIMIT", ("LIMIT", [rung(C("True"), caja("LIMIT", ["value_type := Int", "DisableENO := TRUE"], [f"MN := {I0}", f"IN := {I5}", f"MX := {I4}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", x, "4"), Q(okb("L_LIMIT", "LIMIT")))]))
# --- Expt
x = v("Expt_Out", "Real")
fc("Expt", "Expt", ("Expt", [rung(C("True"), caja("Expt", ["DestType := Real", "SrcType := Real", "DisableENO := TRUE"], [f"IN1 := {R2}", f"IN2 := {R2}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Ge", "Real", x, "3.999"), cmp("Le", "Real", x, "4.001"), Q(okb("L_Expt", "Expt")))]))
# --- DECO / ENCO / SEL
x = v("DECO_Out", "DWord")
fc("DECO", "DECO", ("DECO", [rung(C("True"), caja("DECO", ["value_type := UInt", "return_type := DWord", "DisableENO := TRUE"], [f"IN := {U3}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Eq", "DWord", x, "16#00000008"), Q(okb("L_DECO", "DECO")))]))
x = v("ENCO_Out", "Int")
fc("ENCO", "ENCO", ("ENCO", [rung(C("True"), caja("ENCO", ["src_type := DWord", "DisableENO := TRUE"], [f"IN := {DW}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", x, "4"), Q(okb("L_ENCO", "ENCO")))]))
x = v("SEL_Out", "Int")
fc("SEL", "SEL", ("SEL", [rung(C("True"), caja("SEL", ["value_type := Int"], ["G := True", f"IN0 := {I3}", f"IN1 := {I5}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", x, "5"), Q(okb("L_SEL", "SEL")))]))
# --- InRange / OutRange / OK / NOK
fc("InRange", "IN_RANGE", ("InRange", [rung(caja("InRange", ["SrcType := Int"], [f"MIN := {I1}", f"IN := {I5}", f"MAX := {I9}"]), Q(okb("L_InRange", "IN_RANGE (InRange)")))]))
fc("OutRange", "OUT_RANGE", ("OutRange", [rung(caja("OutRange", ["SrcType := Int"], [f"MIN := {I10}", f"IN := {I5}", f"MAX := {I20}"]), Q(okb("L_OutRange", "OUT_RANGE (OutRange)")))]))
q1, q2 = v("OK_Q", "Bool"), v("NOK_Q", "Bool")
fc("OK_NOK", "OK y NOT_OK", ("OK", [rung(f"OK( {R16} )", Q(q1))]), ("NOK", [rung(f"NOK( {R16} )", Q(q2))]),
   ("Comprobar", [rung(C(q1), NC(q2), Q(okb("L_OK_NOK", "OK / NOT_OK (NOK)")))]))
# --- R_TRIG / F_TRIG con DB de instancia
for nom, db in (("R_TRIG", "T5_Rt_L"), ("F_TRIG", "T5_Ft_L")):
    q = v(f"{nom}_Q", "Bool")
    fc(nom, nom, (nom, [rung(raw(f'            "{db}".{nom}(\n                CLK := {TOG}, \n                Q => {q}\n            )'))]),
       ("Comprobar", [rung(C(q), S(okb(f"L_{nom}", f"{nom} (DB de instancia {db})")))]))
# --- RS (S_RS) y SET_BF / RESET_BF
m_, q = v("RS_Mem", "Bool"), v("RS_Q", "Bool")
fc("RS", "biestable RS", ("RS", [rung(C(F), raw(f"            S_RS(\n                Top := {m_}, \n                S1 := true\n            )"), Q(q))]),
   ("Comprobar", [rung(C(q), Q(okb("L_RS", "Biestable RS (S_RS)")))]))
bf = v("Bits_BF", "Array[0..7] of Bool")
fc("SET_BF", "SET_BF", ("SET_BF", [rung(C("True"), raw(f"            S_BitfieldCoil(\n                Top => {bf}[2], \n                N := 3\n            )"))]),
   ("Comprobar", [rung(C(f"{bf}[2]"), C(f"{bf}[4]"), Q(okb("L_SET_BF", "SET_BF (S_BitfieldCoil)")))]))
bf2 = v("Bits_RBF", "Array[0..7] of Bool")
fc("RESET_BF", "RESET_BF (nombre deducido: R_BitfieldCoil)", ("Preparar", [rung(C("True"), raw(f"            S_BitfieldCoil(\n                Top => {bf2}[0], \n                N := 4\n            )"))]),
   ("RESET_BF", [rung(C("True"), raw(f"            R_BitfieldCoil(\n                Top => {bf2}[1], \n                N := 2\n            )"))]),
   ("Comprobar", [rung(C(f"{bf2}[0]"), NC(f"{bf2}[1]"), NC(f"{bf2}[2]"), C(f"{bf2}[3]"), Q(okb("L_RESET_BF", "[DEDUCIDA] RESET_BF (R_BitfieldCoil)")))]))
# --- Bloques: MOVE_BLK / UMOVE_BLK / FILL_BLK / UFILL_BLK / MOVE_BLK_VARIANT
prep = ("Preparar", [rung(C("True"), move("7", f"{AA}[0]", f"{AA}[1]"))])
for nom, caja_, dst, desc in (("MOVE_BLK", "MoveBlockI", BB, "MOVE_BLK (MoveBlockI)"), ("UMOVE_BLK", "MoveBlockU", CC, "UMOVE_BLK (MoveBlockU)")):
    fc(nom, desc, prep, (nom, [rung(C("True"), caja(caja_, ["DisableENO := TRUE"], [f"IN := {AA}[0]", f"COUNT := {U2}", f"OUT => {dst}[0]"]))]),
       ("Comprobar", [rung(cmp("Eq", "Int", f"{dst}[1]", "7"), Q(okb(f"L_{nom}", desc)))]))
fl = v("Arr_F", "Array[0..9] of Int"); fu = v("Arr_U", "Array[0..9] of Int"); u10 = v("UInt_10", "UInt", "10")
fc("FILL_BLK", "FILL_BLK (FillBlockI)", ("FILL_BLK", [rung(C("True"), caja("FillBlockI", ["DisableENO := TRUE"], [f"IN := {I9}", f"COUNT := {u10}", f"OUT => {fl}[0]"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", f"{fl}[9]", "9"), Q(okb("L_FILL_BLK", "FILL_BLK (FillBlockI)")))]))
fc("UFILL_BLK", "UFILL_BLK (nombre deducido: FillBlockU)", ("UFILL_BLK", [rung(C("True"), caja("FillBlockU", ["DisableENO := TRUE"], [f"IN := {I9}", f"COUNT := {u10}", f"OUT => {fu}[0]"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", f"{fu}[9]", "9"), Q(okb("L_UFILL_BLK", "[DEDUCIDA] UFILL_BLK (FillBlockU)")))]))
rv, cnt, i0, i5 = v("MBV_Ret", "Int"), v("UDInt_2", "UDInt", "2"), v("DInt_0", "DInt", "0"), v("DInt_5", "DInt", "5")
fc("MOVE_BLK_VARIANT", "MOVE_BLK_VARIANT", prep, ("MOVE_BLK_VARIANT", [rung(C("True"), raw(f'''            MOVE_BLK_VARIANT(
                SRC := {AA}, 
                COUNT := {cnt}, 
                SRC_INDEX := {i0}, 
                DEST_INDEX := {i5}, 
                Ret_Val => {rv}, 
                DEST => {EE}
            )'''))]),
   ("Comprobar", [rung(cmp("Eq", "Int", f"{EE}[6]", "7"), cmp("Eq", "Int", rv, "0"), Q(okb("L_MOVE_BLK_VARIANT", "MOVE_BLK_VARIANT")))]))
# --- NORM_X / SCALE_X
x = v("NORM_Out", "Real")
fc("NORM_X", "NORM_X (Normalize)", ("NORM_X", [rung(C("True"), caja("Normalize", ["SrcType := Int", "DestType := Real", "DisableENO := TRUE"], [f"MIN := {I0}", f"VALUE := {I50}", f"MAX := {I100}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Ge", "Real", x, "0.499"), cmp("Le", "Real", x, "0.501"), Q(okb("L_NORM_X", "NORM_X (Normalize)")))]))
x = v("SCALE_Out", "Int")
fc("SCALE_X", "SCALE_X (Scale_X)", ("SCALE_X", [rung(C("True"), caja("Scale_X", ["SrcType := Real", "DestType := Int", "DisableENO := TRUE"], [f"MIN := {I0}", f"VALUE := {R05}", f"MAX := {I100}", f"OUT => {x}"]))]),
   ("Comprobar", [rung(cmp("Eq", "Int", x, "50"), Q(okb("L_SCALE_X", "SCALE_X (Scale_X)")))]))

# --- Base: Tog y Vida
b = Bloque("T5_Base", 42000, "Tanda 1b: Tog y Vida")
b.add("Tog", rung(C(TOG), "Not()", Q(TOG)))
b.add("Vida", rung(C("True"), caja("Add", ["Card := 2", "Type := AUTO", "DisableENO := TRUE"], [f"IN1 := {R}.Vida", "IN2 := 1", f"OUT => {R}.Vida"])))
b.sd()

# --- Fuentes: DB de instancia de R_TRIG / F_TRIG y recuento
w("FUENTES/T5_Rt_L.db", 'DATA_BLOCK "T5_Rt_L"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n"R_TRIG"\n\nBEGIN\n\nEND_DATA_BLOCK\n')
w("FUENTES/T5_Ft_L.db", 'DATA_BLOCK "T5_Ft_L"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n"F_TRIG"\n\nBEGIN\n\nEND_DATA_BLOCK\n')
lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T5_Contar.scl", f'''FUNCTION "T5_Contar" : Void
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
w("XML/T5_Datos.xml", gdb("T5_Datos", 42900, "".join(MEM(n, t, c, s) for n, t, s, c in VARS), "Tanda 1b: datos"))
w("XML/T5_Resultado.xml", gdb("T5_Resultado", 42901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo") +
                              MEM("OK", "Int", "Correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"), "Tanda 1b: un Bool por prueba"))
w("XML/T5_Ciclo.xml", ob("T5_Ciclo", 128, ["T5_Base"] + FCS + ["T5_Contar"], "Tanda 1b: llama a todas las pruebas"))
filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 1b: las cajas KOP aprendidas de la exportación real

{len(RES)} pruebas, una FC por caja, con la sintaxis SD exacta de la exportación `palclaude` (TIA V20). Dos nombres son deducidos por analogía y van marcados [DEDUCIDA]: `R_BitfieldCoil` (RESET_BF) y `FillBlockU` (UFILL_BLK).

Usa FC 42000..42024, DB 42900/42901, OB 128 y los DB de instancia `T5_Rt_L`, `T5_Ft_L`.

1. *XML → Importar ficheros…* → este .zip (sin descomprimir) → Sí a sobrescribir.
2. Compilar (0 errores), cargar en PLCSIM, RUN.
3. `T5_Resultado`: `Vida` sube, `OK` = `Total` ({len(RES)}), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print(len(RES), len(FCS), z)
