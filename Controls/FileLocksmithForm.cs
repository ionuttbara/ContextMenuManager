using ContextMenuManager.BluePointLilac.Methods;
using ContextMenuManager.Methods;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using BluePointLilac.Methods;

namespace ContextMenuManager.Controls
{
    sealed class FileLocksmithForm : Form
    {
        private const int WM_THEMECHANGED = 0x031A;
        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETHEADER = LVM_FIRST + 31;

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly string[] targetPaths;
        private readonly ListView processList = new ListView();
        private readonly ImageList processImages = new ImageList();
        private readonly Label titleLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly Label detailsLabel = new Label();
        private readonly Button btnReload;
        private readonly Button btnEndTask;
        private readonly Button btnAdmin;
        private readonly Button btnClose;
        private readonly Timer processTimer = new Timer();
        private bool scanning;

        private FileLocksmithProcessInfo SelectedProcess
        {
            get { return processList.SelectedItems.Count == 0 ? null : processList.SelectedItems[0].Tag as FileLocksmithProcessInfo; }
        }

        public FileLocksmithForm(string[] paths)
        {
            targetPaths = (paths ?? new string[0]).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            Text = "File Locksmith";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(860, 560).DpiZoom();
            MinimumSize = new Size(720, 460).DpiZoom();
            Font = SystemFonts.MessageBoxFont;

            btnReload = CreateButton("Reload", Properties.Resources.Refresh);
            btnEndTask = CreateButton("End task", Properties.Resources.Delete);
            btnAdmin = CreateButton("Restart as administrator", null);
            btnClose = CreateButton("Close", null);
            btnClose.DialogResult = DialogResult.OK;

            BuildLayout();
            WireEvents();
            ApplyTheme();

            Shown += async (s, e) =>
            {
                ThemeManager.ApplyTitleBar(this);
                btnAdmin.Visible = !FileLocksmithManager.IsAdministrator();
                await ReloadAsync();
            };
        }

        private static Button CreateButton(string text, Image image)
        {
            Image scaled = null;
            if (image != null)
            {
                try { scaled = new Bitmap(image, new Size(18, 18).DpiZoom()); }
                catch { scaled = image; }
            }
            return new Button
            {
                Text = text,
                Image = scaled,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(96, 36).DpiZoom(),
                Padding = new Padding(10, 3, 10, 3).DpiZoom(),
                Margin = new Padding(4).DpiZoom(),
                FlatStyle = FlatStyle.Flat,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
        }

        private void BuildLayout()
        {
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 72.DpiZoom();
            titleLabel.Padding = new Padding(14, 10, 14, 4).DpiZoom();
            titleLabel.Font = new Font(Font.FontFamily, 14F, FontStyle.Regular);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Text = BuildTargetCaption();

            statusLabel.Dock = DockStyle.Top;
            statusLabel.Height = 34.DpiZoom();
            statusLabel.Padding = new Padding(14, 0, 14, 4).DpiZoom();
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            processImages.ColorDepth = ColorDepth.Depth32Bit;
            processImages.ImageSize = new Size(24, 24).DpiZoom();

            processList.Dock = DockStyle.Fill;
            processList.View = View.Details;
            processList.FullRowSelect = true;
            processList.HideSelection = false;
            processList.MultiSelect = false;
            processList.SmallImageList = processImages;
            processList.Columns.Add("Process", 190.DpiZoom());
            processList.Columns.Add("PID", 70.DpiZoom());
            processList.Columns.Add("Type", 95.DpiZoom());
            processList.Columns.Add("User", 180.DpiZoom());
            processList.Columns.Add("Executable", 300.DpiZoom());
            processList.DrawColumnHeader += ProcessList_DrawColumnHeader;
            processList.DrawItem += (s, e) => e.DrawDefault = true;
            processList.DrawSubItem += (s, e) => e.DrawDefault = true;
            processList.HandleCreated += (s, e) => ApplyNativeListTheme();

            detailsLabel.Dock = DockStyle.Bottom;
            detailsLabel.Height = 70.DpiZoom();
            detailsLabel.Padding = new Padding(14, 8, 14, 8).DpiZoom();
            detailsLabel.AutoEllipsis = true;
            detailsLabel.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 54.DpiZoom(),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(7, 5, 7, 5).DpiZoom()
            };
            actions.Controls.Add(btnClose);
            actions.Controls.Add(btnEndTask);
            actions.Controls.Add(btnReload);
            actions.Controls.Add(btnAdmin);

            Controls.Add(processList);
            Controls.Add(detailsLabel);
            Controls.Add(actions);
            Controls.Add(statusLabel);
            Controls.Add(titleLabel);
            CancelButton = btnClose;
        }

        private void WireEvents()
        {
            processList.SelectedIndexChanged += (s, e) => UpdateSelection();
            btnReload.Click += async (s, e) => await ReloadAsync();
            btnEndTask.Click += async (s, e) => await EndSelectedTaskAsync();
            btnAdmin.Click += (s, e) =>
            {
                if (FileLocksmithManager.RelaunchAsAdministrator(targetPaths)) Close();
            };

            processTimer.Interval = 1500;
            processTimer.Tick += (s, e) => RemoveExitedProcesses();
            processTimer.Start();
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTheme(this);
            bool dark = ThemeManager.IsDarkMode();
            Color surface = dark ? Color.FromArgb(34, 34, 34) : Color.White;
            Color text = dark ? Color.White : Color.FromArgb(32, 32, 32);
            Color muted = dark ? Color.FromArgb(190, 190, 190) : Color.FromArgb(80, 80, 80);
            Color buttonBack = dark ? Color.FromArgb(45, 45, 45) : Color.FromArgb(248, 248, 248);
            Color buttonHover = dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(238, 238, 238);
            Color buttonPressed = dark ? Color.FromArgb(68, 68, 68) : Color.FromArgb(226, 226, 226);
            Color border = dark ? Color.FromArgb(82, 82, 82) : Color.FromArgb(190, 190, 190);

            processList.BackColor = surface;
            processList.ForeColor = text;
            titleLabel.ForeColor = text;
            statusLabel.ForeColor = muted;
            detailsLabel.ForeColor = muted;

            // Native ListView scroll bars do not inherit BackColor/ForeColor.
            // Use the Windows Explorer dark theme for the non-client scroll bars and
            // owner-draw the header so the column strip matches the rest of the window.
            processList.OwnerDraw = dark;
            ApplyNativeListTheme();

            foreach (Button button in new[] { btnReload, btnEndTask, btnAdmin, btnClose })
            {
                button.BackColor = buttonBack;
                button.ForeColor = text;
                button.FlatAppearance.BorderColor = border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = buttonHover;
                button.FlatAppearance.MouseDownBackColor = buttonPressed;
            }
        }

        private void ApplyNativeListTheme()
        {
            if (!processList.IsHandleCreated) return;

            bool dark = ThemeManager.IsDarkMode();
            try
            {
                SetWindowTheme(processList.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);

                // Theme the native header handle as well. The owner-drawn dark header below
                // is still used as a deterministic fallback on Windows builds where the
                // header ignores DarkMode_Explorer.
                IntPtr header = SendMessage(processList.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero)
                {
                    SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer", null);
                    SendMessage(header, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                }

                SendMessage(processList.Handle, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                processList.Invalidate(true);
            }
            catch
            {
                // SetWindowTheme is best-effort; custom header drawing still keeps the
                // visible column strip consistent with the application theme.
            }
        }

        private void ProcessList_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (!ThemeManager.IsDarkMode())
            {
                e.DrawDefault = true;
                return;
            }

            Color headerBack = Color.FromArgb(45, 45, 45);
            Color headerText = Color.FromArgb(240, 240, 240);
            Color headerBorder = Color.FromArgb(68, 68, 68);

            using (SolidBrush backBrush = new SolidBrush(headerBack))
                e.Graphics.FillRectangle(backBrush, e.Bounds);

            Rectangle textBounds = new Rectangle(
                e.Bounds.Left + 7.DpiZoom(),
                e.Bounds.Top,
                Math.Max(0, e.Bounds.Width - 14.DpiZoom()),
                e.Bounds.Height);

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (e.Header.TextAlign == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
            else if (e.Header.TextAlign == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
            else flags |= TextFormatFlags.Left;

            TextRenderer.DrawText(e.Graphics, e.Header.Text, processList.Font, textBounds, headerText, flags);

            using (Pen borderPen = new Pen(headerBorder))
            {
                e.Graphics.DrawLine(borderPen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        private string BuildTargetCaption()
        {
            if (targetPaths.Length == 1)
            {
                string name = Path.GetFileName(targetPaths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(name)) name = targetPaths[0];
                return "Processes using “" + name + "”";
            }
            return "Processes using " + targetPaths.Length + " selected items";
        }

        private async Task ReloadAsync()
        {
            if (scanning) return;
            scanning = true;
            ToggleActions(false);
            statusLabel.Text = "Scanning running processes...";
            detailsLabel.Text = string.Empty;

            try
            {
                FileLocksmithScanResult result = await Task.Run(() => FileLocksmithManager.Scan(targetPaths));
                Populate(result);
            }
            catch (Exception ex)
            {
                processList.Items.Clear();
                processImages.Images.Clear();
                statusLabel.Text = "Scan failed.";
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                scanning = false;
                ToggleActions(true);
                UpdateSelection();
            }
        }

        private void Populate(FileLocksmithScanResult result)
        {
            processList.BeginUpdate();
            processList.Items.Clear();
            processImages.Images.Clear();

            foreach (FileLocksmithProcessInfo process in result.Processes)
            {
                int imageIndex = -1;
                Image icon = TryLoadProcessIcon(process.ExecutablePath);
                if (icon != null)
                {
                    processImages.Images.Add(icon);
                    imageIndex = processImages.Images.Count - 1;
                }

                ListViewItem item = new ListViewItem(process.ApplicationName ?? ("Process " + process.ProcessId));
                if (imageIndex >= 0) item.ImageIndex = imageIndex;
                item.SubItems.Add(process.ProcessId.ToString());
                item.SubItems.Add(process.TypeName);
                item.SubItems.Add(string.IsNullOrWhiteSpace(process.UserName) ? "—" : process.UserName);
                item.SubItems.Add(string.IsNullOrWhiteSpace(process.ExecutablePath) ? "Unavailable" : process.ExecutablePath);
                item.Tag = process;
                processList.Items.Add(item);
            }
            processList.EndUpdate();

            if (result.Processes.Count == 0)
                statusLabel.Text = "No locking processes found.";
            else
                statusLabel.Text = result.Processes.Count + " locking process" + (result.Processes.Count == 1 ? string.Empty : "es") + " found.";

            if (result.DirectoryScanTruncated)
                statusLabel.Text += " Directory scan was limited to the first " + result.Resources.Count + " files.";
        }

        private static Image TryLoadProcessIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;
                    return icon.ToBitmap();
                }
            }
            catch { return null; }
        }

        private async Task EndSelectedTaskAsync()
        {
            FileLocksmithProcessInfo process = SelectedProcess;
            if (process == null || process.IsCritical) return;

            if (MessageBox.Show("End “" + process.ApplicationName + "” (PID " + process.ProcessId + ")?\n\nUnsaved data in that process can be lost.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                await Task.Run(() => FileLocksmithManager.KillProcess(process.ProcessId));
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + (FileLocksmithManager.IsAdministrator() ? string.Empty : "\n\nTry Restart as administrator."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleActions(bool enabled)
        {
            btnReload.Enabled = enabled;
            btnAdmin.Enabled = enabled;
            btnClose.Enabled = enabled;
            btnEndTask.Enabled = enabled && SelectedProcess != null && !SelectedProcess.IsCritical;
        }

        private void UpdateSelection()
        {
            FileLocksmithProcessInfo process = SelectedProcess;
            btnEndTask.Enabled = !scanning && process != null && !process.IsCritical;
            if (process == null)
            {
                detailsLabel.Text = "Select a process to see details.";
                return;
            }

            string detail = "PID: " + process.ProcessId + "   •   " + process.TypeName;
            if (!string.IsNullOrWhiteSpace(process.ServiceName)) detail += "   •   Service: " + process.ServiceName;
            if (process.Restartable) detail += "   •   Restartable";
            if (process.IsCritical) detail += "   •   Critical process (End task disabled)";
            if (!string.IsNullOrWhiteSpace(process.ExecutablePath)) detail += Environment.NewLine + process.ExecutablePath;
            detailsLabel.Text = detail;
        }

        private void RemoveExitedProcesses()
        {
            if (scanning || processList.Items.Count == 0) return;
            List<ListViewItem> remove = new List<ListViewItem>();
            foreach (ListViewItem item in processList.Items)
            {
                FileLocksmithProcessInfo process = item.Tag as FileLocksmithProcessInfo;
                if (process != null && !FileLocksmithManager.IsProcessAlive(process.ProcessId)) remove.Add(item);
            }
            if (remove.Count == 0) return;
            foreach (ListViewItem item in remove) processList.Items.Remove(item);
            statusLabel.Text = processList.Items.Count == 0 ? "No locking processes found." : processList.Items.Count + " locking processes found.";
            UpdateSelection();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                processTimer.Stop();
                processTimer.Dispose();
                processImages.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
