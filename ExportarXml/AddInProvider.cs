using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace ExportarXml
{
    /// <summary>
    /// Punto de entrada del Add-In: registra el menú contextual en el árbol del proyecto.
    /// </summary>
    public sealed class AddInProvider : ProjectTreeAddInProvider
    {
        private readonly TiaPortal _tia;

        public AddInProvider(TiaPortal tia)
        {
            _tia = tia;
        }

        protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
        {
            yield return new ExportarXmlAddIn(_tia);
        }
    }
}
