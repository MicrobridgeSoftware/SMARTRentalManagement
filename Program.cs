using System;
using System.Linq;
using System.Windows.Forms;
using Telerik.WinControls;

namespace SMARTRentalManagement
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension
            //    (System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            //{
            //    RadMessageBox.Show("An instance of this application is already running \n" +
            //                       "Concurrent execution is not permitted!", Application.ProductName);
            //    System.Diagnostics.Process.GetCurrentProcess().Kill();
            //}

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}