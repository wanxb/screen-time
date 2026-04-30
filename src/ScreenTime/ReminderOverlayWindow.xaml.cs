using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ScreenTime.Models;
using DrawingColor = System.Drawing.Color;

namespace ScreenTime;

public partial class ReminderOverlayWindow : Window
{
    private const string VideoHostName = "screen-time-cat.local";
    private readonly UserSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly string? _videoDirectory;
    private readonly KeyboardBlocker _keyboardBlocker = new();
    private TimeSpan _remaining;
    private bool _canClose;
    private bool _webViewReady;

    public ReminderOverlayWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _videoDirectory = TryGetCharacterVideoDirectory(settings.ReminderCharacter);
        _remaining = TimeSpan.FromMinutes(Math.Max(1, settings.BreakDurationMinutes));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public string Action { get; private set; } = "completed";

    public void ForceClose()
    {
        Action = "app_exit";
        _canClose = true;
        Close();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderCountdown();
        _timer.Start();
        ActivateOverlay();
        _keyboardBlocker.Start(_settings.AllowCloseFullscreenReminder ? CloseFromKeyboard : null);
        await InitializeWebViewAsync();
    }

    private void ActivateOverlay()
    {
        Topmost = true;
        WindowState = WindowState.Maximized;
        Activate();
        Focus();
        ReminderWebView.Focus();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webViewReady)
        {
            return;
        }

        await ReminderWebView.EnsureCoreWebView2Async();
        ReminderWebView.DefaultBackgroundColor = DrawingColor.Transparent;
        ReminderWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        ReminderWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        ReminderWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        if (!string.IsNullOrWhiteSpace(_videoDirectory))
        {
            ReminderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VideoHostName,
                _videoDirectory,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        _webViewReady = true;
        ReminderWebView.NavigateToString(BuildReminderHtml());
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining -= TimeSpan.FromSeconds(1);
        if (_remaining <= TimeSpan.Zero)
        {
            Action = "completed";
            _canClose = true;
            Close();
            return;
        }

        RenderCountdown();
    }

    private void RenderCountdown()
    {
        var text = $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";
        if (_webViewReady)
        {
            _ = ReminderWebView.ExecuteScriptAsync(
                $"window.setCountdown({JsonSerializer.Serialize(text)});");
        }
    }

    private string BuildReminderHtml()
    {
        var opacity = Math.Clamp(_settings.OverlayOpacity, 0, 0.9).ToString("0.###", CultureInfo.InvariantCulture);
        var closeDisplay = _settings.AllowCloseFullscreenReminder ? "grid" : "none";
        var entrySource = _videoDirectory is null ? string.Empty : $"https://{VideoHostName}/cat_0.webm";
        var idleSource = _videoDirectory is null ? string.Empty : $"https://{VideoHostName}/cat_1.webm";
        var initialCountdown = JsonSerializer.Serialize($"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}");

        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    * { box-sizing: border-box; }
    html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: transparent; }
    body { font-family: Consolas, "Microsoft YaHei UI", monospace; user-select: none; }
    .stage { position: fixed; inset: 0; overflow: hidden; background: rgba(0, 0, 0, {{opacity}}); }
    video {
      position: absolute;
      inset: 0;
      width: 100vw;
      height: 100vh;
      object-fit: contain;
      pointer-events: none;
      background: transparent;
      opacity: 0;
      transition: opacity 80ms linear;
    }
    video.active { opacity: 1; }
    .timer {
      position: absolute;
      left: 128px;
      top: 150px;
      min-width: 306px;
      padding: 16px 58px 16px 18px;
      border: 1px solid rgba(255,255,255,0.26);
      border-radius: 22px;
      background: rgba(255,255,255,0.075);
      box-shadow: 0 10px 30px rgba(0,0,0,0.16);
      backdrop-filter: blur(12px) saturate(125%);
      -webkit-backdrop-filter: blur(12px) saturate(125%);
      z-index: 3;
    }
    #countdown {
      color: #f9f5ea;
      font-size: 88px;
      line-height: 1;
      font-weight: 650;
      text-shadow: 0 2px 18px rgba(0,0,0,0.28);
      white-space: nowrap;
    }
    #close {
      position: absolute;
      top: -13px;
      right: -13px;
      width: 34px;
      height: 34px;
      display: {{closeDisplay}};
      place-items: center;
      border: 1px solid rgba(255,255,255,0.34);
      border-radius: 999px;
      background: rgba(0,0,0,0.15);
      color: #f9f5ea;
      font: 650 20px/1 "Microsoft YaHei UI", sans-serif;
      cursor: pointer;
    }
    #close:hover { background: rgba(255,255,255,0.27); }
  </style>
</head>
<body>
  <div class="stage">
    <video id="entry" autoplay playsinline muted preload="auto" src="{{HtmlEncoder.Default.Encode(entrySource)}}"></video>
    <video id="idle" playsinline muted preload="auto" src="{{HtmlEncoder.Default.Encode(idleSource)}}"></video>
    <div class="timer">
      <div id="countdown"></div>
      <button id="close" aria-label="关闭">×</button>
    </div>
  </div>
  <script>
    const entry = document.getElementById('entry');
    const idle = document.getElementById('idle');

    window.setCountdown = value => {
      document.getElementById('countdown').textContent = value;
    };
    window.setCountdown({{initialCountdown}});

    function show(video) {
      entry.classList.toggle('active', video === entry);
      idle.classList.toggle('active', video === idle);
    }

    idle.addEventListener('canplaythrough', () => {
      if (!entry.src) show(idle);
    }, { once: true });

    entry.addEventListener('canplay', () => show(entry), { once: true });
    entry.addEventListener('ended', () => {
      idle.currentTime = 0;
      const playIdle = () => {
        show(idle);
        idle.play();
      };
      if (idle.readyState >= 2) playIdle();
      else idle.addEventListener('canplay', playIdle, { once: true });
    });

    entry.addEventListener('error', () => {
      idle.currentTime = 0;
      show(idle);
      idle.play();
    });
    idle.addEventListener('ended', () => {
      idle.currentTime = 0;
      idle.play();
    });

    document.getElementById('close').addEventListener('click', () => {
      chrome.webview.postMessage('close');
    });
  </script>
</body>
</html>
""";
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.TryGetWebMessageAsString().Equals("close", StringComparison.OrdinalIgnoreCase))
        {
            Action = "closed";
            _canClose = true;
            Close();
        }
    }

    private static string? TryGetCharacterVideoDirectory(string reminderCharacter)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayRunners", reminderCharacter, "video");
        return Directory.Exists(directory) ? directory : null;
    }

    private void CloseFromKeyboard()
    {
        Dispatcher.Invoke(() =>
        {
            Action = "closed";
            _canClose = true;
            Close();
        });
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == System.Windows.Input.Key.Escape && _settings.AllowCloseFullscreenReminder)
        {
            Action = "closed";
            _canClose = true;
            Close();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_settings.AllowCloseFullscreenReminder && !_canClose)
        {
            e.Cancel = true;
            return;
        }

        _keyboardBlocker.Dispose();
        _timer.Stop();
        _timer.Tick -= OnTick;
        if (ReminderWebView.CoreWebView2 is not null)
        {
            ReminderWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
        ReminderWebView.Dispose();
    }

    private sealed class KeyboardBlocker : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int VkEscape = 0x1B;
        private readonly LowLevelKeyboardProc _proc;
        private nint _hook;
        private Action? _onEscape;

        public KeyboardBlocker()
        {
            _proc = HookCallback;
        }

        public void Start(Action? onEscape)
        {
            _onEscape = onEscape;
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(currentModule?.ModuleName), 0);
        }

        private nint HookCallback(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                var virtualKey = Marshal.ReadInt32(lParam);
                if ((message == WmKeyDown || message == WmSysKeyDown) && virtualKey == VkEscape)
                {
                    _onEscape?.Invoke();
                }

                return 1;
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != 0)
            {
                UnhookWindowsHookEx(_hook);
                _hook = 0;
            }
        }

        private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll")]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint GetModuleHandle(string? lpModuleName);
    }
}
