using BluePointLilac.Methods;
using ContextMenuManager.BluePointLilac.Methods;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BluePointLilac.Controls
{
    public sealed class MyToolBar : FlowLayoutPanel
    {
        public MyToolBar()
        {
            this.Height = 91.DpiZoom();
            this.WrapContents = false;
            this.AutoScroll = true; // All six tabs remain reachable on narrow Windows / larger text settings.
            this.Dock = DockStyle.Top;
            this.DoubleBuffered = true;
            bool isDark = ThemeManager.IsDarkMode();
            this.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
            this.ForeColor = isDark ? Color.White : Color.FromArgb(55, 55, 55);
        }

        private MyToolBarButton selectedButton;
        public MyToolBarButton SelectedButton
        {
            get => selectedButton;
            set
            {
                if(selectedButton == value) return;
                if(selectedButton != null)
                {
                    selectedButton.Opacity = 0;
                    selectedButton.Cursor = Cursors.Hand;
                }
                selectedButton = value;
                if(selectedButton != null)
                {
                    selectedButton.Opacity = 0.4F;
                    selectedButton.Cursor = Cursors.Default;
                }
                SelectedButtonChanged?.Invoke(this, null);
            }
        }

        public event EventHandler SelectedButtonChanged;

        public int SelectedIndex
        {
            get
            {
                if(SelectedButton == null) return -1;
                else return Controls.GetChildIndex(SelectedButton);
            }
            set
            {
                if(value < 0 || value >= this.Controls.Count) SelectedButton = null;
                else SelectedButton = (MyToolBarButton)Controls[value];
            }
        }

        public void AddButton(MyToolBarButton button)
        {
            this.SuspendLayout();
            button.Parent = this;
            button.Margin = new Padding(12, 4, 0, 0).DpiZoom();
            button.MouseDown += (sender, e) =>
            {
                if(e.Button == MouseButtons.Left && button.CanBeSelected) SelectedButton = button;
            };
            button.MouseEnter += (sender, e) =>
            {
                if(button != SelectedButton) button.Opacity = 0.2F;
            };
            button.MouseLeave += (sender, e) =>
            {
                if(button != SelectedButton) button.Opacity = 0;
            };
            this.ResumeLayout();
        }

        public void AddButtons(MyToolBarButton[] buttons)
        {
            int maxWidth = 124.DpiZoom();
            // Multiline captions prevent one long localization from making all six tabs too wide.
            Array.ForEach(buttons, button => maxWidth = Math.Max(maxWidth, Math.Min(124.DpiZoom(),
                button.Text.Split('\n').Max(line => TextRenderer.MeasureText(line, button.Font).Width + 10.DpiZoom()))));
            Array.ForEach(buttons, button => { button.Width = maxWidth; AddButton(button); });
        }
    }

    public sealed class MyToolBarButton : Panel
    {
        public MyToolBarButton(Image image, string text)
        {
            this.SuspendLayout();
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;
            this.Size = new Size(72, 81).DpiZoom();
            this.Controls.AddRange(new Control[] { picImage, lblText });
            lblText.Resize += (sender, e) => this.OnResize(null);
            picImage.Top = 4.DpiZoom();
            lblText.Top = 42.DpiZoom();
            lblText.Width = 116.DpiZoom();
            lblText.Height = 36.DpiZoom();
            lblText.TextAlign = ContentAlignment.TopCenter;
            lblText.SetEnabled(false);
            lblText.ForeColor = ThemeManager.IsDarkMode() ? Color.White : Color.FromArgb(55, 55, 55);
            this.Image = image;
            this.Text = text;
            this.ResumeLayout();
        }

        readonly PictureBox picImage = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(34, 34).DpiZoom(),
            BackColor = Color.Transparent,
            Enabled = false
        };

        readonly Label lblText = new Label
        {
            BackColor = Color.Transparent,
            Font = SystemFonts.MenuFont,
            ForeColor = Color.White,
            AutoSize = false,
        };

        public Image Image
        {
            get => picImage.Image;
            set => picImage.Image = value;
        }
        public new string Text
        {
            get => lblText.Text;
            set => lblText.Text = value;
        }
        public float Opacity
        {
            get => BackColor.A / 255f;
            set
            {
                Color accent = ThemeManager.GetSystemAccentColor();
                BackColor = Color.FromArgb((int)(value * 255), accent);
            }
        }
        public bool CanBeSelected { get; set; } = true;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            lblText.Left = (this.Width - lblText.Width) / 2;
            picImage.Left = (this.Width - picImage.Width) / 2;
        }
    }
}