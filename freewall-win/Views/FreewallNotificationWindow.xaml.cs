using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfUiSymbol = Wpf.Ui.Controls.SymbolRegular;

namespace freewall_win.Views
{
    public partial class FreewallNotificationWindow : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;

        public FreewallNotificationWindow(bool isProtected, string message)
        {
            InitializeComponent();

            if (isProtected)
            {
                IconBadge.Symbol = WpfUiSymbol.ShieldCheckmark24;
                IconBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Emerald green
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x10, 0xB9, 0x81));
                BadgeTxt.Text = "보호 활성화";
                BadgeTxt.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            else
            {
                IconBadge.Symbol = WpfUiSymbol.ShieldDismiss24;
                IconBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)); // Gray
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x9C, 0xA3, 0xAF));
                BadgeTxt.Text = "보호 비활성화";
                BadgeTxt.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
            }

            MsgTxt.Text = message;

            // Windows 11 Bottom-Right placement (16px margin from work area)
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;

            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                CloseWithAnimation();
            };

            Loaded += (s, e) =>
            {
                if (Resources["ShowAnim"] is Storyboard showAnim)
                {
                    showAnim.Begin();
                }
                _autoCloseTimer.Start();
            };

            NotificationCard.MouseLeftButtonDown += (s, e) =>
            {
                _autoCloseTimer.Stop();
                CloseWithAnimation();
            };
        }

        public static void ShowNotification(bool isProtected, string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var notifyWin = new FreewallNotificationWindow(isProtected, message);
                notifyWin.Show();
            });
        }

        private void CloseWithAnimation()
        {
            if (Resources["HideAnim"] is Storyboard hideAnim)
            {
                hideAnim.Completed += (s, e) => Close();
                hideAnim.Begin();
            }
            else
            {
                Close();
            }
        }
    }
}
