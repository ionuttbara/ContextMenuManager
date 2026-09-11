using BluePointLilac.Controls;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class DefaultRemovalList : MyList
    {
        public int FilterIndex { get; set; } = 0;

        private string CurrentCategory
        {
            get
            {
                switch (FilterIndex)
                {
                    case 1: return "File";
                    case 2: return "Folder";
                    case 3: return "Desktop";
                    case 4: return "Drive";
                    case 5: return "Media";
                    case 6: return "System";
                    default: return "All";
                }
            }
        }

        public void LoadItems()
        {
            this.SuspendLayout();
            this.ClearItems();

            List<DefaultMenuRemovalItemDef> items = DefaultMenuRemovalManager.GetItemsByCategory(CurrentCategory);
            this.AddItem(CreateBulkActionItem());

            var groups = items.GroupBy(i => i.Category);
            foreach (var group in groups)
            {
                this.AddItem(new DefaultRemovalGroupItem(group.Key + " Context Menus"));
                foreach (var def in group)
                    this.AddItem(new DefaultRemovalItem(def, indentLevel: 1));
            }

            this.ResumeLayout(true);
        }

        private MyListItem CreateBulkActionItem()
        {
            MyListItem row = new MyListItem { Text = "Bulk actions", HasImage = false };
            row.AddCtr(CreateButton("Restore all", AppImage.AddNewItem, () => SetAll(false)));
            row.AddCtr(CreateButton("Remove all", AppImage.Delete, () => SetAll(true)));
            return row;
        }

        private static Button CreateButton(string text, Image image, Action action)
        {
            Button button = new Button
            {
                Text = text,
                Image = image,
                AutoSize = true,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(4, 1, 4, 1),
                UseVisualStyleBackColor = false
            };
            button.Click += (sender, e) => action();
            ThemeManager.ApplyTheme(button);
            return button;
        }

        private void SetAll(bool remove)
        {
            foreach (DefaultMenuRemovalItemDef item in DefaultMenuRemovalManager.GetItemsByCategory(CurrentCategory))
            {
                if (remove) item.Remove();
                else item.Restore();
            }
            this.BeginInvoke(new Action(LoadItems));
        }
    }
}
