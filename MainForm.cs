using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Drawing;
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
            foreach (Control c in MainControls) c.Visible = false;

            ToolBar.SelectedButtonChanged += (sender, e) => SwitchTab();
            SideBar.HoverIndexChanged += (sender, e) => ShowItemInfo();
            SideBar.SelectIndexChanged += (sender, e) =>
            {
                if (!isSwitching) SwitchItem();
            };

            this.FormClosing += (sender, e) => CloseMainForm();
            HoveredToShowItemPath();
            DragDropToAnalysis();
            AddContextMenus();
            ResizeSideBar();
            JumpItem(0, 0);
        }

        readonly MyToolBarButton[] ToolBarButtons =
        {
            new MyToolBarButton(AppImage.ToolbarHome, UiLanguage.Text("CurrentItems")),
            new MyToolBarButton(AppImage.ToolbarType, AppString.ToolBar.Type),
            new MyToolBarButton(AppImage.ToolbarCustom, UiLanguage.Text("AddCustomItems")),
            new MyToolBarButton(AppImage.ToolbarRemove, UiLanguage.Text("RemoveDefaultMenus")),
            new MyToolBarButton(AppImage.ToolbarAbout, AppString.ToolBar.About)
        };

        private Control[] MainControls => new Control[]
        {
            shellList, shellNewList, sendToList, openWithList, winXList,
            detailedEditList, guidBlockedList, iEList,
            appSettingBox, aboutMeBox, storeAppsList,
            customPresetList, defaultRemovalList, terminalList
        };

        readonly ShellList shellList = new ShellList();
        readonly StoreAppsList storeAppsList = new StoreAppsList();
        readonly TerminalList terminalList = new TerminalList();
        readonly ShellNewList shellNewList = new ShellNewList();
        readonly SendToList sendToList = new SendToList();
        readonly OpenWithList openWithList = new OpenWithList();
        readonly WinXList winXList = new WinXList();
        readonly DetailedEditList detailedEditList = new DetailedEditList();
        readonly GuidBlockedList guidBlockedList = new GuidBlockedList();
        readonly IEList iEList = new IEList();
        readonly AppSettingBox appSettingBox = new AppSettingBox();
        readonly ReadOnlyRichTextBox aboutMeBox = new ReadOnlyRichTextBox();
        readonly ExplorerRestarter explorerRestarter = new ExplorerRestarter();
        readonly CustomPresetList customPresetList = new CustomPresetList();
        readonly DefaultRemovalList defaultRemovalList = new DefaultRemovalList();

        private Control currentActiveControl = null;
        private bool isSwitching = false;

        // Tab 0: Current items
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
            UiLanguage.Text("MicrosoftStoreApps"),
            UiLanguage.Text("Terminal"),
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
            UiLanguage.Text("StoreAppsInfo"),
            UiLanguage.Text("TerminalInfo"),
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

        // Tab 3: Remove Default Menus
        static readonly string[] DefaultRemovalSideBarItems =
        {
            UiLanguage.Text("AllItems"),
            AppString.SideBar.File,
            AppString.SideBar.Folder,
            AppString.SideBar.Desktop,
            AppString.SideBar.Drive,
            UiLanguage.Text("Media"),
            UiLanguage.Text("System")
        };

        static readonly string[] DefaultRemovalSideBarItemInfos =
        {
            UiLanguage.Text("InfoAllRemoval"),
            UiLanguage.Text("InfoFileRemoval"),
            UiLanguage.Text("InfoFolderRemoval"),
            UiLanguage.Text("InfoDesktopRemoval"),
            UiLanguage.Text("InfoDriveRemoval"),
            UiLanguage.Text("InfoMediaRemoval"),
            UiLanguage.Text("InfoSystemRemoval")
        };

        // Tab 4: About
        static readonly string[] AboutItems =
        {
            AppString.SideBar.AppSetting,
            AppString.SideBar.AboutApp
        };

        static readonly string[] AboutItemInfos =
        {
            AppString.SideBar.AppSetting,
            AppString.SideBar.AboutApp
        };

        readonly int[] lastItemIndex = new int[5];

        public void JumpItem(int toolBarIndex, int sideBarIndex)
        {
            if (isSwitching) return;
            isSwitching = true;
            try
            {
                lastItemIndex[toolBarIndex] = sideBarIndex;
                if (ToolBar.SelectedIndex != toolBarIndex)
                {
                    ToolBar.SelectedIndex = toolBarIndex;
                    UpdateSideBarNames();
                }
                SideBar.SelectedIndex = sideBarIndex;
                ExecuteSwitchItem();
            }
            finally
            {
                isSwitching = false;
            }
        }

        private void SwitchTab()
        {
            if (isSwitching) return;
            isSwitching = true;
            try
            {
                UpdateSideBarNames();
                SideBar.SelectedIndex = lastItemIndex[ToolBar.SelectedIndex];
                ExecuteSwitchItem();
            }
            finally
            {
                isSwitching = false;
            }
        }

        private void UpdateSideBarNames()
        {
            switch (ToolBar.SelectedIndex)
            {
                case 0: SideBar.ItemNames = GeneralItems; break;
                case 1: SideBar.ItemNames = TypeItems; break;
                case 2: SideBar.ItemNames = CustomRuleItems; break;
                case 3: SideBar.ItemNames = DefaultRemovalSideBarItems; break;
                case 4: SideBar.ItemNames = AboutItems; break;
            }
        }

        private void ActivateControl(Control ctr, Action loadAction = null)
        {
            if (ctr == null) return;
            MainBody.SuspendLayout();

            foreach (Control c in MainControls)
            {
                if (c != ctr && c.Visible)
                {
                    c.Visible = false;
                }
            }

            loadAction?.Invoke();

            ctr.Visible = true;
            ctr.BringToFront();
            currentActiveControl = ctr;

            MainBody.ResumeLayout(true);
        }

        private void SwitchItem()
        {
            if (SideBar.SelectedIndex == -1) return;
            ExecuteSwitchItem();
            lastItemIndex[ToolBar.SelectedIndex] = SideBar.SelectedIndex;
        }

        private void ExecuteSwitchItem()
        {
            switch (ToolBar.SelectedIndex)
            {
                case 0: SwitchGeneralItem(); break;
                case 1: SwitchTypeItem(); break;
                case 2: SwitchCustomRuleItem(); break;
                case 3: SwitchDefaultRemovalItem(); break;
                case 4: SwitchAboutItem(); break;
            }
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
                    case 3:
                        if (i < DefaultRemovalSideBarItemInfos.Length) { StatusBar.Text = DefaultRemovalSideBarItemInfos[i]; return; }
                        break;
                    case 4:
                        if (i < AboutItemInfos.Length) { StatusBar.Text = AboutItemInfos[i]; return; }
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
                    ActivateControl(storeAppsList, () => { if (storeAppsList.Controls.Count == 0) storeAppsList.LoadItems(); });
                    break;
                case 11:
                    ActivateControl(terminalList, () => terminalList.LoadItems());
                    break;
                case 13:
                    ActivateControl(shellNewList, () => { if (shellNewList.Controls.Count == 0) shellNewList.LoadItems(); });
                    break;
                case 14:
                    ActivateControl(sendToList, () => { if (sendToList.Controls.Count == 0) sendToList.LoadItems(); });
                    break;
                case 15:
                    ActivateControl(openWithList, () => { if (openWithList.Controls.Count == 0) openWithList.LoadItems(); });
                    break;
                case 17:
                    ActivateControl(winXList, () => winXList.LoadItems());
                    break;
                default:
                    if (SideBar.SelectedIndex < 10)
                    {
                        var targetScene = GeneralShellScenes[SideBar.SelectedIndex];
                        ActivateControl(shellList, () =>
                        {
                            shellList.Scene = targetScene;
                            shellList.LoadItems();
                        });
                    }
                    break;
            }
        }

        private void SwitchTypeItem()
        {
            var targetScene = (ShellList.Scenes)TypeShellScenes[SideBar.SelectedIndex];
            ActivateControl(shellList, () =>
            {
                shellList.Scene = targetScene;
                shellList.LoadItems();
            });
        }

        private void SwitchCustomRuleItem()
        {
            if (SideBar.SelectedIndex >= 0 && SideBar.SelectedIndex < 4)
            {
                PresetCategory cat = (PresetCategory)SideBar.SelectedIndex;
                ActivateControl(customPresetList, () =>
                {
                    customPresetList.Category = cat;
                    customPresetList.LoadItems();
                });
            }
        }

        private void SwitchDefaultRemovalItem()
        {
            int filter = SideBar.SelectedIndex;
            ActivateControl(defaultRemovalList, () =>
            {
                defaultRemovalList.FilterIndex = filter;
                defaultRemovalList.LoadItems();
            });
        }

        private void SwitchAboutItem()
        {
            switch (SideBar.SelectedIndex)
            {
                case 0:
                    ActivateControl(appSettingBox, () => { if (appSettingBox.Controls.Count == 0) appSettingBox.LoadItems(); });
                    break;
                case 1:
                    ActivateControl(aboutMeBox, () => LoadAboutInfo());
                    break;
            }
        }

        private void LoadAboutInfo()
        {
            if (aboutMeBox.TextLength > 0) return;

            aboutMeBox.SuspendLayout();
            aboutMeBox.Clear();
            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 13, FontStyle.Bold);
            aboutMeBox.SelectionColor = ThemeManager.IsDarkMode() ? Color.FromArgb(80, 160, 240) : Color.FromArgb(0, 102, 204);
            aboutMeBox.AppendText("ContextMenuManager (Enhanced Community Fork)\n\n");

            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 10, FontStyle.Bold);
            aboutMeBox.SelectionColor = ThemeManager.IsDarkMode() ? Color.White : Color.Black;
            aboutMeBox.AppendText(UiLanguage.Text("AboutProject") + ":\n");
            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 9.5F, FontStyle.Regular);
            aboutMeBox.AppendText("This software is an enhanced fork of the original open-source ContextMenuManager application by BluePointLilac.\n");
            aboutMeBox.AppendText("Maintained, redesigned and modernized by Ionut Bara.\n\n");

            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 10, FontStyle.Bold);
            aboutMeBox.AppendText(UiLanguage.Text("Repository") + ":\n");
            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 9.5F, FontStyle.Regular);
            aboutMeBox.AppendText("https://github.com/ionuttbara/ContextMenuManager\n\n");

            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 10, FontStyle.Bold);
            aboutMeBox.AppendText(UiLanguage.Text("TechnicalDetails") + ":\n");
            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 9.5F, FontStyle.Regular);
            aboutMeBox.AppendText("• Version: 3.3.4.2\n");
            aboutMeBox.AppendText("• Platform: Microsoft .NET Framework 4.8\n");
            aboutMeBox.AppendText("• Architecture: AnyCPU (Native 64-bit / ARM64 / 32-bit execution)\n");
            aboutMeBox.AppendText("• DPI Awareness: Per-Monitor V2 High-DPI auto-rescaling enabled\n\n");

            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 10, FontStyle.Bold);
            aboutMeBox.AppendText(UiLanguage.Text("KeyFeatures") + ":\n");
            aboutMeBox.SelectionFont = new Font(aboutMeBox.Font.FontFamily, 9.5F, FontStyle.Regular);
            aboutMeBox.AppendText("1. Windows Terminal Integration: Dynamic profile discovery from settings.json across all editions (Stable, Preview, Canary, Dev, Unpackaged) with JSON Profile Backup & Restore and native CMD / PowerShell 5 fallback.\n");
            aboutMeBox.AppendText("2. Add Custom Items: Deep system presets including Dedicated vs. Integrated GPU preferences, real-time CPU priority, Windows Firewall access rules, NT SERVICE\\TrustedInstaller privileges, Take Ownership, and recursive file tools.\n");
            aboutMeBox.AppendText("3. Remove Default Menus: Registry toggle hub to eliminate unwanted built-in context menus (Previous Versions, Print, Send To, Troubleshoot Compatibility, BitLocker Drive, Defender EPP, ISO Burn, etc.) with instant non-destructive restoration.\n");
            aboutMeBox.AppendText("4. Win+X Stability Protection: Automated duplicate detection and cleanup preventing Windows Update and system restarts from restoring duplicate default shortcuts.\n");
            aboutMeBox.AppendText("5. Instant Performance: GDI icon memory caching and multi-pass layout suspension delivering instantaneous tab and submenu switching.");

            aboutMeBox.ResumeLayout();
        }

        private void ResizeSideBar()
        {
            SideBar.Width = 0;
            string[] strs = GeneralItems.Concat(TypeItems).Concat(CustomRuleItems).Concat(DefaultRemovalSideBarItems).Concat(AboutItems).ToArray();
            Array.ForEach(strs, str => SideBar.Width = Math.Max(SideBar.Width, SideBar.GetItemWidth(str)));
        }

        private void AddContextMenus()
        {
            var dic = new Dictionary<MyToolBarButton, string[]>
            {
                { ToolBarButtons[0], GeneralItems },
                { ToolBarButtons[1], TypeItems },
                { ToolBarButtons[2], CustomRuleItems },
                { ToolBarButtons[3], DefaultRemovalSideBarItems },
                { ToolBarButtons[4], AboutItems }
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
                        if (toolBarIndex != 4)
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