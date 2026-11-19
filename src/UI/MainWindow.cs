using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MHGUPie.Core;

namespace MHGUPie.UI
{
    /// <summary>The settings window: connection status, design mode, and an on/off switch for every gauge.</summary>
    internal sealed class MainWindow : Window
    {
        private static readonly Brush Bg = Ui.Frozen(Color.FromRgb(0x12, 0x14, 0x19));
        private static readonly Brush Card = Ui.Frozen(Color.FromRgb(0x1C, 0x1F, 0x27));

        private readonly Settings _settings;
        private readonly GameScanner _scanner;
        private readonly Action _onChanged;
        private readonly TextBlock _status, _process;
        private readonly Button _designBtn, _hideBtn;
        public event Action DesignToggled, HideToggled, ResetMeter, OpenInspector;

        public MainWindow(Settings settings, GameScanner scanner, Action onChanged)
        {
            _settings = settings;
            _scanner = scanner;
            _onChanged = onChanged;
            Title = "MHGUPie " + App.Version + " - overlay for MHGU (MHGURecomp)";
            Width = 560;
            Height = 760;
            Background = Bg;
            Foreground = Ui.Text;
            FontFamily = Ui.Font;

            var root = new DockPanel { Margin = new Thickness(14) };

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var title = Ui.Label("MHGUPie " + App.Version, 22, Ui.Accent, true);
            header.Children.Add(title);
            _status = Ui.Label("Starting...", 13, Ui.Text, false);
            _process = Ui.Label("", 11, Ui.Dim, false);
            header.Children.Add(_status);
            header.Children.Add(_process);

            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            _designBtn = Btn("Design mode (move widgets)", delegate { if (DesignToggled != null) DesignToggled(); });
            _hideBtn = Btn("Hide overlay", delegate { if (HideToggled != null) HideToggled(); });
            buttons.Children.Add(_designBtn);
            buttons.Children.Add(_hideBtn);
            buttons.Children.Add(Btn("Reset damage meter", delegate { if (ResetMeter != null) ResetMeter(); }));
            buttons.Children.Add(Btn("Memory inspector", delegate { if (OpenInspector != null) OpenInspector(); }));
            buttons.Children.Add(Btn("Open config folder", delegate
            {
                try { Process.Start("explorer.exe", "\"" + App.ConfigDir + "\""); } catch (Exception) { }
            }));
            buttons.Children.Add(Btn("Reload names/parts", delegate { _scanner.ReloadNameFiles(); }));
            header.Children.Add(buttons);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var tabs = new TabControl { Background = Bg, BorderBrush = Ui.PanelBorder };
            tabs.Items.Add(Tab("Monster widget", MonsterTab()));
            tabs.Items.Add(Tab("Ailments", AilmentTab()));
            tabs.Items.Add(Tab("Damage meter", MeterTab()));
            tabs.Items.Add(Tab("General", GeneralTab()));
            root.Children.Add(tabs);
            Content = root;
        }

        public void UpdateStatus(GameSnapshot snap, bool design, bool hidden)
        {
            _status.Text = snap.Status;
            _status.Foreground = snap.Attached ? Ui.Broken : Ui.Accent;
            _process.Text = snap.ProcessInfo;
            _designBtn.Content = design ? "Design mode: ON (" + _settings.General.DesignModeKey + ")" : "Design mode (move widgets)";
            _hideBtn.Content = hidden ? "Show overlay" : "Hide overlay";
        }

        // ---------------------------------------------------------------- tabs

        private UIElement MonsterTab()
        {
            var m = _settings.Monster;
            var p = Page();
            Check(p, "Show the monster widget", () => m.Enabled, v => m.Enabled = v);
            Section(p, "Health");
            Check(p, "HP numbers (current / max)", () => m.ShowHpNumbers, v => m.ShowHpNumbers = v);
            Check(p, "HP percentage", () => m.ShowHpPercent, v => m.ShowHpPercent = v);
            Check(p, "Damage dealt to each monster", () => m.ShowDamageTaken, v => m.ShowDamageTaken = v);
            SliderRow(p, "Capture line at % HP (0 = off)", 0, 50, 1, () => m.CaptureLinePercent, v => m.CaptureLinePercent = v);
            Section(p, "Parts");
            Check(p, "Show parts", () => m.ShowParts, v => m.ShowParts = v);
            Check(p, "Flinch gauge (HP left until the part flinches)", () => m.ShowFlinch, v => m.ShowFlinch = v);
            Check(p, "Break gauge (HP left until the part breaks)", () => m.ShowBreak, v => m.ShowBreak = v);
            Check(p, "Only parts you've hit", () => m.OnlyHitParts, v => m.OnlyHitParts = v);
            Check(p, "Hide parts once broken", () => m.HideBrokenParts, v => m.HideBrokenParts = v);
            Section(p, "Layout");
            SliderRow(p, "Monsters shown", 1, 4, 1, () => m.MaxMonsters, v => m.MaxMonsters = (int)v);
            SliderRow(p, "Width", 260, 700, 10, () => m.Width, v => m.Width = v);
            SliderRow(p, "Scale", 0.6, 2.0, 0.05, () => m.Scale, v => m.Scale = v);
            SliderRow(p, "Opacity", 0.2, 1.0, 0.05, () => m.Opacity, v => m.Opacity = v);
            return Scroll(p);
        }

        private UIElement AilmentTab()
        {
            var m = _settings.Monster;
            var p = Page();
            Check(p, "Show ailments (hits until stun, poison, sleep...)", () => m.ShowAilments, v => m.ShowAilments = v);
            Check(p, "Buildup numbers (current / needed)", () => m.ShowAilmentNumbers, v => m.ShowAilmentNumbers = v);
            Check(p, "How many times it happened (x2)", () => m.ShowAilmentCounts, v => m.ShowAilmentCounts = v);
            Check(p, "Timer bar while active", () => m.ShowAilmentTimers, v => m.ShowAilmentTimers = v);
            Check(p, "Only ailments that have started building up", () => m.OnlyBuiltAilments, v => m.OnlyBuiltAilments = v);
            Section(p, "Each ailment");
            foreach (AilmentDef a in _scanner.Map.Ailments)
            {
                string name = a.Name;
                if (!m.AilmentEnabled.ContainsKey(name)) m.AilmentEnabled[name] = true;
                Check(p, name + (a.Verified ? "" : "   (offsets from MHXX, check with the inspector)"),
                      () => m.IsAilmentOn(name), v => m.AilmentEnabled[name] = v);
            }
            var note = Ui.Label("Ailment offsets live in config\\MHGU-1.4.0.map.ini. Add or fix entries there and restart.", 11, Ui.Dim, false);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 10, 0, 0);
            p.Children.Add(note);
            return Scroll(p);
        }

        private UIElement MeterTab()
        {
            var d = _settings.Meter;
            var p = Page();
            Check(p, "Show the damage meter", () => d.Enabled, v => d.Enabled = v);
            Check(p, "Hunt timer", () => d.ShowTimer, v => d.ShowTimer = v);
            Check(p, "Total damage", () => d.ShowTotal, v => d.ShowTotal = v);
            Check(p, "DPS", () => d.ShowDps, v => d.ShowDps = v);
            Check(p, "Damage per monster (bars)", () => d.ShowRows, v => d.ShowRows = v);
            Check(p, "DPS graph", () => d.ShowGraph, v => d.ShowGraph = v);
            Section(p, "Layout");
            SliderRow(p, "Width", 220, 600, 10, () => d.Width, v => d.Width = v);
            SliderRow(p, "Scale", 0.6, 2.0, 0.05, () => d.Scale, v => d.Scale = v);
            SliderRow(p, "Opacity", 0.2, 1.0, 0.05, () => d.Opacity, v => d.Opacity = v);
            var note = Ui.Label("MHGU doesn't record damage per hunter, so the meter counts all HP the large monsters lose " +
                                "(your hits, palico, poison, blast, bombs). Solo, that's your damage.", 11, Ui.Dim, false);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 10, 0, 0);
            p.Children.Add(note);
            return Scroll(p);
        }

        private UIElement GeneralTab()
        {
            var g = _settings.General;
            var p = Page();
            Check(p, "Hide the overlay when the game isn't the active window", () => g.HideWhenGameUnfocused, v => g.HideWhenGameUnfocused = v);
            Check(p, "Start minimized", () => g.StartMinimized, v => g.StartMinimized = v);
            SliderRow(p, "Refresh every (ms)", 50, 500, 10, () => g.PollIntervalMs, v => { g.PollIntervalMs = (int)v; _scanner.PollIntervalMs = (int)v; });
            Section(p, "Hotkeys (work anywhere)");
            p.Children.Add(Ui.Label(g.DesignModeKey + ": design mode    " + g.ToggleOverlayKey + ": hide/show    " + g.ResetMeterKey + ": reset meter", 12, Ui.Text, false));
            var note = Ui.Label("Change the keys in settings.ini (General section) while MHGUPie is closed. " +
                                "Run MHGURecomp in Borderless or Windowed mode (its default) so the overlay can draw on top.", 11, Ui.Dim, false);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 6, 0, 0);
            p.Children.Add(note);
            return Scroll(p);
        }

        // ---------------------------------------------------------------- builders

        private static TabItem Tab(string header, UIElement content)
        {
            return new TabItem { Header = header, Content = content, Foreground = Brushes.Black };
        }

        private static StackPanel Page() { return new StackPanel { Margin = new Thickness(12) }; }

        private UIElement Scroll(StackPanel p)
        {
            return new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Card };
        }

        private static void Section(Panel p, string text)
        {
            var l = Ui.Label(text.ToUpperInvariant(), 11, Ui.Accent, true);
            l.Margin = new Thickness(0, 12, 0, 4);
            p.Children.Add(l);
        }

        private void Check(Panel p, string text, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox
            {
                Content = text,
                IsChecked = get(),
                Foreground = Ui.Text,
                Margin = new Thickness(0, 3, 0, 3),
                FontSize = 13
            };
            c.Checked += delegate { set(true); Changed(); };
            c.Unchecked += delegate { set(false); Changed(); };
            p.Children.Add(c);
        }

        private void SliderRow(Panel p, string text, double min, double max, double step, Func<double> get, Action<double> set)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            var label = Ui.Label(text, 13, Ui.Text, false);
            var value = Ui.Label("", 12, Ui.Dim, false);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            var s = new Slider { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, get())), TickFrequency = step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            Action show = () => value.Text = step < 1 ? s.Value.ToString("0.00") : s.Value.ToString("0");
            show();
            s.ValueChanged += delegate { set(s.Value); show(); Changed(); };
            Grid.SetColumn(s, 1);
            Grid.SetColumn(value, 2);
            g.Children.Add(label);
            g.Children.Add(s);
            g.Children.Add(value);
            p.Children.Add(g);
        }

        private static Button Btn(string text, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 4, 10, 4) };
            b.Click += click;
            return b;
        }

        private void Changed()
        {
            _settings.Save();
            if (_onChanged != null) _onChanged();
        }

        // ---------------------------------------------------------------- hotkeys

        public event Action<int> HotkeyPressed;
        private HwndSource _src;

        public void RegisterHotkeys()
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            _src = HwndSource.FromHwnd(helper.Handle);
            _src.AddHook(WndProc);
            Register(helper.Handle, 1, _settings.General.DesignModeKey);
            Register(helper.Handle, 2, _settings.General.ToggleOverlayKey);
            Register(helper.Handle, 3, _settings.General.ResetMeterKey);
        }

        private static void Register(IntPtr hwnd, int id, string keyName)
        {
            Key k;
            if (string.IsNullOrEmpty(keyName) || !Enum.TryParse(keyName, true, out k)) return;
            Native.RegisterHotKey(hwnd, id, 0x4000 /* MOD_NOREPEAT */, (uint)KeyInterop.VirtualKeyFromKey(k));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && HotkeyPressed != null) // WM_HOTKEY
            {
                HotkeyPressed(wParam.ToInt32());
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
