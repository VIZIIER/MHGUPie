using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MHGUPie.Core;

namespace MHGUPie.UI
{
    /// <summary>Damage meter: timer, total, DPS, damage per monster and a DPS-over-time graph.</summary>
    internal sealed class DamageMeterWidget : OverlayWindow
    {
        private static readonly Color[] RowColors =
        {
            Color.FromRgb(0xF2, 0xA4, 0x3A), Color.FromRgb(0x5A, 0xA0, 0xE6), Color.FromRgb(0x7F, 0xD0, 0x6A),
            Color.FromRgb(0xE0, 0x5A, 0x8C), Color.FromRgb(0xB4, 0x8C, 0xF0)
        };

        private readonly Settings _settings;

        public DamageMeterWidget(Settings settings) : base("Damage meter")
        {
            _settings = settings;
            Left = settings.Meter.X;
            Top = settings.Meter.Y;
        }

        protected override void OnMoved(double x, double y)
        {
            _settings.Meter.X = x;
            _settings.Meter.Y = y;
            _settings.Save();
        }

        public override void Render(GameSnapshot snap, Settings settings, bool visible)
        {
            var s = settings.Meter;
            DamageSnapshot d = snap.Damage;
            if (d.Total <= 0 && DesignMode) d = Preview();
            bool show = visible && s.Enabled && (d.Total > 0 || DesignMode);
            ShowOrHide(show);
            if (!show) return;
            SetScale(s.Scale);
            Opacity = Math.Max(0.2, Math.Min(1, s.Opacity));
            var key = new StringBuilder();
            key.Append(s.Width).Append(s.ShowTimer).Append(s.ShowTotal).Append(s.ShowDps).Append(s.ShowRows).Append(s.ShowGraph)
               .Append((int)d.Elapsed.TotalSeconds).Append((int)d.Total).Append(d.Running).Append(d.History.Count);
            SetContent(key.ToString(), delegate { return Build(d, s); });
        }

        public static UIElement Build(DamageSnapshot d, DamageMeterSettings s)
        {
            var body = new StackPanel { Width = Math.Max(220, s.Width) };

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            if (s.ShowTimer)
            {
                var t = Ui.Label(Clock(d.Elapsed) + (d.Running ? "" : "  (ended)"), 12, Ui.Dim, false);
                DockPanel.SetDock(t, Dock.Right);
                head.Children.Add(t);
            }
            head.Children.Add(Ui.Label("DAMAGE METER", 12, Ui.Accent, true));
            body.Children.Add(head);

            if (s.ShowTotal || s.ShowDps)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                if (s.ShowTotal)
                {
                    line.Children.Add(Ui.Label("Total ", 11, Ui.Dim, false));
                    line.Children.Add(Ui.Label(Ui.N(d.Total), 15, Ui.Text, true));
                }
                if (s.ShowDps)
                {
                    var l = Ui.Label("DPS ", 11, Ui.Dim, false);
                    l.Margin = new Thickness(s.ShowTotal ? 14 : 0, 0, 0, 0);
                    line.Children.Add(l);
                    line.Children.Add(Ui.Label(d.Dps.ToString("0.0"), 15, Ui.Text, true));
                }
                body.Children.Add(line);
            }

            if (s.ShowRows)
            {
                int i = 0;
                foreach (var r in d.Rows)
                {
                    Color c = RowColors[i++ % RowColors.Length];
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2), Height = 20 };
                    row.Children.Add(Ui.Bar(r.Share, Ui.Gradient(Color.FromArgb(0xC0, c.R, c.G, c.B), Color.FromArgb(0x60, c.R, c.G, c.B)), 20, -1));
                    var txt = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
                    var val = Ui.Label(Ui.N(r.Damage) + "  " + (r.Share * 100).ToString("0") + "%", 11.5, Ui.Text, false);
                    DockPanel.SetDock(val, Dock.Right);
                    txt.Children.Add(val);
                    txt.Children.Add(Ui.Label(r.Name, 11.5, Ui.Text, true));
                    row.Children.Add(txt);
                    body.Children.Add(row);
                }
            }

            if (s.ShowGraph && d.History.Count > 1)
                body.Children.Add(Graph(d.History, Math.Max(220, s.Width), 64));

            return Ui.PanelBox(body);
        }

        private static FrameworkElement Graph(List<KeyValuePair<double, double>> h, double w, double hgt)
        {
            var canvas = new Canvas { Width = w, Height = hgt, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = true };
            canvas.Children.Add(new Rectangle { Width = w, Height = hgt, Fill = Ui.Track, RadiusX = 3, RadiusY = 3 });
            double maxT = h[h.Count - 1].Key, minT = h[0].Key;
            double maxV = Math.Max(1, h.Max(p => p.Value)) * 1.15;
            var line = new Polyline { Stroke = Ui.Accent, StrokeThickness = 1.6 };
            var area = new Polygon { Fill = Ui.Solid(Color.FromRgb(0xF2, 0xA4, 0x3A), 0x2C) };
            area.Points.Add(new Point(0, hgt));
            foreach (var p in h)
            {
                double x = maxT > minT ? (p.Key - minT) / (maxT - minT) * w : 0;
                double y = hgt - p.Value / maxV * (hgt - 6) - 3;
                line.Points.Add(new Point(x, y));
                area.Points.Add(new Point(x, y));
            }
            area.Points.Add(new Point(w, hgt));
            canvas.Children.Add(area);
            canvas.Children.Add(line);
            var peak = Ui.Label("peak " + h.Max(p => p.Value).ToString("0"), 9.5, Ui.Dim, false);
            Canvas.SetLeft(peak, 4);
            Canvas.SetTop(peak, 1);
            canvas.Children.Add(peak);
            return canvas;
        }

        private static string Clock(TimeSpan t)
        {
            return ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00");
        }

        private static DamageSnapshot Preview()
        {
            var d = new DamageSnapshot { Running = true, Elapsed = TimeSpan.FromSeconds(134), Total = 8420 };
            d.Dps = d.Total / 134;
            d.Rows.Add(new DamageRow { Name = "Preview monster", Damage = 6100, Share = 6100 / 8420.0 });
            d.Rows.Add(new DamageRow { Name = "Second monster", Damage = 2320, Share = 2320 / 8420.0 });
            var rnd = new Random(3);
            double v = 40;
            for (int i = 1; i <= 134; i++) { v = Math.Max(10, v + rnd.NextDouble() * 12 - 5.5); d.History.Add(new KeyValuePair<double, double>(i, v)); }
            return d;
        }
    }
}
