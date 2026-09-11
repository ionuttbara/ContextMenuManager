using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace ContextMenuManager.Methods
{
    enum PresetCategory
    {
        Desktop,
        Folder,
        File,
        Drive
    }

    class RegistryValueItem
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public RegistryValueKind Kind { get; set; }

        public RegistryValueItem(string name, object value, RegistryValueKind kind = RegistryValueKind.String)
        {
            Name = name;
            Value = value;
            Kind = kind;
        }
    }

    class RegistryKeyEntry
    {
        public string SubPath { get; set; }
        public List<RegistryValueItem> Values { get; set; } = new List<RegistryValueItem>();

        public RegistryKeyEntry(string subPath = "")
        {
            SubPath = subPath;
        }

        public void Add(string name, object value, RegistryValueKind kind = RegistryValueKind.String)
        {
            Values.Add(new RegistryValueItem(name, value, kind));
        }
    }

    class CustomPresetParent
    {
        public string KeyPath { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconLocation { get; set; }
        public List<RegistryValueItem> Values { get; set; } = new List<RegistryValueItem>();

        public void EnsureInstalled()
        {
            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(KeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) return;
                foreach (var v in Values)
                {
                    if (v.Name == "@") key.SetValue("", v.Value, v.Kind);
                    else key.SetValue(v.Name, v.Value, v.Kind);
                }
            }
        }

        public void CheckAndCleanParent()
        {
            try
            {
                using (RegistryKey shellKey = Registry.ClassesRoot.OpenSubKey(KeyPath + @"\shell"))
                {
                    if (shellKey == null || shellKey.SubKeyCount == 0)
                    {
                        Registry.ClassesRoot.DeleteSubKeyTree(KeyPath, false);
                    }
                }
            }
            catch { }
        }
    }

    class CustomPreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PresetCategory Category { get; set; }
        public string ExtensionGroup { get; set; }
        public string SubMenuGroup { get; set; }
        public string IconLocation { get; set; }
        public CustomPresetParent ParentMenu { get; set; }
        public string RootKeyPath { get; set; }
        public List<RegistryKeyEntry> Entries { get; set; } = new List<RegistryKeyEntry>();
        public List<string> AdditionalDeletePaths { get; } = new List<string>();

        private static string ResolvePath(string rootPath, string subPath)
        {
            if (string.IsNullOrWhiteSpace(subPath)) return rootPath;

            var parts = new List<string>(rootPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (string raw in subPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = raw.Trim();
                if (part == ".") continue;
                if (part == "..")
                {
                    if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(part);
            }
            return string.Join(@"\", parts);
        }

        public bool IsActive()
        {
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(RootKeyPath))
            {
                if (key == null) return false;
                return key.GetValue("LegacyDisable") == null;
            }
        }

        public void Install()
        {
            if (ParentMenu != null)
            {
                ParentMenu.EnsureInstalled();
            }

            foreach (var entry in Entries)
            {
                string fullPath = ResolvePath(RootKeyPath, entry.SubPath);

                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(fullPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null) continue;
                    foreach (var val in entry.Values)
                    {
                        if (val.Name == "@") key.SetValue("", val.Value, val.Kind);
                        else key.SetValue(val.Name, val.Value, val.Kind);
                    }
                }
            }
        }

        public void Uninstall()
        {
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(RootKeyPath, false);
            }
            catch { }

            foreach (string path in AdditionalDeletePaths)
            {
                try { Registry.ClassesRoot.DeleteSubKeyTree(path, false); }
                catch { }
            }

            if (ParentMenu != null)
            {
                ParentMenu.CheckAndCleanParent();
            }
        }
    }

    static class CustomPresetsManager
    {
        public static List<CustomPreset> Presets { get; } = new List<CustomPreset>();

        static CustomPresetsManager()
        {
            InitFilePresets();
            InitFolderPresets();
            InitDrivePresets();
            InitDesktopPresets();
        }

        private static void InitFilePresets()
        {
            // === EXECUTABLES (.exe) ===
            var appRunParent = new CustomPresetParent
            {
                KeyPath = @"exefile\shell\AppRunConfig",
                Title = "Config Application Run",
                Description = "Cascaded context menu to configure application runtime settings (GPU, CPU Priority, Firewall Access)",
                IconLocation = "taskmgr.exe,1",
                Values =
                {
                    new RegistryValueItem("MUIVerb", "Config Application Run"),
                    new RegistryValueItem("SubCommands", ""),
                    new RegistryValueItem("Icon", "taskmgr.exe,1")
                }
            };

            var gpu = new CustomPreset
            {
                Id = "ExeGpu",
                Name = "Set GPU Usage",
                Description = "Configures DirectX preferences to launch application using Dedicated or Integrated GPU",
                Category = PresetCategory.File,
                ExtensionGroup = "Applications (.exe)",
                SubMenuGroup = "Config Application Run",
                IconLocation = "dxdiag.exe",
                ParentMenu = appRunParent,
                RootKeyPath = @"exefile\shell\AppRunConfig\shell\GPU"
            };
            var eGpu = new RegistryKeyEntry("");
            eGpu.Add("MUIVerb", "Set GPU usage...");
            eGpu.Add("SubCommands", "");
            eGpu.Add("Icon", "dxdiag.exe");
            gpu.Entries.Add(eGpu);
            var eGpu1 = new RegistryKeyEntry(@"shell\001flyout");
            eGpu1.Add("@", "Set to Dedicated GPU");
            gpu.Entries.Add(eGpu1);
            var eGpu1Cmd = new RegistryKeyEntry(@"shell\001flyout\command");
            eGpu1Cmd.Add("@", @"C:\Windows\System32\REG.exe ADD HKEY_CURRENT_USER\SOFTWARE\Microsoft\DirectX\UserGpuPreferences /f /v ""%1"" /d GpuPreference=2;");
            gpu.Entries.Add(eGpu1Cmd);
            var eGpu2 = new RegistryKeyEntry(@"shell\002flyout");
            eGpu2.Add("@", "Set to Integrated GPU");
            gpu.Entries.Add(eGpu2);
            var eGpu2Cmd = new RegistryKeyEntry(@"shell\002flyout\command");
            eGpu2Cmd.Add("@", @"C:\Windows\System32\REG.exe ADD HKEY_CURRENT_USER\SOFTWARE\Microsoft\DirectX\UserGpuPreferences /f /v ""%1"" /d GpuPreference=1;");
            gpu.Entries.Add(eGpu2Cmd);
            var eGpu3 = new RegistryKeyEntry(@"shell\003flyout");
            eGpu3.Add("@", "Reset GPU usage...");
            gpu.Entries.Add(eGpu3);
            var eGpu3Cmd = new RegistryKeyEntry(@"shell\003flyout\command");
            eGpu3Cmd.Add("@", @"C:\Windows\System32\REG.exe DELETE HKEY_CURRENT_USER\SOFTWARE\Microsoft\DirectX\UserGpuPreferences /v ""%1"" /f");
            gpu.Entries.Add(eGpu3Cmd);
            Presets.Add(gpu);

            var prio = new CustomPreset
            {
                Id = "ExePriority",
                Name = "Run with Priority",
                Description = "Adds options to launch the executable with specific CPU process priority (Realtime, High, Normal, Low)",
                Category = PresetCategory.File,
                ExtensionGroup = "Applications (.exe)",
                SubMenuGroup = "Config Application Run",
                IconLocation = "taskmgr.exe",
                ParentMenu = appRunParent,
                RootKeyPath = @"exefile\shell\AppRunConfig\shell\RunPrio"
            };
            var ePrio = new RegistryKeyEntry("");
            ePrio.Add("MUIVerb", "Run with Priority");
            ePrio.Add("SubCommands", "");
            ePrio.Add("Icon", "taskmgr.exe");
            prio.Entries.Add(ePrio);
            string[] prios = { "Realtime", "High", "Above normal", "Normal", "Below normal", "Low" };
            string[] flags = { "/Realtime", "/High", "/AboveNormal", "/Normal", "/BelowNormal", "/Low" };
            for (int i = 0; i < prios.Length; i++)
            {
                string fly = $@"shell\{i + 1:D3}flyout";
                var ef = new RegistryKeyEntry(fly);
                ef.Add("@", prios[i]);
                prio.Entries.Add(ef);
                var efc = new RegistryKeyEntry($@"{fly}\command");
                efc.Add("@", $@"cmd.exe /c start """" {flags[i]} ""%1""");
                prio.Entries.Add(efc);
            }
            Presets.Add(prio);

            var fw = new CustomPreset
            {
                Id = "ExeFirewall",
                Name = "Configure Internet Access",
                Description = "Block or restore outbound internet access for this application using Windows Firewall rules",
                Category = PresetCategory.File,
                ExtensionGroup = "Applications (.exe)",
                SubMenuGroup = "Config Application Run",
                IconLocation = @"%SystemRoot%\system32\FirewallControlPanel.dll,0",
                ParentMenu = appRunParent,
                RootKeyPath = @"exefile\shell\AppRunConfig\shell\WindowsFirewall"
            };
            var eFw = new RegistryKeyEntry("");
            eFw.Add("MUIVerb", "Configure Internet Access...");
            eFw.Add("icon", @"%SystemRoot%\system32\FirewallControlPanel.dll,0");
            eFw.Add("subcommands", "");
            fw.Entries.Add(eFw);
            var eBlock = new RegistryKeyEntry(@"Shell\block");
            eBlock.Add("MUIVerb", "Block internet access");
            eBlock.Add("icon", @"%SystemRoot%\system32\imageres.dll,100");
            fw.Entries.Add(eBlock);
            var eBlockCmd = new RegistryKeyEntry(@"Shell\block\command");
            eBlockCmd.Add("@", "\"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -Executionpolicy ByPass -WindowStyle Hidden -NoLogo -Command \"start powershell -Verb runas -ArgumentList \\\"-NoLogo -WindowStyle Hidden -command `\\\"New-NetFirewallRule -DisplayName ([System.IO.Path]::GetFilenameWithoutExtension('%1')) -Name '%1' -Enabled True -Direction Outbound -Action Block -Program '%1'`\\\"\\\"\"");
            fw.Entries.Add(eBlockCmd);
            var eRem = new RegistryKeyEntry(@"Shell\Remove");
            eRem.Add("MUIVerb", "Restore the internet access");
            eRem.Add("icon", @"%SystemRoot%\system32\imageres.dll,101");
            fw.Entries.Add(eRem);
            var eRemCmd = new RegistryKeyEntry(@"Shell\Remove\command");
            eRemCmd.Add("@", "\"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -Executionpolicy ByPass -WindowStyle Hidden -NoLogo -Command \"start powershell -Verb runas -ArgumentList \\\"-NoLogo -WindowStyle Hidden -command `\\\"Remove-NetFirewallRule -Name '%1'`\\\"\\\"\"");
            fw.Entries.Add(eRemCmd);
            Presets.Add(fw);

            var runAsParent = new CustomPresetParent
            {
                KeyPath = @"exefile\shell\AppRunConfigAs",
                Title = "Run App As...",
                Description = "Cascaded context menu to run executable under elevated or special administrative identities",
                IconLocation = "taskmgr.exe,1",
                Values =
                {
                    new RegistryValueItem("MUIVerb", "Run App As..."),
                    new RegistryValueItem("SubCommands", ""),
                    new RegistryValueItem("Icon", "taskmgr.exe,1")
                }
            };

            var ti = new CustomPreset
            {
                Id = "ExeTrustedInstaller",
                Name = "Run as TrustedInstaller",
                Description = "Execute application with NT SERVICE\\TrustedInstaller privileges via PowerShell",
                Category = PresetCategory.File,
                ExtensionGroup = "Applications (.exe)",
                SubMenuGroup = "Run App As...",
                IconLocation = "powershell.exe,0",
                ParentMenu = runAsParent,
                RootKeyPath = @"exefile\shell\AppRunConfigAs\shell\TrusterdIns"
            };
            var eTi = new RegistryKeyEntry("");
            eTi.Add("MUIVerb", "Run as trustedinstaller...");
            eTi.Add("HasLUAShield", "");
            eTi.Add("Icon", "powershell.exe,0");
            ti.Entries.Add(eTi);
            var eTiCmd = new RegistryKeyEntry("command");
            eTiCmd.Add("@", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -win 1 -nop -c iex((10..40|%{(gp 'Registry::HKCR\RunAsTI' $_ -ea 0).$_})-join[char]10); # --% ""%L""");
            ti.Entries.Add(eTiCmd);
            Presets.Add(ti);

            // === ALL FILES (*) ===
            var fileMgmtParent = new CustomPresetParent
            {
                KeyPath = @"*\shell\FileManagement",
                Title = "File Management",
                Description = "Cascaded file tools for hashing, permissions, and visibility",
                IconLocation = "shell32.dll,0",
                Values =
                {
                    new RegistryValueItem("Icon", "shell32.dll,0"),
                    new RegistryValueItem("MUIVerb", "File Management"),
                    new RegistryValueItem("Position", "Middle"),
                    new RegistryValueItem("SubCommands", "")
                }
            };

            var takeOwn = new CustomPreset
            {
                Id = "FileTakeOwnership",
                Name = "Take Ownership",
                Description = "Grant full administrator ownership and permissions for the selected file",
                Category = PresetCategory.File,
                ExtensionGroup = "All Files (*)",
                SubMenuGroup = "File Management",
                IconLocation = "imageres.dll,-5324",
                ParentMenu = fileMgmtParent,
                RootKeyPath = @"*\shell\FileManagement\shell\0TakeOwnership"
            };
            var eOwn = new RegistryKeyEntry("");
            eOwn.Add("@", "Take Ownership");
            eOwn.Add("HasLUAShield", "");
            eOwn.Add("NoWorkingDirectory", "");
            eOwn.Add("NeverDefault", "");
            takeOwn.Entries.Add(eOwn);
            var eOwnCmd = new RegistryKeyEntry("command");
            eOwnCmd.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\" && icacls \\\"%1\\\" /grant *S-1-3-4:F /t /c /l' -Verb runAs\"");
            takeOwn.Entries.Add(eOwnCmd);
            Presets.Add(takeOwn);

            var hashMenu = new CustomPreset
            {
                Id = "FileCalculateHash",
                Name = "Calculate Hash",
                Description = "Compute SHA1, SHA256, SHA384, SHA512, MD5, or RIPEMD160 hashes",
                Category = PresetCategory.File,
                ExtensionGroup = "All Files (*)",
                SubMenuGroup = "File Management",
                IconLocation = "shell32.dll,-16739",
                ParentMenu = fileMgmtParent,
                RootKeyPath = @"*\shell\FileManagement\shell\GetFileHash"
            };
            var eHash = new RegistryKeyEntry("");
            eHash.Add("MUIVerb", "Calculate Hash");
            eHash.Add("SubCommands", "");
            hashMenu.Entries.Add(eHash);
            string[] algs = { "SHA1", "SHA256", "SHA384", "SHA512", "MD5", "RIPEMD160" };
            for (int i = 0; i < algs.Length; i++)
            {
                string sub = $"shell\\0{i + 1}{algs[i]}";
                var ea = new RegistryKeyEntry(sub);
                ea.Add("MUIVerb", algs[i]);
                hashMenu.Entries.Add(ea);
                var eac = new RegistryKeyEntry($"{sub}\\command");
                eac.Add("@", $"powershell.exe -noexit get-filehash -literalpath '%1' -algorithm {algs[i]} | format-list");
                hashMenu.Entries.Add(eac);
            }
            Presets.Add(hashMenu);

            var clipContent = new CustomPreset
            {
                Id = "FileClipContent",
                Name = "Copy Content to Clipboard",
                Description = "Copies file content directly into Windows clipboard",
                Category = PresetCategory.File,
                ExtensionGroup = "All Files (*)",
                SubMenuGroup = "File Management",
                IconLocation = "DxpTaskSync.dll,-52",
                ParentMenu = fileMgmtParent,
                RootKeyPath = @"*\shell\FileManagement\shell\Copy Content to Clipboard"
            };
            var eClip = new RegistryKeyEntry("");
            eClip.Add("MUIVerb", "Copy Content to Clipboard");
            eClip.Add("Icon", "DxpTaskSync.dll,-52");
            eClip.Add("Position", "Center");
            clipContent.Entries.Add(eClip);
            var eClipCmd = new RegistryKeyEntry("Command");
            eClipCmd.Add("@", "cmd /c clip < \"%1\"");
            clipContent.Entries.Add(eClipCmd);
            Presets.Add(clipContent);

            var fileVis = new CustomPreset
            {
                Id = "FileVisibility",
                Name = "File Visibility (Attributes)",
                Description = "Set Not Hidden, Hidden, or System Hidden attributes on this file",
                Category = PresetCategory.File,
                ExtensionGroup = "All Files (*)",
                SubMenuGroup = "File Management",
                IconLocation = "imageres.dll,-5314",
                ParentMenu = fileMgmtParent,
                RootKeyPath = @"*\shell\FileManagement\shell\HiddenAttribute"
            };
            var eVis = new RegistryKeyEntry("");
            eVis.Add("MUIVerb", "File Visibility");
            eVis.Add("SubCommands", "");
            eVis.Add("Icon", "imageres.dll,-5314");
            fileVis.Entries.Add(eVis);
            var eV0 = new RegistryKeyEntry(@"Shell\Item0");
            eV0.Add("MUIVerb", "Not Hidden");
            fileVis.Entries.Add(eV0);
            var eV0C = new RegistryKeyEntry(@"Shell\Item0\Command");
            eV0C.Add("@", "attrib -s -h \"%1\"");
            fileVis.Entries.Add(eV0C);
            var eV1 = new RegistryKeyEntry(@"Shell\Item1");
            eV1.Add("MUIVerb", "Hidden");
            fileVis.Entries.Add(eV1);
            var eV1C = new RegistryKeyEntry(@"Shell\Item1\Command");
            eV1C.Add("@", "attrib -s +h \"%1\"");
            fileVis.Entries.Add(eV1C);
            var eV2 = new RegistryKeyEntry(@"Shell\Item2");
            eV2.Add("MUIVerb", "System Hidden");
            fileVis.Entries.Add(eV2);
            var eV2C = new RegistryKeyEntry(@"Shell\Item2\Command");
            eV2C.Add("@", "attrib +s +h \"%1\"");
            fileVis.Entries.Add(eV2C);
            Presets.Add(fileVis);

            var permDel = new CustomPreset
            {
                Id = "FilePermanentDelete",
                Name = "Permanent Delete",
                Description = "Bypass the Recycle Bin and permanently remove the selected file",
                Category = PresetCategory.File,
                ExtensionGroup = "All Files (*)",
                SubMenuGroup = "File Management",
                IconLocation = "shell32.dll,-240",
                ParentMenu = fileMgmtParent,
                RootKeyPath = @"*\shell\FileManagement\shell\Windows.PermanentDelete"
            };
            var eDel = new RegistryKeyEntry("");
            eDel.Add("CommandStateSync", "");
            eDel.Add("ExplorerCommandHandler", "{E9571AB2-AD92-4ec6-8924-4E5AD33790F5}");
            eDel.Add("Icon", "shell32.dll,-240");
            eDel.Add("Position", "Bottom");
            permDel.Entries.Add(eDel);
            Presets.Add(permDel);

            // === POWERSHELL SCRIPTS (.ps1) ===
            var ps1 = new CustomPreset
            {
                Id = "Ps1RunAs",
                Name = "Run with PowerShell as Administrator",
                Description = "Executes script (.ps1) elevated as Administrator with ExecutionPolicy Process Bypass",
                Category = PresetCategory.File,
                ExtensionGroup = "PowerShell Scripts (.ps1)",
                IconLocation = "powershell.exe,0",
                RootKeyPath = @"SystemFileAssociations\.ps1\Shell\runas"
            };
            var ePs1 = new RegistryKeyEntry("");
            ePs1.Add("HasLUAShield", "");
            ps1.Entries.Add(ePs1);
            var ePs1Cmd = new RegistryKeyEntry("command");
            ePs1Cmd.Add("@", @"powershell.exe ""-Command"" ""if((Get-ExecutionPolicy ) -ne 'AllSigned') { Set-ExecutionPolicy -Scope Process Bypass }; & '%1'""");
            ps1.Entries.Add(ePs1Cmd);
            Presets.Add(ps1);

            // === VBSCRIPTS (.vbs) ===
            var vbs = new CustomPreset
            {
                Id = "VbsRunAs",
                Name = "Run as Administrator (.vbs)",
                Description = "Executes VBScript (.vbs) elevated under administrative credentials with WScript.exe",
                Category = PresetCategory.File,
                ExtensionGroup = "VBScript (.vbs)",
                IconLocation = @"%SystemRoot%\System32\WScript.exe",
                RootKeyPath = @"VBSFile\Shell\runas"
            };
            var eVbs = new RegistryKeyEntry("");
            eVbs.Add("HasLUAShield", "");
            vbs.Entries.Add(eVbs);
            var eVbsCmd = new RegistryKeyEntry("command");
            eVbsCmd.Add("@", "\"%SystemRoot%\\System32\\WScript.exe\" \"%1\" %*", RegistryValueKind.ExpandString);
            vbs.Entries.Add(eVbsCmd);
            Presets.Add(vbs);

            // === MSI PACKAGES (.msi) ===
            var msi = new CustomPreset
            {
                Id = "MsiRunAs",
                Name = "Install as Administrator (.msi)",
                Description = "Launches Windows Installer package (.msi) with elevated administrative privileges",
                Category = PresetCategory.File,
                ExtensionGroup = "Windows Installer (.msi)",
                IconLocation = @"%SystemRoot%\System32\msiexec.exe",
                RootKeyPath = @"Msi.Package\Shell\runas"
            };
            var eMsi = new RegistryKeyEntry("");
            eMsi.Add("HasLUAShield", "");
            msi.Entries.Add(eMsi);
            var eMsiCmd = new RegistryKeyEntry("command");
            eMsiCmd.Add("@", "\"%SystemRoot%\\System32\\msiexec.exe\" /i \"%1\" %*", RegistryValueKind.ExpandString);
            msi.Entries.Add(eMsiCmd);
            Presets.Add(msi);

            var msiExtract = new CustomPreset
            {
                Id = "MsiExtract",
                Name = "Extract MSI Package",
                Description = "Extracts all files inside .msi installer into a folder named '[File] Contents'",
                Category = PresetCategory.File,
                ExtensionGroup = "Windows Installer (.msi)",
                IconLocation = "shell32.dll,-16817",
                RootKeyPath = @"Msi.Package\shell\Extract"
            };
            var eMsiExtCmd = new RegistryKeyEntry("command");
            eMsiExtCmd.Add("@", "msiexec.exe /a \"%1\" /qb TARGETDIR=\"%1 Contents\"");
            msiExtract.Entries.Add(eMsiExtCmd);
            Presets.Add(msiExtract);

            // === DLL & OCX REGISTRATION ===
            var dllReg = new CustomPreset
            {
                Id = "DllRegister",
                Name = "Register Server (regsvr32)",
                Description = "Registers DLL or OCX COM components into the system registry",
                Category = PresetCategory.File,
                ExtensionGroup = "DLL & OCX Libraries (.dll, .ocx)",
                IconLocation = "shell32.dll,-154",
                RootKeyPath = @"dllfile\shell\Register"
            };
            var eDllR = new RegistryKeyEntry("Command");
            eDllR.Add("@", "regsvr32.exe \"%1\"");
            dllReg.Entries.Add(eDllR);
            Presets.Add(dllReg);

            var dllUnreg = new CustomPreset
            {
                Id = "DllUnregister",
                Name = "Unregister Server (regsvr32 /u)",
                Description = "Unregisters DLL or OCX COM components from the system registry",
                Category = PresetCategory.File,
                ExtensionGroup = "DLL & OCX Libraries (.dll, .ocx)",
                IconLocation = "shell32.dll,-132",
                RootKeyPath = @"dllfile\shell\Unregister"
            };
            var eDllU = new RegistryKeyEntry("Command");
            eDllU.Add("@", "regsvr32.exe /u \"%1\"");
            dllUnreg.Entries.Add(eDllU);
            Presets.Add(dllUnreg);
        }

        private static void InitFolderPresets()
        {
            var folderMgmtParent = new CustomPresetParent
            {
                KeyPath = @"Directory\shell\FolderManagement",
                Title = "Folder Management",
                Description = "Cascaded folder utilities for quick cleanup, ownership, and deletion",
                IconLocation = "explorer.exe,0",
                Values =
                {
                    new RegistryValueItem("Icon", "explorer.exe,0"),
                    new RegistryValueItem("MUIVerb", "Folder Management"),
                    new RegistryValueItem("Position", "Middle"),
                    new RegistryValueItem("SubCommands", "")
                }
            };

            var emptyFolder = new CustomPreset
            {
                Id = "FolderEmptyContents",
                Name = "Empty folder contents",
                Description = "Delete all files in the current folder while preserving subfolders",
                Category = PresetCategory.Folder,
                ExtensionGroup = "Folder Utilities",
                SubMenuGroup = "Folder Management",
                IconLocation = "shell32.dll,-16715",
                ParentMenu = folderMgmtParent,
                RootKeyPath = @"Directory\shell\FolderManagement\shell\EmptyFolder"
            };
            var eEmp = new RegistryKeyEntry("");
            eEmp.Add("Icon", "shell32.dll,-16715");
            eEmp.Add("MUIVerb", "Empty folder");
            eEmp.Add("Position", "Top");
            emptyFolder.Entries.Add(eEmp);
            var eEmpCmd = new RegistryKeyEntry("command");
            eEmpCmd.Add("@", "cmd /c title Empty \"%1\" & (cmd /c echo. & echo This will permanently delete all contents in only this folder and not subfolders. & echo. & choice /c:yn /m \"Are you sure?\") & (if errorlevel 2 exit) & (cmd /c \"cd /d %1 && del /f /q *.*\")");
            emptyFolder.Entries.Add(eEmpCmd);
            Presets.Add(emptyFolder);

            var folderTakeOwn = new CustomPreset
            {
                Id = "FolderTakeOwnership",
                Name = "Take Ownership (Recursive)",
                Description = "Take full ownership and grant permissions to Administrators recursively",
                Category = PresetCategory.Folder,
                ExtensionGroup = "Folder Utilities",
                SubMenuGroup = "Folder Management",
                IconLocation = "imageres.dll,-5324",
                ParentMenu = folderMgmtParent,
                RootKeyPath = @"Directory\shell\FolderManagement\shell\TakeOwnership"
            };
            var eFOwn = new RegistryKeyEntry("");
            eFOwn.Add("@", "Take Ownership");
            eFOwn.Add("AppliesTo", "NOT (System.ItemPathDisplay:=\"C:\\Users\" OR System.ItemPathDisplay:=\"C:\\ProgramData\" OR System.ItemPathDisplay:=\"C:\\Windows\" OR System.ItemPathDisplay:=\"C:\\Windows\\System32\" OR System.ItemPathDisplay:=\"C:\\Program Files\" OR System.ItemPathDisplay:=\"C:\\Program Files (x86)\")");
            eFOwn.Add("HasLUAShield", "");
            eFOwn.Add("NoWorkingDirectory", "");
            eFOwn.Add("Position", "middle");
            folderTakeOwn.Entries.Add(eFOwn);
            var eFOwnCmd = new RegistryKeyEntry("command");
            eFOwnCmd.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\" /r /d y && icacls \\\"%1\\\" /grant *S-1-3-4:F /t /c /l /q' -Verb runAs\"");
            folderTakeOwn.Entries.Add(eFOwnCmd);
            Presets.Add(folderTakeOwn);

            var folderPermDel = new CustomPreset
            {
                Id = "FolderPermanentDelete",
                Name = "Permanent Delete",
                Description = "Permanently removes folder bypassing the Recycle Bin",
                Category = PresetCategory.Folder,
                ExtensionGroup = "Folder Utilities",
                SubMenuGroup = "Folder Management",
                IconLocation = "shell32.dll,-240",
                ParentMenu = folderMgmtParent,
                RootKeyPath = @"Directory\shell\FolderManagement\shell\Windows.PermanentDelete"
            };
            var eFDel = new RegistryKeyEntry("");
            eFDel.Add("CommandStateSync", "");
            eFDel.Add("ExplorerCommandHandler", "{E9571AB2-AD92-4ec6-8924-4E5AD33790F5}");
            eFDel.Add("Icon", "shell32.dll,-240");
            eFDel.Add("Position", "Bottom");
            folderPermDel.Entries.Add(eFDel);
            Presets.Add(folderPermDel);
        }

        private static void InitDrivePresets()
        {
            var driveTakeOwn = new CustomPreset
            {
                Id = "DriveTakeOwnership",
                Name = "Take Ownership (Drive)",
                Description = "Take full administrative control and repair permissions across entire drive",
                Category = PresetCategory.Drive,
                ExtensionGroup = "Drive Utilities",
                IconLocation = "shell32.dll,-9",
                RootKeyPath = @"Drive\shell\runas"
            };
            var eDrv = new RegistryKeyEntry("");
            eDrv.Add("@", "Take Ownership");
            eDrv.Add("HasLUAShield", "");
            eDrv.Add("NoWorkingDirectory", "");
            eDrv.Add("Position", "middle");
            eDrv.Add("AppliesTo", "NOT (System.ItemPathDisplay:=\"C:\\\")");
            driveTakeOwn.Entries.Add(eDrv);
            var eDrvCmd = new RegistryKeyEntry("command");
            eDrvCmd.Add("@", "cmd.exe /c takeown /f \"%1\\\" /r /d y && icacls \"%1\\\" /grant *S-1-3-4:F /t /c");
            driveTakeOwn.Entries.Add(eDrvCmd);
            Presets.Add(driveTakeOwn);
        }

        private static void InitDesktopPresets()
        {
            var kill = new CustomPreset
            {
                Id = "DesktopKillTasks",
                Name = "Kill all not responding tasks",
                Description = "Force terminates all hung, frozen, and unresponsive Windows processes immediately",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                IconLocation = "taskmgr.exe,-30651",
                RootKeyPath = @"DesktopBackground\shell\KillNRTasks"
            };
            var eKill = new RegistryKeyEntry("");
            eKill.Add("icon", "taskmgr.exe,-30651");
            eKill.Add("MUIverb", "Kill all not responding tasks");
            eKill.Add("Position", "Bottom");
            kill.Entries.Add(eKill);
            var eKillCmd = new RegistryKeyEntry("command");
            eKillCmd.Add("@", @"CMD.exe /C taskkill.exe /f /fi ""status eq Not Responding"" & Pause");
            kill.Entries.Add(eKillCmd);
            Presets.Add(kill);

            var devPrinters = new CustomPreset
            {
                Id = "DesktopDevicesPrinters",
                Name = "Devices and Printers",
                Description = "Open legacy Control Panel Devices and Printers shortcut directly from desktop context menu",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                IconLocation = "%systemroot%\\system32\\DeviceCenter.dll,-1",
                RootKeyPath = @"DesktopBackground\Shell\DevicesAndPrinters"
            };
            var eDev = new RegistryKeyEntry("");
            eDev.Add("MUIVerb", "Devices and Printers");
            eDev.Add("Icon", "%systemroot%\\system32\\DeviceCenter.dll,-1");
            devPrinters.Entries.Add(eDev);
            var eDevCmd = new RegistryKeyEntry("Command");
            eDevCmd.Add("@", "explorer.exe shell:::{A8A91A66-3A7D-4424-8D24-04E180695C7A}");
            devPrinters.Entries.Add(eDevCmd);
            Presets.Add(devPrinters);

            // === REFRESH/CONFIG SYSTEM COMPONENTS ===
            var sysParent = new CustomPresetParent
            {
                KeyPath = @"DesktopBackground\shell\RestartA",
                Title = "Refresh/Config System Components",
                Description = "Cascaded desktop submenu for network profile, firewall, HVCI security, and Explorer controls",
                IconLocation = "taskmgr.exe,2",
                Values =
                {
                    new RegistryValueItem("MUIVerb", "Refresh/Config System Components "),
                    new RegistryValueItem("Position", "Bottom"),
                    new RegistryValueItem("Icon", "taskmgr.exe,2"),
                    new RegistryValueItem("Subcommands", "")
                }
            };

            var fwOpt = new CustomPreset
            {
                Id = "DeskFw",
                Name = "Firewall Options",
                Description = "Shortcuts to turn on/off, reset, or configure allowed apps in Windows Firewall",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "FirewallControlPanel.dll,-1",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\RestartA\shell\Firewall"
            };
            var eFwR = new RegistryKeyEntry("");
            eFwR.Add("MUIVerb", "Firewall Options");
            eFwR.Add("Icon", "FirewallControlPanel.dll,-1");
            eFwR.Add("SubCommands", "");
            eFwR.Add("Position", "Bottom");
            fwOpt.Entries.Add(eFwR);
            var eF1 = new RegistryKeyEntry(@"shell\Command001");
            eF1.Add("MUIVerb", "Windows Firewall");
            eF1.Add("Icon", "FirewallControlPanel.dll,-1");
            fwOpt.Entries.Add(eF1);
            var eF1C = new RegistryKeyEntry(@"shell\Command001\Command");
            eF1C.Add("@", "RunDll32.exe shell32.dll,Control_RunDLL firewall.cpl");
            fwOpt.Entries.Add(eF1C);
            var eF2 = new RegistryKeyEntry(@"shell\Command002");
            eF2.Add("MUIVerb", "Windows Firewall with Advanced Security");
            eF2.Add("HasLUAShield", "");
            fwOpt.Entries.Add(eF2);
            var eF2C = new RegistryKeyEntry(@"shell\Command002\Command");
            eF2C.Add("@", "mmc.exe /s wf.msc");
            fwOpt.Entries.Add(eF2C);
            var eF3 = new RegistryKeyEntry(@"shell\Command003");
            eF3.Add("MUIVerb", "Configure Allowed Apps");
            eF3.Add("Icon", "FirewallControlPanel.dll,-1");
            fwOpt.Entries.Add(eF3);
            var eF3C = new RegistryKeyEntry(@"shell\Command003\Command");
            eF3C.Add("@", @"explorer.exe shell:::{4026492F-2F69-46B8-B9BF-5654FC07E423} -Microsoft.WindowsFirewall\pageConfigureApps");
            fwOpt.Entries.Add(eF3C);
            var eF4 = new RegistryKeyEntry(@"shell\Command004");
            eF4.Add("MUIVerb", "Turn On Windows Firewall");
            eF4.Add("HasLUAShield", "");
            eF4.Add("CommandFlags", 32, RegistryValueKind.DWord);
            fwOpt.Entries.Add(eF4);
            var eF4C = new RegistryKeyEntry(@"shell\Command004\Command");
            eF4C.Add("@", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,netsh advfirewall set allprofiles state on' -Verb runAs\"");
            fwOpt.Entries.Add(eF4C);
            var eF5 = new RegistryKeyEntry(@"shell\Command005");
            eF5.Add("MUIVerb", "Turn Off Windows Firewall");
            eF5.Add("HasLUAShield", "");
            fwOpt.Entries.Add(eF5);
            var eF5C = new RegistryKeyEntry(@"shell\Command005\Command");
            eF5C.Add("@", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,netsh advfirewall set allprofiles state off' -Verb runAs\"");
            fwOpt.Entries.Add(eF5C);
            var eF6 = new RegistryKeyEntry(@"shell\Command006");
            eF6.Add("MUIVerb", "Reset Windows Firewall");
            eF6.Add("HasLUAShield", "");
            fwOpt.Entries.Add(eF6);
            var eF6C = new RegistryKeyEntry(@"shell\Command006\Command");
            eF6C.Add("@", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,netsh advfirewall reset' -Verb runAs\"");
            fwOpt.Entries.Add(eF6C);
            Presets.Add(fwOpt);

            var hvci = new CustomPreset
            {
                Id = "DeskHvci",
                Name = "HVCI Security (Core Isolation)",
                Description = "Enable or disable Hypervisor-Protected Code Integrity with system reboot",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "imageres.dll,-5324",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\RestartA\shell\HVCIConfig"
            };
            var eHvci = new RegistryKeyEntry("");
            eHvci.Add("MUIVerb", "HVCI Security (Core Isolation)");
            eHvci.Add("Icon", "imageres.dll,-5324");
            eHvci.Add("SubCommands", "");
            eHvci.Add("Position", "Bottom");
            hvci.Entries.Add(eHvci);
            var eH1 = new RegistryKeyEntry(@"shell\01EnableHVCI");
            eH1.Add("MUIVerb", "Enable HVCI & Restart");
            eH1.Add("Icon", "imageres.dll,-1404");
            hvci.Entries.Add(eH1);
            var eH1C = new RegistryKeyEntry(@"shell\01EnableHVCI\command");
            eH1C.Add("@", "powershell -WindowStyle Hidden -Command \"Start-Process cmd -ArgumentList '/c reg add \\\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity\\\" /v \\\"Enabled\\\" /t REG_DWORD /d 1 /f & shutdown /r /t 0' -Verb RunAs\"");
            hvci.Entries.Add(eH1C);
            var eH2 = new RegistryKeyEntry(@"shell\02DisableHVCI");
            eH2.Add("MUIVerb", "Disable HVCI & Restart");
            eH2.Add("Icon", "imageres.dll,-1403");
            hvci.Entries.Add(eH2);
            var eH2C = new RegistryKeyEntry(@"shell\02DisableHVCI\command");
            eH2C.Add("@", "powershell -WindowStyle Hidden -Command \"Start-Process cmd -ArgumentList '/c reg add \\\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity\\\" /v \\\"Enabled\\\" /t REG_DWORD /d 0 /f & shutdown /r /t 0' -Verb RunAs\"");
            hvci.Entries.Add(eH2C);
            Presets.Add(hvci);

            var netP = new CustomPreset
            {
                Id = "DeskNetProfile",
                Name = "Network Profile (Public/Private)",
                Description = "Switch active network connection profile between Private (trusted) and Public",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "imageres.dll,-1020",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\RestartA\shell\NetProfile"
            };
            var eNet = new RegistryKeyEntry("");
            eNet.Add("MUIVerb", "Network Profile (Public/Private)");
            eNet.Add("Icon", "imageres.dll,-1020");
            eNet.Add("SubCommands", "");
            eNet.Add("Position", "Bottom");
            netP.Entries.Add(eNet);
            var eN1 = new RegistryKeyEntry(@"shell\01SetPrivate");
            eN1.Add("MUIVerb", "Private");
            eN1.Add("Icon", "imageres.dll,-5373");
            netP.Entries.Add(eN1);
            var eN1C = new RegistryKeyEntry(@"shell\01SetPrivate\command");
            eN1C.Add("@", "powershell -WindowStyle Hidden -Command \"Start-Process powershell -ArgumentList '-NoProfile -Command Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private' -Verb RunAs\"");
            netP.Entries.Add(eN1C);
            var eN2 = new RegistryKeyEntry(@"shell\02SetPublic");
            eN2.Add("MUIVerb", "Public");
            eN2.Add("Icon", "imageres.dll,-5302");
            netP.Entries.Add(eN2);
            var eN2C = new RegistryKeyEntry(@"shell\02SetPublic\command");
            eN2C.Add("@", "powershell -WindowStyle Hidden -Command \"Start-Process powershell -ArgumentList '-NoProfile -Command Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Public' -Verb RunAs\"");
            netP.Entries.Add(eN2C);
            Presets.Add(netP);

            var qa = new CustomPreset
            {
                Id = "DeskExplorerRestarter",
                Name = "Restart/Pause File Explorer",
                Description = "Instantly restart or pause the Windows Explorer shell process",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "explorer.exe",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\RestartA\shell\QuickAccess"
            };
            var eQa = new RegistryKeyEntry("");
            eQa.Add("icon", "explorer.exe");
            eQa.Add("Position", "Center");
            eQa.Add("SubCommands", "");
            eQa.Add("MUIVerb", "Restart/Pause File Explorer");
            qa.Entries.Add(eQa);
            var eQ1 = new RegistryKeyEntry(@"shell\01menu");
            eQ1.Add("MUIVerb", "Restart File Explorer");
            eQ1.Add("icon", "explorer.exe,1");
            qa.Entries.Add(eQ1);
            var eQ1Cmd = new RegistryKeyEntry(@"shell\01menu\command");
            eQ1Cmd.Add("@", "cmd.exe /c taskkill /f /im explorer.exe & start explorer.exe");
            qa.Entries.Add(eQ1Cmd);
            var eQ2 = new RegistryKeyEntry(@"shell\02menu");
            eQ2.Add("MUIVerb", "Pause File Explorer");
            eQ2.Add("icon", "explorer.exe,5");
            eQ2.Add("CommandFlags", 32, RegistryValueKind.DWord);
            qa.Entries.Add(eQ2);
            var eQ2Cmd = new RegistryKeyEntry(@"shell\02menu\command");
            eQ2Cmd.Add("@", "cmd.exe /c taskkill /f /im explorer.exe & echo Explorer paused. Press any key... & pause & start explorer.exe & exit");
            qa.Entries.Add(eQ2Cmd);
            Presets.Add(qa);

            var qaView = new CustomPreset
            {
                Id = "DeskHomeQAView",
                Name = "Home / Quick Access View",
                Description = "Show or hide the Home / Quick Access hub in File Explorer sidebar",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "imageres.dll,-1024",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\RestartA\shell\Restart Explorer"
            };
            var eQv = new RegistryKeyEntry("");
            eQv.Add("MUIVerb", "Home / Quick Access View");
            eQv.Add("Icon", "imageres.dll,-1024");
            eQv.Add("Position", "Bottom");
            eQv.Add("SubCommands", "");
            qaView.Entries.Add(eQv);
            var eQv1 = new RegistryKeyEntry(@"shell\01HideQA");
            eQv1.Add("MUIVerb", "Hide Home (Cleaner View)");
            eQv1.Add("Icon", "imageres.dll,-5302");
            qaView.Entries.Add(eQv1);
            var eQv1C = new RegistryKeyEntry(@"shell\01HideQA\command");
            eQv1C.Add("@", "powershell -WindowStyle Hidden -Command \"New-Item -Path 'HKCU:\\Software\\Classes\\CLSID\\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Force -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\\Software\\Classes\\CLSID\\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Name 'System.IsPinnedToNameSpaceTree' -Value 0; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'LaunchTo' -Value 1; Stop-Process -Name explorer\"");
            qaView.Entries.Add(eQv1C);
            var eQv2 = new RegistryKeyEntry(@"shell\02ShowQA");
            eQv2.Add("MUIVerb", "Show Home (Default)");
            eQv2.Add("Icon", "imageres.dll,-5373");
            qaView.Entries.Add(eQv2);
            var eQv2C = new RegistryKeyEntry(@"shell\02ShowQA\command");
            eQv2C.Add("@", "powershell -WindowStyle Hidden -Command \"Remove-Item -Path 'HKCU:\\Software\\Classes\\CLSID\\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Recurse -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'LaunchTo' -Value 2; Stop-Process -Name explorer\"");
            qaView.Entries.Add(eQv2C);
            Presets.Add(qaView);

            // === DEVICE SHUTDOWN MENU ===
            var shutdownParent = new CustomPresetParent
            {
                KeyPath = @"DesktopBackground\Shell\RestartB",
                Title = "Device Shutdown",
                Description = "Cascaded menu for quick shutdown, reboot, UEFI/BIOS restart, safe mode, and display sleep",
                IconLocation = "shell32.dll,-16739",
                Values =
                {
                    new RegistryValueItem("MUIVerb", "Device Shutdown"),
                    new RegistryValueItem("Icon", "shell32.dll,-16739"),
                    new RegistryValueItem("Position", "Bottom"),
                    new RegistryValueItem("SubCommands", "")
                }
            };

            var powerOps = new CustomPreset
            {
                Id = "DesktopPowerOps",
                Name = "Power & Reboot Options",
                Description = "Shut down, restart PC, or restart into Advanced Startup / UEFI BIOS",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Device Shutdown",
                IconLocation = "shell32.dll,-16739",
                ParentMenu = shutdownParent,
                RootKeyPath = @"DesktopBackground\Shell\RestartB\shell\001flyout"
            };
            powerOps.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\002flyout");
            powerOps.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\003flyout");
            powerOps.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\004flyout");
            powerOps.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\005flyout");
            var ePw1 = new RegistryKeyEntry("");
            ePw1.Add("MUIVerb", "Shut Down PC");
            powerOps.Entries.Add(ePw1);
            var ePw1C = new RegistryKeyEntry("command");
            ePw1C.Add("@", "shutdown /s /f /t 0");
            powerOps.Entries.Add(ePw1C);
            var ePw2 = new RegistryKeyEntry(@"..\002flyout");
            ePw2.Add("MUIVerb", "Restart PC");
            ePw2.Add("CommandFlags", 32, RegistryValueKind.DWord);
            powerOps.Entries.Add(ePw2);
            var ePw2C = new RegistryKeyEntry(@"..\002flyout\command");
            ePw2C.Add("@", "shutdown /r /t 0");
            powerOps.Entries.Add(ePw2C);
            var ePw3 = new RegistryKeyEntry(@"..\003flyout");
            ePw3.Add("MUIVerb", "Restart PC. After reboot, re-open unclosed apps.");
            ePw3.Add("CommandFlags", 32, RegistryValueKind.DWord);
            powerOps.Entries.Add(ePw3);
            var ePw3C = new RegistryKeyEntry(@"..\003flyout\command");
            ePw3C.Add("@", "shutdown /g /t 0");
            powerOps.Entries.Add(ePw3C);
            var ePw4 = new RegistryKeyEntry(@"..\004flyout");
            ePw4.Add("MUIVerb", "Restart to Advanced Startup Options");
            ePw4.Add("HasLUAShield", "");
            powerOps.Entries.Add(ePw4);
            var ePw4C = new RegistryKeyEntry(@"..\004flyout\command");
            ePw4C.Add("@", "shutdown /r /o /f /t 0");
            powerOps.Entries.Add(ePw4C);
            var ePw5 = new RegistryKeyEntry(@"..\005flyout");
            ePw5.Add("MUIVerb", "Restart to UEFI/BIOS");
            ePw5.Add("HasLUAShield", "");
            powerOps.Entries.Add(ePw5);
            var ePw5C = new RegistryKeyEntry(@"..\005flyout\command");
            ePw5C.Add("@", "shutdown /r /fw /f /t 0");
            powerOps.Entries.Add(ePw5C);
            Presets.Add(powerOps);

            var safeModes = new CustomPreset
            {
                Id = "DesktopSafeModes",
                Name = "Safe Mode Restart Options",
                Description = "Configures BCD and restarts system in Safe Mode (Minimal, Networking, or Command Prompt)",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Device Shutdown",
                IconLocation = "imageres.dll,-5324",
                ParentMenu = shutdownParent,
                RootKeyPath = @"DesktopBackground\Shell\RestartB\shell\006-SafeMode"
            };
            safeModes.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\006-NormalMode");
            safeModes.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\006-SafeModeNetworking");
            safeModes.AdditionalDeletePaths.Add(@"DesktopBackground\Shell\RestartB\shell\006-SafeModeCommandPrompt");
            var eSm1 = new RegistryKeyEntry("");
            eSm1.Add("@", "Restart in Safe Mode");
            eSm1.Add("HasLUAShield", "");
            safeModes.Entries.Add(eSm1);
            var eSm1C = new RegistryKeyEntry("command");
            eSm1C.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,bcdedit /set {current} safeboot minimal & bcdedit /deletevalue {current} safebootalternateshell & shutdown -r -t 00 -f' -Verb runAs\"");
            safeModes.Entries.Add(eSm1C);
            var eSmNorm = new RegistryKeyEntry(@"..\006-NormalMode");
            eSmNorm.Add("@", "Restart to normal mode (Exit to Safe Mode)");
            eSmNorm.Add("HasLUAShield", "");
            safeModes.Entries.Add(eSmNorm);
            var eSmNormC = new RegistryKeyEntry(@"..\006-NormalMode\command");
            eSmNormC.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,bcdedit /deletevalue {current} safeboot & bcdedit /deletevalue {current} safebootalternateshell & shutdown -r -t 00 -f' -Verb runAs\"");
            safeModes.Entries.Add(eSmNormC);

            var eSmNet = new RegistryKeyEntry(@"..\006-SafeModeNetworking");
            eSmNet.Add("@", "Restart in Safe Mode with Networking");
            eSmNet.Add("HasLUAShield", "");
            safeModes.Entries.Add(eSmNet);
            var eSmNetC = new RegistryKeyEntry(@"..\006-SafeModeNetworking\command");
            eSmNetC.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,bcdedit /set {current} safeboot network & bcdedit /deletevalue {current} safebootalternateshell & shutdown -r -t 00 -f' -Verb runAs\"");
            safeModes.Entries.Add(eSmNetC);

            var eSmCmd = new RegistryKeyEntry(@"..\006-SafeModeCommandPrompt");
            eSmCmd.Add("@", "Restart in Safe Mode with Command Prompt");
            eSmCmd.Add("HasLUAShield", "");
            safeModes.Entries.Add(eSmCmd);
            var eSmCmdC = new RegistryKeyEntry(@"..\006-SafeModeCommandPrompt\command");
            eSmCmdC.Add("@", "powershell -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/c,bcdedit /set {current} safeboot minimal & bcdedit /set {current} safebootalternateshell yes & shutdown -r -t 00 -f' -Verb runAs\"");
            safeModes.Entries.Add(eSmCmdC);
            Presets.Add(safeModes);

            var displayOff = new CustomPreset
            {
                Id = "DesktopTurnOffDisplay",
                Name = "Turn Off Display",
                Description = "Instantly sends standby power command to turn off connected monitors or lock workstation",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Device Shutdown",
                IconLocation = "imageres.dll,-109",
                ParentMenu = shutdownParent,
                RootKeyPath = @"DesktopBackground\Shell\RestartB\shell\007-TurnOffDisplay"
            };
            var eDsp = new RegistryKeyEntry("");
            eDsp.Add("Icon", "imageres.dll,-109");
            eDsp.Add("MUIVerb", "Turn off display");
            eDsp.Add("Position", "Bottom");
            eDsp.Add("SubCommands", "");
            displayOff.Entries.Add(eDsp);
            var eDsp1 = new RegistryKeyEntry(@"shell\01menu");
            eDsp1.Add("Icon", "powercpl.dll,-513");
            eDsp1.Add("MUIVerb", "Turn off display");
            displayOff.Entries.Add(eDsp1);
            var eDsp1C = new RegistryKeyEntry(@"shell\01menu\command");
            eDsp1C.Add("@", "cmd /c \"powershell.exe -Command \"(Add-Type '[DllImport(\\\"user32.dll\\\")]public static extern int SendMessage(int hWnd,int hMsg,int wParam,int lParam);' -Name a -Pas)::SendMessage(-1,0x0112,0xF170,2)\"\"");
            displayOff.Entries.Add(eDsp1C);
            var eDsp2 = new RegistryKeyEntry(@"shell\02menu");
            eDsp2.Add("MUIVerb", "Lock computer and Turn off display");
            eDsp2.Add("CommandFlags", 32, RegistryValueKind.DWord);
            eDsp2.Add("Icon", "imageres.dll,-59");
            displayOff.Entries.Add(eDsp2);
            var eDsp2C = new RegistryKeyEntry(@"shell\02menu\command");
            eDsp2C.Add("@", "cmd /c \"powershell.exe -Command \"(Add-Type '[DllImport(\\\"user32.dll\\\")]public static extern int SendMessage(int hWnd,int hMsg,int wParam,int lParam);' -Name a -Pas)::SendMessage(-1,0x0112,0xF170,2)\" & rundll32.exe user32.dll, LockWorkStation\"");
            displayOff.Entries.Add(eDsp2C);
            Presets.Add(displayOff);
        }

        public static List<CustomPreset> GetPresetsByCategory(PresetCategory category)
        {
            return Presets.FindAll(p => p.Category == category);
        }
    }
}