# -*- coding: utf-8 -*-
"""
simatic.py — utilidades para generar bloques importables con el Add-In SIEMENS-XML-V20 (TIA Portal V20).

Todo lo que hay aquí está COMPROBADO en un S7-1500 (importa, compila y funciona). Formatos:
- SIMATIC SD (KOP): Bloque, red/rung, caja, C/NC/Q/S/RC (contactos y bobinas), cmp, move.
- SimaticML (XML): gdb (DB global), udt, m (miembro de interfaz), ob (OB de ciclo que llama a FC sin parámetros),
  red_vida (red XML que suma 1 a un miembro de un DB).
- w(): escribe un fichero en la carpeta de salida (UTF-8 con BOM y CRLF, como los exporta TIA).

Uso:
    from simatic import *
    OUT = inicio("salida/Mi_Tanda")        # crea XML/, SD/, FUENTES/
    b = Bloque("Mi_FC", 50001, "Título")
    b.add("Título de la red", rung(C("True"), Q('"Mi_DB".Salida')))
    b.sd()                                  # escribe SD/Mi_FC.s7dcl y .s7res
    w("XML/Mi_DB.xml", gdb("Mi_DB", 50900, m("Salida", "Bool"), "Comentario"))
    empaquetar(OUT)                         # OUT.zip listo para XML → Importar ficheros

Sintaxis de cajas: ver ExportarXml/IA/INSTRUCCIONES_IA.md (sección 6). Ejemplos completos: herramientas/tanda*.py.
"""
import os, shutil, zipfile

OUT = None

def inicio(carpeta):
    """Vacía y crea la carpeta de salida con XML/, SD/ y FUENTES/. Devuelve la ruta."""
    global OUT
    OUT = os.path.abspath(carpeta)
    shutil.rmtree(OUT, ignore_errors=True)
    for d in ("XML", "SD", "FUENTES"):
        os.makedirs(os.path.join(OUT, d))
    return OUT

def w(rel, txt, bom=True):
    """Escribe un fichero relativo a la carpeta de salida, en UTF-8 (con BOM por defecto) y CRLF."""
    with open(os.path.join(OUT, rel), "w", encoding="utf-8-sig" if bom else "utf-8", newline="\r\n") as f:
        f.write(txt)

def esc(t):
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")

def empaquetar(carpeta, destino=None):
    """Comprime la carpeta (sin la carpeta raíz) en <carpeta>.zip o en destino. Devuelve la ruta del zip."""
    z = destino or carpeta.rstrip("/\\") + ".zip"
    if os.path.exists(z):
        os.remove(z)
    with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as zf:
        for raiz, _, ficheros in os.walk(carpeta):
            for f in sorted(ficheros):
                p = os.path.join(raiz, f)
                zf.write(p, os.path.relpath(p, carpeta).replace(os.sep, "/"))
    return z

# ------------------------------------------------------------------ SD helpers (formato de las exportaciones reales)
def caja(nombre, plantillas, params, ind="            "):
    p = ""
    if plantillas:
        if len(plantillas) == 1:   # una sola plantilla: en línea, como exporta TIA (Eq{ SrcType := Int }, S_Move{ DisableENO := TRUE })
            p = "{ " + plantillas[0] + " }"
        else:
            p = "{\n" + "; \n".join(ind + "    " + x for x in plantillas) + "\n" + ind + "}"
    a = ", \n".join(ind + "    " + x for x in params)
    return f"{ind}{nombre}{p}(\n{a}\n{ind})\n"
def red(tit, rungs):
    return ("    {\n      S7_Language := \"LAD\";\n      S7_NetworkTitle := \"" + tit + "\"\n    }\n    NETWORK\n" +
            "".join(rungs) + "\n    END_NETWORK\n\n")
def rung(*el, ini="wire#powerrail", fin=""):
    return f"        RUNG {ini}\n" + "".join(e if e.endswith("\n") else "            " + e + "\n" for e in el) + "        END_RUNG" + (" " + fin if fin else "") + "\n"
def C(x): return f"Contact( {x} )"
def NC(x): return f"I_Contact( {x} )"
def Q(x): return f"Coil( {x} )"
def S(x): return f"S_Coil( {x} )"
def RC(x): return f"R_Coil( {x} )"
def cmp(op, t, a, b): return caja(op, [f"SrcType := {t}"], [f"IN1 := {a}", f"IN2 := {b}"])
def move(v, *dst, card=None):
    card = card or len(dst)
    return caja("Move", [f"Card := {card}", "DisableENO := TRUE"], [f"IN := {v}"] + [f"OUT{i+1} => {d}" for i, d in enumerate(dst)])

class Bloque:
    def __init__(s, nombre, numero, titulo):
        s.nombre, s.numero, s.titulo, s.redes, s.textos = nombre, numero, titulo, [], {}
    def add(s, titulo, *rungs):
        mid = f"MLC_{s.numero}_{len(s.redes)+1}"
        s.textos[mid] = titulo; s.redes.append(red(mid, rungs))
    def sd(s, cabecera="FUNCTION", extra=""):
        mid = f"MLC_{s.numero}_0"; s.textos[mid] = s.titulo
        txt = ("{\n    S7_Optimized := \"TRUE\";\n    S7_Language := \"LAD\";\n    S7_Author := \"IA\";\n"
               f"    S7_BlockNumber := \"{s.numero}\";\n    S7_BlockTitle := \"{mid}\"\n}}\n")
        txt += f"{cabecera} \"{s.nombre}\"" + (" : Void" if cabecera == "FUNCTION" else "") + "\n" + extra + "\n"
        txt += "".join(s.redes) + f"END_{cabecera}\n"
        w(f"SD/{s.nombre}.s7dcl", txt)
        res = "<root>\n" + "".join(f'  <Comment Id="{k}">\n    <MultiLanguageText Lang="en-US">{esc(v)}</MultiLanguageText>\n'
                                    f'    <MultiLanguageText Lang="es-ES">{esc(v)}</MultiLanguageText>\n  </Comment>\n' for k, v in s.textos.items()) + "</root>"
        with open(os.path.join(OUT, f"SD/{s.nombre}.s7res"), "w", encoding="utf-8-sig", newline="") as f: f.write(res.replace("\n", "\r\n"))


# ------------------------------------------------------------------ XML (SimaticML)
IFNS = "http://www.siemens.com/automation/Openness/SW/Interface/v5"
def ml(i, comp, text=""):
    t = f"<Text>{esc(text)}</Text>" if text else "<Text />"
    return (f'      <MultilingualText ID="{i:X}" CompositionName="{comp}">\n        <ObjectList>\n'
            f'          <MultilingualTextItem ID="{i+1:X}" CompositionName="Items"><AttributeList><Culture>es-ES</Culture>{t}</AttributeList></MultilingualTextItem>\n'
            f'          <MultilingualTextItem ID="{i+2:X}" CompositionName="Items"><AttributeList><Culture>en-US</Culture>{t}</AttributeList></MultilingualTextItem>\n'
            '        </ObjectList>\n      </MultilingualText>\n')
def m(n, dt, com="", start=None, version=None, db=True):
    a = (f' Version="{version}"' if version else "") + (' Remanence="NonRetain" Accessibility="Public"' if db else "")
    c = f'<Comment><MultiLanguageText Lang="es-ES">{esc(com)}</MultiLanguageText></Comment>' if com else ""
    sv = f"<StartValue>{esc(start)}</StartValue>" if start is not None else ""
    return f'          <Member Name="{n}" Datatype="{esc(dt)}"{a}>{c}{sv}</Member>\n'
def doc(cuerpo): return '<?xml version="1.0" encoding="utf-8"?>\n<Document>\n  <Engineering version="V20" />\n' + cuerpo + '</Document>\n'
def udt(nombre, miembros, com):
    return doc(f'''  <SW.Types.PlcStruct ID="0">
    <AttributeList>
      <Interface><Sections xmlns="{IFNS}">
        <Section Name="None">
{miembros}        </Section>
      </Sections></Interface>
      <Name>{nombre}</Name>
      <Namespace />
    </AttributeList>
    <ObjectList>
{ml(1, "Comment", com)}{ml(4, "Title")}    </ObjectList>
  </SW.Types.PlcStruct>
''')
def gdb(nombre, num, miembros, com, layout="Optimized"):
    return doc(f'''  <SW.Blocks.GlobalDB ID="0">
    <AttributeList>
      <AutoNumber>false</AutoNumber>
      <HeaderAuthor>IA</HeaderAuthor>
      <HeaderFamily>PRUEBA</HeaderFamily>
      <HeaderName />
      <HeaderVersion>0.1</HeaderVersion>
      <Interface><Sections xmlns="{IFNS}">
        <Section Name="Static">
{miembros}        </Section>
      </Sections></Interface>
      <MemoryLayout>{layout}</MemoryLayout>
      <Name>{nombre}</Name>
      <Namespace />
      <Number>{num}</Number>
      <ProgrammingLanguage>DB</ProgrammingLanguage>
    </AttributeList>
    <ObjectList>
{ml(1, "Comment", com)}{ml(4, "Title")}    </ObjectList>
  </SW.Blocks.GlobalDB>
''')


# ------------------------------------------------------------------ OB de ciclo en XML
FLNS = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"
def ob(nombre, num, llamadas, com):
    cus = ""; cid = 4
    for fc in llamadas:
        cus += f'''      <SW.Blocks.CompileUnit ID="{cid:X}" CompositionName="CompileUnits">
        <AttributeList>
          <NetworkSource><FlgNet xmlns="{FLNS}">
  <Parts>
    <Call UId="21">
      <CallInfo Name="{fc}" BlockType="FC" />
    </Call>
  </Parts>
  <Wires>
    <Wire UId="22">
      <Powerrail />
      <NameCon UId="21" Name="en" />
    </Wire>
  </Wires>
</FlgNet></NetworkSource>
          <ProgrammingLanguage>LAD</ProgrammingLanguage>
        </AttributeList>
        <ObjectList>
{ml(cid+1, "Comment")}{ml(cid+4, "Title", fc)}        </ObjectList>
      </SW.Blocks.CompileUnit>
'''
        cid += 7
    return doc(f'''  <SW.Blocks.OB ID="0">
    <AttributeList>
      <AutoNumber>false</AutoNumber>
      <HeaderAuthor>IA</HeaderAuthor>
      <HeaderFamily>PRUEBA</HeaderFamily>
      <HeaderName />
      <HeaderVersion>0.1</HeaderVersion>
      <Interface><Sections xmlns="{IFNS}">
        <Section Name="Input">
          <Member Name="Initial_Call" Datatype="Bool" Informative="true" />
          <Member Name="Remanence" Datatype="Bool" Informative="true" />
        </Section>
        <Section Name="Temp" />
        <Section Name="Constant" />
      </Sections></Interface>
      <IsIECCheckEnabled>false</IsIECCheckEnabled>
      <MemoryLayout>Optimized</MemoryLayout>
      <Name>{nombre}</Name>
      <Namespace />
      <Number>{num}</Number>
      <ProgrammingLanguage>LAD</ProgrammingLanguage>
      <SecondaryType>ProgramCycle</SecondaryType>
      <SetENOAutomatically>false</SetENOAutomatically>
    </AttributeList>
    <ObjectList>
{ml(1, "Comment", com)}{cus}{ml(cid, "Title", nombre)}    </ObjectList>
  </SW.Blocks.OB>
''')

# ------------------------------------------------------------------ Red XML que suma 1 a un miembro de un DB
FL = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"
def red_vida(db, miembro):
    return f'''      <SW.Blocks.CompileUnit ID="FFF0" CompositionName="CompileUnits">
        <AttributeList>
          <NetworkSource><FlgNet xmlns="{FL}">
  <Parts>
    <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="{db}" /><Component Name="{miembro}" /></Symbol></Access>
    <Access Scope="LiteralConstant" UId="22"><Constant><ConstantType>DInt</ConstantType><ConstantValue>1</ConstantValue></Constant></Access>
    <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="{db}" /><Component Name="{miembro}" /></Symbol></Access>
    <Part Name="Add" UId="24" DisabledENO="true">
      <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
      <AutomaticTyped Name="SrcType" />
    </Part>
  </Parts>
  <Wires>
    <Wire UId="25"><Powerrail /><NameCon UId="24" Name="en" /></Wire>
    <Wire UId="26"><IdentCon UId="21" /><NameCon UId="24" Name="in1" /></Wire>
    <Wire UId="27"><IdentCon UId="22" /><NameCon UId="24" Name="in2" /></Wire>
    <Wire UId="28"><NameCon UId="24" Name="out" /><IdentCon UId="23" /></Wire>
  </Wires>
</FlgNet></NetworkSource>
          <ProgrammingLanguage>LAD</ProgrammingLanguage>
        </AttributeList>
        <ObjectList>
          <MultilingualText ID="FFF1" CompositionName="Comment"><ObjectList>
            <MultilingualTextItem ID="FFF2" CompositionName="Items"><AttributeList><Culture>es-ES</Culture><Text /></AttributeList></MultilingualTextItem>
            <MultilingualTextItem ID="FFF3" CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text /></AttributeList></MultilingualTextItem>
          </ObjectList></MultilingualText>
          <MultilingualText ID="FFF4" CompositionName="Title"><ObjectList>
            <MultilingualTextItem ID="FFF5" CompositionName="Items"><AttributeList><Culture>es-ES</Culture><Text>Contador de vida</Text></AttributeList></MultilingualTextItem>
            <MultilingualTextItem ID="FFF6" CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text>Contador de vida</Text></AttributeList></MultilingualTextItem>
          </ObjectList></MultilingualText>
        </ObjectList>
      </SW.Blocks.CompileUnit>
'''
