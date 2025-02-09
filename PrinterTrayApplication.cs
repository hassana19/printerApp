using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Timers;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PrinterTrayApp
{
    public class PrinterTrayApplication
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private System.Timers.Timer refreshTimer;
        private string selectedPrinter;
        private int webSocketPort = 8080;
        private HttpListener httpListener;
        private string logFilePath = "logs.txt";

        public PrinterTrayApplication()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Print,
                Text = "Printer Tray App",
                Visible = true
            };

            contextMenu = new ContextMenuStrip();
            PopulateMenu();

            notifyIcon.ContextMenuStrip = contextMenu;

            // Timer to refresh printer list
            refreshTimer = new System.Timers.Timer(10000);
            refreshTimer.Elapsed += (s, e) => PopulateMenu();
            refreshTimer.Start();

            // Start WebSocket server
            StartWebSocketServer();
        }

        private void PopulateMenu()
        {
            if (contextMenu.InvokeRequired)
            {
                contextMenu.Invoke(new Action(PopulateMenu));
                return;
            }

            contextMenu.Items.Clear();

            // Configure Option
            ToolStripMenuItem configItem = new ToolStripMenuItem("Settings");
            configItem.Click += (s, e) =>
            {
                new SettingsForm(this).ShowDialog();
            };
            contextMenu.Items.Add(configItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // List installed printers
            foreach (var printer in PrinterSettings.InstalledPrinters.Cast<string>())
            {
                ToolStripMenuItem printerItem = new ToolStripMenuItem(printer);
                printerItem.Click += (s, e) =>
                {
                    selectedPrinter = printer;
                    Log($"Selected Printer: {printer}");
                    MessageBox.Show($"Selected Printer: {printer}", "Printer Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                contextMenu.Items.Add(printerItem);
            }

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit option
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) =>
            {
                refreshTimer.Stop();
                notifyIcon.Visible = false;
                httpListener?.Stop();
                Application.Exit();
            };
            contextMenu.Items.Add(exitItem);
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
