using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class WinXList : MyList
    {
        public static readonly string WinXPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\Windows\WinX");
        public static readonly string DefaultWinXPath = Environment.ExpandEnvironmentVariables(@"%SystemDrive%\Users\Default\AppData\Local\Microsoft\Windows\WinX");

        public void LoadItems()
        {
            if (WinOsVersion.Current < WinOsVersion.Win8 || !Directory.Exists(WinXPath)) return;
            AddNewItem();
            LoadWinXItems();
        }

        private void LoadWinXItems()
        {
            bool normalized = false;
            foreach (string dirPath in GetOrderedGroupPaths())
            {
                WinXGroupItem groupItem = new WinXGroupItem(dirPath);
                AddItem(groupItem);

                string[] lnkPaths;
                if (AppConfig.WinXSortable)
                {
                    lnkPaths = GetSortedPaths(dirPath, out bool changed);
                    if (changed) normalized = true;
                }
                else
                {
                    lnkPaths = GetDisplayPaths(dirPath);
                }

                foreach (string path in lnkPaths)
                {
                    WinXItem winXItem = new WinXItem(path, groupItem);
                    winXItem.BtnMoveDown.Visible = winXItem.BtnMoveUp.Visible = AppConfig.WinXSortable;
                    AddItem(winXItem);
                }
            }

            if (normalized)
            {
                ExplorerRestarter.Show();
                AppMessageBox.Show(AppString.Message.WinXSorted);
            }
        }

        private void AddNewItem()
        {
            NewItem newItem = new NewItem();
            AddItem(newItem);
            PictureButton btnCreateDir = new PictureButton(AppImage.NewFolder);
            ToolTipBox.SetToolTip(btnCreateDir, AppString.Tip.CreateGroup);
            newItem.AddCtr(btnCreateDir);
            btnCreateDir.MouseDown += (sender, e) => CreateNewGroup();
            newItem.AddNewItem += () =>
            {
                string[] groupNames = GetGroupNames();
                if (groupNames.Length == 0)
                {
                    CreateNewGroup();
                    groupNames = GetGroupNames();
                }

                using (NewLnkFileDialog dlg1 = new NewLnkFileDialog())
                {
                    if (dlg1.ShowDialog() != DialogResult.OK) return;
                    using (SelectDialog dlg2 = new SelectDialog())
                    {
                        dlg2.Title = AppString.Dialog.SelectGroup;
                        dlg2.Items = groupNames;
                        if (dlg2.ShowDialog() != DialogResult.OK) return;
                        string dirName = dlg2.Selected;
                        string dirPath = Path.Combine(WinXPath, dirName);
                        string itemText = dlg1.ItemText;
                        string targetPath = dlg1.ItemFilePath;
                        string arguments = dlg1.Arguments;
                        string workDir = Path.GetDirectoryName(targetPath);
                        string extension = Path.GetExtension(targetPath).ToLowerInvariant();
                        string fileName = Path.GetFileNameWithoutExtension(targetPath);
                        int nextIndex = GetNextShortcutIndex(dirPath);
                        string lnkName = $"{nextIndex:00} - {fileName}.lnk";
                        string lnkPath = Path.Combine(dirPath, lnkName);
                        lnkPath = ObjectPath.GetNewPathWithIndex(lnkPath, ObjectPath.PathType.File);

                        using (ShellLink shellLink = new ShellLink(lnkPath))
                        {
                            if (extension == ".lnk")
                            {
                                File.Copy(targetPath, lnkPath);
                                shellLink.Load();
                            }
                            else
                            {
                                shellLink.TargetPath = targetPath;
                                shellLink.Arguments = arguments;
                                shellLink.WorkingDirectory = workDir;
                            }
                            shellLink.Description = itemText;
                            shellLink.Save();
                        }

                        DesktopIni.SetLocalizedFileNames(lnkPath, itemText);
                        foreach (MyListItem ctr in Controls)
                        {
                            if (ctr is WinXGroupItem groupItem && groupItem.Text == dirName)
                            {
                                WinXItem item = new WinXItem(lnkPath, groupItem) { Visible = !groupItem.IsFold };
                                item.BtnMoveDown.Visible = item.BtnMoveUp.Visible = AppConfig.WinXSortable;
                                InsertItem(item, GetItemIndex(groupItem) + 1);
                                break;
                            }
                        }
                        WinXHasher.HashLnk(lnkPath);
                        ExplorerRestarter.Show();
                    }
                }
            };
        }

        private void CreateNewGroup()
        {
            string dirPath = GetNextGroupPath();
            Directory.CreateDirectory(dirPath);
            string iniPath = Path.Combine(dirPath, "desktop.ini");
            File.WriteAllText(iniPath, string.Empty, Encoding.Unicode);
            File.SetAttributes(dirPath, File.GetAttributes(dirPath) | FileAttributes.ReadOnly);
            File.SetAttributes(iniPath, File.GetAttributes(iniPath) | FileAttributes.Hidden | FileAttributes.System);
            InsertItem(new WinXGroupItem(dirPath), 1);
        }

        private static string GetNextGroupPath()
        {
            int next = 1;
            foreach (string path in GetOrderedGroupPaths())
            {
                int n = GetGroupNumber(Path.GetFileName(path));
                if (n >= next) next = n + 1;
            }
            return Path.Combine(WinXPath, "Group" + next);
        }

        public static string[] GetGroupNames()
        {
            if (!Directory.Exists(WinXPath)) return new string[0];
            return GetOrderedGroupPaths().Select(Path.GetFileName).ToArray();
        }

        private static string[] GetOrderedGroupPaths()
        {
            if (!Directory.Exists(WinXPath)) return new string[0];
            return Directory.GetDirectories(WinXPath)
                .OrderByDescending(path => GetGroupNumber(Path.GetFileName(path)))
                .ThenByDescending(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static int GetGroupNumber(string name)
        {
            if (name != null && name.StartsWith("Group", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring(5), out int number)) return number;
            return int.MinValue;
        }

        private static int GetShortcutNumber(string path)
        {
            string name = Path.GetFileName(path);
            int separator = name.IndexOf(" - ", StringComparison.Ordinal);
            if (separator > 0 && int.TryParse(name.Substring(0, separator), out int number)) return number;
            return int.MinValue;
        }

        private static string StripShortcutPrefix(string path)
        {
            string name = Path.GetFileName(path);
            int separator = name.IndexOf(" - ", StringComparison.Ordinal);
            return separator > 0 ? name.Substring(separator + 3) : name;
        }

        private static string[] GetDisplayPaths(string groupPath)
        {
            return Directory.GetFiles(groupPath, "*.lnk")
                .OrderByDescending(GetShortcutNumber)
                .ThenBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        internal static int GetNextShortcutIndex(string groupPath)
        {
            int max = 0;
            foreach (string path in Directory.GetFiles(groupPath, "*.lnk"))
            {
                int n = GetShortcutNumber(path);
                if (n > max) max = n;
            }
            return max + 1;
        }

        private static string[] GetSortedPaths(string groupPath, out bool normalized)
        {
            normalized = false;
            string[] paths = Directory.GetFiles(groupPath, "*.lnk");
            if (paths.Length == 0) return paths;

            List<string> logicalOrder = paths
                .OrderBy(path => GetShortcutNumber(path) == int.MinValue ? int.MaxValue : GetShortcutNumber(path))
                .ThenBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            bool needsNormalize = false;
            for (int i = 0; i < logicalOrder.Count; i++)
            {
                if (GetShortcutNumber(logicalOrder[i]) != i + 1)
                {
                    needsNormalize = true;
                    break;
                }
            }

            if (!needsNormalize)
            {
                return logicalOrder.OrderByDescending(GetShortcutNumber).ToArray();
            }

            var staged = new List<Tuple<string, string, string>>();
            foreach (string srcPath in logicalOrder)
            {
                string displayText;
                using (ShellLink srcLnk = new ShellLink(srcPath))
                {
                    displayText = srcLnk.Description?.Trim();
                }
                if (string.IsNullOrEmpty(displayText)) displayText = DesktopIni.GetLocalizedFileNames(srcPath);
                if (string.IsNullOrEmpty(displayText)) displayText = Path.GetFileNameWithoutExtension(StripShortcutPrefix(srcPath));

                string tempPath = Path.Combine(groupPath, ".cmm-" + Guid.NewGuid().ToString("N") + ".tmp");
                string rawName = StripShortcutPrefix(srcPath);
                DesktopIni.DeleteLocalizedFileNames(srcPath);
                File.Move(srcPath, tempPath);
                staged.Add(Tuple.Create(tempPath, rawName, displayText));
            }

            List<string> normalizedPaths = new List<string>();
            for (int i = 0; i < staged.Count; i++)
            {
                string dstPath = Path.Combine(groupPath, $"{i + 1:00} - {staged[i].Item2}");
                File.Move(staged[i].Item1, dstPath);
                DesktopIni.SetLocalizedFileNames(dstPath, staged[i].Item3);
                using (ShellLink dstLnk = new ShellLink(dstPath))
                {
                    dstLnk.Description = staged[i].Item3;
                    dstLnk.Save();
                }
                normalizedPaths.Add(dstPath);
            }

            normalized = true;
            return normalizedPaths.OrderByDescending(GetShortcutNumber).ToArray();
        }
    }
}
