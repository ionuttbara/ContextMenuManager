using BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Windows.Forms;

namespace ContextMenuManager.Controls.Interfaces
{
    interface ITsiRegExportItem
    {
        string Text { get; set; }
        string RegPath { get; }
        ContextMenuStrip ContextMenuStrip { get; set; }
        RegExportMenuItem TsiRegExport { get; set; }
    }

    sealed class RegExportMenuItem : ToolStripMenuItem
    {
        public RegExportMenuItem(ITsiRegExportItem item) : base(AppString.Menu.ExportRegistry)
        {
            item.ContextMenuStrip.Opening += (sender, e) =>
            {
                using (var key = RegistryEx.GetRegistryKey(item.RegPath)) this.Visible = key != null;
            };
            this.Click += (sender, e) =>
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    string time = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
                    dlg.FileName = $"{item.Text} - {time}.reg";
                    dlg.Filter = $"{AppString.Dialog.RegistryFile}|*.reg";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        ExternalProgram.ExportRegistry(item.RegPath, dlg.FileName);
                }
            };
        }
    }
}
