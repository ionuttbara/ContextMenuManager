using Microsoft.Win32;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ContextMenuManager.BluePointLilac.Methods
{
    public static class ThemeManager
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        // Follow the Windows application theme used by the rest of the UI.
        public static bool IsDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        return value != null && Convert.ToInt32(value) == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        public static Color GetSystemAccentColor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("ColorizationColor");
                        if (value != null)
                        {
                            int colorInfo = unchecked((int)Convert.ToInt64(value));
                            return Color.FromArgb(
                                255,
                                (byte)((colorInfo >> 16) & 0xFF),
                                (byte)((colorInfo >> 8) & 0xFF),
                                (byte)(colorInfo & 0xFF));
                        }
                    }
                }
            }
            catch { }
            return SystemColors.Highlight;
        }

        public static Color GetWindowBackColor()
        {
            return IsDarkMode() ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250);
        }

        public static Color GetWindowForeColor()
        {
            return IsDarkMode() ? Color.White : Color.FromArgb(50, 50, 50);
        }

        // Make the native Windows title bar match the application's current light/dark theme.
        // Caption/text colors are supported by Windows 11; immersive dark mode also covers Windows 10.
        public static void ApplyTitleBar(Form form)
        {
            if (form == null || !form.IsHandleCreated) return;

            try
            {
                int useDark = IsDarkMode() ? 1 : 0;
                int result = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDark, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
                        ref useDark, sizeof(int));
                }

                int caption = ToColorRef(GetWindowBackColor());
                int text = ToColorRef(IsDarkMode() ? Color.White : Color.Black);
                int border = ToColorRef(GetSystemAccentColor());
                DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch
            {
                // Older Windows builds may not expose all DWM attributes; the normal title bar remains usable.
            }
        }

        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }

        public static void ApplyTheme(Control control)
        {
            bool isDark = IsDarkMode();
            Color backColor = isDark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
            Color foreColor = isDark ? Color.White : SystemColors.ControlText;

            control.BackColor = backColor;
            control.ForeColor = foreColor;

            if (control is ComboBox cmb)
            {
                cmb.FlatStyle = FlatStyle.Flat;
            }
            else if (control is Button btn)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = GetSystemAccentColor();
            }

            if (isDark && control.ContextMenuStrip != null)
            {
                control.ContextMenuStrip.Renderer = new DarkModeRenderer();
                control.ContextMenuStrip.ForeColor = Color.White;
                control.ContextMenuStrip.BackColor = Color.FromArgb(43, 43, 43);
            }

            foreach (Control child in control.Controls)
            {
                ApplyTheme(child);
            }
        }
    }

    public class DarkModeRenderer : ToolStripProfessionalRenderer
    {
        public DarkModeRenderer() : base(new DarkModeColorTable()) { }
    }

    public class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuBorder => Color.FromArgb(65, 65, 65);
        public override Color MenuItemBorder => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelected => Color.FromArgb(75, 75, 75);
        public override Color MenuStripGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color MenuStripGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(75, 75, 75);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(75, 75, 75);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(43, 43, 43);
    }
}
