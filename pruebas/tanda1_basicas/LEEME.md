# Tanda 1: instrucciones básicas (TIA Portal V20)

**96 pruebas, todas TRUE en marcha** (PLCSIM, CPU 1518-3 PN 6ES7 518-3AT10-0AB0 FW V4.0, 30/10/2026): 40 FC en KOP (SIMATIC SD, una por instrucción) y 56 pruebas SCL (una FC por familia).

## Usar

1. En un proyecto de pruebas (con *Support simulation* activado): *XML → Importar carpeta…* sobre esta carpeta (o comprimirla e *Importar ficheros*).
2. Compilar (0 errores), cargar en PLCSIM, CPU en RUN.
3. `T4_Resultado` online: `Vida` sube, `OK` = `Total` (96), `Todo_OK` = TRUE.

Usa FC 41000..41063, DB 41900/41901 y OB 127.

## Qué hay

- `SD/T4_L_*`: una FC KOP por instrucción. `SD/T4_Base`: señal `Tog` (cambia cada ciclo) y contador `Vida`.
- `FUENTES/T4_S_*.scl`: pruebas SCL por familia; `T4_Inst.db` (IEC_TIMER / IEC_COUNTER), `T4_Rt_S.db` / `T4_Ft_S.db` (instancias de R_TRIG / F_TRIG), `T4_Contar.scl`.
- `XML/`: `T4_Datos`, `T4_Resultado`, OB `T4_Ciclo` (127).

## Pendiente (sintaxis SD por aprender de una exportación real)

Inc, Dec, Min, Max, Limit, Deco, Enco, Sel, Expt, IN_RANGE, OUT_RANGE, OK, NOT_OK, R_TRIG, F_TRIG, RS, SET_BF, RESET_BF, MOVE_BLK, UMOVE_BLK, MOVE_BLK_VARIANT, FILL_BLK, NORM_X, SCALE_X. MOVE_BLK, FILL_BLK y NORM_X con la sintaxis deducida **cierran TIA**.
