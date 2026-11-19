using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MHGUPie.UI
{
    /// <summary>Colours and small builders for the HunterPie-like look (dark glass panels, slim bars).</summary>
    internal static class Ui
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI");
        public static readonly Brush Text = Frozen(Color.FromRgb(0xEE, 0xEE, 0xF0));
        public static readonly Brush Dim = Frozen(Color.FromRgb(0xA8, 0xAD, 0xB8));
        public static readonly Brush Panel = Frozen(Color.FromArgb(0xD8, 0x16, 0x19, 0x20));
        public static readonly Brush PanelBorder = Frozen(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        public static readonly Brush Track = Frozen(Color.FromArgb(0x66, 0x00, 0x00, 0x00));
        public static readonly Brush TrackBorder = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        public static readonly Brush Accent = Frozen(Color.FromRgb(0xF2, 0xA4, 0x3A));
        public static readonly Brush Flinch = Frozen(Color.FromRgb(0xE8, 0x9A, 0x4A));
        public static readonly Brush Break = Frozen(Color.FromRgb(0x9C, 0x7C, 0xF0));
        public static readonly Brush Broken = Frozen(Color.FromRgb(0x5C, 0xC8, 0x7A));
        public static readonly Brush HpHigh = Gradient(Color.FromRgb(0x3F, 0xB8, 0x5E), Color.FromRgb(0x7F, 0xDB, 0x6A));
        public static readonly Brush HpMid = Gradient(Color.FromRgb(0xD9, 0x9A, 0x2B), Color.FromRgb(0xF0, 0xC8, 0x4A));
        public static readonly Brush HpLow = Gradient(Color.FromRgb(0xC8, 0x3A, 0x32), Color.FromRgb(0xEE, 0x6A, 0x4A));
        public static readonly Brush DesignBorder = Frozen(Color.FromRgb(0xF2, 0xA4, 0x3A));

        public static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        public static Brush Gradient(Color a, Color b)
        {
            var g = new LinearGradientBrush(a, b, 0);
            g.Freeze();
            return g;
        }

        public static Brush Solid(Color c, byte alpha)
        {
            return Frozen(Color.FromArgb(alpha, c.R, c.G, c.B));
        }

        public static TextBlock Label(string text, double size, Brush fg, bool bold)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = Font,
                FontSize = size,
                Foreground = fg,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        /// <summary>A slim progress bar: value 0..1, optional marker line (0..1, &lt;0 = none).</summary>
        public static FrameworkElement Bar(double value, Brush fill, double height, double marker)
        {
            value = double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
            var root = new Grid { Height = height, VerticalAlignment = VerticalAlignment.Center };
            root.Children.Add(new Border
            {
                Background = Track,
                BorderBrush = TrackBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(height / 2)
            });
            var fillGrid = new Grid { Margin = new Thickness(1) };
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(value, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - value, GridUnitType.Star) });
            if (value > 0)
                fillGrid.Children.Add(new Border { Background = fill, CornerRadius = new CornerRadius(Math.Max(0, height / 2 - 1)) });
            root.Children.Add(fillGrid);
            if (marker > 0 && marker < 1)
            {
                var mg = new Grid();
                mg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(marker, GridUnitType.Star) });
                mg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - marker, GridUnitType.Star) });
                var line = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.White, Opacity = 0.85 };
                mg.Children.Add(line);
                root.Children.Add(mg);
            }
            return root;
        }

        public static string N(double v) { return v.ToString("#,0", CultureInfo.InvariantCulture); }

        public static Border PanelBox(UIElement child)
        {
            return new Border
            {
                Background = Panel,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Child = child
            };
        }
    }
}
