using System;
using System.Collections.Generic;
using System.Linq;
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
        public static readonly DependencyProperty HorizontalWallLinesProperty = DependencyProperty.Register("HorizontalWallLines", typeof(List<float>), typeof(RoomOverlayControl), new PropertyMetadata(new List<float>()));
        public static readonly DependencyProperty VerticalWallLinesProperty = DependencyProperty.Register("VerticalWallLines", typeof(List<float>), typeof(RoomOverlayControl), new PropertyMetadata(new List<float>()));

        public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }
        public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }
        public double OverlayWidth { get => (double)GetValue(OverlayWidthProperty); set => SetValue(OverlayWidthProperty, value); }
        public double OverlayHeight { get => (double)GetValue(OverlayHeightProperty); set => SetValue(OverlayHeightProperty, value); }
        public List<float> HorizontalWallLines { get => (List<float>)GetValue(HorizontalWallLinesProperty); set => SetValue(HorizontalWallLinesProperty, value); }
        public List<float> VerticalWallLines { get => (List<float>)GetValue(VerticalWallLinesProperty); set => SetValue(VerticalWallLinesProperty, value); }

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
            double minW = FloorTrace.Utilities.Constants.MinControlWidth;
            double minH = FloorTrace.Utilities.Constants.MinControlHeight;
            double newW = Math.Max(minW, OverlayWidth + xSign * args.HorizontalChange);
            double newH = Math.Max(minH, OverlayHeight + ySign * args.VerticalChange);
            double newX = X;
            double newY = Y;

            if (xSign < 0) newX += OverlayWidth - newW;
            if (ySign < 0) newY += OverlayHeight - newH;

            // Apply snapping to edges (5 pixel threshold)
            const double snapThreshold = 5.0;
            
            // Snap left edge (X position) to vertical lines
            if (xSign < 0)
            {
                var snappedX = SnapToNearestLine(newX, VerticalWallLines, snapThreshold);
                if (snappedX.HasValue)
                {
                    newW += newX - snappedX.Value;
                    newX = snappedX.Value;
                }
            }
            
            // Snap right edge (X + Width) to vertical lines
            if (xSign > 0)
            {
                var rightEdge = newX + newW;
                var snappedRight = SnapToNearestLine(rightEdge, VerticalWallLines, snapThreshold);
                if (snappedRight.HasValue)
                {
                    newW = snappedRight.Value - newX;
                }
            }
            
            // Snap top edge (Y position) to horizontal lines
            if (ySign < 0)
            {
                var snappedY = SnapToNearestLine(newY, HorizontalWallLines, snapThreshold);
                if (snappedY.HasValue)
                {
                    newH += newY - snappedY.Value;
                    newY = snappedY.Value;
                }
            }
            
            // Snap bottom edge (Y + Height) to horizontal lines
            if (ySign > 0)
            {
                var bottomEdge = newY + newH;
                var snappedBottom = SnapToNearestLine(bottomEdge, HorizontalWallLines, snapThreshold);
                if (snappedBottom.HasValue)
                {
                    newH = snappedBottom.Value - newY;
                }
            }

            // Ensure minimum dimensions are maintained after snapping
            newW = Math.Max(minW, newW);
            newH = Math.Max(minH, newH);

            X = newX;
            Y = newY;
            OverlayWidth = newW;
            OverlayHeight = newH;

            Width = OverlayWidth;
            Height = OverlayHeight;
            Canvas.SetLeft(this, X);
            Canvas.SetTop(this, Y);
            OnOverlayChanged();
        }
        
        private double? SnapToNearestLine(double value, List<float> lines, double threshold)
        {
            if (lines == null || lines.Count == 0)
                return null;
            
            var nearestLine = lines
                .Select(line => new { Line = line, Distance = Math.Abs(line - value) })
                .Where(x => x.Distance <= threshold)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            
            return nearestLine != null ? (double?)nearestLine.Line : null;
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


