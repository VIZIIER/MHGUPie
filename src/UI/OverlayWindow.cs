using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MHGUPie.Core;

namespace MHGUPie.UI
{
    /// <summary>
    /// A transparent, always-on-top, click-through overlay window (one per widget, like HunterPie).
    /// In design mode it takes the mouse so it can be dragged, and shows an orange frame.
    /// </summary>
    internal abstract class OverlayWindow : Window
    {
        private readonly Border _frame;
        private readonly TextBlock _designLabel;
        private readonly ContentControl _host;
        private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
        private IntPtr _hwnd;
        private bool _design;
        private string _lastKey;

        protected OverlayWindow(string title)
        {
            Title = title;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;

            _host = new ContentControl();
            _designLabel = Ui.Label(title + "  -  drag to move", 11, Ui.Accent, true);
            _designLabel.Margin = new Thickness(2, 0, 0, 2);
            _designLabel.Visibility = Visibility.Collapsed;
            var stack = new StackPanel();
            stack.Children.Add(_designLabel);
            stack.Children.Add(_host);
            _frame = new Border
            {
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(3),
                Background = Brushes.Transparent,
                Child = stack,
                LayoutTransform = _scale
            };
            Content = _frame;
            SourceInitialized += delegate
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                ApplyClickThrough();
            };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (_design && e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch (InvalidOperationException) { }
                    OnMoved(Left, Top);
                }
            };
        }

        protected abstract void OnMoved(double x, double y);

        public void SetScale(double s)
        {
            s = Math.Max(0.5, Math.Min(2.5, s));
            _scale.ScaleX = s;
            _scale.ScaleY = s;
        }

        public bool DesignMode
        {
            get { return _design; }
            set
            {
                if (_design == value) return;
                _design = value;
                _frame.BorderBrush = value ? Ui.DesignBorder : Brushes.Transparent;
                _frame.Background = value ? Ui.Solid(Colors.Black, 0x40) : Brushes.Transparent;
                _designLabel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                _lastKey = null;
                ApplyClickThrough();
            }
        }

        private void ApplyClickThrough()
        {
            if (_hwnd == IntPtr.Zero) return;
            long ex = Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE).ToInt64();
            ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE;
            if (_design) ex &= ~Native.WS_EX_TRANSPARENT;
            else ex |= Native.WS_EX_TRANSPARENT;
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
        }

        /// <summary>Keeps the overlay above a borderless game window.</summary>
        public void KeepOnTop()
        {
            if (_hwnd != IntPtr.Zero && IsVisible)
                Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        /// <summary>Rebuilds the content only when what's shown changed.</summary>
        protected void SetContent(string key, Func<UIElement> build)
        {
            if (key == _lastKey) return;
            _lastKey = key;
            _host.Content = build();
        }

        public void ForceRefresh() { _lastKey = null; }

        public abstract void Render(GameSnapshot snap, Settings settings, bool visible);

        protected void ShowOrHide(bool show)
        {
            if (show && !IsVisible) Show();
            else if (!show && IsVisible) Hide();
        }
    }
}
