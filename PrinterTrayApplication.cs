using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DauPrinterApp
{
    public class PrinterTrayApplication
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem printersMenu;
        private string selectedPrinter;
        private int webSocketPort = 8080;
        private HttpListener httpListener;
        private string logFilePath = "logs.txt";

        public PrinterTrayApplication()
        {
            // Load saved printer from settings
            selectedPrinter = Settings.Default.SelectedPrinter;

            // Setup tray icon and menu
            trayMenu = new ContextMenuStrip();
            printersMenu = new ToolStripMenuItem("Select Printer");

            // Load printer list and settings
            PopulatePrintersMenu();

            trayMenu.Items.Add(printersMenu);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Settings", null, ShowSettings);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon
            {
                Icon = new Icon("icon.ico"),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            // Start WebSocket server
            StartWebSocketServer();

            // Start the refresh timer for printer list
            var refreshTimer = new System.Timers.Timer(10000);
            refreshTimer.Elapsed += (s, e) => PopulatePrintersMenu();
            refreshTimer.Start();
        }

        private void PopulatePrintersMenu()
        {
            printersMenu.DropDownItems.Clear();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                var item = new ToolStripMenuItem(printer)
                {
                    Checked = (printer == selectedPrinter)
                };
                item.Click += (sender, e) => SelectPrinter(printer);
                printersMenu.DropDownItems.Add(item);
            }
        }

        private void SelectPrinter(string printer)
        {
            selectedPrinter = printer;
            Settings.Default.SelectedPrinter = printer;
            Settings.Default.Save(); // Save to application settings
            PopulatePrintersMenu(); // Refresh menu
            Log($"Selected Printer: {printer}");
        }

        private async void StartWebSocketServer()
        {
            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://localhost:{webSocketPort}/");
                httpListener.Start();
                Log($"WebSocket server started on ws://localhost:{webSocketPort}/");

                while (true)
                {
                    HttpListenerContext context = await httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                        _ = HandleWebSocketConnection(wsContext.WebSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"WebSocket Server Error: {ex.Message}");
            }
        }

        private async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            byte[] buffer = new byte[1024];

            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string receivedText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Log($"Received: {receivedText}");

                    if (!string.IsNullOrEmpty(selectedPrinter))
                    {
                        PrintToSelectedPrinter(receivedText);
                    }
                    else
                    {
                        Log("No printer selected.");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
        }

        private void PrintToSelectedPrinter(string text)
        {
            if (string.IsNullOrEmpty(selectedPrinter))
            {
                MessageBox.Show("No printer selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                PrintDocument printDocument = new PrintDocument();
                printDocument.PrinterSettings.PrinterName = selectedPrinter;
                printDocument.PrintPage += (sender, e) =>
                {
                    e.Graphics.DrawString(text, new Font("Arial", 12), Brushes.Black, 10, 10);
                };

                printDocument.Print();
                Log("Printing...");
            }
            catch (Exception ex)
            {
                Log($"Printing error: {ex.Message}");
            }
        }

        private void ShowSettings(object sender, EventArgs e)
        {
            new SettingsForm(this).ShowDialog();
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            httpListener?.Stop();
            Application.Exit();
        }

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch
            {
                MessageBox.Show("Failed to write to log file.", "Log Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void UpdatePort(int newPort)
        {
            webSocketPort = newPort;
            httpListener?.Stop();
            StartWebSocketServer();
            Log($"WebSocket Port Changed to: {newPort}");
        }
    }
}
