using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace ContextMenuManager.Methods
{
    sealed class DefaultMenuRemovalItemDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string IconLocation { get; set; }

        public Func<bool> CheckIsRemoved { get; set; }
        public Action RemoveAction { get; set; }
        public Action RestoreAction { get; set; }

        public bool IsRemoved()
        {
            try
            {
                return CheckIsRemoved != null && CheckIsRemoved();
            }
            catch
            {
                return false;
            }
        }

        public void Remove()
        {
            try { RemoveAction?.Invoke(); } catch { }
        }

        public void Restore()
        {
            try { RestoreAction?.Invoke(); } catch { }
        }
    }

    static class DefaultMenuRemovalManager
    {
        public static List<DefaultMenuRemovalItemDef> Items { get; } = new List<DefaultMenuRemovalItemDef>();

        static DefaultMenuRemovalManager()
        {
            InitFileRemovalItems();
            InitFolderAndDriveRemovalItems();
            InitDesktopRemovalItems();
            InitMediaRemovalItems();
            InitWindowsMediaPlayer11RemovalItem();
            InitSystemRemovalItems();
        }

        private static void DeleteTree(RegistryKey root, string subKey)
        {
            try { root.DeleteSubKeyTree(subKey, false); } catch { }
        }

        private static RegistryBackupTarget Target(string root, string path)
        {
            return new RegistryBackupTarget(root, path);
        }

        private static bool KeyExists(RegistryKey root, string path)
        {
            using (RegistryKey key = root.OpenSubKey(path, false)) return key != null;
        }

        private static bool RegistryValueExists(RegistryKey root, string path, string name)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path, false))
                    return key != null && Array.IndexOf(key.GetValueNames(), name) >= 0;
            }
            catch { return false; }
        }

        private static bool HasProgrammaticAccessOnly(RegistryKey root, string subKey)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subKey))
                    return key != null && key.GetValue("ProgrammaticAccessOnly", null) != null;
            }
            catch { return false; }
        }

        private static void SetProgrammaticAccessOnly(RegistryKey root, string subKey)
        {
            try
            {
                using (RegistryKey key = root.CreateSubKey(subKey))
                    key?.SetValue("ProgrammaticAccessOnly", string.Empty, RegistryValueKind.String);
            }
            catch { }
        }

        private static void RemoveProgrammaticAccessOnly(RegistryKey root, string subKey)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subKey, true))
                    key?.DeleteValue("ProgrammaticAccessOnly", false);
            }
            catch { }
        }

        private static void InitFileRemovalItems()
        {
            // 1. Print Menu (Print pe imagini, scripturi, documente)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePrintMenu",
                Name = "Print Context Menu",
                Category = "File",
                Description = "Elimină comanda rapidă 'Print' de pe fișiere text, imagini, fișiere batch și scripturi.",
                IconLocation = "shell32.dll,-17",
                CheckIsRemoved = () =>
                {
                    string[] paths = {
                        @"SystemFileAssociations\image\shell\print", @"batfile\shell\print", @"cmdfile\shell\print",
                        @"docxfile\shell\print", @"fonfile\shell\print", @"htmlfile\shell\print", @"inffile\shell\print",
                        @"inifile\shell\print", @"JSEFile\Shell\Print", @"otffile\shell\print", @"pfmfile\shell\print",
                        @"regfile\shell\print", @"rtffile\shell\print", @"ttcfile\shell\print", @"ttffile\shell\print",
                        @"txtfile\shell\print", @"VBEFile\Shell\Print", @"VBSFile\Shell\Print", @"WSFFile\Shell\Print"
                    };
                    foreach (string path in paths) if (KeyExists(Registry.ClassesRoot, path)) return false;
                    return true;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemovePrintMenu",
                    Target("HKCR", @"SystemFileAssociations\image\shell\print"), Target("HKCR", @"batfile\shell\print"),
                    Target("HKCR", @"cmdfile\shell\print"), Target("HKCR", @"docxfile\shell\print"),
                    Target("HKCR", @"fonfile\shell\print"), Target("HKCR", @"htmlfile\shell\print"),
                    Target("HKCR", @"inffile\shell\print"), Target("HKCR", @"inifile\shell\print"),
                    Target("HKCR", @"JSEFile\Shell\Print"), Target("HKCR", @"otffile\shell\print"),
                    Target("HKCR", @"pfmfile\shell\print"), Target("HKCR", @"regfile\shell\print"),
                    Target("HKCR", @"rtffile\shell\print"), Target("HKCR", @"ttcfile\shell\print"),
                    Target("HKCR", @"ttffile\shell\print"), Target("HKCR", @"txtfile\shell\print"),
                    Target("HKCR", @"VBEFile\Shell\Print"), Target("HKCR", @"VBSFile\Shell\Print"),
                    Target("HKCR", @"WSFFile\Shell\Print")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemovePrintMenu")
            });

            // 2. Send To Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveSendToMenu",
                Name = "Send To Context Menu",
                Category = "File",
                Description = "Elimină submeniul 'Send to' din meniul contextual al tuturor fișierelor.",
                IconLocation = "shell32.dll,-268",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo") &&
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}") &&
                    !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveSendToMenu",
                    Target("HKCR", @"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo"),
                    Target("HKCR", @"CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveSendToMenu")
            });

            // 3. Troubleshoot Compatibility
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveCompatibility",
                Name = "Troubleshoot Compatibility",
                Category = "File",
                Description = "Elimină asistentul de depanare a compatibilității de pe executabile, MSI și scripturi.",
                IconLocation = "msdt.exe,0",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"Msi.Package\shellex\ContextMenuHandlers\Compatibility") &&
                    !KeyExists(Registry.ClassesRoot, @"exefile\shellex\ContextMenuHandlers\Compatibility") &&
                    !KeyExists(Registry.ClassesRoot, @"batfile\shellex\ContextMenuHandlers\Compatibility") &&
                    !KeyExists(Registry.ClassesRoot, @"cmdfile\shellex\ContextMenuHandlers\Compatibility") &&
                    !KeyExists(Registry.ClassesRoot, @"MSILink\shellex\ContextMenuHandlers\{1d27f844-3a1f-4410-85ac-14651078412d}") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Classes\CLSID\{1d27f844-3a1f-4410-85ac-14651078412d}") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Classes\CompatContextMenu.CompatContextMenu.1\CLSID"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveCompatibility",
                    Target("HKCR", @"Msi.Package\shellex\ContextMenuHandlers\Compatibility"),
                    Target("HKCR", @"exefile\shellex\ContextMenuHandlers\Compatibility"),
                    Target("HKCR", @"batfile\shellex\ContextMenuHandlers\Compatibility"),
                    Target("HKCR", @"cmdfile\shellex\ContextMenuHandlers\Compatibility"),
                    Target("HKCR", @"MSILink\shellex\ContextMenuHandlers\{1d27f844-3a1f-4410-85ac-14651078412d}"),
                    Target("HKLM", @"SOFTWARE\Classes\CLSID\{1d27f844-3a1f-4410-85ac-14651078412d}"),
                    Target("HKLM", @"SOFTWARE\Classes\CompatContextMenu.CompatContextMenu.1\CLSID")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveCompatibility")
            });

            // 4. Edit (Notepad pe TXT și REG)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveEditMenu",
                Name = "Edit (Notepad pe Text & Regfile)",
                Category = "File",
                Description = "Elimină comanda implicită 'Edit' de deschidere în Notepad de pe fișierele .txt și .reg.",
                IconLocation = "notepad.exe,0",
                CheckIsRemoved = () =>
                {
                    using (var k1 = Registry.ClassesRoot.OpenSubKey(@"SystemFileAssociations\text\shell\edit"))
                    using (var k2 = Registry.ClassesRoot.OpenSubKey(@"regfile\shell\edit"))
                        return k1 == null && k2 == null;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveEditMenu",
                    Target("HKCR", @"SystemFileAssociations\text\shell\edit"),
                    Target("HKCR", @"regfile\shell\edit")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveEditMenu")
            });

            // This deletion appeared as an unrelated side-effect in Run app as....reg.
            // Keep it explicit and reversible instead of silently applying it while installing TrustedInstaller.
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveExeSetDesktopWallpaper",
                Name = "Executable 'Set desktop wallpaper' Verb",
                Category = "File",
                Description = "Removes the unrelated setdesktopwallpaper shell verb from executable files. The original key is backed up and can be restored.",
                IconLocation = "shell32.dll,-45",
                CheckIsRemoved = () => !KeyExists(Registry.ClassesRoot, @"exefile\shell\setdesktopwallpaper"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveExeSetDesktopWallpaper",
                    Target("HKCR", @"exefile\shell\setdesktopwallpaper")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveExeSetDesktopWallpaper")
            });

            // 5. Burn Disc Image (ISO)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveIsoBurn",
                Name = "Burn Disc Image (ISO)",
                Category = "File",
                Description = "Elimină comanda nativă Windows 'Burn disc image' de pe imaginile ISO.",
                IconLocation = "isoburn.exe,0",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"Windows.IsoFile\shell\burn"))
                        return k == null;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveIsoBurn",
                    Target("HKCR", @"Windows.IsoFile\shell\burn")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveIsoBurn")
            });

            // 6. Copy To & Move To Folder
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveCopyToMoveTo",
                Name = "Copy To Folder & Move To Folder",
                Category = "File",
                Description = "Elimină gestionarii shell 'Copy to folder' și 'Move to folder'.",
                IconLocation = "shell32.dll,4",
                CheckIsRemoved = () =>
                {
                    string[] ids = {
                        @"CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}",
                        @"CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}",
                        @"WOW6432Node\CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}",
                        @"WOW6432Node\CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}",
                        @"WOW6432Node\CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}",
                        @"WOW6432Node\CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}",
                        @"CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}",
                        @"CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}"
                    };
                    foreach (string path in ids) if (KeyExists(Registry.ClassesRoot, path)) return false;
                    return true;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveCopyToMoveTo",
                    Target("HKCR", @"CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}"),
                    Target("HKCR", @"CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}"),
                    Target("HKCR", @"CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}"),
                    Target("HKCR", @"CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveCopyToMoveTo")
            });

            // 7. NVIDIA Shortcuts Handler (.lnk)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveNvShortcuts",
                Name = "NVIDIA Context Menu pe Shortcuts (.lnk)",
                Category = "File",
                Description = "Elimină extensiile NVIDIA NvAppShExt și OpenGLShExt de pe comenzile rapide (.lnk).",
                IconLocation = "nvcpl.dll,0",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"lnkfile\shellex\ContextMenuHandlers\NvAppShExt") &&
                    !KeyExists(Registry.ClassesRoot, @"lnkfile\shellex\ContextMenuHandlers\OpenGLShExt"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveNvShortcuts",
                    Target("HKCR", @"lnkfile\shellex\ContextMenuHandlers\NvAppShExt"),
                    Target("HKCR", @"lnkfile\shellex\ContextMenuHandlers\OpenGLShExt")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveNvShortcuts")
            });
        }

        private static void InitFolderAndDriveRemovalItems()
        {
            // 8. Pin to Quick access + Add to Favorites
            // Uses ProgrammaticAccessOnly so Windows keeps the underlying verbs
            // available to the shell while hiding them from the context menu.
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "HideQuickAccessFavorites",
                Name = "Hide Quick Access / Add to Favorites",
                Category = "Folder",
                Description = "Hides 'Pin to Quick access' for folders, drives and network locations and hides 'Add to Favorites' for files. Restore removes only the ProgrammaticAccessOnly values.",
                IconLocation = "shell32.dll,-16739",
                CheckIsRemoved = () =>
                {
                    string[] quickAccessPaths = { @"Drive\shell\pintohome", @"Folder\shell\pintohome", @"Network\shell\pintohome" };
                    foreach (string path in quickAccessPaths)
                    {
                        if (!HasProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\" + path)) return false;
                        if (!HasProgrammaticAccessOnly(Registry.ClassesRoot, path)) return false;
                    }

                    if (!HasProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\*\shell\pintohomefile")) return false;
                    if (!HasProgrammaticAccessOnly(Registry.ClassesRoot, @"*\shell\pintohomefile")) return false;
                    return true;
                },
                RemoveAction = () =>
                {
                    string[] quickAccessPaths = { @"Drive\shell\pintohome", @"Folder\shell\pintohome", @"Network\shell\pintohome" };
                    foreach (string path in quickAccessPaths)
                    {
                        SetProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\" + path);
                        SetProgrammaticAccessOnly(Registry.ClassesRoot, path);
                    }

                    // Equivalent of the Windows 11 "Remove Add to Favorites" .reg tweak.
                    SetProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\*\shell\pintohomefile");
                    SetProgrammaticAccessOnly(Registry.ClassesRoot, @"*\shell\pintohomefile");
                },
                RestoreAction = () =>
                {
                    string[] quickAccessPaths = { @"Drive\shell\pintohome", @"Folder\shell\pintohome", @"Network\shell\pintohome" };
                    foreach (string path in quickAccessPaths)
                    {
                        RemoveProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\" + path);
                        RemoveProgrammaticAccessOnly(Registry.ClassesRoot, path);
                    }

                    RemoveProgrammaticAccessOnly(Registry.CurrentUser, @"Software\Classes\*\shell\pintohomefile");
                    RemoveProgrammaticAccessOnly(Registry.ClassesRoot, @"*\shell\pintohomefile");
                }
            });

            // 9. Previous Versions Tab & Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePreviousVersions",
                Name = "Previous Versions (Tab & Context Menu)",
                Category = "Folder",
                Description = "Elimină fila și comenzile contextuale 'Previous Versions' de pe foldere, fișiere și drive-uri.",
                IconLocation = "twext.dll,-1037",
                CheckIsRemoved = () =>
                {
                    string[] paths = {
                        @"CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"WOW6432Node\CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"WOW6432Node\CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\ContextMenuHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"WOW6432Node\CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"AllFilesystemObjects\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"Directory\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"Drive\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}",
                        @"Directory\shellex\PropertySheetHandlers\Offline Files",
                        @"Folder\shellex\PropertySheetHandlers\Offline Files"
                    };
                    foreach (string path in paths) if (KeyExists(Registry.ClassesRoot, path)) return false;
                    return !RegistryValueExists(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "NoPreviousVersionsPage") &&
                           !RegistryValueExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "NoPreviousVersionsPage") &&
                           !RegistryValueExists(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\PreviousVersions", "DisableLocalPage") &&
                           !RegistryValueExists(Registry.CurrentUser, @"Software\Policies\Microsoft\PreviousVersions", "DisableLocalPage");
                },
                RemoveAction = () =>
                {
                    RegistryTreeBackupManager.DeleteWithBackup("RemovePreviousVersions",
                        Target("HKCR", @"CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"WOW6432Node\CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"WOW6432Node\CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\ContextMenuHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"WOW6432Node\CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"AllFilesystemObjects\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"CLSID\{450D8FBA-AD25-11D0-98A8-0800361B1103}\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"Directory\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"Drive\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"),
                        Target("HKCR", @"Directory\shellex\PropertySheetHandlers\Offline Files"),
                        Target("HKCR", @"Folder\shellex\PropertySheetHandlers\Offline Files"));

                    RegistryValueBackupManager.DeleteWithBackup("RemovePreviousVersions.Policy",
                        new RegistryBackupValueTarget("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer", "NoPreviousVersionsPage"),
                        new RegistryBackupValueTarget("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "NoPreviousVersionsPage"),
                        new RegistryBackupValueTarget("HKLM", @"SOFTWARE\Policies\Microsoft\PreviousVersions", "DisableLocalPage"),
                        new RegistryBackupValueTarget("HKCU", @"Software\Policies\Microsoft\PreviousVersions", "DisableLocalPage"));
                },
                RestoreAction = () =>
                {
                    RegistryValueBackupManager.Restore("RemovePreviousVersions.Policy");
                    RegistryTreeBackupManager.Restore("RemovePreviousVersions");
                }
            });

            // 10. BitLocker Drive Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveBitLockerDrive",
                Name = "BitLocker Drive Operations (Lock/PIN)",
                Category = "Drive",
                Description = "Elimină comenzile BitLocker (change passphrase, pin, encrypt, manage, unlock) de pe partiții.",
                IconLocation = "shell32.dll,-48",
                CheckIsRemoved = () =>
                {
                    string[] verbs = { "change-passphrase", "change-pin", "encrypt-bde", "encrypt-bde-elev", "manage-bde", "resume-bde", "resume-bde-elev", "unlock-bde" };
                    foreach (string verb in verbs) if (KeyExists(Registry.ClassesRoot, @"Drive\shell\" + verb)) return false;
                    return true;
                },
                RemoveAction = () =>
                {
                    string[] verbs = { "change-passphrase", "change-pin", "encrypt-bde", "encrypt-bde-elev", "manage-bde", "resume-bde", "resume-bde-elev", "unlock-bde" };
                    var targets = new List<RegistryBackupTarget>();
                    foreach (string verb in verbs) targets.Add(Target("HKCR", @"Drive\shell\" + verb));
                    RegistryTreeBackupManager.DeleteWithBackup("RemoveBitLockerDrive", targets.ToArray());
                },
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveBitLockerDrive")
            });

            // 11. BitLocker Drive Unlock (EhStorShell)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveBitLockerUnlock",
                Name = "Enhanced Storage / BitLocker Unlock (EhStorShell)",
                Category = "Drive",
                Description = "Elimină extensia shell de deblocare prin parolă a unităților BitLocker / Enhanced Storage.",
                IconLocation = "EhStorShell.dll,-101",
                CheckIsRemoved = () =>
                {
                    return !KeyExists(Registry.ClassesRoot, @"CLSID\{2854F705-3548-414C-A113-93E27C808C85}") &&
                           !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{2854F705-3548-414C-A113-93E27C808C85}");
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveBitLockerUnlock",
                    Target("HKCR", @"CLSID\{2854F705-3548-414C-A113-93E27C808C85}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{2854F705-3548-414C-A113-93E27C808C85}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveBitLockerUnlock")
            });

            // 12. Open as Portable & PerfectDisk
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveOpenPortable",
                Name = "Open as Portable Device & PerfectDisk",
                Category = "Drive",
                Description = "Elimină handler-ul WPD 'Open as Portable Device' și comanda 'Optimize using PerfectDisk' de pe unități.",
                IconLocation = "wpdshext.dll,-511",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}") &&
                    !KeyExists(Registry.ClassesRoot, @"Drive\shell\Optimize using PerfectDisk") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveOpenPortable",
                    Target("HKLM", @"SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}"),
                    Target("HKCR", @"CLSID\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}"),
                    Target("HKCR", @"Drive\shell\Optimize using PerfectDisk")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveOpenPortable")
            });
        }

        private static void InitDesktopRemovalItems()
        {
            // 12. Desktop SearchBox (Bing Search)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveDesktopSearchBox",
                Name = "Desktop SearchBox (Bing Search)",
                Category = "Desktop",
                Description = "Elimină meniul de căutare web/local SearchBox integrat pe desktop.",
                IconLocation = "shell32.dll,-22",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}") &&
                    !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveDesktopSearchBox",
                    Target("HKCR", @"CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveDesktopSearchBox")
            });

            // 13. Controlled Folder Access
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveControlledFolderAccess",
                Name = "Controlled Folder Access (Desktop)",
                Category = "Desktop",
                Description = "Elimină scurtătura 'Controlled Folder Access' (Windows Defender) de pe desktop.",
                IconLocation = "imageres.dll,-5324",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\Shell\ControlledFolderAccess"))
                        return k == null;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveControlledFolderAccess",
                    Target("HKCR", @"DesktopBackground\Shell\ControlledFolderAccess")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveControlledFolderAccess")
            });
        }

        private static void InitMediaRemovalItems()
        {
            // 14. Play & Play to (Media Sharing)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePlayMedia",
                Name = "Play & Play To (Media Sharing)",
                Category = "Media",
                Description = "Elimină comenzile 'Play' și 'Play to' de pe directoarele și fișierele audio/video.",
                IconLocation = "wmploc.dll,-102",
                CheckIsRemoved = () =>
                {
                    string[] paths = {
                        @"CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}",
                        @"WOW6432Node\CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}",
                        @"SystemFileAssociations\Directory.Audio\shellex\ContextMenuHandlers\PlayTo",
                        @"SystemFileAssociations\Directory.Image\shellex\ContextMenuHandlers\PlayTo",
                        @"SystemFileAssociations\Directory.Video\shellex\ContextMenuHandlers\PlayTo",
                        @"SystemFileAssociations\audio\shell\Play",
                        @"SystemFileAssociations\Directory.Audio\shell\Play",
                        @"SystemFileAssociations\Directory.Image\shell\Play",
                        @"Stack.Audio\shell\Play",
                        @"Stack.Image\shell\Play",
                        @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell",
                        @"AppXk0g4vb8gvt7b93tg50ybcy892pge6jmt\Shell",
                        @"CLSID\{09A47860-11B0-4DA5-AFA5-26D86198A780}",
                        @"MediaCenter.WTVFile\shell",
                        @"SystemFileAssociations\audio\shell"
                    };
                    foreach (string path in paths) if (KeyExists(Registry.ClassesRoot, path)) return false;
                    return true;
                },
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemovePlayMedia",
                    Target("HKCR", @"CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}"),
                    Target("HKCR", @"SystemFileAssociations\Directory.Audio\shellex\ContextMenuHandlers\PlayTo"),
                    Target("HKCR", @"SystemFileAssociations\Directory.Image\shellex\ContextMenuHandlers\PlayTo"),
                    Target("HKCR", @"SystemFileAssociations\Directory.Video\shellex\ContextMenuHandlers\PlayTo"),
                    Target("HKCR", @"SystemFileAssociations\audio\shell\Play"),
                    Target("HKCR", @"SystemFileAssociations\Directory.Audio\shell\Play"),
                    Target("HKCR", @"SystemFileAssociations\Directory.Image\shell\Play"),
                    Target("HKCR", @"Stack.Audio\shell\Play"),
                    Target("HKCR", @"Stack.Image\shell\Play"),
                    Target("HKCR", @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell"),
                    Target("HKCR", @"AppXk0g4vb8gvt7b93tg50ybcy892pge6jmt\Shell"),
                    Target("HKCR", @"CLSID\{09A47860-11B0-4DA5-AFA5-26D86198A780}"),
                    Target("HKCR", @"MediaCenter.WTVFile\shell"),
                    Target("HKCR", @"SystemFileAssociations\audio\shell")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemovePlayMedia")
            });

            // 15. Cast to Device (PlayTo)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePlayToDevice",
                Name = "Edit with Clipchamp (Shell Handler)",
                Category = "Media",
                Description = "Elimină handler-ele COM folosite de comanda Clipchamp din meniurile fișierelor; păstrează o copie exactă pentru restaurare.",
                IconLocation = "playtomenu.dll,-101",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}") &&
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}") &&
                    !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}") &&
                    !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemovePlayToDevice",
                    Target("HKCR", @"CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}"),
                    Target("HKCR", @"CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemovePlayToDevice")
            });

            // 16. Windows Photos Shell (Create Video / Edit)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePhotosVideoEdit",
                Name = "Windows Photos (Create Video / Edit)",
                Category = "Media",
                Description = "Elimină 'Create a new video' și 'Edit with Photos' de pe fișierele imagine.",
                IconLocation = "imageres.dll,-72",
                CheckIsRemoved = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    foreach (string ext in exts)
                        if (KeyExists(Registry.LocalMachine, $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell")) return false;
                    return !KeyExists(Registry.ClassesRoot, @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellCreateVideo") &&
                           !KeyExists(Registry.ClassesRoot, @"AppXk0g4vb8gvt7b93tg50ybcy892pge6jmt\Shell\ShellCreateVideo") &&
                           !KeyExists(Registry.ClassesRoot, @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellEdit");
                },
                RemoveAction = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    var targets = new List<RegistryBackupTarget>();
                    foreach (string ext in exts) targets.Add(Target("HKLM", $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell"));
                    targets.Add(Target("HKCR", @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellCreateVideo"));
                    targets.Add(Target("HKCR", @"AppXk0g4vb8gvt7b93tg50ybcy892pge6jmt\Shell\ShellCreateVideo"));
                    targets.Add(Target("HKCR", @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellEdit"));
                    RegistryTreeBackupManager.DeleteWithBackup("RemovePhotosVideoEdit", targets.ToArray());
                },
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemovePhotosVideoEdit")
            });

            // 17. Edit with Paint 3D
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePaint3DEdit",
                Name = "Edit with Paint 3D",
                Category = "Media",
                Description = "Elimină comanda 'Edit with Paint 3D' din meniul fișierelor de imagine.",
                IconLocation = "mspaint.exe,0",
                CheckIsRemoved = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    foreach (string ext in exts)
                        if (KeyExists(Registry.LocalMachine, $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell\3D Edit")) return false;
                    return true;
                },
                RemoveAction = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    var targets = new List<RegistryBackupTarget>();
                    foreach (string ext in exts) targets.Add(Target("HKLM", $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell\3D Edit"));
                    RegistryTreeBackupManager.DeleteWithBackup("RemovePaint3DEdit", targets.ToArray());
                },
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemovePaint3DEdit")
            });
        }

        private static void InitWindowsMediaPlayer11RemovalItem()
        {
            // Complete native equivalent of Remove Windows Media Player 11 Shell.reg.
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveWindowsMediaPlayer11Shell",
                Name = "Windows Media Player 11 File Menus",
                Category = "Media",
                Description = "Elimină comenzile shell vechi Windows Media Player 11 pentru formatele media istorice; păstrează subcheile și valorile exacte pentru restaurare.",
                IconLocation = "wmploc.dll,-100",
                CheckIsRemoved = () =>
                {
                    string[] classes = {
                        "3G2", "3GP", "ADTS", "AIFF", "ASF", "ASX", "AU", "AVI", "FLAC", "M2TS", "m3u",
                        "M4A", "MIDI", "MK3D", "MKA", "MKV", "MOV", "MP3", "MP4", "MPEG", "TTS",
                        "WAV", "WAX", "WMA", "WMV", "WPL", "WVX"
                    };
                    foreach (string ext in classes)
                        if (KeyExists(Registry.ClassesRoot, @"WMP11.AssocFile." + ext + @"\shell")) return false;
                    return !KeyExists(Registry.ClassesRoot, @"WMP.DVR-MSFile\shell") &&
                           !KeyExists(Registry.ClassesRoot, @"WMP.WTVFile\shell");
                },
                RemoveAction = () =>
                {
                    string[] classes = {
                        "3G2", "3GP", "ADTS", "AIFF", "ASF", "ASX", "AU", "AVI", "FLAC", "M2TS", "m3u",
                        "M4A", "MIDI", "MK3D", "MKA", "MKV", "MOV", "MP3", "MP4", "MPEG", "TTS",
                        "WAV", "WAX", "WMA", "WMV", "WPL", "WVX"
                    };
                    var targets = new List<RegistryBackupTarget>();
                    foreach (string ext in classes) targets.Add(Target("HKCR", @"WMP11.AssocFile." + ext + @"\shell"));
                    targets.Add(Target("HKCR", @"WMP.DVR-MSFile\shell"));
                    targets.Add(Target("HKCR", @"WMP.WTVFile\shell"));
                    RegistryTreeBackupManager.DeleteWithBackup("RemoveWindowsMediaPlayer11Shell", targets.ToArray());
                },
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveWindowsMediaPlayer11Shell")
            });
        }

        private static void InitSystemRemovalItems()
        {
            // Native equivalent of Remove Windows Terminal UWP Menu.reg.
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveWindowsTerminalUwpMenu",
                Name = "Windows Terminal UWP Shell Integration",
                Category = "System",
                Description = "Elimină înregistrarea PackagedCom a integrării Windows Terminal UWP. Starea inițială este salvată pentru restaurare.",
                IconLocation = "wt.exe,0",
                CheckIsRemoved = () => !KeyExists(Registry.ClassesRoot,
                    @"PackagedCom\ClassIndex\{9F156763-7844-4DC4-B2B1-901F640F5155}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveWindowsTerminalUwpMenu",
                    Target("HKCR", @"PackagedCom\ClassIndex\{9F156763-7844-4DC4-B2B1-901F640F5155}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveWindowsTerminalUwpMenu")
            });

            // 18. Windows Defender Scan (EPP Context Menu)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveDefenderEPP",
                Name = "Windows Defender Scan (EPP)",
                Category = "System",
                Description = "Elimină comanda 'Scan with Microsoft Defender' din meniul fișierelor, folderelor și drive-urilor.",
                IconLocation = "imageres.dll,-5324",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"*\shellex\ContextMenuHandlers\EPP") &&
                    !KeyExists(Registry.ClassesRoot, @"Directory\shellex\ContextMenuHandlers\EPP") &&
                    !KeyExists(Registry.ClassesRoot, @"Drive\shellex\ContextMenuHandlers\EPP") &&
                    !KeyExists(Registry.ClassesRoot, @"*\shell\UpdateEncryptionSettingsWork") &&
                    !KeyExists(Registry.ClassesRoot, @"Directory\shell\UpdateEncryptionSettings"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveDefenderEPP",
                    Target("HKCR", @"*\shellex\ContextMenuHandlers\EPP"),
                    Target("HKCR", @"Directory\shellex\ContextMenuHandlers\EPP"),
                    Target("HKCR", @"Drive\shellex\ContextMenuHandlers\EPP"),
                    Target("HKCR", @"*\shell\UpdateEncryptionSettingsWork"),
                    Target("HKCR", @"Directory\shell\UpdateEncryptionSettings")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveDefenderEPP")
            });

            // 19. EFS Encryption / Decryption Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveEncryptionMenu",
                Name = "Encrypt / Decrypt (EFS Menu)",
                Category = "System",
                Description = "Elimină opțiunile de criptare și decriptare EFS din meniul contextual al fișierelor.",
                IconLocation = "imageres.dll,-59",
                CheckIsRemoved = () =>
                    !KeyExists(Registry.ClassesRoot, @"CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}") &&
                    !KeyExists(Registry.ClassesRoot, @"WOW6432Node\CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EncryptionMenu") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{578480AA-1B1C-4343-AABD-62C0A273DCB5}") &&
                    !KeyExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{811F592B-CDE7-4ca4-A6D4-7BB3F60AD8FB}"),
                RemoveAction = () => RegistryTreeBackupManager.DeleteWithBackup("RemoveEncryptionMenu",
                    Target("HKCR", @"CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}"),
                    Target("HKCR", @"WOW6432Node\CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}"),
                    Target("HKLM", @"SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EncryptionMenu"),
                    Target("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{578480AA-1B1C-4343-AABD-62C0A273DCB5}"),
                    Target("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{811F592B-CDE7-4ca4-A6D4-7BB3F60AD8FB}")),
                RestoreAction = () => RegistryTreeBackupManager.Restore("RemoveEncryptionMenu")
            });
        }

        public static List<DefaultMenuRemovalItemDef> GetItemsByCategory(string category)
        {
            if (string.IsNullOrEmpty(category) || category == "All" || category == "All Items")
                return Items;
            return Items.FindAll(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }
    }
}