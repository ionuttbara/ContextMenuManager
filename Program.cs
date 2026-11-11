using BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace ContextMenuManager
{
    // Compatible with Windows desktop environments supported by .NET Framework 4.8.
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppString.LoadStrings();

            // File Locksmith and Folder Colorizer are lightweight shell actions and must run
            // before the main manager's elevation/single-instance checks.
            if (FileLocksmithManager.TryHandleCommandLine(args)) return;
            if (FolderColorizerManager.TryHandleCommandLine(args)) return;

            // The main manager still runs elevated, preserving the previous behavior for
            // registry locations that require administrative rights. The executable itself
            // is asInvoker so the lightweight Folder Colorizer command can stay unelevated.
            if (!IsAdministrator())
            {
                RelaunchElevated(args);
                return;
            }

            if (SingleInstance.IsRunning()) return;
            Application.Run(new MainForm());
        }

        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private static void RelaunchElevated(string[] args)
        {
            try
            {
                string arguments = args == null ? string.Empty : string.Join(" ", args.Select(Quote));
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch { }
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
