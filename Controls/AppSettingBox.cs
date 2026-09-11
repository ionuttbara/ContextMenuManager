using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System.Drawing;

namespace ContextMenuManager.Controls
{
    sealed class AppSettingBox : MyList
    {
        public AppSettingBox()
        {
            this.SuspendLayout();
            this.Font = SystemFonts.MenuFont;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size + 1F);

            mliTopMost.AddCtr(chkTopMost);
            mliProtect.AddCtr(chkProtect);
            mliWinXSortable.AddCtr(chkWinXSortable);
            mliShowFilePath.AddCtr(chkShowFilePath);
            mliOpenMoreRegedit.AddCtr(chkOpenMoreRegedit);
            mliOpenMoreExplorer.AddCtr(chkOpenMoreExplorer);
            mliHideDisabledItems.AddCtr(chkHideDisabledItems);
            mliHideSysStoreItems.AddCtr(chkHideSysStoreItems);

            chkProtect.CheckChanged += () => AppConfig.ProtectOpenItem = chkProtect.Checked;
            chkWinXSortable.CheckChanged += () => AppConfig.WinXSortable = chkWinXSortable.Checked;
            chkOpenMoreRegedit.CheckChanged += () => AppConfig.OpenMoreRegedit = chkOpenMoreRegedit.Checked;
            chkTopMost.CheckChanged += () => AppConfig.TopMost = this.FindForm().TopMost = chkTopMost.Checked;
            chkOpenMoreExplorer.CheckChanged += () => AppConfig.OpenMoreExplorer = chkOpenMoreExplorer.Checked;
            chkHideDisabledItems.CheckChanged += () => AppConfig.HideDisabledItems = chkHideDisabledItems.Checked;
            chkHideSysStoreItems.CheckChanged += () => AppConfig.HideSysStoreItems = chkHideSysStoreItems.Checked;
            chkShowFilePath.CheckChanged += () => AppConfig.ShowFilePath = chkShowFilePath.Checked;

            this.ResumeLayout();
            ThemeManager.ApplyTheme(this);
        }

        readonly MyListItem mliTopMost = new MyListItem { Text = AppString.Other.TopMost };
        readonly MyCheckBox chkTopMost = new MyCheckBox();

        readonly MyListItem mliProtect = new MyListItem { Text = AppString.Other.ProtectOpenItem };
        readonly MyCheckBox chkProtect = new MyCheckBox();

        readonly MyListItem mliShowFilePath = new MyListItem { Text = AppString.Other.ShowFilePath };
        readonly MyCheckBox chkShowFilePath = new MyCheckBox();

        readonly MyListItem mliOpenMoreRegedit = new MyListItem { Text = AppString.Other.OpenMoreRegedit };
        readonly MyCheckBox chkOpenMoreRegedit = new MyCheckBox();

        readonly MyListItem mliOpenMoreExplorer = new MyListItem { Text = AppString.Other.OpenMoreExplorer };
        readonly MyCheckBox chkOpenMoreExplorer = new MyCheckBox();

        readonly MyListItem mliHideDisabledItems = new MyListItem { Text = AppString.Other.HideDisabledItems };
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
            this.AddItems(new[]
            {
                mliTopMost, mliProtect, mliShowFilePath, mliHideDisabledItems,
                mliHideSysStoreItems, mliOpenMoreRegedit, mliOpenMoreExplorer, mliWinXSortable
            });
            foreach (MyListItem item in this.Controls) item.HasImage = false;

            chkTopMost.Checked = this.FindForm().TopMost;
            chkProtect.Checked = AppConfig.ProtectOpenItem;
            chkWinXSortable.Checked = AppConfig.WinXSortable;
            chkShowFilePath.Checked = AppConfig.ShowFilePath;
            chkOpenMoreRegedit.Checked = AppConfig.OpenMoreRegedit;
            chkOpenMoreExplorer.Checked = AppConfig.OpenMoreExplorer;
            chkHideDisabledItems.Checked = AppConfig.HideDisabledItems;
            chkHideSysStoreItems.Checked = AppConfig.HideSysStoreItems;
        }
    }
}
