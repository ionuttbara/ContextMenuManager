using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace ContextMenuManager.Methods
{
    // Native .NET Framework port of folder-management/src/{clsid_manager,reg_exporter}.rs.
    // Folder Management creates Explorer namespace shortcuts, NOT Folder Colorizer icon menus.
    // No arbitrary CLSIDs/system namespace entries are ever deleted by this manager.
    [DataContract]
    public sealed class ManagedFolderProfile
    {
        [DataMember(Order = 0)] public string Clsid { get; set; }
        [DataMember(Order = 1)] public string Group { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Path { get; set; }
        [DataMember(Order = 4)] public string IconPath { get; set; }
        [DataMember(Order = 5)] public int PositionId { get; set; }
        [DataMember(Order = 6)] public int SortOrder { get; set; }
        [DataMember(Order = 7)] public int DescriptionId { get; set; }
        [DataMember(Order = 8)] public bool ShowDesktop { get; set; }
        [DataMember(Order = 9)] public bool ShowThisPc { get; set; }
        [DataMember(Order = 10)] public bool PinnedNavigation { get; set; }

        public ManagedFolderProfile()
        {
            Clsid = FolderManagementManager.NewClsid();
            Group = "Default";
            Name = string.Empty;
            Path = string.Empty;
            IconPath = string.Empty;
            PositionId = 1;
            SortOrder = 24;
            DescriptionId = 3;
            ShowDesktop = true;
            ShowThisPc = false;
            PinnedNavigation = true;
        }

        public ManagedFolderProfile Copy()
        {
            return (ManagedFolderProfile)MemberwiseClone();
        }

        public override string ToString() { return Name + "  [" + Group + "]"; }
    }

    [DataContract]
    public sealed class FolderProfileDocument
    {
        [DataMember(Order = 0)] public int Version { get; set; }
        [DataMember(Order = 1)] public List<ManagedFolderProfile> Profiles { get; set; }
        public FolderProfileDocument() { Version = 1; Profiles = new List<ManagedFolderProfile>(); }
    }

    [DataContract]
    internal sealed class RustClsidStore
    {
        [DataMember(Name = "map")] public Dictionary<string, string> Map { get; set; }
    }

    internal static class FolderManagementManager
    {
        private const string LegacyRoot = @"SOFTWARE\Gallery Inc\Folder Manager";
        private const string MachineClasses = @"SOFTWARE\Classes\CLSID";
        private const string WowClasses = @"SOFTWARE\Classes\WOW6432Node\CLSID";
        private const string ThisPcRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace";
        private const string DesktopRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace";
        private const string DesktopHide = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        private const string ShellLinkClsid = "{0AFACED1-E828-11D1-9187-B532F1E9575D}";

        internal static string ProfilesPath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Gallery Inc", "ContextMenuManager", "FolderManagement", "profiles.json");

        internal static string NewClsid() { return Guid.NewGuid().ToString("B").ToUpperInvariant(); }

        internal static string CanonicalClsid(string clsid)
        {
            Guid value;
            if (!Guid.TryParse(clsid, out value) || value == Guid.Empty)
                throw new ArgumentException("Invalid CLSID (GUID).", nameof(clsid));
            return value.ToString("B").ToUpperInvariant();
        }

        internal static void Validate(ManagedFolderProfile profile, bool checkDirectory)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Clsid = CanonicalClsid(profile.Clsid);
            if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 200 ||
                profile.Name.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Enter a valid folder name (1–200 characters).", "profile");
            if (string.IsNullOrWhiteSpace(profile.Path) || profile.Path.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Choose a folder path.", "profile");
            if (checkDirectory && !Directory.Exists(Environment.ExpandEnvironmentVariables(profile.Path)))
                throw new DirectoryNotFoundException("Folder does not exist: " + profile.Path);
            if (profile.Group == null || profile.Group.Length > 120 || profile.Group.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Invalid profile group.");
            if (profile.IconPath == null) profile.IconPath = string.Empty;
            if (profile.IconPath.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Invalid icon path.");
            if (profile.IconPath.Length > 0 && checkDirectory)
            {
                string iconFile = Environment.ExpandEnvironmentVariables(profile.IconPath);
                // REG_SZ icon resources like shell32.dll,-3 are supported; .ico paths must exist.
                if (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) && !File.Exists(iconFile))
                    throw new FileNotFoundException("Icon not found.", iconFile);
            }
            if (profile.PositionId < 1 || profile.PositionId > 999999)
                throw new ArgumentException("Profile position must be between 1 and 999999.");
            if (profile.SortOrder < 0 || profile.SortOrder > 65535)
                throw new ArgumentException("Navigation sort order must be between 0 and 65535.");
            if (profile.DescriptionId != 3 && profile.DescriptionId != 6 && profile.DescriptionId != 9)
                throw new ArgumentException("Description ID must be 3, 6 or 9.");
        }

        private static readonly DataContractJsonSerializer Json = new DataContractJsonSerializer(
            typeof(FolderProfileDocument), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        internal static FolderProfileDocument Load()
        {
            if (!File.Exists(ProfilesPath)) return new FolderProfileDocument();
            using (FileStream stream = File.OpenRead(ProfilesPath))
            {
                var result = (FolderProfileDocument)Json.ReadObject(stream);
                if (result == null || result.Profiles == null || result.Version != 1)
                    throw new SerializationException("Unsupported or corrupt Folder Management profiles file. Original file was not changed.");
                foreach (ManagedFolderProfile profile in result.Profiles) Validate(profile, false);
                if (result.Profiles.GroupBy(p => p.Clsid, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    throw new SerializationException("Duplicate CLSIDs in saved profiles; original file was not changed.");
                return result;
            }
        }

        internal static void Save(FolderProfileDocument document)
        {
            if (document == null || document.Profiles == null) throw new ArgumentNullException(nameof(document));
            foreach (ManagedFolderProfile profile in document.Profiles) Validate(profile, false);
            if (document.Profiles.GroupBy(p => p.Clsid, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Two profiles cannot share one CLSID.");
            string dir = System.IO.Path.GetDirectoryName(ProfilesPath);
            Directory.CreateDirectory(dir);
            string temp = ProfilesPath + ".tmp";
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                Json.WriteObject(stream, document);
            if (File.Exists(ProfilesPath))
            {
                string backup = ProfilesPath + ".bak";
                File.Replace(temp, ProfilesPath, backup);
            }
            else File.Move(temp, ProfilesPath);
        }

        internal static void ExportProfiles(IEnumerable<ManagedFolderProfile> profiles, string filename)
        {
            FolderProfileDocument document = new FolderProfileDocument();
            document.Profiles = profiles.Select(p => p.Copy()).ToList();
            foreach (var p in document.Profiles) Validate(p, false);
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                Json.WriteObject(stream, document);
        }

        internal static IList<ManagedFolderProfile> ImportProfiles(string filename)
        {
            using (FileStream stream = File.OpenRead(filename))
            {
                FolderProfileDocument doc = (FolderProfileDocument)Json.ReadObject(stream);
                if (doc == null || doc.Version != 1 || doc.Profiles == null)
                    throw new InvalidDataException("Invalid or unsupported Folder Management JSON bundle.");
                foreach (var p in doc.Profiles) Validate(p, false);
                if (doc.Profiles.GroupBy(p => p.Clsid, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    throw new InvalidDataException("Imported profiles have duplicate CLSIDs.");
                return doc.Profiles;
            }
        }

        // Read ONLY the legacy Gallery Inc store and Rust-generated namespace classes.
        // Reading does not install, remove or alter the existing system configuration.
        internal static List<ManagedFolderProfile> ReadLegacyRegistry()
        {
            var results = new List<ManagedFolderProfile>();
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            using (RegistryKey root = machine.OpenSubKey(LegacyRoot, false))
            {
                if (root == null) return results;
                foreach (string position in root.GetSubKeyNames())
                {
                    int id;
                    if (!int.TryParse(position, out id) || id <= 0 || id > 999999) continue;
                    using (RegistryKey key = root.OpenSubKey(position))
                    {
                        if (key == null) continue;
                        string clsid = key.GetValue("CLSID") as string;
                        if (!TryCanonical(clsid, out clsid)) continue;
                        // Legacy Rust saves profiles in HKLM even when the exported .reg was never applied.
                        // Do not adopt a CLSID already used by an unrelated Windows component.
                        bool installed = IsRustManagedClsid(clsid);
                        if (!installed && ClsidExists(clsid)) continue;
                        ManagedFolderProfile p = installed ? ReadInstalled(clsid) : new ManagedFolderProfile();
                        if (p == null) continue;
                        p.Clsid = clsid;
                        p.Name = installed ? p.Name : (key.GetValue("Name") as string ?? "");
                        p.Path = installed ? p.Path : (key.GetValue("Path") as string ?? "");
                        if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Path)) continue;
                        p.PositionId = id;
                        p.Group = "Rust imported";
                        results.Add(p);
                    }
                }
            }
            return results;
        }

        internal static List<ManagedFolderProfile> ReadRustClsidMap()
        {
            var results = new List<ManagedFolderProfile>();
            // Rust dirs::config_dir on Windows resolves to roaming AppData.
            string oldPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "my_registry_exporter", "clsid_store.json");
            if (!File.Exists(oldPath)) return results;
            var serializer = new DataContractJsonSerializer(typeof(RustClsidStore),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (FileStream stream = File.OpenRead(oldPath))
            {
                RustClsidStore store = (RustClsidStore)serializer.ReadObject(stream);
                foreach (var entry in store.Map ?? new Dictionary<string, string>())
                {
                    string clsid;
                    if (!TryCanonical(entry.Value, out clsid)) continue;
                    // Folder names can contain |. The rightmost separator comes from Rust's name|path key.
                    int sep = entry.Key.LastIndexOf('|');
                    if (sep < 1 || sep >= entry.Key.Length - 1) continue;
                    ManagedFolderProfile p = NewProfile(results);
                    p.Name = entry.Key.Substring(0, sep);
                    p.Path = entry.Key.Substring(sep + 1);
                    p.Clsid = clsid;
                    p.Group = "Rust imported";
                    if (IsRustManagedClsid(clsid))
                    {
                        ManagedFolderProfile installed = ReadInstalled(clsid);
                        if (installed != null)
                        {
                            p.SortOrder = installed.SortOrder;
                            p.DescriptionId = installed.DescriptionId;
                            p.IconPath = installed.IconPath;
                            p.ShowDesktop = installed.ShowDesktop;
                            p.ShowThisPc = installed.ShowThisPc;
                            p.PinnedNavigation = installed.PinnedNavigation;
                        }
                    }
                    if (!results.Any(x => x.Clsid.Equals(p.Clsid, StringComparison.OrdinalIgnoreCase)))
                        results.Add(p);
                }
            }
            return results;
        }

        private static RegistryView MachineView => Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;

        internal static bool TryCanonical(string text, out string canonical)
        {
            canonical = null;
            Guid guid;
            if (!Guid.TryParse(text, out guid) || guid == Guid.Empty) return false;
            canonical = guid.ToString("B").ToUpperInvariant();
            return true;
        }

        // PositionId is a LOCAL profile ordering identifier, not a Windows Explorer/CLSID
        // resource. The old Rust program also wrote numbered HKLM legacy records; those
        // records may belong to a different folder and must remain untouched.
        internal static int FreeProfilePosition(IEnumerable<ManagedFolderProfile> existing,
            int preferred = 1, string exceptClsid = null)
        {
            var used = new HashSet<int>(existing.Where(x => x != null &&
                !string.Equals(x.Clsid, exceptClsid, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.PositionId));
            if (preferred >= 1 && preferred <= 999999 && !used.Contains(preferred)) return preferred;
            for (int candidate = 1; candidate <= 999999; candidate++)
                if (!used.Contains(candidate)) return candidate;
            throw new InvalidOperationException("No free local folder profile positions are available.");
        }

        internal static ManagedFolderProfile NewProfile(IEnumerable<ManagedFolderProfile> existing)
        {
            var p = new ManagedFolderProfile();
            var list = existing.ToList();
            while (list.Any(x => x.Clsid.Equals(p.Clsid, StringComparison.OrdinalIgnoreCase))) p.Clsid = NewClsid();
            p.PositionId = FreeProfilePosition(list);
            return p;
        }

        internal static bool IsRustManagedClsid(string clsid)
        {
            if (!TryCanonical(clsid, out clsid)) return false;
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            using (RegistryKey cls = machine.OpenSubKey(MachineClasses + @"\" + clsid, false))
            {
                if (cls == null) return false;
                using (RegistryKey instance = cls.OpenSubKey("Instance"))
                using (RegistryKey server = cls.OpenSubKey("InProcServer32"))
                using (RegistryKey bag = cls.OpenSubKey(@"Instance\InitPropertyBag"))
                {
                    return instance != null && server != null && bag != null &&
                        string.Equals(instance.GetValue("CLSID") as string, ShellLinkClsid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(server.GetValue(null) as string, "shdocvw.dll", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(bag.GetValue("Target") as string);
                }
            }
        }

        internal static bool IsInstalled(ManagedFolderProfile p)
        {
            if (!IsRustManagedClsid(p.Clsid)) return false;
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            using (RegistryKey bag = machine.OpenSubKey(MachineClasses + @"\" + CanonicalClsid(p.Clsid) + @"\Instance\InitPropertyBag"))
                return string.Equals(bag?.GetValue("Target") as string, p.Path, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ClsidExists(string clsid)
        {
            clsid = CanonicalClsid(clsid);
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            using (RegistryKey cls = machine.OpenSubKey(MachineClasses + @"\" + clsid))
            using (RegistryKey userClass = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\" + clsid))
                return cls != null || userClass != null;
        }

        internal static ManagedFolderProfile ReadInstalled(string clsid)
        {
            if (!IsRustManagedClsid(clsid)) return null;
            clsid = CanonicalClsid(clsid);
            var p = new ManagedFolderProfile { Clsid = clsid, Group = "Rust imported" };
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            using (RegistryKey cls = machine.OpenSubKey(MachineClasses + @"\" + clsid, false))
            using (RegistryKey bag = cls?.OpenSubKey(@"Instance\InitPropertyBag"))
            using (RegistryKey icon = cls?.OpenSubKey("DefaultIcon"))
            using (RegistryKey pc = machine.OpenSubKey(ThisPcRoot + @"\" + clsid))
            using (RegistryKey hide = Registry.CurrentUser.OpenSubKey(DesktopHide))
            {
                p.Name = (cls.GetValue(null) as string) ?? "Folder";
                p.Path = (bag?.GetValue("Target") as string) ?? string.Empty;
                p.IconPath = (icon?.GetValue(null) as string) ?? string.Empty;
                p.SortOrder = Convert.ToInt32(cls.GetValue("SortOrderIndex", 24));
                int description = Convert.ToInt32(cls.GetValue("DescriptionID", 3));
                p.DescriptionId = new[] { 3, 6, 9 }.Contains(description) ? description : 3;
                p.PinnedNavigation = Convert.ToInt32(cls.GetValue("System.IsPinnedtoNameSpaceTree", 1)) != 0;
                p.ShowDesktop = Convert.ToInt32(hide?.GetValue(clsid, 0) ?? 0) == 0;
                p.ShowThisPc = pc != null;
            }
            return p;
        }

        private static string RegText(string input)
        {
            if (input == null) return string.Empty;
            if (input.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentException("Invalid multiline registry string.");
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string DefaultIcon => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "imageres.dll") + ",-27";

        internal static string AddReg(ManagedFolderProfile p)
        {
            Validate(p, false);
            string id = p.Clsid;
            string icon = RegText(string.IsNullOrWhiteSpace(p.IconPath) ? DefaultIcon : p.IconPath);
            string name = RegText(p.Name);
            string path = RegText(p.Path);
            string common = "";
            foreach (string root in new[] { @"HKEY_CLASSES_ROOT\CLSID\", @"HKEY_CLASSES_ROOT\WOW6432Node\CLSID\" })
            {
                string key = root + id;
                common += "[" + key + "]\r\n" +
                    "@=\"" + name + "\"\r\n" +
                    "\"InfoTip\"=\"Show " + name + ".\"\r\n" +
                    "\"System.IsPinnedtoNameSpaceTree\"=dword:" + (p.PinnedNavigation ? "00000001" : "00000000") + "\r\n" +
                    "\"SortOrderIndex\"=dword:" + p.SortOrder.ToString("x8") + "\r\n" +
                    "\"DescriptionID\"=dword:" + p.DescriptionId.ToString("x8") + "\r\n\r\n" +
                    "[" + key + @"\Instance]" + "\r\n\"CLSID\"=\"" + ShellLinkClsid + "\"\r\n\r\n" +
                    "[" + key + @"\Instance\InitPropertyBag]" + "\r\n\"Attributes\"=dword:00000015\r\n\"Target\"=\"" + path + "\"\r\n\r\n" +
                    "[" + key + @"\DefaultIcon]" + "\r\n@=\"" + icon + "\"\r\n\r\n" +
                    "[" + key + @"\InProcServer32]" + "\r\n@=\"shdocvw.dll\"\r\n\"ThreadingModel\"=\"Both\"\r\n\r\n" +
                    "[" + key + @"\ShellEx\PropertySheetHandlers\tab 1 general]" + "\r\n@=\"{21B22460-3AEA-1069-A2DC-08002B30309D}\"\r\n\r\n" +
                    "[" + key + @"\ShellFolder]" + "\r\n\"Attributes\"=dword:f080004d\r\n\"HideAsDeletePerUser\"=\"\"\r\n\"WantsFORPARSING\"=\"\"\r\n\r\n";
            }
            string thisPc = p.ShowThisPc ?
                "[HKEY_LOCAL_MACHINE\\" + ThisPcRoot + @"\" + id + "]\r\n\r\n" :
                "[-HKEY_LOCAL_MACHINE\\" + ThisPcRoot + @"\" + id + "]\r\n\r\n";
            return "Windows Registry Editor Version 5.00\r\n\r\n; Managed Folder: " + id + "\r\n\r\n" + common + thisPc +
                "[HKEY_CURRENT_USER\\" + DesktopRoot + @"\" + id + "]\r\n@=\"" + name + "\"\r\n\r\n" +
                "[HKEY_CURRENT_USER\\" + DesktopHide + "]\r\n\"" + id + "\"=dword:" +
                (p.ShowDesktop ? "00000000" : "00000001") + "\r\n";
        }

        internal static string RemoveReg(string clsid)
        {
            string id = CanonicalClsid(clsid);
            return "Windows Registry Editor Version 5.00\r\n\r\n" +
                "[-HKEY_CLASSES_ROOT\\CLSID\\" + id + "]\r\n" +
                "[-HKEY_CLASSES_ROOT\\WOW6432Node\\CLSID\\" + id + "]\r\n" +
                "[-HKEY_LOCAL_MACHINE\\" + ThisPcRoot + @"\" + id + "]\r\n" +
                "[-HKEY_CURRENT_USER\\" + DesktopRoot + @"\" + id + "]\r\n" +
                "[HKEY_CURRENT_USER\\" + DesktopHide + "]\r\n\"" + id + "\"=-\r\n";
        }

        internal static void WriteRegFile(string path, string contents)
        {
            if (string.IsNullOrWhiteSpace(contents) || !contents.StartsWith("Windows Registry Editor Version 5.00", StringComparison.Ordinal))
                throw new ArgumentException("Not a valid generated registry file.");
            // Regedit expects UTF-16LE BOM for reliable non-ASCII file and folder names.
            File.WriteAllText(path, contents, Encoding.Unicode);
        }

        // Import is data-only. Never execute a third-party .reg file.
        // Read only the known Rust exporter fields; other sections/commands are ignored.
        internal static ManagedFolderProfile ImportRustReg(string path)
        {
            string text = File.ReadAllText(path, Encoding.Default);
            if (!text.Contains("Windows Registry Editor Version 5.00"))
                throw new InvalidDataException("Not a Windows .reg export.");
            Match match = Regex.Match(text, @"(?im)^\s*\[HKEY_CLASSES_ROOT\\CLSID\\(\{[0-9a-f-]{36}\})\]\s*$");
            if (!match.Success) throw new InvalidDataException("No Rust folder CLSID entry found.");
            string clsid = CanonicalClsid(match.Groups[1].Value);
            string keyBody = RegSection(text, @"HKEY_CLASSES_ROOT\CLSID\" + clsid);
            string name = ReadString(keyBody, "@", "");
            string target = ReadString(RegSection(text, @"HKEY_CLASSES_ROOT\CLSID\" + clsid + @"\Instance\InitPropertyBag"), "Target", "");
            string server = ReadString(RegSection(text, @"HKEY_CLASSES_ROOT\CLSID\" + clsid + @"\InProcServer32"), "@", "");
            string instance = ReadString(RegSection(text, @"HKEY_CLASSES_ROOT\CLSID\" + clsid + @"\Instance"), "CLSID", "");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target) ||
                !server.Equals("shdocvw.dll", StringComparison.OrdinalIgnoreCase) ||
                !instance.Equals(ShellLinkClsid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Not a recognized Rust Folder Manager export.");
            ManagedFolderProfile p = new ManagedFolderProfile();
            p.Clsid = clsid;
            p.Name = name;
            p.Path = target;
            p.IconPath = ReadString(RegSection(text, @"HKEY_CLASSES_ROOT\CLSID\" + clsid + @"\DefaultIcon"), "@", "");
            p.Group = "Imported .reg";
            p.SortOrder = ReadDword(keyBody, "SortOrderIndex", 24);
            int did = ReadDword(keyBody, "DescriptionID", 3);
            p.DescriptionId = new[] { 3, 6, 9 }.Contains(did) ? did : 3;
            p.PinnedNavigation = ReadDword(keyBody, "System.IsPinnedtoNameSpaceTree", 1) != 0;
            p.ShowDesktop = ReadDword(RegSection(text, @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel"),
                clsid, 0) == 0;
            p.ShowThisPc = Regex.IsMatch(text, @"(?im)^\s*\[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\" + Regex.Escape(clsid) + @"\]\s*$");
            Validate(p, false);
            return p;
        }

        private static string RegSection(string contents, string section)
        {
            Match m = Regex.Match(contents, @"(?im)^\s*\[" + Regex.Escape(section) + @"\]\s*\r?\n(?<body>(?:^(?!\s*\[).*(?:\r?\n|$))*)");
            return m.Success ? m.Groups["body"].Value : "";
        }

        private static string ReadString(string section, string key, string fallback)
        {
            // .reg quotes and doubled backslashes are escaped; parse only our known keys.
            string prefix = key == "@" ? "@" : "\"" + Regex.Escape(key) + "\"";
            string pattern = @"(?m)^" + prefix + "=\"((?:\\\\.|[^\"\\\\])*)\"";
            Match m = Regex.Match(section, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) return fallback;
            return Regex.Replace(m.Groups[1].Value, @"\\(.)", x => x.Groups[1].Value == "\\" ? "\\" :
                x.Groups[1].Value == "\"" ? "\"" : "\\" + x.Groups[1].Value);
        }

        private static int ReadDword(string section, string key, int fallback)
        {
            Match m = Regex.Match(section, "(?im)^\\\"" + Regex.Escape(key) + "\\\"=dword:([0-9a-f]{1,8})");
            uint value;
            return m.Success && uint.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value) && value <= int.MaxValue ? (int)value : fallback;
        }

        internal static void Install(ManagedFolderProfile p)
        {
            Validate(p, true);
            if (ClsidExists(p.Clsid) && !IsRustManagedClsid(p.Clsid))
                throw new InvalidOperationException("This CLSID belongs to another Windows component. Choose Generate new CLSID; no registry changes were made.");
            // Do not reserve PositionId under LegacyRoot. It is only used by the old
            // Rust application; the .reg export doesn't use it either. Saving modern
            // profiles in profiles.json avoids falsely rejecting perfectly valid
            // Explorer folders when an old installation already owns that number.
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            {
                foreach (string prefix in Environment.Is64BitOperatingSystem ?
                    new[] { MachineClasses, WowClasses } : new[] { MachineClasses })
                {
                    string root = prefix + @"\" + p.Clsid;
                    using (RegistryKey cls = machine.CreateSubKey(root))
                    {
                        cls.SetValue(null, p.Name);
                        cls.SetValue("InfoTip", "Show " + p.Name + ".");
                        cls.SetValue("System.IsPinnedtoNameSpaceTree", p.PinnedNavigation ? 1 : 0, RegistryValueKind.DWord);
                        cls.SetValue("SortOrderIndex", p.SortOrder, RegistryValueKind.DWord);
                        cls.SetValue("DescriptionID", p.DescriptionId, RegistryValueKind.DWord);
                        using (RegistryKey inst = cls.CreateSubKey("Instance"))
                        {
                            inst.SetValue("CLSID", ShellLinkClsid);
                            using (RegistryKey bag = inst.CreateSubKey("InitPropertyBag"))
                            {
                                bag.SetValue("Attributes", 0x15, RegistryValueKind.DWord);
                                bag.SetValue("Target", p.Path);
                            }
                        }
                        using (RegistryKey icon = cls.CreateSubKey("DefaultIcon"))
                            icon.SetValue(null, string.IsNullOrWhiteSpace(p.IconPath) ? DefaultIcon : p.IconPath);
                        using (RegistryKey server = cls.CreateSubKey("InProcServer32"))
                        { server.SetValue(null, "shdocvw.dll"); server.SetValue("ThreadingModel", "Both"); }
                        using (RegistryKey sheet = cls.CreateSubKey(@"ShellEx\PropertySheetHandlers\tab 1 general"))
                            sheet.SetValue(null, "{21B22460-3AEA-1069-A2DC-08002B30309D}");
                        using (RegistryKey sf = cls.CreateSubKey("ShellFolder"))
                        {
                            sf.SetValue("Attributes", unchecked((int)0xf080004d), RegistryValueKind.DWord);
                            sf.SetValue("HideAsDeletePerUser", ""); sf.SetValue("WantsFORPARSING", "");
                        }
                    }
                }
                if (p.ShowThisPc)
                { using (RegistryKey pc = machine.CreateSubKey(ThisPcRoot + @"\" + p.Clsid)) { } }
                else machine.DeleteSubKeyTree(ThisPcRoot + @"\" + p.Clsid, false);
                // LegacyRoot is import-only for new installations. In particular, do
                // not delete/move an older entry for this CLSID on profile updates:
                // the old program may still use that database.
            }
            using (RegistryKey desktop = Registry.CurrentUser.CreateSubKey(DesktopRoot + @"\" + p.Clsid))
                desktop.SetValue(null, p.Name);
            using (RegistryKey hidden = Registry.CurrentUser.CreateSubKey(DesktopHide))
                hidden.SetValue(p.Clsid, p.ShowDesktop ? 0 : 1, RegistryValueKind.DWord);
            NotifyExplorer();
        }

        internal static void Remove(string clsid)
        {
            clsid = CanonicalClsid(clsid);
            if (!IsRustManagedClsid(clsid))
                throw new InvalidOperationException("The CLSID is not a recognized managed/Rust folder. Removal is blocked to protect Windows components.");
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, MachineView))
            {
                // Only remove the known exact CLSID from the legacy DB, not arbitrary registry entries.
                using (RegistryKey db = machine.OpenSubKey(LegacyRoot, true))
                {
                    if (db != null)
                        foreach (string pos in db.GetSubKeyNames())
                        {
                            string entryClsid;
                            using (RegistryKey entry = db.OpenSubKey(pos))
                                entryClsid = entry?.GetValue("CLSID") as string;
                            if (string.Equals(entryClsid, clsid, StringComparison.OrdinalIgnoreCase))
                                db.DeleteSubKeyTree(pos, false);
                        }
                }
                machine.DeleteSubKeyTree(ThisPcRoot + @"\" + clsid, false);
                machine.DeleteSubKeyTree(MachineClasses + @"\" + clsid, false);
                if (Environment.Is64BitOperatingSystem)
                    machine.DeleteSubKeyTree(WowClasses + @"\" + clsid, false);
            }
            Registry.CurrentUser.DeleteSubKeyTree(DesktopRoot + @"\" + clsid, false);
            using (RegistryKey hidden = Registry.CurrentUser.OpenSubKey(DesktopHide, true))
                if (hidden != null && hidden.GetValueNames().Any(x => x.Equals(clsid, StringComparison.OrdinalIgnoreCase)))
                    hidden.DeleteValue(clsid, false);
            NotifyExplorer();
        }

        [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        internal static void NotifyExplorer()
        {
            // SHCNE_ASSOCCHANGED, SHCNF_IDLIST: refresh without forcibly killing Explorer windows.
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
