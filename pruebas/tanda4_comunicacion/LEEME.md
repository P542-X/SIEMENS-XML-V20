# Tanda 4: comunicación — SOLO importar y compilar (17/17 compilan)

Sin conexiones configuradas no se pueden ejecutar: la prueba es que **importen y compilen**. Ningún OB llama a estas FC.
Cada FB del sistema tiene su DB de instancia propio (`T9_<instrucción>_DB`, desde fuente). FC 46001..46017.

1. Guardar. XML → Importar ficheros… → este .zip.
2. Pasar el resumen.
3. Después: XML → Deshacer última importación.

| FC | Instrucción |
|---|---|
| `T9_L_TSEND_C` | TSEND_C |
| `T9_L_TRCV_C` | TRCV_C |
| `T9_L_TCON` | TCON |
| `T9_L_TDISCON` | TDISCON |
| `T9_L_TSEND` | TSEND |
| `T9_L_TRCV` | TRCV |
| `T9_L_TUSEND` | TUSEND |
| `T9_L_TURCV` | TURCV (plantillas deducidas de TUSEND) |
| `T9_L_PUT` | PUT |
| `T9_L_GET` | GET |
| `T9_L_BSEND` | BSEND |
| `T9_L_BRCV` | BRCV |
| `T9_L_USEND` | USEND |
| `T9_L_URCV` | URCV |
| `T9_L_MB_CLIENT` | MB_CLIENT |
| `T9_L_MB_SERVER` | MB_SERVER |
| `T9_L_WWW` | WWW |
