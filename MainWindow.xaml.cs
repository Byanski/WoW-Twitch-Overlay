using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace TwitchWoWOverlay
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID_CTRL_SHIFT_T = 9001;
        private const int HOTKEY_ID_CTRL_F9 = 9002;

        private AppSettings _settings = new();
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource = null;
        private bool _isClickThrough = false;
        private bool _isWebViewInitialized = false;
        private System.Windows.Threading.DispatcherTimer? _notificationTimer;

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            ApplyInitialSettings();
        }

        private void ApplyInitialSettings()
        {
            if (_settings.WindowWidth >= MinWidth)
                Width = _settings.WindowWidth;
            if (_settings.WindowHeight >= MinHeight)
                Height = _settings.WindowHeight;

            if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
            {
                // Ensure on-screen bounds
                double left = _settings.WindowLeft.Value;
                double top = _settings.WindowTop.Value;
                if (left >= 0 && left < SystemParameters.VirtualScreenWidth &&
                    top >= 0 && top < SystemParameters.VirtualScreenHeight)
                {
                    Left = left;
                    Top = top;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                }
            }

            OpacitySlider.Value = Math.Clamp(_settings.Opacity, 0.1, 1.0);
            VolumeSlider.Value = Math.Clamp(_settings.Volume, 0.0, 1.0);
            VolumeLabel.Text = _settings.IsMuted ? "0%" : $"{(int)(_settings.Volume * 100)}%";
            MuteButton.Content = _settings.IsMuted || _settings.Volume == 0.0 ? "🔇" : "🔊";
            ChannelInput.Text = _settings.LastChannel;
            if (!string.IsNullOrWhiteSpace(_settings.LastChannel))
            {
                OverlayTitle.Text = $"Twitch • {_settings.LastChannel}";
            }
            UpdatePlaceholder();

            foreach (ComboBoxItem item in ModeSelector.Items)
            {
                if (item.Tag?.ToString() == _settings.ViewMode)
                {
                    ModeSelector.SelectedItem = item;
                    break;
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(HwndHook);

            RegisterGlobalHotkeys();
            ApplyWindowStyle(clickThrough: false);
            ApplyOpacity(_settings.Opacity);

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TwitchWoWOverlay",
                    "WebView2Data"
                );
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await StreamWebView.EnsureCoreWebView2Async(environment);

                StreamWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                StreamWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                StreamWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                await StreamWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    var css = 'html, body { background: transparent !important; }';
                    var head = document.head || document.getElementsByTagName('head')[0];
                    var style = document.createElement('style');
                    style.type = 'text/css';
                    style.appendChild(document.createTextNode(css));
                    if (head) { head.appendChild(style); }
                ");

                StreamWebView.NavigationCompleted += (s, args) =>
                {
                    ApplyWebViewOpacity(OpacitySlider.Value);
                    ApplyVolume(VolumeSlider.Value, _settings.IsMuted);
                };

                _isWebViewInitialized = true;

                LoadingOverlay.Visibility = Visibility.Collapsed;
                NavigateToChannel(_settings.LastChannel, _settings.ViewMode);
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Failed to initialize WebView2:\n{ex.Message}", "Overlay Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            UnregisterGlobalHotkeys();
            _hwndSource?.RemoveHook(HwndHook);

            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Opacity = OpacitySlider.Value;
            _settings.Volume = VolumeSlider.Value;
            _settings.LastChannel = CleanChannelName(ChannelInput.Text);
            _settings.ViewMode = GetCurrentViewMode();
            _settings.Save();
        }

        private void RegisterGlobalHotkeys()
        {
            // Register Ctrl + Shift + T (0x54)
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_CTRL_SHIFT_T,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, 0x54);

            // Register Ctrl + F9 (0x78)
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_CTRL_F9,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, 0x78);
        }

        private void UnregisterGlobalHotkeys()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_CTRL_SHIFT_T);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_CTRL_F9);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_CTRL_SHIFT_T || id == HOTKEY_ID_CTRL_F9)
                {
                    ToggleClickThrough();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void ToggleClickThrough()
        {
            _isClickThrough = !_isClickThrough;
            ApplyWindowStyle(_isClickThrough);

            if (_isClickThrough)
            {
                ControlsRow.Visibility = Visibility.Collapsed;
                ResizeGripThumb.Visibility = Visibility.Collapsed;
                ClickThroughBadge.Visibility = Visibility.Visible;
                ShowNotification("🔒 Click-Through LOCKED (Clicks pass to WoW) • Press [Ctrl + Shift + T] to unlock");
            }
            else
            {
                ControlsRow.Visibility = Visibility.Visible;
                ResizeGripThumb.Visibility = Visibility.Visible;
                ClickThroughBadge.Visibility = Visibility.Collapsed;
                ShowNotification("🔓 Interactive Mode (Move/Resize/Change stream ready)");
                
                NativeMethods.SetForegroundWindow(_hwnd);
                NativeMethods.SetActiveWindow(_hwnd);
                Activate();
                StreamWebView.Focus();
            }
        }

        private void ShowNotification(string message, int durationSeconds = 4)
        {
            NotificationText.Text = message;
            NotificationBanner.Visibility = Visibility.Visible;

            _notificationTimer?.Stop();
            _notificationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            _notificationTimer.Tick += (s, args) =>
            {
                _notificationTimer.Stop();
                NotificationBanner.Visibility = Visibility.Collapsed;
            };
            _notificationTimer.Start();
        }

        private void ApplyWindowStyle(bool clickThrough)
        {
            if (_hwnd == IntPtr.Zero) return;

            long exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_TOPMOST;

            if (clickThrough)
            {
                exStyle |= NativeMethods.WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
            }

            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
            NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

            ApplyOpacity(OpacitySlider.Value);
        }

        private void ApplyOpacity(double value)
        {
            _settings.Opacity = Math.Clamp(value, 0.1, 1.0);

            if (OpacityLabel != null)
            {
                OpacityLabel.Text = $"{(int)(_settings.Opacity * 100)}%";
            }

            // Fade the WPF frame & UI
            this.Opacity = Math.Clamp(_settings.Opacity, 0.15, 1.0);

            // Fade the Twitch video & page content inside WebView2
            ApplyWebViewOpacity(_settings.Opacity);
        }

        private void ApplyWebViewOpacity(double opacity)
        {
            if (!_isWebViewInitialized || StreamWebView.CoreWebView2 == null) return;

            string op = opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string script = $@"
                try {{
                    document.documentElement.style.opacity = '{op}';
                    if (document.body) {{
                        document.body.style.backgroundColor = 'transparent';
                    }}
                }} catch(e) {{}}
            ";
            StreamWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyVolume(e.NewValue, _settings.IsMuted);
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsMuted = !_settings.IsMuted;
            ApplyVolume(VolumeSlider.Value, _settings.IsMuted);
        }

        private void ApplyVolume(double volume, bool isMuted)
        {
            _settings.Volume = Math.Clamp(volume, 0.0, 1.0);
            _settings.IsMuted = isMuted;

            if (VolumeLabel != null)
            {
                VolumeLabel.Text = isMuted ? "0%" : $"{(int)(_settings.Volume * 100)}%";
            }
            if (MuteButton != null)
            {
                MuteButton.Content = isMuted || _settings.Volume == 0.0 ? "🔇" : "🔊";
            }

            if (!_isWebViewInitialized || StreamWebView.CoreWebView2 == null) return;

            string volStr = (isMuted ? 0.0 : _settings.Volume).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string script = $@"
                try {{
                    var vids = document.getElementsByTagName('video');
                    for (var i = 0; i < vids.length; i++) {{
                        vids[i].volume = {volStr};
                        vids[i].muted = {(isMuted ? "true" : "false")};
                    }}
                }} catch(e) {{}}
            ";
            StreamWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void NavigateToChannel(string input, string mode)
        {
            string channel = CleanChannelName(input);
            if (string.IsNullOrWhiteSpace(channel))
            {
                channel = "twitch";
            }

            if (!_isWebViewInitialized || StreamWebView.CoreWebView2 == null) return;

            string url = mode switch
            {
                "full" => $"https://www.twitch.tv/{channel}",
                "chat" => $"https://www.twitch.tv/popout/{channel}/chat?popout=",
                _ => $"https://www.twitch.tv/{channel}/popout"
            };

            OverlayTitle.Text = $"Twitch • {channel}";
            StreamWebView.CoreWebView2.Navigate(url);
        }

        private static string CleanChannelName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            raw = raw.Trim();
            // If it's a URL like https://twitch.tv/channel or twitch.tv/channel/popout
            var match = Regex.Match(raw, @"(?:twitch\.tv/)?([a-zA-Z0-9_]{3,30})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string candidate = match.Groups[1].Value.ToLowerInvariant();
                if (candidate != "popout" && candidate != "directory" && candidate != "videos")
                {
                    return candidate;
                }
            }
            return raw.TrimStart('@', '/').ToLowerInvariant();
        }

        private string GetCurrentViewMode()
        {
            if (ModeSelector.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                return item.Tag.ToString()!;
            }
            return "popout";
        }

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToChannel(ChannelInput.Text, GetCurrentViewMode());
        }

        private void ChannelInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder();
        }

        private void ChannelInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToChannel(ChannelInput.Text, GetCurrentViewMode());
                e.Handled = true;
            }
            UpdatePlaceholder();
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWebViewInitialized) return;
            NavigateToChannel(ChannelInput.Text, GetCurrentViewMode());
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isWebViewInitialized && StreamWebView.CoreWebView2 != null)
            {
                StreamWebView.CoreWebView2.Reload();
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyOpacity(e.NewValue);
        }

        private void LockToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleClickThrough();
        }

        private void DismissNotification_Click(object sender, RoutedEventArgs e)
        {
            NotificationBanner.Visibility = Visibility.Collapsed;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ResizeGripThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;

            if (newWidth >= MinWidth) Width = newWidth;
            if (newHeight >= MinHeight) Height = newHeight;
        }

        private void UpdatePlaceholder()
        {
            ChannelPlaceholder.Visibility = string.IsNullOrEmpty(ChannelInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
