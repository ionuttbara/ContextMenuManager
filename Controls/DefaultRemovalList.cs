using BluePointLilac.Controls;
using ContextMenuManager.Methods;
using System.Collections.Generic;
using System.Linq;

namespace ContextMenuManager.Controls
{
    sealed class DefaultRemovalList : MyList
    {
        public int FilterIndex { get; set; } = 0;

        public void LoadItems()
        {
            this.SuspendLayout();
            this.ClearItems();

            string categoryFilter = "All";
            switch (FilterIndex)
            {
                case 1: categoryFilter = "File"; break;
                case 2: categoryFilter = "Folder"; break;
                case 3: categoryFilter = "Desktop"; break;
                case 4: categoryFilter = "Drive"; break;
                case 5: categoryFilter = "Media"; break;
                case 6: categoryFilter = "System"; break;
            }

            List<DefaultMenuRemovalItemDef> items = DefaultMenuRemovalManager.GetItemsByCategory(categoryFilter);

            var groups = items.GroupBy(i => i.Category);
            foreach (var group in groups)
            {
                this.AddItem(new DefaultRemovalGroupItem(group.Key + " Context Menus"));
                foreach (var def in group)
                {
                    this.AddItem(new DefaultRemovalItem(def, indentLevel: 1));
                }
            }

            this.ResumeLayout(true);
        }
    }
}