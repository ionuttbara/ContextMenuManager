using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ContextMenuManager.Methods
{
    /// <summary>
    /// Built-in UI localization. No external language/configuration files are required.
    /// The language is selected from the Windows UI culture at application startup.
    /// Supported: English, Romanian, Spanish and Hungarian. Other cultures fall back to English.
    /// </summary>
    static class UiLanguage
    {
        public static string CurrentCode { get; private set; } = "en";

        public static void ApplySystemLanguage()
        {
            string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (code != "ro" && code != "es" && code != "hu") code = "en";
            CurrentCode = code;
            if (code == "en") return;

            Dictionary<string, string> values = code == "ro" ? Romanian : code == "es" ? Spanish : Hungarian;
            foreach (var pair in values)
                SetAppString(pair.Key, pair.Value);
        }

        private static void SetAppString(string key, string value)
        {
            int dot = key.IndexOf('.');
            if (dot <= 0 || dot >= key.Length - 1) return;
            string section = key.Substring(0, dot);
            string property = key.Substring(dot + 1);
            Type nested = typeof(AppString).GetNestedType(section, BindingFlags.Public);
            PropertyInfo pi = nested?.GetProperty(property, BindingFlags.Public | BindingFlags.Static);
            if (pi != null && pi.CanWrite) pi.SetValue(null, value, null);
        }

        public static string Text(string key)
        {
            string en;
            EnglishUi.TryGetValue(key, out en);
            if (CurrentCode == "ro" && RomanianUi.TryGetValue(key, out string ro)) return ro;
            if (CurrentCode == "es" && SpanishUi.TryGetValue(key, out string es)) return es;
            if (CurrentCode == "hu" && HungarianUi.TryGetValue(key, out string hu)) return hu;
            return en ?? key;
        }

        private static readonly Dictionary<string, string> EnglishUi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CurrentItems"] = "Current Items", ["AddCustomItems"] = "Add Custom Items", ["RemoveDefaultMenus"] = "Remove Default Menus",
            ["MicrosoftStoreApps"] = "Microsoft Store Apps", ["Terminal"] = "Terminal", ["AllItems"] = "All Items", ["Media"] = "Media", ["System"] = "System",
            ["BulkActions"] = "Bulk actions", ["AddAll"] = "Add all", ["RemoveAll"] = "Remove all", ["RestoreAll"] = "Restore all",
            ["AddToRegistry"] = "Add to registry", ["ReAddToRegistry"] = "Re-add to registry", ["RemoveFromRegistry"] = "Remove from registry", ["RestoreToRegistry"] = "Restore to registry",
            ["ContextMenus"] = "Context Menus", ["ManageContextMenus"] = "Manage context menu items for {0}", ["SubmenuFor"] = "Submenu for {0}",
            ["InfoAllRemoval"] = "All default-menu removal options", ["InfoFileRemoval"] = "Default menus for files", ["InfoFolderRemoval"] = "Default menus for folders",
            ["InfoDesktopRemoval"] = "Default menus for the desktop", ["InfoDriveRemoval"] = "Default menus for drives", ["InfoMediaRemoval"] = "Media, photo and video menus",
            ["InfoSystemRemoval"] = "Default system and security menus", ["StoreAppsInfo"] = "Packaged apps that add Windows context menu commands",
            ["TerminalInfo"] = "Configure Open in Terminal commands and Windows Terminal profiles",
            ["AboutProject"] = "About this project", ["Repository"] = "Repository & documentation", ["TechnicalDetails"] = "Technical details", ["KeyFeatures"] = "Key features",
            ["WinXRepair"] = "Repair Win+X metadata", ["WinXRepairTip"] = "Rebuild Win+X shortcut metadata and refresh Explorer", ["WinXRepairDone"] = "Win+X metadata repaired: {0} shortcuts updated, {1} failed."
        };

        private static readonly Dictionary<string, string> RomanianUi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CurrentItems"] = "Elemente curente", ["AddCustomItems"] = "Adăugare elemente", ["RemoveDefaultMenus"] = "Eliminare meniuri implicite",
            ["MicrosoftStoreApps"] = "Aplicații Microsoft Store", ["Terminal"] = "Terminal", ["AllItems"] = "Toate elementele", ["Media"] = "Media", ["System"] = "Sistem",
            ["BulkActions"] = "Acțiuni în masă", ["AddAll"] = "Adaugă toate", ["RemoveAll"] = "Elimină toate", ["RestoreAll"] = "Restaurează toate",
            ["AddToRegistry"] = "Adaugă în registry", ["ReAddToRegistry"] = "Adaugă din nou în registry", ["RemoveFromRegistry"] = "Elimină din registry", ["RestoreToRegistry"] = "Restaurează în registry",
            ["ContextMenus"] = "Meniuri contextuale", ["ManageContextMenus"] = "Administrare meniuri contextuale pentru {0}", ["SubmenuFor"] = "Submeniu pentru {0}",
            ["InfoAllRemoval"] = "Toate opțiunile de eliminare a meniurilor implicite", ["InfoFileRemoval"] = "Meniuri implicite pentru fișiere", ["InfoFolderRemoval"] = "Meniuri implicite pentru foldere",
            ["InfoDesktopRemoval"] = "Meniuri implicite pentru desktop", ["InfoDriveRemoval"] = "Meniuri implicite pentru unități", ["InfoMediaRemoval"] = "Meniuri media, fotografii și video",
            ["InfoSystemRemoval"] = "Meniuri implicite de sistem și securitate", ["StoreAppsInfo"] = "Aplicații împachetate care adaugă comenzi în meniul contextual Windows",
            ["TerminalInfo"] = "Configurează comenzile Deschide în Terminal și profilurile Windows Terminal",
            ["AboutProject"] = "Despre proiect", ["Repository"] = "Repository și documentație", ["TechnicalDetails"] = "Detalii tehnice", ["KeyFeatures"] = "Funcții principale",
            ["WinXRepair"] = "Repară Win+X", ["WinXRepairTip"] = "Reface metadatele scurtăturilor Win+X și reîncarcă Explorer", ["WinXRepairDone"] = "Metadatele Win+X au fost reparate: {0} scurtături actualizate, {1} eșuate."
        };

        private static readonly Dictionary<string, string> SpanishUi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CurrentItems"] = "Elementos actuales", ["AddCustomItems"] = "Añadir elementos", ["RemoveDefaultMenus"] = "Quitar menús predeterminados",
            ["MicrosoftStoreApps"] = "Aplicaciones de Microsoft Store", ["Terminal"] = "Terminal", ["AllItems"] = "Todos los elementos", ["Media"] = "Multimedia", ["System"] = "Sistema",
            ["BulkActions"] = "Acciones en bloque", ["AddAll"] = "Añadir todo", ["RemoveAll"] = "Quitar todo", ["RestoreAll"] = "Restaurar todo",
            ["AddToRegistry"] = "Añadir al registro", ["ReAddToRegistry"] = "Volver a añadir al registro", ["RemoveFromRegistry"] = "Quitar del registro", ["RestoreToRegistry"] = "Restaurar en el registro",
            ["ContextMenus"] = "Menús contextuales", ["ManageContextMenus"] = "Administrar menús contextuales para {0}", ["SubmenuFor"] = "Submenú para {0}",
            ["InfoAllRemoval"] = "Todas las opciones para quitar menús predeterminados", ["InfoFileRemoval"] = "Menús predeterminados de archivos", ["InfoFolderRemoval"] = "Menús predeterminados de carpetas",
            ["InfoDesktopRemoval"] = "Menús predeterminados del escritorio", ["InfoDriveRemoval"] = "Menús predeterminados de unidades", ["InfoMediaRemoval"] = "Menús multimedia, foto y vídeo",
            ["InfoSystemRemoval"] = "Menús predeterminados del sistema y seguridad", ["StoreAppsInfo"] = "Aplicaciones empaquetadas que añaden comandos al menú contextual de Windows",
            ["TerminalInfo"] = "Configurar comandos Abrir en Terminal y perfiles de Windows Terminal",
            ["AboutProject"] = "Acerca del proyecto", ["Repository"] = "Repositorio y documentación", ["TechnicalDetails"] = "Detalles técnicos", ["KeyFeatures"] = "Funciones principales",
            ["WinXRepair"] = "Reparar Win+X", ["WinXRepairTip"] = "Reconstruye los metadatos de los accesos directos Win+X y actualiza Explorer", ["WinXRepairDone"] = "Metadatos Win+X reparados: {0} accesos actualizados, {1} fallidos."
        };

        private static readonly Dictionary<string, string> HungarianUi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CurrentItems"] = "Jelenlegi elemek", ["AddCustomItems"] = "Egyéni elemek hozzáadása", ["RemoveDefaultMenus"] = "Alapértelmezett menük eltávolítása",
            ["MicrosoftStoreApps"] = "Microsoft Store alkalmazások", ["Terminal"] = "Terminál", ["AllItems"] = "Összes elem", ["Media"] = "Média", ["System"] = "Rendszer",
            ["BulkActions"] = "Csoportos műveletek", ["AddAll"] = "Összes hozzáadása", ["RemoveAll"] = "Összes eltávolítása", ["RestoreAll"] = "Összes visszaállítása",
            ["AddToRegistry"] = "Hozzáadás a rendszerleíró adatbázishoz", ["ReAddToRegistry"] = "Újbóli hozzáadás a rendszerleíró adatbázishoz", ["RemoveFromRegistry"] = "Eltávolítás a rendszerleíró adatbázisból", ["RestoreToRegistry"] = "Visszaállítás a rendszerleíró adatbázisba",
            ["ContextMenus"] = "Helyi menük", ["ManageContextMenus"] = "Helyi menük kezelése: {0}", ["SubmenuFor"] = "Almenü: {0}",
            ["InfoAllRemoval"] = "Minden alapértelmezett menüeltávolítási lehetőség", ["InfoFileRemoval"] = "Fájlok alapértelmezett menüi", ["InfoFolderRemoval"] = "Mappák alapértelmezett menüi",
            ["InfoDesktopRemoval"] = "Asztal alapértelmezett menüi", ["InfoDriveRemoval"] = "Meghajtók alapértelmezett menüi", ["InfoMediaRemoval"] = "Média-, kép- és videómenük",
            ["InfoSystemRemoval"] = "Rendszer- és biztonsági alapértelmezett menük", ["StoreAppsInfo"] = "Csomagolt alkalmazások, amelyek Windows helyi menüparancsokat adnak hozzá",
            ["TerminalInfo"] = "A Megnyitás terminálban parancsok és a Windows Terminal profilok beállítása",
            ["AboutProject"] = "A projektről", ["Repository"] = "Forráskód és dokumentáció", ["TechnicalDetails"] = "Technikai részletek", ["KeyFeatures"] = "Fő funkciók",
            ["WinXRepair"] = "Win+X javítása", ["WinXRepairTip"] = "Újraépíti a Win+X parancsikonok metaadatait és frissíti az Explorert", ["WinXRepairDone"] = "Win+X metaadatok javítva: {0} parancsikon frissítve, {1} sikertelen."
        };

        private static readonly Dictionary<string, string> Romanian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["General.AppName"] = "Manager meniuri contextuale Windows",
            ["ToolBar.Home"] = "Elemente curente", ["ToolBar.Type"] = "Tipuri de fișiere", ["ToolBar.Rule"] = "Alte reguli", ["ToolBar.Refresh"] = "Reîmprospătare", ["ToolBar.About"] = "Despre",
            ["SideBar.File"] = "Fișiere", ["SideBar.Folder"] = "Foldere", ["SideBar.Directory"] = "Directoare", ["SideBar.Background"] = "Fundal folder", ["SideBar.Desktop"] = "Desktop", ["SideBar.Drive"] = "Unități",
            ["SideBar.AllObjects"] = "Toate obiectele", ["SideBar.Computer"] = "Acest PC", ["SideBar.RecycleBin"] = "Coș de reciclare", ["SideBar.Library"] = "Biblioteci", ["SideBar.New"] = "Nou", ["SideBar.SendTo"] = "Trimite către",
            ["SideBar.OpenWith"] = "Deschide cu", ["SideBar.WinX"] = "Win+X", ["SideBar.LnkFile"] = "Fișiere .lnk", ["SideBar.UwpLnk"] = "Scurtături UWP", ["SideBar.ExeFile"] = "Fișiere .exe",
            ["SideBar.UnknownType"] = "Tip necunoscut", ["SideBar.MenuAnalysis"] = "Analiză meniu", ["SideBar.CustomExtension"] = "Extensie specifică", ["SideBar.PerceivedType"] = "Tip perceput", ["SideBar.DirectoryType"] = "Tip folder",
            ["SideBar.EnhanceMenu"] = "Îmbunătățiri meniu", ["SideBar.DetailedEdit"] = "Editare detaliată", ["SideBar.GuidBlocked"] = "GUID blocate", ["SideBar.DragDrop"] = "Glisare și fixare", ["SideBar.PublicReferences"] = "Referințe publice",
            ["SideBar.CustomRegPath"] = "Cale registry personalizată", ["SideBar.IEMenu"] = "Meniu contextual IE", ["SideBar.AppSetting"] = "Setări", ["SideBar.AppLanguage"] = "Limbă", ["SideBar.AboutApp"] = "Informații aplicație",
            ["StatusBar.File"] = "Meniu contextual pentru toate fișierele", ["StatusBar.Folder"] = "Meniu contextual pentru toate folderele", ["StatusBar.Directory"] = "Meniu contextual pentru directoare", ["StatusBar.Background"] = "Meniu contextual pentru fundalul folderelor și desktopului",
            ["StatusBar.Desktop"] = "Meniu contextual pentru Desktop", ["StatusBar.Drive"] = "Meniu contextual pentru unități", ["StatusBar.AllObjects"] = "Meniu contextual pentru toate obiectele sistemului de fișiere", ["StatusBar.Computer"] = "Meniu contextual pentru Acest PC",
            ["StatusBar.RecycleBin"] = "Meniu contextual pentru Coșul de reciclare", ["StatusBar.Library"] = "Meniu contextual pentru biblioteci", ["StatusBar.New"] = "Meniul Nou", ["StatusBar.SendTo"] = "Meniul Trimite către", ["StatusBar.OpenWith"] = "Meniul Deschide cu",
            ["StatusBar.WinX"] = "Administrare meniu Win+X", ["StatusBar.LnkFile"] = "Meniu contextual pentru scurtături .lnk", ["StatusBar.UwpLnk"] = "Meniu contextual pentru scurtături/aplicații UWP", ["StatusBar.ExeFile"] = "Meniu contextual pentru executabile .exe",
            ["StatusBar.CustomExtension"] = "Personalizează meniul unei extensii", ["StatusBar.PerceivedType"] = "Personalizează meniul unui tip perceput", ["StatusBar.DirectoryType"] = "Personalizează meniul unui tip de folder", ["StatusBar.UnknownType"] = "Meniu pentru tipuri de fișiere neasociate", ["StatusBar.MenuAnalysis"] = "Analizează locația meniului contextual",
            ["Menu.ChangeText"] = "Redenumește", ["Menu.ItemIcon"] = "Pictogramă", ["Menu.ChangeIcon"] = "Schimbă pictograma", ["Menu.AddIcon"] = "Adaugă pictogramă", ["Menu.DeleteIcon"] = "Șterge pictograma", ["Menu.ItemPosition"] = "Poziție element",
            ["Menu.SetDefault"] = "Implicit", ["Menu.SetTop"] = "Sus", ["Menu.SetBottom"] = "Jos", ["Menu.OtherAttributes"] = "Alte proprietăți", ["Menu.OnlyWithShift"] = "Afișează doar cu Shift", ["Menu.OnlyInExplorer"] = "Afișează doar în Explorer",
            ["Menu.Details"] = "Detalii", ["Menu.WebSearch"] = "Căutare web", ["Menu.ChangeCommand"] = "Schimbă comanda", ["Menu.RunAsAdministrator"] = "Rulează ca administrator", ["Menu.FileProperties"] = "Proprietăți fișier", ["Menu.FileLocation"] = "Locație fișier",
            ["Menu.RegistryLocation"] = "Locație registry", ["Menu.ExportRegistry"] = "Exportă registry", ["Menu.Delete"] = "Șterge elementul", ["Menu.ChangeGroup"] = "Schimbă grupul", ["Menu.RestoreDefault"] = "Restaurează implicit", ["Menu.Edit"] = "Editare", ["Menu.Save"] = "Salvare",
            ["Dialog.Browse"] = "Răsfoire", ["Dialog.Program"] = "Program", ["Dialog.AllFiles"] = "Toate fișierele", ["Dialog.RegistryFile"] = "Fișier registry", ["Dialog.ItemText"] = "Text element", ["Dialog.ItemCommand"] = "Comandă meniu", ["Dialog.CommandArguments"] = "Argumente comandă",
            ["Dialog.SingleMenu"] = "Meniu pe un nivel", ["Dialog.MultiMenu"] = "Meniu pe mai multe niveluri", ["Dialog.Public"] = "Public", ["Dialog.Private"] = "Privat", ["Dialog.SelectAll"] = "Selectează tot", ["Dialog.SelectExtension"] = "Selectează o extensie de fișier",
            ["Dialog.SelectPerceivedType"] = "Selectează un tip perceput", ["Dialog.SelectDirectoryType"] = "Selectează un tip de folder", ["Dialog.SelectNewItemType"] = "Selectează tipul elementului nou", ["Dialog.SelectGroup"] = "Selectează grupul", ["Dialog.SelectObjectType"] = "Selectează tipul obiectului de analizat",
            ["Message.TextCannotBeEmpty"] = "Textul meniului nu poate fi gol!", ["Message.CommandCannotBeEmpty"] = "Comanda meniului nu poate fi goală!", ["Message.ConfirmDelete"] = "Sigur dorești să ștergi acest element?", ["Message.FileNotExists"] = "Fișierul nu există!", ["Message.FolderNotExists"] = "Folderul nu există!",
            ["Message.CannotChangePath"] = "Calea fișierului nu poate fi modificată!", ["Message.CopiedToClipboard"] = "Copiat în clipboard:", ["Message.RestartApp"] = "Aplicația va reporni!", ["Message.RestoreDefault"] = "Confirmi restaurarea meniului implicit?", ["Message.DeleteGroup"] = "Sigur dorești să ștergi definitiv acest grup și toate elementele lui?",
            ["Message.WinXSorted"] = "Elementele Win+X au fost renumerotate. Repornește File Explorer pentru aplicarea modificărilor.", ["Tip.RestartExplorer"] = "File Explorer trebuie repornit pentru aplicarea modificărilor.", ["Tip.CreateGroup"] = "Creează grup", ["Tip.DropOrSelectObject"] = "Glisează sau selectează un obiect",
            ["Other.ProtectOpenItem"] = "Protejează elementul Deschide", ["Other.WinXSortable"] = "Activează sortarea meniului Win+X", ["Other.ShowFilePath"] = "Afișează calea fișierului în bara de stare", ["Other.OpenMoreRegedit"] = "Deschide Registry Editor într-o fereastră separată", ["Other.OpenMoreExplorer"] = "Deschide File Explorer într-o fereastră separată",
            ["Other.HideDisabledItems"] = "Ascunde elementele dezactivate", ["Other.HideSysStoreItems"] = "Ascunde elementele de sistem", ["Other.TopMost"] = "Întotdeauna deasupra", ["Other.CurrentFilePath"] = "Cale fișier curent:", ["Other.CurrentRegPath"] = "Cale registry curentă:"
        };

        private static readonly Dictionary<string, string> Spanish = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["General.AppName"] = "Administrador de menús contextuales de Windows",
            ["ToolBar.Home"] = "Elementos actuales", ["ToolBar.Type"] = "Tipos de archivo", ["ToolBar.Rule"] = "Otras reglas", ["ToolBar.Refresh"] = "Actualizar", ["ToolBar.About"] = "Acerca de",
            ["SideBar.File"] = "Archivos", ["SideBar.Folder"] = "Carpetas", ["SideBar.Directory"] = "Directorios", ["SideBar.Background"] = "Fondo de carpeta", ["SideBar.Desktop"] = "Escritorio", ["SideBar.Drive"] = "Unidades",
            ["SideBar.AllObjects"] = "Todos los objetos", ["SideBar.Computer"] = "Este equipo", ["SideBar.RecycleBin"] = "Papelera de reciclaje", ["SideBar.Library"] = "Bibliotecas", ["SideBar.New"] = "Nuevo", ["SideBar.SendTo"] = "Enviar a",
            ["SideBar.OpenWith"] = "Abrir con", ["SideBar.WinX"] = "Win+X", ["SideBar.LnkFile"] = "Archivos .lnk", ["SideBar.UwpLnk"] = "Accesos UWP", ["SideBar.ExeFile"] = "Archivos .exe", ["SideBar.UnknownType"] = "Tipo desconocido",
            ["SideBar.MenuAnalysis"] = "Análisis del menú", ["SideBar.CustomExtension"] = "Extensión específica", ["SideBar.PerceivedType"] = "Tipo percibido", ["SideBar.DirectoryType"] = "Tipo de carpeta", ["SideBar.EnhanceMenu"] = "Mejoras del menú", ["SideBar.DetailedEdit"] = "Edición detallada",
            ["SideBar.GuidBlocked"] = "GUID bloqueados", ["SideBar.DragDrop"] = "Arrastrar y soltar", ["SideBar.PublicReferences"] = "Referencias públicas", ["SideBar.CustomRegPath"] = "Ruta de registro personalizada", ["SideBar.IEMenu"] = "Menú contextual de IE", ["SideBar.AppSetting"] = "Configuración", ["SideBar.AppLanguage"] = "Idioma", ["SideBar.AboutApp"] = "Información de la aplicación",
            ["Menu.ChangeText"] = "Cambiar nombre", ["Menu.ChangeIcon"] = "Cambiar icono", ["Menu.AddIcon"] = "Añadir icono", ["Menu.DeleteIcon"] = "Eliminar icono", ["Menu.Details"] = "Detalles", ["Menu.WebSearch"] = "Buscar en la web", ["Menu.ChangeCommand"] = "Cambiar comando", ["Menu.RunAsAdministrator"] = "Ejecutar como administrador",
            ["Menu.FileProperties"] = "Propiedades del archivo", ["Menu.FileLocation"] = "Ubicación del archivo", ["Menu.RegistryLocation"] = "Ubicación del registro", ["Menu.ExportRegistry"] = "Exportar registro", ["Menu.Delete"] = "Eliminar elemento", ["Menu.ChangeGroup"] = "Cambiar grupo", ["Menu.RestoreDefault"] = "Restaurar predeterminado", ["Menu.Edit"] = "Editar", ["Menu.Save"] = "Guardar",
            ["Dialog.Browse"] = "Examinar", ["Dialog.Program"] = "Programa", ["Dialog.AllFiles"] = "Todos los archivos", ["Dialog.RegistryFile"] = "Archivo de registro", ["Dialog.ItemText"] = "Texto del elemento", ["Dialog.ItemCommand"] = "Comando del menú", ["Dialog.CommandArguments"] = "Argumentos del comando", ["Dialog.SelectAll"] = "Seleccionar todo",
            ["Dialog.SelectExtension"] = "Selecciona una extensión de archivo", ["Dialog.SelectPerceivedType"] = "Selecciona un tipo percibido", ["Dialog.SelectDirectoryType"] = "Selecciona un tipo de carpeta", ["Dialog.SelectNewItemType"] = "Selecciona el tipo de elemento nuevo", ["Dialog.SelectGroup"] = "Selecciona el grupo", ["Dialog.SelectObjectType"] = "Selecciona el tipo de objeto que analizar",
            ["Message.TextCannotBeEmpty"] = "¡El texto del menú no puede estar vacío!", ["Message.CommandCannotBeEmpty"] = "¡El comando del menú no puede estar vacío!", ["Message.ConfirmDelete"] = "¿Seguro que quieres eliminar este elemento?", ["Message.FileNotExists"] = "¡El archivo no existe!", ["Message.FolderNotExists"] = "¡La carpeta no existe!", ["Message.CannotChangePath"] = "¡No se puede cambiar la ruta del archivo!",
            ["Message.CopiedToClipboard"] = "Copiado al portapapeles:", ["Message.RestartApp"] = "¡La aplicación se reiniciará!", ["Message.RestoreDefault"] = "¿Confirmas restaurar el menú predeterminado?", ["Message.DeleteGroup"] = "¿Seguro que quieres eliminar permanentemente este grupo y todos sus elementos?", ["Message.WinXSorted"] = "Los elementos Win+X se han renumerado. Reinicia el Explorador de archivos para aplicar los cambios.",
            ["Tip.RestartExplorer"] = "Hay que reiniciar el Explorador de archivos para aplicar los cambios.", ["Tip.CreateGroup"] = "Crear grupo", ["Tip.DropOrSelectObject"] = "Arrastra o selecciona un objeto",
            ["Other.ProtectOpenItem"] = "Proteger el elemento Abrir", ["Other.WinXSortable"] = "Activar ordenación del menú Win+X", ["Other.ShowFilePath"] = "Mostrar ruta del archivo en la barra de estado", ["Other.OpenMoreRegedit"] = "Abrir el Editor del Registro en otra ventana", ["Other.OpenMoreExplorer"] = "Abrir el Explorador de archivos en otra ventana", ["Other.HideDisabledItems"] = "Ocultar elementos deshabilitados", ["Other.HideSysStoreItems"] = "Ocultar elementos del sistema", ["Other.TopMost"] = "Siempre visible"
        };

        private static readonly Dictionary<string, string> Hungarian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["General.AppName"] = "Windows helyi menü kezelő",
            ["ToolBar.Home"] = "Jelenlegi elemek", ["ToolBar.Type"] = "Fájltípusok", ["ToolBar.Rule"] = "Egyéb szabályok", ["ToolBar.Refresh"] = "Frissítés", ["ToolBar.About"] = "Névjegy",
            ["SideBar.File"] = "Fájlok", ["SideBar.Folder"] = "Mappák", ["SideBar.Directory"] = "Könyvtárak", ["SideBar.Background"] = "Mappaháttér", ["SideBar.Desktop"] = "Asztal", ["SideBar.Drive"] = "Meghajtók", ["SideBar.AllObjects"] = "Minden objektum", ["SideBar.Computer"] = "Ez a gép", ["SideBar.RecycleBin"] = "Lomtár", ["SideBar.Library"] = "Könyvtárak",
            ["SideBar.New"] = "Új", ["SideBar.SendTo"] = "Küldés", ["SideBar.OpenWith"] = "Megnyitás ezzel", ["SideBar.WinX"] = "Win+X", ["SideBar.LnkFile"] = ".lnk fájlok", ["SideBar.UwpLnk"] = "UWP parancsikonok", ["SideBar.ExeFile"] = ".exe fájlok", ["SideBar.UnknownType"] = "Ismeretlen típus", ["SideBar.MenuAnalysis"] = "Menüelemzés", ["SideBar.CustomExtension"] = "Egyedi kiterjesztés", ["SideBar.PerceivedType"] = "Érzékelt típus", ["SideBar.DirectoryType"] = "Mappatípus",
            ["SideBar.EnhanceMenu"] = "Menübővítések", ["SideBar.DetailedEdit"] = "Részletes szerkesztés", ["SideBar.GuidBlocked"] = "Blokkolt GUID", ["SideBar.DragDrop"] = "Fogd és vidd", ["SideBar.PublicReferences"] = "Nyilvános hivatkozások", ["SideBar.CustomRegPath"] = "Egyéni rendszerleíró útvonal", ["SideBar.IEMenu"] = "IE helyi menü", ["SideBar.AppSetting"] = "Beállítások", ["SideBar.AppLanguage"] = "Nyelv", ["SideBar.AboutApp"] = "Alkalmazásinformáció",
            ["Menu.ChangeText"] = "Átnevezés", ["Menu.ChangeIcon"] = "Ikon módosítása", ["Menu.AddIcon"] = "Ikon hozzáadása", ["Menu.DeleteIcon"] = "Ikon törlése", ["Menu.Details"] = "Részletek", ["Menu.WebSearch"] = "Webes keresés", ["Menu.ChangeCommand"] = "Parancs módosítása", ["Menu.RunAsAdministrator"] = "Futtatás rendszergazdaként", ["Menu.FileProperties"] = "Fájl tulajdonságai", ["Menu.FileLocation"] = "Fájl helye", ["Menu.RegistryLocation"] = "Rendszerleíró adatbázis helye", ["Menu.ExportRegistry"] = "Rendszerleíró adatbázis exportálása", ["Menu.Delete"] = "Elem törlése", ["Menu.ChangeGroup"] = "Csoport módosítása", ["Menu.RestoreDefault"] = "Alapértelmezés visszaállítása", ["Menu.Edit"] = "Szerkesztés", ["Menu.Save"] = "Mentés",
            ["Dialog.Browse"] = "Tallózás", ["Dialog.Program"] = "Program", ["Dialog.AllFiles"] = "Minden fájl", ["Dialog.RegistryFile"] = "Rendszerleíró fájl", ["Dialog.ItemText"] = "Elem szövege", ["Dialog.ItemCommand"] = "Menüparancs", ["Dialog.CommandArguments"] = "Parancs argumentumai", ["Dialog.SelectAll"] = "Összes kijelölése", ["Dialog.SelectExtension"] = "Válassz fájlkiterjesztést", ["Dialog.SelectPerceivedType"] = "Válassz érzékelt fájltípust", ["Dialog.SelectDirectoryType"] = "Válassz mappatípust", ["Dialog.SelectNewItemType"] = "Válaszd ki az új elem típusát", ["Dialog.SelectGroup"] = "Válassz csoportot", ["Dialog.SelectObjectType"] = "Válaszd ki az elemezendő objektumtípust",
            ["Message.TextCannotBeEmpty"] = "A menü szövege nem lehet üres!", ["Message.CommandCannotBeEmpty"] = "A menüparancs nem lehet üres!", ["Message.ConfirmDelete"] = "Biztosan törölni szeretnéd ezt az elemet?", ["Message.FileNotExists"] = "A fájl nem létezik!", ["Message.FolderNotExists"] = "A mappa nem létezik!", ["Message.CannotChangePath"] = "A fájl elérési útja nem módosítható!", ["Message.CopiedToClipboard"] = "Vágólapra másolva:", ["Message.RestartApp"] = "Az alkalmazás újraindul!", ["Message.RestoreDefault"] = "Visszaállítod az alapértelmezett menüt?", ["Message.DeleteGroup"] = "Biztosan végleg törlöd ezt a csoportot és minden elemét?", ["Message.WinXSorted"] = "A Win+X elemek újraszámozása megtörtént. A módosításokhoz indítsd újra a Fájlkezelőt.",
            ["Tip.RestartExplorer"] = "A módosítások alkalmazásához újra kell indítani a Fájlkezelőt.", ["Tip.CreateGroup"] = "Csoport létrehozása", ["Tip.DropOrSelectObject"] = "Húzz ide vagy válassz egy objektumot",
            ["Other.ProtectOpenItem"] = "A Megnyitás elem védelme", ["Other.WinXSortable"] = "Win+X menürendezés engedélyezése", ["Other.ShowFilePath"] = "Fájl elérési útjának megjelenítése az állapotsorban", ["Other.OpenMoreRegedit"] = "Rendszerleíróadatbázis-szerkesztő megnyitása külön ablakban", ["Other.OpenMoreExplorer"] = "Fájlkezelő megnyitása külön ablakban", ["Other.HideDisabledItems"] = "Letiltott elemek elrejtése", ["Other.HideSysStoreItems"] = "Rendszerelemek elrejtése", ["Other.TopMost"] = "Mindig felül"
        };
    }
}
