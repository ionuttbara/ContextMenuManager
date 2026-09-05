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
                string fullPath = string.IsNullOrEmpty(entry.SubPath)
                    ? RootKeyPath
                    : $@"{RootKeyPath}\{entry.SubPath}";

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
            InitExePresets();
            InitScriptPresets();
            InitDesktopPresets();
        }

        private static void InitExePresets()
        {
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

            // 1. Set GPU Usage[cite: 8]
            var gpu = new CustomPreset
            {
                Id = "ExeGpu",
                Name = "Set GPU Usage",
                Description = "Configures DirectX preference to launch this app using Dedicated or Integrated GPU",
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

            // 2. Run with Priority[cite: 9]
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

            // 3. Configure Internet Access[cite: 9]
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

            // 4. Run as TrustedInstaller[cite: 7]
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
        }

        private static void InitScriptPresets()
        {
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

            var msi = new CustomPreset
            {
                Id = "MsiRunAs",
                Name = "Install / Run as Administrator (.msi)",
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
                Description = "Desktop submenu with shortcuts to turn on/off, reset, or open advanced Firewall settings",
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
                Description = "Enables or disables Hypervisor-Protected Code Integrity (Device Guard) with reboot",
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
                Description = "Submenu to set current active network adapter profile to Private (trusted) or Public (restricted)",
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
                Description = "Desktop command to instantly restart or pause the Windows Explorer process",
                Category = PresetCategory.Desktop,
                ExtensionGroup = "Desktop Background",
                SubMenuGroup = "Refresh/Config System Components",
                IconLocation = "explorer.exe",
                ParentMenu = sysParent,
                RootKeyPath = @"DesktopBackground\shell\QuickAccess"
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
            eQ2Cmd.Add("@", "cmd.exe /c taskkill /f /im explorer.exe & echo Explorer oprit. Apasa orice tasta... & pause & start explorer.exe & exit");
            qa.Entries.Add(eQ2Cmd);
            Presets.Add(qa);

            var qaView = new CustomPreset
            {
                Id = "DeskHomeQAView",
                Name = "Home / Quick Access View",
                Description = "Desktop submenu to show or hide the Home / Quick Access node in Explorer sidebar",
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
        }

        public static List<CustomPreset> GetPresetsByCategory(PresetCategory category)
        {
            return Presets.FindAll(p => p.Category == category);
        }
    }
}