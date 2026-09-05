using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.Methods;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
	public sealed class StoreAppsList : MyList
	{
		public void LoadItems()
		{
			this.ClearItems();
			var menus = UwpHelper.GetAppsWithContextMenu();

			// GRUPĂM MENIURILE DUPĂ NUMELE APLICAȚIEI
			var groupedMenus = menus.GroupBy(m => m.AppName).OrderBy(g => g.Key);

			foreach (var group in groupedMenus)
			{
				// 1. Creăm Header-ul aplicației
				var header = new StoreAppGroupHeader(group.Key, group.First());
				this.AddItem(header);

				// 2. Creăm și atașăm sub-meniurile
				foreach (var menu in group)
				{
					var item = new StoreAppMenuItem(menu);
					header.AddChildItem(item);
					this.AddItem(item);
				}

				// 3. Verificăm inițial dacă toate meniurile sunt active pentru a bifa "Master Switch-ul"
				header.UpdateMasterCheckState();
			}
		}
	}

	// --- CLASA PENTRU HEADER APLICAȚIE (Meniul Părinte) ---
	public sealed class StoreAppGroupHeader : MyListItem
	{
		readonly List<StoreAppMenuItem> childItems = new List<StoreAppMenuItem>();

		// Clonăm imaginea nativă pentru a o putea roti fără să afectăm restul aplicației
		readonly PictureButton btnFold = new PictureButton((Image)AppImage.Up.Clone());
		readonly MyCheckBox chkMaster = new MyCheckBox();

		bool isExpanded = false; // By default: restrâns (collapsed)
		bool suspendEvent = false;

		public StoreAppGroupHeader(string appName, UwpMenuInfo info)
		{
			this.Text = appName;
			this.Image = GetUwpIcon(info.InstallLocation, info.LogoRelativePath);
			this.Font = new Font(this.Font, FontStyle.Bold); // Bold pentru estetică

			// Săgeata pentru restrângere / extindere
			btnFold.Image.RotateFlip(RotateFlipType.Rotate180FlipNone); // Întoarsă în jos inițial
			ToolTipBox.SetToolTip(btnFold, "Expand / Collapse");
			btnFold.MouseDown += (sender, e) => ToggleFold();

			// Master Switch pentru toate meniurile aplicației (Dezactivare / Activare totală)
			ToolTipBox.SetToolTip(chkMaster, "Enable / Disable All Menus");
			chkMaster.CheckChanged += () =>
			{
				if (suspendEvent) return;

				foreach (var child in childItems)
				{
					// Schimbăm statusul fiecărui sub-meniu sincron
					child.SetCheckedState(chkMaster.Checked);
				}

				// Notificăm o singură dată după ce am schimbat X regiștri
				ExplorerRestarter.Show();
			};

			PictureButton btnOpen = new PictureButton(AppImage.Open);
			ToolTipBox.SetToolTip(btnOpen, AppString.Menu.FileLocation);
			btnOpen.MouseDown += (sender, e) => ExternalProgram.OpenDirectory(info.InstallLocation);

			this.AddCtr(btnFold);
			this.AddCtr(chkMaster);
			this.AddCtr(btnOpen);

			// Dacă apeși oriunde pe zona goală a textului aplicației se va deschide sub-meniul
			this.Cursor = Cursors.Hand;
			this.MouseDown += (sender, e) => ToggleFold();
		}

		public void AddChildItem(StoreAppMenuItem item)
		{
			childItems.Add(item);
			item.Visible = isExpanded; // ascundem din start elementul
			item.StateChanged += UpdateMasterCheckState; // ascultăm dacă userul dă click manual pe un sub-item
		}

		private void ToggleFold()
		{
			isExpanded = !isExpanded;
			// Rotim săgeata vizual
			btnFold.Image.RotateFlip(RotateFlipType.Rotate180FlipNone);
			btnFold.Invalidate();

			// Afișăm sau ascundem rândurile din josul ei
			foreach (var child in childItems)
			{
				child.Visible = isExpanded;
			}
		}

		public void UpdateMasterCheckState()
		{
			bool allChecked = true;
			foreach (var child in childItems)
			{
				if (!child.IsChecked) { allChecked = false; break; }
			}

			suspendEvent = true; // Împiedicăm CheckChanged-ul din a declanșa loop-ul Master înapoi
			chkMaster.Checked = allChecked;
			suspendEvent = false;
		}

		private Image GetUwpIcon(string installLocation, string logoRelPath)
		{
			Image rawImg = null;
			if (!string.IsNullOrEmpty(logoRelPath))
			{
				try
				{
					string fullPath = Path.Combine(installLocation, logoRelPath.Replace('/', '\\'));
					string dir = Path.GetDirectoryName(fullPath);
					string name = Path.GetFileNameWithoutExtension(fullPath);

					if (Directory.Exists(dir))
					{
						if (File.Exists(fullPath)) rawImg = Image.FromFile(fullPath);
						else
						{
							string[] matches = Directory.GetFiles(dir, name + "*.png");
							if (matches.Length > 0) rawImg = Image.FromFile(matches[0]);
						}
					}
				}
				catch { }
			}

			if (rawImg == null) rawImg = new Bitmap(AppImage.MicrosoftStore);

			int size = 26.DpiZoom();
			Bitmap bmp = new Bitmap(size, size);
			using (Graphics g = Graphics.FromImage(bmp))
			{
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				g.DrawImage(rawImg, new Rectangle(0, 0, size, size));
			}

			rawImg.Dispose();
			return bmp;
		}
	}

	// --- CLASA PENTRU FIECARE ACȚIUNE DE MENIU (Sub-meniul Copil) ---
	public sealed class StoreAppMenuItem : MyListItem
	{
		readonly MyCheckBox chkVisible = new MyCheckBox();
		const string BlockedRegPath = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

		readonly UwpMenuInfo menuInfo;
		readonly string guidStr;

		public Action StateChanged; // Transmite către părinte (Header) că și-a schimbat starea
		bool suspendEvent = false;

		public StoreAppMenuItem(UwpMenuInfo menu)
		{
			this.menuInfo = menu;
			this.guidStr = menu.Clsid.ToString("B");
			string targetDisplay = FormatTargetType(menu.TargetType);

			this.Text = $"        [{targetDisplay}]  -  {menu.MenuId}";
			this.Image = AppImage.SubItems;

			chkVisible.Checked = !IsBlocked(guidStr);

			// Logică de declanșare a click-ului individual
			chkVisible.CheckChanged += () =>
			{
				if (suspendEvent) return;

				SetRegistryState(chkVisible.Checked);
				StateChanged?.Invoke(); // anunță Master Switch-ul
				ExplorerRestarter.Show();
			};

			this.AddCtr(chkVisible);
		}

		public bool IsChecked => chkVisible.Checked;

		// Această funcție e apelată DOAR de Master Switch-ul de sus
		public void SetCheckedState(bool check)
		{
			if (chkVisible.Checked == check) return;

			suspendEvent = true; // Suspendăm evenimentul local pentru a evita ferestre multiple de restart
			chkVisible.Checked = check;
			SetRegistryState(check);
			suspendEvent = false;
		}

		private void SetRegistryState(bool check)
		{
			if (check) UnblockMenu(guidStr);
			else BlockMenu(guidStr, menuInfo.MenuId);
		}

		private string FormatTargetType(string type)
		{
			if (string.IsNullOrEmpty(type)) return "General";
			if (type == "*") return "All Files";
			if (type.Equals("Directory", StringComparison.OrdinalIgnoreCase)) return "Folder";
			if (type.Equals(@"Directory\Background", StringComparison.OrdinalIgnoreCase)) return "Folder Background";
			if (type.Equals("DesktopBackground", StringComparison.OrdinalIgnoreCase)) return "Desktop";
			if (type.Equals("Drive", StringComparison.OrdinalIgnoreCase)) return "Drive";
			if (type.StartsWith(".")) return type.ToUpper();
			return type;
		}

		private bool IsBlocked(string guid)
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(BlockedRegPath))
			{
				return key?.GetValue(guid) != null;
			}
		}

		private void BlockMenu(string guid, string menuId)
		{
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(BlockedRegPath))
			{
				key.SetValue(guid, menuId, RegistryValueKind.String);
			}
		}

		private void UnblockMenu(string guid)
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(BlockedRegPath, true))
			{
				key?.DeleteValue(guid, false);
			}
		}
	}
}