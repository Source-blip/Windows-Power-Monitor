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
        private readonly Label _currentUnit;
        private readonly Label _powerCaption;
        private readonly Label _statusLine;
        private readonly Label _sourcePill;
        private readonly Label _summaryToday;
        private readonly Label _summaryMonth;
        private readonly Label _todayKWh;
        private readonly Label _monthKWh;
        private readonly Label _todayCost;
        private readonly Label _monthCost;
        private readonly Label _confidenceText;
        private readonly MeterBar _confidenceBar;
        private readonly Label _lastUpdate;
        private readonly Dictionary<string, ComponentRow> _componentRows = new Dictionary<string, ComponentRow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Label> _sourceLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Label> _chartSegments = new Dictionary<int, Label>();
        private readonly Dictionary<string, SidebarItem> _sidebarItems = new Dictionary<string, SidebarItem>(StringComparer.OrdinalIgnoreCase);
        private readonly ChartPanel _chart;
        private readonly Panel _settingsPanel;
        private QuickSettingItem _quickAutoStart;
        private QuickSettingItem _quickBubbleVisible;
        private QuickSettingItem _quickHighPowerAlert;
        private CheckBox _autoStart;
        private CheckBox _bubbleVisible;
        private CheckBox _highPowerAlert;
        private NumericUpDown _margin;
        private NumericUpDown _rate;
        private NumericUpDown _alertThreshold;
        private int _selectedChartHours = 12;
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
            Size = new Size(1180, 760);
            MinimumSize = new Size(1120, 720);
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(8, 12, 17);
            Icon = SystemIcons.Application;

            _root = new Panel();
            _root.Dock = DockStyle.Fill;
            _root.BackColor = Color.FromArgb(12, 17, 24);
            _root.Padding = new Padding(28);
            Controls.Add(_root);
            AttachWindowDrag(_root);

            Label logo = new Label();
            logo.Text = "⚡";
            logo.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            logo.ForeColor = Color.White;
            logo.BackColor = Color.FromArgb(39, 129, 255);
            logo.TextAlign = ContentAlignment.MiddleCenter;
            logo.Location = new Point(30, 28);
            logo.Size = new Size(38, 38);
            _root.Controls.Add(logo);
            AttachWindowDrag(logo);

            Label title = new Label();
            title.Text = "主机用电监控";
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(242, 246, 252);
            title.AutoSize = true;
            title.Location = new Point(78, 28);
            _root.Controls.Add(title);
            AttachWindowDrag(title);

            _statusLine = new Label();
            _statusLine.Text = "实时监控中 · Windows 10+";
            _statusLine.ForeColor = Color.FromArgb(156, 166, 178);
            _statusLine.AutoSize = false;
            _statusLine.TextAlign = ContentAlignment.MiddleLeft;
            _statusLine.Location = new Point(276, 36);
            _statusLine.Size = new Size(380, 22);
            _root.Controls.Add(_statusLine);
            AttachWindowDrag(_statusLine);

            RoundedPanel sidebar = new RoundedPanel();
            sidebar.BackColor = Color.FromArgb(17, 24, 33);
            sidebar.BorderColor = Color.FromArgb(36, 48, 62);
            sidebar.Radius = 10;
            sidebar.Location = new Point(18, 90);
            sidebar.Size = new Size(170, 636);
            _root.Controls.Add(sidebar);

            AddSidebarItem(sidebar, "overview", "⌂", "概览", true, 20, delegate { SelectSidebar("overview", false); });
            AddSidebarItem(sidebar, "trend", "⌁", "功耗趋势", false, 72, delegate { SelectSidebar("trend", false); });
            AddSidebarItem(sidebar, "components", "▣", "组件详情", false, 124, delegate { SelectSidebar("components", false); });
            AddSidebarItem(sidebar, "usage", "◷", "用电统计", false, 176, delegate { SelectSidebar("usage", false); });
            AddSidebarItem(sidebar, "cost", "$", "成本统计", false, 228, delegate { SelectSidebar("cost", false); });
            AddSidebarItem(sidebar, "settings", "⚙", "设置", false, 280, delegate { SelectSidebar("settings", true); });

            RoundedPanel health = new RoundedPanel();
            health.BackColor = Color.FromArgb(20, 28, 38);
            health.BorderColor = Color.FromArgb(41, 54, 68);
            health.Radius = 8;
            health.Location = new Point(18, 540);
            health.Size = new Size(134, 74);
            sidebar.Controls.Add(health);

            Label healthText = new Label();
            healthText.Text = "●  监控正常";
            healthText.ForeColor = Color.FromArgb(92, 220, 133);
            healthText.AutoSize = true;
            healthText.Location = new Point(14, 16);
            health.Controls.Add(healthText);

            _lastUpdate = new Label();
            _lastUpdate.Text = "上次更新：--";
            _lastUpdate.ForeColor = Color.FromArgb(175, 184, 194);
            _lastUpdate.AutoSize = true;
            _lastUpdate.Location = new Point(14, 44);
            health.Controls.Add(_lastUpdate);

            _settingsButton = new Label();
            _settingsButton.Text = "⚙";
            _settingsButton.BackColor = Color.FromArgb(21, 29, 39);
            _settingsButton.ForeColor = Color.FromArgb(226, 236, 248);
            _settingsButton.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            _settingsButton.Size = new Size(38, 34);
            _settingsButton.TextAlign = ContentAlignment.MiddleCenter;
            _settingsButton.BorderStyle = BorderStyle.FixedSingle;
            _settingsButton.Cursor = Cursors.Hand;
            _settingsButton.Click += delegate { ToggleSettingsPanel(); };
            _settingsButton.MouseEnter += delegate { _settingsButton.BackColor = Color.FromArgb(33, 46, 60); };
            _settingsButton.MouseLeave += delegate { _settingsButton.BackColor = Color.FromArgb(21, 29, 39); };
            _settingsButton.Visible = false;
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
            hero.BackColor = Color.FromArgb(18, 25, 34);
            hero.BorderColor = Color.FromArgb(42, 56, 70);
            hero.Radius = 10;
            hero.Location = new Point(210, 100);
            hero.Size = new Size(548, 220);
            hero.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _root.Controls.Add(hero);

            _powerCaption = new Label();
            _powerCaption.Text = "整机实时功耗";
            _powerCaption.ForeColor = Color.FromArgb(218, 226, 236);
            _powerCaption.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            _powerCaption.AutoSize = true;
            _powerCaption.Location = new Point(26, 22);
            hero.Controls.Add(_powerCaption);

            _sourcePill = new Label();
            _sourcePill.Text = "实时 + 估算";
            _sourcePill.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            _sourcePill.ForeColor = Color.FromArgb(221, 234, 248);
            _sourcePill.BackColor = Color.FromArgb(27, 36, 48);
            _sourcePill.TextAlign = ContentAlignment.MiddleCenter;
            _sourcePill.Location = new Point(402, 18);
            _sourcePill.Size = new Size(118, 32);
            hero.Controls.Add(_sourcePill);

            _currentPower = new Label();
            _currentPower.Text = "--";
            _currentPower.Font = new Font(Font.FontFamily, 48F, FontStyle.Bold);
            _currentPower.ForeColor = Color.FromArgb(245, 248, 252);
            _currentPower.AutoSize = false;
            _currentPower.TextAlign = ContentAlignment.MiddleLeft;
            _currentPower.Location = new Point(24, 56);
            _currentPower.Size = new Size(238, 72);
            hero.Controls.Add(_currentPower);

            _currentUnit = new Label();
            _currentUnit.Text = "W";
            _currentUnit.Font = new Font(Font.FontFamily, 32F, FontStyle.Bold);
            _currentUnit.ForeColor = Color.FromArgb(65, 151, 255);
            _currentUnit.AutoSize = true;
            _currentUnit.Location = new Point(244, 76);
            hero.Controls.Add(_currentUnit);

            BuildSourceStrip(hero, new Point(26, 142), new Size(496, 46));

            _confidenceText = new Label();
            _confidenceText.Text = "数据置信度  --";
            _confidenceText.ForeColor = Color.FromArgb(178, 188, 200);
            _confidenceText.AutoSize = true;
            _confidenceText.Location = new Point(28, 198);
            hero.Controls.Add(_confidenceText);

            _confidenceBar = new MeterBar();
            _confidenceBar.Location = new Point(150, 201);
            _confidenceBar.Size = new Size(180, 8);
            _confidenceBar.FillColor = Color.FromArgb(68, 151, 255);
            hero.Controls.Add(_confidenceBar);

            RoundedPanel summary = new RoundedPanel();
            summary.BackColor = Color.FromArgb(18, 25, 34);
            summary.BorderColor = Color.FromArgb(42, 56, 70);
            summary.Radius = 10;
            summary.Location = new Point(776, 100);
            summary.Size = new Size(366, 220);
            summary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _root.Controls.Add(summary);

            Label summaryTitle = new Label();
            summaryTitle.Text = "用电摘要";
            summaryTitle.ForeColor = Color.FromArgb(218, 226, 236);
            summaryTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            summaryTitle.AutoSize = true;
            summaryTitle.Location = new Point(24, 22);
            summary.Controls.Add(summaryTitle);

            _summaryToday = AddSummaryMetric(summary, "▣", "今日用电", "-- 度", Color.FromArgb(68, 151, 255), new Point(28, 64));
            _summaryMonth = AddSummaryMetric(summary, "▣", "本月用电", "-- 度", Color.FromArgb(51, 214, 112), new Point(196, 64));
            _todayKWh = _summaryToday;
            _monthKWh = _summaryMonth;
            _todayCost = AddSummaryMetric(summary, "$", "今日电费", "-- 元", Color.FromArgb(249, 204, 58), new Point(28, 146));
            _monthCost = AddSummaryMetric(summary, "▱", "本月电费", "-- 元", Color.FromArgb(166, 98, 255), new Point(196, 146));

            RoundedPanel chartCard = new RoundedPanel();
            chartCard.BackColor = Color.FromArgb(18, 25, 34);
            chartCard.BorderColor = Color.FromArgb(42, 56, 70);
            chartCard.Radius = 10;
            chartCard.Location = new Point(210, 334);
            chartCard.Size = new Size(932, 194);
            chartCard.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _root.Controls.Add(chartCard);

            Label chartTitle = new Label();
            chartTitle.Text = "今日功耗趋势";
            chartTitle.ForeColor = Color.FromArgb(218, 226, 236);
            chartTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            chartTitle.AutoSize = true;
            chartTitle.Location = new Point(22, 18);
            chartCard.Controls.Add(chartTitle);

            AddSegment(chartCard, "1小时", 1, 636);
            AddSegment(chartCard, "6小时", 6, 704);
            AddSegment(chartCard, "12小时", 12, 772);
            AddSegment(chartCard, "24小时", 24, 846);

            _chart = new ChartPanel();
            _chart.Location = new Point(18, 52);
            _chart.Size = new Size(896, 124);
            _chart.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            chartCard.Controls.Add(_chart);
            SelectChartRange(_selectedChartHours);

            RoundedPanel components = new RoundedPanel();
            components.BackColor = Color.FromArgb(18, 25, 34);
            components.BorderColor = Color.FromArgb(42, 56, 70);
            components.Radius = 10;
            components.Location = new Point(210, 540);
            components.Size = new Size(606, 186);
            _root.Controls.Add(components);

            Label compTitle = new Label();
            compTitle.Text = "组件功耗（当前）";
            compTitle.ForeColor = Color.FromArgb(218, 226, 236);
            compTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            compTitle.AutoSize = true;
            compTitle.Location = new Point(22, 18);
            components.Controls.Add(compTitle);

            _componentRows["CPU"] = CreateComponentRow(components, "▣", "CPU", Color.FromArgb(73, 171, 255), 48);
            _componentRows["GPU"] = CreateComponentRow(components, "▤", "GPU", Color.FromArgb(72, 218, 119), 76);
            _componentRows["Memory"] = CreateComponentRow(components, "▥", "内存", Color.FromArgb(255, 205, 68), 104);
            _componentRows["Storage"] = CreateComponentRow(components, "▧", "硬盘", Color.FromArgb(255, 205, 68), 132);
            _componentRows["Board"] = CreateComponentRow(components, "▦", "主板", Color.FromArgb(255, 205, 68), 160);

            RoundedPanel quick = new RoundedPanel();
            quick.BackColor = Color.FromArgb(18, 25, 34);
            quick.BorderColor = Color.FromArgb(42, 56, 70);
            quick.Radius = 10;
            quick.Location = new Point(834, 540);
            quick.Size = new Size(308, 186);
            _root.Controls.Add(quick);

            Label quickTitle = new Label();
            quickTitle.Text = "快捷设置";
            quickTitle.ForeColor = Color.FromArgb(218, 226, 236);
            quickTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            quickTitle.AutoSize = true;
            quickTitle.Location = new Point(20, 18);
            quick.Controls.Add(quickTitle);
            _quickAutoStart = AddQuickSetting(quick, "⏻", "开机启动", "随系统启动时自动运行", _settings.AutoStart, 50, ToggleAutoStart);
            _quickBubbleVisible = AddQuickSetting(quick, "◉", "气泡显示", "在桌面显示功耗气泡", _settings.BubbleVisible, 94, ToggleBubbleVisible);
            _quickHighPowerAlert = AddQuickSetting(quick, "⚠", "高功耗提醒", AlertSubtitle(), _settings.HighPowerAlert, 138, ToggleHighPowerAlert);

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
            SelectSidebar("settings", false);
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

            _currentPower.Text = FormatWattsNumber(sample.TotalWatts);
            LayoutPowerReadout();
            _statusLine.Text = "整机实时功耗 · 每 " + _settings.SampleSeconds.ToString(CultureInfo.InvariantCulture) +
                               " 秒刷新 · 补偿 " + _settings.MarginPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            _todayKWh.Text = sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _monthKWh.Text = sample.MonthKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _summaryToday.Text = sample.TodayKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _summaryMonth.Text = sample.MonthKWh.ToString("0.00", CultureInfo.InvariantCulture) + " 度";
            _todayCost.Text = "¥" + sample.TodayCost.ToString("0.00", CultureInfo.InvariantCulture);
            _monthCost.Text = "¥" + sample.MonthCost.ToString("0.00", CultureInfo.InvariantCulture);
            _lastUpdate.Text = "上次更新：" + sample.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            double confidence = EstimateConfidence(sample);
            _confidenceBar.ValuePercent = confidence;
            _confidenceText.Text = "数据置信度  " + (confidence >= 85 ? "高" : confidence >= 70 ? "中" : "基础") +
                                   "（" + confidence.ToString("0", CultureInfo.InvariantCulture) + "%）";
            _sourcePill.Text = confidence >= 85 ? "实时 + 估算" : "估算为主";
            UpdateComponentRows(sample);
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
            using (Pen pen = new Pen(Color.FromArgb(45, 57, 72)))
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
            {
                SelectSidebar("settings", false);
                _settingsPanel.BringToFront();
            }
            else
                SelectSidebar("overview", false);
        }

        private void SelectSidebar(string key, bool toggleSettings)
        {
            foreach (KeyValuePair<string, SidebarItem> item in _sidebarItems)
                item.Value.SetSelected(item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (toggleSettings)
            {
                _settingsPanel.Visible = !_settingsPanel.Visible;
                if (_settingsPanel.Visible)
                    _settingsPanel.BringToFront();
                else
                    SelectSidebar("overview", false);
                return;
            }

            if (!key.Equals("settings", StringComparison.OrdinalIgnoreCase))
                _settingsPanel.Visible = false;
        }

        private void LayoutPowerReadout()
        {
            using (Graphics graphics = CreateGraphics())
            {
                Font font = _currentPower.Font;
                float width = graphics.MeasureString(_currentPower.Text, font).Width;
                int x = Math.Max(168, Math.Min(398, _currentPower.Left + (int)Math.Ceiling(width) + 12));
                _currentUnit.Location = new Point(x, 77);
            }
        }

        private void ToggleAutoStart()
        {
            _settings.AutoStart = !_settings.AutoStart;
            SaveSettingsAndNotify(true);
        }

        private void ToggleBubbleVisible()
        {
            _settings.BubbleVisible = !_settings.BubbleVisible;
            SaveSettingsAndNotify(false);
        }

        private void ToggleHighPowerAlert()
        {
            _settings.HighPowerAlert = !_settings.HighPowerAlert;
            SaveSettingsAndNotify(false);
        }

        private void SaveSettingsAndNotify(bool applyAutoStart)
        {
            _settings.Save();
            if (applyAutoStart)
                _settings.ApplyAutoStart(Application.ExecutablePath);
            SyncSettingsUi();
            Action changed = SettingsChanged;
            if (changed != null)
                changed();
        }

        private void SyncSettingsUi()
        {
            _loading = true;
            if (_autoStart != null)
                _autoStart.Checked = _settings.AutoStart;
            if (_bubbleVisible != null)
                _bubbleVisible.Checked = _settings.BubbleVisible;
            if (_highPowerAlert != null)
                _highPowerAlert.Checked = _settings.HighPowerAlert;
            if (_margin != null)
                _margin.Value = SafeDecimal(_settings.MarginPercent, _margin.Minimum, _margin.Maximum);
            if (_rate != null)
                _rate.Value = SafeDecimal(_settings.ElectricityRate, _rate.Minimum, _rate.Maximum);
            if (_alertThreshold != null)
                _alertThreshold.Value = SafeDecimal(_settings.HighPowerThresholdWatts, _alertThreshold.Minimum, _alertThreshold.Maximum);
            _loading = false;

            if (_quickAutoStart != null)
                _quickAutoStart.SetChecked(_settings.AutoStart);
            if (_quickBubbleVisible != null)
                _quickBubbleVisible.SetChecked(_settings.BubbleVisible);
            if (_quickHighPowerAlert != null)
            {
                _quickHighPowerAlert.SetChecked(_settings.HighPowerAlert);
                _quickHighPowerAlert.Subtitle.Text = AlertSubtitle();
            }
        }

        private static decimal SafeDecimal(double value, decimal min, decimal max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                value = (double)min;
            decimal result = (decimal)value;
            if (result < min)
                return min;
            if (result > max)
                return max;
            return result;
        }

        private string AlertSubtitle()
        {
            return "超过 " + _settings.HighPowerThresholdWatts.ToString("0", CultureInfo.InvariantCulture) + " W 时提醒";
        }

        private void LayoutChrome()
        {
            _closeButton.Location = new Point(_root.ClientSize.Width - 58, 20);
            _maximizeButton.Location = new Point(_root.ClientSize.Width - 106, 20);
            _minimizeButton.Location = new Point(_root.ClientSize.Width - 154, 20);
            _settingsButton.Location = new Point(0, 0);
            _settingsPanel.Location = new Point(204, 112);
        }

        private Label CreateWindowButton(string text)
        {
            Label button = new Label();
            button.Text = text;
            button.ForeColor = Color.FromArgb(220, 228, 238);
            button.BackColor = Color.Transparent;
            button.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Size = new Size(42, 34);
            button.Cursor = Cursors.Hand;
            button.MouseEnter += delegate { button.BackColor = Color.FromArgb(33, 44, 57); };
            button.MouseLeave += delegate { button.BackColor = Color.Transparent; };
            return button;
        }

        private void AddSidebarItem(Control parent, string key, string icon, string text, bool selected, int y, Action click)
        {
            RoundedPanel item = new RoundedPanel();
            item.BackColor = selected ? Color.FromArgb(36, 48, 62) : Color.FromArgb(17, 24, 33);
            item.BorderColor = selected ? Color.FromArgb(47, 62, 79) : Color.FromArgb(17, 24, 33);
            item.Radius = 8;
            item.Location = new Point(14, y);
            item.Size = new Size(142, 42);
            item.Cursor = Cursors.Hand;
            parent.Controls.Add(item);

            Label iconLabel = new Label();
            iconLabel.Text = icon;
            iconLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            iconLabel.ForeColor = selected ? Color.FromArgb(79, 166, 255) : Color.FromArgb(150, 160, 172);
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            iconLabel.Location = new Point(10, 9);
            iconLabel.Size = new Size(22, 22);
            iconLabel.Cursor = Cursors.Hand;
            item.Controls.Add(iconLabel);

            Label label = new Label();
            label.Text = text;
            label.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            label.ForeColor = selected ? Color.FromArgb(235, 241, 248) : Color.FromArgb(176, 185, 196);
            label.AutoSize = true;
            label.Location = new Point(42, 12);
            label.Cursor = Cursors.Hand;
            item.Controls.Add(label);

            EventHandler handler = delegate
            {
                if (click != null)
                    click();
            };
            item.Click += handler;
            iconLabel.Click += handler;
            label.Click += handler;
            item.MouseEnter += delegate
            {
                if (!_sidebarItems.ContainsKey(key) || !_sidebarItems[key].Selected)
                    item.BackColor = Color.FromArgb(24, 34, 45);
            };
            item.MouseLeave += delegate
            {
                if (!_sidebarItems.ContainsKey(key) || !_sidebarItems[key].Selected)
                    item.BackColor = Color.FromArgb(17, 24, 33);
            };
            _sidebarItems[key] = new SidebarItem(item, iconLabel, label, selected);
        }

        private Label AddSummaryMetric(Control parent, string icon, string title, string value, Color accent, Point location)
        {
            Label iconLabel = new Label();
            iconLabel.Text = icon;
            iconLabel.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            iconLabel.ForeColor = accent;
            iconLabel.Location = location;
            iconLabel.Size = new Size(30, 28);
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            parent.Controls.Add(iconLabel);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(158, 168, 180);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(location.X + 42, location.Y);
            parent.Controls.Add(titleLabel);

            Label valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Font = new Font(Font.FontFamily, 17F, FontStyle.Bold);
            valueLabel.ForeColor = Color.FromArgb(238, 244, 250);
            valueLabel.AutoSize = false;
            valueLabel.Location = new Point(location.X + 42, location.Y + 24);
            valueLabel.Size = new Size(124, 32);
            parent.Controls.Add(valueLabel);
            return valueLabel;
        }

        private void AddSegment(Control parent, string text, int hours, int x)
        {
            Label segment = new Label();
            segment.Text = text;
            segment.Font = new Font(Font.FontFamily, 9F, FontStyle.Regular);
            segment.ForeColor = Color.FromArgb(164, 173, 184);
            segment.BackColor = Color.FromArgb(20, 28, 38);
            segment.BorderStyle = BorderStyle.FixedSingle;
            segment.TextAlign = ContentAlignment.MiddleCenter;
            segment.Location = new Point(x, 18);
            segment.Size = new Size(66, 28);
            segment.Cursor = Cursors.Hand;
            segment.Click += delegate { SelectChartRange(hours); };
            parent.Controls.Add(segment);
            _chartSegments[hours] = segment;
        }

        private void SelectChartRange(int hours)
        {
            _selectedChartHours = hours;
            if (_chart != null)
                _chart.ConfigureWindow(hours, Math.Max(1, _settings.SampleSeconds));
            foreach (KeyValuePair<int, Label> item in _chartSegments)
            {
                bool selected = item.Key == hours;
                item.Value.ForeColor = selected ? Color.FromArgb(93, 169, 255) : Color.FromArgb(164, 173, 184);
                item.Value.BackColor = selected ? Color.FromArgb(38, 51, 68) : Color.FromArgb(20, 28, 38);
            }
        }

        private void BuildSourceStrip(Control parent, Point location, Size size)
        {
            RoundedPanel strip = new RoundedPanel();
            strip.BackColor = Color.FromArgb(20, 28, 38);
            strip.BorderColor = Color.FromArgb(40, 53, 68);
            strip.Radius = 8;
            strip.Location = location;
            strip.Size = size;
            parent.Controls.Add(strip);

            AddSourceCell(strip, "CPU", "实时", Color.FromArgb(78, 219, 125), 10);
            AddSourceCell(strip, "GPU", "实时", Color.FromArgb(78, 219, 125), 108);
            AddSourceCell(strip, "内存", "估算", Color.FromArgb(255, 205, 68), 206);
            AddSourceCell(strip, "硬盘", "估算", Color.FromArgb(255, 205, 68), 304);
            AddSourceCell(strip, "主板", "估算", Color.FromArgb(255, 205, 68), 402);
        }

        private void AddSourceCell(Control parent, string title, string source, Color dot, int x)
        {
            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(223, 231, 240);
            titleLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.TopCenter;
            titleLabel.Location = new Point(x, 7);
            titleLabel.Size = new Size(78, 18);
            parent.Controls.Add(titleLabel);

            Label sourceLabel = new Label();
            sourceLabel.Text = source + "  ●";
            sourceLabel.ForeColor = dot;
            sourceLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular);
            sourceLabel.TextAlign = ContentAlignment.TopCenter;
            sourceLabel.Location = new Point(x, 26);
            sourceLabel.Size = new Size(78, 18);
            parent.Controls.Add(sourceLabel);
            _sourceLabels[title] = sourceLabel;
        }

        private ComponentRow CreateComponentRow(Control parent, string icon, string title, Color color, int y)
        {
            Label iconLabel = new Label();
            iconLabel.Text = icon;
            iconLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            iconLabel.ForeColor = color;
            iconLabel.Location = new Point(24, y);
            iconLabel.Size = new Size(24, 22);
            parent.Controls.Add(iconLabel);

            Label name = new Label();
            name.Text = title;
            name.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            name.ForeColor = Color.FromArgb(232, 239, 247);
            name.Location = new Point(58, y + 2);
            name.Size = new Size(68, 20);
            parent.Controls.Add(name);

            MeterBar bar = new MeterBar();
            bar.FillColor = color;
            bar.Location = new Point(138, y + 7);
            bar.Size = new Size(286, 8);
            parent.Controls.Add(bar);

            Label watts = new Label();
            watts.Text = "-- W";
            watts.ForeColor = Color.FromArgb(238, 244, 250);
            watts.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            watts.TextAlign = ContentAlignment.MiddleRight;
            watts.Location = new Point(430, y + 1);
            watts.Size = new Size(62, 20);
            parent.Controls.Add(watts);

            Label percent = new Label();
            percent.Text = "--";
            percent.ForeColor = color;
            percent.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            percent.TextAlign = ContentAlignment.MiddleRight;
            percent.Location = new Point(498, y + 1);
            percent.Size = new Size(48, 20);
            parent.Controls.Add(percent);

            Label source = new Label();
            source.Text = "--";
            source.ForeColor = Color.FromArgb(154, 164, 176);
            source.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular);
            source.Location = new Point(550, y + 2);
            source.Size = new Size(46, 18);
            parent.Controls.Add(source);

            return new ComponentRow(watts, percent, source, bar);
        }

        private QuickSettingItem AddQuickSetting(Control parent, string icon, string title, string subtitle, bool enabled, int y, Action click)
        {
            RoundedPanel row = new RoundedPanel();
            row.BackColor = Color.FromArgb(20, 28, 38);
            row.BorderColor = Color.FromArgb(39, 52, 66);
            row.Radius = 8;
            row.Location = new Point(18, y);
            row.Size = new Size(272, 38);
            parent.Controls.Add(row);

            Label iconLabel = new Label();
            iconLabel.Text = icon;
            iconLabel.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            iconLabel.ForeColor = enabled ? Color.FromArgb(82, 167, 255) : Color.FromArgb(255, 204, 76);
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            iconLabel.Location = new Point(8, 6);
            iconLabel.Size = new Size(28, 26);
            row.Controls.Add(iconLabel);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(232, 239, 247);
            titleLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(46, 4);
            row.Controls.Add(titleLabel);

            Label sub = new Label();
            sub.Text = subtitle;
            sub.ForeColor = Color.FromArgb(143, 153, 166);
            sub.Font = new Font(Font.FontFamily, 8F, FontStyle.Regular);
            sub.AutoSize = true;
            sub.Location = new Point(46, 21);
            row.Controls.Add(sub);

            ToggleIndicator toggle = new ToggleIndicator();
            toggle.Checked = enabled;
            toggle.Location = new Point(226, 10);
            row.Controls.Add(toggle);

            EventHandler handler = delegate
            {
                if (click != null)
                    click();
            };
            row.Click += handler;
            iconLabel.Click += handler;
            titleLabel.Click += handler;
            sub.Click += handler;
            toggle.Click += handler;
            row.Cursor = Cursors.Hand;
            iconLabel.Cursor = Cursors.Hand;
            titleLabel.Cursor = Cursors.Hand;
            sub.Cursor = Cursors.Hand;
            toggle.Cursor = Cursors.Hand;

            return new QuickSettingItem(iconLabel, sub, toggle);
        }

        private void UpdateComponentRows(PowerSample sample)
        {
            double total = sample.TotalBeforeMarginWatts > 1 ? sample.TotalBeforeMarginWatts : sample.TotalWatts;
            UpdateComponentRow("CPU", sample.CpuWatts, FindSource(sample, "CPU"), total);
            UpdateComponentRow("GPU", sample.GpuWatts, FindSource(sample, "GPU"), total);
            UpdateComponentRow("Memory", sample.MemoryWatts, FindSource(sample, "Memory"), total);
            UpdateComponentRow("Storage", sample.StorageWatts, FindSource(sample, "Storage"), total);
            UpdateComponentRow("Board", sample.PlatformWatts, FindSource(sample, "Board"), total);
            UpdateSourceStrip(sample);
        }

        private void UpdateComponentRow(string key, double watts, PowerSourceKind source, double total)
        {
            ComponentRow row;
            if (!_componentRows.TryGetValue(key, out row))
                return;
            double percent = total > 0 ? Math.Max(0, Math.Min(100, watts / total * 100.0)) : 0;
            row.Watts.Text = watts.ToString("0", CultureInfo.InvariantCulture) + " W";
            row.Percent.Text = percent.ToString("0", CultureInfo.InvariantCulture) + "%";
            row.Source.Text = SourceText(source);
            row.Source.ForeColor = SourceColor(source);
            row.Bar.ValuePercent = percent;
        }

        private void UpdateSourceStrip(PowerSample sample)
        {
            UpdateSourceLabel("CPU", FindSource(sample, "CPU"));
            UpdateSourceLabel("GPU", FindSource(sample, "GPU"));
            UpdateSourceLabel("内存", FindSource(sample, "Memory"));
            UpdateSourceLabel("硬盘", FindSource(sample, "Storage"));
            UpdateSourceLabel("主板", FindSource(sample, "Board"));
        }

        private void UpdateSourceLabel(string key, PowerSourceKind source)
        {
            Label label;
            if (!_sourceLabels.TryGetValue(key, out label))
                return;
            label.Text = SourceText(source) + "  ●";
            label.ForeColor = SourceColor(source);
        }

        private PowerSourceKind FindSource(PowerSample sample, string name)
        {
            if (sample == null || sample.Components == null)
                return PowerSourceKind.Estimated;
            foreach (ComponentPower component in sample.Components)
            {
                if (component != null && string.Equals(component.Name, name, StringComparison.OrdinalIgnoreCase))
                    return component.Source;
            }
            return PowerSourceKind.Estimated;
        }

        private static string SourceText(PowerSourceKind source)
        {
            if (source == PowerSourceKind.Measured)
                return "实时";
            if (source == PowerSourceKind.Defaulted)
                return "默认";
            if (source == PowerSourceKind.Unavailable)
                return "不可用";
            return "估算";
        }

        private static Color SourceColor(PowerSourceKind source)
        {
            if (source == PowerSourceKind.Measured)
                return Color.FromArgb(83, 222, 128);
            if (source == PowerSourceKind.Defaulted)
                return Color.FromArgb(255, 205, 68);
            if (source == PowerSourceKind.Unavailable)
                return Color.FromArgb(235, 99, 99);
            return Color.FromArgb(255, 205, 68);
        }

        private static double EstimateConfidence(PowerSample sample)
        {
            if (sample == null || sample.Components == null || sample.Components.Count == 0)
                return 62;
            bool cpuMeasured = false;
            bool gpuMeasured = false;
            foreach (ComponentPower component in sample.Components)
            {
                if (component.Source != PowerSourceKind.Measured)
                    continue;
                if (string.Equals(component.Name, "CPU", StringComparison.OrdinalIgnoreCase))
                    cpuMeasured = true;
                if (string.Equals(component.Name, "GPU", StringComparison.OrdinalIgnoreCase))
                    gpuMeasured = true;
            }
            if (cpuMeasured && gpuMeasured)
                return 92;
            if (cpuMeasured || gpuMeasured)
                return 78;
            return 64;
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
            panel.Height = 398;
            panel.BackColor = Color.FromArgb(18, 25, 34);
            panel.BorderColor = Color.FromArgb(52, 68, 84);
            panel.Radius = 10;
            panel.Padding = new Padding(16);

            Label title = new Label();
            title.Text = "设置";
            title.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(238, 244, 250);
            title.AutoSize = true;
            title.Location = new Point(18, 16);
            panel.Controls.Add(title);

            _loading = true;

            _autoStart = new CheckBox();
            _autoStart.Text = "开机自启";
            _autoStart.Checked = _settings.AutoStart;
            _autoStart.AutoSize = true;
            _autoStart.ForeColor = Color.FromArgb(220, 229, 239);
            _autoStart.Location = new Point(20, 58);
            panel.Controls.Add(_autoStart);

            _bubbleVisible = new CheckBox();
            _bubbleVisible.Text = "显示悬浮气泡";
            _bubbleVisible.Checked = _settings.BubbleVisible;
            _bubbleVisible.AutoSize = true;
            _bubbleVisible.ForeColor = Color.FromArgb(220, 229, 239);
            _bubbleVisible.Location = new Point(20, 90);
            panel.Controls.Add(_bubbleVisible);

            _highPowerAlert = new CheckBox();
            _highPowerAlert.Text = "高功耗提醒";
            _highPowerAlert.Checked = _settings.HighPowerAlert;
            _highPowerAlert.AutoSize = true;
            _highPowerAlert.ForeColor = Color.FromArgb(220, 229, 239);
            _highPowerAlert.Location = new Point(20, 122);
            panel.Controls.Add(_highPowerAlert);

            Label thresholdLabel = new Label();
            thresholdLabel.Text = "提醒阈值 W";
            thresholdLabel.ForeColor = Color.FromArgb(158, 168, 180);
            thresholdLabel.AutoSize = true;
            thresholdLabel.Location = new Point(20, 164);
            panel.Controls.Add(thresholdLabel);

            _alertThreshold = new NumericUpDown();
            _alertThreshold.Minimum = 50;
            _alertThreshold.Maximum = 2000;
            _alertThreshold.DecimalPlaces = 0;
            _alertThreshold.Increment = 10;
            _alertThreshold.Value = (decimal)_settings.HighPowerThresholdWatts;
            _alertThreshold.Location = new Point(145, 160);
            _alertThreshold.Width = 92;
            _alertThreshold.BackColor = Color.FromArgb(24, 34, 45);
            _alertThreshold.ForeColor = Color.FromArgb(236, 242, 248);
            panel.Controls.Add(_alertThreshold);

            Label marginLabel = new Label();
            marginLabel.Text = "补偿百分比";
            marginLabel.ForeColor = Color.FromArgb(158, 168, 180);
            marginLabel.AutoSize = true;
            marginLabel.Location = new Point(20, 204);
            panel.Controls.Add(marginLabel);

            _margin = new NumericUpDown();
            _margin.Minimum = 0;
            _margin.Maximum = 60;
            _margin.DecimalPlaces = 1;
            _margin.Increment = 1;
            _margin.Value = (decimal)_settings.MarginPercent;
            _margin.Location = new Point(145, 200);
            _margin.Width = 92;
            _margin.BackColor = Color.FromArgb(24, 34, 45);
            _margin.ForeColor = Color.FromArgb(236, 242, 248);
            panel.Controls.Add(_margin);

            Label rateLabel = new Label();
            rateLabel.Text = "电价 元/度";
            rateLabel.ForeColor = Color.FromArgb(158, 168, 180);
            rateLabel.AutoSize = true;
            rateLabel.Location = new Point(20, 244);
            panel.Controls.Add(rateLabel);

            _rate = new NumericUpDown();
            _rate.Minimum = 0;
            _rate.Maximum = 5;
            _rate.DecimalPlaces = 2;
            _rate.Increment = 0.01M;
            _rate.Value = (decimal)_settings.ElectricityRate;
            _rate.Location = new Point(145, 240);
            _rate.Width = 92;
            _rate.BackColor = Color.FromArgb(24, 34, 45);
            _rate.ForeColor = Color.FromArgb(236, 242, 248);
            panel.Controls.Add(_rate);

            Label note = new Label();
            note.Text = "设置默认隐藏。后台优先读取真实传感器，读不到的主机内部硬件按配件估算。";
            note.ForeColor = Color.FromArgb(146, 158, 172);
            note.Location = new Point(20, 286);
            note.Size = new Size(232, 52);
            panel.Controls.Add(note);

            Button close = new Button();
            close.Text = "收起";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderColor = Color.FromArgb(66, 84, 104);
            close.BackColor = Color.FromArgb(28, 39, 52);
            close.ForeColor = Color.FromArgb(228, 236, 246);
            close.Location = new Point(176, 352);
            close.Size = new Size(76, 28);
            close.Click += delegate { panel.Visible = false; };
            panel.Controls.Add(close);

            _autoStart.CheckedChanged += SettingsControlChanged;
            _bubbleVisible.CheckedChanged += SettingsControlChanged;
            _highPowerAlert.CheckedChanged += SettingsControlChanged;
            _alertThreshold.ValueChanged += SettingsControlChanged;
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
            _settings.HighPowerAlert = _highPowerAlert.Checked;
            _settings.HighPowerThresholdWatts = (double)_alertThreshold.Value;
            _settings.MarginPercent = (double)_margin.Value;
            _settings.ElectricityRate = (double)_rate.Value;
            SaveSettingsAndNotify(sender == _autoStart);
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

        private static string FormatWattsNumber(double watts)
        {
            if (watts >= 100)
                return watts.ToString("0", CultureInfo.InvariantCulture);
            return watts.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private sealed class ComponentRow
        {
            public readonly Label Watts;
            public readonly Label Percent;
            public readonly Label Source;
            public readonly MeterBar Bar;

            public ComponentRow(Label watts, Label percent, Label source, MeterBar bar)
            {
                Watts = watts;
                Percent = percent;
                Source = source;
                Bar = bar;
            }
        }

        private sealed class SidebarItem
        {
            private readonly RoundedPanel _panel;
            private readonly Label _icon;
            private readonly Label _label;

            public bool Selected;

            public SidebarItem(RoundedPanel panel, Label icon, Label label, bool selected)
            {
                _panel = panel;
                _icon = icon;
                _label = label;
                SetSelected(selected);
            }

            public void SetSelected(bool selected)
            {
                Selected = selected;
                _panel.BackColor = selected ? Color.FromArgb(36, 48, 62) : Color.FromArgb(17, 24, 33);
                _panel.BorderColor = selected ? Color.FromArgb(47, 62, 79) : Color.FromArgb(17, 24, 33);
                _icon.ForeColor = selected ? Color.FromArgb(79, 166, 255) : Color.FromArgb(150, 160, 172);
                _label.ForeColor = selected ? Color.FromArgb(235, 241, 248) : Color.FromArgb(176, 185, 196);
                _panel.Invalidate();
            }
        }

        private sealed class QuickSettingItem
        {
            private readonly Label _icon;
            public readonly Label Subtitle;
            private readonly ToggleIndicator _toggle;

            public QuickSettingItem(Label icon, Label subtitle, ToggleIndicator toggle)
            {
                _icon = icon;
                Subtitle = subtitle;
                _toggle = toggle;
            }

            public void SetChecked(bool value)
            {
                _toggle.Checked = value;
                _toggle.Invalidate();
                _icon.ForeColor = value ? Color.FromArgb(82, 167, 255) : Color.FromArgb(255, 204, 76);
            }
        }
    }

    public sealed class MeterBar : Panel
    {
        private double _valuePercent;

        public Color FillColor = Color.FromArgb(68, 151, 255);
        public Color TrackColor = Color.FromArgb(42, 51, 63);

        public double ValuePercent
        {
            get { return _valuePercent; }
            set
            {
                _valuePercent = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public MeterBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath track = RoundedPanel.CreateRoundPath(rect, Math.Max(2, Height / 2)))
            using (SolidBrush trackBrush = new SolidBrush(TrackColor))
                e.Graphics.FillPath(trackBrush, track);

            int fillWidth = Math.Max(0, (int)Math.Round((Width - 1) * ValuePercent / 100.0));
            if (fillWidth <= 0)
                return;
            Rectangle fillRect = new Rectangle(0, 0, fillWidth, Height - 1);
            using (GraphicsPath fill = RoundedPanel.CreateRoundPath(fillRect, Math.Max(2, Height / 2)))
            using (SolidBrush fillBrush = new SolidBrush(FillColor))
                e.Graphics.FillPath(fillBrush, fill);
        }
    }

    public sealed class ToggleIndicator : Control
    {
        public bool Checked;

        public ToggleIndicator()
        {
            Size = new Size(36, 18);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPanel.CreateRoundPath(rect, Height / 2))
            using (SolidBrush brush = new SolidBrush(Checked ? Color.FromArgb(69, 151, 255) : Color.FromArgb(67, 77, 88)))
                e.Graphics.FillPath(brush, path);
            int knob = Height - 6;
            int x = Checked ? Width - knob - 4 : 4;
            using (SolidBrush knobBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(knobBrush, x, 3, knob, knob);
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
            Size = new Size(318, 82);
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
            using (Font wattsFont = new Font("Microsoft YaHei UI", 30F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font unitFont = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font textFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point))
            {
                int requiredWidth = (int)Math.Ceiling(MeasureBubbleTextWidth(graphics, wattsFont, unitFont, textFont)) + 22;
                Rectangle work = Screen.FromPoint(Location).WorkingArea;
                int maxWidth = Math.Max(318, work.Width - 32);
                int nextWidth = Math.Max(318, Math.Min(maxWidth, requiredWidth));
                if (nextWidth == Width && Height == 82)
                    return;

                Size = new Size(nextWidth, 82);
                Location = ClampToWorkingArea(Location);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle pill = new Rectangle(10, 8, Width - 20, Height - 24);
            for (int i = 10; i >= 1; i--)
            {
                Rectangle shadowRect = new Rectangle(pill.X - i / 2, pill.Y + i + 2, pill.Width + i, pill.Height + i / 2);
                using (GraphicsPath shadowPath = RoundedPath(shadowRect, 18))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(7 + i * 4, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using (GraphicsPath pillPath = RoundedPath(pill, 18))
            using (LinearGradientBrush pillBrush = new LinearGradientBrush(pill, Color.FromArgb(45, 54, 65), Color.FromArgb(28, 34, 43), LinearGradientMode.Vertical))
            using (Pen border = new Pen(Color.FromArgb(91, 104, 120), 1.2F))
            using (Pen highlight = new Pen(Color.FromArgb(75, 255, 255, 255), 1F))
            {
                e.Graphics.FillPath(pillBrush, pillPath);
                e.Graphics.DrawPath(border, pillPath);
                e.Graphics.DrawLine(highlight, pill.Left + 20, pill.Top + 2, pill.Right - 20, pill.Top + 2);
            }

            DrawBubbleText(e.Graphics, pill);
        }

        private void DrawBubbleText(Graphics graphics, Rectangle pill)
        {
            using (Font wattsFont = FitWattsFont(graphics, pill))
            using (Font unitFont = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point))
            using (Font textFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush soft = new SolidBrush(Color.FromArgb(238, 243, 250)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(198, 207, 218)))
            {
                float x = pill.X + 28;
                float wattsY = pill.Y + (pill.Height - wattsFont.Height) / 2F - 2;
                graphics.DrawString(_wattsText, wattsFont, white, x, wattsY);
                x += graphics.MeasureString(_wattsText, wattsFont).Width + 8;

                float unitY = pill.Y + (pill.Height - unitFont.Height) / 2F + 4;
                graphics.DrawString("W", unitFont, soft, x, unitY);
                x += graphics.MeasureString("W", unitFont).Width + 14;

                graphics.DrawString("·", textFont, muted, x, pill.Y + (pill.Height - textFont.Height) / 2F + 1);
                x += graphics.MeasureString("·", textFont).Width + 12;

                graphics.DrawString(_todayText, textFont, soft, x, pill.Y + (pill.Height - textFont.Height) / 2F + 1);
            }
        }

        private float MeasureBubbleTextWidth(Graphics graphics, Font wattsFont, Font unitFont, Font textFont)
        {
            return 28 +
                   graphics.MeasureString(_wattsText, wattsFont).Width + 8 +
                   graphics.MeasureString("W", unitFont).Width + 14 +
                   graphics.MeasureString("·", textFont).Width + 12 +
                   graphics.MeasureString(_todayText, textFont).Width +
                   28;
        }

        private Font FitWattsFont(Graphics graphics, Rectangle pill)
        {
            float size = 30F;
            while (size > 22F)
            {
                using (Font testWatts = new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Point))
                using (Font testUnit = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point))
                using (Font testText = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point))
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
        private int _sampleCapacity = 60;
        private int _windowHours = 12;

        public ChartPanel()
        {
            BackColor = Color.FromArgb(18, 25, 34);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void Add(double value)
        {
            _values.Add(value);
            TrimToCapacity();
            Invalidate();
        }

        public void ConfigureWindow(int hours, int sampleSeconds)
        {
            _windowHours = Math.Max(1, Math.Min(24, hours));
            int seconds = Math.Max(1, sampleSeconds);
            _sampleCapacity = Math.Max(60, Math.Min(86400, _windowHours * 3600 / seconds));
            TrimToCapacity();
            Invalidate();
        }

        private void TrimToCapacity()
        {
            int remove = _values.Count - _sampleCapacity;
            if (remove > 0)
                _values.RemoveRange(0, remove);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(18, 25, 34));
            Rectangle plot = new Rectangle(44, 10, Math.Max(10, Width - 58), Math.Max(10, Height - 30));
            using (Pen grid = new Pen(Color.FromArgb(49, 60, 74)))
            using (Pen axis = new Pen(Color.FromArgb(76, 91, 108)))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(160, 170, 182)))
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
                string leftLabel = _windowHours == 1 ? "-1h" : "-" + _windowHours.ToString(CultureInfo.InvariantCulture) + "h";
                string midLabel = _windowHours == 1 ? "-30m" : "-" + (_windowHours / 2).ToString(CultureInfo.InvariantCulture) + "h";
                g.DrawString(leftLabel, Font, labelBrush, plot.Left, plot.Bottom + 5);
                g.DrawString(midLabel, Font, labelBrush, plot.Left + plot.Width / 2 - 18, plot.Bottom + 5);
                g.DrawString("现在", Font, labelBrush, plot.Right - 32, plot.Bottom + 5);
            }

            if (_values.Count < 2)
                return;

            double max = Math.Max(500, Math.Ceiling(_values.Max() / 100.0) * 100.0);
            double min = 0;

            int renderCount = Math.Min(_values.Count, Math.Max(2, plot.Width));
            PointF[] points = new PointF[renderCount];
            for (int i = 0; i < renderCount; i++)
            {
                int sourceIndex = renderCount == 1 ? 0 : (int)Math.Round((double)i * (_values.Count - 1) / Math.Max(1, renderCount - 1));
                float x = plot.Left + (float)i * plot.Width / Math.Max(1, renderCount - 1);
                float y = plot.Bottom - (float)((_values[sourceIndex] - min) / (max - min)) * plot.Height;
                points[i] = new PointF(x, y);
            }
            PointF[] fill = new PointF[points.Length + 2];
            points.CopyTo(fill, 0);
            fill[fill.Length - 2] = new PointF(points[points.Length - 1].X, plot.Bottom);
            fill[fill.Length - 1] = new PointF(points[0].X, plot.Bottom);
            using (LinearGradientBrush area = new LinearGradientBrush(plot, Color.FromArgb(95, 59, 147, 255), Color.FromArgb(12, 59, 147, 255), LinearGradientMode.Vertical))
                g.FillPolygon(area, fill);
            using (Pen line = new Pen(Color.FromArgb(72, 155, 255), 2.2F))
                g.DrawLines(line, points);
            PointF last = points[points.Length - 1];
            using (Pen marker = new Pen(Color.FromArgb(160, 210, 235, 255), 1F))
                g.DrawLine(marker, last.X, plot.Top, last.X, plot.Bottom);
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(75, 159, 255)))
                g.FillEllipse(dot, last.X - 4, last.Y - 4, 8, 8);
            using (Pen dotBorder = new Pen(Color.White, 1.2F))
                g.DrawEllipse(dotBorder, last.X - 4, last.Y - 4, 8, 8);
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
        private DateTime _lastHighPowerNotification = DateTime.MinValue;
        private bool _inHighPowerState;

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

            MaybeNotifyHighPower(sample);

            if (_mainForm != null && !_mainForm.IsDisposed)
                _mainForm.UpdateSample(sample);
            if (_bubbleForm != null && !_bubbleForm.IsDisposed)
                _bubbleForm.UpdateSample(sample);
        }

        private void MaybeNotifyHighPower(PowerSample sample)
        {
            if (sample == null)
                return;

            DateTime now = DateTime.Now;
            bool shouldNotify = HighPowerAlertPolicy.ShouldNotify(
                _settings.HighPowerAlert,
                sample.TotalWatts,
                _settings.HighPowerThresholdWatts,
                now,
                _lastHighPowerNotification,
                _inHighPowerState,
                10);

            _inHighPowerState = HighPowerAlertPolicy.IsHighPowerState(sample.TotalWatts, _settings.HighPowerThresholdWatts);

            if (!shouldNotify)
                return;

            _lastHighPowerNotification = now;
            try
            {
                _notifyIcon.BalloonTipTitle = "主机功耗偏高";
                _notifyIcon.BalloonTipText = "当前约 " + sample.TotalWatts.ToString("0", CultureInfo.InvariantCulture) +
                                             " W，超过提醒阈值 " + _settings.HighPowerThresholdWatts.ToString("0", CultureInfo.InvariantCulture) + " W。";
                _notifyIcon.ShowBalloonTip(3500);
            }
            catch
            {
            }
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
            _settings.ApplyAutoStart(Application.ExecutablePath);
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
