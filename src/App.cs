using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MHGUPie.Core;
using MHGUPie.UI;

namespace MHGUPie
{
    internal static class App
    {
        public const string Version = "1.0";
        public static string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        public static string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        /// <summary>Where settings.ini and the error log go: next to the exe, or %APPDATA%\MHGUPie if that folder is read-only.</summary>
        public static string DataDir = AppDomain.CurrentDomain.BaseDirectory;

        private static Settings _settings;
        private static GameScanner _scanner;
        private static List<OverlayWindow> _overlays = new List<OverlayWindow>();
        private static MainWindow _main;
        private static InspectorWindow _inspector;
        private static bool _design;
        private static System.Threading.Mutex _single;

        [STAThread]
        public static int Main(string[] args)
        {
            string mapPath = Path.Combine(ConfigDir, "MHGU-1.4.0.map.ini");
            if (!File.Exists(mapPath))
            {
                MessageBox.Show("Missing " + mapPath + "\nKeep the config folder next to MHGUPie.exe.", "MHGUPie");
                return 1;
            }
            bool snapshot = Array.IndexOf(args, "--snapshot") >= 0;
            if (!snapshot)
            {
                bool first;
                _single = new System.Threading.Mutex(true, "MHGUPie.SingleInstance", out first);
                if (!first) return 0; // already running (e.g. started again by the launcher)
            }
            DataDir = PickDataDir();
            AddressMap map = AddressMap.Load(mapPath);
            _settings = Settings.Load(Path.Combine(DataDir, "settings.ini"));
            foreach (var a in map.Ailments)
                if (!_settings.Monster.AilmentEnabled.ContainsKey(a.Name)) _settings.Monster.AilmentEnabled[a.Name] = true;
            FitToScreen(_settings, !File.Exists(Path.Combine(DataDir, "settings.ini")));

            _scanner = new GameScanner(map, ConfigDir);
            _scanner.PollIntervalMs = Math.Max(30, _settings.General.PollIntervalMs);
            if (!string.IsNullOrEmpty(_settings.General.GameDataPath)) _scanner.GameDataPathOverride = _settings.General.GameDataPath;

            int si = Array.IndexOf(args, "--snapshot");
            if (si >= 0 && si + 1 < args.Length) return SnapshotMode(args[si + 1], args);

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                LogError(e.Exception);
                e.Handled = true; // keep the overlay running; the error is in MHGUPie-error.log
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
            _scanner.Start();

            _overlays.Add(new MonsterWidget(_settings));
            _overlays.Add(new DamageMeterWidget(_settings));

            _main = new MainWindow(_settings, _scanner, delegate { foreach (var o in _overlays) o.ForceRefresh(); });
            _main.DesignToggled += ToggleDesign;
            _main.HideToggled += ToggleHide;
            _main.ResetMeter += delegate { _scanner.Damage.Reset(); };
            _main.OpenInspector += delegate
            {
                if (_inspector == null || !_inspector.IsLoaded) { _inspector = new InspectorWindow(_scanner); _inspector.Owner = _main; }
                _inspector.Show();
                _inspector.Activate();
            };
            _main.HotkeyPressed += delegate(int id)
            {
                if (id == 1) ToggleDesign();
                else if (id == 2) ToggleHide();
                else if (id == 3) _scanner.Damage.Reset();
            };
            _main.SourceInitialized += delegate { _main.RegisterHotkeys(); };
            _main.Closed += delegate { _settings.Save(); foreach (var o in _overlays) o.Close(); _scanner.Dispose(); };
            if (_settings.General.StartMinimized) _main.WindowState = WindowState.Minimized;

            bool closeWithGame = Array.IndexOf(args, "--close-with-game") >= 0;
            bool wasAttached = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            int ticks = 0;
            timer.Tick += delegate
            {
                GameSnapshot snap = _scanner.Latest;
                if (snap.Attached) wasAttached = true;
                else if (closeWithGame && wasAttached) { _main.Close(); return; }
                bool visible = !_settings.General.HideAllOverlays &&
                               (_design || (snap.Attached && (!_settings.General.HideWhenGameUnfocused || snap.GameFocused)));
                foreach (var o in _overlays)
                {
                    o.DesignMode = _design;
                    o.Render(snap, _settings, visible);
                }
                if (++ticks % 10 == 0)
                {
                    _main.UpdateStatus(snap, _design, _settings.General.HideAllOverlays);
                    foreach (var o in _overlays) o.KeepOnTop();
                }
            };
            timer.Start();
            return app.Run(_main);
        }

        private static void LogError(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(DataDir, "MHGUPie-error.log"), DateTime.Now + "  " + ex + Environment.NewLine + Environment.NewLine); }
            catch (Exception) { }
        }

        private static string PickDataDir()
        {
            try
            {
                string probe = Path.Combine(AppDir, ".write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return AppDir;
            }
            catch (Exception)
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MHGUPie");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// <summary>First run: place the widgets for this PC's screen. Later runs: pull back widgets that are off-screen.</summary>
        private static void FitToScreen(Settings s, bool firstRun)
        {
            double sw = SystemParameters.VirtualScreenWidth, sh = SystemParameters.VirtualScreenHeight;
            double sl = SystemParameters.VirtualScreenLeft, st = SystemParameters.VirtualScreenTop;
            double pw = SystemParameters.PrimaryScreenWidth, ph = SystemParameters.PrimaryScreenHeight;
            if (firstRun || s.Monster.X < sl || s.Monster.X > sl + sw - 80 || s.Monster.Y < st || s.Monster.Y > st + sh - 80)
            {
                s.Monster.X = Math.Max(0, pw - s.Monster.Width * s.Monster.Scale - 40);
                s.Monster.Y = Math.Round(ph * 0.08);
            }
            if (firstRun || s.Meter.X < sl || s.Meter.X > sl + sw - 80 || s.Meter.Y < st || s.Meter.Y > st + sh - 80)
            {
                s.Meter.X = 40;
                s.Meter.Y = Math.Round(ph * 0.35);
            }
        }

        private static void ToggleDesign()
        {
            _design = !_design;
            if (!_design) _settings.Save();
        }

        private static void ToggleHide()
        {
            _settings.General.HideAllOverlays = !_settings.General.HideAllOverlays;
            _settings.Save();
        }

        /// <summary>
        /// Test mode: connect, wait, then render both widgets to PNG files and dump what was read to a text file.
        ///   MHGUPie.exe --snapshot &lt;folder&gt; [--wait seconds]
        /// </summary>
        private static int SnapshotMode(string outDir, string[] args)
        {
            int wi = Array.IndexOf(args, "--wait");
            double wait = 6;
            if (wi >= 0 && wi + 1 < args.Length) double.TryParse(args[wi + 1], out wait);
            Directory.CreateDirectory(outDir);
            _scanner.Start();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(wait) };
            int code = 0;
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    GameSnapshot snap = _scanner.Latest;
                    var sb = new StringBuilder();
                    sb.AppendLine("Attached: " + snap.Attached + "  " + snap.ProcessInfo);
                    sb.AppendLine("Status: " + snap.Status);
                    foreach (var m in snap.Monsters)
                    {
                        sb.AppendLine("Monster slot " + m.Slot + " type " + m.TypeId + " '" + m.Name + "' HP " + m.Hp + "/" + m.MaxHp + " status @" + m.Status.ToString("X8"));
                        foreach (var p in m.Parts)
                            sb.AppendLine("  part " + p.Index + " " + p.Name + " flinch " + p.FlinchHp + "/" + p.MaxFlinchHp + " x" + p.FlinchCount + " break " + p.BreakHp + "/" + p.MaxBreakHp + " x" + p.BreakCount + (p.Broken ? " BROKEN" : ""));
                        foreach (var a in m.Ailments)
                            sb.AppendLine("  ailment " + a.Def.Name + " " + a.Buildup + "/" + a.Threshold + " x" + a.Count + " timer " + a.Timer + "/" + a.MaxTimer);
                    }
                    var d = snap.Damage;
                    sb.AppendLine("Damage total " + d.Total + " dps " + d.Dps.ToString("0.0") + " elapsed " + d.Elapsed + " rows " + string.Join(", ", d.Rows.Select(r => r.Name + "=" + r.Damage)));
                    File.WriteAllText(Path.Combine(outDir, "snapshot.txt"), sb.ToString());
                    if (snap.Monsters.Count > 0)
                        RenderPng(MonsterWidget.Build(snap.Monsters.Take(_settings.Monster.MaxMonsters).ToList(), _settings.Monster), Path.Combine(outDir, "monster_widget.png"));
                    if (d.Total > 0)
                        RenderPng(DamageMeterWidget.Build(d, _settings.Meter), Path.Combine(outDir, "damage_meter.png"));
                    var win = new MainWindow(_settings, _scanner, null) { Left = -20000, Top = -20000, ShowActivated = false };
                    win.Show();
                    win.UpdateStatus(snap, false, false);
                    win.UpdateLayout();
                    var content = (FrameworkElement)win.Content;
                    var bmp = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    var vis = new DrawingVisual();
                    using (var dc = vis.RenderOpen())
                    {
                        dc.DrawRectangle(win.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                        dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                    }
                    bmp.Render(vis);
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(bmp));
                    using (var f = File.Create(Path.Combine(outDir, "settings_window.png"))) enc.Save(f);
                    win.Close();
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(outDir, "error.txt"), ex.ToString());
                    code = 2;
                }
                _scanner.Dispose();
                app.Shutdown(code);
            };
            t.Start();
            app.Run();
            return code;
        }

        private static void RenderPng(UIElement e, string path)
        {
            var host = new System.Windows.Controls.Border { Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x4A)), Padding = new Thickness(12), Child = e };
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            host.Arrange(new Rect(host.DesiredSize));
            host.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth), (int)Math.Ceiling(host.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(host);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var f = File.Create(path)) enc.Save(f);
        }
    }
}
