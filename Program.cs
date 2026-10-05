using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace Cub
{
    internal static class Program
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [STAThread]
        private static void Main()
        {
            try { SetCurrentProcessExplicitAppUserModelID("NeonBear.Cub"); } catch { }   // own taskbar identity
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool admin;
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                admin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            if (!admin)
            {
                MessageBox.Show("Please run Cub as Administrator.", "Cub",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
