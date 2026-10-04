using System;
using System.Security.Principal;
using System.Windows.Forms;

namespace NeonBearDebloat
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool admin;
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                admin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            if (!admin)
            {
                MessageBox.Show("Please run NeonBear Debloater as Administrator.", "NEONBEAR",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
