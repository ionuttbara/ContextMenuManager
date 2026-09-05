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
            this.ClearItems();
            List<CustomPreset> items = CustomPresetsManager.GetPresetsByCategory(currentCategory);

            var extGroups = items.GroupBy(p => p.ExtensionGroup);

            foreach (var extGroup in extGroups)
            {
                // Nivel 0: Antetul de grup (doar dacă suntem în File sau dacă categoria are extensii)
                if (currentCategory == PresetCategory.File)
                {
                    this.AddItem(new CustomPresetGroupItem(extGroup.Key, $"Manage context menu items for {extGroup.Key}"));
                }

                // Subgrupurile cascadate (ex: Config Application Run)
                var subGroups = extGroup.GroupBy(p => p.SubMenuGroup);

                foreach (var subGroup in subGroups)
                {
                    if (!string.IsNullOrEmpty(subGroup.Key))
                    {
                        var first = subGroup.First();
                        string parentIcon = first.ParentMenu?.IconLocation ?? first.IconLocation;
                        string parentDesc = first.ParentMenu?.Description ?? $"Submenu for {subGroup.Key}";

                        // Nivel 1: Submeniul părinte indentat cu 6 spații
                        int headerIndent = (currentCategory == PresetCategory.File) ? 6 : 0;
                        this.AddItem(new CustomPresetSubMenuHeaderItem(subGroup.Key, parentIcon, headerIndent, parentDesc));

                        // Nivel 2: Opțiunile din interiorul submeniului indentate cu 13 spații
                        int childIndent = (currentCategory == PresetCategory.File) ? 13 : 6;
                        foreach (var preset in subGroup)
                        {
                            this.AddItem(new CustomPresetItem(preset, indentSpaces: childIndent));
                        }
                    }
                    else
                    {
                        // Element de sine stătător (fără submeniu părinte)
                        int directIndent = (currentCategory == PresetCategory.File) ? 6 : 0;
                        foreach (var preset in subGroup)
                        {
                            this.AddItem(new CustomPresetItem(preset, indentSpaces: directIndent));
                        }
                    }
                }
            }
        }
    }
}