using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
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
        private string WordWrap(string text, Font font, SizeF layoutSize, Graphics graphics)
        {
            string[] words = text.Split(' '); string wrappedText = ""; string line = ""; foreach (string word in words)
            {
                string testLine = (line.Length == 0) ? word : line + " " + word; SizeF testSize = graphics.MeasureString(testLine, font, (int)layoutSize.Width); if (testSize.Width > layoutSize.Width)
                {
                    wrappedText += line + "\n"; // New line when text exceeds width
                                   line = word; 
                } else { line = testLine; } 
            } wrappedText += line; // Add the last line
                                  
            return wrappedText;
         }

        private bool IsPdfUrl(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return false;

            try
            {
                Uri uri = new Uri(data);
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
                     
            }
            catch
            {
                return false;
            }
        }
        private void PrintToSelectedPrinter(string data)
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

                float pageWidthInInches = (float)Settings.Default.PageWidth;
                int pageWidth = (int)(pageWidthInInches * 100); // Convert inches to hundredths

                printDocument.DefaultPageSettings.PaperSize = new PaperSize("Custom", pageWidth, 1100);

                // Detect if input is a Base64 image
                if (IsPdfUrl(data))
                {
                    PrintPdfFromUrl(data);
                }
                else if (IsBase64Image(data))
                {
                    Image image = Base64ToImage(data);

                    printDocument.PrintPage += (sender, e) =>
                    {
                        Image resizedImage = ResizeImage(image, e.PageBounds.Width);
                        e.Graphics.DrawImage(resizedImage, new Point(0, 0));
                    };
                }
                else
                {
                    // Standard text printing
                    printDocument.PrintPage += (sender, e) =>
                    {
                        Graphics graphics = e.Graphics;
                        Font font = new Font("Courier New", 9, FontStyle.Regular);
                        SolidBrush brush = new SolidBrush(Color.Black);
                        float x = 5, y = 5;
                        float maxWidth = e.PageBounds.Width - 10;

                        StringFormat format = new StringFormat();
                        format.Alignment = StringAlignment.Near;

                        string wrappedText = WordWrap(data, font, new SizeF(maxWidth, e.PageBounds.Height), graphics);
                        graphics.DrawString(wrappedText, font, brush, new RectangleF(x, y, maxWidth, e.PageBounds.Height), format);
                    };
                }

                printDocument.Print();
                Log("Printing...");
            }
            catch (Exception ex)
            {
                Log($"Printing error: {ex.Message}");
            }
        }

        private void PrintPdf(string filePath)
        {
            try
            {
                string sumatraPath = @"sumatra.exe"; // Change if needed

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = sumatraPath,
                    Arguments = $"-print-to \"{selectedPrinter}\" \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                    Log("PDF printed successfully.");
                }
                else
                {
                    Log("Failed to start SumatraPDF.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error printing PDF: {ex.Message}");
            }
        }

        private void PrintPdfFromUrl(string pdfUrl)
        {
            try
            {
                string tempFilePath = Path.Combine(Path.GetTempPath(), "temp_print.pdf");

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(pdfUrl, tempFilePath);
                    Log($"Downloaded PDF to: {tempFilePath}");
                }

                PrintPdf(tempFilePath);
            }
            catch (Exception ex)
            {
                Log($"Error downloading PDF: {ex.Message}");
            }
        }
        private bool IsBase64Image(string base64)
        {
            base64 = base64.Trim();

            // Check if it starts with "data:image/" (data URL format)
            if (base64.StartsWith("data:image/"))
                return true;

            // If it is a raw Base64 string, check valid encoding pattern
            return (base64.Length % 4 == 0) && Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,2}$");
        }


        private Image Base64ToImage(string base64String)
        {
            try
            {
                // Remove Base64 prefix if it exists (e.g., "data:image/png;base64,")
                if (base64String.Contains(","))
                    base64String = base64String.Split(',')[1];

                byte[] imageBytes = Convert.FromBase64String(base64String);
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    return Image.FromStream(ms);
                }
            }
            catch (Exception ex)
            {
                Log($"Error decoding Base64 image: {ex.Message}");
                return null;
            }
        }

        private Image ResizeImage(Image image, int width)
        {
            if (image == null) return null;

            int newHeight = (int)((double)image.Height / image.Width * width); // Maintain aspect ratio
            Bitmap resized = new Bitmap(image, new Size(width, newHeight));
            return resized;
        }




        /*
        private void PrintToSelectedPrinter(string text)
        {
            if (string.IsNullOrEmpty(selectedPrinter))
            {
                MessageBox.Show("No printer selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                //PrintDocument printDocument = new PrintDocument();
                //printDocument.PrinterSettings.PrinterName = selectedPrinter;
                //double pageWidth = Settings.Default.PageWidth;
                //printDocument.DefaultPageSettings.PaperSize = new PaperSize("Custom", (int)(pageWidth * 100), 1100); // Page width in 100ths of an inch, adjust height if needed

                //printDocument.PrintPage += (sender, e) =>
                //{
                //    e.Graphics.DrawString(text, new Font("Arial", 12), Brushes.Black, 10, 10);
                //};

              

                PrintDocument printDocument = new PrintDocument();
                printDocument.PrinterSettings.PrinterName = selectedPrinter;

                // Get page width from settings and convert to hundredths of an inch (1 inch = 100)
                float pageWidthInInches = (float)Settings.Default.PageWidth;
                int pageWidth = (int)(pageWidthInInches * 100); // Convert inches to hundredths

                printDocument.DefaultPageSettings.PaperSize = new PaperSize("Custom", pageWidth, 1100); // Height is auto-adjusted

                printDocument.PrintPage += (sender, e) =>
                {
                    Graphics graphics = e.Graphics;
                    Font font = new Font("Courier New", 9, FontStyle.Regular); // Use monospaced font for better alignment
                    SolidBrush brush = new SolidBrush(Color.Black);
                    float x = 5;  // Left padding
                    float y = 5;  // Top padding
                    float maxWidth = e.PageBounds.Width - 10; // Set max width

                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Near;

                    // Wrap text to fit within 3-inch width
                    SizeF layoutSize = new SizeF(maxWidth, e.PageBounds.Height);
                    string wrappedText = WordWrap(text, font, layoutSize, graphics);

                    graphics.DrawString(wrappedText, font, brush, new RectangleF(x, y, maxWidth, e.PageBounds.Height), format);
                };
                printDocument.Print();
                Log("Printing...");
            }
            catch (Exception ex)
            {
                Log($"Printing error: {ex.Message}");
            }
        }
        */

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
