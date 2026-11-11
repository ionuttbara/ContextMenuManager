using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ContextMenuManager.Controls
{
    /// <summary>Integrated Folder Management editor; does not launch the old Rust executable.</summary>
    internal sealed class FolderManagementPanel : UserControl
    {
        private FolderProfileDocument document;
        private ManagedFolderProfile current;
        private bool dirty, suppress, draft;
        private readonly ListBox profiles = new ListBox();
        private readonly ComboBox groupFilter = new ComboBox();
        private readonly TextBox name = new TextBox(), folderPath = new TextBox(), iconPath = new TextBox(),
            group = new TextBox(), clsid = new TextBox();
        private readonly NumericUpDown position = new NumericUpDown(), sort = new NumericUpDown();
        private readonly ComboBox description = new ComboBox();
        private readonly CheckBox desktop = new CheckBox(), thisPc = new CheckBox(), pinned = new CheckBox();
        private readonly Label status = new Label(), heading = new Label();
        private readonly Button newClsid;
        private readonly ToolTip buttonTips = new ToolTip();
        private readonly TableLayoutPanel split = new TableLayoutPanel();
        private readonly bool dark = ThemeManager.IsDarkMode();
        private readonly Color accent = ThemeManager.GetSystemAccentColor();
        private Color BaseColor => dark ? Color.FromArgb(32, 32, 32) : Color.White;
        private Color Surface => dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(245, 245, 245);
        private Color TextColor => dark ? Color.White : Color.FromArgb(43, 43, 43);
        internal bool IsLoaded => document != null;
        internal bool ConfirmClose() => document == null || LeaveCurrent();

        public FolderManagementPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = BaseColor;
            ForeColor = TextColor;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            split.Dock = DockStyle.Fill;
            split.ColumnCount = 2;
            split.RowCount = 1;
            split.Margin = Padding.Empty;
            split.Padding = Padding.Empty;
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245.DpiZoom()));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(split);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1,
                RowCount = 4, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Surface };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 36.DpiZoom()));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32.DpiZoom()));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 176.DpiZoom()));
            split.Controls.Add(left, 0, 0);
            Label leftTitle = LabelFor(T("Saved folders", "Foldere salvate", "Carpetas guardadas", "Mentett mappák"));
            leftTitle.Dock = DockStyle.Top;
            leftTitle.Height = 36;
            leftTitle.Font = new Font(Font, FontStyle.Bold);
            leftTitle.Padding = new Padding(9, 10, 0, 2);
            left.Controls.Add(leftTitle, 0, 0);
            groupFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            groupFilter.Dock = DockStyle.Top;
            groupFilter.Height = 30;
            groupFilter.SelectedIndexChanged += (s, e) =>
            {
                if (suppress) return;
                string desiredGroup = groupFilter.SelectedIndex == 0 ? null : groupFilter.SelectedItem as string;
                if (!LeaveCurrent())
                {
                    suppress = true;
                    string original = current?.Group;
                    groupFilter.SelectedIndex = original == null ? 0 :
                        Math.Max(0, groupFilter.Items.IndexOf(original));
                    suppress = false;
                    return;
                }
                suppress = true;
                groupFilter.SelectedIndex = desiredGroup == null ? 0 :
                    Math.Max(0, groupFilter.Items.IndexOf(desiredGroup));
                suppress = false;
                RefreshList(current?.Clsid);
            };
            left.Controls.Add(groupFilter, 0, 1);

            profiles.Dock = DockStyle.Fill;
            profiles.DrawMode = DrawMode.OwnerDrawFixed;
            profiles.ItemHeight = 45;
            profiles.IntegralHeight = false;
            profiles.BorderStyle = BorderStyle.None;
            profiles.BackColor = Surface;
            profiles.ForeColor = TextColor;
            profiles.DrawItem += DrawProfile;
            profiles.SelectedIndexChanged += (s, e) => OnSelectionChanged();
            left.Controls.Add(profiles, 0, 2);

            var leftButtons = new TableLayoutPanel { Dock = DockStyle.Bottom, Padding = new Padding(6, 6, 6, 5),
                ColumnCount = 2, RowCount = 5, Height = 176, BackColor = Surface };
            leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int n = 0; n < 5; n++) leftButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            AddLeft(leftButtons, 0, 0, T("New", "Nou", "Nuevo", "Új"), NewProfile);
            AddLeft(leftButtons, 1, 0, T("Duplicate", "Duplicare", "Duplicar", "Másolás"), Duplicate);
            AddLeft(leftButtons, 0, 1, T("Delete", "Ștergere", "Eliminar", "Törlés"), DeleteProfile);
            AddLeft(leftButtons, 1, 1, T("Refresh", "Reîmprosp.", "Actualizar", "Frissítés"), ReloadProfiles);
            AddLeft(leftButtons, 0, 2, T("Import .reg", "Import .reg", "Importar .reg", "Reg. import"), ImportReg);
            AddLeft(leftButtons, 1, 2, T("Import Rust", "Import Rust", "Importar Rust", "Rust import"), ImportLegacy);
            AddLeft(leftButtons, 0, 3, T("Import JSON", "Import JSON", "Importar JSON", "JSON import"), ImportJson);
            AddLeft(leftButtons, 1, 3, T("Export JSON", "Export JSON", "Exportar JSON", "JSON export"), ExportJson);
            AddLeft(leftButtons, 0, 4, T("Install group", "Instalare grup", "Instalar grupo", "Csoport telep."), () => GroupAction(true));
            AddLeft(leftButtons, 1, 4, T("Remove group", "Ștergere grup", "Quitar grupo", "Csoport eltávol."), () => GroupAction(false));
            left.Controls.Add(leftButtons, 0, 3);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BaseColor };
            split.Controls.Add(scroll, 1, 0);
            var form = new TableLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(11, 11, 14, 16),
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, BackColor = BaseColor };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            form.Resize += (s, e) => { };
            scroll.Controls.Add(form);
            scroll.Resize += (s, e) =>
            {
                // Keep the editor as wide as the viewport without runaway autosizing.
                form.Width = Math.Max(355, scroll.ClientSize.Width - (scroll.VerticalScroll.Visible ? 20 : 2));
            };

            heading = LabelFor(T("Folder information", "Informații folder", "Datos de carpeta", "Mappa adatai"));
            heading.Font = new Font(Font.FontFamily, Font.Size + 1.4f, FontStyle.Bold);
            heading.ForeColor = accent;
            heading.Dock = DockStyle.Fill;
            heading.Padding = new Padding(0, 1, 0, 7);
            heading.AutoSize = true;
            AddRow(form, "", heading, null, 33, true);
            name.Dock = DockStyle.Fill;
            AddRow(form, T("Folder name", "Nume folder", "Nombre", "Mappanév"), name, null, 34);
            folderPath.Dock = DockStyle.Fill;
            AddRow(form, T("Folder path", "Cale folder", "Ruta", "Mappa útvonal"), folderPath,
                MakeButton("...", BrowseFolder), 35);
            iconPath.Dock = DockStyle.Fill;
            AddRow(form, T("Icon / .ico", "Iconiță / .ico", "Icono / .ico", "Ikon / .ico"), iconPath,
                MakeButton("...", BrowseIcon), 35);
            group.Dock = DockStyle.Fill;
            AddRow(form, T("Profile group", "Grup profil", "Grupo", "Profilcsoport"), group, null, 34);
            position.Minimum = 1; position.Maximum = 999999; position.Value = 1;
            position.Dock = DockStyle.Left; position.Width = 94;
            AddRow(form, T("Position ID", "ID poziție", "ID posición", "Pozíció ID"), position, null, 35);
            buttonTips.SetToolTip(position, T("Local profile ID. Old Rust registry positions do not block installation.",
                "ID local. Pozițiile vechi din registrul Rust nu blochează instalarea.",
                "ID local. Los registros antiguos Rust no bloquean la instalación.",
                "Helyi azonosító. A régi Rust-bejegyzések nem akadályozzák a telepítést."));
            sort.Minimum = 0; sort.Maximum = 65535; sort.Value = 24;
            sort.Dock = DockStyle.Left; sort.Width = 94;
            AddRow(form, T("Nav sort order", "Ordine navigare", "Orden nav.", "Sorrend"), sort, null, 35);
            description.Items.AddRange(new object[] { "3 - Folder", "6 - System", "9 - Other" });
            description.DropDownStyle = ComboBoxStyle.DropDownList;
            description.SelectedIndex = 0;
            description.Dock = DockStyle.Left; description.Width = 135;
            AddRow(form, "DescriptionID", description, null, 35);
            clsid.Dock = DockStyle.Fill; clsid.ReadOnly = true; clsid.TabStop = false;
            clsid.Font = new Font(Font.FontFamily, Math.Max(7, Font.Size - 1));
            newClsid = MakeButton(T("Generate", "Generează", "Generar", "Generálás"), RegenerateClsid);
            AddRow(form, "CLSID / GUID", clsid, newClsid, 35);

            Label vis = LabelFor(T("Explorer visibility", "Vizibilitate Explorer", "Visibilidad", "Explorer láthatóság"));
            vis.Font = new Font(Font, FontStyle.Bold);
            vis.ForeColor = accent; vis.Padding = new Padding(0, 9, 0, 5);
            AddRow(form, "", vis, null, 34, true);
            desktop.Text = T("Show on Desktop", "Afișare pe Desktop", "Mostrar en escritorio", "Asztalon mutat");
            thisPc.Text = T("Show in This PC", "Afișare în Acest PC", "Mostrar en Este equipo", "Ez a gép alatt");
            pinned.Text = T("Pin in navigation pane", "Fixare în navigare", "Fijar en navegación", "Navigációhoz rögzít");
            foreach (CheckBox cb in new[] { desktop, thisPc, pinned })
            {
                cb.AutoSize = true; cb.Dock = DockStyle.Fill; cb.ForeColor = TextColor;
                AddRow(form, "", cb, null, 28, true);
            }
            Label actions = LabelFor(T("Registry actions", "Acțiuni registru", "Acciones del registro", "Registry műveletek"));
            actions.Font = new Font(Font, FontStyle.Bold); actions.ForeColor = accent;
            actions.Padding = new Padding(0, 10, 0, 5);
            AddRow(form, "", actions, null, 34, true);
            var actionPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 1) };
            AddAction(actionPanel, T("Save profile", "Salvează profil", "Guardar perfil", "Profil mentése"), () => SaveCurrent());
            AddAction(actionPanel, T("Install / update", "Instalează / actualizează", "Instalar / actualizar", "Telepítés / frissítés"), Install);
            AddAction(actionPanel, T("Remove from Windows", "Elimină din Windows", "Quitar de Windows", "Eltávolítás Windowsból"), Remove);
            AddAction(actionPanel, T("Export ADD .reg", "Export ADAUGĂ .reg", "Exportar ADD .reg", "Hozzáadás .reg"), () => ExportReg(false));
            AddAction(actionPanel, T("Export REMOVE .reg", "Export ȘTERGE .reg", "Exportar REMOVE .reg", "Eltávolítás .reg"), () => ExportReg(true));
            AddAction(actionPanel, T("Remove by CLSID...", "Ștergere după CLSID...", "Quitar por CLSID...", "CLSID törlése..."), RemoveByClsid);
            AddRow(form, "", actionPanel, null, 137, true);
            status = LabelFor(T("Profiles are saved locally; registry changes require Administrator.",
                "Profilurile sunt salvate local; modificările registrului necesită Administrator.",
                "Perfiles locales; cambios del registro requieren administrador.",
                "Helyi profilok; a rendszerleíró módosítás adminisztrátori jogot igényel."));
            status.AutoSize = true;
            status.MaximumSize = new Size(550, 0);
            status.Padding = new Padding(0, 7, 0, 7);
            status.ForeColor = TextColor;
            AddRow(form, "", status, null, 74, true);

            foreach (Control input in new Control[] { name, folderPath, iconPath, group, clsid, groupFilter,
                description, position, sort })
            {
                input.BackColor = dark ? Color.FromArgb(53, 53, 53) : Color.White;
                input.ForeColor = TextColor;
            }

            foreach (Control ctrl in new Control[] { name, folderPath, iconPath, group })
                ctrl.TextChanged += (s, e) => MarkDirty();
            position.ValueChanged += (s, e) => MarkDirty();
            sort.ValueChanged += (s, e) => MarkDirty();
            description.SelectedIndexChanged += (s, e) => MarkDirty();
            desktop.CheckedChanged += (s, e) => MarkDirty();
            thisPc.CheckedChanged += (s, e) => MarkDirty();
            pinned.CheckedChanged += (s, e) => MarkDirty();
        }

        internal void LoadData()
        {
            try
            {
                document = FolderManagementManager.Load();
                RefreshList(document.Profiles.FirstOrDefault()?.Clsid);
                if (document.Profiles.Count == 0) CreateDraft();
            }
            catch (Exception ex)
            {
                document = null; ShowError(ex);
            }
        }

        private static string T(string en, string ro, string es, string hu)
        {
            switch (UiLanguage.CurrentCode)
            {
                case "ro": return ro;
                case "es": return es;
                case "hu": return hu;
                default: return en;
            }
        }

        private Label LabelFor(string text) => new Label { Text = text, TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false, ForeColor = TextColor, BackColor = Color.Transparent };

        private void AddRow(TableLayoutPanel table, string label, Control control, Control side, int height, bool wide = false)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            if (wide)
            {
                table.Controls.Add(control, 0, row);
                table.SetColumnSpan(control, 3);
                control.Dock = DockStyle.Fill;
                return;
            }
            Label caption = LabelFor(label);
            caption.Dock = DockStyle.Fill;
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(control, 1, row);
            if (side != null)
            {
                side.Dock = DockStyle.Fill;
                table.Controls.Add(side, 2, row);
            }
            else table.SetColumnSpan(control, 2);
            control.Margin = new Padding(2, 3, 2, 4);
        }

        private Button MakeButton(string text, Action action)
        {
            Button btn = new Button { Text = text, AutoSize = false, BackColor = Surface, ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 2, 3, 2), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = dark ? Color.FromArgb(75, 75, 75) : Color.FromArgb(190, 190, 190);
            btn.Click += (s, e) => { try { action(); } catch (Exception ex) { ShowError(ex); } };
            return btn;
        }

        private void AddLeft(TableLayoutPanel table, int x, int y, string text, Action action)
        {
            Button btn = MakeButton(text, action); btn.Dock = DockStyle.Fill;
            btn.Font = new Font(Font.FontFamily, Math.Max(7, Font.Size - 0.3f));
            table.Controls.Add(btn, x, y);
            btn.AutoEllipsis = true;
            buttonTips.SetToolTip(btn, text);
        }

        private void AddAction(FlowLayoutPanel panel, string text, Action action)
        {
            Button btn = MakeButton(text, action);
            btn.AutoSize = true;
            btn.MinimumSize = new Size(116, 30);
            btn.Padding = new Padding(6, 3, 6, 3);
            panel.Controls.Add(btn);
        }

        private void DrawProfile(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= profiles.Items.Count) return;
            ManagedFolderProfile p = (ManagedFolderProfile)profiles.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (Brush bg = new SolidBrush(selected ? accent : Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
            Color ink = selected ? Color.White : TextColor;
            Color secondary = selected ? Color.White : (dark ? Color.Silver : Color.DimGray);
            TextRenderer.DrawText(e.Graphics, p.Name, new Font(Font, FontStyle.Bold),
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 3, e.Bounds.Width - 20, 19), ink,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string installState = FolderManagementManager.IsRustManagedClsid(p.Clsid) ?
                (FolderManagementManager.IsInstalled(p) ? "Installed" : "Installed / edited") : "Not installed";
            string line = p.Group + "  ·  " + p.PositionId + "  ·  " + installState;
            TextRenderer.DrawText(e.Graphics, line, Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 24, e.Bounds.Width - 15, 17), secondary,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private string FilterGroup => groupFilter.SelectedIndex <= 0 ? null : groupFilter.SelectedItem as string;

        private void RefreshList(string selectedClsid)
        {
            if (document == null) return;
            suppress = true;
            try
            {
                string selectedGroup = groupFilter.SelectedItem as string;
                var groups = document.Profiles.Select(p => string.IsNullOrWhiteSpace(p.Group) ? "Default" : p.Group.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                groupFilter.Items.Clear();
                groupFilter.Items.Add(T("All profile groups", "Toate grupurile", "Todos los grupos", "Összes csoport"));
                foreach (string item in groups) groupFilter.Items.Add(item);
                groupFilter.SelectedIndex = selectedGroup != null && groups.Any(g => g.Equals(selectedGroup, StringComparison.OrdinalIgnoreCase))
                    ? groupFilter.Items.IndexOf(groups.First(g => g.Equals(selectedGroup, StringComparison.OrdinalIgnoreCase))) : 0;
                profiles.BeginUpdate();
                profiles.Items.Clear();
                foreach (ManagedFolderProfile p in document.Profiles.OrderBy(p => p.Group).ThenBy(p => p.PositionId))
                    if (FilterGroup == null || string.Equals(p.Group, FilterGroup, StringComparison.OrdinalIgnoreCase))
                        profiles.Items.Add(p);
                if (selectedClsid != null)
                    for (int i = 0; i < profiles.Items.Count; i++)
                        if (((ManagedFolderProfile)profiles.Items[i]).Clsid == selectedClsid) { profiles.SelectedIndex = i; break; }
                profiles.EndUpdate();
                if (selectedClsid != null && profiles.SelectedIndex < 0) ShowProfile(null, false);
                else if (profiles.SelectedItem is ManagedFolderProfile && !dirty)
                    ShowProfile((ManagedFolderProfile)profiles.SelectedItem, false);
            }
            finally { suppress = false; }
        }

        private void OnSelectionChanged()
        {
            if (suppress) return;
            ManagedFolderProfile picked = profiles.SelectedItem as ManagedFolderProfile;
            if (picked == null || (!draft && current != null && picked.Clsid == current.Clsid)) return;
            if (!LeaveCurrent())
            {
                suppress = true;
                profiles.SelectedItem = draft ? null : current;
                suppress = false;
                return;
            }
            // Saving in LeaveCurrent rebuilds the list. Restore the item the user clicked.
            ManagedFolderProfile updated = document.Profiles.FirstOrDefault(p => p.Clsid == picked.Clsid);
            if (updated == null) return;
            suppress = true;
            profiles.SelectedItem = updated;
            suppress = false;
            ShowProfile(updated, false);
        }

        private void ShowProfile(ManagedFolderProfile p, bool newDraft)
        {
            suppress = true;
            try
            {
                current = p;
                draft = newDraft;
                dirty = false;
                if (p == null) return;
                name.Text = p.Name;
                folderPath.Text = p.Path;
                iconPath.Text = p.IconPath;
                group.Text = p.Group;
                clsid.Text = p.Clsid;
                position.Value = Math.Max(position.Minimum, Math.Min(position.Maximum, p.PositionId));
                sort.Value = Math.Max(sort.Minimum, Math.Min(sort.Maximum, p.SortOrder));
                description.SelectedIndex = p.DescriptionId == 6 ? 1 : p.DescriptionId == 9 ? 2 : 0;
                desktop.Checked = p.ShowDesktop;
                thisPc.Checked = p.ShowThisPc;
                pinned.Checked = p.PinnedNavigation;
                newClsid.Enabled = newDraft && !FolderManagementManager.IsRustManagedClsid(p.Clsid);
                heading.Text = newDraft ? T("New folder profile", "Profil nou de folder", "Nuevo perfil", "Új mappaprofil") : p.Name;
            }
            finally { suppress = false; }
        }

        private void MarkDirty()
        {
            if (suppress || current == null) return;
            dirty = true;
            heading.Text = (draft ? T("New profile", "Profil nou", "Perfil nuevo", "Új profil") : name.Text) + " *";
        }

        private bool LeaveCurrent()
        {
            if (!dirty && !draft) return true;
            if (draft && !dirty && string.IsNullOrWhiteSpace(name.Text) && string.IsNullOrWhiteSpace(folderPath.Text)) return true;
            DialogResult answer = MessageBox.Show(T("Save edits to this folder profile?", "Salvezi modificările acestui profil?",
                "¿Guardar los cambios de este perfil?", "Menti a profil módosításait?"), "Folder Management",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return false;
            if (answer == DialogResult.Yes) return SaveCurrent();
            // Discard unsaved changes rather than leaking the dirty editor into subsequent actions.
            if (draft) { draft = false; current = null; dirty = false; }
            else ShowProfile(current, false);
            return true;
        }

        private ManagedFolderProfile ReadEditor()
        {
            if (current == null) throw new InvalidOperationException("Select or create a profile first.");
            ManagedFolderProfile p = current.Copy();
            p.Name = name.Text.Trim();
            p.Path = folderPath.Text.Trim();
            p.IconPath = iconPath.Text.Trim();
            p.Group = string.IsNullOrWhiteSpace(group.Text) ? "Default" : group.Text.Trim();
            p.PositionId = (int)position.Value;
            p.SortOrder = (int)sort.Value;
            p.DescriptionId = description.SelectedIndex == 1 ? 6 : description.SelectedIndex == 2 ? 9 : 3;
            p.ShowDesktop = desktop.Checked;
            p.ShowThisPc = thisPc.Checked;
            p.PinnedNavigation = pinned.Checked;
            FolderManagementManager.Validate(p, false);
            // A manually chosen ID can collide with a different *saved* profile.
            // Reassign only this profile, never replace the other folder.
            p.PositionId = FolderManagementManager.FreeProfilePosition(
                document.Profiles, p.PositionId, p.Clsid);
            return p;
        }

        private bool SaveCurrent()
        {
            if (document == null) return false;
            try
            {
                int requestedPosition = (int)position.Value;
                ManagedFolderProfile updated = ReadEditor();
                int index = document.Profiles.FindIndex(p => p.Clsid == updated.Clsid);
                if (index >= 0) document.Profiles[index] = updated;
                else document.Profiles.Add(updated);
                try { FolderManagementManager.Save(document); }
                catch
                {
                    if (index >= 0) document.Profiles[index] = current;
                    else document.Profiles.Remove(updated);
                    throw;
                }
                current = updated;
                dirty = draft = false;
                if (FilterGroup != null && !updated.Group.Equals(FilterGroup, StringComparison.OrdinalIgnoreCase))
                {
                    suppress = true;
                    groupFilter.SelectedIndex = 0;
                    suppress = false;
                }
                RefreshList(updated.Clsid);
                Status(updated.PositionId == requestedPosition ?
                    T("Profile saved.", "Profil salvat.", "Perfil guardado.", "Profil mentve.") :
                    T("Profile saved. Conflicting local position reassigned to ",
                      "Profil salvat. Poziția locală ocupată a fost schimbată în ",
                      "Perfil guardado. Posición local reasignada a ",
                      "Profil mentve. A foglalt helyi pozíció módosult erre: ") + updated.PositionId + ".");
                return true;
            }
            catch (Exception ex) { ShowError(ex); return false; }
        }

        private void CreateDraft(ManagedFolderProfile clone = null)
        {
            ManagedFolderProfile p = clone ?? FolderManagementManager.NewProfile(document.Profiles);
            suppress = true;
            profiles.ClearSelected();
            suppress = false;
            ShowProfile(p, true);
            if (clone != null) dirty = true;
            Status(T("New profile: choose a folder and save.", "Profil nou: alege folderul și salvează.",
                "Nuevo perfil: elige carpeta y guarda.", "Új profil: válassz mappát, majd mentsd."));
        }

        private void NewProfile() { if (document != null && LeaveCurrent()) CreateDraft(); }
        private void Duplicate()
        {
            if (document == null || current == null || !LeaveCurrent()) return;
            ManagedFolderProfile source = current.Copy();
            var p = FolderManagementManager.NewProfile(document.Profiles);
            source.Clsid = p.Clsid; source.PositionId = p.PositionId;
            source.Name = source.Name + " (copy)";
            CreateDraft(source);
        }

        private void DeleteProfile()
        {
            if (document == null || current == null) return;
            if (draft) { CreateDraft(); return; }
            if (FolderManagementManager.IsRustManagedClsid(current.Clsid))
            {
                Status(T("Remove the folder from Windows before deleting its saved profile.",
                    "Elimină folderul din Windows înainte de a șterge profilul.",
                    "Quita primero la carpeta de Windows.", "Előbb távolítsd el a mappát a Windowsból."));
                return;
            }
            if (MessageBox.Show(T("Delete saved profile? (No registry changes)", "Ștergi profilul salvat? (Fără modificări de registru)",
                "¿Eliminar perfil sin modificar el registro?", "Törlöd a mentett profilt? (A regisztráció változatlan)"),
                "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ManagedFolderProfile removed = current;
            document.Profiles.RemoveAll(p => p.Clsid == removed.Clsid);
            try { FolderManagementManager.Save(document); }
            catch { document.Profiles.Add(removed); throw; }
            RefreshList(null);
            if (document.Profiles.Count > 0) RefreshList(document.Profiles[0].Clsid);
            else CreateDraft();
        }

        private void RegenerateClsid()
        {
            if (current == null) return;
            if (FolderManagementManager.ClsidExists(current.Clsid))
                throw new InvalidOperationException("Remove the installed folder before changing its CLSID, or use Duplicate.");
            string newId = FolderManagementManager.NewClsid();
            if (draft)
            {
                current.Clsid = newId;
                clsid.Text = newId;
                MarkDirty();
                return;
            }
            // A saved profile's CLSID is its persistent identity; generate by cloning instead.
            throw new InvalidOperationException("Duplicate this profile to generate a new CLSID. Saved profile IDs are persistent.");
        }

        private void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Folder Management - choose target folder",
                ShowNewFolderButton = true })
            {
                if (Directory.Exists(folderPath.Text)) dlg.SelectedPath = folderPath.Text;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                folderPath.Text = dlg.SelectedPath;
                if (string.IsNullOrWhiteSpace(name.Text))
                    name.Text = Path.GetFileName(dlg.SelectedPath.TrimEnd('\\', '/'));
            }
        }

        private void BrowseIcon()
        {
            using (var dlg = new OpenFileDialog { Title = "Folder icon", Filter = "Icons (*.ico)|*.ico|All files (*.*)|*.*" })
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK) iconPath.Text = dlg.FileName;
        }

        private void Install()
        {
            if (!SaveCurrent()) return;
            if (MessageBox.Show(T("Install or update this folder in Explorer? Windows registry will be modified.",
                "Instalezi sau actualizezi folderul în Explorer? Registrul Windows va fi modificat.",
                "¿Instalar o actualizar esta carpeta en Explorer?", "Telepíted/frissíted ezt a mappát az Explorerben?"),
                "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            FolderManagementManager.Install(current);
            profiles.Invalidate();
            Status(T("Folder installed / updated. Explorer was notified.", "Folder instalat / actualizat. Explorer a fost notificat.",
                "Carpeta instalada / actualizada.", "Mappa telepítve/frissítve."));
        }

        private void Remove()
        {
            if (current == null) return;
            if (!FolderManagementManager.IsRustManagedClsid(current.Clsid))
            { Status(T("This profile is not installed.", "Acest profil nu este instalat.", "Este perfil no está instalado.", "Nincs telepítve.")); return; }
            if (MessageBox.Show(T("Remove this folder from Explorer? The real folder and saved profile will NOT be deleted.",
                "Elimini folderul din Explorer? Folderul real și profilul salvat NU vor fi șterse.",
                "¿Quitar de Explorer? No se borran la carpeta real ni el perfil.",
                "Eltávolítod az Explorerből? A valódi mappa és a profil megmarad."),
                "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            FolderManagementManager.Remove(current.Clsid);
            profiles.Invalidate();
            Status(T("Explorer folder removed. Real files are untouched.", "Folderul Explorer a fost eliminat. Fișierele reale sunt intacte.",
                "Carpeta quitada; archivos reales intactos.", "Eltávolítva; a fájlok sértetlenek."));
        }

        private void RemoveByClsid()
        {
            using (var input = new InputDialog { Title = "Folder Management - CLSID", Text = current?.Clsid })
            {
                if (input.ShowDialog(FindForm()) != DialogResult.OK) return;
                string id = FolderManagementManager.CanonicalClsid((input.Text ?? "").Trim());
                if (!FolderManagementManager.IsRustManagedClsid(id))
                    throw new InvalidOperationException("Only a recognizable Rust / Folder Management CLSID can be removed; arbitrary system CLSIDs are blocked.");
                if (MessageBox.Show("Remove managed Explorer folder " + id + "? Real files will be untouched.",
                    "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                FolderManagementManager.Remove(id);
                profiles.Invalidate();
                Status("Removed " + id + ". Saved profiles are kept.");
            }
        }

        private void ExportReg(bool remove)
        {
            if (current == null) return;
            // Export any valid draft without saving it, but never export unsaved edits silently.
            ManagedFolderProfile p = ReadEditor();
            if (FolderManagementManager.ClsidExists(p.Clsid) && !FolderManagementManager.IsRustManagedClsid(p.Clsid))
                throw new InvalidOperationException("CLSID is already used by a different Windows component. No registry export is permitted; duplicate with a fresh CLSID instead.");
            using (var dlg = new SaveFileDialog { Filter = "Windows registry (*.reg)|*.reg", DefaultExt = "reg",
                FileName = (remove ? "remove_" : "add_") + SafeName(p.Name) + ".reg" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                FolderManagementManager.WriteRegFile(dlg.FileName, remove ? FolderManagementManager.RemoveReg(p.Clsid) : FolderManagementManager.AddReg(p));
                Status("Exported " + dlg.FileName + " (UTF-16LE). Exporting does not change the registry.");
            }
        }

        private static string SafeName(string text)
        {
            return new string((text ?? "folder").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        }

        private void ReloadProfiles()
        {
            if (!LeaveCurrent()) return;
            document = null;
            LoadData();
        }

        private void ImportLegacy()
        {
            if (document == null || !LeaveCurrent()) return;
            List<ManagedFolderProfile> found = FolderManagementManager.ReadLegacyRegistry();
            found.AddRange(FolderManagementManager.ReadRustClsidMap());
            MergeProfiles(found);
        }

        private void ImportReg()
        {
            if (document == null || !LeaveCurrent()) return;
            using (var dlg = new OpenFileDialog { Title = "Import Rust Folder Manager .reg profile(s)",
                Filter = "Registry profiles (*.reg)|*.reg", Multiselect = true })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                var found = new List<ManagedFolderProfile>();
                var errors = new List<string>();
                foreach (string path in dlg.FileNames)
                {
                    try { found.Add(FolderManagementManager.ImportRustReg(path)); }
                    catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
                }
                MergeProfiles(found);
                if (errors.Count > 0) MessageBox.Show(string.Join(Environment.NewLine, errors.Take(12)),
                    "Import errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ImportJson()
        {
            if (document == null || !LeaveCurrent()) return;
            using (var dlg = new OpenFileDialog { Title = "Import Folder Management profile bundle",
                Filter = "Folder profiles (*.json)|*.json" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                MergeProfiles(FolderManagementManager.ImportProfiles(dlg.FileName));
            }
        }

        private void MergeProfiles(IEnumerable<ManagedFolderProfile> candidates)
        {
            var incoming = candidates.ToList();
            var accepted = new List<ManagedFolderProfile>();
            int skipped = 0, adjusted = 0;
            foreach (ManagedFolderProfile p in incoming)
            {
                try
                {
                    FolderManagementManager.Validate(p, false);
                    if (FolderManagementManager.ClsidExists(p.Clsid) && !FolderManagementManager.IsRustManagedClsid(p.Clsid))
                    { skipped++; continue; }
                    var same = document.Profiles.Concat(accepted)
                        .FirstOrDefault(x => x.Clsid.Equals(p.Clsid, StringComparison.OrdinalIgnoreCase));
                    if (same != null) { skipped++; continue; }
                    if (document.Profiles.Concat(accepted).Any(x => x.PositionId == p.PositionId))
                    {
                        p.PositionId = FolderManagementManager.FreeProfilePosition(document.Profiles.Concat(accepted), p.PositionId);
                        adjusted++;
                    }
                    accepted.Add(p);
                }
                catch { skipped++; }
            }
            if (accepted.Count == 0)
            {
                Status("Nothing new found. Skipped duplicates or invalid items: " + skipped + ".");
                return;
            }
            if (MessageBox.Show("Import " + accepted.Count + " profile(s), without installing them? " +
                "Skipped: " + skipped + ", reassigned position IDs: " + adjusted + ".",
                "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            document.Profiles.AddRange(accepted);
            try { FolderManagementManager.Save(document); }
            catch { foreach (var p in accepted) document.Profiles.Remove(p); throw; }
            suppress = true;
            groupFilter.SelectedIndex = 0;
            suppress = false;
            RefreshList(accepted[0].Clsid);
            Status("Imported " + accepted.Count + " profile(s), skipped " + skipped + ". Registry unchanged.");
        }

        private void ExportJson()
        {
            if (document == null) return;
            IEnumerable<ManagedFolderProfile> toExport = FilterGroup == null ? document.Profiles :
                document.Profiles.Where(p => p.Group.Equals(FilterGroup, StringComparison.OrdinalIgnoreCase));
            using (var dlg = new SaveFileDialog { Title = "Export folder profile bundle",
                Filter = "Folder profiles (*.json)|*.json", DefaultExt = "json", FileName = "folder-profiles.json" })
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                { FolderManagementManager.ExportProfiles(toExport, dlg.FileName); Status("Profiles exported (no registry changes)."); }
        }

        private void GroupAction(bool install)
        {
            if (document == null || !LeaveCurrent()) return;
            string target = FilterGroup;
            if (target == null)
                target = (current?.Group ?? "Default").Trim();
            var selected = document.Profiles.Where(p => p.Group.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList();
            if (selected.Count == 0) { Status("This group has no saved profiles."); return; }
            if (MessageBox.Show((install ? "Install / update " : "Remove from Explorer ") + selected.Count +
                " profile(s) in group '" + target + "'? Existing folder contents are never deleted.",
                "Folder Management", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int ok = 0;
            var failures = new List<string>();
            foreach (var p in selected)
            {
                try
                {
                    if (install) FolderManagementManager.Install(p);
                    else if (FolderManagementManager.IsRustManagedClsid(p.Clsid)) FolderManagementManager.Remove(p.Clsid);
                    ok++;
                }
                catch (Exception ex) { failures.Add(p.Name + ": " + ex.Message); }
            }
            profiles.Invalidate();
            Status((install ? "Installed / updated: " : "Processed removals: ") + ok +
                ". Failures: " + failures.Count + ". " + string.Join("; ", failures.Take(2)));
            if (failures.Count > 0) MessageBox.Show(string.Join(Environment.NewLine, failures.Take(15)),
                "Folder Management", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Status(string text) { status.Text = text; status.ForeColor = TextColor; }
        private void ShowError(Exception ex)
        {
            status.Text = ex.Message;
            status.ForeColor = dark ? Color.Salmon : Color.DarkRed;
            MessageBox.Show(ex.Message, "Folder Management", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
