using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ScreenTime.Core;
using ScreenTime.Models;
using ScreenTime.Services;
using DrawingColor = System.Drawing.Color;
using Point = System.Windows.Point;

namespace ScreenTime;

public partial class ReminderOverlayWindow : Window
{
    private const string AssetHostName = "screen-time-assets.local";
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private const double MaxBackdropBlurRadius = 34;
    private readonly UserSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly string? _assetDirectory;
    private readonly string? _entryVideoFile;
    private readonly string? _idleVideoFile;
    private readonly string[] _imageFrameFiles;
    private readonly KeyboardBlocker _keyboardBlocker = new();
    private TimeSpan _remaining;
    private bool _canClose;
    private bool _webViewReady;

    public ReminderOverlayWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _assetDirectory = TryGetCharacterAssetDirectory(settings.ReminderCharacter);
        (_entryVideoFile, _idleVideoFile) = TryGetCharacterVideoFiles(_assetDirectory, settings.ReminderCharacter);
        _imageFrameFiles = GetCharacterImageFrames(_assetDirectory, settings.ReminderCharacter);
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
        try
        {
            RenderCountdown();
            _timer.Start();
            ActivateOverlay();
            CaptureBlurredDesktopBackdrop();
            _keyboardBlocker.Start(_settings.AllowCloseFullscreenReminder ? CloseFromKeyboard : null);
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Reminder overlay initialization failed");
            Action = "overlay_error";
            _canClose = true;
            Close();
        }
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

        if (!string.IsNullOrWhiteSpace(_assetDirectory))
        {
            ReminderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AssetHostName,
                _assetDirectory,
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
        var closeDisplay = _settings.AllowCloseFullscreenReminder ? "grid" : "none";
        var entrySource = _entryVideoFile is null ? string.Empty : $"https://{AssetHostName}/{_entryVideoFile}";
        var idleSource = _idleVideoFile is null ? string.Empty : $"https://{AssetHostName}/{_idleVideoFile}";
        var frameSources = JsonSerializer.Serialize(_imageFrameFiles.Select(file => $"https://{AssetHostName}/{file}").ToArray());
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
    .stage { position: fixed; inset: 0; overflow: hidden; background: transparent; }
    video {
      position: absolute;
      inset: 0;
      width: 100vw;
      height: 100vh;
      object-fit: contain;
      pointer-events: none;
      background: transparent;
      opacity: 0;
      visibility: hidden;
      display: none;
    }
    video.active {
      opacity: 1;
      visibility: visible;
      display: block;
    }
    #sprite {
      position: absolute;
      left: 50%;
      top: 50%;
      width: min(54vw, 640px);
      height: min(54vw, 640px);
      object-fit: contain;
      transform: translate(-50%, -48%);
      pointer-events: none;
      display: none;
      filter: drop-shadow(0 18px 28px rgba(0,0,0,0.22));
    }
    #sprite.active { display: block; }
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
    <video id="entry" playsinline muted preload="auto" src="{{HtmlEncoder.Default.Encode(entrySource)}}"></video>
    <video id="idle" playsinline muted preload="auto" src="{{HtmlEncoder.Default.Encode(idleSource)}}"></video>
    <img id="sprite" alt="" />
    <div class="timer">
      <div id="countdown"></div>
      <button id="close" aria-label="关闭">×</button>
    </div>
  </div>
  <script>
    const entry = document.getElementById('entry');
    const idle = document.getElementById('idle');
    const sprite = document.getElementById('sprite');
    const frames = {{frameSources}};
    let frameIndex = 0;
    let spriteTimer = null;

    window.setCountdown = value => {
      document.getElementById('countdown').textContent = value;
    };
    window.setCountdown({{initialCountdown}});

    function show(video) {
      if (video !== entry) entry.pause();
      if (video !== idle) idle.pause();
      entry.classList.toggle('active', video === entry);
      idle.classList.toggle('active', video === idle);
      sprite.classList.toggle('active', video === sprite);
    }

    function playSprite() {
      if (!frames.length) return;
      show(sprite);
      sprite.src = frames[0];
      if (spriteTimer) clearInterval(spriteTimer);
      spriteTimer = setInterval(() => {
        frameIndex = (frameIndex + 1) % frames.length;
        sprite.src = frames[frameIndex];
      }, 120);
    }

    function playVideo(video) {
      show(video);
      const promise = video.play();
      if (promise) promise.catch(() => {});
    }

    function hasSource(video) {
      return !!video.getAttribute('src');
    }

    function playIdle() {
      if (!hasSource(idle)) {
        playSprite();
        return;
      }

      idle.currentTime = 0;
      if (idle.readyState >= 2) playVideo(idle);
      else idle.addEventListener('canplay', () => playVideo(idle), { once: true });
    }

    entry.addEventListener('ended', () => {
      playIdle();
    });

    entry.addEventListener('error', () => {
      playIdle();
    });
    idle.addEventListener('ended', () => {
      idle.currentTime = 0;
      const promise = idle.play();
      if (promise) promise.catch(() => {});
    });

    document.getElementById('close').addEventListener('click', () => {
      chrome.webview.postMessage('close');
    });

    if (hasSource(entry)) {
      if (entry.readyState >= 2) playVideo(entry);
      else entry.addEventListener('canplay', () => playVideo(entry), { once: true });
      entry.load();
    } else if (hasSource(idle)) {
      playIdle();
    } else {
      playSprite();
    }
  </script>
</body>
</html>
""";
    }

    private void CaptureBlurredDesktopBackdrop()
    {
        var blurStrength = Math.Clamp(_settings.OverlayOpacity, 0, 1);
        BlurredDesktopEffect.Radius = blurStrength * MaxBackdropBlurRadius;

        if (ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = PointToScreen(new Point(0, 0));
        var width = Math.Max(1, (int)Math.Round(ActualWidth * transform.M11));
        var height = Math.Max(1, (int)Math.Round(ActualHeight * transform.M22));

        try
        {
            if (handle != 0)
            {
                NativeMethods.SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
            }

            using var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), 0, 0, bitmap.Size);
            }

            var hBitmap = bitmap.GetHbitmap();
            try
            {
                var image = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    nint.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                BlurredDesktopBackdrop.Source = image;
                BlurredDesktopBackdrop.Visibility = Visibility.Visible;
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Failed to capture blurred reminder backdrop");
            BlurredDesktopBackdrop.Source = null;
            BlurredDesktopBackdrop.Visibility = Visibility.Collapsed;
        }
        finally
        {
            if (handle != 0)
            {
                NativeMethods.SetWindowDisplayAffinity(handle, WdaNone);
            }
        }
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

    private static string? TryGetCharacterAssetDirectory(string reminderCharacter)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayRunners", reminderCharacter);
        return Directory.Exists(directory) ? directory : null;
    }

    private static (string? Entry, string? Idle) TryGetCharacterVideoFiles(string? assetDirectory, string reminderCharacter)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory))
        {
            return (null, null);
        }

        var videoDirectory = Path.Combine(assetDirectory, "video");
        if (!Directory.Exists(videoDirectory))
        {
            return (null, null);
        }

        var entryFile = Path.Combine("video", $"{reminderCharacter}_0.webm");
        var idleFile = Path.Combine("video", $"{reminderCharacter}_1.webm");
        return File.Exists(Path.Combine(assetDirectory, entryFile))
            && File.Exists(Path.Combine(assetDirectory, idleFile))
            ? (entryFile.Replace('\\', '/'), idleFile.Replace('\\', '/'))
            : (null, null);
    }

    private static string[] GetCharacterImageFrames(string? assetDirectory, string reminderCharacter)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory) || !Directory.Exists(assetDirectory))
        {
            return [];
        }

        return Directory.GetFiles(assetDirectory, $"{reminderCharacter}_*.png")
            .Where(path => !Path.GetFileName(path).StartsWith($"{reminderCharacter}_overlay_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToArray();
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
        private const int VkShift = 0x10;
        private const int VkSnapshot = 0x2C;
        private const int VkEscape = 0x1B;
        private const int VkLeftShift = 0xA0;
        private const int VkRightShift = 0xA1;
        private const int VkLeftWin = 0x5B;
        private const int VkRightWin = 0x5C;
        private const int VkS = 0x53;
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
                if (IsScreenshotShortcutKey(virtualKey))
                {
                    return CallNextHookEx(_hook, nCode, wParam, lParam);
                }

                if ((message == WmKeyDown || message == WmSysKeyDown) && virtualKey == VkEscape)
                {
                    _onEscape?.Invoke();
                }

                return 1;
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsScreenshotShortcutKey(int virtualKey)
        {
            if (virtualKey == VkSnapshot)
            {
                return true;
            }

            if (virtualKey is VkLeftWin or VkRightWin or VkShift or VkLeftShift or VkRightShift)
            {
                return true;
            }

            return virtualKey == VkS && IsKeyDown(VkShift) && (IsKeyDown(VkLeftWin) || IsKeyDown(VkRightWin));
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
