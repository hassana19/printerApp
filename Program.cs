using System;
using System.Windows.Forms;

namespace PrinterTrayApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new PrinterTrayApplication();
            Application.Run();
        }
    }
}
