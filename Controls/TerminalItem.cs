using BluePointLilac.Controls;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls.Interfaces;
using ContextMenuManager.Methods;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class TerminalGroupHeaderItem : MyListItem
    {
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public TerminalGroupHeaderItem(string title, string description = null)
        {
            this.Text = title;
            this.Description = description ?? title;
            this.Image = AppImage.Type;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold);
            this.ForeColor = ThemeManager.IsDarkMode() ? Color.FromArgb(80, 160, 240) : Color.FromArgb(0, 102, 204);
        }
    }

    sealed class TerminalActionItem : MyListItem
    {
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public TerminalActionItem(string text, string description, Image icon, Action onClick)
        {
            this.Text = text;
            this.Description = description;
            this.Image = icon ?? IconHelper.GetIconImage(TerminalProfilesManager.DefaultTerminalIcon);
            this.Cursor = Cursors.Hand;
            this.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) onClick?.Invoke();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TranslateTransform(28, 0);
            base.OnPaint(e);
            e.Graphics.ResetTransform();
        }
    }

    sealed class TerminalBlockExtensionItem : MyListItem, IChkVisibleItem
    {
        public VisibleCheckBox ChkVisible { get; set; }
        public string Description => "Blocks default Windows 11 Terminal extension {9F156763-7844-4DC4-B2B1-901F640F5155} and cleans duplicate cmd/powershell/wsl verbs.";
        public string ItemFilePath => Description;

        public bool ItemVisible
        {
            get => TerminalProfilesManager.IsTerminalBlocked();
            set
            {
                TerminalProfilesManager.SetTerminalBlocked(value);
                if (this.ChkVisible != null) this.ChkVisible.Checked = value;
            }
        }

        public TerminalBlockExtensionItem()
        {
            this.Text = "Block Default Windows Terminal Shell Extension & Clean Duplicates";
            this.Image = Properties.Resources.Delete;
            this.ChkVisible = new VisibleCheckBox(this);
            this.ChkVisible.Checked = this.ItemVisible;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TranslateTransform(28, 0);
            base.OnPaint(e);
            e.Graphics.ResetTransform();
        }
    }

    sealed class TerminalProfileItem : MyListItem, IChkVisibleItem
    {
        public TerminalProfile Profile { get; set; }
        public VisibleCheckBox ChkVisible { get; set; }
        public string Description => Profile?.Description;
        public string ItemFilePath => Profile?.Description;

        public bool ItemVisible
        {
            get => Profile != null && TerminalProfilesManager.IsProfileInstalled(Profile);
            set
            {
                if (Profile == null) return;
                if (value == TerminalProfilesManager.IsProfileInstalled(Profile)) return;

                if (value)
                {
                    TerminalProfilesManager.InstallProfile(Profile);
                }
                else
                {
                    TerminalProfilesManager.UninstallProfile(Profile);
                }

                if (this.ChkVisible != null)
                {
                    this.ChkVisible.Checked = value;
                }
            }
        }

        public TerminalProfileItem(TerminalProfile profile)
        {
            this.Profile = profile;
            this.Text = profile.Name;
            this.Image = IconHelper.GetIconImage(string.IsNullOrEmpty(profile.IconLocation) ? TerminalProfilesManager.DefaultTerminalIcon : profile.IconLocation) ?? AppImage.Custom;

            this.ChkVisible = new VisibleCheckBox(this);
            this.ChkVisible.Checked = this.ItemVisible;

            InitContextMenu();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TranslateTransform(28, 0);
            base.OnPaint(e);
            e.Graphics.ResetTransform();
        }

        private void InitContextMenu()
        {
            ContextMenuStrip cms = new ContextMenuStrip();
            if (ThemeManager.IsDarkMode())
            {
                cms.Renderer = new ContextMenuManager.BluePointLilac.Methods.DarkModeRenderer();
                cms.ForeColor = Color.White;
                cms.BackColor = Color.FromArgb(43, 43, 43);
            }

            ToolStripMenuItem itemAdd = new ToolStripMenuItem("Add to 'Open terminal in place...'");
            itemAdd.Image = Properties.Resources.Add;
            itemAdd.Click += (s, e) => this.ItemVisible = true;

            ToolStripMenuItem itemRemove = new ToolStripMenuItem("Remove from 'Open terminal in place...'");
            itemRemove.Image = Properties.Resources.Delete;
            itemRemove.Click += (s, e) => this.ItemVisible = false;

            cms.Items.Add(itemAdd);
            cms.Items.Add(itemRemove);

            cms.Opening += (s, e) =>
            {
                bool active = this.ItemVisible;
                itemAdd.Enabled = !active;
                itemRemove.Enabled = active;
            };

            this.ContextMenuStrip = cms;
            foreach (Control child in this.Controls) child.ContextMenuStrip = cms;
            this.ControlAdded += (s, e) => e.Control.ContextMenuStrip = cms;
        }
    }
}