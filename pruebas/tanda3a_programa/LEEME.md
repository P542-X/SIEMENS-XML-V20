# Tanda 3A: control del programa, Variant, DB, interrupciones y avisos (TIA V20) — 20/20 TRUE en marcha

20 pruebas en marcha. Sintaxis SD de la exportación real `Aprender_T3`. FC 44000..44019, FB `T7_L_Alarm` + DB 44090, DB 44900/44901, `T7_ArrayDB` (DB de array), `T7_Std` (DB estándar, número **44902** — PEEK/POKE lo usan por número), OB 130 (ciclo), **OB20** (alarma de retardo) y **OB30** (alarma cíclica, 100 ms).

Si el OB20 o el OB30 ya existen en el proyecto, borrarlos antes (o responder Sí a sobrescribir).

1. XML → Importar ficheros… → este .zip → Sí a sobrescribir.
2. Compilar (0 errores). 
3. Cargar en PLCSIM (DB: reinicializar), RUN.
4. `T7_Resultado`: `Vida` sube, `OK` = `Total` (20), `Todo_OK` = TRUE.

| Bool | Prueba |
|---|---|
| `L_JMP` | JMP + LABEL (JumpCoil, Label) |
| `L_JMPN` | JMPN (I_JumpCoil) |
| `L_JMP_LIST` | JMP_LIST (JumpList), K = 1 |
| `L_SWITCH` | SWITCH (Switch), K = 5 → caso 1 |
| `L_RE_TRIGR` | RE_TRIGR |
| `L_GET_ERROR` | GET_ERROR (GetError) y GET_ERR_ID (GetErrorID) sin error |
| `L_WAIT` | WAIT |
| `L_VariantGet` | VariantGet (Variant → Int) |
| `L_VariantPut` | VariantPut (Int → Variant) |
| `L_ArrayDB` | WriteToArrayDB + ReadFromArrayDB |
| `L_ATTR_DB` | ATTR_DB sobre T7_Std (longitud > 0) |
| `L_AIRT` | DIS_AIRT + EN_AIRT |
| `L_SRT_DINT` | SRT_DINT + QRY_DINT: el OB20 se ha ejecutado |
| `L_QRY_CINT` | QRY_CINT y el OB30 se ejecuta |
| `L_Program_Alarm` | Program_Alarm sin error |
| `L_RTM` | RTM modo 0 (leer) |
| `S_PEEK_POKE` | [SCL] POKE + PEEK en un DB de acceso estándar |
| `S_BIG_LITTLE` | [SCL] WRITE_BIG/READ_BIG y WRITE_LITTLE/READ_LITTLE |
| `S_ArrayDB` | [SCL] WriteToArrayDB / ReadFromArrayDB |
| `S_Variant` | [SCL] VariantGet / VariantPut, TypeOf |
