using BluePointLilac.Controls;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls.Interfaces;
using ContextMenuManager.Methods;
using System.Drawing;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    // Antet nivel 0: Grupul de extensii
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

    // Antet nivel 1: Meniul cascadat parinte
    sealed class CustomPresetSubMenuHeaderItem : MyListItem
    {
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public CustomPresetSubMenuHeaderItem(string subMenuTitle, string iconLocation, int indentSpaces = 6, string description = null)
        {
            string spaces = new string(' ', indentSpaces);
            this.Text = $"{spaces}{subMenuTitle}";
            this.Description = description ?? $"Submenu container for {subMenuTitle}";
            this.Image = IconHelper.GetIconImage(iconLocation) ?? AppImage.Custom;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold);
            this.ForeColor = ThemeManager.IsDarkMode() ? Color.FromArgb(200, 200, 200) : Color.FromArgb(50, 50, 50);
        }
    }

    // Element configurabil nivel 1 sau 2
    sealed class CustomPresetItem : MyListItem, IChkVisibleItem
    {
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

        public CustomPresetItem(CustomPreset preset, int indentSpaces = 0)
        {
            this.Preset = preset;
            string spaces = indentSpaces > 0 ? new string(' ', indentSpaces) : "";
            this.Text = $"{spaces}{preset.Name}";

            this.Image = IconHelper.GetIconImage(preset.IconLocation) ?? AppImage.Custom;

            this.ChkVisible = new VisibleCheckBox(this);
            this.ChkVisible.Checked = this.ItemVisible;

            InitContextMenu();
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