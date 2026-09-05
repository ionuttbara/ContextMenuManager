using BluePointLilac.Controls;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Controls.Interfaces;
using ContextMenuManager.Methods;
using System.Drawing;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class DefaultRemovalGroupItem : MyListItem
    {
        public string Description { get; set; }
        public string ItemFilePath => Description;

        public DefaultRemovalGroupItem(string groupTitle, string description = null)
        {
            this.Text = groupTitle;
            this.Description = description ?? $"Remove default context menu items for {groupTitle}";
            this.Image = AppImage.Type;
            this.Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Bold);
            this.ForeColor = ThemeManager.IsDarkMode() ? Color.FromArgb(80, 160, 240) : Color.FromArgb(0, 102, 204);
        }
    }

    sealed class DefaultRemovalItem : MyListItem, IChkVisibleItem
    {
        public int IndentLevel { get; set; } = 1;

        public DefaultMenuRemovalItemDef ItemDef { get; set; }

        public VisibleCheckBox ChkVisible { get; set; }

        public string Description => ItemDef?.Description;

        public string ItemFilePath => ItemDef?.Description;

        // Daca nu exista in registri -> e eliminat -> este bifat (true)
        // Daca exista in registri -> este nebifat (false)
        public bool ItemVisible
        {
            get => ItemDef != null && ItemDef.IsRemoved();
            set
            {
                if (ItemDef == null) return;
                if (value == ItemDef.IsRemoved()) return;

                if (value)
                {
                    ItemDef.Remove();
                }
                else
                {
                    ItemDef.Restore();
                }

                if (this.ChkVisible != null)
                {
                    this.ChkVisible.Checked = value;
                }
            }
        }

        public DefaultRemovalItem(DefaultMenuRemovalItemDef def, int indentLevel = 1)
        {
            this.ItemDef = def;
            this.IndentLevel = indentLevel;
            this.Text = def.Name;

            this.Image = IconHelper.GetIconImage(def.IconLocation) ?? Properties.Resources.Delete;

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

            ToolStripMenuItem itemRemove = new ToolStripMenuItem("Remove from registry");
            itemRemove.Image = Properties.Resources.Delete;
            itemRemove.Click += (s, e) =>
            {
                this.ItemVisible = true;
            };

            ToolStripMenuItem itemRestore = new ToolStripMenuItem("Restore to registry");
            itemRestore.Image = Properties.Resources.Add;
            itemRestore.Click += (s, e) =>
            {
                this.ItemVisible = false;
            };

            cms.Items.Add(itemRemove);
            cms.Items.Add(itemRestore);

            cms.Opening += (s, e) =>
            {
                bool isRemoved = this.ItemVisible;
                itemRemove.Enabled = !isRemoved;
                itemRestore.Enabled = isRemoved;
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