using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FloorTrace.Controls
{
    public partial class RoomOverlayControl : System.Windows.Controls.UserControl
    {
        public RoomOverlayControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public event EventHandler? OverlayChanged;

        public static readonly DependencyProperty XProperty = DependencyProperty.Register("X", typeof(double), typeof(RoomOverlayControl), new PropertyMetadata(0.0, OnPosChanged));
        public static readonly DependencyProperty YProperty = DependencyProperty.Register("Y", typeof(double), typeof(RoomOverlayControl), new PropertyMetadata(0.0, OnPosChanged));
        public static readonly DependencyProperty OverlayWidthProperty = DependencyProperty.Register("OverlayWidth", typeof(double), typeof(RoomOverlayControl), new PropertyMetadata(100.0, OnSizeChanged));
        public static readonly DependencyProperty OverlayHeightProperty = DependencyProperty.Register("OverlayHeight", typeof(double), typeof(RoomOverlayControl), new PropertyMetadata(80.0, OnSizeChanged));

        public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }
        public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }
        public double OverlayWidth { get => (double)GetValue(OverlayWidthProperty); set => SetValue(OverlayWidthProperty, value); }
        public double OverlayHeight { get => (double)GetValue(OverlayHeightProperty); set => SetValue(OverlayHeightProperty, value); }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Canvas.SetLeft(this, X);
            Canvas.SetTop(this, Y);
            Width = OverlayWidth;
            Height = OverlayHeight;

            // Move handler
            MoveThumb.DragDelta += (s, args) =>
            {
                X += args.HorizontalChange;
                Y += args.VerticalChange;
                Canvas.SetLeft(this, X);
                Canvas.SetTop(this, Y);
                OnOverlayChanged();
            };

            // Resize handlers
            TopLeft.DragDelta += (s, args) => ResizeFromCorner(args, -1, -1);
            TopRight.DragDelta += (s, args) => ResizeFromCorner(args, +1, -1);
            BottomLeft.DragDelta += (s, args) => ResizeFromCorner(args, -1, +1);
            BottomRight.DragDelta += (s, args) => ResizeFromCorner(args, +1, +1);
        }

        private void ResizeFromCorner(DragDeltaEventArgs args, int xSign, int ySign)
        {
            const double minW = 20, minH = 20;
            double newW = Math.Max(minW, OverlayWidth + xSign * args.HorizontalChange);
            double newH = Math.Max(minH, OverlayHeight + ySign * args.VerticalChange);

            if (xSign < 0) X += OverlayWidth - newW;
            if (ySign < 0) Y += OverlayHeight - newH;

            OverlayWidth = newW;
            OverlayHeight = newH;

            Width = OverlayWidth;
            Height = OverlayHeight;
            Canvas.SetLeft(this, X);
            Canvas.SetTop(this, Y);
            OnOverlayChanged();
        }

        private static void OnPosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoomOverlayControl c)
            {
                Canvas.SetLeft(c, c.X);
                Canvas.SetTop(c, c.Y);
            }
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoomOverlayControl c)
            {
                c.Width = c.OverlayWidth;
                c.Height = c.OverlayHeight;
                c.OnOverlayChanged();
            }
        }


        private void OnOverlayChanged()
        {
            OverlayChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}


