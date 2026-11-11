# Catalog de portare a meniurilor Registry

Scopul acestei modificări este portarea conținutului din arhiva `Context Menu.zip` în managerii C# existenți. Cele 39 de scripturi `.reg` din arhiva-sursă **nu sunt incluse ca scripturi în proiectul final**; ele au fost folosite doar pentru inventariere și mapare.

## Implementări native și restaurare

- `Methods/CustomPresetsManager.cs` conține preseturi pentru instalarea/eliminarea meniurilor de adăugare. Au fost adăugate preseturi native pentru crearea fișierului TXT, deschiderea explicită a unui script PowerShell și suport OCX pentru înregistrare/dezînregistrare; lista de hash include și `MACTripleDES`.
- `Methods/DefaultMenuRemovalManager.cs` mapează opțiunile de eliminare la acțiuni verificabile și reversibile. Pentru cheile eliminate se salvează arborii Registry originali înaintea modificării.
- `Methods/RegistryTreeBackupManager.cs` oferă backup/restaurare persistentă a arborilor Registry, în `%LOCALAPPDATA%\ContextMenuManager\RegistryBackups`.
- `RegistryValueBackupManager` (în același fișier) păstrează starea anterioară a valorilor individuale, inclusiv valori care lipseau și chei părinte care nu existau.
- `Methods/TerminalProfilesManager.cs` gestionează meniul Terminal prin interfața existentă și acum salvează cheile legacy CMD/PowerShell/WSL înainte de a le elimina, pentru a le putea restaura când toggle-ul este dezactivat.
- `Controls/CustomPresetItem.cs`, `Controls/DefaultRemovalItem.cs` și `Controls/TerminalItem.cs` recitesc starea efectivă din Registry după acțiune; interfața nu mai bifează o operație ca reușită doar pentru că a fost solicitată.

## Scripturile de adăugare

| Fișier-sursă | Implementarea nativă din aplicație | Note |
|---|---|---|
| `add-devices-and-printers-context-menu.reg` | `DesktopDevicesPrinters` | Preset existent; verificarea stării folosește cheia presetului. |
| `config_run/Configuration application run.reg` | `ExePriority`, `ExeFirewall` și grupul `AppRunConfig` din `CustomPresetsManager` | Opțiunile sunt definite în cod, nu importate din fișier. |
| `config_run/Run app as....reg` | `ExeTrustedInstaller` | Ștergerea fără legătură a `exefile\shell\setdesktopwallpaper` a fost separată în opțiunea reversibilă `RemoveExeSetDesktopWallpaper`. |
| `config_run/Set GPU Usage to... .reg` | `ExeGpu` | Preset existent; rămâne nativ în cod. |
| `menu list/Extract MSi.reg` | `MsiExtract` | Preset existent. |
| `menu list/File Management Menu.reg` | `FileLocksmith`, `FileTakeOwnership`, `FileCalculateHash`, `FileClipContent`, `FileVisibility`, `FilePermanentDelete` | Hash-urile includ `MACTripleDES`; meniurile sunt create de manager. |
| `menu list/Folder management Menu.reg` | `FolderColorizer`, `FolderFileLocksmith`, `FolderEmptyContents`, `FolderTakeOwnership`, `FolderPermanentDelete` | Preseturi existente. |
| `menu list/Register DLL and OCX.reg` | `DllRegister`, `DllUnregister` | Acum scrie și cheile `ocxfile\shell\...`, nu doar `dllfile`. |
| `menu list/Restart Menu.reg` | `DesktopPowerOps`, `DesktopSafeModes`, `DesktopTurnOffDisplay` și părintele `Device Shutdown` | Comenzile sunt reprezentate de preseturi individuale ale aplicației. |
| `menu list/Take Onwership.reg` | `FileTakeOwnership`, `FolderTakeOwnership`, `DriveTakeOwnership` | Preseturi existente. |
| `menu list/TerminalBehaivofMelodyMenu.reg` | `TerminalProfilesManager` și `TerminalBlockExtensionItem` | Profilurile sunt generate și instalate prin codul managerului existent; curățarea cheilor legacy are backup/restaurare. |
| `new_menu/TXT File in New Menu.reg` | `NewTxtFile` | Setează doar `NullFile`, fără a suprascrie alte valori deja existente în `.txt\ShellNew`; eliminarea nu șterge setări ShellNew fără legătură. |
| `powershell.reg` | `Ps1OpenWithPowerShell` | Adaptare sigură: adaugă verbul de meniu explicit, fără să preia asocierea implicită `.ps1` și fără să seteze `ExecutionPolicy Unrestricted`. |
| `restart_kill component/Config System Components .reg` | `DeskFw`, `DeskHvci`, `DeskNetProfile`, `DeskExplorerRestarter`, `DeskHomeQAView` | Comenzile sunt preseturi native existente, grupate sub „Refresh/Config System Components”. |
| `restart_kill component/Kill all not responding tasks.reg` | `DesktopKillTasks` | Preset existent. |
| `run as admin/Add_Run_as_administrator_to_MSI_context_menu.reg` | `MsiRunAs` | Preset existent; asocierea implicită MSI nu este înlocuită. |
| `run as admin/Add_Run_as_administrator_to_PS1_context_menu.reg` | `Ps1RunAs` | Preset existent. |
| `run as admin/Add_Run_as_administrator_to_VBS_context_menu.reg` | `VbsRunAs` | Preset existent. |

## Scripturile de eliminare

| Fișier-sursă | Implementarea nativă din aplicație | Note |
|---|---|---|
| `removal/Removal of Edit.reg` | `RemoveEditMenu` | Backup/restaurare exactă a cheilor țintă. |
| `removal/Remove 3D Edit.reg` | `RemovePaint3DEdit` | Backup al cheilor Shell pentru extensiile grafice. Dublura `.jpg` din fișierul-sursă este normalizată. |
| `removal/Remove Burn ISO Image Windows.reg` | `RemoveIsoBurn` | Backup/restaurare exactă. |
| `removal/Remove CopyTo and MoveTo Menu.reg` | `RemoveCopyToMoveTo` | Toate cele 8 ținte CLSID sunt verificate și salvate; nu se mai recreează o cheie generică aproximativă. |
| `removal/Remove Desktop Search Box Menu.reg` | `RemoveDesktopSearchBox` | Backup al ambelor chei CLSID. |
| `removal/Remove Ecription Context Menu.reg` | `RemoveEncryptionMenu` | Backup pentru CLSID, handler și cheile ShellServiceObjects. |
| `removal/Remove Edit with Clipchamp.reg` | `RemovePlayToDevice` (numele afișat este acum „Edit with Clipchamp”) | Păstrează/restaurează cele patru chei CLSID, inclusiv view-ul WOW6432Node. |
| `removal/Remove Enhanced Storage Context Menu.reg` | `RemoveBitLockerUnlock` | Acoperă CLSID-ul Enhanced Storage și view-ul WOW6432Node; dublura malformată din `.reg` nu este reprodusă. |
| `removal/Remove Image Shell.reg` | `RemovePhotosVideoEdit` | Elimină cheile Shell de imagine și cele trei intrări AppX specifice, cu backup. |
| `removal/Remove NVIDIA lnk menus.reg` | `RemoveNvShortcuts` | Backup pentru ambii handleri NVIDIA. |
| `removal/Remove Open as Portable Device.reg` | `RemoveOpenPortable` | Backup pentru handlerul WPD, CLSID și verbul PerfectDisk. |
| `removal/Remove PlayTo.reg` | `RemovePlayMedia` | Acoperă handlerii PlayTo/Play, shell-ul audio, Media Center și arborii AppX din sursă; backup înainte de ștergere. |
| `removal/Remove Previous Version Menu.reg` | `RemovePreviousVersions` | Salvează arborii CLSID/PropertySheetHandler și starea inițială a valorilor policy înainte de eliminare. |
| `removal/Remove Print Menu.reg` | `RemovePrintMenu` | Verifică toate cele 19 ținte, cu backup/restaurare. |
| `removal/Remove Sent To.reg` | `RemoveSendToMenu` | Backup pentru handler și CLSID-urile aferente. |
| `removal/Remove WSL Menu.reg` | Toggle-ul existent `TerminalBlockExtensionItem` / `TerminalProfilesManager` | Cele trei intrări WSL fac parte din curățarea legacy comună CMD/PowerShell/WSL, care acum este reversibilă. Nu se adaugă încă un toggle duplicat. |
| `removal/Remove Windows BitLocker Menu.reg` | `RemoveBitLockerDrive` | Verifică toate cele opt verbe; backup/restaurare exactă. |
| `removal/Remove Windows Encryption EPP Menus.reg` | `RemoveDefenderEPP` și toggle-ul existent `RemoveControlledFolderAccess` | EPP este separat de scurtătura Controlled Folder Access, pentru a evita suprapunerea a două acțiuni de eliminare. |
| `removal/Remove Windows Media Player 11 Shell.reg` | `RemoveWindowsMediaPlayer11Shell` | Opțiune nouă, pentru toate clasele WMP11 listate în sursă, cu backup/restaurare. |
| `removal/Remove Windows Terminal UWP Menu.reg` | `RemoveWindowsTerminalUwpMenu` | Opțiune nouă pentru cheia `PackagedCom\ClassIndex\{9F156763-7844-4DC4-B2B1-901F640F5155}`, cu backup/restaurare. |
| `removal/remove troubleshoot compatibility context menu.reg` | `RemoveCompatibility` | Verifică și salvează toate țintele Compatibility. |

## Decizii de portare

1. Fișierele `.reg` au fost tratate ca **specificație de comportament**, nu ca livrabile runtime. În proiectul portat nu se adaugă director `RegistryScripts` și nu se execută `regedit.exe` pentru aceste caracteristici.
2. Restaurarea folosește starea reală a Registry dinaintea operației, nu reconstruiește doar niște valori implicite presupuse.
3. Scriptul `powershell.reg` înlocuia asocierea globală `.ps1` și impunea `ExecutionPolicy Unrestricted`; aceasta nu este reprodusă. Presetul nou adaugă o comandă explicită în meniul contextual fără a reduce politica de execuție.
4. Ștergerea `exefile\shell\setdesktopwallpaper` din `Run app as....reg` nu este aplicată implicit odată cu TrustedInstaller; a devenit o acțiune separată și reversibilă.
5. Fișierele-sursă aveau unele ținte duplicate și o intrare duplicată malformată. Acestea au fost normalizate la nivelul operațiilor de cod.

## Verificare

- Inclus în `.csproj`: `Methods\RegistryTreeBackupManager.cs`.
- Inventar sursă: 39 fișiere `.reg` mapate mai sus.
- Fișierele `.reg` din arhiva-sursă nu sunt copiate în proiect. Rămân cele 3 resurse `.reg` care existau deja în proiect și nu provin din `Context Menu.zip`: două pentru Quick Access și resursa `Properties/Resources/ShellNew/0.reg`.
- Build .NET Framework și teste Registry reale trebuie rulate în Windows; mediul de portare nu are `dotnet`, `msbuild`, `csc` sau `mono`, deci compilarea nu a putut fi confirmată aici.
