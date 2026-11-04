# Tanda 7: bloques, interfaz y OB (en marcha) — 6/6 TRUE

| Bool en `T12_Resultado` | Prueba |
|---|---|
| `L_Constante` | VAR CONSTANT en un FB KOP (LIMITE = 10) |
| `L_Variant` | Variant como entrada de un FB KOP + VariantGet |
| `S_ArrayStar` | Array[*] en SCL: suma con LOWER_BOUND/UPPER_BOUND de un Array[0..4] y de un Array[1..3] |
| `L_ArrayStar` | Array[*] en la interfaz de una FC KOP (SD) llamada con dos arrays |
| `L_Startup` | OB100 (Startup) creado por XML: se ha ejecutado al arrancar |
| `L_ProgError` | OB121 (ProgrammingError) creado por XML: atrapa un índice fuera de rango |

`Startup` (OB100) y `ProgrammingError` (OB121) llevan **el mismo nombre que los que creaste a mano** en TESTXML: responder **Sí** a sobrescribir.
El OB121 atrapa a propósito un índice fuera de rango (una vez): la CPU sigue en RUN.

1. XML → Importar ficheros… → este .zip → Sí.
2. Compilar (0 errores). Cargar en PLCSIM (DB: reinicializar), **STOP → RUN** (para que se ejecute el OB100).
3. `T12_Resultado`: `OK` = 6, `Todo_OK` = TRUE.

FB 48010/48011 + DB, FC 48012/48013, DB 48900/48901, OB 132, OB100, OB121.
