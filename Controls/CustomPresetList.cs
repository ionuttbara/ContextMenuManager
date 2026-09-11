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
    sealed class CustomPresetList : MyList
    {
        private PresetCategory currentCategory = PresetCategory.Desktop;

        public PresetCategory Category
        {
            get => currentCategory;
            set => currentCategory = value;
        }

        public void LoadItems()
        {
            this.SuspendLayout();
            this.ClearItems();
            List<CustomPreset> items = CustomPresetsManager.GetPresetsByCategory(currentCategory);

            this.AddItem(CreateBulkActionItem());

            var extGroups = items.GroupBy(p => p.ExtensionGroup);
            foreach (var extGroup in extGroups)
            {
                if (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder)
                    this.AddItem(new CustomPresetGroupItem(extGroup.Key, string.Format(UiLanguage.Text("ManageContextMenus"), extGroup.Key)));

                var subGroups = extGroup.GroupBy(p => p.SubMenuGroup);
                foreach (var subGroup in subGroups)
                {
                    if (!string.IsNullOrEmpty(subGroup.Key))
                    {
                        var first = subGroup.First();
                        string parentIcon = first.ParentMenu?.IconLocation ?? first.IconLocation;
                        string parentDesc = first.ParentMenu?.Description ?? string.Format(UiLanguage.Text("SubmenuFor"), subGroup.Key);

                        int headerIndent = (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder) ? 1 : 0;
                        this.AddItem(new CustomPresetSubMenuHeaderItem(subGroup.Key, parentIcon, headerIndent, parentDesc));

                        int childIndent = headerIndent + 1;
                        foreach (var preset in subGroup)
                            this.AddItem(new CustomPresetItem(preset, indentLevel: childIndent));
                    }
                    else
                    {
                        int directIndent = (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder) ? 1 : 0;
                        foreach (var preset in subGroup)
                            this.AddItem(new CustomPresetItem(preset, indentLevel: directIndent));
                    }
                }
            }
            this.ResumeLayout(true);
        }

        private MyListItem CreateBulkActionItem()
        {
            MyListItem row = new MyListItem { Text = UiLanguage.Text("BulkActions"), HasImage = false };
            row.AddCtr(CreateButton(UiLanguage.Text("RemoveAll"), AppImage.Delete, () => SetAll(false)));
            row.AddCtr(CreateButton(UiLanguage.Text("AddAll"), AppImage.AddNewItem, () => SetAll(true)));
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

        private void SetAll(bool install)
        {
            foreach (CustomPreset preset in CustomPresetsManager.GetPresetsByCategory(currentCategory))
            {
                try
                {
                    if (install) preset.Install();
                    else preset.Uninstall();
                }
                catch { }
            }
            this.BeginInvoke(new Action(LoadItems));
        }
    }
}
