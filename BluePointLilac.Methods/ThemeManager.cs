using Microsoft.Win32;
using System.Drawing;
using System.Windows.Forms;

namespace ContextMenuManager.BluePointLilac.Methods
{
	public static class ThemeManager
	{
		// Check if Windows is in Dark Mode
		public static bool IsDarkMode()
		{
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
				{
					if (key != null)
					{
						object value = key.GetValue("AppsUseLightTheme");
						return value != null && (int)value == 0;
					}
				}
			}
			catch { }
			return false; // Default to light mode
		}

		// Get the System Accent Color
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
							int colorInfo = (int)value;
							return Color.FromArgb(
								255, // Force Alpha to 255 (Opaque)
								(byte)((colorInfo >> 16) & 0xFF), // R
								(byte)((colorInfo >> 8) & 0xFF),  // G
								(byte)(colorInfo & 0xFF)          // B
							);
						}
					}
				}
			}
			catch { }
			return SystemColors.Highlight; // Fallback default color
		}

		public static void ApplyTheme(Control control)
		{
			bool isDark = IsDarkMode();

			// Define your color palette
			Color backColor = isDark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
			Color foreColor = isDark ? Color.White : SystemColors.ControlText;

			// Apply to the current control
			control.BackColor = backColor;
			control.ForeColor = foreColor;

			// Apply specific styles to specialized controls
			if (control is ComboBox cmb)
			{
				cmb.FlatStyle = FlatStyle.Flat;
			}
			else if (control is Button btn)
			{
				btn.FlatStyle = FlatStyle.Flat;
				btn.FlatAppearance.BorderColor = GetSystemAccentColor();
			}

			// Apply theme to ContextMenus attached directly to controls
			if (isDark && control.ContextMenuStrip != null)
			{
				control.ContextMenuStrip.Renderer = new DarkModeRenderer();
				control.ContextMenuStrip.ForeColor = Color.White;
				control.ContextMenuStrip.BackColor = Color.FromArgb(43, 43, 43);
			}

			// Recursively apply to all child controls (panels, lists, etc.)
			foreach (Control child in control.Controls)
			{
				ApplyTheme(child);
			}
		}
	}

	// --- MUST BE OUTSIDE THE ThemeManager CLASS, BUT INSIDE THE NAMESPACE ---

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