# Referencia de formatos (comprobados en TIA Portal V20, S7-1500)

Ficheros que **han entrado y compilado** en un PLC real, para copiar su formato al generar bloques.

| Carpeta | Contenido |
|---|---|
| `SD/` | Bloques en SIMATIC SD. **Formato preferido para KOP**: sin versiones, vale para cualquier CPU. |
| `XML/` | SimaticML. Las cajas llevan la versión de la CPU con la que se exportaron. |

| Fichero | Qué enseña |
|---|---|
| `SD/T2_Bits` | Contact, I_Contact, Not, S_Coil, R_Coil, PBox, contactos en serie, variable de tabla PLC |
| `SD/T2_Mover` | Move (1 y 2 salidas), Swap, Convert, S_Move, LEN, Serialize, Deserialize, Calculate, comparaciones en serie |
| `SD/T2_Texto` | CONCAT, VAL_STRG, STRG_VAL, MAX_LEN, Strg_TO_Chars |
| `SD/T2_Tiempo` | TON sobre un IEC_TIMER de un DB global, RD_SYS_T |
| `SD/T2_Llamadas` | Llamada a FC con parámetros y a FB con DB de instancia |
| `SD/T2_FB_KOP` + `T2_FB_KOP_DB` | FB con interfaz y TON multiinstancia; DB de instancia en SD |
| `SD/T3_OR` | OR: dos ramas en paralelo |
| `SD/T3_Ramas` | Una condición con dos salidas |
| `SD/T3_Ret_A` | Llamada a FC con valor de retorno |
| `XML/Serialize_Deserialize_V2.2.xml` | Serialize y Deserialize 2.2 en KOP con `src_type` / `dest_type` (exportado de TIA) |

Para añadir una caja: ponerla en un bloque de prueba, exportarlo con el Add-In y dejar aquí el fichero. La batería completa de pruebas está en `../pruebas/importacion/`.
