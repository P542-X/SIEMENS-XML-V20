# -*- coding: utf-8 -*-
"""Tanda 4B: comunicación EN MARCHA en PLCSIM, el PLC hablando consigo mismo:
TSEND_C/TRCV_C, TCON/TSEND/TRCV/TDISCON y Modbus TCP (MB_CLIENT contra MB_SERVER)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from simatic import *
OUT = inicio(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida", "tanda4b_com_marcha"))
MEM = m
R = '"T10_Resultado"'
RES = [("S_TSEND_C_TRCV_C", "TSEND_C → TRCV_C (puerto 2001): llegan los 4 bytes"),
       ("S_TCON_TSEND_TRCV", "TCON ×2 + TSEND → TRCV (puerto 2002): llegan los 4 bytes"),
       ("S_TDISCON", "TDISCON cierra la conexión cliente"),
       ("S_Modbus", "MB_CLIENT lee 2 registros de MB_SERVER (puerto 502)")]

def conn(nombre, ide, activo, puerto_local, puerto_remoto):
    ip = "#IP[1]", "#IP[2]", "#IP[3]", "#IP[4]"
    rem = [f"#{nombre}.RemoteAddress.ADDR[{k}] := {ip[k-1] if activo else '0'};" for k in range(1, 5)]
    return f"""	#{nombre}.InterfaceId := "Local~PROFINET_interface_1";
	#{nombre}.ID := {ide};
	#{nombre}.ConnectionType := 16#0B;          // TCP
	#{nombre}.ActiveEstablished := {'TRUE' if activo else 'FALSE'};
	""" + "\n\t".join(rem) + f"""
	#{nombre}.RemotePort := {puerto_remoto};
	#{nombre}.LocalPort := {puerto_local};
"""

scl = f'''FUNCTION_BLOCK "T10_Com"
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
   VAR
      IP : Array[1..4] of Byte := [192, 168, 0, 1];   // IP de la interfaz PROFINET X1 del PLC (cambiar si es otra)
      Iniciado : Bool;
      Reloj : TON_TIME;
      Pulso : Bool;
      // --- 1) TSEND_C / TRCV_C (compactas): servidor puerto 2001
      cSrvC : TCON_IP_v4;
      cCliC : TCON_IP_v4;
      sendC : TSEND_C;
      rcvC : TRCV_C;
      rstS : Bool;
      rstR : Bool;
      txC : Array[0..3] of Byte;
      rxC : Array[0..3] of Byte;
      // --- 2) TCON + TSEND / TRCV + TDISCON: servidor puerto 2002
      cSrv : TCON_IP_v4;
      cCli : TCON_IP_v4;
      tconS : TCON;
      tconC : TCON;
      tsend : TSEND;
      trcv : TRCV;
      tdis : TDISCON;
      ConS_OK : Bool;
      ConC_OK : Bool;
      Desconectado : Bool;
      tx : Array[0..3] of Byte;
      rx : Array[0..3] of Byte;
      // --- 3) Modbus TCP: servidor puerto 502
      cMbS : TCON_IP_v4;
      cMbC : TCON_IP_v4;
      mbS : MB_SERVER;
      mbC : MB_CLIENT;
      Registros : Array[0..9] of Word;
      Leidos : Array[0..1] of Word;
      // --- Último estado de cada instrucción (para diagnóstico)
      St_SendC : Word;
      St_RcvC : Word;
      St_TconS : Word;
      St_TconC : Word;
      St_Tsend : Word;
      St_Trcv : Word;
      St_Tdis : Word;
      St_MbS : Word;
      St_MbC : Word;
   END_VAR

BEGIN
	IF NOT #Iniciado THEN
{conn("cSrvC", 1, False, 2001, 0)}{conn("cCliC", 2, True, 0, 2001)}{conn("cSrv", 3, False, 2002, 0)}{conn("cCli", 4, True, 0, 2002)}{conn("cMbS", 5, False, 502, 0)}{conn("cMbC", 6, True, 0, 502)}
	    #txC[0] := 16#12; #txC[1] := 16#34; #txC[2] := 16#56; #txC[3] := 16#78;
	    #tx[0] := 16#A1; #tx[1] := 16#B2; #tx[2] := 16#C3; #tx[3] := 16#D4;
	    #Registros[0] := 1234; #Registros[1] := 5678;
	    #Iniciado := TRUE;
	END_IF;

	// Pulso de un ciclo cada segundo (REQ de envíos y lecturas).
	// OJO: no usar #Reloj(IN := NOT #Reloj.Q): en S7-1500 leer .Q actualiza el temporizador,
	// así que IN pasa a FALSE justo cuando vence y el pulso no sale nunca.
	#Reloj(IN := TRUE, PT := T#1S);
	#Pulso := #Reloj.Q;
	IF #Pulso THEN
	    #Reloj(IN := FALSE, PT := T#1S);   // reinicia: vuelve a contar en el ciclo siguiente
	END_IF;

	// ---------- 1) TSEND_C / TRCV_C
	#rcvC(EN_R := TRUE, CONT := TRUE, LEN := 4, CONNECT := #cSrvC, DATA := #rxC, COM_RST := #rstR);
	#sendC(REQ := #Pulso, CONT := TRUE, LEN := 4, CONNECT := #cCliC, DATA := #txC, COM_RST := #rstS);
	IF #rcvC.STATUS <> 16#7000 THEN #St_RcvC := #rcvC.STATUS; END_IF;
	IF #sendC.STATUS <> 16#7000 THEN #St_SendC := #sendC.STATUS; END_IF;
	IF #rcvC.DONE AND #rxC[0] = 16#12 AND #rxC[1] = 16#34 AND #rxC[2] = 16#56 AND #rxC[3] = 16#78 THEN
	    {R}.S_TSEND_C_TRCV_C := TRUE;
	END_IF;

	// ---------- 2) TCON / TSEND / TRCV / TDISCON
	#tconS(REQ := #Pulso AND NOT #ConS_OK, ID := 3, CONNECT := #cSrv);
	IF #tconS.DONE THEN #ConS_OK := TRUE; END_IF;
	IF #tconS.ERROR THEN #St_TconS := #tconS.STATUS; END_IF;
	#tconC(REQ := #Pulso AND NOT #ConC_OK AND NOT #Desconectado, ID := 4, CONNECT := #cCli);
	IF #tconC.DONE THEN #ConC_OK := TRUE; END_IF;
	IF #tconC.ERROR THEN #St_TconC := #tconC.STATUS; END_IF;
	#trcv(EN_R := #ConS_OK, ID := 3, LEN := 4, DATA := #rx);
	IF #trcv.ERROR THEN #St_Trcv := #trcv.STATUS; END_IF;
	#tsend(REQ := #Pulso AND #ConC_OK AND NOT {R}.S_TCON_TSEND_TRCV, ID := 4, LEN := 4, DATA := #tx);
	IF #tsend.ERROR THEN #St_Tsend := #tsend.STATUS; END_IF;
	IF #trcv.NDR AND #rx[0] = 16#A1 AND #rx[1] = 16#B2 AND #rx[2] = 16#C3 AND #rx[3] = 16#D4 THEN
	    {R}.S_TCON_TSEND_TRCV := TRUE;
	END_IF;
	// Cuando ha llegado, se cierra la conexión del cliente con TDISCON (una vez)
	#tdis(REQ := {R}.S_TCON_TSEND_TRCV AND #ConC_OK AND NOT #Desconectado, ID := 4);
	IF #tdis.DONE THEN
	    #Desconectado := TRUE;
	    #ConC_OK := FALSE;
	    {R}.S_TDISCON := TRUE;
	END_IF;
	IF #tdis.ERROR THEN #St_Tdis := #tdis.STATUS; END_IF;

	// ---------- 3) Modbus TCP: el cliente lee 2 registros del servidor (40001 = Registros[0])
	#mbS(DISCONNECT := FALSE, MB_HOLD_REG := #Registros, CONNECT := #cMbS);
	IF #mbS.ERROR THEN #St_MbS := #mbS.STATUS; END_IF;
	#mbC(REQ := #Pulso, DISCONNECT := FALSE, MB_MODE := 0, MB_DATA_ADDR := 40001, MB_DATA_LEN := 2, MB_DATA_PTR := #Leidos, CONNECT := #cMbC);
	IF #mbC.ERROR THEN #St_MbC := #mbC.STATUS; END_IF;
	IF #mbC.DONE AND #Leidos[0] = 1234 AND #Leidos[1] = 5678 THEN
	    {R}.S_Modbus := TRUE;
	END_IF;
END_FUNCTION_BLOCK

DATA_BLOCK "T10_Com_DB"
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
NON_RETAIN
"T10_Com"

BEGIN

END_DATA_BLOCK

FUNCTION "T10_Llamar" : Void
{{ S7_Optimized_Access := 'TRUE' }}
VERSION : 0.1
BEGIN
	"T10_Com_DB"();
	{R}.Vida := {R}.Vida + 1;
	{R}.OK := BOOL_TO_INT({R}.S_TSEND_C_TRCV_C) + BOOL_TO_INT({R}.S_TCON_TSEND_TRCV) + BOOL_TO_INT({R}.S_TDISCON) + BOOL_TO_INT({R}.S_Modbus);
	{R}.Todo_OK := {R}.OK = {R}.Total;
END_FUNCTION
'''
w("FUENTES/T10_Com.scl", scl)
w("XML/T10_Resultado.xml", gdb("T10_Resultado", 47901, "".join(MEM(n, "Bool", d) for n, d in RES) + MEM("Vida", "DInt", "Sube cada ciclo") +
                               MEM("OK", "Int", "Correctas") + MEM("Total", "Int", "Pruebas", str(len(RES))) + MEM("Todo_OK", "Bool", "Todas correctas"), "Tanda 4B: comunicación en marcha"))
w("XML/T10_Ciclo.xml", ob("T10_Ciclo", 131, ["T10_Llamar"], "Tanda 4B: comunicación del PLC consigo mismo"))
filas = "\n".join(f"| `{n}` | {d} |" for n, d in RES)
w("LEEME.md", f'''# Tanda 4B: comunicación en marcha (el PLC hablando consigo mismo)

| Bool en `T10_Resultado` | Prueba |
|---|---|
{filas}

## Antes de importar

- La **IP** que usan los clientes es la del propio PLC: por defecto **192.168.0.1**. Mira la de `PLC_1` en *Device configuration → PROFINET interface [X1] → Ethernet addresses*. Si es otra, después de importar cámbiala en `T10_Com_DB` (miembro `IP`) o en la fuente.
- La interfaz es la constante de sistema `"Local~PROFINET_interface_1"` (X1).

## Importar y probar

1. XML → Importar ficheros… → este .zip.
2. Compilar (0 errores). Cargar en PLCSIM, RUN.
3. Esperar unos segundos (envía cada segundo) y mirar `T10_Resultado`: `OK` = `Total` ({len(RES)}), `Todo_OK` = TRUE.
4. Si alguna se queda en FALSE: abrir `T10_Com_DB` online y mirar los `St_…` (último STATUS de cada instrucción). Pasármelos.

Si PLCSIM no permite que el PLC se conecte a sí mismo, las tres se quedarán en FALSE con un STATUS de conexión: entonces la comunicación queda en "importa y compila".

DB 47901, OB 131, FB `T10_Com` + `T10_Com_DB`, FC `T10_Llamar`. Puertos 2001, 2002 y 502; IDs de conexión 1 a 6.
''', bom=False)
z = empaquetar(OUT)
print(z)
