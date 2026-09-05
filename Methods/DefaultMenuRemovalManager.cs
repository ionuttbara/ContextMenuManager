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
            InitSystemRemovalItems();
        }

        private static void DeleteTree(RegistryKey root, string subKey)
        {
            try { root.DeleteSubKeyTree(subKey, false); } catch { }
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
                    using (var k1 = Registry.ClassesRoot.OpenSubKey(@"txtfile\shell\print"))
                    using (var k2 = Registry.ClassesRoot.OpenSubKey(@"batfile\shell\print"))
                    {
                        return k1 == null && k2 == null;
                    }
                },
                RemoveAction = () =>
                {
                    string[] targets = {
                        @"SystemFileAssociations\image\shell\print", @"batfile\shell\print", @"cmdfile\shell\print",
                        @"docxfile\shell\print", @"fonfile\shell\print", @"htmlfile\shell\print", @"inffile\shell\print",
                        @"inifile\shell\print", @"JSEFile\Shell\Print", @"otffile\shell\print", @"pfmfile\shell\print",
                        @"regfile\shell\print", @"rtffile\shell\print", @"ttcfile\shell\print", @"ttffile\shell\print",
                        @"txtfile\shell\print", @"VBEFile\Shell\Print", @"VBSFile\Shell\Print", @"WSFFile\Shell\Print"
                    };
                    foreach (var t in targets) DeleteTree(Registry.ClassesRoot, t);
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"txtfile\shell\print\command"))
                        k?.SetValue("", @"%SystemRoot%\system32\NOTEPAD.EXE /p %1");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"batfile\shell\print\command"))
                        k?.SetValue("", @"%SystemRoot%\system32\NOTEPAD.EXE /p %1");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"regfile\shell\print\command"))
                        k?.SetValue("", @"%SystemRoot%\system32\NOTEPAD.EXE /p %1");
                }
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
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo"))
                        k?.SetValue("", "{7BA4C740-9E81-11CF-99D3-00AA004AE837}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{7BA4C740-9E81-11CF-99D3-00AA004AE837}\InProcServer32"))
                    {
                        k?.SetValue("", "shell32.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
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
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"exefile\shellex\ContextMenuHandlers\Compatibility"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"Msi.Package\shellex\ContextMenuHandlers\Compatibility");
                    DeleteTree(Registry.ClassesRoot, @"exefile\shellex\ContextMenuHandlers\Compatibility");
                    DeleteTree(Registry.ClassesRoot, @"batfile\shellex\ContextMenuHandlers\Compatibility");
                    DeleteTree(Registry.ClassesRoot, @"cmdfile\shellex\ContextMenuHandlers\Compatibility");
                    DeleteTree(Registry.ClassesRoot, @"MSILink\shellex\ContextMenuHandlers\{1d27f844-3a1f-4410-85ac-14651078412d}");
                    DeleteTree(Registry.LocalMachine, @"SOFTWARE\Classes\CLSID\{1d27f844-3a1f-4410-85ac-14651078412d}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"exefile\shellex\ContextMenuHandlers\Compatibility"))
                        k?.SetValue("", "{1d27f844-3a1f-4410-85ac-14651078412d}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"batfile\shellex\ContextMenuHandlers\Compatibility"))
                        k?.SetValue("", "{1d27f844-3a1f-4410-85ac-14651078412d}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"Msi.Package\shellex\ContextMenuHandlers\Compatibility"))
                        k?.SetValue("", "{1d27f844-3a1f-4410-85ac-14651078412d}");
                }
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
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\text\shell\edit");
                    DeleteTree(Registry.ClassesRoot, @"regfile\shell\edit");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"SystemFileAssociations\text\shell\edit\command"))
                        k?.SetValue("", @"%SystemRoot%\system32\NOTEPAD.EXE %1");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"regfile\shell\edit\command"))
                        k?.SetValue("", @"%SystemRoot%\system32\NOTEPAD.EXE %1");
                }
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
                RemoveAction = () => DeleteTree(Registry.ClassesRoot, @"Windows.IsoFile\shell\burn"),
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"Windows.IsoFile\shell\burn"))
                    {
                        k?.SetValue("MUIVerb", "@isoburn.exe,-351");
                        k?.SetValue("Icon", "isoburn.exe,0");
                        using (var cmd = k?.CreateSubKey("command"))
                            cmd?.SetValue("", @"%SystemRoot%\system32\isoburn.exe ""%1""");
                    }
                }
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
                    using (var k1 = Registry.ClassesRoot.OpenSubKey(@"CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}"))
                    using (var k2 = Registry.ClassesRoot.OpenSubKey(@"CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}"))
                        return k1 == null && k2 == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{AF65E2EA-3739-4e57-9C5F-7F43C949CE5E}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{3852C2E2-4A16-4b11-8E71-F8904C37EC3D}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{A0202464-B4B4-4b85-9628-CCD46DF16942}");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{F60C3E02-214A-462b-9B07-56EA38545A13}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{C2FBB630-2971-11d1-A18C-00C04FD75D13}\InprocServer32"))
                    {
                        k?.SetValue("", "shell32.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
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
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"lnkfile\shellex\ContextMenuHandlers\NvAppShExt"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"lnkfile\shellex\ContextMenuHandlers\NvAppShExt");
                    DeleteTree(Registry.ClassesRoot, @"lnkfile\shellex\ContextMenuHandlers\OpenGLShExt");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"lnkfile\shellex\ContextMenuHandlers\NvAppShExt"))
                        k?.SetValue("", "{a929c4ce-fd36-4270-b4f5-34ecac5bd63c}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"lnkfile\shellex\ContextMenuHandlers\OpenGLShExt"))
                        k?.SetValue("", "{e97dec16-a50d-49bb-ae24-cf682282e08d}");
                }
            });
        }

        private static void InitFolderAndDriveRemovalItems()
        {
            // 8. Previous Versions Tab & Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePreviousVersions",
                Name = "Previous Versions (Tab & Context Menu)",
                Category = "Folder",
                Description = "Elimină fila și comenzile contextuale 'Previous Versions' de pe foldere, fișiere și drive-uri.",
                IconLocation = "twext.dll,-1037",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                    DeleteTree(Registry.ClassesRoot, @"AllFilesystemObjects\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                    DeleteTree(Registry.ClassesRoot, @"Directory\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                    DeleteTree(Registry.ClassesRoot, @"Drive\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                    DeleteTree(Registry.ClassesRoot, @"Directory\shellex\PropertySheetHandlers\Offline Files");
                    DeleteTree(Registry.ClassesRoot, @"Folder\shellex\PropertySheetHandlers\Offline Files");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{596AB062-B4D2-4215-9F74-E9109B0A8153}\InprocServer32"))
                    {
                        k?.SetValue("", "twext.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"Directory\shellex\PropertySheetHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}"))
                        k?.SetValue("", "{596AB062-B4D2-4215-9F74-E9109B0A8153}");
                }
            });

            // 9. BitLocker Drive Menu
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveBitLockerDrive",
                Name = "BitLocker Drive Operations (Lock/PIN)",
                Category = "Drive",
                Description = "Elimină comenzile BitLocker (change passphrase, pin, encrypt, manage, unlock) de pe partiții.",
                IconLocation = "shell32.dll,-48",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"Drive\shell\manage-bde"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    string[] verbs = { "change-passphrase", "change-pin", "encrypt-bde", "encrypt-bde-elev", "manage-bde", "resume-bde", "resume-bde-elev", "unlock-bde" };
                    foreach (var v in verbs) DeleteTree(Registry.ClassesRoot, $@"Drive\shell\{v}");
                },
                RestoreAction = () =>
                {
                    Registry.ClassesRoot.CreateSubKey(@"Drive\shell\manage-bde");
                    Registry.ClassesRoot.CreateSubKey(@"Drive\shell\unlock-bde");
                }
            });

            // 10. BitLocker Drive Unlock (EhStorShell)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveBitLockerUnlock",
                Name = "BitLocker Drive Unlock (EhStorShell)",
                Category = "Drive",
                Description = "Elimină extensia shell de deblocare prin parolă a unităților BitLocker / Enhanced Storage.",
                IconLocation = "EhStorShell.dll,-101",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{2854F705-3548-414C-A113-93E27C808C85}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{2854F705-3548-414C-A113-93E27C808C85}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{2854F705-3548-414C-A113-93E27C808C85}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{2854F705-3548-414C-A113-93E27C808C85}\InprocServer32"))
                    {
                        k?.SetValue("", "EhStorShell.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
            });

            // 11. Open as Portable & PerfectDisk
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveOpenPortable",
                Name = "Open as Portable Device & PerfectDisk",
                Category = "Drive",
                Description = "Elimină handler-ul WPD 'Open as Portable Device' și comanda 'Optimize using PerfectDisk' de pe unități.",
                IconLocation = "wpdshext.dll,-511",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.LocalMachine, @"SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}");
                    DeleteTree(Registry.ClassesRoot, @"Drive\shell\Optimize using PerfectDisk");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}\InprocServer32"))
                    {
                        k?.SetValue("", "wpdshext.dll");
                        k?.SetValue("ThreadingModel", "Both");
                    }
                }
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
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{69AD30A4-B8B0-43C0-82CA-2B7AFE9AA1B7}\InprocServer32"))
                    {
                        k?.SetValue("", "shell32.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
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
                RemoveAction = () => DeleteTree(Registry.ClassesRoot, @"DesktopBackground\Shell\ControlledFolderAccess"),
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"DesktopBackground\Shell\ControlledFolderAccess\command"))
                        k?.SetValue("", @"explorer.exe windowsdefender://ransomwareprotection");
                }
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
                    using (var k1 = Registry.ClassesRoot.OpenSubKey(@"SystemFileAssociations\audio\shell\Play"))
                    using (var k2 = Registry.ClassesRoot.OpenSubKey(@"CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}"))
                        return k1 == null && k2 == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}");
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\Directory.Audio\shellex\ContextMenuHandlers\PlayTo");
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\Directory.Image\shellex\ContextMenuHandlers\PlayTo");
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\Directory.Video\shellex\ContextMenuHandlers\PlayTo");
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\audio\shell\Play");
                    DeleteTree(Registry.ClassesRoot, @"SystemFileAssociations\Directory.Audio\shell\Play");
                    DeleteTree(Registry.ClassesRoot, @"Stack.Audio\shell\Play");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{7AD84985-87B4-4a16-BE58-8B72A5B390F7}\InprocServer32"))
                    {
                        k?.SetValue("", "playtomenu.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
            });

            // 15. Cast to Device (PlayTo)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemovePlayToDevice",
                Name = "Cast to Device (PlayTo Menu)",
                Category = "Media",
                Description = "Elimină opțiunea 'Cast to Device' din meniul contextual al fișierelor multimedia.",
                IconLocation = "playtomenu.dll,-101",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}");
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{AF679D58-078E-48D4-A1E3-474F10DF76B8}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{8AB635F8-9A67-4698-AB99-784AD929F3B4}\InprocServer32"))
                    {
                        k?.SetValue("", "playtomenu.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                }
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
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellCreateVideo"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    foreach (var ext in exts) DeleteTree(Registry.LocalMachine, $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell");
                    DeleteTree(Registry.ClassesRoot, @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellCreateVideo");
                    DeleteTree(Registry.ClassesRoot, @"AppXk0g4vb8gvt7b93tg50ybcy892pge6jmt\Shell\ShellCreateVideo");
                    DeleteTree(Registry.ClassesRoot, @"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellEdit");
                },
                RestoreAction = () =>
                {
                    Registry.ClassesRoot.CreateSubKey(@"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellCreateVideo");
                    Registry.ClassesRoot.CreateSubKey(@"AppX43hnxtbyyps62jhe9sqpdzxn1790zetc\Shell\ShellEdit");
                }
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
                    using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\SystemFileAssociations\.jpg\Shell\3D Edit"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    foreach (var ext in exts) DeleteTree(Registry.LocalMachine, $@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell\3D Edit");
                },
                RestoreAction = () =>
                {
                    string[] exts = { ".bmp", ".jpeg", ".jpe", ".jpg", ".png", ".gif", ".tif", ".tiff" };
                    foreach (var ext in exts)
                    {
                        using (var k = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Classes\SystemFileAssociations\{ext}\Shell\3D Edit"))
                        {
                            k?.SetValue("", "Edit with Paint 3D");
                            k?.SetValue("Icon", "mspaint.exe");
                        }
                    }
                }
            });
        }

        private static void InitSystemRemovalItems()
        {
            // 18. Windows Defender Scan (EPP Context Menu)
            Items.Add(new DefaultMenuRemovalItemDef
            {
                Id = "RemoveDefenderEPP",
                Name = "Windows Defender Scan (EPP)",
                Category = "System",
                Description = "Elimină comanda 'Scan with Microsoft Defender' din meniul fișierelor, folderelor și drive-urilor.",
                IconLocation = "imageres.dll,-5324",
                CheckIsRemoved = () =>
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"*\shellex\ContextMenuHandlers\EPP"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"*\shellex\ContextMenuHandlers\EPP");
                    DeleteTree(Registry.ClassesRoot, @"Directory\shellex\ContextMenuHandlers\EPP");
                    DeleteTree(Registry.ClassesRoot, @"Drive\shellex\ContextMenuHandlers\EPP");
                    DeleteTree(Registry.ClassesRoot, @"*\shell\UpdateEncryptionSettingsWork");
                    DeleteTree(Registry.ClassesRoot, @"Directory\shell\UpdateEncryptionSettings");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"*\shellex\ContextMenuHandlers\EPP"))
                        k?.SetValue("", "{09A47860-11B0-4DA5-AFA5-26D86198A780}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"Directory\shellex\ContextMenuHandlers\EPP"))
                        k?.SetValue("", "{09A47860-11B0-4DA5-AFA5-26D86198A780}");
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"Drive\shellex\ContextMenuHandlers\EPP"))
                        k?.SetValue("", "{09A47860-11B0-4DA5-AFA5-26D86198A780}");
                }
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
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(@"CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}"))
                        return k == null;
                },
                RemoveAction = () =>
                {
                    DeleteTree(Registry.ClassesRoot, @"CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}");
                    DeleteTree(Registry.ClassesRoot, @"WOW6432Node\CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}");
                    DeleteTree(Registry.LocalMachine, @"SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EncryptionMenu");
                    DeleteTree(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{578480AA-1B1C-4343-AABD-62C0A273DCB5}");
                    DeleteTree(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellServiceObjects\{811F592B-CDE7-4ca4-A6D4-7BB3F60AD8FB}");
                },
                RestoreAction = () =>
                {
                    using (var k = Registry.ClassesRoot.CreateSubKey(@"CLSID\{A470F8CF-A1E8-4f65-8335-227475AA5C46}\InprocServer32"))
                    {
                        k?.SetValue("", "shell32.dll");
                        k?.SetValue("ThreadingModel", "Apartment");
                    }
                    using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EncryptionMenu"))
                        k?.SetValue("", "{A470F8CF-A1E8-4f65-8335-227475AA5C46}");
                }
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