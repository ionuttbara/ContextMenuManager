using BluePointLilac.Methods;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ContextMenuManager.Methods
{
    sealed class FolderColorizerIconEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string ResourceName { get; set; }

        public override string ToString() => Name ?? Id;
    }

    sealed class FolderColorizerPackage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsBuiltIn { get; set; }
        public string PackageIconPath { get; set; }
        public string PackageIconResourceName { get; set; }
        public string PackageDirectory { get; set; }
        public List<FolderColorizerIconEntry> Icons { get; } = new List<FolderColorizerIconEntry>();

        public override string ToString() => Name ?? Id;
    }

    static class FolderColorizerManager
    {
        // Keep exactly THREE colorizer verbs beside (never inside) Folder Management.
        // Register under Directory\shell, where the known-good built-ins worked.
        // User icons are entries within Mark folder as, never extra top-level verbs.
        private const string ContextMenuShellPath = @"Directory\shell";
        private const string OldContextMenuShellPath = @"Folder\shell";
        private const string ClassesPrefix = @"SOFTWARE\Classes\";
        private const string ContextMenuRootPrefix = "ContextMenuManager.FolderColorizer.";
        private const string GroupedMenuKey = ContextMenuRootPrefix + "Root";
        private const string LegacyDirectContextMenuKeyPath = @"Directory\shell\MarkThisFolder";
        private const string LegacyContextMenuKeyPath = @"Directory\shell\FolderManagement\shell\MarkThisFolder";
        private const string LegacyContextMenuDefinitionRoot = @"Directory\ContextMenus\ContextMenuManager.FolderColorizer";
        private const string MarkerKey = "ContextMenuManagerFolderColorizer";
        private const string ManagedIconPrefix = ".cmm-folder-";
        private const string ManifestName = "manifest.xml";

        private const int SHCNE_ATTRIBUTES = 0x00000800;
        private const int SHCNE_UPDATEDIR = 0x00001000;
        private const int SHCNE_UPDATEITEM = 0x00002000;
        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        private const uint SHCNF_PATHW = 0x0005;
        private const uint SHCNF_FLUSH = 0x1000;
        private const uint SHCNF_FLUSHNOWAIT = 0x2000;
        private const uint FCSM_ICONFILE = 0x00000010;
        private const uint FCS_FORCEWRITE = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFOLDERCUSTOMSETTINGS
        {
            public uint dwSize;
            public uint dwMask;
            public IntPtr pvid;
            public IntPtr pszWebViewTemplate;
            public uint cchWebViewTemplate;
            public IntPtr pszWebViewTemplateVersion;
            public IntPtr pszInfoTip;
            public uint cchInfoTip;
            public IntPtr pclsid;
            public uint dwFlags;
            public IntPtr pszIconFile;
            public uint cchIconFile;
            public int iIconIndex;
            public IntPtr pszLogo;
            public uint cchLogo;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHChangeNotify")]
        private static extern void SHChangeNotifyPath(int wEventId, uint uFlags, string dwItem1, string dwItem2);

        [DllImport("shell32.dll", EntryPoint = "SHChangeNotify")]
        private static extern void SHChangeNotifyIdList(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll")]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "PathMakeSystemFolderW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PathMakeSystemFolder(string pszPath);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "PathUnmakeSystemFolderW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PathUnmakeSystemFolder(string pszPath);

        public static string RootDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Gallery Inc", "ContextMenuManager", "FolderColorizer");

        private static string BuiltInCacheDirectory => Path.Combine(RootDirectory, "BuiltIn");
        private static string CustomPackagesDirectory => Path.Combine(RootDirectory, "Packages");
        private static string MarkDirectory => Path.Combine(RootDirectory, "MarkFolderAs");
        private static string MarkManifest => Path.Combine(MarkDirectory, ManifestName);
        private const string MarkId = "builtin-status";

        // HKCR is a merged view of HKLM/HKCU. Writing explicitly to the machine
        // Classes hive gives the elevated manager a predictable installation target.
        private static RegistryKey OpenClassesHive(RegistryHive hive)
        {
            RegistryView view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(hive, view);
        }

        private static bool ContainsOwnedMenu(RegistryKey hive, string shellPath)
        {
            using (RegistryKey shell = hive.OpenSubKey(ClassesPrefix + shellPath))
                return shell != null && shell.GetSubKeyNames().Any(n =>
                    n.StartsWith(ContextMenuRootPrefix, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsContextMenuInstalled()
        {
            try
            {
                foreach (RegistryHive location in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                {
                    using (RegistryKey hive = OpenClassesHive(location))
                    {
                        if (ContainsOwnedMenu(hive, ContextMenuShellPath) ||
                            ContainsOwnedMenu(hive, OldContextMenuShellPath) ||
                            IsOwnedLegacyKey(hive, LegacyDirectContextMenuKeyPath) ||
                            IsOwnedLegacyKey(hive, LegacyContextMenuKeyPath))
                            return true;
                    }
                }
            }
            catch { /* No write access is needed merely to read the install state. */ }
            return false;
        }

        public static int InstalledPackageCount()
        {
            using (RegistryKey hive = OpenClassesHive(RegistryHive.ClassesRoot))
            using (RegistryKey shell = hive.OpenSubKey(ContextMenuShellPath))
            {
                if (shell == null) return 0;
                return GetBuiltInPackages().Count(p =>
                {
                    using (RegistryKey k = shell.OpenSubKey(MenuKey(p.Id)))
                        return string.Equals(k?.GetValue(MarkerKey) as string, p.Id, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        public static bool IsPackageRegistered(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return false;
            using (RegistryKey hive = OpenClassesHive(RegistryHive.ClassesRoot))
            using (RegistryKey shell = hive.OpenSubKey(ContextMenuShellPath))
            using (RegistryKey key = shell?.OpenSubKey(MenuKey(packageId)))
                return string.Equals(key?.GetValue(MarkerKey) as string, packageId, StringComparison.OrdinalIgnoreCase);
        }

        private static string MenuKey(string id)
        {
            switch (id)
            {
                case MarkId: return ContextMenuRootPrefix + "010.builtin-status";
                case "builtin-colors": return ContextMenuRootPrefix + "020.builtin-colors";
                case "builtin-types": return ContextMenuRootPrefix + "030.builtin-types";
                default: return string.Empty; // Custom top-level packages are no longer supported.
            }
        }

        private static List<FolderColorizerPackage> BuildInstallPlan()
        {
            List<FolderColorizerPackage> packages = GetPackages();
            if (packages.Count != 3 || packages.Any(p => string.IsNullOrEmpty(MenuKey(p.Id))))
                throw new InvalidDataException("The Colorizer must contain exactly the three built-in categories.");
            foreach (FolderColorizerPackage package in packages)
            {
                if (package.Icons.Count == 0)
                    throw new InvalidDataException("The category '" + package.Name + "' is empty.");
                foreach (FolderColorizerIconEntry icon in package.Icons)
                    if (icon == null || (!(!string.IsNullOrWhiteSpace(icon.ResourceName) &&
                        Assembly.GetExecutingAssembly().GetManifestResourceInfo(icon.ResourceName) != null) &&
                        (string.IsNullOrWhiteSpace(icon.FilePath) || !File.Exists(icon.FilePath))))
                        throw new FileNotFoundException("Missing icon in " + package.Name + ": " + icon?.Name);
            }
            // First 13 + More icons + Reset = at most 15 children in Mark folder as.
            // More icons can hold 14 direct commands or 14 pages of 14 icons.
            if (packages[0].Icons.Count > 13 + 14 * 14)
                throw new InvalidDataException("Mark folder as supports a maximum of 209 icons.");
            return packages;
        }

        private static void RegisterIconVerb(RegistryKey hive, string shellPath, int number,
            FolderColorizerPackage package, FolderColorizerIconEntry icon)
        {
            string path = shellPath + @"\" + number.ToString("D3") + "_" + SanitizeRegistryKeyName(icon.Id);
            using (RegistryKey item = hive.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (item == null) throw new IOException("Could not create " + path);
                item.SetValue("MUIVerb", icon.Name, RegistryValueKind.String);
                if (File.Exists(icon.FilePath)) item.SetValue("Icon", icon.FilePath, RegistryValueKind.String);
            }
            using (RegistryKey cmd = hive.CreateSubKey(path + @"\command", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (cmd == null) throw new IOException("Could not create command for " + icon.Name);
                cmd.SetValue(string.Empty, BuildCommandLine("apply", package.Id, icon.Id), RegistryValueKind.String);
            }
        }

        private static void RegisterGroup(RegistryKey hive, string path, string name)
        {
            using (RegistryKey key = hive.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) throw new IOException("Could not create " + path);
                key.SetValue("MUIVerb", name, RegistryValueKind.String);
                key.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            }
            using (RegistryKey shell = hive.CreateSubKey(path + @"\shell", RegistryKeyPermissionCheck.ReadWriteSubTree))
                if (shell == null) throw new IOException("Could not create " + path + @"\shell");
        }

        private static void WriteCategory(RegistryKey hive, FolderColorizerPackage package)
        {
            string root = ClassesPrefix + ContextMenuShellPath + @"\" + MenuKey(package.Id);
            hive.DeleteSubKeyTree(root, false);
            using (RegistryKey key = hive.CreateSubKey(root, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) throw new IOException("Could not create " + root);
                key.SetValue("MUIVerb", package.Name, RegistryValueKind.String);
                key.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
                key.SetValue("Position", "Middle", RegistryValueKind.String);
                key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
                key.SetValue(MarkerKey, package.Id, RegistryValueKind.String);
                key.SetValue("Icon", GetPackageIconForRegistry(package), RegistryValueKind.String);
            }
            string shell = root + @"\shell";
            List<FolderColorizerIconEntry> icons = package.Icons;
            bool mark = package.Id == MarkId;
            bool overflow = mark && icons.Count > 14;
            int directCount = overflow ? 13 : icons.Count;
            for (int i = 0; i < directCount; i++) RegisterIconVerb(hive, shell, i + 1, package, icons[i]);
            if (overflow)
            {
                string more = shell + @"\014_MoreIcons";
                RegisterGroup(hive, more, "More icons...");
                List<FolderColorizerIconEntry> extra = icons.Skip(directCount).ToList();
                if (extra.Count <= 14)
                {
                    for (int i = 0; i < extra.Count; i++)
                        RegisterIconVerb(hive, more + @"\shell", i + 1, package, extra[i]);
                }
                else
                {
                    int pages = (extra.Count + 13) / 14;
                    for (int page = 0; page < pages; page++)
                    {
                        string pagePath = more + @"\shell\" + (page + 1).ToString("D3") + "_Page";
                        RegisterGroup(hive, pagePath, "Icons " + (page + 1) + "/" + pages);
                        List<FolderColorizerIconEntry> chunk = extra.Skip(page * 14).Take(14).ToList();
                        for (int i = 0; i < chunk.Count; i++)
                            RegisterIconVerb(hive, pagePath + @"\shell", i + 1, package, chunk[i]);
                    }
                }
            }
            if (mark)
            {
                string resetPath = shell + @"\999_Reset";
                using (RegistryKey reset = hive.CreateSubKey(resetPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (reset == null) throw new IOException("Could not create " + resetPath);
                    reset.SetValue("MUIVerb", UiLanguage.Text("FolderColorizerReset"), RegistryValueKind.String);
                    string resetIcon = Path.Combine(BuiltInCacheDirectory, "Pack_01", "empty.ico");
                    if (File.Exists(resetIcon)) reset.SetValue("Icon", resetIcon, RegistryValueKind.String);
                }
                using (RegistryKey cmd = hive.CreateSubKey(resetPath + @"\command", RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (cmd == null) throw new IOException("Could not create reset command");
                    cmd.SetValue(string.Empty, BuildCommandLine("reset", null, null), RegistryValueKind.String);
                }
            }
        }

        private static List<string> ReadCommands(RegistryKey parent, int depth = 0)
        {
            if (depth > 5) throw new InvalidDataException("Unexpected menu nesting level.");
            List<string> result = new List<string>();
            using (RegistryKey shell = parent?.OpenSubKey("shell"))
            {
                if (shell == null) return result;
                foreach (string name in shell.GetSubKeyNames())
                using (RegistryKey child = shell.OpenSubKey(name))
                {
                    using (RegistryKey command = child?.OpenSubKey("command"))
                    {
                        string line = command?.GetValue(string.Empty) as string;
                        if (!string.IsNullOrWhiteSpace(line)) result.Add(line);
                    }
                    result.AddRange(ReadCommands(child, depth + 1));
                }
            }
            return result;
        }

        private static void VerifyMenuLayout(RegistryKey root, List<FolderColorizerPackage> packages)
        {
            using (RegistryKey shell = root.OpenSubKey(ContextMenuShellPath))
            {
                if (shell == null) throw new IOException("Explorer's Directory shell registry key is missing.");
                string[] owned = shell.GetSubKeyNames().Where(n => n.StartsWith(
                    ContextMenuRootPrefix, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (owned.Length != 3) throw new IOException("Expected exactly three Folder Colorizer menus; found " + owned.Length);
                foreach (FolderColorizerPackage package in packages)
                using (RegistryKey menu = shell.OpenSubKey(MenuKey(package.Id)))
                {
                    if (menu == null || !string.Equals(menu.GetValue(MarkerKey) as string, package.Id,
                        StringComparison.OrdinalIgnoreCase) || menu.GetValue("SubCommands") == null)
                        throw new IOException("Missing or invalid Colorizer menu: " + package.Name);
                    List<string> actual = ReadCommands(menu);
                    List<string> expected = package.Icons.Select(i => BuildCommandLine("apply", package.Id, i.Id)).ToList();
                    if (package.Id == MarkId) expected.Add(BuildCommandLine("reset", null, null));
                    if (actual.Count != expected.Count || expected.Any(line => actual.Count(v => v == line) != 1))
                        throw new IOException("Incomplete Colorizer commands: " + package.Name);
                }
            }
        }

        public static void InstallContextMenu()
        {
            EnsureBuiltInPacksExtracted();
            // Old custom manifests are kept as backups. Import their icons into ONE
            // editable Mark folder as menu, never a fourth Explorer root cascade.
            MigrateLegacyPackages();
            List<FolderColorizerPackage> packages = BuildInstallPlan();
            using (RegistryKey machine = OpenClassesHive(RegistryHive.LocalMachine))
            {
                // Only the three owned keys are recreated. FolderManagement and
                // other vendors' Directory\shell keys are never altered.
                foreach (FolderColorizerPackage package in packages) WriteCategory(machine, package);
                HashSet<string> expected = new HashSet<string>(packages.Select(p => MenuKey(p.Id)), StringComparer.OrdinalIgnoreCase);
                RemoveOwnedPackageKeys(machine, ContextMenuShellPath, expected);
                RemoveOwnedPackageKeys(machine, OldContextMenuShellPath, null);
                RemoveOwnedLegacyKeys(machine);
            }
            using (RegistryKey user = OpenClassesHive(RegistryHive.CurrentUser))
            {
                RemoveOwnedPackageKeys(user, ContextMenuShellPath, null);
                RemoveOwnedPackageKeys(user, OldContextMenuShellPath, null);
                RemoveOwnedLegacyKeys(user);
            }
            using (RegistryKey merged = OpenClassesHive(RegistryHive.ClassesRoot))
                VerifyMenuLayout(merged, packages);
            NotifyShellContextMenuChanged();
        }

        public static void UninstallContextMenu()
        {
            // Uninstall Folder Colorizer only. Do not remove sibling Folder
            // Management, File Locksmith or any third-party MarkThisFolder verb.
            using (RegistryKey machine = OpenClassesHive(RegistryHive.LocalMachine))
            {
                RemoveOwnedPackageKeys(machine, ContextMenuShellPath, null);
                RemoveOwnedPackageKeys(machine, OldContextMenuShellPath, null);
                RemoveOwnedLegacyKeys(machine);
            }
            using (RegistryKey user = OpenClassesHive(RegistryHive.CurrentUser))
            {
                RemoveOwnedPackageKeys(user, ContextMenuShellPath, null);
                RemoveOwnedPackageKeys(user, OldContextMenuShellPath, null);
                RemoveOwnedLegacyKeys(user);
            }
            NotifyShellContextMenuChanged();
        }

        private static void RemoveOwnedPackageKeys(RegistryKey hive, string shellPath, HashSet<string> keep)
        {
            string path = ClassesPrefix + shellPath;
            using (RegistryKey shell = hive.OpenSubKey(path))
            {
                if (shell == null) return;
                foreach (string name in shell.GetSubKeyNames())
                {
                    if (!name.StartsWith(ContextMenuRootPrefix, StringComparison.OrdinalIgnoreCase) ||
                        (keep != null && keep.Contains(name))) continue;
                    hive.DeleteSubKeyTree(path + @"\" + name, false);
                }
            }
        }

        // Prior builds used these generic verb names. Delete only when the
        // registration can positively be identified as one of OUR commands.
        private static bool IsOwnedLegacyKey(RegistryKey hive, string path)
        {
            using (RegistryKey key = hive.OpenSubKey(ClassesPrefix + path))
                return ContainsOurCommand(key, 4);
        }

        private static bool ContainsOurCommand(RegistryKey key, int depth)
        {
            if (key == null) return false;
            if (key.GetValue(MarkerKey) != null) return true;
            foreach (string valueName in key.GetValueNames())
            {
                string value = key.GetValue(valueName) as string;
                if (value != null && value.IndexOf("--folder-colorizer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            if (depth == 0) return false;
            foreach (string childName in key.GetSubKeyNames())
                using (RegistryKey child = key.OpenSubKey(childName))
                    if (ContainsOurCommand(child, depth - 1)) return true;
            return false;
        }

        private static void RemoveOwnedLegacyKeys(RegistryKey hive)
        {
            foreach (string path in new[] { LegacyDirectContextMenuKeyPath, LegacyContextMenuKeyPath })
                if (IsOwnedLegacyKey(hive, path))
                    hive.DeleteSubKeyTree(ClassesPrefix + path, false);
            // Specific old resource namespace belongs exclusively to this app.
            hive.DeleteSubKeyTree(ClassesPrefix + LegacyContextMenuDefinitionRoot, false);
        }

        internal static void LogInstallFailure(string operation, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(RootDirectory);
                File.AppendAllText(Path.Combine(RootDirectory, "install-errors.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + operation + "\r\n" +
                    ex + "\r\n\r\n", Encoding.UTF8);
            }
            catch { /* Logging must never prevent the program from starting. */ }
        }

        public static void RefreshContextMenuIfInstalled()
        {
            if (!IsContextMenuInstalled()) return;
            InstallContextMenu();
        }

        private static string BuildCommandLine(string action, string packageId, string iconId)
        {
            string exe = Application.ExecutablePath;
            if (string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase))
                return Quote(exe) + " --folder-colorizer reset \"%1\"";
            return Quote(exe) + " --folder-colorizer apply " + Quote(packageId) + " " + Quote(iconId) + " \"%1\"";
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string SanitizeRegistryKeyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item";
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            return sb.ToString();
        }

        public static bool TryHandleCommandLine(string[] args)
        {
            if (args == null || args.Length < 2) return false;
            if (!string.Equals(args[0], "--folder-colorizer", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                string action = args[1];
                if (string.Equals(action, "repair", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsRunningAsAdministrator()) TryRelaunchElevated(args);
                    else
                    {
                        InstallContextMenu();
                        MessageBox.Show("Folder Colorizer repaired: " + InstalledPackageCount() +
                            " package menus registered. Check the classic Explorer context menu.",
                            AppString.General.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return true;
                }
                if (string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
                {
                    ResetFolder(args[2]);
                    return true;
                }
                if (string.Equals(action, "apply", StringComparison.OrdinalIgnoreCase) && args.Length >= 5)
                {
                    ApplyFolderIcon(args[4], args[2], args[3]);
                    return true;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (!IsRunningAsAdministrator()) TryRelaunchElevated(args);
                else MessageBox.Show(ex.Message, AppString.General.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return true;
            }
            catch (SecurityException ex)
            {
                if (!IsRunningAsAdministrator()) TryRelaunchElevated(args);
                else MessageBox.Show(ex.Message, AppString.General.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return true;
            }
            catch (Exception ex)
            {
                LogInstallFailure("Folder Colorizer shell action", ex);
                MessageBox.Show(ex.Message, AppString.General.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return true;
            }
            return true;
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static void TryRelaunchElevated(string[] args)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = string.Join(" ", args.Select(Quote)),
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
            }
            catch { }
        }

        public static void ApplyFolderIcon(string folderPath, string packageId, string iconId)
        {
            folderPath = NormalizeFolderPath(folderPath);
            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);

            FolderColorizerPackage package = GetPackages().FirstOrDefault(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
            FolderColorizerIconEntry icon = package?.Icons.FirstOrDefault(i => string.Equals(i.Id, iconId, StringComparison.OrdinalIgnoreCase));
            if (icon == null) throw new FileNotFoundException(UiLanguage.Text("FolderColorizerIconMissing"));

            byte[] iconBytes = GetIconBytes(icon);
            if (iconBytes == null || iconBytes.Length == 0) throw new InvalidDataException(UiLanguage.Text("FolderColorizerIconMissing"));

            // Keep the previous managed icon alive until Explorer has accepted the new
            // desktop.ini value. Deleting the old icon first can leave Explorer displaying
            // a blank/default icon from its folder cache until explorer.exe is restarted.
            string randomCode = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            string iconFileName = ManagedIconPrefix + randomCode + ".ico";
            string iconPath = Path.Combine(folderPath, iconFileName);
            string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

            File.WriteAllBytes(iconPath, iconBytes);
            File.SetAttributes(iconPath, FileAttributes.Hidden | FileAttributes.System);

            // Use the Shell's supported folder-customization API as well as our own marker.
            // FCS_FORCEWRITE is important when replacing one custom icon with another.
            try { PathMakeSystemFolder(folderPath); } catch { }
            ForceWriteFolderIconWithShell(folderPath, iconFileName);

            IniWriter ini = new IniWriter(desktopIniPath);
            ini.SetValue(".ShellClassInfo", "IconResource", iconFileName + ",0");
            ini.SetValue(".ShellClassInfo", "IconFile", iconFileName);
            ini.SetValue(".ShellClassInfo", "IconIndex", "0");
            ini.SetValue(".ShellClassInfo", MarkerKey, "1");
            File.SetAttributes(desktopIniPath, FileAttributes.Hidden | FileAttributes.System);

            try { PathMakeSystemFolder(folderPath); }
            catch
            {
                DirectoryInfo directory = new DirectoryInfo(folderPath);
                directory.Attributes = directory.Attributes | FileAttributes.ReadOnly;
            }

            RefreshFolder(folderPath);

            // The new resource has now been announced to Explorer. Old randomized icons can
            // be removed without creating the disappearing-icon race seen in 3.3.4.3.
            RemoveManagedIconFiles(folderPath, iconFileName);
            RefreshFolder(folderPath);
        }

        public static void ResetFolder(string folderPath)
        {
            folderPath = NormalizeFolderPath(folderPath);
            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);

            string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
            bool ownedCustomization = false;
            if (File.Exists(desktopIniPath))
            {
                IniWriter ini = new IniWriter(desktopIniPath);
                string marker = ini.GetValue(".ShellClassInfo", MarkerKey);
                if (string.Equals(marker, "1", StringComparison.OrdinalIgnoreCase))
                {
                    ownedCustomization = true;
                    ini.DeleteKey(".ShellClassInfo", "IconResource");
                    ini.DeleteKey(".ShellClassInfo", "IconFile");
                    ini.DeleteKey(".ShellClassInfo", "IconIndex");
                    ini.DeleteKey(".ShellClassInfo", MarkerKey);
                    TryDeleteEmptyDesktopIni(desktopIniPath);
                }
            }

            RemoveManagedIconFiles(folderPath, null);
            try
            {
                if (ownedCustomization && !File.Exists(desktopIniPath))
                {
                    if (!PathUnmakeSystemFolder(folderPath))
                    {
                        DirectoryInfo directory = new DirectoryInfo(folderPath);
                        directory.Attributes = directory.Attributes & ~FileAttributes.ReadOnly;
                    }
                }
            }
            catch { }
            RefreshFolder(folderPath);
        }

        private static void TryDeleteEmptyDesktopIni(string desktopIniPath)
        {
            try
            {
                if (!File.Exists(desktopIniPath)) return;
                FileAttributes attrs = File.GetAttributes(desktopIniPath);
                File.SetAttributes(desktopIniPath, FileAttributes.Normal);
                string[] meaningful = File.ReadAllLines(desktopIniPath)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(";") && !s.StartsWith("#"))
                    .ToArray();
                bool onlyEmptyShellClassInfo = meaningful.Length == 0 ||
                    (meaningful.Length == 1 && string.Equals(meaningful[0], "[.ShellClassInfo]", StringComparison.OrdinalIgnoreCase));
                if (onlyEmptyShellClassInfo) File.Delete(desktopIniPath);
                else File.SetAttributes(desktopIniPath, attrs | FileAttributes.Hidden | FileAttributes.System);
            }
            catch { }
        }

        private static void RemoveManagedIconFiles(string folderPath, string keepFileName)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folderPath, ManagedIconPrefix + "*.ico", SearchOption.TopDirectoryOnly))
                {
                    if (!string.IsNullOrWhiteSpace(keepFileName) &&
                        string.Equals(Path.GetFileName(file), keepFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ForceWriteFolderIconWithShell(string folderPath, string iconFileName)
        {
            IntPtr iconPtr = IntPtr.Zero;
            try
            {
                iconPtr = Marshal.StringToCoTaskMemUni(iconFileName);
                SHFOLDERCUSTOMSETTINGS settings = new SHFOLDERCUSTOMSETTINGS
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(SHFOLDERCUSTOMSETTINGS)),
                    dwMask = FCSM_ICONFILE,
                    pszIconFile = iconPtr,
                    cchIconFile = 0,
                    iIconIndex = 0
                };
                SHGetSetFolderCustomSettings(ref settings, folderPath, FCS_FORCEWRITE);
            }
            catch { }
            finally
            {
                if (iconPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(iconPtr);
            }
        }

        private static string NormalizeFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return folderPath;
            string value = Environment.ExpandEnvironmentVariables(folderPath.Trim().Trim('"'));
            return Path.GetFullPath(value);
        }

        private static void RefreshFolder(string folderPath)
        {
            try
            {
                string parent = null;
                try { parent = Directory.GetParent(folderPath)?.FullName; }
                catch { }

                // Notify by both path and PIDL. Explorer can keep a folder icon cached against
                // the PIDL even after SHCNE_UPDATEITEM(path), which is why 3.3.4.3 could update
                // once and then require explorer.exe to be restarted.
                uint pathFlags = SHCNF_PATHW | SHCNF_FLUSH;
                SHChangeNotifyPath(SHCNE_ATTRIBUTES, pathFlags, folderPath, null);
                SHChangeNotifyPath(SHCNE_UPDATEITEM, pathFlags, folderPath, null);
                SHChangeNotifyPath(SHCNE_UPDATEDIR, pathFlags, folderPath, null);
                NotifyPathByPidl(folderPath, SHCNE_ATTRIBUTES);
                NotifyPathByPidl(folderPath, SHCNE_UPDATEITEM);
                NotifyPathByPidl(folderPath, SHCNE_UPDATEDIR);

                if (!string.IsNullOrWhiteSpace(parent))
                {
                    SHChangeNotifyPath(SHCNE_UPDATEDIR, pathFlags, parent, null);
                    NotifyPathByPidl(parent, SHCNE_UPDATEDIR);
                }

                // Ask already-open Explorer folder views to re-enumerate immediately. This is
                // a targeted UI refresh and does not terminate/restart explorer.exe.
                RefreshOpenExplorerWindows();
                RunIconCacheRefresh();
                Thread.Sleep(80);

                // Repeat after ie4uinit has invalidated the icon cache. FLUSHNOWAIT avoids
                // making the shell command feel stuck if Explorer is busy.
                uint finalFlags = SHCNF_PATHW | SHCNF_FLUSHNOWAIT;
                SHChangeNotifyPath(SHCNE_ATTRIBUTES | SHCNE_UPDATEITEM, finalFlags, folderPath, null);
                if (!string.IsNullOrWhiteSpace(parent))
                    SHChangeNotifyPath(SHCNE_UPDATEDIR, finalFlags, parent, null);
                NotifyPathByPidl(folderPath, SHCNE_UPDATEITEM);
                RefreshOpenExplorerWindows();
            }
            catch { }
        }

        private static void NotifyPathByPidl(string path, int eventId)
        {
            IntPtr pidl = IntPtr.Zero;
            try
            {
                pidl = ILCreateFromPathW(path);
                if (pidl != IntPtr.Zero)
                    SHChangeNotifyIdList(eventId, SHCNF_IDLIST | SHCNF_FLUSH, pidl, IntPtr.Zero);
            }
            catch { }
            finally
            {
                if (pidl != IntPtr.Zero) ILFree(pidl);
            }
        }

        private static void RefreshOpenExplorerWindows()
        {
            object shell = null;
            object windows = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return;
                shell = Activator.CreateInstance(shellType);
                windows = shellType.InvokeMember("Windows", BindingFlags.InvokeMethod, null, shell, null);
                if (windows == null) return;

                object countValue = windows.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, windows, null);
                int count = Convert.ToInt32(countValue);
                for (int i = 0; i < count; i++)
                {
                    object window = null;
                    object document = null;
                    try
                    {
                        window = windows.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, windows, new object[] { i });
                        if (window == null) continue;
                        document = window.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, window, null);
                        if (document == null) continue;
                        document.GetType().InvokeMember("Refresh", BindingFlags.InvokeMethod, null, document, null);
                    }
                    catch { }
                    finally
                    {
                        try { if (document != null && Marshal.IsComObject(document)) Marshal.FinalReleaseComObject(document); } catch { }
                        try { if (window != null && Marshal.IsComObject(window)) Marshal.FinalReleaseComObject(window); } catch { }
                    }
                }
            }
            catch { }
            finally
            {
                try { if (windows != null && Marshal.IsComObject(windows)) Marshal.FinalReleaseComObject(windows); } catch { }
                try { if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); } catch { }
            }
        }

        private static void NotifyShellContextMenuChanged()
        {
            try
            {
                SHChangeNotifyIdList(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        private static void RunIconCacheRefresh()
        {
            try
            {
                string tool = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ie4uinit.exe");
                if (!File.Exists(tool)) return;

                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = tool,
                    Arguments = "-show",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    process?.WaitForExit(2500);
                }
            }
            catch { }
        }

        private static byte[] GetIconBytes(FolderColorizerIconEntry icon)
        {
            if (icon == null) return null;
            if (!string.IsNullOrWhiteSpace(icon.ResourceName))
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(icon.ResourceName))
                {
                    if (stream == null) return null;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(icon.FilePath) && File.Exists(icon.FilePath))
                return File.ReadAllBytes(icon.FilePath);
            return null;
        }

        public static List<FolderColorizerPackage> GetPackages()
        {
            EnsureBuiltInPacksExtracted();
            List<FolderColorizerPackage> result = GetBuiltInPackages();
            ApplyMarkCustomization(result[0]);
            return result;
        }

        private static List<FolderColorizerPackage> GetBuiltInPackages()
        {
            return new List<FolderColorizerPackage>
            {
                CreateBuiltInPackage(
                    "builtin-status", "Mark folder as...", "Pack_03",
                    new [] { "High Priority", "Normal Priority", "Low Priority", "Planned Work", "Half-Done Work", "Finished Work", "Work/Assignment", "Private/Locker", "Important Files", "Temporary Files", "Finalized Folder Updates", "Queued Folder Updates", "Folder Updates", "Another type..." }),
                CreateBuiltInPackage(
                    "builtin-colors", "Set folder color...", "Pack_01",
                    new [] { "Black", "Gray", "Red", "Brown", "Orange", "Lime", "Purple", "Yellow", "Emerald", "Navy Blue", "Blue", "Rose", "Cherry", "Topaz" }),
                CreateBuiltInPackage(
                    "builtin-types", "Set folder for...", "Pack_02",
                    new [] { "Music", "Pictures", "Videos", "Cloud", "Desktop", "Link", "Downloads", "Dropbox", "Search", "User", "FolderType 1", "FolderType 2", "FolderType 3", "Gaming" })
            };
        }

        private static FolderColorizerPackage CreateBuiltInPackage(string id, string name, string packFolder, string[] labels)
        {
            FolderColorizerPackage package = new FolderColorizerPackage
            {
                Id = id,
                Name = name,
                IsBuiltIn = true,
                PackageDirectory = Path.Combine(BuiltInCacheDirectory, packFolder),
                PackageIconPath = Path.Combine(BuiltInCacheDirectory, packFolder, "00.ico"),
                PackageIconResourceName = "ContextMenuManager.FolderColorizer." + packFolder + ".00.ico"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                string fileName = (i + 1).ToString("D2") + ".ico";
                package.Icons.Add(new FolderColorizerIconEntry
                {
                    Id = (i + 1).ToString("D2"),
                    Name = labels[i],
                    FilePath = Path.Combine(package.PackageDirectory, fileName),
                    ResourceName = "ContextMenuManager.FolderColorizer." + packFolder + "." + fileName
                });
            }
            return package;
        }

        public static void EnsureBuiltInPacksExtracted()
        {
            Directory.CreateDirectory(BuiltInCacheDirectory);
            string cacheMarker = Path.Combine(BuiltInCacheDirectory, ".cache-version");
            string buildId = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString("N");
            try
            {
                if (File.Exists(cacheMarker) && string.Equals(File.ReadAllText(cacheMarker).Trim(), buildId, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(BuiltInCacheDirectory, "Pack_01", "14.ico"))
                    && File.Exists(Path.Combine(BuiltInCacheDirectory, "Pack_02", "14.ico"))
                    && File.Exists(Path.Combine(BuiltInCacheDirectory, "Pack_03", "14.ico")))
                    return;
            }
            catch { }

            foreach (string pack in new[] { "Pack_01", "Pack_02", "Pack_03" })
            {
                string targetDir = Path.Combine(BuiltInCacheDirectory, pack);
                Directory.CreateDirectory(targetDir);
                for (int i = 0; i <= 14; i++)
                {
                    string fileName = i.ToString("D2") + ".ico";
                    ExtractEmbeddedResource("ContextMenuManager.FolderColorizer." + pack + "." + fileName, Path.Combine(targetDir, fileName));
                }
                if (pack == "Pack_01")
                    ExtractEmbeddedResource("ContextMenuManager.FolderColorizer.Pack_01.empty.ico", Path.Combine(targetDir, "empty.ico"));
            }
            try { File.WriteAllText(cacheMarker, buildId, Encoding.ASCII); } catch { }
        }

        private static void ExtractEmbeddedResource(string resourceName, string targetPath)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;
                    byte[] newBytes;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        newBytes = ms.ToArray();
                    }
                    if (File.Exists(targetPath))
                    {
                        try
                        {
                            byte[] oldBytes = File.ReadAllBytes(targetPath);
                            if (oldBytes.SequenceEqual(newBytes)) return;
                            File.SetAttributes(targetPath, FileAttributes.Normal);
                        }
                        catch { }
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    File.WriteAllBytes(targetPath, newBytes);
                }
            }
            catch { }
        }

        private static string GetPackageIconForRegistry(FolderColorizerPackage package)
        {
            if (package == null) return "explorer.exe,0";
            if (!string.IsNullOrWhiteSpace(package.PackageIconPath) && File.Exists(package.PackageIconPath)) return package.PackageIconPath;
            if (package.Icons.Count > 0 && File.Exists(package.Icons[0].FilePath)) return package.Icons[0].FilePath;
            return "explorer.exe,0";
        }

        private static List<FolderColorizerPackage> LoadCustomPackages()
        {
            List<FolderColorizerPackage> result = new List<FolderColorizerPackage>();
            try
            {
                Directory.CreateDirectory(CustomPackagesDirectory);
                foreach (string dir in Directory.GetDirectories(CustomPackagesDirectory))
                {
                    string manifest = Path.Combine(dir, ManifestName);
                    if (!File.Exists(manifest)) continue;
                    try
                    {
                        XDocument doc = XDocument.Load(manifest);
                        XElement root = doc.Root;
                        if (root == null || !string.Equals(root.Name.LocalName, "package", StringComparison.OrdinalIgnoreCase)) continue;
                        FolderColorizerPackage package = new FolderColorizerPackage
                        {
                            Id = (string)root.Attribute("id") ?? Path.GetFileName(dir),
                            Name = (string)root.Attribute("name") ?? Path.GetFileName(dir),
                            IsBuiltIn = false,
                            PackageDirectory = dir
                        };
                        string packageIcon = (string)root.Attribute("icon");
                        if (!string.IsNullOrWhiteSpace(packageIcon) && packageIcon == Path.GetFileName(packageIcon))
                            package.PackageIconPath = Path.Combine(dir, packageIcon);
                        foreach (XElement xIcon in root.Elements("icon"))
                        {
                            string file = (string)xIcon.Attribute("file");
                            if (string.IsNullOrWhiteSpace(file)) continue;
                            if (file != Path.GetFileName(file) ||
                                !string.Equals(Path.GetExtension(file), ".ico", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("Unsafe legacy icon filename: " + file);
                            package.Icons.Add(new FolderColorizerIconEntry
                            {
                                Id = (string)xIcon.Attribute("id") ?? Guid.NewGuid().ToString("N"),
                                Name = (string)xIcon.Attribute("name") ?? Path.GetFileNameWithoutExtension(file),
                                FilePath = Path.Combine(dir, file)
                            });
                        }
                        result.Add(package);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("Cannot read custom Folder Colorizer package: " + manifest +
                            ". Existing Explorer menus were preserved. " + ex.Message, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidDataException) throw;
                throw new IOException("Cannot enumerate custom Folder Colorizer packages in " +
                    CustomPackagesDirectory + ". Existing Explorer menus were preserved.", ex);
            }
            return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // The three categories are permanent. Only Mark folder as is editable.
        // Custom entries and overrides are stored OUTSIDE the embedded resource cache;
        // publishing a new EXE can safely update the defaults without losing edits.
        private static XDocument LoadMarkManifest()
        {
            if (!File.Exists(MarkManifest)) return new XDocument(new XElement("mark-folder-as"));
            try
            {
                XDocument doc = XDocument.Load(MarkManifest);
                if (doc.Root == null || doc.Root.Name != "mark-folder-as")
                    throw new InvalidDataException("Unexpected Mark folder as XML root.");
                return doc;
            }
            catch (Exception ex)
            {
                // Preserve corrupt XML for recovery, but do not keep an obsolete
                // fourth context-menu root alive because an edit file is broken.
                string backup = MarkManifest + ".corrupt-" + Guid.NewGuid().ToString("N") + ".xml";
                try { File.Move(MarkManifest, backup); }
                catch (Exception backupError)
                {
                    throw new IOException("Cannot back up the damaged Mark customization file. " +
                        "Original data has NOT been overwritten: " + MarkManifest, backupError);
                }
                LogInstallFailure("Damaged Mark XML moved to " + backup, ex);
                return new XDocument(new XElement("mark-folder-as"));
            }
        }

        private static string SafeMarkFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) ||
                !string.Equals(Path.GetExtension(file), ".ico", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.Combine(MarkDirectory, file);
        }

        private static void ApplyMarkCustomization(FolderColorizerPackage mark)
        {
            XDocument doc = LoadMarkManifest();
            foreach (XElement entry in doc.Root.Elements("builtin"))
            {
                FolderColorizerIconEntry icon = mark.Icons.FirstOrDefault(i => i.Id == (string)entry.Attribute("id"));
                if (icon == null) continue;
                string label = (string)entry.Attribute("name");
                if (!string.IsNullOrWhiteSpace(label)) icon.Name = label;
                string replacement = SafeMarkFile((string)entry.Attribute("file"));
                if (replacement != null)
                {
                    if (File.Exists(replacement))
                    {
                        icon.FilePath = replacement;
                        icon.ResourceName = null;
                    }
                    else LogInstallFailure("Missing Mark override; using embedded default",
                        new FileNotFoundException(icon.Name, replacement));
                }
                if ((string)entry.Attribute("hidden") == "true") mark.Icons.Remove(icon);
            }
            foreach (XElement entry in doc.Root.Elements("icon"))
            {
                string id = (string)entry.Attribute("id");
                string name = (string)entry.Attribute("name");
                string file = SafeMarkFile((string)entry.Attribute("file"));
                if (string.IsNullOrWhiteSpace(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-zA-Z0-9_-]{1,96}$") ||
                    mark.Icons.Any(i => i.Id == id) || file == null || !File.Exists(file))
                {
                    LogInstallFailure("Skipped invalid Mark icon", new InvalidDataException(
                        "ID=" + id + " in " + MarkManifest));
                    continue;
                }
                mark.Icons.Add(new FolderColorizerIconEntry { Id = id, Name = NormalizeDisplayName(name), FilePath = file });
            }
        }

        private static string NormalizeDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Item";
            return string.Join(" ", value.Replace("\r", " ").Replace("\n", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private static void SaveMarkCustomization(FolderColorizerPackage mark, string justMigratedId = null)
        {
            Directory.CreateDirectory(MarkDirectory);
            XElement root = new XElement("mark-folder-as");
            XDocument previous = LoadMarkManifest();
            // Preserve migration tombstones: deleted imported icons must stay deleted.
            foreach (XElement migrated in previous.Root.Elements("migrated")) root.Add(new XElement(migrated));
            if (!string.IsNullOrWhiteSpace(justMigratedId) &&
                !root.Elements("migrated").Any(x => (string)x.Attribute("id") == justMigratedId))
                root.Add(new XElement("migrated", new XAttribute("id", justMigratedId)));
            FolderColorizerPackage defaults = GetBuiltInPackages()[0];
            foreach (FolderColorizerIconEntry initial in defaults.Icons)
            {
                FolderColorizerIconEntry edited = mark.Icons.FirstOrDefault(i => i.Id == initial.Id);
                XElement entry = new XElement("builtin", new XAttribute("id", initial.Id));
                if (edited == null) entry.Add(new XAttribute("hidden", "true"));
                else
                {
                    if (edited.Name != initial.Name) entry.Add(new XAttribute("name", edited.Name));
                    if (edited.ResourceName == null)
                    {
                        string full = Path.GetFullPath(edited.FilePath ?? "");
                        if (SafeMarkFile(Path.GetFileName(full)) != full)
                            throw new InvalidDataException("Override must be in the Mark folder as directory.");
                        entry.Add(new XAttribute("file", Path.GetFileName(full)));
                    }
                }
                if (entry.HasAttributes && (entry.Attributes().Count() > 1)) root.Add(entry);
            }
            foreach (FolderColorizerIconEntry icon in mark.Icons.Where(i => !defaults.Icons.Any(d => d.Id == i.Id)))
            {
                string full = Path.GetFullPath(icon.FilePath ?? "");
                if (SafeMarkFile(Path.GetFileName(full)) != full || !File.Exists(full))
                    throw new InvalidDataException("Custom icon is missing or outside the Mark folder as directory: " + icon.Name);
                root.Add(new XElement("icon", new XAttribute("id", icon.Id),
                    new XAttribute("name", icon.Name), new XAttribute("file", Path.GetFileName(full))));
            }
            string temp = MarkManifest + ".new";
            new XDocument(root).Save(temp);
            if (File.Exists(MarkManifest)) File.Replace(temp, MarkManifest, MarkManifest + ".previous");
            else File.Move(temp, MarkManifest);
        }

        private static string CopyMarkIcon(string source)
        {
            if (!File.Exists(source) || !string.Equals(Path.GetExtension(source), ".ico", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select a valid .ico file: " + source);
            using (Icon preview = new Icon(source)) { if (preview == null) throw new InvalidDataException("Invalid ICO file"); }
            Directory.CreateDirectory(MarkDirectory);
            string target = Path.Combine(MarkDirectory, "icon-" + Guid.NewGuid().ToString("N") + ".ico");
            File.Copy(source, target, false);
            return target;
        }

        public static void MigrateLegacyPackages()
        {
            // A one-way COPY: never delete or edit the original Packages folders.
            if (!Directory.Exists(CustomPackagesDirectory)) return;
            List<FolderColorizerPackage> legacyPackages;
            try { legacyPackages = LoadCustomPackages(); }
            catch (Exception ex)
            {
                // A corrupt OLD package must not prevent the three default menus
                // or Folder Management from being repaired. Preserve it untouched.
                LogInstallFailure("Skipped unreadable legacy package(s)", ex);
                return;
            }
            foreach (FolderColorizerPackage legacy in legacyPackages)
            {
                try
                {
                    XDocument document = LoadMarkManifest();
                    string legacyId = legacy.Id;
                    if (document.Root.Elements("migrated").Any(x => (string)x.Attribute("id") == legacyId)) continue;
                    FolderColorizerPackage mark = GetPackages()[0];
                    foreach (FolderColorizerIconEntry icon in legacy.Icons)
                    {
                        try
                        {
                            string target = CopyMarkIcon(icon.FilePath);
                            mark.Icons.Add(new FolderColorizerIconEntry
                            {
                                Id = "user-" + Guid.NewGuid().ToString("N"),
                                Name = NormalizeDisplayName(icon.Name), FilePath = target
                            });
                        }
                        catch (Exception ex)
                        {
                            // Invalid legacy icons never prevent normal menu setup.
                            // Their original backup stays in Packages for inspection.
                            LogInstallFailure("Skipped legacy icon " + legacy.Name + " / " + icon.Name, ex);
                        }
                    }
                    // Icon entries and the migration tombstone commit atomically.
                    // Reopening the application cannot import the same icons twice.
                    SaveMarkCustomization(mark, legacyId);
                }
                catch (Exception ex)
                {
                    // The migration is best-effort, NEVER a reason to hide Folder
                    // Management or block the three permanent Colorizer categories.
                    LogInstallFailure("Skipped legacy package " + legacy.Name, ex);
                }
            }
        }

        public static void AddIcons(string packageId, IEnumerable<string> sources)
        {
            FolderColorizerPackage mark = GetEditableMark(packageId);
            string[] incoming = (sources ?? Enumerable.Empty<string>()).ToArray();
            if (mark.Icons.Count + incoming.Length > 209)
                throw new InvalidOperationException("Mark folder as supports up to 209 icons, including the built-in ones.");
            foreach (string path in incoming)
            {
                string target = CopyMarkIcon(path);
                mark.Icons.Add(new FolderColorizerIconEntry { Id = "user-" + Guid.NewGuid().ToString("N"),
                    Name = NormalizeDisplayName(Path.GetFileNameWithoutExtension(path)), FilePath = target });
            }
            SaveMarkCustomization(mark);
            RefreshContextMenuIfInstalled();
        }

        public static void RenameIcon(string packageId, string iconId, string newName)
        {
            FolderColorizerPackage mark = GetEditableMark(packageId);
            FolderColorizerIconEntry icon = mark.Icons.FirstOrDefault(i => i.Id == iconId);
            if (icon == null || string.IsNullOrWhiteSpace(newName)) return;
            icon.Name = NormalizeDisplayName(newName);
            SaveMarkCustomization(mark);
            RefreshContextMenuIfInstalled();
        }

        public static void ReplaceIcon(string packageId, string iconId, string replacement)
        {
            FolderColorizerPackage mark = GetEditableMark(packageId);
            FolderColorizerIconEntry icon = mark.Icons.FirstOrDefault(i => i.Id == iconId);
            if (icon == null) return;
            string target = CopyMarkIcon(replacement);
            icon.FilePath = target;
            icon.ResourceName = null;
            SaveMarkCustomization(mark);
            RefreshContextMenuIfInstalled();
        }

        public static void RemoveIcon(string packageId, string iconId)
        {
            FolderColorizerPackage mark = GetEditableMark(packageId);
            FolderColorizerIconEntry icon = mark.Icons.FirstOrDefault(i => i.Id == iconId);
            if (icon == null) return;
            mark.Icons.Remove(icon);
            if (mark.Icons.Count == 0) throw new InvalidOperationException("Mark folder as needs at least one icon.");
            SaveMarkCustomization(mark);
            RefreshContextMenuIfInstalled();
            // Do not delete .ico assets still referenced by earlier Explorer menus.
        }

        public static void RestoreMarkDefaults()
        {
            FolderColorizerPackage mark = GetBuiltInPackages()[0];
            SaveMarkCustomization(mark); // Preserve migration tombstones; no re-import.
            RefreshContextMenuIfInstalled();
        }

        private static FolderColorizerPackage GetEditableMark(string id)
        {
            if (!string.Equals(id, MarkId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only Mark folder as is editable.");
            return GetPackages()[0];
        }

        public static Image LoadPreviewImage(string iconPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) return null;
                using (Icon icon = new Icon(iconPath, new Size(48, 48)))
                    return icon.ToBitmap();
            }
            catch
            {
                try
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(iconPath))
                        return icon?.ToBitmap();
                }
                catch { return null; }
            }
        }
    }
}
