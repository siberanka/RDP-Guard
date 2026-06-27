using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RDPGuard
{
    public sealed class MainForm : Form
    {
        private const int MaxDisplayedBlockedIps = 5000;
        private readonly object _updateSync = new object();
        private readonly GuardService _service;
        private readonly StartupManager _startupManager = new StartupManager();
        private readonly bool _startInTray;
        private readonly EventWaitHandle _showWindowEvent;
        private NotifyIcon _notifyIcon;
        private bool _allowExit;
        private bool _disposed;
        private bool _updateCheckRunning;
        private string _languageCode = "en";
        private string _themeMode = "system";
        private ThemePalette _theme;
        private ToolStripMenuItem _trayToggleItem;

        private Label _titleLabel;
        private Label _statusLabel;
        private Label _versionLabel;
        private Label _updateStatusLabel;
        private Label _thresholdLabel;
        private Label _intervalLabel;
        private Label _languageLabel;
        private Label _themeLabel;
        private Label _whitelistLabel;
        private Label _blockedTitleLabel;
        private Label _logTitleLabel;
        private Panel _settingsPanel;
        private NumericUpDown _thresholdInput;
        private NumericUpDown _intervalInput;
        private ComboBox _languageCombo;
        private ComboBox _themeCombo;
        private CheckBox _startupCheck;
        private TextBox _whitelistText;
        private Button _toggleButton;
        private Button _checkButton;
        private Button _updateButton;
        private Button _saveButton;
        private Button _unblockButton;
        private Button _clearLogButton;
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

            var initialConfig = _service.GetConfigSnapshot();
            _languageCode = Localization.NormalizeLanguageCode(initialConfig.LanguageCode);
            _themeMode = NormalizeThemeMode(initialConfig.ThemeMode);
            _theme = ResolveTheme(_themeMode);
            Text = "RDP Guard";
            Icon = LoadApplicationIcon();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            Size = new Size(1060, 740);
            Font = new Font("Segoe UI", 9F);
            BackColor = _theme.WindowBack;
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

            BeginInvoke(new Action(() => CheckForUpdates(showDialog: false)));
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
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            root.Controls.Add(header, 0, 0);

            var iconBox = new PictureBox
            {
                Image = Icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 12, 8)
            };
            header.Controls.Add(iconBox, 0, 0);

            var titlePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0) };
            titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            _titleLabel = new Label
            {
                Text = "RDP Guard",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 22F),
                TextAlign = ContentAlignment.BottomLeft
            };
            _versionLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            titlePanel.Controls.Add(_titleLabel, 0, 0);
            titlePanel.Controls.Add(_versionLabel, 0, 1);
            header.Controls.Add(titlePanel, 1, 0);

            var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0) };
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            _statusLabel = new Label
            {
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill
            };
            _updateStatusLabel = new Label
            {
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopRight,
                Dock = DockStyle.Fill
            };
            statusPanel.Controls.Add(_statusLabel, 0, 0);
            statusPanel.Controls.Add(_updateStatusLabel, 0, 1);
            header.Controls.Add(statusPanel, 2, 0);

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
                HorizontalScrollbar = true,
                IntegralHeight = false
            };
            root.Controls.Add(WrapSection(_logList, out _logTitleLabel, _clearLogButton = Button(string.Empty)), 0, 3);
            _clearLogButton.Click += (_, __) => _logList.Items.Clear();
        }

        private Control BuildSettingsPanel()
        {
            _settingsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 10)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 4
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _settingsPanel.Controls.Add(table);

            _thresholdLabel = LabelFor(string.Empty, DockStyle.Fill);
            table.Controls.Add(_thresholdLabel, 0, 0);
            _thresholdInput = new NumericUpDown { Minimum = AppConfig.MinFailureThreshold, Maximum = AppConfig.MaxFailureThreshold, Dock = DockStyle.Fill };
            table.Controls.Add(_thresholdInput, 1, 0);

            _intervalLabel = LabelFor(string.Empty, DockStyle.Fill);
            table.Controls.Add(_intervalLabel, 0, 1);
            _intervalInput = new NumericUpDown { Minimum = AppConfig.MinCheckIntervalMinutes, Maximum = AppConfig.MaxCheckIntervalMinutes, Dock = DockStyle.Fill };
            table.Controls.Add(_intervalInput, 1, 1);

            _startupCheck = new CheckBox { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            table.Controls.Add(_startupCheck, 2, 0);
            table.SetColumnSpan(_startupCheck, 2);

            var languagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 8, 0) };
            languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
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
            table.SetColumnSpan(languagePanel, 2);

            var themePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 8, 0) };
            themePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            themePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _themeLabel = LabelFor(string.Empty, DockStyle.Fill);
            _themeCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _themeCombo.SelectedIndexChanged += (_, __) =>
            {
                var themeCode = GetSelectedThemeMode();
                if (!string.Equals(themeCode, _themeMode, StringComparison.OrdinalIgnoreCase))
                {
                    _themeMode = themeCode;
                    ApplyTheme();
                }
            };
            themePanel.Controls.Add(_themeLabel, 0, 0);
            themePanel.Controls.Add(_themeCombo, 1, 0);
            table.Controls.Add(themePanel, 4, 2);
            table.SetColumnSpan(themePanel, 2);

            _toggleButton = Button(string.Empty);
            _toggleButton.Click += (_, __) => ToggleMonitoring();
            table.Controls.Add(_toggleButton, 4, 0);

            _checkButton = Button(string.Empty);
            _checkButton.Click += (_, __) => ThreadPool.QueueUserWorkItem(___ => _service.CheckNow());
            table.Controls.Add(_checkButton, 5, 0);

            _saveButton = Button(string.Empty);
            _saveButton.Click += (_, __) => SaveSettings();
            table.Controls.Add(_saveButton, 4, 1);

            _unblockButton = Button(string.Empty);
            _unblockButton.Enabled = false;
            _unblockButton.Click += (_, __) => UnblockSelected();
            table.Controls.Add(_unblockButton, 5, 1);

            _updateButton = Button(string.Empty);
            _updateButton.Click += (_, __) => CheckForUpdates(showDialog: true);
            table.Controls.Add(_updateButton, 4, 3);
            table.SetColumnSpan(_updateButton, 2);

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
            table.SetColumnSpan(_whitelistText, 3);
            table.Controls.Add(_whitelistText, 1, 2);
            table.SetRowSpan(_whitelistText, 2);

            return _settingsPanel;
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

        private Button Button(string text)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(6, 2, 0, 4),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Control WrapSection(Control content, out Label label, Button actionButton = null)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                ColumnCount = actionButton == null ? 1 : 2,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (actionButton != null)
            {
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
                actionButton.Margin = new Padding(8, 1, 0, 3);
            }

            label = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 10F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            header.Controls.Add(label, 0, 0);
            if (actionButton != null)
            {
                header.Controls.Add(actionButton, 1, 0);
            }

            panel.Controls.Add(content);
            panel.Controls.Add(header);
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
                Icon = Icon ?? SystemIcons.Shield,
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
            _themeMode = NormalizeThemeMode(config.ThemeMode);
            ApplyLocalization();
            ApplyTheme();
            _thresholdInput.Value = Math.Min(_thresholdInput.Maximum, Math.Max(_thresholdInput.Minimum, config.FailureThreshold));
            _intervalInput.Value = Math.Min(_intervalInput.Maximum, Math.Max(_intervalInput.Minimum, config.CheckIntervalMinutes));
            _startupCheck.Checked = config.StartWithWindows || _startupManager.IsEnabled();
            _whitelistText.Text = string.Join(Environment.NewLine, config.Whitelist);
            SelectLanguage(config.LanguageCode);
            SelectTheme(config.ThemeMode);

            _toggleButton.Text = config.MonitorEnabled ? L("Stop") : L("Start");
            _toggleButton.BackColor = config.MonitorEnabled ? _theme.Danger : _theme.Accent;
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
                config.ThemeMode = GetSelectedThemeMode();
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
            _statusLabel.Text = text;
            if (_versionLabel != null)
            {
                _versionLabel.Text = Localization.Format(_languageCode, "Version", AppInfo.Version);
            }
        }

        private void CheckForUpdates(bool showDialog)
        {
            lock (_updateSync)
            {
                if (_updateCheckRunning)
                {
                    return;
                }

                _updateCheckRunning = true;
            }

            _updateButton.Enabled = false;
            _updateStatusLabel.Text = L("UpdateChecking");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = UpdateChecker.CheckLatest();
                Ui(() =>
                {
                    _updateCheckRunning = false;
                    _updateButton.Enabled = true;
                    if (!result.Success)
                    {
                        var message = Localization.Format(_languageCode, "UpdateFailed", result.ErrorMessage);
                        _updateStatusLabel.Text = message;
                        AddLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message);
                        if (showDialog)
                        {
                            MessageBox.Show(message, "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        return;
                    }

                    if (result.IsUpdateAvailable)
                    {
                        var message = Localization.Format(_languageCode, "UpdateAvailable", result.LatestTag);
                        _updateStatusLabel.Text = message;
                        AddLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message);
                        if (showDialog)
                        {
                            var dialogResult = MessageBox.Show(
                                Localization.Format(_languageCode, "UpdateAvailableMessage", result.LatestTag) + Environment.NewLine + result.LatestUrl,
                                "RDP Guard",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Information);
                            if (dialogResult == DialogResult.OK)
                            {
                                OpenUrl(result.LatestUrl);
                            }
                        }

                        return;
                    }

                    _updateStatusLabel.Text = L("UpdateCurrent");
                    if (showDialog)
                    {
                        MessageBox.Show(L("UpdateCurrent"), "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            });
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(url) ? AppInfo.RepositoryUrl : url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Opening release URL failed", ex);
            }
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
            if (_titleLabel != null)
            {
                _titleLabel.Text = L("AppTitle");
            }

            if (_versionLabel != null)
            {
                _versionLabel.Text = Localization.Format(_languageCode, "Version", AppInfo.Version);
            }

            _thresholdLabel.Text = L("Threshold");
            _intervalLabel.Text = L("Interval");
            _languageLabel.Text = L("Language");
            _themeLabel.Text = L("Theme");
            _startupCheck.Text = L("StartWithWindows");
            _checkButton.Text = L("CheckNow");
            _updateButton.Text = L("CheckUpdates");
            _saveButton.Text = L("Save");
            _unblockButton.Text = L("Unblock");
            _clearLogButton.Text = L("ClearLog");
            _whitelistLabel.Text = L("Whitelist");
            _blockedTitleLabel.Text = L("BlockedIps");
            _logTitleLabel.Text = L("Log");
            RefreshThemeItems();

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

        private void RefreshThemeItems()
        {
            var selected = _themeMode;
            _themeCombo.BeginUpdate();
            _themeCombo.Items.Clear();
            _themeCombo.Items.Add(new ThemeOption("system", L("ThemeSystem")));
            _themeCombo.Items.Add(new ThemeOption("light", L("ThemeLight")));
            _themeCombo.Items.Add(new ThemeOption("dark", L("ThemeDark")));
            _themeCombo.EndUpdate();
            SelectTheme(string.IsNullOrWhiteSpace(selected) ? _themeMode : selected);
        }

        private void SelectTheme(string code)
        {
            code = NormalizeThemeMode(code);
            foreach (var item in _themeCombo.Items.OfType<ThemeOption>())
            {
                if (string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    _themeCombo.SelectedItem = item;
                    return;
                }
            }

            if (_themeCombo.Items.Count > 0)
            {
                _themeCombo.SelectedIndex = 0;
            }
        }

        private string GetSelectedThemeMode()
        {
            return _themeCombo != null && _themeCombo.SelectedItem is ThemeOption option
                ? NormalizeThemeMode(option.Code)
                : NormalizeThemeMode(_themeMode);
        }

        private static string NormalizeThemeMode(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value == "light" || value == "dark" || value == "system"
                ? value
                : "system";
        }

        private void ApplyTheme()
        {
            _theme = ResolveTheme(_themeMode);
            BackColor = _theme.WindowBack;
            ForeColor = _theme.Text;
            ApplyThemeRecursive(this);
            if (_settingsPanel != null)
            {
                _settingsPanel.BackColor = _theme.PanelBack;
            }

            if (_titleLabel != null)
            {
                _titleLabel.ForeColor = _theme.Title;
            }

            if (_versionLabel != null)
            {
                _versionLabel.ForeColor = _theme.SubtleText;
            }

            if (_toggleButton != null)
            {
                var active = _service.GetConfigSnapshot().MonitorEnabled;
                _toggleButton.BackColor = active ? _theme.Danger : _theme.Accent;
                _toggleButton.ForeColor = Color.White;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = Icon ?? SystemIcons.Shield;
            }
        }

        private void ApplyThemeRecursive(Control control)
        {
            foreach (Control child in control.Controls)
            {
                if (child is Button button)
                {
                    button.BackColor = button == _toggleButton ? button.BackColor : _theme.Accent;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = _theme.Border;
                }
                else if (child is TextBox || child is ListBox || child is ListView || child is NumericUpDown || child is ComboBox)
                {
                    child.BackColor = _theme.InputBack;
                    child.ForeColor = _theme.Text;
                }
                else if (child is Panel || child is TableLayoutPanel)
                {
                    child.BackColor = child == _settingsPanel ? _theme.PanelBack : _theme.WindowBack;
                    child.ForeColor = _theme.Text;
                }
                else if (child is Label)
                {
                    child.BackColor = Color.Transparent;
                    child.ForeColor = _theme.Text;
                }
                else if (child is CheckBox)
                {
                    child.BackColor = Color.Transparent;
                    child.ForeColor = _theme.Text;
                }

                ApplyThemeRecursive(child);
            }
        }

        private static ThemePalette ResolveTheme(string mode)
        {
            mode = NormalizeThemeMode(mode);
            var dark = mode == "dark" || (mode == "system" && IsWindowsDarkTheme());
            return dark ? ThemePalette.Dark : ThemePalette.Light;
        }

        private static bool IsWindowsDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int intValue && intValue == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Shield;
            }
            catch
            {
                return SystemIcons.Shield;
            }
        }

        private enum CloseChoice
        {
            Cancel,
            MinimizeToTray,
            CloseProgram
        }

        private sealed class ThemeOption
        {
            public ThemeOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public string Code { get; }
            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class ThemePalette
        {
            public static readonly ThemePalette Light = new ThemePalette(
                Color.FromArgb(243, 246, 250),
                Color.White,
                Color.FromArgb(250, 251, 253),
                Color.FromArgb(19, 27, 39),
                Color.FromArgb(31, 41, 55),
                Color.FromArgb(91, 101, 117),
                Color.FromArgb(27, 97, 178),
                Color.FromArgb(180, 55, 55),
                Color.FromArgb(208, 216, 226));

            public static readonly ThemePalette Dark = new ThemePalette(
                Color.FromArgb(24, 28, 34),
                Color.FromArgb(32, 37, 45),
                Color.FromArgb(38, 44, 53),
                Color.FromArgb(237, 241, 246),
                Color.FromArgb(248, 250, 252),
                Color.FromArgb(168, 177, 190),
                Color.FromArgb(64, 133, 220),
                Color.FromArgb(190, 78, 78),
                Color.FromArgb(72, 82, 96));

            private ThemePalette(Color windowBack, Color panelBack, Color inputBack, Color text, Color title, Color subtleText, Color accent, Color danger, Color border)
            {
                WindowBack = windowBack;
                PanelBack = panelBack;
                InputBack = inputBack;
                Text = text;
                Title = title;
                SubtleText = subtleText;
                Accent = accent;
                Danger = danger;
                Border = border;
            }

            public Color WindowBack { get; }
            public Color PanelBack { get; }
            public Color InputBack { get; }
            public Color Text { get; }
            public Color Title { get; }
            public Color SubtleText { get; }
            public Color Accent { get; }
            public Color Danger { get; }
            public Color Border { get; }
        }
    }
}
