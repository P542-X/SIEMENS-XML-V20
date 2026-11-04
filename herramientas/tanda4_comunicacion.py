# -*- coding: utf-8 -*-
"""Tanda 4: comunicación. SOLO importar y compilar (sin conexiones reales). Una FC KOP por instrucción,
FB del sistema con DB de instancia propio (desde fuente). Sintaxis SD de la exportación real Aprender_T4."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda4_comunicacion"))
LISTA, DBS = [], []
def raw(t): return "            " + t.strip().replace("\n", "\n            ") + "\n"
def SYS(nombre, tipo, ver="1.0"): return f"        {{\n             InstructionName := \"{tipo}\";\n             LibVersion := \"{ver}\"\n        }}\n        {nombre} : {tipo};\n"
BASE = "".join(f"        {x}\n" for x in ("req : Bool;", "done : Bool;", "busy : Bool;", "err : Bool;", "st : Word;", "data : Array[0..99] of Byte;", "len : UDInt;"))
def add(nombre, fb, desc, temp, params, plantilla="", entrada=""):
    n = f"T9_L_{nombre}"; num = 46001 + len(LISTA)
    db = f"T9_{nombre}_DB"; DBS.append((db, fb))
    b = Bloque(n, num, f"Tanda 4 (solo compilar): {desc}")
    llamada = f'"{db}".{fb}{plantilla}(\n' + ", \n".join("    " + p for p in params) + "\n)"
    b.add(desc, rung(C("#req"), raw(llamada)))
    b.sd("FUNCTION", (entrada or "") + "    VAR_TEMP \n" + BASE + temp + "    END_VAR\n")
    LISTA.append((n, desc))
OUC = "".join(f"        {x}\n" for x in ("id : CONN_OUC;",)) + SYS("con", "TCON_IP_v4")
add("TSEND_C", "TSEND_C", "TSEND_C", OUC.replace("id : CONN_OUC;\n", "") + "        rst : Bool;\n",
    ["REQ := #req", "CONT := #req", "LEN := #len", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "CONNECT := #con", "DATA := #data", "ADDR :=  ", "COM_RST := #rst"])
add("TRCV_C", "TRCV_C", "TRCV_C", OUC.replace("id : CONN_OUC;\n", "") + "        rst : Bool;\n        rlen : UDInt;\n",
    ["EN_R := #req", "CONT := #req", "LEN := #len", "ADHOC := #req", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "RCVD_LEN => #rlen", "CONNECT := #con", "DATA := #data", "ADDR :=  ", "COM_RST := #rst"])
add("TCON", "TCON", "TCON", OUC, ["REQ := #req", "ID := #id", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "CONNECT := #con"],
    "{\n    con_type := Variant; \n    id_type := CONN_OUC\n}")
add("TDISCON", "TDISCON", "TDISCON", "        id : CONN_OUC;\n", ["REQ := #req", "ID := #id", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st"], "{ id_type := CONN_OUC }")
add("TSEND", "TSEND", "TSEND", "        id : CONN_OUC;\n", ["REQ := #req", "ID := #id", "LEN := #len", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "DATA := #data"],
    "{\n    data_type := Variant; \n    len_type := UDInt; \n    id_type := CONN_OUC\n}")
add("TRCV", "TRCV", "TRCV", "        id : CONN_OUC;\n        rlen : UDInt;\n", ["EN_R := #req", "ID := #id", "LEN := #len", "ADHOC := #req", "NDR => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "RCVD_LEN => #rlen", "DATA := #data"],
    "{\n    ptr_type := Variant; \n    value_type := UDInt; \n    id_type := CONN_OUC; \n    card := 1\n}")
add("TUSEND", "TUSEND", "TUSEND", "        id : CONN_OUC;\n" + SYS("dir", "TADDR_Param"), ["REQ := #req", "ID := #id", "LEN := #len", "DONE => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "DATA := #data", "ADDR := #dir"],
    "{\n    ptr_type := Variant; \n    value_type := UDInt; \n    id_type := CONN_OUC; \n    addr_type := Variant\n}")
add("TURCV", "TURCV", "TURCV (plantillas deducidas de TUSEND)", "        id : CONN_OUC;\n        rlen : UDInt;\n" + SYS("dir", "TADDR_Param"),
    ["EN_R := #req", "ID := #id", "LEN := #len", "NDR => #done", "BUSY => #busy", "ERROR => #err", "STATUS => #st", "RCVD_LEN => #rlen", "DATA := #data", "ADDR := #dir"],
    "{\n    ptr_type := Variant; \n    value_type := UDInt; \n    id_type := CONN_OUC; \n    addr_type := Variant\n}")
REM = "    VAR_IN_OUT \n        remoto : Remote;\n    END_VAR\n"
add("PUT", "PUT", "PUT", "        id : CONN_PRG;\n", ["REQ := #req", "ID := #id", "DONE => #done", "ERROR => #err", "STATUS => #st", "ADDR_1 := #remoto", "ADDR_2 :=  ", "ADDR_3 :=  ", "ADDR_4 :=  ",
    "SD_1 := #data", "SD_2 :=  ", "SD_3 :=  ", "SD_4 :=  "], "{\n    remote_type := Remote; \n    local_type := Variant\n}", REM)
add("GET", "GET", "GET", "        id : CONN_PRG;\n", ["REQ := #req", "ID := #id", "NDR => #done", "ERROR => #err", "STATUS => #st", "ADDR_1 := #remoto", "ADDR_2 :=  ", "ADDR_3 :=  ", "ADDR_4 :=  ",
    "RD_1 := #data", "RD_2 :=  ", "RD_3 :=  ", "RD_4 :=  "], "{\n    remote_type := Remote; \n    local_type := Variant\n}", REM)
PRG = "        id : CONN_PRG;\n        rid : CONN_R_ID;\n        wlen : Word;\n"
TPL = "{\n    ptr_type := Variant; \n    id_type := CONN_PRG; \n    r_id_type := CONN_R_ID\n}"
add("BSEND", "BSEND", "BSEND", PRG, ["REQ := #req", "R := #req", "ID := #id", "R_ID := #rid", "DONE => #done", "ERROR => #err", "STATUS => #st", "SD_1 := #data", "LEN := #wlen"], TPL)
add("BRCV", "BRCV", "BRCV", PRG, ["EN_R := #req", "ID := #id", "R_ID := #rid", "NDR => #done", "ERROR => #err", "STATUS => #st", "RD_1 := #data", "LEN := #wlen"], TPL)
add("USEND", "USEND", "USEND", PRG, ["REQ := #req", "ID := #id", "R_ID := #rid", "DONE => #done", "ERROR => #err", "STATUS => #st", "SD_1 := #data", "SD_2 :=  ", "SD_3 :=  ", "SD_4 :=  "], TPL)
add("URCV", "URCV", "URCV", PRG, ["EN_R := #req", "ID := #id", "R_ID := #rid", "NDR => #done", "ERROR => #err", "STATUS => #st", "RD_1 := #data", "RD_2 :=  ", "RD_3 :=  ", "RD_4 :=  "], TPL)
MB = SYS("con", "TCON_IP_v4") + "        regs : Array[0..9] of Word;\n        mode : USInt;\n        addr : UDInt;\n        mlen : UInt;\n"
add("MB_CLIENT", "MB_CLIENT", "MB_CLIENT", MB, ["REQ := #req", "DISCONNECT := #req", "MB_MODE := #mode", "MB_DATA_ADDR := #addr", "MB_DATA_LEN := #mlen",
    "RD_MB_DATA_ADDR :=  ", "RD_MB_DATA_LEN :=  ", "WR_MB_DATA_ADDR :=  ", "WR_MB_DATA_LEN :=  ", "DONE => #done", "BUSY => #busy",
    "ERROR => #err", "STATUS => #st", "MB_DATA_PTR := #regs", "CONNECT := #con", "RD_MB_DATA_PTR :=  ", "WR_MB_DATA_PTR :=  "])
add("MB_SERVER", "MB_SERVER", "MB_SERVER", MB, ["DISCONNECT := #req", "NDR => #done", "DR => #busy", "ERROR => #err", "STATUS => #st", "MB_HOLD_REG := #regs", "CONNECT := #con"])

# WWW no es un FB: función con plantilla
num = 46001 + len(LISTA)
b = Bloque("T9_L_WWW", num, "Tanda 4 (solo compilar): WWW")
b.add("WWW", rung(C("#req"), raw("WWW{ value_type := DB_WWW }(\n    CTRL_DB := #dbw, \n    RET_VAL => #ret\n)")))
b.sd("FUNCTION", "    VAR_TEMP \n        req : Bool;\n        ret : Int;\n        dbw : DB_WWW;\n    END_VAR\n")
LISTA.append(("T9_L_WWW", "WWW"))

# DB de instancia de cada FB del sistema, desde fuente (como R_TRIG y RDREC)
for db, fb in DBS:
    w(f"FUENTES/{db}.db", f'DATA_BLOCK "{db}"\n{{ S7_Optimized_Access := \'TRUE\' }}\nVERSION : 0.1\nNON_RETAIN\n{fb}\n\nBEGIN\n\nEND_DATA_BLOCK\n')

filas = "\n".join(f"| `{n}` | {d} |" for n, d in LISTA)
w("LEEME.md", f'''# Tanda 4: comunicación — SOLO importar y compilar

Sin conexiones configuradas no se pueden ejecutar: la prueba es que **importen y compilen**. Ningún OB llama a estas FC.
Cada FB del sistema tiene su DB de instancia propio (`T9_<instrucción>_DB`, desde fuente). FC 46001..{num}.

1. Guardar. XML → Importar ficheros… → este .zip.
2. Pasar el resumen.
3. Después: XML → Deshacer última importación.

| FC | Instrucción |
|---|---|
{filas}
''', bom=False)
z = empaquetar(OUT)
print(len(LISTA), z)
