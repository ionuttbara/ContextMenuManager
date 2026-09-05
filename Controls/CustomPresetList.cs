using BluePointLilac.Controls;
using ContextMenuManager.Methods;
using System.Collections.Generic;
using System.Linq;

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

            var extGroups = items.GroupBy(p => p.ExtensionGroup);

            foreach (var extGroup in extGroups)
            {
                if (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder)
                {
                    this.AddItem(new CustomPresetGroupItem(extGroup.Key, $"Manage context menu items for {extGroup.Key}"));
                }

                var subGroups = extGroup.GroupBy(p => p.SubMenuGroup);

                foreach (var subGroup in subGroups)
                {
                    if (!string.IsNullOrEmpty(subGroup.Key))
                    {
                        var first = subGroup.First();
                        string parentIcon = first.ParentMenu?.IconLocation ?? first.IconLocation;
                        string parentDesc = first.ParentMenu?.Description ?? $"Submenu for {subGroup.Key}";

                        int headerIndent = (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder) ? 1 : 0;
                        this.AddItem(new CustomPresetSubMenuHeaderItem(subGroup.Key, parentIcon, headerIndent, parentDesc));

                        int childIndent = headerIndent + 1;
                        foreach (var preset in subGroup)
                        {
                            this.AddItem(new CustomPresetItem(preset, indentLevel: childIndent));
                        }
                    }
                    else
                    {
                        int directIndent = (currentCategory == PresetCategory.File || currentCategory == PresetCategory.Folder) ? 1 : 0;
                        foreach (var preset in subGroup)
                        {
                            this.AddItem(new CustomPresetItem(preset, indentLevel: directIndent));
                        }
                    }
                }
            }
            this.ResumeLayout(true);
        }
    }
}