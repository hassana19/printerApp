using System;
using System.Windows.Forms;

namespace PrinterTrayApp
{
    public partial class SettingsForm : Form
    {
        private PrinterTrayApplication app;

        public SettingsForm(PrinterTrayApplication appInstance)
        {
            app = appInstance;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Label lblPort = new Label { Text = "WebSocket Port:", Left = 10, Top = 10, Width = 100 };
            TextBox txtPort = new TextBox { Left = 120, Top = 10, Width = 100, Text = "8080" };
            Button btnSave = new Button { Text = "Save", Left = 120, Top = 40, Width = 80 };

            btnSave.Click += (sender, e) =>
            {
                if (int.TryParse(txtPort.Text, out int newPort))
                {
                    app.UpdatePort(newPort);
                    MessageBox.Show("Port updated. Restart WebSocket server.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid port number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Controls.Add(lblPort);
            Controls.Add(txtPort);
            Controls.Add(btnSave);
            Text = "Settings";
            Size = new System.Drawing.Size(250, 120);
        }
    }
}
