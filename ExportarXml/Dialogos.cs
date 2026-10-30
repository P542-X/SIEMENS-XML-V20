using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ExportarXml
{
    internal enum Respuesta { Si, No, Cancelar }

    /// <summary>
    /// Diálogos de Windows. Se abren en un hilo STA propio con una ventana propietaria
    /// "TopMost" visible (pero transparente) para que salgan delante de TIA Portal.
    /// Solo usa código .NET: el Add-In corre aislado y las llamadas directas a Windows
    /// (P/Invoke, COM) pueden estar bloqueadas.
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
                    dlg.Title = "Selecciona los XML o fuentes a importar";
                    dlg.Filter = "XML y fuentes (*.xml;*.scl;*.udt;*.db;*.awl)|*.xml;*.scl;*.udt;*.db;*.awl|" +
                                 "SimaticML (*.xml)|*.xml|Fuentes externas (*.scl;*.udt;*.db;*.awl)|*.scl;*.udt;*.db;*.awl";
                    dlg.Multiselect = true;
                    dlg.RestoreDirectory = true; // si no, la carpeta elegida queda "en uso" mientras TIA esté abierto
                    return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.FileNames : null;
                }
            });
        }

        private static string _ultimaCarpeta;

        public static string ElegirCarpeta()
        {
            return EnSta(() =>
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                using (Form owner = Propietario())
                {
                    dlg.Description = "Carpeta con los XML o fuentes a importar (se incluyen las subcarpetas)";
                    dlg.ShowNewFolderButton = false;
                    if (Directory.Exists(_ultimaCarpeta)) dlg.SelectedPath = _ultimaCarpeta;
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                    _ultimaCarpeta = dlg.SelectedPath;
                    return dlg.SelectedPath;
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

        /// <summary>
        /// Ventana propietaria mostrada de verdad (un TopMost oculto no cuenta), transparente
        /// y centrada en la pantalla del cursor; los diálogos que cuelgan de ella salen encima.
        /// </summary>
        private static Form Propietario()
        {
            Rectangle pantalla = Screen.FromPoint(Cursor.Position).WorkingArea;
            Form owner = new Form
            {
                TopMost = true,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(1, 1),
                Location = new Point(pantalla.X + pantalla.Width / 2, pantalla.Y + pantalla.Height / 3),
                Opacity = 0
            };
            owner.Show();
            owner.Activate();
            return owner;
        }

        // ---------- Hilo de diálogos ----------
        // Un único hilo STA con su bucle de mensajes, vivo mientras TIA esté abierto. Si cada
        // diálogo se abre en un hilo nuevo que luego muere, el explorador de Windows deja
        // objetos ligados a ese hilo muerto y el siguiente diálogo se queda pillado.

        private static readonly object _cerrojo = new object();
        private static Control _hiloUi;

        private static T EnSta<T>(Func<T> funcion)
        {
            T resultado = default(T);
            Exception error = null;
            HiloUi().Invoke((MethodInvoker)(() =>
            {
                try { resultado = funcion(); }
                catch (Exception ex) { error = ex; }
            }));
            if (error != null) throw error;
            return resultado;
        }

        private static Control HiloUi()
        {
            lock (_cerrojo)
            {
                if (_hiloUi != null) return _hiloUi;
                using (ManualResetEvent listo = new ManualResetEvent(false))
                {
                    Thread hilo = new Thread(() =>
                    {
                        Control c = new Control();
                        GC.KeepAlive(c.Handle); // fuerza a crear la ventana en este hilo
                        _hiloUi = c;
                        listo.Set();
                        Application.Run();
                    });
                    hilo.SetApartmentState(ApartmentState.STA);
                    hilo.IsBackground = true;
                    hilo.Name = "ExportarXml.Dialogos";
                    hilo.Start();
                    listo.WaitOne();
                }
                return _hiloUi;
            }
        }
    }
}
