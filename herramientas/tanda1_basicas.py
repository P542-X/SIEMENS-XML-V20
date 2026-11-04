# -*- coding: utf-8 -*-
"""Tanda 1: instrucciones básicas de TIA V20. Una FC KOP (SD) por instrucción + SCL por familia.
Cada prueba pone a TRUE un Bool en T4_Resultado si funciona en la CPU."""
import os, shutil, zipfile
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda1_basicas"))

MEM = m   # el generador usa "m" como variable local más abajo
D, I, R = '"T4_Datos"', '"T4_Inst"', '"T4_Resultado"'
VARS, INST, RES, LADFC, SCL = [], [], [], [], {}
num = [41001]
def v(n, t, start=None, com=""):
    VARS.append((n, t, start, com)); return f"{D}.{n}"
def inst(n, t):
    if t not in ("R_TRIG", "F_TRIG"): INST.append((n, t))
    return f"{I}.{n}"
def okb(n, d): RES.append((n, d)); return f"{R}.{n}"
TOG = v("Tog", "Bool", None, "Cambia en cada ciclo (lo escribe T4_Base)")
F = v("Falso", "Bool", "false", "Siempre FALSE")
I5, I3 = v("Int_5", "Int", "5", "Constante 5"), v("Int_3", "Int", "3", "Constante 3")
R16, R2 = v("Real_16", "Real", "16.0", "Constante 16.0"), v("Real_2", "Real", "2.0", "Constante 2.0")
W0F = v("Word_00F0", "Word", "16#00F0", "Constante 16#00F0")
W33 = v("Word_0033", "Word", "16#0033", "Constante 16#0033")
v("Mem_Runtime", "LReal")

SKIP = {"Dec", "Inc", "Deco", "Enco", "F_TRIG", "R_TRIG", "IN_Range", "OUT_Range", "OK", "Limit", "Max", "Min", "Sel",
        "RS", "SET_BF", "CountOfElements", "Expt", "MOVE_BLK", "UMOVE_BLK", "MOVE_BLK_VARIANT", "FILL_BLK", "NORM_X", "SCALE_X"}
def T(t): return [f"SrcType := {t}", "DisableENO := TRUE"]   # tipo explícito: SrcType (Type := Real deja el tipo en Undef)
def lad(nombre, desc, *redes):
    """FC KOP en SD para una instrucción. redes: (titulo, [rungs])"""
    if nombre in SKIP: return
    n = f"T4_L_{nombre}"
    b = Bloque(n, num[0], f"Tanda 1 KOP: {desc}"); num[0] += 1
    for t, rs in redes: b.add(t, *rs)
    b.sd(); LADFC.append(n)
def scl(familia, nombre, desc, codigo):
    SCL.setdefault(familia, []).append((okb(f"S_{nombre}", "[SCL] " + desc), codigo))
def entre(var, lo, hi, tipo="Real"):   # dos comparaciones en serie: lo <= var <= hi
    return [cmp("Ge", tipo, var, lo), cmp("Le", tipo, var, hi)]

# ===================================================== 1. Lógica de bits
q = v("ICoil_Q", "Bool")
lad("I_Coil", "bobina negada", ("Bobina negada", [rung(C(F), f"I_Coil( {q} )")]),
    ("Comprobar", [rung(C(q), Q(okb("L_I_Coil", "Bobina negada -( / )-")))]))
m, qq = v("SR_Mem", "Bool"), v("SR_Q", "Bool")
lad("SR", "biestable SR (S_SR)", ("SR", [rung(C("True"), f"S_SR(\n                Top := {m}, \n                R1 := {F}\n            )\n", Q(qq))]),
    ("Comprobar", [rung(C(qq), Q(okb("L_SR", "Biestable SR (S_SR)")))]))
m, qq = v("RS_Mem", "Bool"), v("RS_Q", "Bool")
lad("RS", "biestable RS (R_RS)", ("RS", [rung(C(F), f"R_RS(\n                Top := {m}, \n                S1 := True\n            )\n", Q(qq))]),
    ("Comprobar", [rung(C(qq), Q(okb("L_RS", "Biestable RS (R_RS)")))]))
m = v("PCont_Mem", "Bool")
lad("P_Contact", "contacto de flanco positivo", ("Flanco", [rung(f"P_Contact(\n                Top := {TOG}, \n                Bottom := {m}\n            )\n", S(okb("L_P_Contact", "Contacto flanco positivo -|P|-")))]))
m = v("NCont_Mem", "Bool")
lad("N_Contact", "contacto de flanco negativo", ("Flanco", [rung(f"N_Contact(\n                Top := {TOG}, \n                Bottom := {m}\n            )\n", S(okb("L_N_Contact", "Contacto flanco negativo -|N|-")))]))
m = v("NBox_Mem", "Bool")
lad("NBox", "flanco negativo (N_TRIG)", ("Flanco", [rung(C(TOG), f"NBox( {m} )", S(okb("L_NBox", "Flanco negativo N_TRIG")))]))
m, qq = v("PCoil_Mem", "Bool"), v("PCoil_Q", "Bool")
lad("P_Coil", "bobina de flanco positivo", ("Flanco", [rung(C(TOG), f"P_Coil(\n                Top := {qq}, \n                Bottom := {m}\n            )\n")]),
    ("Comprobar", [rung(C(qq), S(okb("L_P_Coil", "Bobina flanco positivo -(P)-")))]))
lad("R_TRIG", "R_TRIG (instancia en DB)", ("R_TRIG", [rung(C(TOG), f"{inst('Rt_L', 'R_TRIG')}.R_TRIG()", S(okb("L_R_TRIG", "R_TRIG")))]))
lad("F_TRIG", "F_TRIG (instancia en DB)", ("F_TRIG", [rung(C(TOG), f"{inst('Ft_L', 'F_TRIG')}.F_TRIG()", S(okb("L_F_TRIG", "F_TRIG")))]))
bf = v("Bits_BF", "Array[0..7] of Bool")
lad("SET_BF", "SET_BF y RESET_BF", ("SET_BF", [rung(C("True"), f"SET_BF(\n                Top := {bf}[2], \n                Bottom := 3\n            )\n")]),
    ("RESET_BF", [rung(C("True"), f"RESET_BF(\n                Top := {bf}[3], \n                Bottom := 1\n            )\n")]),
    ("Comprobar", [rung(C(f"{bf}[2]"), NC(f"{bf}[3]"), C(f"{bf}[4]"), Q(okb("L_SET_BF", "SET_BF / RESET_BF")))]))
scl("Bits", "R_TRIG", "R_TRIG", f'"T4_Rt_S"(CLK := {TOG});\n\tIF "T4_Rt_S".Q THEN {R}.S_R_TRIG := TRUE; END_IF;')
scl("Bits", "F_TRIG", "F_TRIG", f'"T4_Ft_S"(CLK := {TOG});\n\tIF "T4_Ft_S".Q THEN {R}.S_F_TRIG := TRUE; END_IF;')

# ===================================================== 2. Temporizadores
def tmr(tipo, extra=""):
    t = inst(f"{tipo}_L", "IEC_TIMER")
    return f"{t}.{tipo}{{ time_type := Time }}(\n                {extra}PT := T#100MS, \n                ET =>  \n            )\n"
lad("TP", "impulso TP", ("TP", [rung(C("True"), tmr("TP"), S(okb("L_TP", "Temporizador TP")))]))
lad("TOF", "retardo a la desconexión TOF", ("TOF", [rung(C("True"), tmr("TOF"), S(okb("L_TOF", "Temporizador TOF")))]))
lad("TONR", "retardo a la conexión con memoria TONR", ("TONR", [rung(C("True"), tmr("TONR", f"R := {F}, \n                "), Q(okb("L_TONR", "Temporizador TONR")))]))
for tipo in ("TP", "TON", "TOF", "TONR"):
    t = inst(f"{tipo}_S", "IEC_TIMER")
    r_ = f"R := FALSE, " if tipo == "TONR" else ""
    scl("Tiempo", tipo, f"{tipo}", f"{t}.{tipo}(IN := TRUE, {r_}PT := T#100MS);\n\tIF {t}.Q THEN {R}.S_{tipo} := TRUE; END_IF;")

# ===================================================== 3. Contadores
def cnt(tipo, params):
    c = inst(f"{tipo}_L", "IEC_COUNTER")
    return f"{c}.{tipo}{{ value_type := Int }}(\n" + ", \n".join("                " + p for p in params) + "\n            )\n"
lad("CTU", "contador ascendente CTU", ("CTU", [rung(C(TOG), cnt("CTU", [f"R := {F}", "PV := 3", "CV =>  "]), S(okb("L_CTU", "Contador CTU")))]))
lad("CTD", "contador descendente CTD", ("CTD", [rung(C(TOG), cnt("CTD", [f"LD := {F}", "PV := 3", "CV =>  "]), S(okb("L_CTD", "Contador CTD")))]))
lad("CTUD", "contador CTUD", ("CTUD", [rung(C(TOG), cnt("CTUD", [f"CD := {F}", f"R := {F}", f"LD := {F}", "PV := 2", "QD =>  ", "CV =>  "]), S(okb("L_CTUD", "Contador CTUD")))]))
for tipo, prm, q_ in (("CTU", f"CU := {TOG}, R := FALSE, PV := 3", "QU"), ("CTD", f"CD := {TOG}, LD := FALSE, PV := 3", "QD"),
                      ("CTUD", f"CU := {TOG}, CD := FALSE, R := FALSE, LD := FALSE, PV := 2", "QU")):
    c = inst(f"{tipo}_S", "IEC_COUNTER")
    scl("Contadores", tipo, tipo, f"{c}.{tipo}({prm});\n\tIF {c}.{q_} THEN {R}.S_{tipo} := TRUE; END_IF;")

# ===================================================== 4. Comparación
lad("IN_Range", "valor dentro de rango", ("IN_Range", [rung(caja("IN_Range", ["value_type := Int"], ["MIN := 1", f"VAL := {I5}", "MAX := 9"]), Q(okb("L_IN_Range", "IN_RANGE")))]))
lad("OUT_Range", "valor fuera de rango", ("OUT_Range", [rung(caja("OUT_Range", ["value_type := Int"], ["MIN := 10", f"VAL := {I5}", "MAX := 20"]), Q(okb("L_OUT_Range", "OUT_RANGE")))]))
lad("OK", "OK y NOT_OK (validez de Real)", ("OK", [rung(f"OK( {R16} )", Q(v("OK_Q", "Bool")))]),
    ("NOT_OK", [rung(f"NOT_OK( {R16} )", Q(v("NOTOK_Q", "Bool")))]),
    ("Comprobar", [rung(C(f"{D}.OK_Q"), NC(f"{D}.NOTOK_Q"), Q(okb("L_OK", "OK / NOT_OK")))]))

# ===================================================== 5. Matemáticas
def mate(nombre, desc, caja_txt, out, lo, hi, tipo="Real", sclcode=None):
    lad(nombre, desc, (nombre, [rung(C("True"), caja_txt)]), ("Comprobar", [rung(*entre(out, lo, hi, tipo), Q(okb(f"L_{nombre}", desc)))]))
    if sclcode: scl("Matematicas", nombre, desc, sclcode)
AT = ["Type := AUTO", "DisableENO := TRUE"]
o = v("Mod_Out", "Int"); mate("Mod", "Mod (resto)", caja("Mod", T("Int"), [f"IN1 := {I5}", "IN2 := 3", f"OUT => {o}"]), o, "2", "2", "Int",
     f"{R}.S_Mod := ({I5} MOD 3) = 2;")
o = v("Neg_Out", "Int"); mate("Neg", "Neg (cambio de signo)", caja("Neg", T("Int"), [f"IN := {I5}", f"OUT => {o}"]), o, "-5", "-5", "Int", f"#i := -{I5};\n\t{R}.S_Neg := #i = -5;")
o = v("Inc_Var", "Int"); mate("Inc", "Inc (incrementar)", caja("Inc", AT, [f"IN_OUT := {o}"]), o, "1", "32767", "Int")
o = v("Dec_Var", "Int"); mate("Dec", "Dec (decrementar)", caja("Dec", AT, [f"IN_OUT := {o}"]), o, "-32768", "-1", "Int")
o = v("Abs_Out", "Int"); n5 = v("Int_m5", "Int", "-5", "Constante -5")
mate("Abs", "Abs (valor absoluto)", caja("Abs", T("Int"), [f"IN := {n5}", f"OUT => {o}"]), o, "5", "5", "Int", f"{R}.S_Abs := ABS({n5}) = 5;")
o = v("Min_Out", "Int"); mate("Min", "Min", caja("Min", ["Card := 2"] + AT, [f"IN1 := {I5}", f"IN2 := {I3}", f"OUT => {o}"]), o, "3", "3", "Int",
     f"#i := MIN(IN1 := {I5}, IN2 := {I3});\n\t{R}.S_Min := #i = 3;")
o = v("Max_Out", "Int"); mate("Max", "Max", caja("Max", ["Card := 2"] + AT, [f"IN1 := {I5}", f"IN2 := {I3}", f"OUT => {o}"]), o, "5", "5", "Int",
     f"#i := MAX(IN1 := {I5}, IN2 := {I3});\n\t{R}.S_Max := #i = 5;")
o = v("Limit_Out", "Int"); mate("Limit", "Limit", caja("Limit", AT, ["MN := 0", f"IN := {I5}", "MX := 4", f"OUT => {o}"]), o, "4", "4", "Int",
     f"#i := LIMIT(MN := INT#0, IN := {I5}, MX := INT#4);\n\t{R}.S_Limit := #i = 4;")
for fn, entrada, lo, hi, s_expr in [("Sqr", R2, "3.999", "4.001", f"SQR({R2})"), ("Sqrt", R16, "3.999", "4.001", f"SQRT({R16})"),
                                    ("Ln", R16, "2.772", "2.773", f"LN({R16})"), ("Exp", R2, "7.388", "7.390", f"EXP({R2})"),
                                    ("Sin", R2, "0.909", "0.910", f"SIN({R2})"), ("Cos", R2, "-0.417", "-0.416", f"COS({R2})"),
                                    ("Tan", R2, "-2.186", "-2.184", f"TAN({R2})"), ("Frac", v("Real_2_75", "Real", "2.75", "Constante 2.75"), "0.749", "0.751", f"FRAC({D}.Real_2_75)")]:
    o = v(f"{fn}_Out", "Real")
    mate(fn, fn, caja(fn, T("Real"), [f"IN := {entrada}", f"OUT => {o}"]), o, lo, hi, "Real", f"{R}.S_{fn} := ABS({s_expr} - ({lo} + {hi}) / 2.0) < 0.01;")
rh = v("Real_0_5", "Real", "0.5", "Constante 0.5")
for fn, lo, hi, s_expr in [("Asin", "0.523", "0.524", f"ASIN({rh})"), ("Acos", "1.047", "1.048", f"ACOS({rh})"), ("Atan", "0.463", "0.464", f"ATAN({rh})")]:
    o = v(f"{fn}_Out", "Real")
    mate(fn, fn, caja(fn, T("Real"), [f"IN := {rh}", f"OUT => {o}"]), o, lo, hi, "Real", f"{R}.S_{fn} := ABS({s_expr} - ({lo} + {hi}) / 2.0) < 0.01;")
o = v("Expt_Out", "Real")
mate("Expt", "Expt (potencia)", caja("Expt", T("Real"), [f"IN1 := {R2}", f"IN2 := {R2}", f"OUT => {o}"]), o, "3.999", "4.001", "Real",
     f"{R}.S_Expt := ABS({R2} ** {R2} - 4.0) < 0.01;")
scl("Matematicas", "Inc_Dec", "INC / DEC no existen en SCL: x := x + 1", f"#i := {I5} + 1 - 1;\n\t{R}.S_Inc_Dec := #i = 5;")

# ===================================================== 6. Mover
a1, a2 = v("Arr_A", "Array[0..9] of Int"), v("Arr_B", "Array[0..9] of Int")
lad("MOVE_BLK", "MOVE_BLK", ("Preparar", [rung(C("True"), move("7", f"{a1}[0]", f"{a1}[1]"))]),
    ("MOVE_BLK", [rung(C("True"), caja("MOVE_BLK", ["DisableENO := TRUE"], [f"IN := {a1}[0]", "COUNT := 2", f"OUT => {a2}[0]"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", f"{a2}[1]", "7"), Q(okb("L_MOVE_BLK", "MOVE_BLK")))]))
a3 = v("Arr_C", "Array[0..9] of Int")
lad("FILL_BLK", "FILL_BLK", ("FILL_BLK", [rung(C("True"), caja("FILL_BLK", ["DisableENO := TRUE"], ["IN := 9", "COUNT := 10", f"OUT => {a3}[0]"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", f"{a3}[9]", "9"), Q(okb("L_FILL_BLK", "FILL_BLK")))]))
a4 = v("Arr_D", "Array[0..9] of Int")
lad("UMOVE_BLK", "UMOVE_BLK", ("UMOVE_BLK", [rung(C("True"), caja("UMOVE_BLK", ["DisableENO := TRUE"], [f"IN := {a1}[0]", "COUNT := 2", f"OUT => {a4}[0]"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", f"{a4}[0]", "7"), Q(okb("L_UMOVE_BLK", "UMOVE_BLK")))]))
a5, rv = v("Arr_E", "Array[0..9] of Int"), v("MBV_Ret", "Int")
lad("MOVE_BLK_VARIANT", "MOVE_BLK_VARIANT", ("MOVE_BLK_VARIANT", [rung(C("True"), caja("MOVE_BLK_VARIANT", ["DisableENO := TRUE"],
    [f"SRC := {a1}", "COUNT := 2", "SRC_INDEX := 0", "DEST_INDEX := 5", f"Ret_Val => {rv}", f"DEST => {a5}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", f"{a5}[6]", "7"), cmp("Eq", "Int", rv, "0"), Q(okb("L_MOVE_BLK_VARIANT", "MOVE_BLK_VARIANT")))]))
ce = v("COE_Out", "UDInt")
lad("CountOfElements", "CountOfElements", ("CountOfElements", [rung(C("True"), caja("CountOfElements", ["DisableENO := TRUE"], [f"IN := {a1}", f"Ret_Val => {ce}"]))]),
    ("Comprobar", [rung(cmp("Eq", "UDInt", ce, "10"), Q(okb("L_CountOfElements", "CountOfElements")))]))
scl("Mover", "MOVE_BLK", "MOVE_BLK", f"{a1}[0] := 7; {a1}[1] := 7;\n\tMOVE_BLK(IN := {a1}[0], COUNT := 2, OUT => {D}.Arr_S1[0]);\n\t{R}.S_MOVE_BLK := {D}.Arr_S1[1] = 7;")
v("Arr_S1", "Array[0..9] of Int"); v("Arr_S2", "Array[0..9] of Int"); v("Arr_S3", "Array[0..9] of Int")
scl("Mover", "FILL_BLK", "FILL_BLK", f"FILL_BLK(IN := 9, COUNT := 10, OUT => {D}.Arr_S2[0]);\n\t{R}.S_FILL_BLK := {D}.Arr_S2[9] = 9;")
scl("Mover", "MOVE_BLK_VARIANT", "MOVE_BLK_VARIANT", f"#r := MOVE_BLK_VARIANT(SRC := {a1}, COUNT := 2, SRC_INDEX := 0, DEST_INDEX := 5, DEST => {D}.Arr_S3);\n\t{R}.S_MOVE_BLK_VARIANT := #r = 0 AND {D}.Arr_S3[6] = 7;")
scl("Mover", "CountOfElements", "CountOfElements (dentro de una FC con parámetro Variant)", f'{R}.S_CountOfElements := "T4_S_Coe"(Arr := {a1}) = 10;')
scl("Mover", "SWAP", "SWAP", f"#w := SWAP(WORD#16#1234);\n\t{R}.S_SWAP := #w = WORD#16#3412;")

# ===================================================== 7. Conversión
for fn in ("Round", "Ceil", "Floor", "Trunc"):
    o = v(f"{fn}_Out", "DInt"); entrada = v(f"{fn}_In", "Real", "2.5", "Constante 2.5")
    esperado = {"Round": "2", "Ceil": "3", "Floor": "2", "Trunc": "2"}[fn]
    lad(fn, fn, (fn, [rung(C("True"), caja(fn, ["SrcType := Real", "DestType := DInt", "DisableENO := TRUE"], [f"IN := {entrada}", f"OUT => {o}"]))]),
        ("Comprobar", [rung(cmp("Eq", "DInt", o, esperado), Q(okb(f"L_{fn}", fn)))]))
    scl("Conversion", fn, fn, f"#d := {fn.upper()}({entrada});\n\t{R}.S_{fn} := #d = {esperado};")
o = v("ScaleX_Out", "Int")
lad("SCALE_X", "SCALE_X", ("SCALE_X", [rung(C("True"), caja("SCALE_X", ["value_type := Real", "out_type := Int", "DisableENO := TRUE"],
    ["MIN := 0", "VALUE := 0.5", "MAX := 100", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", o, "50"), Q(okb("L_SCALE_X", "SCALE_X")))]))
o = v("NormX_Out", "Real")
lad("NORM_X", "NORM_X", ("NORM_X", [rung(C("True"), caja("NORM_X", ["value_type := Int", "out_type := Real", "DisableENO := TRUE"],
    ["MIN := 0", "VALUE := 50", "MAX := 100", f"OUT => {o}"]))]),
    ("Comprobar", [rung(*entre(o, "0.499", "0.501"), Q(okb("L_NORM_X", "NORM_X")))]))
scl("Conversion", "SCALE_X", "SCALE_X", f"#i := SCALE_X(MIN := INT#0, VALUE := REAL#0.5, MAX := INT#100);\n\t{R}.S_SCALE_X := #i = 50;")
scl("Conversion", "NORM_X", "NORM_X", f"#re := NORM_X(MIN := INT#0, VALUE := INT#50, MAX := INT#100);\n\t{R}.S_NORM_X := ABS(#re - 0.5) < 0.001;")

# ===================================================== 8. Lógica de palabras
for fn, card, esperado in (("And", True, "16#0030"), ("Or", True, "16#00F3"), ("Xor", True, "16#00C3")):
    o = v(f"{fn}_Out", "Word")
    lad(fn, fn.upper(), (fn, [rung(C("True"), caja(fn, ["Card := 2"] + T("Word"), [f"IN1 := {W0F}", f"IN2 := {W33}", f"OUT => {o}"]))]),
        ("Comprobar", [rung(cmp("Eq", "Word", o, esperado), Q(okb(f"L_{fn}", fn.upper())))]))
    op = {"And": "AND", "Or": "OR", "Xor": "XOR"}[fn]
    scl("Palabras", fn, fn.upper(), f"{R}.S_{fn} := ({W0F} {op} {W33}) = WORD#{esperado};")
o = v("Inv_Out", "Word")
lad("Inv", "INV (complemento)", ("Inv", [rung(C("True"), caja("Inv", T("Word"), [f"IN := {W0F}", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Word", o, "16#FF0F"), Q(okb("L_Inv", "INV")))]))
scl("Palabras", "Inv", "NOT de Word", f"{R}.S_Inv := NOT {W0F} = WORD#16#FF0F;")
o = v("Deco_Out", "DWord")
lad("Deco", "DECO", ("Deco", [rung(C("True"), caja("Deco", ["Type := AUTO", "DisableENO := TRUE"], ["IN := 3", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "DWord", o, "16#00000008"), Q(okb("L_Deco", "DECO")))]))
o = v("Enco_Out", "Int")
lad("Enco", "ENCO", ("Enco", [rung(C("True"), caja("Enco", ["Type := AUTO", "DisableENO := TRUE"], [f"IN := {W0F}", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", o, "4"), Q(okb("L_Enco", "ENCO")))]))
o = v("Sel_Out", "Int")
lad("Sel", "SEL", ("Sel", [rung(C("True"), caja("Sel", AT, ["G := True", f"IN0 := {I3}", f"IN1 := {I5}", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", o, "5"), Q(okb("L_Sel", "SEL")))]))
o = v("Mux_Out", "Int")
lad("Mux", "MUX", ("Mux", [rung(C("True"), caja("Mux", ["Card := 2"] + T("Int"), ["K := 1", f"IN0 := {I3}", f"IN1 := {I5}", "ELSE := 0", f"OUT => {o}"]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", o, "5"), Q(okb("L_Mux", "MUX")))]))
o0, o1 = v("Demux_0", "Int"), v("Demux_1", "Int")
lad("Demux", "DEMUX", ("Demux", [rung(C("True"), caja("Demux", ["Card := 2"] + T("Int"), ["K := 1", f"IN := {I5}", f"OUT0 => {o0}", f"OUT1 => {o1}", "ELSE =>  "]))]),
    ("Comprobar", [rung(cmp("Eq", "Int", o1, "5"), Q(okb("L_Demux", "DEMUX")))]))
scl("Palabras", "Deco", "DECO", f"#dw := DECO(IN := 3);\n\t{R}.S_Deco := #dw = DWORD#16#00000008;")
scl("Palabras", "Enco", "ENCO", f"#i := ENCO({W0F});\n\t{R}.S_Enco := #i = 4;")
scl("Palabras", "Sel", "SEL", f"#i := SEL(G := TRUE, IN0 := {I3}, IN1 := {I5});\n\t{R}.S_Sel := #i = 5;")
scl("Palabras", "Mux", "MUX", f"#i := MUX(K := 1, IN0 := {I3}, IN1 := {I5}, INELSE := 0);\n\t{R}.S_Mux := #i = 5;")

# ===================================================== 9. Desplazamiento y rotación
for fn, n_, esperado in (("Shr", "4", "16#000F"), ("Shl", "4", "16#0F00"), ("Ror", "8", "16#F000"), ("Rol", "8", "16#F000")):
    o = v(f"{fn}_Out", "Word")
    lad(fn, fn.upper(), (fn, [rung(C("True"), caja(fn, T("Word"), [f"IN := {W0F}", f"N := {n_}", f"OUT => {o}"]))]),
        ("Comprobar", [rung(cmp("Eq", "Word", o, esperado), Q(okb(f"L_{fn}", fn.upper())))]))
    scl("Desplazamiento", fn, fn.upper(), f"#w := {fn.upper()}(IN := {W0F}, N := {n_});\n\t{R}.S_{fn} := #w = WORD#{esperado};")

# ===================================================== 10. Control del programa (SCL)
scl("Control", "REPEAT", "REPEAT / UNTIL", "#i := 0;\n\tREPEAT\n\t    #i := #i + 1;\n\tUNTIL #i >= 3\n\tEND_REPEAT;\n\t" + f"{R}.S_REPEAT := #i = 3;")
scl("Control", "EXIT_CONTINUE", "EXIT y CONTINUE", "#i := 0;\n\tFOR #j := 1 TO 10 DO\n\t    IF #j = 2 THEN CONTINUE; END_IF;\n\t    IF #j = 5 THEN EXIT; END_IF;\n\t    #i := #i + 1;\n\tEND_FOR;\n\t" + f"{R}.S_EXIT_CONTINUE := #i = 3;")
scl("Control", "GOTO", "GOTO y etiqueta", "#i := 0;\n\tGOTO SALTO;\n\t#i := 99;\nSALTO:\n\t#i := #i + 1;\n\t" + f"{R}.S_GOTO := #i = 1;")
scl("Control", "RUNTIME", "RUNTIME", f"#lr := RUNTIME({D}.Mem_Runtime);\n\t" + f"{R}.S_RUNTIME := TRUE;")
scl("Control", "GET_ERR_ID", "GET_ERR_ID", "#w := GET_ERR_ID();\n\t" + f"{R}.S_GET_ERR_ID := TRUE;")

# ===================================================== Base: señal que cambia cada ciclo (KOP probado)
b = Bloque("T4_Base", 41000, "Tanda 1: señal Tog (cambia cada ciclo)")
b.add("Tog", rung(C(TOG), "Not()", Q(TOG)))
b.add("Vida", rung(C("True"), caja("Add", ["Card := 2", "Type := AUTO", "DisableENO := TRUE"], [f"IN1 := {R}.Vida", "IN2 := 1", f"OUT => {R}.Vida"])))
b.sd()

# ===================================================== Fuentes SCL: una por familia (si una no compila, no arrastra a las demás)
COE = '''FUNCTION "T4_S_Coe" : UDInt
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
   VAR_IN_OUT
      Arr : Variant;
   END_VAR

BEGIN
	// CountOfElements solo admite un parámetro Variant, no un array directamente
	#T4_S_Coe := CountOfElements(#Arr);
END_FUNCTION

'''
for fam, pruebas in SCL.items():
    cuerpo = "\n\n".join(f"\t// {RES[[r[0] for r in RES].index(n.split('.')[1])][1]}\n\t{c}" for n, c in pruebas)
    w(f"FUENTES/T4_S_{fam}.scl", (COE if fam == "Mover" else "") + f'''FUNCTION "T4_S_{fam}" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_TEMP
      i : Int;
      j : Int;
      r : Int;
      d : DInt;
      re : Real;
      w : Word;
      dw : DWord;
      lr : LReal;
   END_VAR

BEGIN
{cuerpo}
END_FUNCTION
''')
# DB de instancias como fuente: sin versiones, vale para cualquier CPU
w("FUENTES/T4_Inst.db", 'DATA_BLOCK "T4_Inst"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n   VAR \n' +
  "".join(f"      {n} : {t};\n" for n, t in INST) + "   END_VAR\n\n\nBEGIN\n\nEND_DATA_BLOCK\n")
# DB de instancia de R_TRIG / F_TRIG: cada uno en su fuente y con una forma distinta del tipo, para saber cuál acepta TIA
w("FUENTES/T4_Rt_S.db", 'DATA_BLOCK "T4_Rt_S"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n"R_TRIG"\n\nBEGIN\n\nEND_DATA_BLOCK\n')
w("FUENTES/T4_Ft_S.db", 'DATA_BLOCK "T4_Ft_S"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\nF_TRIG\n\nBEGIN\n\nEND_DATA_BLOCK\n')

# Contar resultados (SCL)
lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T4_Contar.scl", f'''FUNCTION "T4_Contar" : Void
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

RES[:] = [(n, d) for n, d in RES if not (n.startswith("L_") and n[2:] in SKIP)]
# XML: datos y resultados
w("XML/T4_Datos.xml", gdb("T4_Datos", 41900, "".join(MEM(n, t, c, s) for n, t, s, c in VARS), "Tanda 1: datos de las pruebas"))
w("XML/T4_Resultado.xml", gdb("T4_Resultado", 41901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo si T4_Base se ejecuta") +
                              MEM("OK", "Int", "Pruebas correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"),
                              "Tanda 1: un Bool por prueba (L_ = KOP, S_ = SCL)"))

lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T4_Contar.scl", f'''FUNCTION "T4_Contar" : Void
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
# OB de ciclo que llama a todo (XML, formato probado)
w("XML/T4_Ciclo.xml", ob("T4_Ciclo", 127, ["T4_Base"] + LADFC + [f"T4_S_{f}" for f in SCL] + ["T4_Contar"], "Tanda 1: llama a todas las pruebas"))
# Lista de bloques para el paso 2 (llamadas)
llamadas = ["T4_Base"] + LADFC + [f"T4_S_{f}" for f in SCL] + ["T4_Contar"]
pass  # lista de llamadas: ver XML/T4_Ciclo.xml
filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 1: instrucciones básicas (TIA Portal V20)

{len(RES)} pruebas: {len(LADFC)} FC en KOP (SIMATIC SD, una por instrucción) y {len(SCL)} FC en SCL (una por familia). Resultados en `T4_Resultado` (`L_` = KOP, `S_` = SCL).

**Paso 1** (este paquete): importar en el proyecto de pruebas y pasar el resumen. Muchas cajas KOP no aparecen en ninguna exportación y su sintaxis SD está deducida: las que no entren o no compilen dirán qué hay que aprender.

**Paso 2**: con el resumen, se genera el OB que llama solo a lo que compila (y bloques vacíos en lugar de lo que no), para poder cargar y ver los resultados online.

| Bool | Prueba |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print("pruebas", len(RES), "KOP", len(LADFC), "SCL", len(SCL), "vars", len(VARS), "inst", len(INST))
