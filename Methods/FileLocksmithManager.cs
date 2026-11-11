using ContextMenuManager.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ContextMenuManager.Methods
{
    sealed class FileLocksmithProcessInfo
    {
        public int ProcessId { get; set; }
        public string ApplicationName { get; set; }
        public string ServiceName { get; set; }
        public string ExecutablePath { get; set; }
        public string UserName { get; set; }
        public bool Restartable { get; set; }
        public int ApplicationType { get; set; }
        public uint AppStatus { get; set; }
        public bool IsCritical => ApplicationType == 1000;

        public string TypeName
        {
            get
            {
                switch (ApplicationType)
                {
                    case 1: return "Application";
                    case 2: return "Window";
                    case 3: return "Service";
                    case 4: return "Explorer";
                    case 5: return "Console";
                    case 1000: return "Critical";
                    default: return "Process";
                }
            }
        }
    }

    sealed class FileLocksmithScanResult
    {
        public List<FileLocksmithProcessInfo> Processes { get; } = new List<FileLocksmithProcessInfo>();
        public List<string> Resources { get; } = new List<string>();
        public bool DirectoryScanTruncated { get; set; }
        public int DirectoryCount { get; set; }
    }

    static class FileLocksmithManager
    {
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_MORE_DATA = 234;
        private const int CCH_RM_SESSION_KEY = 32;
        private const int MAX_DIRECTORY_FILES = 4096;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string strServiceShortName;

            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint dwSessionHandle, uint nFiles, string[] rgsFileNames,
            uint nApplications, RM_UNIQUE_PROCESS[] rgApplications, uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        public static bool TryHandleCommandLine(string[] args)
        {
            if (args == null || args.Length == 0 || !string.Equals(args[0], "--file-locksmith", StringComparison.OrdinalIgnoreCase))
                return false;

            List<string> remaining = args.Skip(1).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            bool kill = remaining.RemoveAll(a => string.Equals(a, "--kill", StringComparison.OrdinalIgnoreCase)) > 0;
            bool json = remaining.RemoveAll(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)) > 0;
            bool wait = remaining.RemoveAll(a => string.Equals(a, "--wait", StringComparison.OrdinalIgnoreCase)) > 0;
            bool help = remaining.RemoveAll(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)) > 0;

            if (help || kill || json || wait)
            {
                AttachToParentConsole();
                if (help)
                {
                    WriteHelp();
                    return true;
                }

                if (remaining.Count == 0)
                {
                    Console.Error.WriteLine("File Locksmith: one or more file or directory paths are required.");
                    return true;
                }

                try
                {
                    if (kill)
                    {
                        FileLocksmithScanResult initial = Scan(remaining);
                        foreach (FileLocksmithProcessInfo process in initial.Processes)
                        {
                            if (process.IsCritical || process.ProcessId == Process.GetCurrentProcess().Id) continue;
                            try { KillProcess(process.ProcessId); }
                            catch { }
                        }
                    }

                    if (wait)
                    {
                        while (true)
                        {
                            FileLocksmithScanResult check = Scan(remaining);
                            if (check.Processes.Count == 0) break;
                            Thread.Sleep(500);
                        }
                    }

                    FileLocksmithScanResult result = Scan(remaining);
                    if (json) WriteJson(result);
                    else WriteHumanReadable(result);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("File Locksmith: " + ex.Message);
                }
                return true;
            }

            if (remaining.Count == 0)
            {
                MessageBox.Show("Select one or more files or folders to scan.", "File Locksmith", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            Application.Run(new FileLocksmithForm(remaining.ToArray()));
            return true;
        }


        public static bool IsContextMenuInstalled()
        {
            return RegistryKeyExists(@"*\shell\FileManagement\shell\FileLocksmith")
                && RegistryKeyExists(@"Directory\shell\FolderManagement\shell\FileLocksmith");
        }

        public static void InstallContextMenu()
        {
            InstallParentMenu(@"*\shell\FileManagement", "File Management", "shell32.dll,0");
            InstallLocksmithVerb(@"*\shell\FileManagement\shell\FileLocksmith");

            InstallParentMenu(@"Directory\shell\FolderManagement", "Folder Management", "explorer.exe,0");
            InstallLocksmithVerb(@"Directory\shell\FolderManagement\shell\FileLocksmith");

            RefreshShellMenus();
        }

        public static void UninstallContextMenu()
        {
            DeleteKeyTree(@"*\shell\FileManagement\shell\FileLocksmith");
            DeleteKeyTree(@"Directory\shell\FolderManagement\shell\FileLocksmith");
            CleanupEmptyParent(@"*\shell\FileManagement");
            CleanupEmptyParent(@"Directory\shell\FolderManagement");
            RefreshShellMenus();
        }

        private static void InstallParentMenu(string keyPath, string title, string icon)
        {
            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) return;
                key.SetValue("MUIVerb", title, RegistryValueKind.String);
                key.SetValue("Icon", icon, RegistryValueKind.String);
                key.SetValue("Position", "Middle", RegistryValueKind.String);
                key.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            }
        }

        private static void InstallLocksmithVerb(string keyPath)
        {
            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) return;
                key.SetValue("MUIVerb", "Unlock with File Locksmith", RegistryValueKind.String);
                key.SetValue("Icon", "shell32.dll,-16769", RegistryValueKind.String);
                key.SetValue("NeverDefault", string.Empty, RegistryValueKind.String);
                key.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
            }
            using (RegistryKey command = Registry.ClassesRoot.CreateSubKey(keyPath + @"\command", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (command != null)
                    command.SetValue(string.Empty, BuildShellCommand(), RegistryValueKind.String);
            }
        }

        private static bool RegistryKeyExists(string keyPath)
        {
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(keyPath))
                    return key != null && key.GetValue("LegacyDisable") == null;
            }
            catch { return false; }
        }

        private static void DeleteKeyTree(string keyPath)
        {
            try { Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false); }
            catch { }
        }

        private static void CleanupEmptyParent(string keyPath)
        {
            try
            {
                using (RegistryKey shell = Registry.ClassesRoot.OpenSubKey(keyPath + @"\shell"))
                {
                    if (shell != null && shell.SubKeyCount > 0) return;
                }
                Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false);
            }
            catch { }
        }

        private static void RefreshShellMenus()
        {
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }

        public static string BuildShellCommand()
        {
            return Quote(Application.ExecutablePath) + " --file-locksmith \"%1\"";
        }

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static bool RelaunchAsAdministrator(IEnumerable<string> paths)
        {
            try
            {
                string arguments = "--file-locksmith " + string.Join(" ", paths.Select(Quote));
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                return true;
            }
            catch { return false; }
        }

        public static FileLocksmithScanResult Scan(IEnumerable<string> inputPaths)
        {
            FileLocksmithScanResult result = new FileLocksmithScanResult();
            HashSet<string> resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawPath in inputPaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(rawPath)) continue;
                string path;
                try { path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('"'))); }
                catch { continue; }

                if (File.Exists(path))
                {
                    resources.Add(path);
                    continue;
                }

                if (Directory.Exists(path))
                {
                    result.DirectoryCount++;
                    AddDirectoryFiles(path, resources, result);
                }
            }

            result.Resources.AddRange(resources);
            if (result.Resources.Count == 0) return result;

            uint sessionHandle;
            StringBuilder sessionKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
            int error = RmStartSession(out sessionHandle, 0, sessionKey);
            if (error != ERROR_SUCCESS) throw new InvalidOperationException("Restart Manager could not start. Error " + error + ".");

            try
            {
                string[] files = result.Resources.ToArray();
                error = RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, null, 0, null);
                if (error != ERROR_SUCCESS) throw new InvalidOperationException("Restart Manager could not register the selected resources. Error " + error + ".");

                uint needed = 0;
                uint count = 0;
                uint rebootReasons = 0;
                error = RmGetList(sessionHandle, out needed, ref count, null, ref rebootReasons);
                if (error == ERROR_SUCCESS && needed == 0) return result;
                if (error != ERROR_MORE_DATA && error != ERROR_SUCCESS)
                    throw new InvalidOperationException("Restart Manager could not query locking processes. Error " + error + ".");

                RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[Math.Max(needed, 1)];
                count = (uint)processInfo.Length;
                error = RmGetList(sessionHandle, out needed, ref count, processInfo, ref rebootReasons);
                if (error == ERROR_MORE_DATA && needed > processInfo.Length)
                {
                    processInfo = new RM_PROCESS_INFO[needed];
                    count = needed;
                    error = RmGetList(sessionHandle, out needed, ref count, processInfo, ref rebootReasons);
                }
                if (error != ERROR_SUCCESS)
                    throw new InvalidOperationException("Restart Manager could not return locking processes. Error " + error + ".");

                HashSet<int> seen = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    RM_PROCESS_INFO native = processInfo[i];
                    int pid = native.Process.dwProcessId;
                    if (pid <= 0 || !seen.Add(pid)) continue;

                    FileLocksmithProcessInfo info = new FileLocksmithProcessInfo
                    {
                        ProcessId = pid,
                        ApplicationName = string.IsNullOrWhiteSpace(native.strAppName) ? "Process " + pid : native.strAppName,
                        ServiceName = native.strServiceShortName ?? string.Empty,
                        Restartable = native.bRestartable,
                        ApplicationType = native.ApplicationType,
                        AppStatus = native.AppStatus,
                        ExecutablePath = TryGetProcessPath(pid),
                        UserName = TryGetProcessUser(pid)
                    };
                    result.Processes.Add(info);
                }

                result.Processes.Sort((a, b) => string.Compare(a.ApplicationName, b.ApplicationName, StringComparison.CurrentCultureIgnoreCase));
                return result;
            }
            finally
            {
                RmEndSession(sessionHandle);
            }
        }

        private static void AddDirectoryFiles(string root, HashSet<string> resources, FileLocksmithScanResult result)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0 && resources.Count < MAX_DIRECTORY_FILES)
            {
                string folder = pending.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder))
                    {
                        resources.Add(file);
                        if (resources.Count >= MAX_DIRECTORY_FILES)
                        {
                            result.DirectoryScanTruncated = true;
                            return;
                        }
                    }
                }
                catch { }

                try
                {
                    foreach (string directory in Directory.EnumerateDirectories(folder)) pending.Push(directory);
                }
                catch { }
            }

            if (pending.Count > 0) result.DirectoryScanTruncated = true;
        }

        public static void KillProcess(int processId)
        {
            if (processId <= 0 || processId == Process.GetCurrentProcess().Id) return;
            using (Process process = Process.GetProcessById(processId))
            {
                process.Kill();
                try { process.WaitForExit(2500); }
                catch { }
            }
        }

        public static bool IsProcessAlive(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId)) return !process.HasExited;
            }
            catch { return false; }
        }

        private static string TryGetProcessPath(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return process.MainModule == null ? string.Empty : process.MainModule.FileName;
            }
            catch { return string.Empty; }
        }

        private static string TryGetProcessUser(int processId)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out token)) return string.Empty;
                    using (WindowsIdentity identity = new WindowsIdentity(token)) return identity.Name ?? string.Empty;
                }
            }
            catch { return string.Empty; }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void AttachToParentConsole()
        {
            try
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Stream stdout = Console.OpenStandardOutput();
                Stream stderr = Console.OpenStandardError();
                if (stdout != Stream.Null) Console.SetOut(new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true });
                if (stderr != Stream.Null) Console.SetError(new StreamWriter(stderr, new UTF8Encoding(false)) { AutoFlush = true });
            }
            catch { }
        }

        private static void WriteHelp()
        {
            Console.WriteLine("File Locksmith - ContextMenuManager 3.3.4.4");
            Console.WriteLine("Usage:");
            Console.WriteLine("  ContextMenuManager.exe --file-locksmith <path> [<path> ...]");
            Console.WriteLine("  ContextMenuManager.exe --file-locksmith --json <path> [<path> ...]");
            Console.WriteLine("  ContextMenuManager.exe --file-locksmith --kill <path> [<path> ...]");
            Console.WriteLine("  ContextMenuManager.exe --file-locksmith --wait <path> [<path> ...]");
        }

        private static void WriteHumanReadable(FileLocksmithScanResult result)
        {
            if (result.Processes.Count == 0)
            {
                Console.WriteLine("No locking processes found.");
                return;
            }

            foreach (FileLocksmithProcessInfo process in result.Processes)
            {
                Console.WriteLine("{0} (PID {1}){2}", process.ApplicationName, process.ProcessId,
                    string.IsNullOrWhiteSpace(process.ExecutablePath) ? string.Empty : " - " + process.ExecutablePath);
            }
        }

        private static void WriteJson(FileLocksmithScanResult result)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"processes\":[");
            for (int i = 0; i < result.Processes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                FileLocksmithProcessInfo p = result.Processes[i];
                sb.Append("{\"pid\":").Append(p.ProcessId)
                    .Append(",\"name\":\"").Append(JsonEscape(p.ApplicationName)).Append("\"")
                    .Append(",\"type\":\"").Append(JsonEscape(p.TypeName)).Append("\"")
                    .Append(",\"path\":\"").Append(JsonEscape(p.ExecutablePath)).Append("\"")
                    .Append(",\"user\":\"").Append(JsonEscape(p.UserName)).Append("\"")
                    .Append(",\"restartable\":").Append(p.Restartable ? "true" : "false")
                    .Append(",\"critical\":").Append(p.IsCritical ? "true" : "false").Append('}');
            }
            sb.Append("],\"resourceCount\":").Append(result.Resources.Count)
                .Append(",\"directoryScanTruncated\":").Append(result.DirectoryScanTruncated ? "true" : "false").Append('}');
            Console.WriteLine(sb.ToString());
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
