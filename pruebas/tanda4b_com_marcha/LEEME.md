# Tanda 4B: comunicación en marcha (el PLC hablando consigo mismo) — 4/4 TRUE

| Bool en `T10_Resultado` | Prueba |
|---|---|
| `S_TSEND_C_TRCV_C` | TSEND_C → TRCV_C (puerto 2001): llegan los 4 bytes |
| `S_TCON_TSEND_TRCV` | TCON ×2 + TSEND → TRCV (puerto 2002): llegan los 4 bytes |
| `S_TDISCON` | TDISCON cierra la conexión cliente |
| `S_Modbus` | MB_CLIENT lee 2 registros de MB_SERVER (puerto 502) |

## Antes de importar

- La **IP** que usan los clientes es la del propio PLC: por defecto **192.168.0.1**. Mira la de `PLC_1` en *Device configuration → PROFINET interface [X1] → Ethernet addresses*. Si es otra, después de importar cámbiala en `T10_Com_DB` (miembro `IP`) o en la fuente.
- La interfaz es la constante de sistema `"Local~PROFINET_interface_1"` (X1).

## Importar y probar

1. XML → Importar ficheros… → este .zip.
2. Compilar (0 errores). Cargar en PLCSIM, RUN.
3. Esperar unos segundos (envía cada segundo) y mirar `T10_Resultado`: `OK` = `Total` (4), `Todo_OK` = TRUE.
4. Si alguna se queda en FALSE: abrir `T10_Com_DB` online y mirar los `St_…` (último STATUS de cada instrucción). Pasármelos.

Si PLCSIM no permite que el PLC se conecte a sí mismo, las tres se quedarán en FALSE con un STATUS de conexión: entonces la comunicación queda en "importa y compila".

DB 47901, OB 131, FB `T10_Com` + `T10_Com_DB`, FC `T10_Llamar`. Puertos 2001, 2002 y 502; IDs de conexión 1 a 6.
