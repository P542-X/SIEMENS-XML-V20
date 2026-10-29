using System;
using System.Threading;
using System.Windows.Forms;

namespace ExportarXml
{
    internal enum Respuesta { Si, No, Cancelar }

    /// <summary>
    /// Diálogos de Windows. Se abren en un hilo STA propio y con una ventana
    /// "TopMost" como propietaria para que salgan delante de TIA Portal.
    /// </summary>
    internal static class Dialogos
    {
        public static string[] ElegirFicheros()
        {
            return EnSta(() =>
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                using (Form owner = Propietario())
                {
                    dlg.Title = "Selecciona los XML a importar";
                    dlg.Filter = "SimaticML (*.xml)|*.xml";
                    dlg.Multiselect = true;
                    return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.FileNames : null;
                }
            });
        }

        public static string ElegirCarpeta()
        {
            return EnSta(() =>
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                using (Form owner = Propietario())
                {
                    dlg.Description = "Carpeta con los XML a importar (se incluyen las subcarpetas)";
                    dlg.ShowNewFolderButton = false;
                    return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
                }
            });
        }

        public static Respuesta Preguntar(string texto, string titulo)
        {
            return EnSta(() =>
            {
                using (Form owner = Propietario())
                {
                    DialogResult r = MessageBox.Show(owner, texto, titulo,
                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) return Respuesta.Si;
                    if (r == DialogResult.No) return Respuesta.No;
                    return Respuesta.Cancelar;
                }
            });
        }

        public static void Informar(string texto, string titulo, bool hayErrores)
        {
            EnSta(() =>
            {
                using (Form owner = Propietario())
                {
                    MessageBox.Show(owner, texto, titulo, MessageBoxButtons.OK,
                        hayErrores ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
                return true;
            });
        }

        private static Form Propietario()
        {
            return new Form { TopMost = true, ShowInTaskbar = false };
        }

        private static T EnSta<T>(Func<T> funcion)
        {
            T resultado = default(T);
            Exception error = null;
            Thread hilo = new Thread(() =>
            {
                try { resultado = funcion(); }
                catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null) throw error;
            return resultado;
        }
    }
}
