using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class AppSettingBox : MyList
    {
        public AppSettingBox()
        {
            this.SuspendLayout();
            this.Font = SystemFonts.MenuFont;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size + 1F);
            mliConfigDir.AddCtrs(new Control[] { cmbConfigDir, btnConfigDir });
            mliBackup.AddCtrs(new Control[] { chkBackup, btnBackupDir });
            mliRepo.AddCtr(cmbRepo);
            mliTopMost.AddCtr(chkTopMost);
            mliProtect.AddCtr(chkProtect);
            mliWinXSortable.AddCtr(chkWinXSortable);
            mliShowFilePath.AddCtr(chkShowFilePath);
            mliOpenMoreRegedit.AddCtr(chkOpenMoreRegedit);
            mliOpenMoreExplorer.AddCtr(chkOpenMoreExplorer);
            mliHideDisabledItems.AddCtr(chkHideDisabledItems);
            mliHideSysStoreItems.AddCtr(chkHideSysStoreItems);

            ToolTipBox.SetToolTip(cmbConfigDir, AppString.Tip.ConfigPath);
            ToolTipBox.SetToolTip(btnConfigDir, AppString.Menu.FileLocation);
            ToolTipBox.SetToolTip(btnBackupDir, AppString.Menu.FileLocation);

            cmbRepo.Items.AddRange(new[] { "Github", "Gitee" });
            cmbConfigDir.Items.AddRange(new[] { AppString.Other.AppDataDir, AppString.Other.AppDir });
            cmbConfigDir.AutosizeDropDownWidth();
            cmbRepo.AutosizeDropDownWidth();

            
            btnConfigDir.MouseDown += (sender, e) => ExternalProgram.OpenDirectory(AppConfig.ConfigDir);
            btnBackupDir.MouseDown += (sender, e) => ExternalProgram.OpenDirectory(AppConfig.BackupDir);
            chkBackup.CheckChanged += () => AppConfig.AutoBackup = chkBackup.Checked;
            chkProtect.CheckChanged += () => AppConfig.ProtectOpenItem = chkProtect.Checked;
            chkWinXSortable.CheckChanged += () => AppConfig.WinXSortable = chkWinXSortable.Checked;
            chkOpenMoreRegedit.CheckChanged += () => AppConfig.OpenMoreRegedit = chkOpenMoreRegedit.Checked;
            chkTopMost.CheckChanged += () => AppConfig.TopMost = this.FindForm().TopMost = chkTopMost.Checked;
            chkOpenMoreExplorer.CheckChanged += () => AppConfig.OpenMoreExplorer = chkOpenMoreExplorer.Checked;
            chkHideDisabledItems.CheckChanged += () => AppConfig.HideDisabledItems = chkHideDisabledItems.Checked;
            chkHideSysStoreItems.CheckChanged += () => AppConfig.HideSysStoreItems = chkHideSysStoreItems.Checked;
            chkShowFilePath.CheckChanged += () => AppConfig.ShowFilePath = chkShowFilePath.Checked;
            cmbConfigDir.SelectionChangeCommitted += (sender, e) => ChangeConfigDir();
            this.ResumeLayout();
			ThemeManager.ApplyTheme(this);
		}

        readonly MyListItem mliConfigDir = new MyListItem
        {
            Text = AppString.Other.ConfigPath
        };
        readonly ComboBox cmbConfigDir = new ComboBox();
        readonly PictureButton btnConfigDir = new PictureButton(AppImage.Open);

        readonly MyListItem mliRepo = new MyListItem
        {
            Text = AppString.Other.SetRequestRepo
        };
        readonly ComboBox cmbRepo = new ComboBox();

        readonly MyListItem mliBackup = new MyListItem
        {
            Text = AppString.Other.AutoBackup
        };
        readonly MyCheckBox chkBackup = new MyCheckBox();
        readonly PictureButton btnBackupDir = new PictureButton(AppImage.Open);

        readonly MyListItem mliTopMost = new MyListItem
        {
            Text = AppString.Other.TopMost
        };
        readonly MyCheckBox chkTopMost = new MyCheckBox();

        readonly MyListItem mliProtect = new MyListItem
        {
            Text = AppString.Other.ProtectOpenItem
        };
        readonly MyCheckBox chkProtect = new MyCheckBox();


        readonly MyListItem mliShowFilePath = new MyListItem
        {
            Text = AppString.Other.ShowFilePath
        };
        readonly MyCheckBox chkShowFilePath = new MyCheckBox();

        readonly MyListItem mliOpenMoreRegedit = new MyListItem
        {
            Text = AppString.Other.OpenMoreRegedit
        };
        readonly MyCheckBox chkOpenMoreRegedit = new MyCheckBox();

        readonly MyListItem mliOpenMoreExplorer = new MyListItem
        {
            Text = AppString.Other.OpenMoreExplorer
        };
        readonly MyCheckBox chkOpenMoreExplorer = new MyCheckBox();

        readonly MyListItem mliHideDisabledItems = new MyListItem
        {
            Text = AppString.Other.HideDisabledItems
        };
        readonly MyCheckBox chkHideDisabledItems = new MyCheckBox();

        readonly MyListItem mliWinXSortable = new MyListItem
        {
            Text = AppString.Other.WinXSortable,
            Visible = WinOsVersion.Current >= WinOsVersion.Win8
        };
        readonly MyCheckBox chkWinXSortable = new MyCheckBox();

        readonly MyListItem mliHideSysStoreItems = new MyListItem
        {
            Text = AppString.Other.HideSysStoreItems,
            Visible = WinOsVersion.Current >= WinOsVersion.Win7
        };
        readonly MyCheckBox chkHideSysStoreItems = new MyCheckBox();

        public override void ClearItems()
        {
            this.Controls.Clear();
        }

        public void LoadItems()
        {
            this.AddItems(new[] { mliConfigDir, mliBackup, mliTopMost, mliProtect, mliShowFilePath,
                mliHideDisabledItems, mliHideSysStoreItems, mliOpenMoreRegedit, mliOpenMoreExplorer, mliWinXSortable });
            foreach(MyListItem item in this.Controls) item.HasImage = false;
            cmbConfigDir.SelectedIndex = AppConfig.SaveToAppDir ? 1 : 0;
            chkBackup.Checked = AppConfig.AutoBackup;
            chkTopMost.Checked = this.FindForm().TopMost;
            chkProtect.Checked = AppConfig.ProtectOpenItem;
            chkWinXSortable.Checked = AppConfig.WinXSortable;
            chkShowFilePath.Checked = AppConfig.ShowFilePath;
            chkOpenMoreRegedit.Checked = AppConfig.OpenMoreRegedit;
            chkOpenMoreExplorer.Checked = AppConfig.OpenMoreExplorer;
            chkHideDisabledItems.Checked = AppConfig.HideDisabledItems;
            chkHideSysStoreItems.Checked = AppConfig.HideSysStoreItems;
        }

        private void ChangeConfigDir()
        {
            string newPath = (cmbConfigDir.SelectedIndex == 0) ? AppConfig.AppDataConfigDir : AppConfig.AppConfigDir;
            if(newPath == AppConfig.ConfigDir) return;
            if(AppMessageBox.Show(AppString.Message.RestartApp, MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                cmbConfigDir.SelectedIndex = AppConfig.SaveToAppDir ? 1 : 0;
            }
            else
            {
                DirectoryEx.CopyTo(AppConfig.ConfigDir, newPath);
                Directory.Delete(AppConfig.ConfigDir, true);
                SingleInstance.Restart();
            }
        }

       
    }
}