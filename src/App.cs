using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace PowerUI
{
    internal sealed class App : IDisposable
    {
        private readonly Application application;
        private readonly Window window;
        private readonly Forms.NotifyIcon tray;
        private readonly DispatcherTimer refreshTimer, statusTimer;
        private AppState state;
        private bool busy, disposed, settingsOpen, freshThisRun, statusFreshThisRun, dark, initializing = true;
        private string readError, saveError;
        private Icon icon;
        private string iconLabel;
        private bool iconDark;
        private int iconSize;
        private readonly IntPtr handle;
        private readonly bool? themeOverride;
        private readonly bool keyboard;
        private readonly string statePath;
        private static App mouseApp, keyboardApp;
        private static TrayDisplay trayDisplay;
        private int backdropResult;
        private bool backdropEnabled;

        [STAThread]
        private static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
            if (args.Length == 1 && args[0] == "--about")
            {
                ShowAbout();
                return 0;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\PowerUI.Tray", out created))
            {
                if (!created) return 0;
                try
                {
                    var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    bool captureMode = args.Length >= 2 && args[0] == "--capture";
                    bool? theme = args.Contains("--light") ? (bool?)false : args.Contains("--dark") ? (bool?)true : null;
                    using (var mouse = new App(application, theme, false))
                    using (var keys = new App(application, theme, true))
                    {
                        mouseApp = mouse;
                        keyboardApp = keys;
                        mouse.Update();
                        keys.Update();
                        var app = args.Contains("--keyboard") || trayDisplay == TrayDisplay.Keyboard ? keys : mouse;
                        if (captureMode)
                        {
                            if (!args.Contains("--background")) app.Show();
                            if (args.Contains("--settings")) app.ToggleSettings();
                            var capture = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                            capture.Tick += delegate
                            {
                                capture.Stop();
                                app.Show();
                                application.Dispatcher.BeginInvoke(new Action(delegate
                                {
                                    try { app.Capture(Path.GetFullPath(args[1])); }
                                    catch (Exception error) { File.WriteAllText(args[1] + ".error.log", error.ToString()); }
                                    finally { application.Shutdown(); }
                                }), DispatcherPriority.ContextIdle);
                            };
                            capture.Start();
                        }
                        else if (args.Length >= 1 && args[0] == "--show" && args.All(a => a == "--show" || a == "--keyboard" || a == "--settings"))
                        {
                            app.Show();
                            if (args.Contains("--settings")) app.ToggleSettings();
                        }
                        else if (args.Length != 0) throw new ArgumentException("Usage: PowerUI.exe [--show [--keyboard] [--settings]|--about|--capture path]");
                        else if (stateIsNew(app.state)) app.Show();
                        application.Run();
                    }
                    return 0;
                }
                catch (Exception error)
                {
                    if (args.Length > 0 && args[0] == "--capture")
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerUI.error.log"), error.ToString());
                    else Forms.MessageBox.Show(error.GetBaseException().Message, "PowerUI", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static bool stateIsNew(AppState state) { return state.Percent < 0; }

        private static void ShowAbout()
        {
            var tabs = new TabControl();
            string[] resources = { "README.txt", "LICENSE", "THIRD_PARTY_NOTICES.md" };
            string[] titles = { "使用说明", "MIT License", "第三方声明" };
            for (int index = 0; index < resources.Length; index++)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resources[index]))
                using (var reader = new StreamReader(stream))
                    tabs.Items.Add(new TabItem { Header = titles[index], Content = new TextBox {
                        Text = reader.ReadToEnd(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12)
                    } });
            }
            new Window { Title = "关于 PowerUI", Width = 680, Height = 560, MinWidth = 320, MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = tabs }.ShowDialog();
        }

        private App(Application application, bool? themeOverride, bool keyboard)
        {
            this.application = application;
            this.themeOverride = themeOverride;
            this.keyboard = keyboard;
            statePath = keyboard ? AppState.KeyboardStatePath : AppState.StatePath;
            try { state = AppState.Load(statePath); }
            catch (Exception)
            {
                state = new AppState();
                string backup = statePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                try { File.Copy(statePath, backup); }
                catch (Exception) { saveError = "设置文件损坏且无法备份，暂不保存设置"; }
            }
            if (!keyboard) trayDisplay = state.TrayDisplay;
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shell.xaml"))
                window = (Window)XamlReader.Load(resource);
            window.MaxHeight = SystemParameters.WorkArea.Height - 24;
            handle = new WindowInteropHelper(window).EnsureHandle();
            var source = HwndSource.FromHwnd(handle);
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            var margins = new DesktopNative.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DesktopNative.DwmExtendFrameIntoClientArea(handle, ref margins);
            int corners = 2;
            DesktopNative.DwmSetWindowAttribute(handle, 33, ref corners, 4);
            window.Deactivated += delegate { if (!settingsOpen) window.Hide(); };
            window.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == Key.Escape) window.Hide();
            };
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!disposed) { e.Cancel = true; window.Hide(); }
            };
            Control<Button>("Dismiss").Click += delegate { window.Hide(); };
            Control<Button>("Refresh").Click += delegate { Refresh(); };
            Control<Grid>("BatteryTrack").SizeChanged += delegate { Update(); };
            Control<Button>("Settings").Click += delegate { ToggleSettings(); };
            Control<Button>("Exit").Click += delegate { application.Shutdown(); };
            Control<CheckBox>("Alerts").IsChecked = state.Alerts;
            Control<Slider>("Threshold").Value = state.Threshold;
            Control<CheckBox>("Startup").IsChecked = IsStartupEnabled();
            foreach (TrayDisplay display in Enum.GetValues(typeof(TrayDisplay)))
            {
                var option = Control<RadioButton>("Tray" + display);
                option.Checked += delegate(object sender, RoutedEventArgs e)
                {
                    if (initializing) return;
                    trayDisplay = (TrayDisplay)Enum.Parse(typeof(TrayDisplay), ((RadioButton)sender).Tag.ToString());
                    mouseApp.state.TrayDisplay = trayDisplay;
                    mouseApp.Save();
                    mouseApp.Update();
                    keyboardApp.Update();
                };
            }
            Control<CheckBox>("Alerts").Checked += AlertsChanged;
            Control<CheckBox>("Alerts").Unchecked += AlertsChanged;
            Control<Slider>("Threshold").ValueChanged += delegate
            {
                if (initializing) return;
                state.Threshold = (int)Control<Slider>("Threshold").Value;
                Save();
                Update();
            };
            Control<CheckBox>("Startup").Checked += StartupChanged;
            Control<CheckBox>("Startup").Unchecked += StartupChanged;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开 PowerUI", null, delegate { Show(); });
            menu.Items.Add("刷新电量", null, delegate { Refresh(); });
            menu.Items.Add("查看鼠标", null, delegate { if (mouseApp != null) mouseApp.Show(); });
            menu.Items.Add("查看键盘", null, delegate { if (keyboardApp != null) keyboardApp.Show(); });
            menu.Items.Add("关于 PowerUI", null, delegate { ShowAbout(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { application.Shutdown(); });
            tray = new Forms.NotifyIcon { Text = "PowerUI", ContextMenuStrip = menu };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e)
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    if (window.IsVisible) window.Hide();
                    else Show();
                }
            };
            tray.BalloonTipClicked += delegate { Show(); };
            ApplyTheme();
            tray.Visible = true;
            SystemEvents.UserPreferenceChanged += PreferencesChanged;
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            refreshTimer.Tick += delegate { Refresh(); };
            refreshTimer.Start();
            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            statusTimer.Tick += delegate { Update(); };
            statusTimer.Start();
            initializing = false;
            Update();
            Refresh();
        }

        private T Control<T>(string name) where T : class { return (T)window.FindName(name); }
        private string Model { get { return keyboard ? state.DeviceName ?? "VGN S99" : state.ReceiverVid == 0x046d ? state.DeviceName ?? "罗技鼠标"
            : state.ReceiverPid == 0x1421 ? "雷柏 VT3S MAX" : state.ReceiverPid == 0x1417 ? "雷柏 VT3 MAX" : "无线鼠标"; } }
        private bool Recent { get { return freshThisRun && (keyboard || !state.Charging) && state.Percent >= 0 && DateTime.UtcNow - state.ReadAtUtc <= TimeSpan.FromMinutes(5) && state.ReadAtUtc <= DateTime.UtcNow; } }
        private bool ChargingRecent { get { return statusFreshThisRun && state.Charging && DateTime.UtcNow - state.StatusAtUtc <= TimeSpan.FromMinutes(5); } }

        private void Show()
        {
            ApplyTheme();
            Update();
            window.Show();
            Place();
            window.Activate();
            if (DateTime.UtcNow - state.ReadAtUtc > TimeSpan.FromSeconds(60)) Refresh();
        }

        private void Place()
        {
            var work = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
            double scale = DesktopNative.GetDpiForWindow(handle) / 96.0;
            int width = (int)Math.Ceiling(window.Width * scale);
            int height = (int)Math.Ceiling(Math.Min(window.Height, window.MaxHeight) * scale);
            DesktopNative.SetWindowPos(handle, new IntPtr(-1), work.Right - width - 12, work.Bottom - height - 12, width, height, 0x0010);
        }

        private void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            Control<StackPanel>("SettingsPanel").Visibility = settingsOpen ? Visibility.Visible : Visibility.Collapsed;
            window.Height = settingsOpen ? 612 : 300;
            Place();
        }

        private async void Refresh()
        {
            if (busy || disposed) return;
            busy = true;
            Update();
            try
            {
                var reading = await Task.Run(() => DeviceBattery.Read(5000, keyboard));
                if (disposed) return;
                if (reading == null) readError = "暂未取得新读数";
                else
                {
                    ushort previousVid = state.ReceiverVid == 0 && state.ReceiverPid != 0 ? (ushort)0x24ae : state.ReceiverVid;
                    if (previousVid != reading.ReceiverVid || state.ReceiverPid != reading.ReceiverPid || state.DeviceIndex != reading.DeviceIndex)
                    {
                        state.Alerted = false;
                        state.Percent = -1;
                        state.ReadAtUtc = default(DateTime);
                        freshThisRun = false;
                    }
                    if (reading.Percent >= 0)
                    {
                        state.Percent = reading.Percent;
                        state.ReadAtUtc = reading.ReadAtUtc;
                        freshThisRun = true;
                    }
                    state.Charging = reading.Charging;
                    state.ReceiverPid = reading.ReceiverPid;
                    state.ReceiverVid = reading.ReceiverVid;
                    state.DeviceIndex = reading.DeviceIndex;
                    state.DeviceName = reading.DeviceName;
                    state.StatusAtUtc = reading.ReadAtUtc;
                    statusFreshThisRun = true;
                    readError = null;
                    Control<TextBlock>("Status").ToolTip = null;
                    if (reading.Percent > state.Threshold + 5) state.Alerted = false;
                    if (state.Alerts && reading.Percent >= 0 && reading.Percent <= state.Threshold && !state.Alerted)
                    {
                        state.Alerted = true;
                        // Persist before showing a notification so a restart cannot reissue the same alert.
                        Save();
                        if (saveError == null)
                        {
                            var notificationTray = tray.Visible ? tray : keyboard ? mouseApp.tray : keyboardApp.tray;
                            notificationTray.ShowBalloonTip(6000, "外设电量偏低", Model + " 剩余 " + reading.Percent + "%", Forms.ToolTipIcon.Warning);
                        }
                        else state.Alerted = false;
                    }
                    else Save();
                }
            }
            catch (Exception error)
            {
                readError = "暂未取得新读数";
                Control<TextBlock>("Status").ToolTip = error.GetBaseException().Message;
            }
            finally
            {
                busy = false;
                if (!disposed) Update();
            }
        }

        private void Update()
        {
            if (tray == null || disposed) return;
            bool recent = Recent;
            string label = recent ? state.Percent.ToString() : "--";
            Control<TextBlock>("DeviceName").Text = Model;
            Control<TextBlock>("AppTitle").Text = keyboard ? "PowerUI · 键盘" : "PowerUI · 鼠标";
            Control<TextBlock>("DeviceGlyph").Text = keyboard ? "\uE765" : "\uE962";
            Control<TextBlock>("Connection").Text = state.ReceiverVid == 0x046d
                ? (state.ReceiverPid == 0xb034 ? "蓝牙" : "Logi Bolt · 2.4G 无线") : "2.4G 无线";
            Control<TextBlock>("Battery").Text = label;
            Control<TextBlock>("Percent").Visibility = recent ? Visibility.Visible : Visibility.Collapsed;
            Control<TextBlock>("Charging").Visibility = ChargingRecent ? Visibility.Visible : Visibility.Collapsed;
            Control<Button>("Refresh").IsEnabled = !busy;
            Control<Border>("BatteryFill").Width = recent ? Math.Max(0, Control<Grid>("BatteryTrack").ActualWidth * state.Percent / 100.0) : 0;
            Control<Border>("BatteryFill").Background = new SolidColorBrush(recent && state.Percent <= state.Threshold
                ? MediaColor.FromRgb(218, 122, 39) : MediaColor.FromRgb(21, 159, 124));
            Control<TextBlock>("Status").Text = busy ? "正在读取…" : ChargingRecent ? "充电中" : recent ? readError ?? "已更新" : readError ?? "等待电量";
            Control<TextBlock>("Updated").Text = state.Percent < 0 ? "尚未取得读数" :
                (recent ? "更新于 " : "上次电量 " + state.Percent + "% · ") + state.ReadAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss");
            Control<TextBlock>("ThresholdText").Text = state.Threshold + "%";
            Control<TextBlock>("SaveStatus").Text = saveError ?? "";
            bool wasInitializing = initializing;
            initializing = true;
            foreach (TrayDisplay display in Enum.GetValues(typeof(TrayDisplay)))
                Control<RadioButton>("Tray" + display).IsChecked = display == trayDisplay;
            Control<CheckBox>("Startup").IsChecked = IsStartupEnabled();
            initializing = wasInitializing;
            tray.Visible = trayDisplay == TrayDisplay.Separate
                || (keyboard ? trayDisplay == TrayDisplay.Keyboard : trayDisplay == TrayDisplay.Mouse);
            tray.Text = Model + " · " + (ChargingRecent ? "充电中" : recent ? state.Percent + "%" : "电量未更新");
            bool taskbarDark = ReadPreference("SystemUsesLightTheme", 1) == 0;
            int size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
            if (iconLabel != label || icon == null || iconDark != taskbarDark || iconSize != size)
            {
                var next = TrayIcon.Create(size, recent ? state.Percent : -1, taskbarDark,
                    keyboard ? TrayDevice.Keyboard : TrayDevice.Mouse);
                tray.Icon = next;
                if (icon != null) icon.Dispose();
                icon = next;
                iconLabel = label;
                iconDark = taskbarDark;
                iconSize = size;
            }
        }

        private static int ReadPreference(string name, int fallback)
        {
            return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", name, fallback));
        }

        private void ApplyTheme()
        {
            dark = themeOverride ?? (ReadPreference("AppsUseLightTheme", 1) == 0);
            DesktopNative.PowerStatus power;
            bool saver = DesktopNative.GetSystemPowerStatus(out power) && power.Saver == 1;
            bool acrylic = ReadPreference("EnableTransparency", 1) != 0 && !SystemParameters.HighContrast && !Forms.SystemInformation.TerminalServerSession && !saver;
            int mode = dark ? 1 : 0;
            DesktopNative.DwmSetWindowAttribute(handle, 20, ref mode, 4);
            int backdrop = acrylic ? 3 : 1;
            backdropResult = DesktopNative.DwmSetWindowAttribute(handle, 38, ref backdrop, 4);
            if (backdropResult != 0) acrylic = false;
            backdropEnabled = acrylic;
            var surface = Control<Border>("Surface");
            if (SystemParameters.HighContrast)
            {
                window.Resources["Ink"] = System.Windows.SystemColors.WindowTextBrush;
                window.Resources["Muted"] = System.Windows.SystemColors.WindowTextBrush;
                surface.Background = System.Windows.SystemColors.WindowBrush;
            }
            else
            {
                window.Resources["Ink"] = new SolidColorBrush(dark ? MediaColor.FromRgb(237, 243, 240) : MediaColor.FromRgb(31, 40, 35));
                window.Resources["Muted"] = new SolidColorBrush(dark ? MediaColor.FromRgb(161, 176, 168) : MediaColor.FromRgb(91, 108, 98));
                surface.Background = new SolidColorBrush(dark ? MediaColor.FromArgb(acrylic ? (byte)154 : (byte)255, 26, 33, 29)
                    : MediaColor.FromArgb(acrylic ? (byte)132 : (byte)255, 247, 250, 248));
            }
        }

        private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!disposed) application.Dispatcher.BeginInvoke(new Action(delegate { if (!disposed) { ApplyTheme(); Update(); } }));
        }

        private void AlertsChanged(object sender, RoutedEventArgs e)
        {
            if (initializing) return;
            state.Alerts = Control<CheckBox>("Alerts").IsChecked == true;
            Save();
        }

        private void Save()
        {
            if (saveError == "设置文件损坏且无法备份，暂不保存设置") return;
            try { state.Save(statePath); saveError = null; }
            catch (Exception) { saveError = "无法保存设置，请将便携文件放到可写目录"; }
        }

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private static string StartupCommand { get { return "\"" + Assembly.GetExecutingAssembly().Location + "\""; } }
        private static bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && Convert.ToString(key.GetValue("PowerUI")) == StartupCommand;
        }

        private void StartupChanged(object sender, RoutedEventArgs e)
        {
            if (initializing) return;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (Control<CheckBox>("Startup").IsChecked == true) key.SetValue("PowerUI", StartupCommand);
                    else key.DeleteValue("PowerUI", false);
                }
                saveError = null;
            }
            catch (Exception)
            {
                initializing = true;
                Control<CheckBox>("Startup").IsChecked = IsStartupEnabled();
                initializing = false;
                saveError = "开机启动设置未保存";
            }
            Update();
        }

        private void Capture(string path)
        {
            Update();
            window.UpdateLayout();
            DesktopNative.DwmFlush();
            DesktopNative.Rect rect;
            if (!DesktopNative.GetWindowRect(handle, out rect)) throw new IOException("Cannot locate the app window.");
            using (var bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                if (DesktopNative.GetForegroundWindow() == handle)
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                else
                {
                    // Never capture a different app if this window could not take foreground focus.
                    IntPtr dc = graphics.GetHdc();
                    try { if (!DesktopNative.PrintWindow(handle, dc, 2)) throw new IOException("Window capture failed."); }
                    finally { graphics.ReleaseHdc(dc); }
                }
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            File.WriteAllText(path + ".capture.txt", "Backdrop HRESULT=" + backdropResult + "; acrylic enabled=" + backdropEnabled
                + "; foreground capture=" + (DesktopNative.GetForegroundWindow() == handle)
                + "; DPI=" + DesktopNative.GetDpiForWindow(handle) + "; theme=" + (dark ? "dark" : "light")
                + "; tray mode=" + trayDisplay + "; mouse tray=" + mouseApp.tray.Visible + "; keyboard tray=" + keyboardApp.tray.Visible
                + "; mouse percent=" + mouseApp.state.Percent + "; keyboard percent=" + keyboardApp.state.Percent);
        }

        public void Dispose()
        {
            disposed = true;
            refreshTimer.Stop();
            statusTimer.Stop();
            SystemEvents.UserPreferenceChanged -= PreferencesChanged;
            tray.Visible = false;
            tray.ContextMenuStrip.Dispose();
            tray.Dispose();
            if (icon != null) icon.Dispose();
            window.Close();
        }
    }
}
