# Tanda 2: fecha/hora y texto (TIA V20) — 27/27 TRUE en marcha

27 pruebas (22 FC KOP con la sintaxis SD de la exportación real `Aprender_T2` + 5 grupos SCL). FC 43001..43022, DB 43900/43901, OB 129.

**Ojo:** `WR_LOC_T` y `WR_SYS_T` escriben la hora del PLC (una sola vez, con la misma hora que acaban de leer). Hacerlo solo en el PLC de pruebas / PLCSIM.

1. XML → Importar ficheros… → este .zip → Sí a sobrescribir.
2. Compilar (0 errores), cargar en PLCSIM, RUN.
3. `T6_Resultado`: `Vida` sube, `OK` = `Total` (27), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
| `L_T_CONV_T_DI` | T_CONV Time→DInt |
| `L_T_CONV_DI_T` | T_CONV DInt→Time |
| `L_T_ADD` | T_ADD DTL + Time (10:00 + 1h) |
| `L_T_SUB` | T_SUB DTL − Time (10:00 − 1h) |
| `L_T_DIFF` | T_DIFF DTL (11:00 − 10:00) |
| `L_T_COMBINE` | T_COMBINE Date + Time_Of_Day |
| `L_RD_LOC_T` | RD_LOC_T (hora local) |
| `L_WR_LOC_T` | WR_LOC_T |
| `L_WR_SYS_T` | WR_SYS_T |
| `L_Eq_DTL` | Eq con SrcType DTL |
| `L_S_CONV_I_S` | S_CONV Int→String (comprobado con la vuelta) |
| `L_S_CONV_S_I` | S_CONV String→Int |
| `L_LEFT` | LEFT |
| `L_RIGHT` | RIGHT |
| `L_MID` | MID |
| `L_DELETE` | DELETE |
| `L_INSERT` | INSERT |
| `L_REPLACE` | REPLACE |
| `L_FIND` | FIND |
| `L_ATH` | ATH '1A2B' → 16#1A, 16#2B |
| `L_HTA` | HTA 16#1A, 16#2B → '1A2B' |
| `L_Eq_Char` | Eq con SrcType Char |
| `S_Fecha` | [SCL] TIME_TO_DINT, DINT_TO_TIME, T_ADD, T_SUB, T_DIFF, CONCAT_DATE_TOD (T_COMBINE en KOP) |
| `S_Reloj` | [SCL] RD_LOC_T, RD_SYS_T, WR_LOC_T y WR_SYS_T (una vez) |
| `S_Texto` | [SCL] INT_TO_STRING, STRING_TO_INT, LEFT, RIGHT, MID, DELETE, INSERT, REPLACE, FIND |
| `S_Hex` | [SCL] ATH y HTA |
| `S_Comparar` | [SCL] comparar DTL y Char |
