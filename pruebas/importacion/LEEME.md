# Batería de pruebas de importación

49 casos que comprueban que lo generado (XML, SIMATIC SD y fuentes SCL) **importa, compila y funciona** en un S7-1500 con TIA Portal V20. Cada prueba pone a TRUE un Bool si su resultado en el PLC es el esperado.

## Usar

1. Comprimir el contenido de esta carpeta (o seleccionar la carpeta) e importarlo en un PLC de pruebas con *XML → Importar carpeta…*.
2. Compilar y cargar (PLC o PLCSIM).
3. Online:
   - `T2_Resultado`: **`Base_Todo_OK` = TRUE** y `Base_OK` = 41 (`Nuevas_OK` no se usa).
   - `T3_Resultado`: los 8 Bool a TRUE.
4. Quitarlo: *XML → Deshacer última importación*.

## Qué hay

| Formato | Objetos |
|---|---|
| XML | UDT `T2_UDT_Hijo`, `T2_UDT_Padre`; tabla `T2_Variables` (%M950.0, %MW952); DB `T2_Datos` (40921), `T2_Datos_Nuevos` (40922), `T2_Resultado` (40923), `T3_Datos` (40939), `T3_Resultado` (40940); OB de ciclo `T2_Ciclo` (125) y `T2_Ciclo_Nuevos` (126) |
| SD | FC `T2_Bits`, `T2_Comparar`, `T2_Mover`, `T2_Mates`, `T2_Texto`, `T2_Tiempo`, `T2_Llamadas` (40911..40917); FB `T2_FB_KOP` (40919) + DB `T2_FB_KOP_DB` (40920); FC `T3_OR`, `T3_Ramas`, `T3_Mul`, `T3_Div`, `T3_Le`, `T3_Ret_A`, `T3_Ret_B`, `T3_Eq_String` (40931..40938) |
| Fuente SCL | FC `T2_SCL_Suma`, `T2_SCL_Doble`, `T2_SCL_Pruebas`, `T2_Contar_OK`; FB `T2_SCL_FB` + DB `T2_SCL_FB_DB` |

Usa números de bloque 40911..40940, OB 125/126 y %M950/%MW952: comprobar que estén libres en el PLC de pruebas.

## Historial

- Import y compilación comprobados el 30/10/2026 en CPU 1518-3 PN (6ES7 518-3AT10-0AB0, FW V4.0), TIA Portal V20.
- `T3_Mul` y `T3_Div` corregidos tras la primera prueba (todas las entradas constantes → no compilaba con `Type := AUTO`).
- El primer intento de OR (empezar una rama con `RUNG wire#w1`) hacía fallar el bloque entero; la sintaxis buena es la de `T3_OR`.
