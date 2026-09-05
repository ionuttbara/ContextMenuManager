using BluePointLilac.Methods;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ContextMenuManager.Methods
{
	// Model de date ce include "TargetType" (Cauza/Unde va apărea meniul)
	public class UwpMenuInfo
	{
		public string AppName { get; set; }
		public string MenuId { get; set; }
		public string TargetType { get; set; }
		public Guid Clsid { get; set; }
		public string InstallLocation { get; set; }
		public string LogoRelativePath { get; set; }
	}

	public static class UwpHelper
	{
		private const string PackageRegPath = @"HKEY_CLASSES_ROOT\PackagedCom\Package";
		private const string PackagesRegPath = @"HKEY_CLASSES_ROOT\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Packages";

		// --- METODELE ORIGINALE RESTAURATE ȘI CORECTATE ---
		public static string GetPackageName(string uwpName)
		{
			if (string.IsNullOrEmpty(uwpName)) return null;

			using (RegistryKey root = RegistryEx.GetRegistryKey(PackageRegPath))
			{
				if (root == null) return null;
				foreach (string pkgName in root.GetSubKeyNames())
				{
					if (pkgName.StartsWith(uwpName, StringComparison.OrdinalIgnoreCase)) return pkgName;
				}
			}
			return null;
		}

		public static string GetRegPath(string pkgName)
		{
			if (string.IsNullOrEmpty(pkgName)) return null;
			return $@"{PackagesRegPath}\{pkgName}";
		}

		public static string GetRegPath(string uwpName, Guid guid)
		{
			string pkgName = GetPackageName(uwpName);
			if (pkgName == null) return null;
			return $@"{PackageRegPath}\{pkgName}\Class\{guid.ToString("B")}";
		}

		public static string GetFilePath(string pkgName)
		{
			string regPath = GetRegPath(pkgName);
			if (regPath == null) return null;

			using (RegistryKey key = RegistryEx.GetRegistryKey(regPath))
			{
				if (key != null) return key.GetValue("Path")?.ToString();
			}
			return null;
		}

		public static string GetFilePath(string uwpName, Guid guid)
		{
			string regPath = GetRegPath(uwpName, guid);
			if (regPath == null) return null;

			using (RegistryKey key = RegistryEx.GetRegistryKey(regPath))
			{
				if (key != null) return key.GetValue("DllPath")?.ToString();
			}
			return null;
		}

		// --- METODA CARE PREIA MENIURILE WIN 11 + CAUZA LOR ---
		public static List<UwpMenuInfo> GetAppsWithContextMenu()
		{
			var menus = new List<UwpMenuInfo>();

			using (RegistryKey packagesKey = RegistryEx.GetRegistryKey(PackagesRegPath))
			{
				if (packagesKey == null) return menus;

				foreach (string pkgName in packagesKey.GetSubKeyNames())
				{
					using (RegistryKey pkgKey = packagesKey.OpenSubKey(pkgName))
					{
						if (pkgKey == null) continue;

						string path = pkgKey.GetValue("Path")?.ToString();
						if (string.IsNullOrEmpty(path)) continue;

						string manifestPath = $@"{path}\AppxManifest.xml";
						if (!File.Exists(manifestPath)) continue;

						try
						{
							XmlDocument doc = new XmlDocument();
							doc.Load(manifestPath);

							XmlNodeList verbs = doc.SelectNodes("//*[local-name()='FileExplorerContextMenus']//*[local-name()='Verb']");

							if (verbs != null && verbs.Count > 0)
							{
								XmlNode identity = doc.SelectSingleNode("//*[local-name()='Identity']");
								XmlNode properties = doc.SelectSingleNode("//*[local-name()='Properties']");

								string appName = properties?.SelectSingleNode("*[local-name()='DisplayName']")?.InnerText ?? pkgName;
								if (appName.StartsWith("ms-resource:")) appName = identity?.Attributes["Name"]?.Value ?? pkgName;

								string logoPath = properties?.SelectSingleNode("*[local-name()='Square44x44Logo']")?.InnerText
											   ?? properties?.SelectSingleNode("*[local-name()='Logo']")?.InnerText;

								foreach (XmlNode verb in verbs)
								{
									string clsidStr = verb.Attributes["Clsid"]?.Value;
									string menuId = verb.Attributes["Id"]?.Value;

									// EXTRAGEM ȚINTA (unde va apărea pe ecran: folder, fișier text etc.)
									string targetType = verb.ParentNode?.Attributes["Type"]?.Value ?? "Unknown";

									if (Guid.TryParse(clsidStr, out Guid clsid))
									{
										menus.Add(new UwpMenuInfo
										{
											AppName = appName,
											MenuId = menuId,
											TargetType = targetType,
											Clsid = clsid,
											InstallLocation = path,
											LogoRelativePath = logoPath
										});
									}
								}
							}
						}
						catch { }
					}
				}
			}
			return menus;
		}
	}
}