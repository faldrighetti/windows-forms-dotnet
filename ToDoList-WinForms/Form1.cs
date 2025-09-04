using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml.Serialization;


namespace ToDoList_WinForms
{
    public partial class Form1 : Form
    {
        private Button btnAdd;
        private CheckedListBox lstTasks;
        private Button btnDelete;
        private Button btnEdit;
        private Button btnClearCompleted;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblCounts;

        // Persistencia
        private string dataDir;
        private string dataPath;

        public Form1()
        {
            InitializeComponent();

            // Ruta base: %AppData%\ToDoList_WinForms  (fallback a carpeta de la app si no existe)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                appData = Application.StartupPath;

            dataDir = Path.Combine(appData, "ToDoList_WinForms");
            dataPath = Path.Combine(dataDir, "todos.xml");

            SetupUi();

            this.Load += (s, e) => LoadData();
            this.FormClosing += (s, e) =>
            {
                try { SaveData(); } catch { /* no bloquear cierre */ }
            };
        }

        public class TodoItem
        {
            public string Title;
            public bool Done;

            // Ctor sin params requerido por XmlSerializer
            public TodoItem() { }
            public TodoItem(string title, bool done)
            {
                Title = title;
                Done = done;
            }
        }

        private void SetupUi()
        {
            // Botón Agregar
            btnAdd = new Button
            {
                Text = "Agregar",
                Width = 90,
                Top = 12,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Botón Eliminar
            btnDelete = new Button
            {
                Text = "Eliminar",
                Width = 90,
                Top = 12,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Botón Editar
            btnEdit = new Button
            {
                Text = "Editar",
                Width = 90,
                Top = 12,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Botón Limpiar completadas
            btnClearCompleted = new Button
            {
                Text = "Limpiar completadas",
                Width = 140,
                Top = 12,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Fijar alturas (evita problemas de DPI)
            btnAdd.Height = btnDelete.Height = btnEdit.Height = btnClearCompleted.Height = 28;

            // Posicionamiento (derecha → izquierda)
            btnAdd.Left = ClientSize.Width - 12 - btnAdd.Width;
            btnDelete.Left = btnAdd.Left - 8 - btnDelete.Width;
            btnEdit.Left = btnDelete.Left - 8 - btnEdit.Width;
            btnClearCompleted.Left = btnEdit.Left - 8 - btnClearCompleted.Width;

            // Lista (ahora va pegada a los botones, sin textbox)
            lstTasks = new CheckedListBox
            {
                Left = 12,
                Top = btnAdd.Bottom + 12,
                Width = ClientSize.Width - 24,
                Height = ClientSize.Height - (btnAdd.Bottom + 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                CheckOnClick = true
            };

            // Guardar al marcar/destildar + actualizar contadores
            lstTasks.ItemCheck += (s, e) =>
            {
                this.BeginInvoke((Action)delegate
                {
                    try { SaveData(); } catch { }
                    UpdateCounters();
                });
            };

            // Eventos
            btnAdd.Click += (s, e) => AddTaskViaPrompt();     // <-- NUEVO flujo
            btnDelete.Click += (s, e) => DeleteSelected();
            btnEdit.Click += (s, e) => EditSelected();

            // Atajos en la lista
            lstTasks.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.F2) { EditSelected(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Insert || (e.Control && e.KeyCode == Keys.N))
                {
                    AddTaskViaPrompt(); e.SuppressKeyPress = true;
                }
            };

            lstTasks.DoubleClick += (s, e) => EditSelected();

            // Agregar controles
            Controls.Add(btnAdd);
            Controls.Add(btnDelete);
            Controls.Add(btnEdit);
            Controls.Add(btnClearCompleted);
            Controls.Add(lstTasks);

            // Layout en redimensionado
            this.Resize += (s, e) =>
            {
                btnAdd.Left = ClientSize.Width - 12 - btnAdd.Width;
                btnDelete.Left = btnAdd.Left - 8 - btnDelete.Width;
                btnEdit.Left = btnDelete.Left - 8 - btnEdit.Width;
                btnClearCompleted.Left = btnEdit.Left - 8 - btnClearCompleted.Width;

                lstTasks.Left = 12;
                lstTasks.Top = btnAdd.Bottom + 12;
                lstTasks.Width = ClientSize.Width - 24;
                lstTasks.Height = ClientSize.Height - (btnAdd.Bottom + 24);
            };

            // StatusStrip
            statusStrip = new StatusStrip();
            lblCounts = new ToolStripStatusLabel();
            lblCounts.Spring = true;
            lblCounts.TextAlign = ContentAlignment.MiddleLeft;
            statusStrip.Items.Add(lblCounts);
            statusStrip.SizingGrip = false;
            statusStrip.Dock = DockStyle.Bottom;
            Controls.Add(statusStrip);

            UpdateCounters();
        }


        private void UpdateCounters()
        {
            int total = lstTasks.Items.Count;
            int completed = 0;

            for (int i = 0; i < lstTasks.Items.Count; i++)
            {
                if (lstTasks.GetItemChecked(i)) completed++;
            }

            int pending = total - completed;
            lblCounts.Text = "Pendientes: " + pending + "   |   Completadas: " + completed + "   |   Total: " + total;
        }

        private void AddTaskViaPrompt()
        {
            string text = Prompt("Nueva tarea", "");
            if (text == null) return;
            text = text.Trim();
            if (text.Length == 0) return;

            lstTasks.Items.Add(text);
            SaveData();
            UpdateCounters();
        }

        private void DeleteSelected()
        {
            if (lstTasks.SelectedIndices.Count == 0) return;

            // Elimina de abajo hacia arriba por índice
            for (int i = lstTasks.SelectedIndices.Count - 1; i >= 0; i--)
            {
                int idx = lstTasks.SelectedIndices[i];
                lstTasks.Items.RemoveAt(idx);
            }

            SaveData();
            UpdateCounters();
        }

        private void EditSelected()
        {
            if (lstTasks.SelectedIndex < 0) return;

            int idx = lstTasks.SelectedIndex;
            string original = lstTasks.Items[idx]?.ToString() ?? "";

            string edited = Prompt("Editar tarea", original);
            if (edited == null) return;
            edited = edited.Trim();
            if (edited.Length == 0) return;

            lstTasks.Items[idx] = edited;
            lstTasks.SelectedIndex = idx;

            SaveData();
            UpdateCounters();
        }

        private void ClearCompleted()
        {
            for (int i = lstTasks.Items.Count - 1; i >= 0; i--)
            {
                if (lstTasks.GetItemChecked(i))
                    lstTasks.Items.RemoveAt(i);
            }

            SaveData();
            UpdateCounters();
        }

        private static string Prompt(string title, string current)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.Width = 420;
                form.Height = 140;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;

                var txt = new TextBox
                {
                    Left = 12,
                    Top = 12,
                    Width = 380,
                    Text = current,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    Left = 230,
                    Top = 50,
                    Width = 75,
                    DialogResult = DialogResult.OK
                };

                var btnCancel = new Button
                {
                    Text = "Cancelar",
                    Left = 317,
                    Top = 50,
                    Width = 75,
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.AddRange(new Control[] { txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK)
                    return txt.Text;
                else
                    return null;
            }
        }

        private void SaveData()
        {
            try
            {
                if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(dataPath))
                    throw new InvalidOperationException("Rutas de persistencia no inicializadas.");

                if (!Directory.Exists(dataDir))
                    Directory.CreateDirectory(dataDir);

                var items = new System.Collections.Generic.List<TodoItem>();
                for (int i = 0; i < lstTasks.Items.Count; i++)
                {
                    string text = lstTasks.Items[i] != null ? lstTasks.Items[i].ToString() : "";
                    bool done = lstTasks.GetItemChecked(i);
                    items.Add(new TodoItem(text, done));
                }

                var xs = new XmlSerializer(typeof(System.Collections.Generic.List<TodoItem>));
                using (var fs = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    xs.Serialize(fs, items);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo guardar: " + ex.Message);
            }
        }

        private void LoadData()
        {
            try
            {
                if (string.IsNullOrEmpty(dataPath) || !File.Exists(dataPath))
                    return;

                var xs = new XmlSerializer(typeof(System.Collections.Generic.List<TodoItem>));
                using (var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var items = (System.Collections.Generic.List<TodoItem>)xs.Deserialize(fs);

                    lstTasks.BeginUpdate();
                    lstTasks.Items.Clear();

                    foreach (var it in items)
                    {
                        int idx = lstTasks.Items.Add(it.Title ?? "");
                        lstTasks.SetItemChecked(idx, it.Done);
                    }

                    lstTasks.EndUpdate();
                    UpdateCounters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo cargar: " + ex.Message);
            }
        }
    }
}