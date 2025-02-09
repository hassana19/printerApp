using System;
using System.Windows.Forms;

namespace DauPrinterApp
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
            // WebSocket Port Configuration
            Label lblPort = new Label { Text = "WebSocket Port:", Left = 10, Top = 10, Width = 100 };
            TextBox txtPort = new TextBox { Left = 120, Top = 10, Width = 100, Text = "8080" };

            // Page Width Configuration
            Label lblPageWidth = new Label { Text = "Page Width (inches):", Left = 10, Top = 40, Width = 150 };
            NumericUpDown numericUpDownPageWidth = new NumericUpDown
            {
                Left = 180,
                Top = 40,
                Width = 100,
                Minimum = 1,
                Maximum = 100,
                DecimalPlaces = 2,
                Value = (decimal)Settings.Default.PageWidth // Load page width setting
            };

            // Save Button
            Button btnSave = new Button { Text = "Save", Left = 120, Top = 80, Width = 80 };

            btnSave.Click += (sender, e) =>
            {
                if (int.TryParse(txtPort.Text, out int newPort))
                {
                    app.UpdatePort(newPort); // Update WebSocket Port
                    Settings.Default.PageWidth = (double)numericUpDownPageWidth.Value; // Save page width
                    Settings.Default.Save(); // Save settings

                    MessageBox.Show("Settings updated. Restart WebSocket server.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid port number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Controls.Add(lblPort);
            Controls.Add(txtPort);
            Controls.Add(lblPageWidth);
            Controls.Add(numericUpDownPageWidth);
            Controls.Add(btnSave);
            Text = "Settings";
            Size = new System.Drawing.Size(450, 150); // Adjust size to fit the new controls
        }
    }
}
