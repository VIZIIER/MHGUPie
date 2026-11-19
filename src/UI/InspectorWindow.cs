using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MHGUPie.Core;

namespace MHGUPie.UI
{
    /// <summary>
    /// Live view of a monster's status block, to check or find offsets (e.g. poison buildup): take a snapshot,
    /// hit the monster with poison, and look at what changed.
    /// </summary>
    internal sealed class InspectorWindow : Window
    {
        private readonly GameScanner _scanner;
        private readonly ComboBox _monster = new ComboBox { Width = 230, Margin = new Thickness(0, 0, 8, 0) };
        private readonly TextBox _start = new TextBox { Text = "0x5C80", Width = 80, Margin = new Thickness(4, 0, 8, 0) };
        private readonly TextBox _length = new TextBox { Text = "0x1A0", Width = 70, Margin = new Thickness(4, 0, 8, 0) };
        private readonly ComboBox _type = new ComboBox { Width = 70, Margin = new Thickness(4, 0, 8, 0) };
        private readonly CheckBox _onlyChanged = new CheckBox { Content = "only changed since snapshot", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBox _out = new TextBox
        {
            FontFamily = new FontFamily("Consolas"), FontSize = 12, IsReadOnly = true,
            Background = Ui.Frozen(Color.FromRgb(0x10, 0x11, 0x15)), Foreground = Ui.Text,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, AcceptsReturn = true
        };
        private byte[] _snapshot;
        private uint _snapshotOf;
        private readonly DispatcherTimer _timer;

        public InspectorWindow(GameScanner scanner)
        {
            _scanner = scanner;
            Title = "MHGUPie memory inspector";
            Width = 760;
            Height = 640;
            Background = Ui.Frozen(Color.FromRgb(0x18, 0x1A, 0x20));
            foreach (string t in new[] { "s16", "u16", "u8", "s32", "f32" }) _type.Items.Add(t);
            _type.SelectedIndex = 0;

            var bar = new WrapPanel { Margin = new Thickness(8) };
            bar.Children.Add(_monster);
            bar.Children.Add(Ui.Label("start", 12, Ui.Text, false));
            bar.Children.Add(_start);
            bar.Children.Add(Ui.Label("length", 12, Ui.Text, false));
            bar.Children.Add(_length);
            bar.Children.Add(Ui.Label("as", 12, Ui.Text, false));
            bar.Children.Add(_type);
            var snap = new Button { Content = "Take snapshot", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
            snap.Click += delegate { TakeSnapshot(); };
            bar.Children.Add(snap);
            bar.Children.Add(_onlyChanged);

            var help = Ui.Label("Offsets are from the monster's status block (the one HP 0x370 is in). Example: snapshot, poison the " +
                                "monster once, tick 'only changed' - the poison buildup offset shows up. Put it in config\\MHGU-1.4.0.map.ini.", 11, Ui.Dim, false);
            help.TextWrapping = TextWrapping.Wrap;
            help.Margin = new Thickness(8, 0, 8, 6);

            var root = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            DockPanel.SetDock(help, Dock.Top);
            root.Children.Add(bar);
            root.Children.Add(help);
            root.Children.Add(_out);
            Content = root;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += delegate { Refresh(); };
            _timer.Start();
            Closed += delegate { _timer.Stop(); };
        }

        private MonsterState Selected(GameSnapshot snap)
        {
            var names = snap.Monsters.Select(m => "slot " + m.Slot + ": " + m.Name + "  @" + m.Status.ToString("X8")).ToList();
            var current = _monster.SelectedItem as string;
            if (!_monster.Items.Cast<string>().SequenceEqual(names))
            {
                _monster.Items.Clear();
                foreach (var n in names) _monster.Items.Add(n);
                _monster.SelectedItem = current != null && names.Contains(current) ? current : (names.Count > 0 ? names[0] : null);
            }
            int i = _monster.SelectedIndex;
            return i >= 0 && i < snap.Monsters.Count ? snap.Monsters[i] : null;
        }

        private bool Range(out int start, out int len)
        {
            long s, l;
            bool ok = Ini.TryParseNum(_start.Text, out s) & Ini.TryParseNum(_length.Text, out l);
            start = (int)Math.Max(0, s);
            len = (int)Math.Max(2, Math.Min(0x4000, l));
            return ok;
        }

        private void TakeSnapshot()
        {
            var m = Selected(_scanner.Latest);
            int start, len;
            if (m == null || !Range(out start, out len)) return;
            _snapshot = _scanner.Memory.ReadBytes(m.Status + (uint)start, len);
            _snapshotOf = m.Status + (uint)start;
        }

        private void Refresh()
        {
            GameSnapshot snap = _scanner.Latest;
            if (!snap.Attached) { _out.Text = "Not connected to the game."; return; }
            var m = Selected(snap);
            if (m == null) { _out.Text = "No large monster right now. Start a hunt."; return; }
            int start, len;
            if (!Range(out start, out len)) { _out.Text = "Start/length must be numbers (hex like 0x5D76 or decimal)."; return; }
            byte[] b = _scanner.Memory.ReadBytes(m.Status + (uint)start, len);
            if (b == null) { _out.Text = "Read failed."; return; }
            bool cmp = _snapshot != null && _snapshotOf == m.Status + (uint)start && _snapshot.Length == b.Length;
            string type = (string)_type.SelectedItem;
            int size = type == "u8" ? 1 : (type == "s16" || type == "u16") ? 2 : 4;
            var sb = new StringBuilder();
            sb.AppendLine(m.Name + "   HP " + m.Hp + "/" + m.MaxHp + "   status block guest 0x" + m.Status.ToString("X8"));
            sb.AppendLine();
            int shown = 0;
            for (int o = 0; o + size <= b.Length; o += size)
            {
                string now = Fmt(b, o, type);
                string was = cmp ? Fmt(_snapshot, o, type) : null;
                bool changed = cmp && now != was;
                if (_onlyChanged.IsChecked == true && !changed) continue;
                sb.Append("0x").Append((start + o).ToString("X4")).Append("  ").Append(now.PadLeft(12));
                if (changed) sb.Append("   was ").Append(was);
                sb.AppendLine();
                if (++shown > 2000) break;
            }
            string text = sb.ToString();
            if (_out.Text != text)
            {
                double off = _out.VerticalOffset;
                _out.Text = text;
                _out.ScrollToVerticalOffset(off);
            }
        }

        private static string Fmt(byte[] b, int o, string t)
        {
            switch (t)
            {
                case "u8": return b[o].ToString();
                case "s16": return BitConverter.ToInt16(b, o).ToString();
                case "u16": return BitConverter.ToUInt16(b, o).ToString();
                case "s32": return BitConverter.ToInt32(b, o).ToString();
                default: return BitConverter.ToSingle(b, o).ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }
}
