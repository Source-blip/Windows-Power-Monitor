using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace HostPowerMonitor
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly HardwareInventory _inventory;
        private readonly Panel _root;
        private readonly Label _settingsButton;
        private readonly Label _minimizeButton;
        private readonly Label _maximizeButton;
        private readonly Label _closeButton;
        private readonly Label _currentPower;
        private readonly Label _powerCaption;
        private readonly Label _statusLine;
        private readonly Label _sourcePill;
        private readonly Label _summaryToday;
        private readonly Label _summaryMonth;
        private readonly Label _todayKWh;
        private readonly Label _monthKWh;
        private readonly Label _todayCost;
        private readonly Label _monthCost;
        private readonly ChartPanel _chart;
        private readonly Panel _settingsPanel;
        private CheckBox _autoStart;
        private CheckBox _bubbleVisible;
        private NumericUpDown _margin;
        private NumericUpDown _rate;
        private bool _loading;
        private bool _allowClose;
        private bool _windowDragging;
        private Point _dragStart;
        private Point _formStart;

        public event Action SettingsChanged;

        public MainForm(AppSettings settings, HardwareInventory inventory)
        {
            _settings = settings;
            _inventory = inventory;
            Text = "主机用电监控";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1040, 640);
            MinimumSize = new Size(980, 610);
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;
            Icon = SystemIcons.Application;

            _root = new Panel();
            _root.Dock = DockStyle.Fill;
            _root.BackColor = Color.FromArgb(247, 250, 253);
            _root.Padding = new Padding(28);
            Controls.Add(_root);
            AttachWindowDrag(_root);

            Label logo = new Label();
            logo.Text = "⚡";
            logo.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            logo.ForeColor = Color.White;
            logo.BackColor = Color.FromArgb(36, 132, 184);
            logo.TextAlign = ContentAlignment.MiddleCenter;
            logo.Location = new Point(34, 30);
            logo.Size = new Size(38, 38);
            _root.Controls.Add(logo);
            AttachWindowDrag(logo);

            Label title = new Label();
            title.Text = "主机用电监控";
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(22, 34, 45);
            title.AutoSize = true;
            title.Location = new Point(88, 28);
            _root.Controls.Add(title);
            AttachWindowDrag(title);

            _statusLine = new Label();
            _statusLine.Text = "后台自动识别硬件，前台只显示整机用电结果";
            _statusLine.ForeColor = Color.FromArgb(96, 108, 120);
            _statusLine.AutoSize = true;
            _statusLine.Location = new Point(90, 64);
            _root.Controls.Add(_statusLine);
            AttachWindowDrag(_statusLine);

            _settingsButton = new Label();
            _settingsButton.Text = "⚙";
            _settingsButton.BackColor = Color.White;
            _settingsButton.ForeColor = Color.FromArgb(35, 94, 140);
            _settingsButton.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            _settingsButton.Size = new Size(38, 34);
            _settingsButton.TextAlign = ContentAlignment.MiddleCenter;
            _settingsButton.BorderStyle = BorderStyle.FixedSingle;
            _settingsButton.Cursor = Cursors.Hand;
            _settingsButton.Click += delegate { ToggleSettingsPanel(); };
            _settingsButton.MouseEnter += delegate { _settingsButton.BackColor = Color.FromArgb(236, 246, 255); };
            _settingsButton.MouseLeave += delegate { _settingsButton.BackColor = Color.White; };
            _root.Controls.Add(_settingsButton);
            _settingsButton.BringToFront();
            ToolTip tooltip = new ToolTip();
            tooltip.SetToolTip(_settingsButton, "设置");

            _minimizeButton = CreateWindowButton("—");
            _minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            _root.Controls.Add(_minimizeButton);

            _maximizeButton = CreateWindowButton("□");
            _maximizeButton.Click += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            };
            _root.Controls.Add(_maximizeButton);

            _closeButton = CreateWindowButton("×");
            _closeButton.Click += delegate { Hide(); };
            _root.Controls.Add(_closeButton);

            RoundedPanel hero = new RoundedPanel();
            hero.BackColor = Color.White;
            hero.BorderColor = Color.FromArgb(214, 226, 237);
            hero.Radius = 10;
            hero.Location = new Point(34, 128);
            hero.Size = new Size(500, 166);
            hero.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _root.Controls.Add(hero);

            _powerCaption = new Label();
            _powerCaption.Text = "整机实时功耗";
            _powerCaption.ForeColor = Color.FromArgb(74, 105, 132);
            _powerCaption.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            _powerCaption.AutoSize = true;
            _powerCaption.Location = new Point(24, 18);
            hero.Controls.Add(_powerCaption);

            _sourcePill = new Label();
            _sourcePill.Text = "实时";
            _sourcePill.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            _sourcePill.ForeColor = Color.FromArgb(22, 102, 135);
            _sourcePill.BackColor = Color.FromArgb(229, 247, 247);
            _sourcePill.TextAlign = ContentAlignment.MiddleCenter;
            _sourcePill.Location = new Point(370, 18);
            _sourcePill.Size = new Size(104, 28);
            hero.Controls.Add(_sourcePill);

            _currentPower = new Label();
            _currentPower.Text = "-- W";
            _currentPower.Font = new Font(Font.FontFamily, 44F, FontStyle.Bold);
            _currentPower.ForeColor = Color.FromArgb(18, 79, 139);
            _currentPower.AutoSize = false;
            _currentPower.TextAlign = ContentAlignment.MiddleLeft;
            _currentPower.Location = new Point(22, 54);
            _currentPower.Size = new Size(420, 86);
            hero.Controls.Add(_currentPower);

            RoundedPanel summary = new RoundedPanel();
            summary.BackColor = Color.White;
            summary.BorderColor = Color.FromArgb(214, 226, 237);
            summary.Radius = 10;
            summary.Location = new Point(558, 128);
            summary.Size = new Size(412, 166);
            summary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _root.Controls.Add(summary);

            Label summaryTitle = new Label();
            summaryTitle.Text = "用电摘要";
            summaryTitle.ForeColor = Color.FromArgb(74, 105, 132);
            summaryTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            summaryTitle.AutoSize = true;
            summaryTitle.Location = new Point(24, 18);
            summary.Controls.Add(summaryTitle);

            Label todayLabel = new Label();
            todayLabel.Text = "今日";
            todayLabel.ForeColor = Color.FromArgb(96, 108, 120);
            todayLabel.AutoSize = true;
            todayLabel.Location = new Point(26, 62);
            summary.Controls.Add(todayLabel);

            _summaryToday = new Label();
            _summaryToday.Text = "-- 度";
            _summaryToday.Font = new Font(Font.FontFamily, 21F, FontStyle.Bold);
            _summaryToday.ForeColor = Color.FromArgb(18, 79, 139);
            _summaryToday.AutoSize = false;
            _summaryToday.Location = new Point(24, 84);
            _summaryToday.Size = new Size(150, 46);
            summary.Controls.Add(_summaryToday);

            Label monthLabel = new Label();
            monthLabel.Text = "本月";
            monthLabel.ForeColor = Color.FromArgb(96, 108, 120);
            monthLabel.AutoSize = true;
            monthLabel.Location = new Point(210, 62);
            summary.Controls.Add(monthLabel);

            _summaryMonth = new Label();
            _summaryMonth.Text = "-- 度";
            _summaryMonth.Font = new Font(Font.FontFamily, 21F, FontStyle.Bold);
            _summaryMonth.ForeColor = Color.FromArgb(24, 132, 96);
            _summaryMonth.AutoSize = false;
            _summaryMonth.Location = new Point(208, 84);
            _summaryMonth.Size = new Size(150, 46);
            summary.Controls.Add(_summaryMonth);

            FlowLayoutPanel cards = new FlowLayoutPanel();
            cards.Location = new Point(34, 322);
            cards.Size = new Size(936, 104);
            cards.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cards.WrapContents = false;
            cards.AutoScroll = false;
            _root.Controls.Add(cards);

            _todayKWh = AddMetricCard(cards, "今日用电", "-- 度", Color.FromArgb(43, 132, 181));
            _monthKWh = AddMetricCard(cards, "本月用电", "-- 度", Color.FromArgb(31, 139, 104));
            _todayCost = AddMetricCard(cards, "今日电费", "-- 元", Color.FromArgb(55, 98, 168));
            _monthCost = AddMetricCard(cards, "本月电费", "-- 元", Color.FromArgb(117, 93, 164));

            RoundedPanel chartCard = new RoundedPanel();
            chartCard.BackColor = Color.White;
            chartCard.BorderColor = Color.FromArgb(214, 226, 237);
            chartCard.Radius = 10;
            chartCard.Location = new Point(34, 456);
            chartCard.Size = new Size(936, 142);
            chartCard.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            _root.Controls.Add(chartCard);

            Label chartTitle = new Label();
            chartTitle.Text = "最近 60 次采样";
            chartTitle.ForeColor = Color.FromArgb(74, 105, 132);
            chartTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            chartTitle.AutoSize = true;
            chartTitle.Location = new Point(18, 13);
            chartCard.Controls.Add(chartTitle);

            _chart = new ChartPanel();
            _chart.Location = new Point(14, 38);
            _chart.Size = new Size(908, 88);
            _chart.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            chartCard.Controls.Add(_chart);

            _settingsPanel = BuildSettingsPanel();
            _settingsPanel.Visible = false;
            Controls.Add(_settingsPanel);
            _settingsPanel.BringToFront();

            Resize += delegate { LayoutChrome(); };
            Resize += delegate { ApplyRoundedWindow(); };
            LayoutChrome();
            ApplyRoundedWindow();
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        public void ShowSettingsPanel()
        {
            _settingsPanel.Visible = true;
            _settingsPanel.BringToFront();
        }

        public void UpdateSample(PowerSample sample)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<PowerSample>(UpdateSample), sample);
                return;
            }

            _currentPower.Text = FormatWatts(sample.TotalWatts);
            _statusLine.Text = "整机实时功耗 · 每 " + _settings.SampleSeconds.ToString(CultureInfo.InvariantCulture) +
                               " 秒刷新 · 补偿 " + _settings.MarginPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            _todayKWh.Text = sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _monthKWh.Text = sample.MonthKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _summaryToday.Text = sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _summaryMonth.Text = sample.MonthKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _todayCost.Text = "¥" + sample.TodayCost.ToString("0.00", CultureInfo.InvariantCulture);
            _monthCost.Text = "¥" + sample.MonthCost.ToString("0.00", CultureInfo.InvariantCulture);
            _chart.Add(sample.TotalWatts);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(203, 214, 224)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void ApplyRoundedWindow()
        {
            if (Width <= 0 || Height <= 0)
                return;
            using (GraphicsPath path = RoundedPanel.CreateRoundPath(new Rectangle(0, 0, Width, Height), 12))
                Region = new Region(path);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_settingsPanel != null)
                _settingsPanel.Visible = false;
        }

        private void ToggleSettingsPanel()
        {
            _settingsPanel.Visible = !_settingsPanel.Visible;
            if (_settingsPanel.Visible)
                _settingsPanel.BringToFront();
        }

        private void LayoutChrome()
        {
            _closeButton.Location = new Point(_root.ClientSize.Width - 58, 20);
            _maximizeButton.Location = new Point(_root.ClientSize.Width - 106, 20);
            _minimizeButton.Location = new Point(_root.ClientSize.Width - 154, 20);
            _settingsButton.Location = new Point(_root.ClientSize.Width - 82, 74);
            _settingsPanel.Location = new Point(Math.Max(12, ClientSize.Width - _settingsPanel.Width - 74), 118);
        }

        private Label CreateWindowButton(string text)
        {
            Label button = new Label();
            button.Text = text;
            button.ForeColor = Color.FromArgb(21, 28, 36);
            button.BackColor = Color.Transparent;
            button.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Size = new Size(42, 34);
            button.Cursor = Cursors.Hand;
            button.MouseEnter += delegate { button.BackColor = Color.FromArgb(236, 241, 247); };
            button.MouseLeave += delegate { button.BackColor = Color.Transparent; };
            return button;
        }

        private void AttachWindowDrag(Control control)
        {
            control.MouseDown += BeginWindowDrag;
            control.MouseMove += MoveWindowDrag;
            control.MouseUp += EndWindowDrag;
        }

        private void BeginWindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            _windowDragging = true;
            _dragStart = Cursor.Position;
            _formStart = Location;
        }

        private void MoveWindowDrag(object sender, MouseEventArgs e)
        {
            if (!_windowDragging)
                return;
            Point current = Cursor.Position;
            Location = new Point(_formStart.X + current.X - _dragStart.X, _formStart.Y + current.Y - _dragStart.Y);
        }

        private void EndWindowDrag(object sender, MouseEventArgs e)
        {
            _windowDragging = false;
        }

        private Panel BuildSettingsPanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Width = 282;
            panel.Height = 330;
            panel.BackColor = Color.White;
            panel.BorderColor = Color.FromArgb(204, 219, 232);
            panel.Radius = 10;
            panel.Padding = new Padding(16);

            Label title = new Label();
            title.Text = "设置";
            title.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(22, 34, 45);
            title.AutoSize = true;
            title.Location = new Point(18, 16);
            panel.Controls.Add(title);

            _loading = true;

            _autoStart = new CheckBox();
            _autoStart.Text = "开机自启";
            _autoStart.Checked = _settings.AutoStart;
            _autoStart.AutoSize = true;
            _autoStart.Location = new Point(20, 58);
            panel.Controls.Add(_autoStart);

            _bubbleVisible = new CheckBox();
            _bubbleVisible.Text = "显示悬浮气泡";
            _bubbleVisible.Checked = _settings.BubbleVisible;
            _bubbleVisible.AutoSize = true;
            _bubbleVisible.Location = new Point(20, 90);
            panel.Controls.Add(_bubbleVisible);

            Label marginLabel = new Label();
            marginLabel.Text = "补偿百分比";
            marginLabel.ForeColor = Color.FromArgb(82, 94, 106);
            marginLabel.AutoSize = true;
            marginLabel.Location = new Point(20, 132);
            panel.Controls.Add(marginLabel);

            _margin = new NumericUpDown();
            _margin.Minimum = 0;
            _margin.Maximum = 60;
            _margin.DecimalPlaces = 1;
            _margin.Increment = 1;
            _margin.Value = (decimal)_settings.MarginPercent;
            _margin.Location = new Point(145, 128);
            _margin.Width = 92;
            panel.Controls.Add(_margin);

            Label rateLabel = new Label();
            rateLabel.Text = "电价 元/度";
            rateLabel.ForeColor = Color.FromArgb(82, 94, 106);
            rateLabel.AutoSize = true;
            rateLabel.Location = new Point(20, 172);
            panel.Controls.Add(rateLabel);

            _rate = new NumericUpDown();
            _rate.Minimum = 0;
            _rate.Maximum = 5;
            _rate.DecimalPlaces = 2;
            _rate.Increment = 0.01M;
            _rate.Value = (decimal)_settings.ElectricityRate;
            _rate.Location = new Point(145, 168);
            _rate.Width = 92;
            panel.Controls.Add(_rate);

            Label note = new Label();
            note.Text = "设置默认隐藏。后台优先读取真实传感器，读不到的主机内部硬件按配件估算。";
            note.ForeColor = Color.FromArgb(98, 110, 122);
            note.Location = new Point(20, 216);
            note.Size = new Size(232, 52);
            panel.Controls.Add(note);

            Button close = new Button();
            close.Text = "收起";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderColor = Color.FromArgb(206, 218, 231);
            close.BackColor = Color.FromArgb(245, 249, 252);
            close.Location = new Point(176, 284);
            close.Size = new Size(76, 28);
            close.Click += delegate { panel.Visible = false; };
            panel.Controls.Add(close);

            _autoStart.CheckedChanged += SettingsControlChanged;
            _bubbleVisible.CheckedChanged += SettingsControlChanged;
            _margin.ValueChanged += SettingsControlChanged;
            _rate.ValueChanged += SettingsControlChanged;
            _loading = false;

            return panel;
        }

        private void SettingsControlChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            _settings.AutoStart = _autoStart.Checked;
            _settings.BubbleVisible = _bubbleVisible.Checked;
            _settings.MarginPercent = (double)_margin.Value;
            _settings.ElectricityRate = (double)_rate.Value;
            _settings.Save();
            _settings.ApplyAutoStart(Application.ExecutablePath);
            Action changed = SettingsChanged;
            if (changed != null)
                changed();
        }

        private static Label AddMetricCard(FlowLayoutPanel parent, string title, string value, Color accent)
        {
            RoundedPanel card = new RoundedPanel();
            card.BackColor = Color.White;
            card.BorderColor = Color.FromArgb(214, 226, 237);
            card.Radius = 10;
            card.Margin = new Padding(0, 0, 14, 0);
            card.Size = new Size(214, 94);

            Panel accentBar = new Panel();
            accentBar.BackColor = accent;
            accentBar.Location = new Point(0, 0);
            accentBar.Size = new Size(4, card.Height);
            accentBar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            card.Controls.Add(accentBar);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(96, 108, 120);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(18, 16);
            card.Controls.Add(titleLabel);

            Label valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Font = new Font(parent.Font.FontFamily, 16F, FontStyle.Bold);
            valueLabel.ForeColor = Color.FromArgb(22, 34, 45);
            valueLabel.AutoSize = false;
            valueLabel.Location = new Point(18, 42);
            valueLabel.Size = new Size(176, 32);
            card.Controls.Add(valueLabel);

            parent.Controls.Add(card);
            return valueLabel;
        }

        private static string FormatWatts(double watts)
        {
            if (watts >= 100)
                return watts.ToString("0", CultureInfo.InvariantCulture) + " W";
            return watts.ToString("0.0", CultureInfo.InvariantCulture) + " W";
        }
    }

    public sealed class BubbleForm : Form
    {
        private readonly AppSettings _settings;
        private string _wattsText = "--";
        private string _todayText = "今日 -- 度";
        private bool _dragging;
        private bool _dragMoved;
        private Point _dragStart;
        private Point _formStart;
        private readonly Color _transparentBackColor = Color.FromArgb(1, 1, 1);

        public event Action BubbleClicked;
        public event Action PositionChangedByUser;

        public BubbleForm(AppSettings settings)
        {
            _settings = settings;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(238, 58);
            BackColor = _transparentBackColor;
            TransparencyKey = _transparentBackColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            MouseDown += BeginDrag;
            MouseMove += DragMove;
            MouseUp += EndDrag;
            Click += OnClickOpen;

            SetInitialLocation();
        }

        public void UpdateSample(PowerSample sample)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<PowerSample>(UpdateSample), sample);
                return;
            }
            _wattsText = sample.TotalWatts.ToString("0", CultureInfo.InvariantCulture);
            _todayText = "今日 " + sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            ResizeToFitContent();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void SetInitialLocation()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int x = _settings.BubbleX >= 0 ? _settings.BubbleX : work.Right - Width - 28;
            int y = _settings.BubbleY >= 0 ? _settings.BubbleY : work.Top + 120;
            Location = ClampToWorkingArea(new Point(x, y));
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            _dragging = true;
            _dragMoved = false;
            _dragStart = Cursor.Position;
            _formStart = Location;
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging)
                return;
            Point current = Cursor.Position;
            if (Math.Abs(current.X - _dragStart.X) > 3 || Math.Abs(current.Y - _dragStart.Y) > 3)
                _dragMoved = true;
            Point next = new Point(_formStart.X + current.X - _dragStart.X, _formStart.Y + current.Y - _dragStart.Y);
            Location = ClampToWorkingArea(next);
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            if (!_dragging)
                return;
            _dragging = false;
            _settings.BubbleX = Location.X;
            _settings.BubbleY = Location.Y;
            _settings.Save();
            Action changed = PositionChangedByUser;
            if (changed != null)
                changed();
        }

        private void OnClickOpen(object sender, EventArgs e)
        {
            if (_dragging || _dragMoved)
            {
                _dragMoved = false;
                return;
            }
            Action clicked = BubbleClicked;
            if (clicked != null)
                clicked();
        }

        private Point ClampToWorkingArea(Point point)
        {
            Screen screen = Screen.FromPoint(point);
            Rectangle work = screen.WorkingArea;
            int x = Math.Max(work.Left, Math.Min(work.Right - Width, point.X));
            int y = Math.Max(work.Top, Math.Min(work.Bottom - Height, point.Y));
            return new Point(x, y);
        }

        private void ResizeToFitContent()
        {
            if (IsDisposed)
                return;

            using (Graphics graphics = CreateGraphics())
            using (Font wattsFont = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font unitFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font textFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point))
            {
                int requiredWidth = (int)Math.Ceiling(MeasureBubbleTextWidth(graphics, wattsFont, unitFont, textFont)) + 16;
                Rectangle work = Screen.FromPoint(Location).WorkingArea;
                int maxWidth = Math.Max(238, work.Width - 32);
                int nextWidth = Math.Max(238, Math.Min(maxWidth, requiredWidth));
                if (nextWidth == Width && Height == 58)
                    return;

                Size = new Size(nextWidth, 58);
                Location = ClampToWorkingArea(Location);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle pill = new Rectangle(8, 8, Width - 16, Height - 17);
            for (int i = 5; i >= 1; i--)
            {
                Rectangle shadowRect = new Rectangle(pill.X - i / 2, pill.Y + i, pill.Width + i, pill.Height + i / 2);
                using (GraphicsPath shadowPath = RoundedPath(shadowRect, 11))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(10 + i * 7, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using (GraphicsPath pillPath = RoundedPath(pill, 11))
            using (SolidBrush pillBrush = new SolidBrush(Color.FromArgb(45, 50, 57)))
                e.Graphics.FillPath(pillBrush, pillPath);

            DrawBubbleText(e.Graphics, pill);
        }

        private void DrawBubbleText(Graphics graphics, Rectangle pill)
        {
            using (Font wattsFont = FitWattsFont(graphics, pill))
            using (Font unitFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font textFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush soft = new SolidBrush(Color.FromArgb(224, 229, 235)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(190, 197, 205)))
            {
                float x = pill.X + 18;
                float wattsY = pill.Y + (pill.Height - wattsFont.Height) / 2F - 1;
                graphics.DrawString(_wattsText, wattsFont, white, x, wattsY);
                x += graphics.MeasureString(_wattsText, wattsFont).Width + 5;

                float unitY = pill.Y + (pill.Height - unitFont.Height) / 2F + 2;
                graphics.DrawString("W", unitFont, soft, x, unitY);
                x += graphics.MeasureString("W", unitFont).Width + 9;

                graphics.DrawString("·", textFont, muted, x, pill.Y + (pill.Height - textFont.Height) / 2F + 1);
                x += graphics.MeasureString("·", textFont).Width + 8;

                graphics.DrawString(_todayText, textFont, soft, x, pill.Y + (pill.Height - textFont.Height) / 2F + 1);
            }
        }

        private float MeasureBubbleTextWidth(Graphics graphics, Font wattsFont, Font unitFont, Font textFont)
        {
            return 18 +
                   graphics.MeasureString(_wattsText, wattsFont).Width + 5 +
                   graphics.MeasureString("W", unitFont).Width + 9 +
                   graphics.MeasureString("·", textFont).Width + 8 +
                   graphics.MeasureString(_todayText, textFont).Width +
                   18;
        }

        private Font FitWattsFont(Graphics graphics, Rectangle pill)
        {
            float size = 22F;
            while (size > 17F)
            {
                using (Font testWatts = new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Point))
                using (Font testUnit = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point))
                using (Font testText = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point))
                {
                    float width = MeasureBubbleTextWidth(graphics, testWatts, testUnit, testText);
                    if (width <= pill.Width)
                        break;
                }
                size -= 1F;
            }
            return new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Point);
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class ChartPanel : Panel
    {
        private readonly List<double> _values = new List<double>();

        public ChartPanel()
        {
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void Add(double value)
        {
            _values.Add(value);
            while (_values.Count > 60)
                _values.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            Rectangle plot = new Rectangle(44, 10, Math.Max(10, Width - 58), Math.Max(10, Height - 34));
            using (Pen grid = new Pen(Color.FromArgb(224, 231, 238)))
            using (Pen axis = new Pen(Color.FromArgb(175, 187, 198)))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(81, 91, 102)))
            {
                for (int i = 0; i <= 5; i++)
                {
                    int y = plot.Top + i * plot.Height / 5;
                    int watts = 500 - i * 100;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    g.DrawString(watts.ToString(CultureInfo.InvariantCulture), Font, labelBrush, 2, y - 8);
                }
                g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
                g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                g.DrawString("W", Font, labelBrush, 2, plot.Bottom + 5);
                g.DrawString("60", Font, labelBrush, plot.Left, plot.Bottom + 5);
                g.DrawString("30", Font, labelBrush, plot.Left + plot.Width / 2 - 10, plot.Bottom + 5);
                g.DrawString("0 (次)", Font, labelBrush, plot.Right - 42, plot.Bottom + 5);
            }

            if (_values.Count < 2)
                return;

            double max = Math.Max(500, Math.Ceiling(_values.Max() / 100.0) * 100.0);
            double min = 0;

            PointF[] points = new PointF[_values.Count];
            for (int i = 0; i < _values.Count; i++)
            {
                float x = plot.Left + (float)i * plot.Width / Math.Max(1, _values.Count - 1);
                float y = plot.Bottom - (float)((_values[i] - min) / (max - min)) * plot.Height;
                points[i] = new PointF(x, y);
            }
            PointF[] fill = new PointF[points.Length + 2];
            points.CopyTo(fill, 0);
            fill[fill.Length - 2] = new PointF(points[points.Length - 1].X, plot.Bottom);
            fill[fill.Length - 1] = new PointF(points[0].X, plot.Bottom);
            using (SolidBrush area = new SolidBrush(Color.FromArgb(34, 39, 126, 218)))
                g.FillPolygon(area, fill);
            using (Pen line = new Pen(Color.FromArgb(39, 138, 191), 2.2F))
                g.DrawLines(line, points);
        }
    }

    public sealed class RoundedPanel : Panel
    {
        public int Radius = 8;
        public Color BorderColor = Color.FromArgb(214, 226, 237);

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Rectangle shadowRect = new Rectangle(2, 3, Width - 5, Height - 5);
            using (GraphicsPath shadow = CreateRoundPath(shadowRect, Radius))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(20, 30, 48, 68)))
                e.Graphics.FillPath(shadowBrush, shadow);
            using (GraphicsPath path = CreateRoundPath(rect, Radius))
            using (SolidBrush brush = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        public static GraphicsPath CreateRoundPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class TrayAppContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly HardwareInventory _inventory;
        private readonly PowerEstimator _estimator;
        private readonly HistoryStore _history;
        private readonly MonitorService _monitor;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly ShowMessageWindow _messageWindow;
        private MainForm _mainForm;
        private BubbleForm _bubbleForm;
        private PowerSample _lastSample;

        public TrayAppContext(string exePath, bool showMainOnStart)
        {
            _settings = AppSettings.Load();
            _settings.Save();
            _settings.ApplyAutoStart(exePath);

            _inventory = HardwareDetector.Detect();
            _estimator = new PowerEstimator(_inventory, _settings);
            _history = new HistoryStore(_settings.HistoryRetentionDays);
            _monitor = new MonitorService(_estimator, _history, _settings);
            _monitor.SampleReady += OnSampleReady;
            _messageWindow = new ShowMessageWindow(delegate { ShowMainForm(); });

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开面板", null, delegate { ShowMainForm(); });
            menu.Items.Add("显示/隐藏气泡", null, delegate { ToggleBubble(); });
            _pauseItem = new ToolStripMenuItem("暂停监控", null, delegate { ToggleMonitor(); });
            menu.Items.Add(_pauseItem);
            menu.Items.Add("设置", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApp(); });

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "主机用电监控";
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += delegate { ShowMainForm(); };

            if (_settings.BubbleVisible)
                ShowBubble();
            _monitor.Start();
            if (showMainOnStart)
                ShowMainForm();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monitor.Dispose();
                _history.Flush();
                _estimator.Dispose();
                if (_notifyIcon != null)
                    _notifyIcon.Dispose();
                if (_messageWindow != null)
                    _messageWindow.Dispose();
                if (_bubbleForm != null)
                    _bubbleForm.Dispose();
                if (_mainForm != null)
                    _mainForm.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnSampleReady(PowerSample sample)
        {
            _lastSample = sample;
            string text = "主机 " + sample.TotalWatts.ToString("0", CultureInfo.InvariantCulture) +
                          " W · 今日 " + sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            try
            {
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }
            catch
            {
            }

            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.UpdateSample(sample);
            if (_bubbleForm != null && !_bubbleForm.IsDisposed)
                _bubbleForm.UpdateSample(sample);
        }

        private void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_settings, _inventory);
                _mainForm.SettingsChanged += ApplySettingsFromUi;
            }
            if (_lastSample != null)
                _mainForm.UpdateSample(_lastSample);
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        private void ShowSettings()
        {
            ShowMainForm();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.ShowSettingsPanel();
        }

        private void ApplySettingsFromUi()
        {
            if (_settings.BubbleVisible)
                ShowBubble();
            else
                HideBubble();
        }

        private void ToggleBubble()
        {
            _settings.BubbleVisible = !_settings.BubbleVisible;
            _settings.Save();
            if (_settings.BubbleVisible)
                ShowBubble();
            else
                HideBubble();
        }

        private void ShowBubble()
        {
            if (_bubbleForm == null || _bubbleForm.IsDisposed)
            {
                _bubbleForm = new BubbleForm(_settings);
                _bubbleForm.BubbleClicked += delegate { ShowMainForm(); };
            }
            if (_lastSample != null)
                _bubbleForm.UpdateSample(_lastSample);
            _bubbleForm.Show();
        }

        private void HideBubble()
        {
            if (_bubbleForm != null && !_bubbleForm.IsDisposed)
                _bubbleForm.Hide();
        }

        private void ToggleMonitor()
        {
            if (_monitor.IsRunning)
            {
                _monitor.Stop();
                _pauseItem.Text = "继续监控";
            }
            else
            {
                _monitor.Start();
                _pauseItem.Text = "暂停监控";
            }
        }

        private void ExitApp()
        {
            _monitor.Stop();
            _settings.Save();
            _history.Flush();
            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.AllowClose();
            _notifyIcon.Visible = false;
            ExitThread();
        }

        private sealed class ShowMessageWindow : NativeWindow, IDisposable
        {
            private readonly Action _showMain;

            public ShowMessageWindow(Action showMain)
            {
                _showMain = showMain;
                CreateParams cp = new CreateParams();
                cp.Caption = "HostPowerMonitor.MessageWindow";
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == SingleInstanceMessenger.ShowMainMessage)
                {
                    if (_showMain != null)
                        _showMain();
                    return;
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                try
                {
                    DestroyHandle();
                }
                catch
                {
                }
            }
        }
    }
}
