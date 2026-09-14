using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WpfApplication = System.Windows.Application;

namespace freewall_win
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Text { get; set; } = "";
        public bool IsError { get; set; }
        public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    }

    public class SpoofDPIManager : INotifyPropertyChanged
    {
        private static SpoofDPIManager? _instance;
        public static SpoofDPIManager Instance => _instance ??= new SpoofDPIManager();

        private Process? _process;
        private readonly AppSettings _settings = AppSettings.Instance;

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _statusMessage = "보호 비활성화됨";
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        public event Action? StateChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const int MaxLogCount = 500;

        public string? FindBinaryPath()
        {
            if (!string.IsNullOrEmpty(_settings.CustomBinaryPath) && File.Exists(_settings.CustomBinaryPath))
            {
                return _settings.CustomBinaryPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            if (_settings.Engine == "spoofdpi")
            {
                string candidate1 = Path.Combine(baseDir, "spoofdpi", "spoofdpi.exe");
                if (File.Exists(candidate1)) return candidate1;

                string candidate2 = Path.Combine(baseDir, "spoofdpi.exe");
                if (File.Exists(candidate2)) return candidate2;
            }
            else
            {
                string arch = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";

                string candidate1 = Path.Combine(baseDir, "goodbyedpi", arch, "goodbyedpi.exe");
                if (File.Exists(candidate1)) return candidate1;

                string candidate2 = Path.Combine(baseDir, arch, "goodbyedpi.exe");
                if (File.Exists(candidate2)) return candidate2;

                string candidate3 = Path.Combine(baseDir, "goodbyedpi.exe");
                if (File.Exists(candidate3)) return candidate3;
            }

            return null;
        }

        public async void Start()
        {
            if (IsRunning) return;

            await Task.Run(() => KillExistingProcesses());

            string? binaryPath = FindBinaryPath();
            if (string.IsNullOrEmpty(binaryPath) || !File.Exists(binaryPath))
            {
                AppendLog("오류: 실행 파일(spoofdpi.exe)을 찾을 수 없습니다.", true);
                StatusMessage = "바이너리 없음";
                StateChanged?.Invoke();
                return;
            }

            string workingDir = Path.GetDirectoryName(binaryPath)!;
            var argsBuilder = new StringBuilder();

            if (_settings.Engine == "spoofdpi")
            {
                // SpoofDPI arguments (WPF manages proxy directly for zero latency)
                argsBuilder.Append($"-listen-port {_settings.SpoofDpiPort} -system-proxy=false ");
                if (!string.IsNullOrWhiteSpace(_settings.SpoofDpiDnsAddr))
                {
                    argsBuilder.Append($"-dns-addr {_settings.SpoofDpiDnsAddr} -dns-port {_settings.SpoofDpiDnsPort} ");
                }
                if (_settings.SpoofDpiEnableDoh)
                {
                    argsBuilder.Append("-enable-doh ");
                }
                if (_settings.SpoofDpiWindowSize > 0)
                {
                    argsBuilder.Append($"-window-size {_settings.SpoofDpiWindowSize} ");
                }
            }
            else
            {
                // GoodbyeDPI arguments
                if (_settings.ModePreset == "chunk1b")
                {
                    argsBuilder.Append("-p -r -s -f 2 -e 1 --reverse-frag");
                }
                else if (_settings.ModePreset == "custom")
                {
                    argsBuilder.Append(_settings.CustomArguments);
                }
                else
                {
                    argsBuilder.Append(_settings.ModePreset);
                }

                if (_settings.UseCustomDns && !string.IsNullOrWhiteSpace(_settings.CustomDnsIp))
                {
                    argsBuilder.Append($" --dns-addr {_settings.CustomDnsIp} --dns-port {_settings.CustomDnsPort}");
                }
            }

            string arguments = argsBuilder.ToString().Trim();
            AppendLog($"$ spoofdpi {arguments}", false);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                _process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        WpfApplication.Current?.Dispatcher.InvokeAsync(() => AppendLog(e.Data, false));
                    }
                };

                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        WpfApplication.Current?.Dispatcher.InvokeAsync(() => AppendLog(e.Data, true));
                    }
                };

                _process.Exited += (s, e) =>
                {
                    int exitCode = _process?.ExitCode ?? 0;
                    if (_settings.Engine == "spoofdpi")
                    {
                        WindowsProxyManager.DisableProxy();
                    }
                    WpfApplication.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        IsRunning = false;
                        StatusMessage = $"중지됨 ({exitCode})";
                        AppendLog($"프로세스가 종료되었습니다. (코드: {exitCode})", exitCode != 0);
                        StateChanged?.Invoke();
                    });
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                if (_settings.Engine == "spoofdpi")
                {
                    // Configure Windows System Proxy
                    WindowsProxyManager.SetProxy($"127.0.0.1:{_settings.SpoofDpiPort}");
                }

                IsRunning = true;
                StatusMessage = $"보호 활성화됨 ({(_settings.Engine == "spoofdpi" ? "127.0.0.1:" + _settings.SpoofDpiPort : _settings.ModePreset)})";
                AppendLog("보호 서비스가 성공적으로 시작되었습니다.", false);
            }
            catch (Exception ex)
            {
                if (_settings.Engine == "spoofdpi")
                {
                    WindowsProxyManager.DisableProxy();
                }
                IsRunning = false;
                StatusMessage = "시작 실패";
                AppendLog($"실행 실패: {ex.Message}", true);
            }

            StateChanged?.Invoke();
        }

        public async void Stop()
        {
            var proc = _process;
            _process = null;

            if (_settings.Engine == "spoofdpi")
            {
                WindowsProxyManager.DisableProxy();
            }

            if (proc != null && !proc.HasExited)
            {
                AppendLog("보호 중지 중...", false);
                await Task.Run(() =>
                {
                    try
                    {
                        proc.Kill(true);
                        proc.WaitForExit(1500);
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                });
            }

            await Task.Run(() => KillExistingProcesses());

            IsRunning = false;
            StatusMessage = "보호 비활성화됨";
            AppendLog("보호가 비활성화되었습니다.", false);
            StateChanged?.Invoke();
        }

        public void Toggle()
        {
            if (IsRunning) Stop();
            else Start();
        }

        public void AppendLog(string text, bool isError)
        {
            Logs.Add(new LogEntry { Text = text, IsError = isError });
            if (Logs.Count > MaxLogCount)
            {
                Logs.RemoveAt(0);
            }
        }

        public void ClearLogs()
        {
            Logs.Clear();
        }

        private void KillExistingProcesses()
        {
            try
            {
                var names = new[] { "spoofdpi", "goodbyedpi" };
                foreach (var name in names)
                {
                    var processes = Process.GetProcessesByName(name);
                    foreach (var p in processes)
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
