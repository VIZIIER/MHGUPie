using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MHGUPie.Core;

namespace MHGUPie.UI
{
    /// <summary>Monster HP + parts + ailments widget (HunterPie's monster widget for MHGU).</summary>
    internal sealed class MonsterWidget : OverlayWindow
    {
        private readonly Settings _settings;

        public MonsterWidget(Settings settings) : base("Monsters")
        {
            _settings = settings;
            Left = settings.Monster.X;
            Top = settings.Monster.Y;
        }

        protected override void OnMoved(double x, double y)
        {
            _settings.Monster.X = x;
            _settings.Monster.Y = y;
            _settings.Save();
        }

        public override void Render(GameSnapshot snap, Settings settings, bool visible)
        {
            var s = settings.Monster;
            List<MonsterState> list = snap.Monsters.Take(Math.Max(1, s.MaxMonsters)).ToList();
            if (list.Count == 0 && DesignMode) list = Preview(snap);
            bool show = visible && s.Enabled && list.Count > 0;
            ShowOrHide(show);
            if (!show) return;
            SetScale(s.Scale);
            Opacity = Math.Max(0.2, Math.Min(1, s.Opacity));
            SetContent(Key(list, s), delegate { return Build(list, s); });
        }

        private static string Key(List<MonsterState> list, MonsterWidgetSettings s)
        {
            var sb = new StringBuilder();
            sb.Append(s.Width).Append(s.ShowHpNumbers).Append(s.ShowHpPercent).Append(s.ShowParts).Append(s.ShowFlinch)
              .Append(s.ShowBreak).Append(s.OnlyHitParts).Append(s.HideBrokenParts).Append(s.ShowAilments).Append(s.OnlyBuiltAilments)
              .Append(s.ShowAilmentNumbers).Append(s.ShowAilmentCounts).Append(s.ShowAilmentTimers).Append(s.CaptureLinePercent)
              .Append(s.ShowDamageTaken).Append(string.Join(",", s.AilmentEnabled.Select(kv => kv.Key + kv.Value)));
            foreach (var m in list)
            {
                sb.Append('|').Append(m.Name).Append(m.Hp).Append('/').Append(m.MaxHp).Append(m.Dead).Append((int)m.DamageTaken);
                foreach (var p in m.Parts) sb.Append(p.FlinchHp).Append(',').Append(p.FlinchCount).Append(',').Append(p.BreakHp).Append(',').Append(p.BreakCount).Append(p.Name).Append(';');
                foreach (var a in m.Ailments) sb.Append(a.Buildup).Append(',').Append(a.Threshold).Append(',').Append(a.Count).Append(',').Append(a.Timer / 15).Append(';');
            }
            return sb.ToString();
        }

        public static UIElement Build(List<MonsterState> list, MonsterWidgetSettings s)
        {
            var stack = new StackPanel { Width = Math.Max(260, s.Width) };
            foreach (var m in list)
            {
                var card = BuildMonster(m, s);
                card.Margin = new Thickness(0, 0, 0, 6);
                stack.Children.Add(card);
            }
            return stack;
        }

        private static FrameworkElement BuildMonster(MonsterState m, MonsterWidgetSettings s)
        {
            var body = new StackPanel();

            // name + HP text
            var head = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 3) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(right, Dock.Right);
            if (s.ShowHpNumbers)
                right.Children.Add(Ui.Label(m.Dead ? "0 / " + Ui.N(m.MaxHp) : Ui.N(m.Hp) + " / " + Ui.N(m.MaxHp), 12, Ui.Text, false));
            if (s.ShowHpPercent)
            {
                var pct = Ui.Label((m.HealthPercent * 100).ToString("0.0") + "%", 12, Ui.Dim, false);
                pct.Margin = new Thickness(8, 0, 0, 0);
                right.Children.Add(pct);
            }
            head.Children.Add(right);
            head.Children.Add(Ui.Label(m.Dead ? m.Name + "  (dead / captured)" : m.Name, 14, m.Dead ? Ui.Dim : Ui.Text, true));
            body.Children.Add(head);

            double hp = m.HealthPercent;
            Brush hpBrush = hp > 0.5 ? Ui.HpHigh : hp > 0.2 ? Ui.HpMid : Ui.HpLow;
            body.Children.Add(Ui.Bar(hp, hpBrush, 12, s.CaptureLinePercent > 0 ? s.CaptureLinePercent / 100.0 : -1));

            if (s.ShowDamageTaken && m.DamageTaken > 0)
            {
                var dt = Ui.Label("Damage dealt: " + Ui.N(m.DamageTaken), 11, Ui.Dim, false);
                dt.Margin = new Thickness(0, 2, 0, 0);
                body.Children.Add(dt);
            }

            // ailments
            if (s.ShowAilments)
            {
                var ails = m.Ailments.Where(a => s.IsAilmentOn(a.Def.Name) && (!s.OnlyBuiltAilments || a.EverBuilt)).ToList();
                if (ails.Count > 0)
                {
                    var g = TwoColumnGrid(ails.Count);
                    for (int i = 0; i < ails.Count; i++) Place(g, AilmentRow(ails[i], s), i);
                    g.Margin = new Thickness(0, 6, 0, 0);
                    body.Children.Add(g);
                }
            }

            // parts
            if (s.ShowParts && (s.ShowFlinch || s.ShowBreak))
            {
                var parts = m.Parts.Where(p => (!s.OnlyHitParts || p.EverHit) && !(s.HideBrokenParts && p.Broken)).ToList();
                if (parts.Count > 0)
                {
                    var g = TwoColumnGrid(parts.Count);
                    for (int i = 0; i < parts.Count; i++) Place(g, PartRow(parts[i], s), i);
                    g.Margin = new Thickness(0, 6, 0, 0);
                    body.Children.Add(g);
                }
            }

            return Ui.PanelBox(body);
        }

        private static Grid TwoColumnGrid(int count)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < (count + 1) / 2; r++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return g;
        }

        private static void Place(Grid g, UIElement e, int i)
        {
            Grid.SetRow(e, i / 2);
            Grid.SetColumn(e, (i % 2) * 2);
            g.Children.Add(e);
        }

        private static FrameworkElement AilmentRow(AilmentState a, MonsterWidgetSettings s)
        {
            var color = new SolidColorBrush(a.Def.Color);
            color.Freeze();
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            var top = new DockPanel();
            var info = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(info, Dock.Right);
            if (s.ShowAilmentNumbers) info.Children.Add(Ui.Label(a.Buildup + "/" + a.Threshold, 10.5, Ui.Dim, false));
            if (s.ShowAilmentCounts && a.Count > 0)
            {
                var c = Ui.Label("x" + a.Count, 10.5, color, true);
                c.Margin = new Thickness(6, 0, 0, 0);
                info.Children.Add(c);
            }
            top.Children.Add(info);
            string name = a.Def.Name + (a.Def.Verified ? "" : "");
            top.Children.Add(Ui.Label(name + (a.Active ? "  (active)" : ""), 11.5, color, a.Active));
            sp.Children.Add(top);
            double v;
            Brush fill = color;
            if (a.Active && s.ShowAilmentTimers && a.MaxTimer > 0)
            {
                v = a.Timer / (double)a.MaxTimer;
                fill = Ui.Gradient(a.Def.Color, Colors.White);
            }
            else v = a.Threshold > 0 ? a.Buildup / (double)a.Threshold : 0;
            sp.Children.Add(Ui.Bar(v, fill, 5, -1));
            return sp;
        }

        private static FrameworkElement PartRow(PartState p, MonsterWidgetSettings s)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            var top = new DockPanel();
            var info = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(info, Dock.Right);
            if (s.ShowFlinch && p.FlinchCount > 0) info.Children.Add(Ui.Label("flinch x" + p.FlinchCount, 10.5, Ui.Flinch, false));
            if (s.ShowBreak && p.Breakable)
            {
                var b = Ui.Label(p.Broken ? "BROKEN" : "break " + Math.Max(0, (int)p.BreakHp), 10.5, p.Broken ? Ui.Broken : Ui.Break, p.Broken);
                b.Margin = new Thickness(6, 0, 0, 0);
                info.Children.Add(b);
            }
            top.Children.Add(info);
            top.Children.Add(Ui.Label(p.Name, 11.5, Ui.Text, false));
            sp.Children.Add(top);
            if (s.ShowFlinch && p.MaxFlinchHp > 0)
                sp.Children.Add(Ui.Bar(p.FlinchHp / (double)p.MaxFlinchHp, Ui.Flinch, 4, -1));
            if (s.ShowBreak && p.Breakable && !p.Broken && p.MaxBreakHp > 0)
            {
                var bb = Ui.Bar(p.BreakHp / (double)p.MaxBreakHp, Ui.Break, 4, -1);
                bb.Margin = new Thickness(0, 2, 0, 0);
                sp.Children.Add(bb);
            }
            return sp;
        }

        /// <summary>Example monster shown in design mode while there's no hunt, so the widget can be placed.</summary>
        private static List<MonsterState> Preview(GameSnapshot snap)
        {
            var m = new MonsterState { Name = "Preview monster", Hp = 7300, MaxHp = 12000 };
            string[] names = { "Head", "Body", "Left wing", "Right wing", "Tail", "Legs" };
            for (int i = 0; i < names.Length; i++)
                m.Parts.Add(new PartState { Index = i, Name = names[i], FlinchHp = 100 + 40 * i, MaxFlinchHp = 350, FlinchCount = i % 3, BreakHp = i == 4 ? 0 : 200, MaxBreakHp = 400, Breakable = i != 1, Broken = i == 4, EverHit = true });
            string[] an = { "Poison", "Paralysis", "Sleep", "Stun", "Exhaust", "Blast" };
            Color[] ac = { Color.FromRgb(0xB4, 0x5A, 0xD2), Color.FromRgb(0xE8, 0xD2, 0x3C), Color.FromRgb(0x5A, 0xA0, 0xE6), Color.FromRgb(0xF0, 0xE6, 0xA0), Color.FromRgb(0x7F, 0xD0, 0xC8), Color.FromRgb(0xF0, 0x6A, 0x3C) };
            for (int i = 0; i < an.Length; i++)
                m.Ailments.Add(new AilmentState { Def = new AilmentDef { Name = an[i], Color = ac[i], Source = "verified" }, Buildup = 30 + 25 * i, Threshold = 200, Count = i % 2, EverBuilt = true });
            return new List<MonsterState> { m };
        }
    }
}
