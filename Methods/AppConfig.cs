using Microsoft.Win32;
using System;
using System.Drawing;
using System.Globalization;

namespace ContextMenuManager.Methods
{
    static class AppConfig
    {
        public const string RegistryRoot = @"Software\ContextMenuManager";

        private static object GetValue(string section, string key)
        {
            try
            {
                using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey($@"{RegistryRoot}\{section}"))
                    return regKey?.GetValue(key);
            }
            catch { return null; }
        }

        private static string GetString(string section, string key)
            => GetValue(section, key)?.ToString() ?? string.Empty;

        private static void SetValue(string section, string key, object value, RegistryValueKind kind = RegistryValueKind.String)
        {
            try
            {
                using (RegistryKey regKey = Registry.CurrentUser.CreateSubKey($@"{RegistryRoot}\{section}"))
                    regKey?.SetValue(key, value, kind);
            }
            catch { }
        }

        private static bool GetBool(string section, string key, bool defaultValue)
        {
            object value = GetValue(section, key);
            if (value == null) return defaultValue;
            if (value is int i) return i != 0;
            if (int.TryParse(value.ToString(), out int n)) return n != 0;
            return defaultValue;
        }

        private static void SetBool(string section, string key, bool value)
            => SetValue(section, key, value ? 1 : 0, RegistryValueKind.DWord);

        // The UI language dictionary is embedded in the executable.  This value is used only
        // when a built-in rule has a Culture condition.
        public static string Language => CultureInfo.CurrentUICulture.Name;

        public static bool ProtectOpenItem
        {
            get => GetBool("General", "ProtectOpenItem", true);
            set => SetBool("General", "ProtectOpenItem", value);
        }

        public static bool ShowFilePath
        {
            get => GetBool("General", "ShowFilePath", false);
            set => SetBool("General", "ShowFilePath", value);
        }

        public static bool WinXSortable
        {
            get => GetBool("General", "WinXSortable", false);
            set => SetBool("General", "WinXSortable", value);
        }

        public static bool OpenMoreRegedit
        {
            get => GetBool("General", "OpenMoreRegedit", false);
            set => SetBool("General", "OpenMoreRegedit", value);
        }

        public static bool OpenMoreExplorer
        {
            get => GetBool("General", "OpenMoreExplorer", false);
            set => SetBool("General", "OpenMoreExplorer", value);
        }

        public static bool HideDisabledItems
        {
            get => GetBool("General", "HideDisabledItems", false);
            set => SetBool("General", "HideDisabledItems", value);
        }

        public static bool HideSysStoreItems
        {
            get => GetBool("General", "HideSysStoreItems", true);
            set => SetBool("General", "HideSysStoreItems", value);
        }

        public static bool TopMost
        {
            get => GetBool("Window", "TopMost", false);
            set => SetBool("Window", "TopMost", value);
        }

        public static Size MainFormSize
        {
            get
            {
                string str = GetString("Window", "MainFormSize");
                int index = str.IndexOf(',');
                if (index == -1) return Size.Empty;
                if (int.TryParse(str.Substring(0, index), out int width) &&
                    int.TryParse(str.Substring(index + 1), out int height))
                    return new Size(width, height);
                return Size.Empty;
            }
            set => SetValue("Window", "MainFormSize", value.Width + "," + value.Height);
        }
    }
}
