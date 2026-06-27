using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace RDPGuard
{
    public sealed class MainForm : Form
    {
        private const int MaxDisplayedBlockedIps = 5000;
        private readonly GuardService _service;
        private readonly StartupManager _startupManager = new StartupManager();
        private readonly bool _startInTray;
        private readonly EventWaitHandle _showWindowEvent;
        private NotifyIcon _notifyIcon;
        private bool _allowExit;
        private bool _disposed;
        private string _languageCode = "en";
        private ToolStripMenuItem _trayToggleItem;

        private Label _statusLabel;
        private Label _thresholdLabel;
        private Label _intervalLabel;
        private Label _languageLabel;
        private Label _whitelistLabel;
        private Label _blockedTitleLabel;
        private Label _logTitleLabel;
        private NumericUpDown _thresholdInput;
        private NumericUpDown _intervalInput;
        private ComboBox _languageCombo;
        private CheckBox _startupCheck;
        private TextBox _whitelistText;
        private Button _toggleButton;
        private Button _checkButton;
        private Button _saveButton;
        private Button _unblockButton;
        private ListView _blockedList;
        private ListBox _logList;
        private ToolStripMenuItem _trayOpenItem;
        private ToolStripMenuItem _trayHideItem;
        private ToolStripMenuItem _trayCheckItem;
        private ToolStripMenuItem _trayExitItem;

        public MainForm(bool startInTray, EventWaitHandle showWindowEvent)
        {
            _startInTray = startInTray;
            _showWindowEvent = showWindowEvent;
            _service = new GuardService(AppConfig.Load());
            _service.Log += (_, message) => Ui(() => AddLog(message));
            _service.ConfigChanged += (_, __) => Ui(RefreshFromConfig);
            _service.CheckCompleted += (_, args) => Ui(() => SetStatus(Localization.Format(_languageCode, "LastCheck", args.InspectedEvents, args.UniqueIps, args.BlockedIps)));

            Text = "RDP Guard";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(980, 680);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(246, 247, 249);
            ShowInTaskbar = !startInTray;
            WindowState = startInTray ? FormWindowState.Minimized : FormWindowState.Normal;

            BuildUi();
            BuildTray();
            RefreshFromConfig();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _service.Start(runStartupLookback: true);
            StartShowWindowListener();

            if (_startInTray)
            {
                BeginInvoke(new Action(HideToTray));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                var choice = ShowCloseChoiceDialog();
                if (choice == CloseChoice.MinimizeToTray)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }

                if (choice == CloseChoice.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                _allowExit = true;
            }

            ExitApplication();
            base.OnFormClosing(e);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            root.Controls.Add(header, 0, 0);

            var title = new Label
            {
                Text = "RDP Guard",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22F),
                ForeColor = Color.FromArgb(24, 31, 42),
                Anchor = AnchorStyles.Left
            };
            header.Controls.Add(title, 0, 0);

            _statusLabel = new Label
            {
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(73, 82, 96)
            };
            header.Controls.Add(_statusLabel, 1, 0);

            root.Controls.Add(BuildSettingsPanel(), 0, 1);

            _blockedList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _blockedList.Columns.Add("Engellenen IP", 180);
            _blockedList.Columns.Add("Tarih", 170);
            _blockedList.Columns.Add("Sayi", 70);
            _blockedList.Columns.Add("Kural", 430);
            _blockedList.SelectedIndexChanged += (_, __) => _unblockButton.Enabled = _blockedList.SelectedItems.Count > 0;
            root.Controls.Add(WrapSection(_blockedList, out _blockedTitleLabel), 0, 2);

            _logList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true
            };
            root.Controls.Add(WrapSection(_logList, out _logTitleLabel), 0, 3);
        }

        private Control BuildSettingsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 3
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(table);

            _thresholdLabel = LabelFor(string.Empty, DockStyle.Fill);
            table.Controls.Add(_thresholdLabel, 0, 0);
            _thresholdInput = new NumericUpDown { Minimum = AppConfig.MinFailureThreshold, Maximum = AppConfig.MaxFailureThreshold, Dock = DockStyle.Fill };
            table.Controls.Add(_thresholdInput, 1, 0);

            _intervalLabel = LabelFor(string.Empty, DockStyle.Fill);
            table.Controls.Add(_intervalLabel, 0, 1);
            _intervalInput = new NumericUpDown { Minimum = AppConfig.MinCheckIntervalMinutes, Maximum = AppConfig.MaxCheckIntervalMinutes, Dock = DockStyle.Fill };
            table.Controls.Add(_intervalInput, 1, 1);

            _startupCheck = new CheckBox { Dock = DockStyle.Fill };
            table.Controls.Add(_startupCheck, 2, 0);

            var languagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _languageLabel = LabelFor(string.Empty, DockStyle.Fill);
            _languageCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var language in Localization.Languages)
            {
                _languageCombo.Items.Add(language);
            }
            languagePanel.Controls.Add(_languageLabel, 0, 0);
            languagePanel.Controls.Add(_languageCombo, 1, 0);
            table.Controls.Add(languagePanel, 2, 1);

            _toggleButton = Button(string.Empty);
            _toggleButton.Click += (_, __) => ToggleMonitoring();
            table.Controls.Add(_toggleButton, 3, 0);

            _checkButton = Button(string.Empty);
            _checkButton.Click += (_, __) => ThreadPool.QueueUserWorkItem(___ => _service.CheckNow());
            table.Controls.Add(_checkButton, 4, 0);

            _saveButton = Button(string.Empty);
            _saveButton.Click += (_, __) => SaveSettings();
            table.Controls.Add(_saveButton, 3, 1);

            _unblockButton = Button(string.Empty);
            _unblockButton.Enabled = false;
            _unblockButton.Click += (_, __) => UnblockSelected();
            table.Controls.Add(_unblockButton, 4, 1);

            _whitelistText = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };
            _whitelistLabel = LabelFor(string.Empty, DockStyle.Top);
            table.Controls.Add(_whitelistLabel, 0, 2);
            table.SetColumnSpan(_whitelistText, 4);
            table.Controls.Add(_whitelistText, 1, 2);

            return panel;
        }

        private static Label LabelFor(string text, DockStyle dock)
        {
            return new Label
            {
                Text = text,
                Dock = dock,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(59, 67, 80)
            };
        }

        private static Button Button(string text)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 91, 186),
                ForeColor = Color.White,
                Margin = new Padding(6, 2, 0, 4),
                UseVisualStyleBackColor = false
            };
        }

        private static Control WrapSection(Control content, out Label label)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };

            label = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(38, 45, 57)
            };

            panel.Controls.Add(content);
            panel.Controls.Add(label);
            return panel;
        }

        private void BuildTray()
        {
            var menu = new ContextMenuStrip();
            _trayOpenItem = new ToolStripMenuItem(string.Empty, null, (_, __) => ShowFromTray());
            _trayHideItem = new ToolStripMenuItem(string.Empty, null, (_, __) => HideToTray());
            _trayCheckItem = new ToolStripMenuItem(string.Empty, null, (_, __) => ThreadPool.QueueUserWorkItem(___ => _service.CheckNow()));
            _trayToggleItem = new ToolStripMenuItem(string.Empty, null, (_, __) => ToggleMonitoring());
            _trayExitItem = new ToolStripMenuItem(string.Empty, null, (_, __) => ConfirmExitFromTray());
            menu.Items.Add(_trayOpenItem);
            menu.Items.Add(_trayHideItem);
            menu.Items.Add(_trayCheckItem);
            menu.Items.Add(_trayToggleItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_trayExitItem);

            _notifyIcon = new NotifyIcon
            {
                Text = "RDP Guard",
                Icon = SystemIcons.Shield,
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowFromTray();
                }
            };
            _notifyIcon.DoubleClick += (_, __) => ShowFromTray();
        }

        private void RefreshFromConfig()
        {
            var config = _service.GetConfigSnapshot();
            _languageCode = Localization.NormalizeLanguageCode(config.LanguageCode);
            ApplyLocalization();
            _thresholdInput.Value = Math.Min(_thresholdInput.Maximum, Math.Max(_thresholdInput.Minimum, config.FailureThreshold));
            _intervalInput.Value = Math.Min(_intervalInput.Maximum, Math.Max(_intervalInput.Minimum, config.CheckIntervalMinutes));
            _startupCheck.Checked = config.StartWithWindows || _startupManager.IsEnabled();
            _whitelistText.Text = string.Join(Environment.NewLine, config.Whitelist);
            SelectLanguage(config.LanguageCode);

            _toggleButton.Text = config.MonitorEnabled ? L("Stop") : L("Start");
            _toggleButton.BackColor = config.MonitorEnabled ? Color.FromArgb(163, 55, 55) : Color.FromArgb(31, 91, 186);
            SetStatus(config.MonitorEnabled ? L("Active") : L("Stopped"));
            _notifyIcon.Text = config.MonitorEnabled ? "RDP Guard - " + L("Active") : "RDP Guard - " + L("Stopped");
            if (_trayToggleItem != null)
            {
                _trayToggleItem.Text = config.MonitorEnabled ? L("StopProtection") : L("StartProtection");
            }

            _blockedList.BeginUpdate();
            _blockedList.Items.Clear();
            var totalBlockedIps = config.BlockedIps.Count;
            var displayedBlockedIps = 0;
            foreach (var item in config.BlockedIps.Take(MaxDisplayedBlockedIps))
            {
                var listItem = new ListViewItem(item.IpAddress);
                listItem.SubItems.Add(item.BlockedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                listItem.SubItems.Add(item.FailureCount.ToString());
                listItem.SubItems.Add(GetDisplayRuleName(item));
                _blockedList.Items.Add(listItem);
                displayedBlockedIps++;
            }
            _blockedList.EndUpdate();
            if (totalBlockedIps > displayedBlockedIps)
            {
                _blockedTitleLabel.Text = L("BlockedIps") + " (" + displayedBlockedIps + "/" + totalBlockedIps + ")";
            }

            _unblockButton.Enabled = _blockedList.SelectedItems.Count > 0;
        }

        private void SaveSettings()
        {
            try
            {
                var config = _service.GetConfigSnapshot();
                config.FailureThreshold = (int)_thresholdInput.Value;
                config.CheckIntervalMinutes = (int)_intervalInput.Value;
                config.StartWithWindows = _startupCheck.Checked;
                config.BlockOutbound = false;
                config.LanguageCode = GetSelectedLanguageCode();
                config.Whitelist = _whitelistText.Lines
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToList();

                var exePath = Assembly.GetExecutingAssembly().Location;
                _startupManager.SetEnabled(config.StartWithWindows, exePath);
                _languageCode = config.LanguageCode;
                _service.ApplyConfig(config);
                AddLog(L("SettingsSaved"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleMonitoring()
        {
            var current = _service.GetConfigSnapshot();
            if (current.MonitorEnabled)
            {
                _service.Stop();
                return;
            }

            current.MonitorEnabled = true;
            _service.ApplyConfig(current);
        }

        private void UnblockSelected()
        {
            if (_blockedList.SelectedItems.Count == 0)
            {
                return;
            }

            var ip = _blockedList.SelectedItems[0].Text;
            _service.Unblock(ip);
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            _notifyIcon.Visible = true;
        }

        private void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void AddLog(string line)
        {
            _logList.Items.Insert(0, line);
            while (_logList.Items.Count > 300)
            {
                _logList.Items.RemoveAt(_logList.Items.Count - 1);
            }
        }

        private void SetStatus(string text)
        {
            _statusLabel.Text = text + "  |  " + AppConfig.ConfigPath;
        }

        private void Ui(Action action)
        {
            if (_disposed || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch
                {
                }

                return;
            }

            action();
        }

        private void ConfirmExitFromTray()
        {
            var choice = ShowCloseChoiceDialog();
            if (choice != CloseChoice.CloseProgram)
            {
                if (choice == CloseChoice.MinimizeToTray)
                {
                    HideToTray();
                }

                return;
            }

            _allowExit = true;
            Close();
        }

        private void ExitApplication()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.Dispose();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void StartShowWindowListener()
        {
            if (_showWindowEvent == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (!_disposed)
                {
                    try
                    {
                        _showWindowEvent.WaitOne();
                        Ui(ShowFromTray);
                    }
                    catch
                    {
                        return;
                    }
                }
            });
        }

        private CloseChoice ShowCloseChoiceDialog()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "RDP Guard";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ControlBox = false;
                dialog.ClientSize = new Size(520, 150);
                dialog.Font = Font;
                dialog.BackColor = Color.White;
                dialog.ShowInTaskbar = false;

                var title = new Label
                {
                    Text = L("CloseTitle"),
                    Font = new Font("Segoe UI Semibold", 11F),
                    Location = new Point(18, 16),
                    Size = new Size(480, 24)
                };

                var message = new Label
                {
                    Text = L("CloseMessage"),
                    Location = new Point(18, 48),
                    Size = new Size(480, 42)
                };

                var minimizeButton = new Button
                {
                    Text = L("MinimizeToTray"),
                    Location = new Point(18, 100),
                    Size = new Size(260, 32),
                    DialogResult = DialogResult.Yes
                };

                var closeButton = new Button
                {
                    Text = L("CloseProgram"),
                    Location = new Point(288, 100),
                    Size = new Size(210, 32),
                    DialogResult = DialogResult.No
                };

                dialog.Controls.Add(title);
                dialog.Controls.Add(message);
                dialog.Controls.Add(minimizeButton);
                dialog.Controls.Add(closeButton);
                dialog.AcceptButton = minimizeButton;
                dialog.CancelButton = minimizeButton;

                var result = dialog.ShowDialog(this);
                if (result == DialogResult.Yes)
                {
                    return CloseChoice.MinimizeToTray;
                }

                if (result == DialogResult.No)
                {
                    return CloseChoice.CloseProgram;
                }

                return CloseChoice.Cancel;
            }
        }

        private static string GetDisplayRuleName(BlockedIpRecord item)
        {
            if (!string.IsNullOrWhiteSpace(item.RuleName))
            {
                return item.RuleName;
            }

            if (!string.IsNullOrWhiteSpace(item.InboundRuleName))
            {
                return item.InboundRuleName;
            }

            return item.OutboundRuleName ?? string.Empty;
        }

        private void ApplyLocalization()
        {
            Text = L("AppTitle");
            _thresholdLabel.Text = L("Threshold");
            _intervalLabel.Text = L("Interval");
            _languageLabel.Text = L("Language");
            _startupCheck.Text = L("StartWithWindows");
            _checkButton.Text = L("CheckNow");
            _saveButton.Text = L("Save");
            _unblockButton.Text = L("Unblock");
            _whitelistLabel.Text = L("Whitelist");
            _blockedTitleLabel.Text = L("BlockedIps");
            _logTitleLabel.Text = L("Log");

            if (_blockedList.Columns.Count >= 4)
            {
                _blockedList.Columns[0].Text = L("Ip");
                _blockedList.Columns[1].Text = L("Date");
                _blockedList.Columns[2].Text = L("Count");
                _blockedList.Columns[3].Text = L("Rule");
            }

            if (_trayOpenItem != null)
            {
                _trayOpenItem.Text = L("Open");
                _trayHideItem.Text = L("Hide");
                _trayCheckItem.Text = L("CheckNow");
                _trayExitItem.Text = L("Exit");
            }
        }

        private string L(string key)
        {
            return Localization.Text(_languageCode, key);
        }

        private void SelectLanguage(string code)
        {
            code = Localization.NormalizeLanguageCode(code);
            foreach (var item in _languageCombo.Items.OfType<LanguageOption>())
            {
                if (string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    _languageCombo.SelectedItem = item;
                    return;
                }
            }

            _languageCombo.SelectedIndex = 0;
        }

        private string GetSelectedLanguageCode()
        {
            return _languageCombo.SelectedItem is LanguageOption language
                ? Localization.NormalizeLanguageCode(language.Code)
                : "en";
        }

        private enum CloseChoice
        {
            Cancel,
            MinimizeToTray,
            CloseProgram
        }
    }
}
