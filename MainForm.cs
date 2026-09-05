using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager
{
    sealed class MainForm : MyMainForm
    {
        public MainForm()
        {
            this.TopMost = AppConfig.TopMost;
            this.Size = AppConfig.MainFormSize;
            this.Text = AppString.General.AppName;
            this.Controls.Add(explorerRestarter);
            ToolBar.AddButtons(ToolBarButtons);
            MainBody.Controls.AddRange(MainControls);
            ToolBar.SelectedButtonChanged += (sender, e) => SwitchTab();
            SideBar.HoverIndexChanged += (sender, e) => ShowItemInfo();
            SideBar.SelectIndexChanged += (sender, e) => SwitchItem();
            this.FormClosing += (sender, e) => CloseMainForm();
            HoveredToShowItemPath();
            DragDropToAnalysis();
            AddContextMenus();
            ResizeSideBar();
            JumpItem(0, 0);
        }

        readonly MyToolBarButton[] ToolBarButtons =
        {
            new MyToolBarButton(AppImage.Home, "Current items"),
            new MyToolBarButton(AppImage.Type, AppString.ToolBar.Type),
            new MyToolBarButton(AppImage.Custom, "Add Custom Items"),
            new MyToolBarButton(AppImage.About, AppString.ToolBar.About)
        };

        private Control[] MainControls => new Control[]
        {
            shellList, shellNewList, sendToList, openWithList, winXList,
            detailedEditList, guidBlockedList, iEList,
            appSettingBox, dictionariesBox, aboutMeBox, storeAppsList,
            customPresetList
        };

        readonly ShellList shellList = new ShellList();
        readonly StoreAppsList storeAppsList = new StoreAppsList();
        readonly ShellNewList shellNewList = new ShellNewList();
        readonly SendToList sendToList = new SendToList();
        readonly OpenWithList openWithList = new OpenWithList();
        readonly WinXList winXList = new WinXList();
        readonly DetailedEditList detailedEditList = new DetailedEditList();
        readonly GuidBlockedList guidBlockedList = new GuidBlockedList();
        readonly IEList iEList = new IEList();
        readonly AppSettingBox appSettingBox = new AppSettingBox();
        readonly DictionariesBox dictionariesBox = new DictionariesBox();
        readonly ReadOnlyRichTextBox aboutMeBox = new ReadOnlyRichTextBox();
        readonly ExplorerRestarter explorerRestarter = new ExplorerRestarter();
        readonly CustomPresetList customPresetList = new CustomPresetList();

        // Tab 0: Current items (toate elementele existente din sistem)
        static readonly string[] GeneralItems =
        {
            AppString.SideBar.File,
            AppString.SideBar.Folder,
            AppString.SideBar.Directory,
            AppString.SideBar.Background,
            AppString.SideBar.Desktop,
            AppString.SideBar.Drive,
            AppString.SideBar.AllObjects,
            AppString.SideBar.Computer,
            AppString.SideBar.RecycleBin,
            AppString.SideBar.Library,
            "Microsoft Store Apps",
            null,
            AppString.SideBar.New,
            AppString.SideBar.SendTo,
            AppString.SideBar.OpenWith,
            null,
            AppString.SideBar.WinX
        };

        static readonly string[] GeneralItemInfos =
        {
            AppString.StatusBar.File,
            AppString.StatusBar.Folder,
            AppString.StatusBar.Directory,
            AppString.StatusBar.Background,
            AppString.StatusBar.Desktop,
            AppString.StatusBar.Drive,
            AppString.StatusBar.AllObjects,
            AppString.StatusBar.Computer,
            AppString.StatusBar.RecycleBin,
            AppString.StatusBar.Library,
            "UWP apps that add Windows 11 context menus",
            null,
            AppString.StatusBar.New,
            AppString.StatusBar.SendTo,
            AppString.StatusBar.OpenWith,
            null,
            AppString.StatusBar.WinX
        };

        static readonly ShellList.Scenes[] GeneralShellScenes =
        {
            ShellList.Scenes.File,
            ShellList.Scenes.Folder,
            ShellList.Scenes.Directory,
            ShellList.Scenes.Background,
            ShellList.Scenes.Desktop,
            ShellList.Scenes.Drive,
            ShellList.Scenes.AllObjects,
            ShellList.Scenes.Computer,
            ShellList.Scenes.RecycleBin,
            ShellList.Scenes.Library
        };

        // Tab 1: File Types
        static readonly string[] TypeItems =
        {
            AppString.SideBar.LnkFile,
            AppString.SideBar.UwpLnk,
            AppString.SideBar.ExeFile,
            null,
            AppString.SideBar.CustomExtension,
            AppString.SideBar.PerceivedType,
            AppString.SideBar.DirectoryType,
            null,
            AppString.SideBar.UnknownType,
            AppString.SideBar.MenuAnalysis
        };

        static readonly string[] TypeItemInfos =
        {
            AppString.StatusBar.LnkFile,
            AppString.StatusBar.UwpLnk,
            AppString.StatusBar.ExeFile,
            null,
            AppString.StatusBar.CustomExtension,
            AppString.StatusBar.PerceivedType,
            AppString.StatusBar.DirectoryType,
            null,
            AppString.StatusBar.UnknownType,
            AppString.StatusBar.MenuAnalysis
        };

        static readonly ShellList.Scenes?[] TypeShellScenes =
        {
            ShellList.Scenes.LnkFile,
            ShellList.Scenes.UwpLnk,
            ShellList.Scenes.ExeFile,
            null,
            ShellList.Scenes.CustomExtension,
            ShellList.Scenes.PerceivedType,
            ShellList.Scenes.DirectoryType,
            null,
            ShellList.Scenes.UnknownType,
            ShellList.Scenes.MenuAnalysis
        };

        // Tab 2: Add Custom Items
        static readonly string[] CustomRuleItems =
        {
            AppString.SideBar.Desktop,
            AppString.SideBar.Folder,
            AppString.SideBar.File,
            AppString.SideBar.Drive
        };

        static readonly string[] CustomRuleItemInfos =
        {
            AppString.StatusBar.Desktop,
            AppString.StatusBar.Folder,
            AppString.StatusBar.File,
            AppString.StatusBar.Drive
        };

        // Tab 3: About
        static readonly string[] AboutItems =
        {
            AppString.SideBar.AppSetting,
            AppString.SideBar.Dictionaries,
            AppString.SideBar.AboutApp
        };

        static readonly string[] SettingItems =
        {
            AppString.Other.TopMost,
            null,
            AppString.Other.ShowFilePath,
            AppString.Other.HideDisabledItems,
            null,
            AppString.Other.OpenMoreRegedit,
            AppString.Other.OpenMoreExplorer
        };

        readonly int[] lastItemIndex = new int[4];

        public void JumpItem(int toolBarIndex, int sideBarIndex)
        {
            bool flag1 = ToolBar.SelectedIndex == toolBarIndex;
            bool flag2 = SideBar.SelectedIndex == sideBarIndex;
            lastItemIndex[toolBarIndex] = sideBarIndex;
            ToolBar.SelectedIndex = toolBarIndex;
            if (flag1 || flag2)
            {
                SideBar.SelectedIndex = sideBarIndex;
                SwitchItem();
            }
        }

        private void SwitchTab()
        {
            switch (ToolBar.SelectedIndex)
            {
                case 0:
                    SideBar.ItemNames = GeneralItems;
                    break;
                case 1:
                    SideBar.ItemNames = TypeItems;
                    break;
                case 2:
                    SideBar.ItemNames = CustomRuleItems;
                    break;
                case 3:
                    SideBar.ItemNames = AboutItems;
                    break;
            }
            SideBar.SelectedIndex = lastItemIndex[ToolBar.SelectedIndex];
        }

        private void SwitchItem()
        {
            foreach (Control ctr in MainControls)
            {
                ctr.Visible = false;
                if (ctr is MyList list) list.ClearItems();
            }
            if (SideBar.SelectedIndex == -1) return;

            switch (ToolBar.SelectedIndex)
            {
                case 0:
                    SwitchGeneralItem();
                    break;
                case 1:
                    SwitchTypeItem();
                    break;
                case 2:
                    SwitchCustomRuleItem();
                    break;
                case 3:
                    SwitchAboutItem();
                    break;
            }
            lastItemIndex[ToolBar.SelectedIndex] = SideBar.SelectedIndex;
            this.SuspendMainBodyWhenMove = MainControls.ToList().Any(ctr => ctr.Controls.Count > 50);
        }

        private void ShowItemInfo()
        {
            if (SideBar.HoveredIndex >= 0)
            {
                int i = SideBar.HoveredIndex;
                switch (ToolBar.SelectedIndex)
                {
                    case 0:
                        if (i < GeneralItemInfos.Length) { StatusBar.Text = GeneralItemInfos[i]; return; }
                        break;
                    case 1:
                        if (i < TypeItemInfos.Length) { StatusBar.Text = TypeItemInfos[i]; return; }
                        break;
                    case 2:
                        if (i < CustomRuleItemInfos.Length) { StatusBar.Text = CustomRuleItemInfos[i]; return; }
                        break;
                }
            }
            StatusBar.Text = MyStatusBar.DefaultText;
        }

        private void HoveredToShowItemPath()
        {
            foreach (Control ctr in MainBody.Controls)
            {
                if (ctr is MyList list && list != appSettingBox)
                {
                    list.HoveredItemChanged += (sender, e) =>
                    {
                        MyListItem item = list.HoveredItem;
                        if (item == null)
                        {
                            StatusBar.Text = MyStatusBar.DefaultText;
                            return;
                        }

                        
                        foreach (string prop in new[] { "Description", "ItemFilePath", "RegPath", "GroupPath", "SelectedPath" })
                        {
                            string path = item.GetType().GetProperty(prop)?.GetValue(item, null)?.ToString();
                            if (!path.IsNullOrWhiteSpace()) { StatusBar.Text = path; return; }
                        }
                        StatusBar.Text = item.Text.Trim();
                    };
                }
            }
        }

        private void DragDropToAnalysis()
        {
            var droper = new ElevatedFileDroper(this);
            droper.DragDrop += (sender, e) =>
            {
                ShellList.CurrentFileObjectPath = droper.DropFilePaths[0];
                JumpItem(1, 9);
            };
        }

        private void SwitchGeneralItem()
        {
            switch (SideBar.SelectedIndex)
            {
                case 10:
                    storeAppsList.LoadItems();
                    storeAppsList.Visible = true;
                    break;
                case 12:
                    shellNewList.LoadItems();
                    shellNewList.Visible = true;
                    break;
                case 13:
                    sendToList.LoadItems();
                    sendToList.Visible = true;
                    break;
                case 14:
                    openWithList.LoadItems();
                    openWithList.Visible = true;
                    break;
                case 16:
                    winXList.LoadItems();
                    winXList.Visible = true;
                    break;
                default:
                    if (SideBar.SelectedIndex < 10)
                    {
                        shellList.Scene = GeneralShellScenes[SideBar.SelectedIndex];
                        shellList.LoadItems();
                        shellList.Visible = true;
                    }
                    break;
            }
        }

        private void SwitchTypeItem()
        {
            shellList.Scene = (ShellList.Scenes)TypeShellScenes[SideBar.SelectedIndex];
            shellList.LoadItems();
            shellList.Visible = true;
        }

        private void SwitchCustomRuleItem()
        {
            if (SideBar.SelectedIndex >= 0 && SideBar.SelectedIndex < 4)
            {
                customPresetList.Category = (PresetCategory)SideBar.SelectedIndex;
                customPresetList.LoadItems();
                customPresetList.Visible = true;
            }
        }

        private void SwitchAboutItem()
        {
            switch (SideBar.SelectedIndex)
            {
                case 0:
                    appSettingBox.LoadItems();
                    appSettingBox.Visible = true;
                    break;
                case 1:
                    dictionariesBox.LoadText();
                    dictionariesBox.Visible = true;
                    break;
                case 2:
                    if (aboutMeBox.TextLength == 0) aboutMeBox.LoadIni(AppString.Other.AboutApp);
                    aboutMeBox.Visible = true;
                    break;
            }
        }

        private void ResizeSideBar()
        {
            SideBar.Width = 0;
            string[] strs = GeneralItems.Concat(TypeItems).Concat(CustomRuleItems).Concat(AboutItems).ToArray();
            Array.ForEach(strs, str => SideBar.Width = Math.Max(SideBar.Width, SideBar.GetItemWidth(str)));
        }

        private void AddContextMenus()
        {
            var dic = new Dictionary<MyToolBarButton, string[]>
            {
                { ToolBarButtons[0], GeneralItems },
                { ToolBarButtons[1], TypeItems },
                { ToolBarButtons[2], CustomRuleItems },
                { ToolBarButtons[3], SettingItems }
            };

            foreach (var item in dic)
            {
                ContextMenuStrip cms = new ContextMenuStrip();
                if (ThemeManager.IsDarkMode()) cms.Renderer = new ContextMenuManager.BluePointLilac.Methods.DarkModeRenderer();
                cms.ForeColor = Color.White;
                cms.BackColor = Color.FromArgb(43, 43, 43);
                cms.MouseEnter += (sender, e) =>
                {
                    if (item.Key != ToolBar.SelectedButton) item.Key.Opacity = 0.2F;
                };
                cms.Closed += (sender, e) =>
                {
                    if (item.Key != ToolBar.SelectedButton) item.Key.Opacity = 0;
                };
                item.Key.MouseDown += (sender, e) =>
                {
                    if (e.Button != MouseButtons.Right) return;
                    if (sender == ToolBar.SelectedButton) return;
                    cms.Show(item.Key, e.Location);
                };

                for (int i = 0; i < item.Value.Length; i++)
                {
                    if (item.Value[i] == null) cms.Items.Add(new ToolStripSeparator());
                    else
                    {
                        ToolStripMenuItem tsi = new ToolStripMenuItem(item.Value[i]);
                        cms.Items.Add(tsi);
                        int toolBarIndex = ToolBar.Controls.GetChildIndex(item.Key);
                        int index = i;
                        if (toolBarIndex != 3)
                        {
                            tsi.Click += (sender, e) => JumpItem(toolBarIndex, index);
                            cms.Opening += (sender, e) => tsi.Checked = lastItemIndex[toolBarIndex] == index;
                        }
                        else
                        {
                            tsi.Click += (sender, e) =>
                            {
                                switch (index)
                                {
                                    case 0:
                                        AppConfig.TopMost = this.TopMost = !tsi.Checked; break;
                                    case 2:
                                        AppConfig.ShowFilePath = !tsi.Checked; break;
                                    case 3:
                                        AppConfig.HideDisabledItems = !tsi.Checked; SwitchItem(); break;
                                    case 5:
                                        AppConfig.OpenMoreRegedit = !tsi.Checked; break;
                                    case 6:
                                        AppConfig.OpenMoreExplorer = !tsi.Checked; break;
                                }
                            };
                            cms.Opening += (sender, e) =>
                            {
                                switch (index)
                                {
                                    case 0:
                                        tsi.Checked = this.TopMost; break;
                                    case 2:
                                        tsi.Checked = AppConfig.ShowFilePath; break;
                                    case 3:
                                        tsi.Checked = AppConfig.HideDisabledItems; break;
                                    case 5:
                                        tsi.Checked = AppConfig.OpenMoreRegedit; break;
                                    case 6:
                                        tsi.Checked = AppConfig.OpenMoreExplorer; break;
                                }
                            };
                        }
                    }
                }
            }
        }

        private void CloseMainForm()
        {
            if (explorerRestarter.Visible && AppMessageBox.Show(explorerRestarter.Text,
                MessageBoxButtons.OKCancel) == DialogResult.OK) ExternalProgram.RestartExplorer();
            this.Opacity = 0;
            this.WindowState = FormWindowState.Normal;
            explorerRestarter.Visible = false;
            AppConfig.MainFormSize = this.Size;
        }
    }
}