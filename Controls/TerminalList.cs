using BluePointLilac.Controls;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    sealed class TerminalList : MyList
    {
        public void LoadItems()
        {
            this.SuspendLayout();
            this.ClearItems();

            bool hasTerminal = TerminalProfilesManager.DetectTerminal(out string edition, out _, out _);
            List<TerminalProfile> profiles = TerminalProfilesManager.GetAvailableProfiles();

            // Backup & Restore Actions
            this.AddItem(new TerminalGroupHeaderItem("Profile Management (Backup & Restore)", "Export or import Windows Terminal profiles as JSON"));

            this.AddItem(new TerminalActionItem("Backup Terminal Profiles to JSON...", "Save a copy of your terminal profiles settings to a JSON file", Properties.Resources.DownLoad, () =>
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                    sfd.FileName = "WindowsTerminal_Profiles_Backup.json";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (TerminalProfilesManager.BackupProfiles(sfd.FileName))
                        {
                            AppMessageBox.Show("Terminal profiles backup successfully saved!", MessageBoxButtons.OK);
                        }
                        else
                        {
                            AppMessageBox.Show("Could not backup terminal profiles.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }));

            this.AddItem(new TerminalActionItem("Restore Terminal Profiles from JSON...", "Restore Windows Terminal profiles from a previously saved JSON backup", Properties.Resources.Open, () =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        if (TerminalProfilesManager.RestoreProfiles(ofd.FileName))
                        {
                            AppMessageBox.Show("Terminal profiles restored successfully! (Previous settings saved as .bak)", MessageBoxButtons.OK);
                            this.LoadItems();
                        }
                        else
                        {
                            AppMessageBox.Show("Failed to restore terminal profiles from selected JSON file.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }));

            if (hasTerminal)
            {
                this.AddItem(new TerminalGroupHeaderItem($"{edition} Profiles (settings.json)", "Profiles loaded dynamically from Windows Terminal settings.json"));
                foreach (var p in profiles)
                {
                    if (p.IsWindowsTerminal) this.AddItem(new TerminalProfileItem(p));
                }

                this.AddItem(new TerminalGroupHeaderItem("Standalone Consoles (Direct execution)", "Direct cmd.exe and powershell.exe console entries"));
                foreach (var p in profiles)
                {
                    if (!p.IsWindowsTerminal) this.AddItem(new TerminalProfileItem(p));
                }
            }
            else
            {
                this.AddItem(new TerminalGroupHeaderItem("Standalone Consoles (Terminal Not Detected)", "Windows Terminal was not found. Using native console commands directly."));
                foreach (var p in profiles)
                {
                    this.AddItem(new TerminalProfileItem(p));
                }
            }

            this.AddItem(new TerminalGroupHeaderItem("Windows 11 Integration & Cleanup", "Clean scattered context menu items and block native terminal extension"));
            this.AddItem(new TerminalBlockExtensionItem());

            this.ResumeLayout(true);
        }
    }
}