using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ContextMenuManager.Methods
{
    static class IconHelper
    {
        private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Image GetIconImage(string iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation)) return null;

            iconLocation = iconLocation.Trim('\"', ' ');
            if (IconCache.TryGetValue(iconLocation, out Image cached))
            {
                return cached;
            }

            Image img = ExtractIconInternal(iconLocation);
            if (img != null)
            {
                IconCache[iconLocation] = img;
            }
            return img;
        }

        private static Image ExtractIconInternal(string iconLocation)
        {
            try
            {
                string path = iconLocation;
                int index = 0;

                int commaIndex = iconLocation.LastIndexOf(',');
                if (commaIndex > 0)
                {
                    string indexStr = iconLocation.Substring(commaIndex + 1).Trim();
                    if (int.TryParse(indexStr, out int parsedIndex))
                    {
                        index = parsedIndex;
                        path = iconLocation.Substring(0, commaIndex).Trim();
                    }
                }

                path = Environment.ExpandEnvironmentVariables(path);
                if (!Path.IsPathRooted(path))
                {
                    string sysPath = Path.Combine(Environment.SystemDirectory, path);
                    if (File.Exists(sysPath)) path = sysPath;
                }

                if (!File.Exists(path)) return null;

                if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using (var ico = new Icon(path, 32, 32))
                    {
                        return ico.ToBitmap();
                    }
                }

                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];

                uint read = ExtractIconEx(path, index, large, small, 1);
                IntPtr hIcon = IntPtr.Zero;

                if (small[0] != IntPtr.Zero) hIcon = small[0];
                else if (large[0] != IntPtr.Zero) hIcon = large[0];

                if (hIcon != IntPtr.Zero)
                {
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        Bitmap bmp = (Bitmap)icon.ToBitmap().Clone();
                        if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                        if (small[0] != IntPtr.Zero && small[0] != large[0]) DestroyIcon(small[0]);
                        return bmp;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}