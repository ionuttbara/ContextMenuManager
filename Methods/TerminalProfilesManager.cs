using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContextMenuManager.Methods
{
    sealed class TerminalProfile
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public string CommandLine { get; set; }
        public string Icon { get; set; }
        public bool Hidden { get; set; }
        public bool IsWindowsTerminal { get; set; }
        public string KeyName { get; set; }
        public string Verb { get; set; }
        public string CommandString { get; set; }
        public string IconLocation { get; set; }
        public string Description { get; set; }
    }

    static class TerminalProfilesManager
    {
        public const string DefaultTerminalIcon = "cmd.exe,0";

        private static readonly string[] ParentRoots = new[]
        {
            @"Directory\shell\GalleryTerminalBehaivor",
            @"Directory\Background\shell\GalleryTerminalBehaivor",
            @"Drive\shell\GalleryTerminalBehaivor"
        };

        public static bool DetectTerminal(out string editionName, out string settingsPath, out string wtExe)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var candidates = new[]
            {
                new {
                    Edition = "Windows Terminal",
                    Settings = Path.Combine(localAppData, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json"),
                    Exe = Path.Combine(localAppData, @"Microsoft\WindowsApps\Microsoft.WindowsTerminal_8wekyb3d8bbwe\wt.exe")
                },
                new {
                    Edition = "Windows Terminal Preview",
                    Settings = Path.Combine(localAppData, @"Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json"),
                    Exe = Path.Combine(localAppData, @"Microsoft\WindowsApps\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\wt.exe")
                },
                new {
                    Edition = "Windows Terminal Canary",
                    Settings = Path.Combine(localAppData, @"Packages\Microsoft.WindowsTerminalCanary_8wekyb3d8bbwe\LocalState\settings.json"),
                    Exe = Path.Combine(localAppData, @"Microsoft\WindowsApps\Microsoft.WindowsTerminalCanary_8wekyb3d8bbwe\wt.exe")
                },
                new {
                    Edition = "Windows Terminal Dev",
                    Settings = Path.Combine(localAppData, @"Packages\Microsoft.WindowsTerminalDev_8wekyb3d8bbwe\LocalState\settings.json"),
                    Exe = Path.Combine(localAppData, @"Microsoft\WindowsApps\Microsoft.WindowsTerminalDev_8wekyb3d8bbwe\wt.exe")
                },
                new {
                    Edition = "Windows Terminal (Unpackaged)",
                    Settings = Path.Combine(localAppData, @"Microsoft\Windows Terminal\settings.json"),
                    Exe = "wt.exe"
                }
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c.Settings))
                {
                    editionName = c.Edition;
                    settingsPath = c.Settings;
                    wtExe = File.Exists(c.Exe) ? c.Exe : "wt.exe";
                    return true;
                }
            }

            editionName = null;
            settingsPath = null;
            wtExe = "cmd.exe";
            return false;
        }

        public static List<TerminalProfile> GetAvailableProfiles()
        {
            var list = new List<TerminalProfile>();

            bool hasTerminal = DetectTerminal(out string edition, out string settingsPath, out string wtExe);

            if (hasTerminal && File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                    string cleaned = StripCommentsAndTrailingCommas(json);

                    byte[] bytes = Encoding.UTF8.GetBytes(cleaned);
                    using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, new System.Xml.XmlDictionaryReaderQuotas()))
                    {
                        XElement root = XElement.Load(reader);

                        IEnumerable<XElement> profileNodes = root.Element("profiles")?.Element("list")?.Elements("item");
                        if (profileNodes == null || !profileNodes.Any())
                        {
                            profileNodes = root.Element("profiles")?.Elements("item");
                        }
                        if (profileNodes == null || !profileNodes.Any())
                        {
                            profileNodes = root.Descendants("profiles").Descendants("item");
                        }

                        if (profileNodes != null)
                        {
                            foreach (var node in profileNodes)
                            {
                                string name = node.Element("name")?.Value;
                                if (string.IsNullOrWhiteSpace(name)) continue;

                                string hiddenVal = node.Element("hidden")?.Value;
                                if (string.Equals(hiddenVal, "true", StringComparison.OrdinalIgnoreCase)) continue;

                                string guid = node.Element("guid")?.Value ?? "";
                                string icon = node.Element("icon")?.Value ?? "";
                                string cmdLine = node.Element("commandline")?.Value ?? "";

                                string safeKey = "WT_" + Regex.Replace(name, @"[^\w]", "");
                                if (safeKey == "WT_") safeKey = "WT_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                                string resolvedIcon = ResolveIcon(name, icon);

                                list.Add(new TerminalProfile
                                {
                                    Guid = guid,
                                    Name = name,
                                    CommandLine = cmdLine,
                                    Icon = icon,
                                    Hidden = false,
                                    IsWindowsTerminal = true,
                                    KeyName = safeKey,
                                    Verb = $"Open {name} here...",
                                    CommandString = $"\"{wtExe}\" -p \"{name}\" -d \"%V\"",
                                    IconLocation = string.IsNullOrEmpty(resolvedIcon) ? DefaultTerminalIcon : resolvedIcon,
                                    Description = $"Open {name} using {edition} in current folder"
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // Standalone Fallbacks (cmd.exe,0 as default)
            list.Add(new TerminalProfile
            {
                Name = "Command Prompt (Classic CMD)",
                KeyName = "cmd2",
                Verb = "Open CMD Window here...",
                CommandString = "cmd.exe /s /k pushd \"%V\"",
                IconLocation = DefaultTerminalIcon,
                IsWindowsTerminal = false,
                Description = "Open native Command Prompt window in current directory"
            });

            list.Add(new TerminalProfile
            {
                Name = "PowerShell 5 (Classic)",
                KeyName = "Powershell",
                Verb = "Open PowerShell 5 Window here...",
                CommandString = "powershell.exe -noexit -command Set-Location -literalPath '%V'",
                IconLocation = "powershell.exe",
                IsWindowsTerminal = false,
                Description = "Open native Windows PowerShell 5 console in current directory"
            });

            return list;
        }

        private static string StripCommentsAndTrailingCommas(string json)
        {
            var re = new Regex(@"(@(?:""[^""]*"")+|""(?:[^""\\]|\\.)*"")|//.*|/\*(?s).*?\*/");
            string noComments = re.Replace(json, me => me.Groups[1].Success ? me.Groups[1].Value : "");
            return Regex.Replace(noComments, @",\s*([\]}])", "$1");
        }

        private static string ResolveIcon(string name, string iconPath)
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                string exp = Environment.ExpandEnvironmentVariables(iconPath);
                if (File.Exists(exp)) return exp;
            }

            string lower = name.ToLowerInvariant();
            if (lower.Contains("powershell") || lower.Contains("pwsh")) return "powershell.exe";
            if (lower.Contains("cmd") || lower.Contains("command prompt")) return DefaultTerminalIcon;
            if (lower.Contains("ubuntu") || lower.Contains("debian") || lower.Contains("kali") || lower.Contains("wsl") || lower.Contains("linux")) return "wsl.exe";

            return DefaultTerminalIcon;
        }

        public static bool IsProfileInstalled(TerminalProfile profile)
        {
            using (var key = Registry.ClassesRoot.OpenSubKey($@"Directory\shell\GalleryTerminalBehaivor\shell\{profile.KeyName}"))
            {
                return key != null;
            }
        }

        public static void InstallProfile(TerminalProfile profile)
        {
            foreach (var root in ParentRoots)
            {
                using (var parent = Registry.ClassesRoot.CreateSubKey(root, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (parent == null) continue;
                    parent.SetValue("MUIVerb", "Open terminal in place...");
                    parent.SetValue("Icon", DefaultTerminalIcon);
                    parent.SetValue("Position", "Middle");
                    parent.SetValue("SubCommands", "");
                }

                string itemPath = $@"{root}\shell\{profile.KeyName}";
                using (var item = Registry.ClassesRoot.CreateSubKey(itemPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (item == null) continue;
                    item.SetValue("", profile.Verb);
                    item.SetValue("Icon", string.IsNullOrEmpty(profile.IconLocation) ? DefaultTerminalIcon : profile.IconLocation);
                    item.SetValue("NoWorkingDirectory", "");
                    if (profile.KeyName == "Powershell")
                    {
                        item.SetValue("ShowBasedOnVelocityId", 0x00639bc8, RegistryValueKind.DWord);
                    }

                    using (var cmd = item.CreateSubKey("command"))
                    {
                        cmd?.SetValue("", profile.CommandString);
                    }
                }
            }
        }

        public static void UninstallProfile(TerminalProfile profile)
        {
            foreach (var root in ParentRoots)
            {
                string itemPath = $@"{root}\shell\{profile.KeyName}";
                try { Registry.ClassesRoot.DeleteSubKeyTree(itemPath, false); } catch { }

                try
                {
                    using (var shellKey = Registry.ClassesRoot.OpenSubKey($@"{root}\shell"))
                    {
                        if (shellKey == null || shellKey.SubKeyCount == 0)
                        {
                            Registry.ClassesRoot.DeleteSubKeyTree(root, false);
                        }
                    }
                }
                catch { }
            }
        }

        public static bool BackupProfiles(string destinationPath)
        {
            try
            {
                if (DetectTerminal(out _, out string settingsPath, out _))
                {
                    if (File.Exists(settingsPath))
                    {
                        File.Copy(settingsPath, destinationPath, true);
                        return true;
                    }
                }

                var profiles = GetAvailableProfiles();
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"generator\": \"ContextMenuManager\",");
                sb.AppendLine($"  \"backupDate\": \"{DateTime.Now:O}\",");
                sb.AppendLine("  \"profiles\": [");
                for (int i = 0; i < profiles.Count; i++)
                {
                    var p = profiles[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"name\": \"{p.Name}\",");
                    sb.AppendLine($"      \"command\": \"{p.CommandString.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
                    sb.AppendLine($"      \"icon\": \"{(p.IconLocation ?? DefaultTerminalIcon).Replace("\\", "\\\\")}\"");
                    sb.Append("    }");
                    if (i < profiles.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreProfiles(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return false;

                if (DetectTerminal(out _, out string settingsPath, out _))
                {
                    if (File.Exists(settingsPath))
                    {
                        string backupBeforeRestore = settingsPath + ".bak";
                        File.Copy(settingsPath, backupBeforeRestore, true);
                    }
                    File.Copy(sourcePath, settingsPath, true);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTerminalBlocked()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked"))
            {
                return key != null && key.GetValue("{9F156763-7844-4DC4-B2B1-901F640F5155}") != null;
            }
        }

        public static void SetTerminalBlocked(bool block)
        {
            if (block)
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key?.SetValue("{9F156763-7844-4DC4-B2B1-901F640F5155}", "");
                }

                string[] cleanups = new[]
                {
                    @"Directory\shell\cmd",
                    @"Directory\shell\Powershell",
                    @"Directory\shell\WSL",
                    @"Directory\Background\shell\OpenWindowsTerminalProfiles",
                    @"Directory\Background\shell\Powershell",
                    @"Directory\Background\shell\WSL",
                    @"Drive\shell\cmd",
                    @"Drive\shell\Powershell",
                    @"Drive\shell\WSL"
                };
                foreach (var c in cleanups)
                {
                    try { Registry.ClassesRoot.DeleteSubKeyTree(c, false); } catch { }
                }
            }
            else
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", true))
                    {
                        key?.DeleteValue("{9F156763-7844-4DC4-B2B1-901F640F5155}", false);
                    }
                }
                catch { }
            }
        }
    }
}