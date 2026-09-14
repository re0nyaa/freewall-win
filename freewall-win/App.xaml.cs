using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using freewall_win.Views;
using WpfApplication = System.Windows.Application;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfContextMenu = System.Windows.Controls.ContextMenu;

namespace freewall_win
{
    public partial class App : WpfApplication
    {
        private TaskbarIcon? _trayIcon;
        private WpfMenuItem? _toggleMenuItem;
        private bool _lastNotifiedRunning = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            SetupTrayIcon();

            bool startMinimized = false;
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                }
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            if (!startMinimized)
            {
                mainWindow.Show();
            }

            SpoofDPIManager.Instance.StateChanged += OnManager_StateChanged;
            UpdateTrayMenu();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = Resources["FreewallTrayIcon"] as TaskbarIcon;
            if (_trayIcon == null)
            {
                _trayIcon = new TaskbarIcon();
            }

            _trayIcon.Icon = SystemIcons.Shield;
            _trayIcon.ToolTipText = "freewall";
            _trayIcon.TrayLeftMouseDown += (s, e) => ShowMainWindow();

            var contextMenu = new WpfContextMenu
            {
                Style = TryFindResource("Win11CleanContextMenuStyle") as Style
            };

            // 1. 보호 시작 / 보호 중지
            _toggleMenuItem = new WpfMenuItem
            {
                Header = "보호 시작",
                Style = TryFindResource("Win11CleanMenuItemStyle") as Style
            };
            _toggleMenuItem.Click += (s, e) => SpoofDPIManager.Instance.Toggle();
            contextMenu.Items.Add(_toggleMenuItem);

            contextMenu.Items.Add(new WpfSeparator
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(4, 2, 4, 2)
            });

            // 2. 대시보드 열기
            var openItem = new WpfMenuItem
            {
                Header = "대시보드 열기",
                Style = TryFindResource("Win11CleanMenuItemStyle") as Style
            };
            openItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(openItem);

            contextMenu.Items.Add(new WpfSeparator
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(4, 2, 4, 2)
            });

            // 3. 종료
            var exitItem = new WpfMenuItem
            {
                Header = "종료",
                Style = TryFindResource("Win11CleanMenuItemStyle") as Style
            };
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
        }

        private void OnManager_StateChanged()
        {
            Dispatcher.Invoke(() =>
            {
                bool running = SpoofDPIManager.Instance.IsRunning;
                _lastNotifiedRunning = running;
                UpdateTrayMenu();
            });
        }

        private void UpdateTrayMenu()
        {
            bool running = SpoofDPIManager.Instance.IsRunning;

            if (_toggleMenuItem != null)
            {
                _toggleMenuItem.Header = running ? "보호 중지" : "보호 시작";
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
            }

            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        public void ExitApplication()
        {
            SpoofDPIManager.Instance.Stop();

            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Collapsed;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            Environment.Exit(0);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            SpoofDPIManager.Instance.Stop();

            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Collapsed;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }
}
