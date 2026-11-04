# Herramientas: generadores de baterías de prueba

| Fichero | Qué genera |
|---|---|
| `simatic.py` | Librería común: bloques SD en KOP (`Bloque`, `rung`, `caja`, `C`, `NC`, `Q`, `S`, `RC`, `cmp`, `move`), XML (`gdb`, `udt`, `m`, `ob`, `red_vida`), escritura (`inicio`, `w`, `empaquetar`) |
| `tanda1_basicas.py` | Tanda 1: 96 pruebas de instrucciones básicas (KOP + SCL) |
| `tanda1b_complemento.py` | Tanda 1b: las 24 cajas con nombre SD distinto del de TIA |
| `tanda2_fecha_texto.py` | Tanda 2: fecha/hora y texto (27) |
| `tanda3a_programa.py` | Tanda 3A: saltos, Variant, DB de array, interrupciones (OB20/OB30), avisos (20) |
| `tanda3b_compilar.py` | Tanda 3B: hardware, diagnóstico, DataLog, recetas (27, solo compilar) |
| `tanda4_comunicacion.py` | Tanda 4: comunicación (17, solo compilar) |
| `tanda4b_com_marcha.py` | Tanda 4B: TCP y Modbus en marcha, el PLC conectado a sí mismo (4) |
| `tanda7_bloques.py` | Tanda 7: OB100/OB121 por XML, VAR CONSTANT, Variant, Array[*] (6) |

## Uso

```
cd herramientas
python tanda2_fecha_texto.py                 # salida/tanda2_fecha_texto/ + .zip
python tanda2_fecha_texto.py C:\otra\carpeta # en otra carpeta
```

El .zip se importa con *XML → Importar ficheros…*. Cada batería trae su `LEEME.md` con los números de bloque que usa y qué comprueba cada `Bool` del DB de resultados.

## Nueva tanda

1. Copiar la tanda más parecida.
2. Cambiar los prefijos (`T6_`…) y el rango de números de bloque.
3. Escribir cada prueba con la sintaxis de una exportación real (ver `GUIA_PRUEBAS.md`).
4. Ejecutarla, importarla y seguir la guía hasta que todo dé TRUE.

Ejemplo mínimo:

```python
from simatic import *
OUT = inicio("salida/mi_prueba")
b = Bloque("P_Suma", 60001, "Prueba de Add")
b.add("Sumar", rung(C("True"), caja("Add", ["Card := 2", "Type := AUTO", "DisableENO := TRUE"],
                                    ['IN1 := "P_Datos".A', "IN2 := 3", 'OUT => "P_Datos".R'])))
b.add("Comprobar", rung(cmp("Eq", "Int", '"P_Datos".R', "8"), Q('"P_Datos".OK')))
b.sd()
w("XML/P_Datos.xml", gdb("P_Datos", 60900, m("A", "Int", "Entrada", "5") + m("R", "Int") + m("OK", "Bool"), "Datos de la prueba"))
w("XML/P_Ciclo.xml", ob("P_Ciclo", 200, ["P_Suma"], "Llama a la prueba"))
print(empaquetar(OUT))
```
