using BluePointLilac.Controls;
using ContextMenuManager.Methods;
using System.Windows.Forms;

namespace ContextMenuManager.Controls.Interfaces
{
    interface ITsiDeleteItem
    {
        DeleteMeMenuItem TsiDeleteMe { get; set; }
        void DeleteMe();
    }

    interface ITsiRegDeleteItem : ITsiDeleteItem
    {
        string Text { get; }
        string RegPath { get; }
    }

    sealed class DeleteMeMenuItem : ToolStripMenuItem
    {
        public DeleteMeMenuItem(ITsiDeleteItem item) : base(AppString.Menu.Delete)
        {
            this.Click += (sender, e) =>
            {
                if (AppMessageBox.Show(AppString.Message.ConfirmDeletePermanently,
                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;

                MyListItem listItem = (MyListItem)item;
                MyList list = (MyList)listItem.Parent;
                int index = list.GetItemIndex(listItem);
                if (index == list.Controls.Count - 1) index--;
                try
                {
                    item.DeleteMe();
                }
                catch
                {
                    AppMessageBox.Show(AppString.Message.AuthorityProtection);
                    return;
                }
                list.Controls.Remove(listItem);
                if (list.Controls.Count > 0 && index >= 0) list.Controls[index].Focus();
                listItem.Dispose();
            };
        }
    }
}
