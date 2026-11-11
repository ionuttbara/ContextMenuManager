using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class FolderColorizerManagerForm : Form
    {
        private readonly ListBox packageList = new ListBox();
        private readonly ListView iconList = new ListView();
        private readonly ImageList iconImages = new ImageList();
        private readonly Label packageInfo = new Label();
        private readonly Button btnRestoreMark;
        private readonly Button btnAddIcons;
        private readonly Button btnRenameIcon;
        private readonly Button btnReplaceIcon;
        private readonly Button btnDeleteIcon;
        private readonly Button btnRefreshMenu;
        private readonly Button btnClose;
        private SplitContainer split;

        private FolderColorizerPackage SelectedPackage => packageList.SelectedItem as FolderColorizerPackage;
        private FolderColorizerIconEntry SelectedIcon => iconList.SelectedItems.Count > 0 ? iconList.SelectedItems[0].Tag as FolderColorizerIconEntry : null;

        public FolderColorizerManagerForm()
        {
            Text = UiLanguage.Text("FolderColorizerCategories");
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(1020, 615).DpiZoom();
            MinimumSize = new Size(900, 520).DpiZoom();
            Font = SystemFonts.MessageBoxFont;

            btnRestoreMark = CreateButton(UiLanguage.Text("FolderColorizerRestoreMark"), Properties.Resources.Refresh);
            btnAddIcons = CreateButton(UiLanguage.Text("FolderColorizerAddIcons"), Properties.Resources.Add);
            btnRenameIcon = CreateButton(UiLanguage.Text("FolderColorizerRename"), null);
            btnReplaceIcon = CreateButton(UiLanguage.Text("FolderColorizerReplaceIcon"), Properties.Resources.AddExisting);
            btnDeleteIcon = CreateButton(UiLanguage.Text("FolderColorizerDeleteIcon"), Properties.Resources.Delete);
            btnRefreshMenu = CreateButton(UiLanguage.Text("FolderColorizerRefreshMenu"), Properties.Resources.Refresh);
            btnClose = CreateButton(UiLanguage.Text("FolderColorizerClose"), null);
            btnClose.DialogResult = DialogResult.OK;

            BuildLayout();
            WireEvents();
            ApplyTheme();

            Shown += (s, e) =>
            {
                ThemeManager.ApplyTitleBar(this);
                ApplyInitialSplitterPosition();
                try { FolderColorizerManager.MigrateLegacyPackages(); LoadPackages(); }
                catch (Exception ex) { ShowError(ex); }
            };
        }

        private static Button CreateButton(string text, Image image)
        {
            Image buttonImage = null;
            if (image != null)
            {
                try { buttonImage = new Bitmap(image, new Size(20, 20).DpiZoom()); }
                catch { buttonImage = image; }
            }

            return new Button
            {
                Text = text,
                Image = buttonImage,
                AutoSize = false,
                Height = 36.DpiZoom(),
                Padding = new Padding(10, 3, 10, 3).DpiZoom(),
                Margin = new Padding(5).DpiZoom(),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
        }

        private void BuildLayout()
        {
            packageList.Dock = DockStyle.Fill;
            packageList.IntegralHeight = false;
            packageList.BorderStyle = BorderStyle.FixedSingle;

            iconImages.ColorDepth = ColorDepth.Depth32Bit;
            iconImages.ImageSize = new Size(48, 48).DpiZoom();
            iconList.Dock = DockStyle.Fill;
            iconList.View = View.LargeIcon;
            iconList.LargeImageList = iconImages;
            iconList.MultiSelect = false;
            iconList.HideSelection = false;
            iconList.LabelWrap = true;
            iconList.ShowItemToolTips = true;
            iconList.BorderStyle = BorderStyle.FixedSingle;

            packageInfo.Dock = DockStyle.Top;
            packageInfo.AutoSize = false;
            packageInfo.Height = 44.DpiZoom();
            packageInfo.Padding = new Padding(10, 6, 10, 4).DpiZoom();
            packageInfo.TextAlign = ContentAlignment.MiddleLeft;

            Panel packageButtons = new Panel { Dock = DockStyle.Bottom, Height = 55.DpiZoom(),
                Padding = new Padding(4).DpiZoom() };
            btnRestoreMark.Dock = DockStyle.Fill;
            packageButtons.Controls.Add(btnRestoreMark);

            Panel leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 6, 0).DpiZoom() };
            leftPanel.Controls.Add(packageList);
            leftPanel.Controls.Add(packageButtons);

            FlowLayoutPanel iconButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52.DpiZoom(),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 3, 4, 3).DpiZoom()
            };
            btnAddIcons.Width = 120.DpiZoom();
            btnRenameIcon.Width = 104.DpiZoom();
            btnReplaceIcon.Width = 130.DpiZoom();
            btnDeleteIcon.Width = 126.DpiZoom();
            iconButtons.Controls.AddRange(new Control[] { btnAddIcons, btnRenameIcon, btnReplaceIcon, btnDeleteIcon });

            Panel rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(iconList);
            rightPanel.Controls.Add(packageInfo);
            rightPanel.Controls.Add(iconButtons);

            // Do not set SplitterDistance/PanelMinSize in the initializer. At this
            // point WinForms still gives SplitContainer its tiny design-time width, so
            // setting a large Panel2MinSize can throw before the form is shown.
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5.DpiZoom(),
                FixedPanel = FixedPanel.Panel1
            };
            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);

            Label note = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Text = UiLanguage.Text("FolderColorizerBuiltInNote"),
                Padding = new Padding(10, 0, 8, 0).DpiZoom(),
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnRefreshMenu.Width = 190.DpiZoom();
            btnClose.Width = 92.DpiZoom();

            FlowLayoutPanel bottomActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(2).DpiZoom(),
                Margin = Padding.Empty
            };
            bottomActions.Controls.Add(btnClose);
            bottomActions.Controls.Add(btnRefreshMenu);

            TableLayoutPanel bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58.DpiZoom(),
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4, 2, 4, 2).DpiZoom()
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottom.Controls.Add(note, 0, 0);
            bottom.Controls.Add(bottomActions, 1, 0);

            Controls.Add(split);
            Controls.Add(bottom);
            CancelButton = btnClose;
        }

        private void ApplyInitialSplitterPosition()
        {
            if (split == null || split.IsDisposed) return;

            // Apply the requested ~215 px package pane only after layout has given the
            // control its real width. Keep a useful right pane on smaller/high-DPI
            // layouts and never impose min-size values that can invalidate the splitter.
            int desired = 215.DpiZoom();
            int leftFloor = 150.DpiZoom();
            int rightFloor = 320.DpiZoom();
            int available = split.ClientSize.Width - split.SplitterWidth;
            if (available <= leftFloor) return;

            int maximum = Math.Max(leftFloor, available - rightFloor);
            int distance = Math.Max(leftFloor, Math.Min(desired, maximum));
            if (distance > 0 && distance < available)
                split.SplitterDistance = distance;
        }

        private void WireEvents()
        {
            packageList.SelectedIndexChanged += (s, e) => LoadSelectedPackage();
            iconList.SelectedIndexChanged += (s, e) => UpdateActionButtons();
            iconList.DoubleClick += (s, e) => RenameSelectedIcon();

            btnRestoreMark.Click += (s, e) => RestoreMarkDefaults();
            btnAddIcons.Click += (s, e) => AddIcons();
            btnRenameIcon.Click += (s, e) => RenameSelectedIcon();
            btnReplaceIcon.Click += (s, e) => ReplaceSelectedIcon();
            btnDeleteIcon.Click += (s, e) => DeleteSelectedIcon();
            btnRefreshMenu.Click += (s, e) =>
            {
                try
                {
                    if (!FolderColorizerManager.IsContextMenuInstalled())
                    {
                        MessageBox.Show("Enable Folder Colorizer under Folder Utilities first.",
                            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    FolderColorizerManager.InstallContextMenu();
                    MessageBox.Show(UiLanguage.Text("FolderColorizerRefreshed") + "\n" +
                        FolderColorizerManager.InstalledPackageCount() +
                        " categories registered beside Folder Management.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    FolderColorizerManager.LogInstallFailure("Refresh context menu", ex);
                    MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTheme(this);
            bool dark = ThemeManager.IsDarkMode();

            Color surface = dark ? Color.FromArgb(36, 36, 36) : Color.White;
            Color text = dark ? Color.White : Color.FromArgb(32, 32, 32);
            packageList.BackColor = surface;
            packageList.ForeColor = text;
            iconList.BackColor = surface;
            iconList.ForeColor = text;

            Color buttonBack = dark ? Color.FromArgb(45, 45, 45) : Color.FromArgb(248, 248, 248);
            Color buttonHover = dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(238, 238, 238);
            Color buttonPressed = dark ? Color.FromArgb(68, 68, 68) : Color.FromArgb(226, 226, 226);
            Color border = dark ? Color.FromArgb(82, 82, 82) : Color.FromArgb(190, 190, 190);

            foreach (Button button in new[]
            {
                btnRestoreMark, btnAddIcons, btnRenameIcon, btnReplaceIcon, btnDeleteIcon, btnRefreshMenu, btnClose
            })
            {
                button.BackColor = buttonBack;
                button.ForeColor = text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = border;
                button.FlatAppearance.MouseOverBackColor = buttonHover;
                button.FlatAppearance.MouseDownBackColor = buttonPressed;
            }
        }

        private void LoadPackages(string selectId = null)
        {
            packageList.BeginUpdate();
            try
            {
                packageList.Items.Clear();
                foreach (FolderColorizerPackage package in FolderColorizerManager.GetPackages()) packageList.Items.Add(package);
            }
            finally { packageList.EndUpdate(); }

            if (!string.IsNullOrWhiteSpace(selectId))
            {
                for (int i = 0; i < packageList.Items.Count; i++)
                {
                    FolderColorizerPackage p = packageList.Items[i] as FolderColorizerPackage;
                    if (p != null && string.Equals(p.Id, selectId, StringComparison.OrdinalIgnoreCase))
                    {
                        packageList.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (packageList.SelectedIndex < 0 && packageList.Items.Count > 0) packageList.SelectedIndex = 0;
            UpdateActionButtons();
        }

        private void LoadSelectedPackage()
        {
            iconList.BeginUpdate();
            iconList.Items.Clear();
            iconImages.Images.Clear();
            FolderColorizerPackage package = SelectedPackage;
            if (package != null)
            {
                int imageIndex = 0;
                foreach (FolderColorizerIconEntry icon in package.Icons)
                {
                    Image img = FolderColorizerManager.LoadPreviewImage(icon.FilePath);
                    if (img != null)
                    {
                        iconImages.Images.Add(img);
                        ListViewItem item = new ListViewItem(icon.Name, imageIndex++) { Tag = icon, ToolTipText = icon.Name };
                        iconList.Items.Add(item);
                    }
                    else
                    {
                        ListViewItem item = new ListViewItem(icon.Name) { Tag = icon, ToolTipText = icon.Name };
                        iconList.Items.Add(item);
                    }
                }
                packageInfo.Text = package.Name + "  •  " + package.Icons.Count + " " + UiLanguage.Text("FolderColorizerIconsCount") +
                    (package.Id == "builtin-status" ? "  •  " + UiLanguage.Text("FolderColorizerEditable") :
                        "  •  " + UiLanguage.Text("FolderColorizerReadOnly")) +
                    (FolderColorizerManager.IsPackageRegistered(package.Id) ? "  •  Menu registered" : "  •  Not registered");
            }
            else packageInfo.Text = string.Empty;
            iconList.EndUpdate();
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool editable = SelectedPackage?.Id == "builtin-status";
            btnRestoreMark.Enabled = editable;
            btnAddIcons.Enabled = editable;
            btnRenameIcon.Enabled = editable && SelectedIcon != null;
            btnReplaceIcon.Enabled = editable && SelectedIcon != null;
            btnDeleteIcon.Enabled = editable && SelectedIcon != null;
            btnRefreshMenu.Enabled = FolderColorizerManager.IsContextMenuInstalled();
        }

        private void RestoreMarkDefaults()
        {
            if (SelectedPackage?.Id != "builtin-status") return;
            if (MessageBox.Show(UiLanguage.Text("FolderColorizerConfirmRestore"), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { FolderColorizerManager.RestoreMarkDefaults(); LoadPackages("builtin-status"); }
            catch (Exception ex) { ShowError(ex); }
        }

        private void AddIcons()
        {
            FolderColorizerPackage package = SelectedPackage;
            if (package?.Id != "builtin-status") return;
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Icon files (*.ico)|*.ico",
                Multiselect = true,
                Title = UiLanguage.Text("FolderColorizerAddIcons")
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    FolderColorizerManager.AddIcons(package.Id, dlg.FileNames);
                    LoadPackages(package.Id);
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void RenameSelectedIcon()
        {
            FolderColorizerPackage package = SelectedPackage;
            FolderColorizerIconEntry icon = SelectedIcon;
            if (package?.Id != "builtin-status" || icon == null) return;
            using (InputDialog dlg = new InputDialog { Title = UiLanguage.Text("FolderColorizerRename"), Text = icon.Name })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Text)) return;
                try
                {
                    FolderColorizerManager.RenameIcon(package.Id, icon.Id, dlg.Text);
                    LoadPackages(package.Id);
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void ReplaceSelectedIcon()
        {
            FolderColorizerPackage package = SelectedPackage;
            FolderColorizerIconEntry icon = SelectedIcon;
            if (package?.Id != "builtin-status" || icon == null) return;
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Icon files (*.ico)|*.ico", Title = UiLanguage.Text("FolderColorizerReplaceIcon")
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { FolderColorizerManager.ReplaceIcon(package.Id, icon.Id, dlg.FileName); LoadPackages(package.Id); }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void DeleteSelectedIcon()
        {
            FolderColorizerPackage package = SelectedPackage;
            FolderColorizerIconEntry icon = SelectedIcon;
            if (package?.Id != "builtin-status" || icon == null) return;
            if (MessageBox.Show(string.Format(UiLanguage.Text("FolderColorizerConfirmDeleteIcon"), icon.Name), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                FolderColorizerManager.RemoveIcon(package.Id, icon.Id);
                LoadPackages(package.Id);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowError(Exception ex)
        {
            FolderColorizerManager.LogInstallFailure("Package editor", ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
