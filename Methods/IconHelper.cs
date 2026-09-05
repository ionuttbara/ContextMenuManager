using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ContextMenuManager.Methods
{
    static class IconHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Image GetIconImage(string iconLocation, int targetSize = 32)
        {
            if (string.IsNullOrWhiteSpace(iconLocation)) return null;

            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(iconLocation).Trim().Trim('"');
                string filePath = expanded;
                int iconIndex = 0;

                int commaIdx = expanded.LastIndexOf(',');
                if (commaIdx > 0)
                {
                    string indexPart = expanded.Substring(commaIdx + 1).Trim();
                    if (int.TryParse(indexPart, out int idx))
                    {
                        iconIndex = idx;
                        filePath = expanded.Substring(0, commaIdx).Trim().Trim('"');
                    }
                }

                filePath = ResolveFilePath(filePath);
                if (!File.Exists(filePath)) return null;

                if (string.Equals(Path.GetExtension(filePath), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using (var ico = new Icon(filePath, targetSize, targetSize))
                    {
                        return ico.ToBitmap();
                    }
                }

                IntPtr[] largeIcons = new IntPtr[1];
                IntPtr[] smallIcons = new IntPtr[1];
                uint extracted = ExtractIconEx(filePath, iconIndex, largeIcons, smallIcons, 1);

                IntPtr hIcon = (targetSize <= 16 && smallIcons[0] != IntPtr.Zero) ? smallIcons[0] : largeIcons[0];
                if (hIcon == IntPtr.Zero && smallIcons[0] != IntPtr.Zero) hIcon = smallIcons[0];

                if (hIcon != IntPtr.Zero)
                {
                    using (var icon = Icon.FromHandle(hIcon))
                    {
                        Bitmap bmp = icon.ToBitmap();
                        if (largeIcons[0] != IntPtr.Zero) DestroyIcon(largeIcons[0]);
                        if (smallIcons[0] != IntPtr.Zero && smallIcons[0] != largeIcons[0]) DestroyIcon(smallIcons[0]);
                        return bmp;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string ResolveFilePath(string fileName)
        {
            if (File.Exists(fileName)) return fileName;

            string sys32 = Path.Combine(Environment.SystemDirectory, fileName);
            if (File.Exists(sys32)) return sys32;

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string inWin = Path.Combine(winDir, fileName);
            if (File.Exists(inWin)) return inWin;

            string psDir = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0", fileName);
            if (File.Exists(psDir)) return psDir;

            return fileName;
        }
    }
}