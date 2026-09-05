using BluePointLilac.Controls;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls.Interfaces;
using ContextMenuManager.Methods;
using System.Drawing;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    // Nivel 0: Antet categorie/extensie
    sealed class CustomPresetGroupItem : MyListItem
    {
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public CustomPresetGroupItem(string groupTitle, string description = null)
        {
            this.Text = groupTitle;
            this.Description = description ?? $"Context menu items for {groupTitle}";
            this.Image = AppImage.Type;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold);
            this.ForeColor = ThemeManager.IsDarkMode() ? Color.FromArgb(80, 160, 240) : Color.FromArgb(0, 102, 204);
        }
    }

    // Nivel 1: Antet meniu cascadat (Submeniu parinte)
    sealed class CustomPresetSubMenuHeaderItem : MyListItem
    {
        public int IndentLevel { get; set; } = 1;
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public CustomPresetSubMenuHeaderItem(string subMenuTitle, string iconLocation, int indentLevel = 1, string description = null)
        {
            this.IndentLevel = indentLevel;
            this.Text = subMenuTitle;
            this.Description = description ?? $"Submenu container for {subMenuTitle}";
            this.Image = IconHelper.GetIconImage(iconLocation) ?? AppImage.Custom;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold);
            this.ForeColor = ThemeManager.IsDarkMode() ? Color.FromArgb(200, 200, 200) : Color.FromArgb(50, 50, 50);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int shift = IndentLevel * 28;
            if (shift > 0)
            {
                e.Graphics.TranslateTransform(shift, 0);
                base.OnPaint(e);
                e.Graphics.ResetTransform();
            }
            else
            {
                base.OnPaint(e);
            }
        }
    }

    // Nivel 1 sau 2: Element configurabil (Toggle Switch + Context Menu)
    sealed class CustomPresetItem : MyListItem, IChkVisibleItem
    {
        public int IndentLevel { get; set; } = 0;

        public CustomPreset Preset { get; set; }

        public VisibleCheckBox ChkVisible { get; set; }

        public string Description => Preset?.Description;

        public string RegPath => Preset != null ? $@"HKEY_CLASSES_ROOT\{Preset.RootKeyPath}" : null;

        public string ItemFilePath => !string.IsNullOrEmpty(Preset?.Description) ? Preset.Description : RegPath;

        public bool ItemVisible
        {
            get => Preset != null && Preset.IsActive();
            set
            {
                if (Preset == null) return;
                if (value == Preset.IsActive()) return;

                if (value)
                {
                    Preset.Install();
                }
                else
                {
                    Preset.Uninstall();
                }
                if (this.ChkVisible != null)
                {
                    this.ChkVisible.Checked = value;
                }
            }
        }

        public CustomPresetItem(CustomPreset preset, int indentLevel = 0)
        {
            this.Preset = preset;
            this.IndentLevel = indentLevel;
            this.Text = preset.Name;

            this.Image = IconHelper.GetIconImage(preset.IconLocation) ?? AppImage.Custom;

            this.ChkVisible = new VisibleCheckBox(this);
            this.ChkVisible.Checked = this.ItemVisible;

            InitContextMenu();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int shift = IndentLevel * 28;
            if (shift > 0)
            {
                e.Graphics.TranslateTransform(shift, 0);
                base.OnPaint(e);
                e.Graphics.ResetTransform();
            }
            else
            {
                base.OnPaint(e);
            }
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

            ToolStripMenuItem itemAdd = new ToolStripMenuItem("Add to registry");
            itemAdd.Image = Properties.Resources.Add;
            itemAdd.Click += (s, e) =>
            {
                this.ItemVisible = true;
            };

            ToolStripMenuItem itemRemove = new ToolStripMenuItem("Remove from registry");
            itemRemove.Image = Properties.Resources.Delete;
            itemRemove.Click += (s, e) =>
            {
                this.ItemVisible = false;
            };

            cms.Items.Add(itemAdd);
            cms.Items.Add(itemRemove);

            cms.Opening += (s, e) =>
            {
                bool active = this.ItemVisible;
                itemAdd.Text = active ? "Re-add to registry" : "Add to registry";
                itemRemove.Enabled = active;
            };

            this.ContextMenuStrip = cms;
            foreach (Control child in this.Controls)
            {
                child.ContextMenuStrip = cms;
            }
            this.ControlAdded += (s, e) => e.Control.ContextMenuStrip = cms;
        }
    }
}