# Tanda 1b: las cajas KOP aprendidas de la exportación real (24/24 TRUE en marcha)

24 pruebas, una FC por caja, con la sintaxis SD exacta de la exportación `palclaude` (TIA V20). Dos nombres son deducidos por analogía y van marcados [DEDUCIDA]: `R_BitfieldCoil` (RESET_BF) y `FillBlockU` (UFILL_BLK).

Usa FC 42000..42024, DB 42900/42901, OB 128 y los DB de instancia `T5_Rt_L`, `T5_Ft_L`.

1. *XML → Importar ficheros…* → este .zip (sin descomprimir) → Sí a sobrescribir.
2. Compilar (0 errores), cargar en PLCSIM, RUN.
3. `T5_Resultado`: `Vida` sube, `OK` = `Total` (24), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
| `L_Inc` | Inc |
| `L_Dec` | Dec |
| `L_MIN` | MIN |
| `L_MAX` | MAX |
| `L_LIMIT` | LIMIT |
| `L_Expt` | Expt |
| `L_DECO` | DECO |
| `L_ENCO` | ENCO |
| `L_SEL` | SEL |
| `L_InRange` | IN_RANGE (InRange) |
| `L_OutRange` | OUT_RANGE (OutRange) |
| `L_OK_NOK` | OK / NOT_OK (NOK) |
| `L_R_TRIG` | R_TRIG (DB de instancia T5_Rt_L) |
| `L_F_TRIG` | F_TRIG (DB de instancia T5_Ft_L) |
| `L_RS` | Biestable RS (S_RS) |
| `L_SET_BF` | SET_BF (S_BitfieldCoil) |
| `L_RESET_BF` | [DEDUCIDA] RESET_BF (R_BitfieldCoil) |
| `L_MOVE_BLK` | MOVE_BLK (MoveBlockI) |
| `L_UMOVE_BLK` | UMOVE_BLK (MoveBlockU) |
| `L_FILL_BLK` | FILL_BLK (FillBlockI) |
| `L_UFILL_BLK` | [DEDUCIDA] UFILL_BLK (FillBlockU) |
| `L_MOVE_BLK_VARIANT` | MOVE_BLK_VARIANT |
| `L_NORM_X` | NORM_X (Normalize) |
| `L_SCALE_X` | SCALE_X (Scale_X) |
