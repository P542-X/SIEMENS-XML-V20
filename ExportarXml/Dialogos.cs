using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
                    dlg.Title = "Selecciona lo que quieres importar (XML, SD, fuentes o un paquete .zip)";
                    dlg.Filter = "Todo lo importable|*.xml;*.scl;*.udt;*.db;*.awl;*.s7dcl;*.s7dcf;*.s7dcs;*.s7dbd;*.json;*.zip|" +
                                 "Paquete exportado (*.zip)|*.zip|" +
                                 "SimaticML (*.xml)|*.xml|" +
                                 "Fuentes externas (*.scl;*.udt;*.db;*.awl)|*.scl;*.udt;*.db;*.awl|" +
                                 "SIMATIC SD (*.s7dcl;*.s7dcf;*.s7dcs;*.s7dbd)|*.s7dcl;*.s7dcf;*.s7dcs;*.s7dbd|" +
                                 "Pantallas WinCC Unified (*.json)|*.json";
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

        /// <summary>Lista con casillas para elegir varias opciones. Devuelve las elegidas, o null si se cancela.</summary>
        public static List<string> ElegirVarios(string titulo, string texto, IList<string> opciones)
        {
            return EnSta(() =>
            {
                using (Form owner = Propietario())
                using (Form f = new Form())
                {
                    f.Text = titulo;
                    f.StartPosition = FormStartPosition.CenterScreen;
                    f.Size = new Size(560, 620);
                    f.MinimizeBox = false;
                    f.MaximizeBox = false;
                    f.ShowInTaskbar = false;
                    f.TopMost = true;

                    Label etiqueta = new Label { Text = texto, Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0) };
                    TextBox buscar = new TextBox { Dock = DockStyle.Top };
                    CheckedListBox lista = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
                    HashSet<string> marcadas = new HashSet<string>(StringComparer.Ordinal);
                    bool rellenando = false;
                    Action rellenar = () =>
                    {
                        rellenando = true;
                        lista.BeginUpdate();
                        lista.Items.Clear();
                        foreach (string o in opciones)
                            if (buscar.Text.Length == 0 || o.IndexOf(buscar.Text, StringComparison.OrdinalIgnoreCase) >= 0)
                                lista.Items.Add(o, marcadas.Contains(o));
                        lista.EndUpdate();
                        rellenando = false;
                    };
                    lista.ItemCheck += (s, e) =>
                    {
                        if (rellenando) return;
                        string o = (string)lista.Items[e.Index];
                        if (e.NewValue == CheckState.Checked) marcadas.Add(o); else marcadas.Remove(o);
                    };
                    buscar.TextChanged += (s, e) => rellenar();

                    FlowLayoutPanel botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
                    Button aceptar = new Button { Text = "Exportar", DialogResult = DialogResult.OK, Width = 90 };
                    Button cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90 };
                    Button ninguna = new Button { Text = "Ninguna", Width = 90 };
                    Button todas = new Button { Text = "Todas (visibles)", Width = 110 };
                    todas.Click += (s, e) => { for (int i = 0; i < lista.Items.Count; i++) lista.SetItemChecked(i, true); };
                    ninguna.Click += (s, e) => { marcadas.Clear(); rellenar(); };
                    botones.Controls.AddRange(new Control[] { aceptar, cancelar, ninguna, todas });
                    f.AcceptButton = aceptar;
                    f.CancelButton = cancelar;

                    Label filtro = new Label { Text = "Buscar:", Dock = DockStyle.Top, Height = 18, Padding = new Padding(8, 2, 0, 0) };
                    f.Controls.Add(lista);
                    f.Controls.Add(buscar);
                    f.Controls.Add(filtro);
                    f.Controls.Add(etiqueta);
                    f.Controls.Add(botones);
                    rellenar();

                    if (f.ShowDialog(owner) != DialogResult.OK) return null;
                    return opciones.Where(marcadas.Contains).ToList();
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
        /// Como Informar, pero sin esperar a que se cierre: TIA recupera el control en cuanto termina el Add-In.
        /// Para el mensaje final de una exportación (si queda detrás de TIA, TIA no se queda esperándolo).
        /// </summary>
        public static void InformarSinEsperar(string texto, string titulo, bool hayErrores)
        {
            HiloUi().BeginInvoke((MethodInvoker)(() =>
            {
                try
                {
                    using (Form owner = Propietario())
                    {
                        owner.Activate();
                        MessageBox.Show(owner, texto, titulo, MessageBoxButtons.OK,
                            hayErrores ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                    }
                }
                catch { }
            }));
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
