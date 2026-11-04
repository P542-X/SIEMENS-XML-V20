# Tanda 3B: hardware, diagnóstico, avisos, DataLog, recetas — SOLO importar y compilar (27/27 compilan)

Estas instrucciones necesitan hardware real, tarjeta de memoria o avisos configurados: **no se ejecutan** (ningún OB las llama). La prueba es que **importen y compilen**.

1. Guardar. XML → Importar ficheros… → este .zip.
2. Pasar el resumen (con la 2.4.2 dice en qué bloque está cada error).
3. Después: XML → Deshacer última importación (no hace falta dejarlas en el proyecto).

FC 45001..45019 (KOP) y fuentes `T8_S_*` (SCL), DB `T8_*_DB`, `T8_Receta`.

| Bloque | Instrucción |
|---|---|
| `T8_L_INIT_RD` | INIT_RD |
| `T8_L_CREATE_DB` | CREATE_DB |
| `T8_L_DELETE_DB` | DELETE_DB |
| `T8_L_READ_DBL` | READ_DBL |
| `T8_L_WRIT_DBL` | WRIT_DBL |
| `T8_L_ATTACH` | ATTACH |
| `T8_L_DETACH` | DETACH |
| `T8_L_SET_CINT` | SET_CINT |
| `T8_L_CAN_DINT` | CAN_DINT |
| `T8_L_SET_TINT` | SET_TINT |
| `T8_L_CAN_TINT` | CAN_TINT |
| `T8_L_Gen_UsrMsg` | Gen_UsrMsg |
| `T8_L_LED` | LED |
| `T8_L_RD_SINFO` | RD_SINFO |
| `T8_L_DeviceStates` | DeviceStates |
| `T8_L_ModuleStates` | ModuleStates |
| `T8_L_GET_DIAG` | GET_DIAG |
| `T8_L_GEO2LOG` | GEO2LOG |
| `T8_L_LOG2GEO` | LOG2GEO |
| `T8_S_RDREC` + `T8_RDREC_DB` | RDREC (SCL, FB del sistema con DB de instancia) |
| `T8_S_WRREC` + `T8_WRREC_DB` | WRREC (SCL, FB del sistema con DB de instancia) |
| `T8_S_GETIO` + `T8_GETIO_DB` | GETIO (SCL, FB del sistema con DB de instancia) |
| `T8_S_SETIO` + `T8_SETIO_DB` | SETIO (SCL, FB del sistema con DB de instancia) |
| `T8_S_Get_IM_Data` + `T8_Get_IM_Data_DB` | Get_IM_Data (SCL, FB del sistema con DB de instancia) |
| `T8_S_Get_Name` + `T8_Get_Name_DB` | Get_Name (SCL, FB del sistema con DB de instancia) |
| `T8_S_DataLogCreate` + `T8_DataLogCreate_DB` | DataLogCreate (SCL, FB del sistema con DB de instancia) |
| `T8_S_RecipeExport` + `T8_RecipeExport_DB` | RecipeExport (SCL, FB del sistema con DB de instancia) |
