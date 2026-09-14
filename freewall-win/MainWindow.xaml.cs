using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;

namespace freewall_win
{
    public partial class MainWindow : FluentWindow
    {
        private readonly SpoofDPIManager _manager = SpoofDPIManager.Instance;
        private readonly AppSettings _settings = AppSettings.Instance;
        private bool _isInitializing = true;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _manager;

            _manager.StateChanged += Manager_StateChanged;
            _manager.Logs.CollectionChanged += Logs_CollectionChanged;

            LoadSettingsToUI();
            _isInitializing = false;
            UpdateStatusUI();

            if (_settings.AutoStartProtection)
            {
                _manager.Start();
            }
        }

        private void Manager_StateChanged()
        {
            Dispatcher.Invoke(UpdateStatusUI);
        }

        private void Logs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtLogCount != null)
                {
                    TxtLogCount.Text = $"{_manager.Logs.Count}개 라인";
                }
                if (ChkAutoScroll?.IsChecked == true && LogScrollViewer != null)
                {
                    LogScrollViewer.ScrollToEnd();
                }
            });
        }

        private void UpdateStatusUI()
        {
            if (TxtStatus == null || StatusDot == null) return;

            bool isRunning = _manager.IsRunning;

            if (isRunning)
            {
                TxtStatus.Text = "보호 활성화됨";
                StatusDot.Fill = (SolidColorBrush)FindResource("AccentGreenBrush");
            }
            else
            {
                TxtStatus.Text = "보호 비활성화됨";
                StatusDot.Fill = (SolidColorBrush)FindResource("InactiveGrayBrush");
            }

            // 1. 실행 중 옵션 잠금 및 안내문구 표시
            if (PanelSettingsContainer != null)
            {
                PanelSettingsContainer.IsEnabled = !isRunning;
            }
            if (PanelRunningNotice != null)
            {
                PanelRunningNotice.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
            }

            string presetLabel = _settings.Engine == "spoofdpi" 
                ? $"127.0.0.1:{_settings.SpoofDpiPort}"
                : (_settings.ModePreset switch
                {
                    "chunk1b" => "Chunk 1B (Disorder)",
                    "-1" => "Mode 1 (표준)",
                    "-2" => "Mode 2 (고속)",
                    "-3" => "Mode 3 (균형)",
                    "-4" => "Mode 4 (최대속도)",
                    "-5" => "Mode 5 (역방향)",
                    "custom" => "Custom",
                    _ => _settings.ModePreset.Replace("-", "Mode ")
                });

            if (BadgePreset != null) BadgePreset.Text = presetLabel;
            if (ChkAutoProtect != null)
            {
                ChkAutoProtect.IsChecked = AutoStartManager.IsEnabled && _settings.AutoStartProtection;
            }
        }

        private void LoadSettingsToUI()
        {
            if (ChkLaunchAtStartup != null) ChkLaunchAtStartup.IsChecked = AutoStartManager.IsEnabled;
            if (ChkAutoStartProtection != null) ChkAutoStartProtection.IsChecked = _settings.AutoStartProtection;

            if (TxtCustomArgs != null) TxtCustomArgs.Text = _settings.CustomArguments;

            if (ChkUseCustomDns != null) ChkUseCustomDns.IsChecked = _settings.UseCustomDns;
            if (PanelDnsInputs != null) PanelDnsInputs.Visibility = _settings.UseCustomDns ? Visibility.Visible : Visibility.Collapsed;
            if (TxtDnsIp != null) TxtDnsIp.Text = _settings.CustomDnsIp;
            if (TxtDnsPort != null) TxtDnsPort.Text = _settings.CustomDnsPort.ToString();

            switch (_settings.ModePreset)
            {
                case "chunk1b": if (RadioModeChunk1B != null) RadioModeChunk1B.IsChecked = true; break;
                case "-1": if (RadioMode1 != null) RadioMode1.IsChecked = true; break;
                case "-2": if (RadioMode2 != null) RadioMode2.IsChecked = true; break;
                case "-3": if (RadioMode3 != null) RadioMode3.IsChecked = true; break;
                case "-4": if (RadioMode4 != null) RadioMode4.IsChecked = true; break;
                case "-5": if (RadioMode5 != null) RadioMode5.IsChecked = true; break;
                case "custom":
                    if (RadioModeCustom != null) RadioModeCustom.IsChecked = true;
                    if (TxtCustomArgs != null) TxtCustomArgs.Visibility = Visibility.Visible;
                    break;
                default: if (RadioModeChunk1B != null) RadioModeChunk1B.IsChecked = true; break;
            }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || ViewProtection == null || ViewSettings == null || ViewLogs == null) return;

            ViewProtection.Visibility = TabProtection?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ViewSettings.Visibility = TabSettings?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ViewLogs.Visibility = TabLogs?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnPowerToggle_Click(object sender, RoutedEventArgs e)
        {
            _manager.Toggle();
        }

        private void ChkAutoProtect_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool enable = ChkAutoProtect?.IsChecked == true;
            AutoStartManager.SetEnabled(enable);
            _settings.AutoStartProtection = enable;
            _settings.LaunchAtStartup = enable;
            _settings.Save();
            LoadSettingsToUI();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SaveSettingsFromUI();
        }

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (RadioModeChunk1B?.IsChecked == true) _settings.ModePreset = "chunk1b";
            else if (RadioMode1?.IsChecked == true) _settings.ModePreset = "-1";
            else if (RadioMode2?.IsChecked == true) _settings.ModePreset = "-2";
            else if (RadioMode3?.IsChecked == true) _settings.ModePreset = "-3";
            else if (RadioMode4?.IsChecked == true) _settings.ModePreset = "-4";
            else if (RadioMode5?.IsChecked == true) _settings.ModePreset = "-5";
            else if (RadioModeCustom?.IsChecked == true)
            {
                _settings.ModePreset = "custom";
                if (TxtCustomArgs != null) TxtCustomArgs.Visibility = Visibility.Visible;
            }

            if (RadioModeCustom?.IsChecked == false && TxtCustomArgs != null)
            {
                TxtCustomArgs.Visibility = Visibility.Collapsed;
            }

            _settings.Save();
            UpdateStatusUI();
        }

        private void BtnPresetChunk1B_Click(object sender, RoutedEventArgs e)
        {
            if (_manager.IsRunning) return;
            if (RadioModeChunk1B != null) RadioModeChunk1B.IsChecked = true;
            _settings.ModePreset = "chunk1b";
            _settings.Save();
            LoadSettingsToUI();
            UpdateStatusUI();
        }

        private void BtnPresetMode1_Click(object sender, RoutedEventArgs e)
        {
            if (_manager.IsRunning) return;
            if (RadioMode1 != null) RadioMode1.IsChecked = true;
            _settings.ModePreset = "-1";
            _settings.Save();
            LoadSettingsToUI();
            UpdateStatusUI();
        }

        private void TxtCustomArgs_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TxtCustomArgs == null) return;
            _settings.CustomArguments = TxtCustomArgs.Text;
            _settings.Save();
        }

        private void ChkUseCustomDns_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isCustom = ChkUseCustomDns?.IsChecked == true;
            _settings.UseCustomDns = isCustom;
            if (PanelDnsInputs != null)
            {
                PanelDnsInputs.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            }
            _settings.Save();
        }

        private void TxtDns_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (TxtDnsIp != null) _settings.CustomDnsIp = TxtDnsIp.Text.Trim();
            if (TxtDnsPort != null && int.TryParse(TxtDnsPort.Text.Trim(), out int port))
            {
                _settings.CustomDnsPort = port;
            }
            _settings.Save();
        }

        private void SaveSettingsFromUI()
        {
            if (_isInitializing) return;

            if (ChkAutoStartProtection != null)
            {
                _settings.AutoStartProtection = ChkAutoStartProtection.IsChecked == true;
            }

            if (ChkLaunchAtStartup != null)
            {
                bool startup = ChkLaunchAtStartup.IsChecked == true;
                AutoStartManager.SetEnabled(startup);
                _settings.LaunchAtStartup = startup;
            }

            _settings.Save();
            UpdateStatusUI();
        }

        private void BtnCopyLogs_Click(object sender, RoutedEventArgs e)
        {
            string logs = string.Join(Environment.NewLine, _manager.Logs.Select(l => $"[{l.FormattedTime}] {l.Text}"));
            if (!string.IsNullOrEmpty(logs))
            {
                WpfClipboard.SetText(logs);
                WpfMessageBox.Show("로그가 클립보드에 복사되었습니다.", "알림", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _manager.ClearLogs();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close to tray
            e.Cancel = true;
            this.Hide();
        }
    }
}
