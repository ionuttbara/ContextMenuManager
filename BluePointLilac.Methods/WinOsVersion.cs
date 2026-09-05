using System;

namespace BluePointLilac.Methods
{
    // 判断Windows系统版本
    // https://docs.microsoft.com/windows/release-health/release-information
    public static class WinOsVersion
    {
        public static readonly Version Current = Environment.OSVersion.Version;
        public static readonly Version Win10 = new Version(10, 0);
        public static readonly Version Win8_1 = new Version(6, 3);
        public static readonly Version Win8 = new Version(6, 2);
        public static readonly Version Win7 = new Version(6, 1);
        public static readonly Version Vista = new Version(6, 0);
        public static readonly Version XP = new Version(5, 1);

        public static readonly Version Win10_1507 = new Version(10, 0, 10240);
        public static readonly Version Win10_1511 = new Version(10, 0, 10586);
        public static readonly Version Win10_1607 = new Version(10, 0, 14393);
        public static readonly Version Win10_1703 = new Version(10, 0, 15063);
        public static readonly Version Win10_1709 = new Version(10, 0, 16299);
        public static readonly Version Win10_1803 = new Version(10, 0, 17134);
        public static readonly Version Win10_1809 = new Version(10, 0, 17763);
        public static readonly Version Win10_1903 = new Version(10, 0, 18362);
        public static readonly Version Win10_1909 = new Version(10, 0, 18363);
        public static readonly Version Win10_2004 = new Version(10, 0, 19041);
        public static readonly Version Win10_20H2 = new Version(10, 0, 19042);
		public static readonly Version Win10_21H2 = new Version(10, 0, 19043);
		public static readonly Version Win10_22H2 = new Version(10, 0, 19045);
		public static readonly Version Win11_21H2 = new Version(10, 0, 20000);
		public static readonly Version Win11_22H2 = new Version(10, 0, 22621);
		public static readonly Version Win11_23H2 = new Version(10, 0, 22631);
		public static readonly Version Win11_24H2 = new Version(10, 0, 26100);
		public static readonly Version Win11_25H2 = new Version(10, 0, 26120);
		public static readonly Version Win11_26H1 = new Version(10, 0, 28000);
	}
}