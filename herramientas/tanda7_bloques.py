# -*- coding: utf-8 -*-
"""Tanda 7 en marcha: OB de arranque y de error de programación creados por XML, VAR CONSTANT, Variant de entrada, Array[*]."""
import re
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda7_bloques"))
MEM = m
D, R = '"T12_Datos"', '"T12_Resultado"'
RES = [("L_Constante", "VAR CONSTANT en un FB KOP (LIMITE = 10)"),
       ("L_Variant", "Variant como entrada de un FB KOP + VariantGet"),
       ("S_ArrayStar", "Array[*] en SCL: suma con LOWER_BOUND/UPPER_BOUND de un Array[0..4] y de un Array[1..3]"),
       ("L_ArrayStar", "Array[*] en la interfaz de una FC KOP (SD) llamada con dos arrays"),
       ("L_Startup", "OB100 (Startup) creado por XML: se ha ejecutado al arrancar"),
       ("L_ProgError", "OB121 (ProgrammingError) creado por XML: atrapa un índice fuera de rango")]
VARS = [("Arranques", "DInt", None, "Lo incrementa el OB100"), ("ErroresProg", "DInt", None, "Lo incrementa el OB121"),
        ("Const_Out", "UInt", None, ""), ("Var_Out", "Int", None, ""), ("Int_5", "Int", "5", ""),
        ("A5", "Array[0..4] of Int", None, "1..5"), ("A3", "Array[1..3] of Int", None, "10,20,30"),
        ("Suma5", "DInt", None, ""), ("Suma3", "DInt", None, ""), ("Num5", "DInt", None, ""), ("Num3", "DInt", None, ""),
        ("Indice", "Int", "7", "Índice fuera de rango a propósito"), ("Error_Hecho", "Bool", None, "")]

# --- FB KOP con VAR CONSTANT y FB KOP con Variant de entrada (formas de la exportación real)
fb = Bloque("T12_FB_Const", 48010, "Tanda 7: VAR CONSTANT")
fb.add("Usar la constante", rung(C("True"), caja("Move", ["Card := 1", "DisableENO := TRUE"], ["IN := #LIMITE", "OUT1 => #Salida"])))
fb.sd("FUNCTION_BLOCK", "    VAR_OUTPUT \n        Salida : UInt;\n    END_VAR\n    VAR CONSTANT \n        LIMITE : Int := 10;\n    END_VAR\n")
fb = Bloque("T12_FB_Variant", 48011, "Tanda 7: Variant de entrada")
fb.add("VariantGet", rung(C("True"), caja("VariantGet", ["src_type := Variant", "dst_type := Variant"], ["SRC := #entrada", "DST => #Salida"])))
fb.sd("FUNCTION_BLOCK", "    VAR_INPUT \n        entrada : Variant;\n    END_VAR\n    VAR_OUTPUT \n        Salida : Int;\n    END_VAR\n")
for n, fbn in (("T12_FB_Const_DB", "T12_FB_Const"), ("T12_FB_Variant_DB", "T12_FB_Variant")):
    w(f"SD/{n}.s7dcl", f'        {{\n           S7_Optimized := "TRUE";\n           S7_StandardRetain := "FALSE"\n        }}\n    DATA_BLOCK {n} : {fbn}\n    END_DATA_BLOCK\n')
# --- FC KOP con Array[*] (SD): cuenta elementos con UPPER_BOUND/LOWER_BOUND
fc = Bloque("T12_FC_ArrayStar", 48012, "Tanda 7: Array[*] en KOP")
AD = lambda op, a_, b_, o: caja(op, (["Card := 2"] if op == "Add" else []) + ["Type := AUTO", "DisableENO := TRUE"], [f"IN1 := {a_}", f"IN2 := {b_}", f"OUT => {o}"])
fc.add("Límite superior", rung(C("True"), caja("Upper_Bound", ["src_type := Variant", "DisableENO := FALSE"], ["ARR := #lista", "DIM := 1", "OUT => #hi"])))
fc.add("Límite inferior", rung(C("True"), caja("Lower_Bound", ["src_type := Variant", "DisableENO := FALSE"], ["ARR := #lista", "DIM := 1", "OUT => #lo"])))
fc.add("hi - lo", rung(C("True"), AD("Sub", "#hi", "#lo", "#d")))
fc.add("+ 1", rung(C("True"), AD("Add", "#d", "1", "#n")))
fc.sd("FUNCTION", "    VAR_OUTPUT \n        n : DInt;\n    END_VAR\n    VAR_IN_OUT \n        lista : Array[*] of Int;\n    END_VAR\n    VAR_TEMP \n        hi : DInt;\n        lo : DInt;\n        d : DInt;\n    END_VAR\n")
fcb = Bloque("T12_FC_ArrayStar_B", 48014, "Tanda 7: Array[*] escrito con asterisco (variante)")
fcb.add("Límite superior", rung(C("True"), caja("Upper_Bound", ["src_type := Variant", "DisableENO := FALSE"], ["ARR := #lista", "DIM := 1", "OUT => #n"])))
fcb.sd("FUNCTION", "    VAR_OUTPUT \n        n : DInt;\n    END_VAR\n    VAR_IN_OUT \n        lista : Array[*] of Int;\n    END_VAR\n")
# --- FC KOP que lo llama todo y comprueba
b = Bloque("T12_Pruebas", 48013, "Tanda 7: llamadas y comprobaciones")
b.add("FB con constante", rung(raw := f'            "T12_FB_Const_DB"(\n                Salida => {D}.Const_Out\n            )\n'))
b.add("Comprobar constante", rung(cmp("Eq", "UInt", f"{D}.Const_Out", "10"), Q(f"{R}.L_Constante")))
b.add("FB con Variant", rung(f'            "T12_FB_Variant_DB"(\n                entrada := {D}.Int_5, \n                Salida => {D}.Var_Out\n            )\n'))
b.add("Comprobar Variant", rung(cmp("Eq", "Int", f"{D}.Var_Out", "5"), Q(f"{R}.L_Variant")))
b.add("Array[*] con 5", rung(f'            "T12_FC_ArrayStar"(\n                n => {D}.Num5, \n                lista := {D}.A5\n            )\n'))
b.add("Array[*] con 3", rung(f'            "T12_FC_ArrayStar"(\n                n => {D}.Num3, \n                lista := {D}.A3\n            )\n'))
b.add("Comprobar Array[*]", rung(cmp("Eq", "DInt", f"{D}.Num5", "5"), cmp("Eq", "DInt", f"{D}.Num3", "3"), Q(f"{R}.L_ArrayStar")))
b.add("Comprobar OB100", rung(cmp("Gt", "DInt", f"{D}.Arranques", "0"), Q(f"{R}.L_Startup")))
b.add("Comprobar OB121", rung(cmp("Gt", "DInt", f"{D}.ErroresProg", "0"), Q(f"{R}.L_ProgError")))
b.add("Vida", rung(C("True"), caja("Add", ["Card := 2", "Type := AUTO", "DisableENO := TRUE"], [f"IN1 := {R}.Vida", "IN2 := 1", f"OUT => {R}.Vida"])))
b.sd()

# --- SCL: Array[*] y el error de programación provocado
w("FUENTES/T12_SCL.scl", f'''FUNCTION "T12_Suma" : DInt
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_IN_OUT
      a : Array[*] of Int;
   END_VAR
   VAR_TEMP
      i : DInt;
      s : DInt;
   END_VAR

BEGIN
	#s := 0;
	FOR #i := LOWER_BOUND(ARR := #a, DIM := 1) TO UPPER_BOUND(ARR := #a, DIM := 1) DO
	    #s := #s + #a[#i];
	END_FOR;
	#T12_Suma := #s;
END_FUNCTION

FUNCTION "T12_S_Pruebas" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR_TEMP
      i : Int;
   END_VAR

BEGIN
	FOR #i := 0 TO 4 DO {D}.A5[#i] := #i + 1; END_FOR;
	{D}.A3[1] := 10; {D}.A3[2] := 20; {D}.A3[3] := 30;
	{D}.Suma5 := "T12_Suma"(a := {D}.A5);
	{D}.Suma3 := "T12_Suma"(a := {D}.A3);
	{R}.S_ArrayStar := {D}.Suma5 = 15 AND {D}.Suma3 = 60;

	// Error de programación a propósito (una vez): índice 7 en un Array[0..4] -> lo atrapa el OB121
	IF NOT {D}.Error_Hecho THEN
	    {D}.Error_Hecho := TRUE;
	    {D}.A5[{D}.Indice] := 0;
	END_IF;
END_FUNCTION
''')
w("XML/T12_Datos.xml", gdb("T12_Datos", 48900, "".join(MEM(n, t, c, s) for n, t, s, c in VARS), "Tanda 7: datos"))
w("XML/T12_Resultado.xml", gdb("T12_Resultado", 48901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo") +
                               MEM("OK", "Int", "Correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"), "Tanda 7: un Bool por prueba"))
lineas = "\n".join(f"\tIF {R}.{n} THEN #n := #n + 1; END_IF;" for n, _ in RES)
w("FUENTES/T12_Contar.scl", f'''FUNCTION "T12_Contar" : Void
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
w("XML/T12_Ciclo.xml", ob("T12_Ciclo", 132, ["T12_S_Pruebas", "T12_Pruebas", "T12_Contar"], "Tanda 7: llama a las pruebas"))
# --- OB100 y OB121 por XML, con los nombres que ya tienes en TESTXML (se sobrescriben)
def ob_tipo(nombre, numero, tipo, entradas, miembro):
    x = ob(nombre, numero, [], f"Tanda 7: {tipo}, incrementa {miembro}")
    x = x.replace("<SecondaryType>ProgramCycle</SecondaryType>", f"<SecondaryType>{tipo}</SecondaryType>")
    if tipo in ("ProgrammingError", "IOAccessError", "TimeErrorInterrupt", "DiagnosticErrorInterrupt", "RackOrStationFailure"):
        x = re.sub(r"\s*<AutoNumber>[^<]*</AutoNumber>", "", x)   # número fijo: TIA no admite AutoNumber
    ent = "".join(f'          <Member Name="{n}" Datatype="{t}" Informative="true" />\n' for n, t in entradas)
    x = re.sub(r'<Section Name="Input">.*?</Section>', f'<Section Name="Input">\n{ent}        </Section>', x, flags=re.S)
    i = x.index('    <ObjectList>') + len('    <ObjectList>\n')
    j = x.index('</MultilingualText>\n', i) + len('</MultilingualText>\n')
    return x[:j] + red_vida("T12_Datos", miembro).replace("\r\n", "\n") + x[j:]
w("XML/Startup.xml", ob_tipo("Startup", 100, "Startup", [("LostRetentive", "Bool"), ("LostRTC", "Bool")], "Arranques"))
w("XML/ProgrammingError.xml", ob_tipo("ProgrammingError", 121, "ProgrammingError",
    [("BlockNr", "UInt"), ("Reaction", "USInt"), ("Fault_ID", "Byte"), ("BlockType", "USInt"), ("Area", "USInt"), ("DBNr", "DB_ANY"),
     ("Csg_OBNr", "OB_ANY"), ("Csg_Prio", "USInt"), ("Width", "USInt")], "ErroresProg"))
filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 7: bloques, interfaz y OB (en marcha)

| Bool en `T12_Resultado` | Prueba |
|---|---|
{filas}

`Startup` (OB100) y `ProgrammingError` (OB121) llevan **el mismo nombre que los que creaste a mano** en TESTXML: responder **Sí** a sobrescribir.
El OB121 atrapa a propósito un índice fuera de rango (una vez): la CPU sigue en RUN.

1. XML → Importar ficheros… → este .zip → Sí.
2. Compilar (0 errores). Cargar en PLCSIM (DB: reinicializar), **STOP → RUN** (para que se ejecute el OB100).
3. `T12_Resultado`: `OK` = {len(RES)}, `Todo_OK` = TRUE.

FB 48010/48011 + DB, FC 48012/48013, DB 48900/48901, OB 132, OB100, OB121.
''', bom=False)
z = empaquetar(OUT)
print(len(RES), z)
