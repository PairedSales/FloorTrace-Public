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

            MoveThumb.DragDelta += (s, args) =>
            {
                X += args.HorizontalChange;
                Y += args.VerticalChange;
                Canvas.SetLeft(this, X);
                Canvas.SetTop(this, Y);
                OnOverlayChanged();
            };

            TopLeft.DragDelta += (s, args) => ResizeFromCorner(args, -1, -1);
            TopRight.DragDelta += (s, args) => ResizeFromCorner(args, +1, -1);
            BottomLeft.DragDelta += (s, args) => ResizeFromCorner(args, -1, +1);
            BottomRight.DragDelta += (s, args) => ResizeFromCorner(args, +1, +1);
        }

        private void ResizeFromCorner(DragDeltaEventArgs args, int xSign, int ySign)
        {
            var newDimensions = CalculateNewDimensions(args, xSign, ySign);
            
            var snappedDimensions = ApplySnappingToEdges(newDimensions, xSign, ySign);
            
            UpdateOverlayProperties(snappedDimensions);
        }

        /// <summary>
        /// Calculates new dimensions based on drag direction and minimum constraints.
        /// </summary>
        private (double X, double Y, double Width, double Height) CalculateNewDimensions(DragDeltaEventArgs args, int xSign, int ySign)
        {
            double minW = FloorTrace.Utilities.Constants.MinControlWidth;
            double minH = FloorTrace.Utilities.Constants.MinControlHeight;
            double newW = Math.Max(minW, OverlayWidth + xSign * args.HorizontalChange);
            double newH = Math.Max(minH, OverlayHeight + ySign * args.VerticalChange);
            double newX = X;
            double newY = Y;

            if (xSign < 0) newX += OverlayWidth - newW;
            if (ySign < 0) newY += OverlayHeight - newH;

            return (newX, newY, newW, newH);
        }

        /// <summary>
        /// Applies snapping to wall lines for all edges based on resize direction.
        /// </summary>
        private (double X, double Y, double Width, double Height) ApplySnappingToEdges((double X, double Y, double Width, double Height) dimensions, int xSign, int ySign)
        {
            const double snapThreshold = 5.0;
            
            SnapEdgeToLines(dimensions.X, dimensions.Width, VerticalWallLines, snapThreshold, xSign, out var snappedX, out var snappedWidth);
            
            SnapEdgeToLines(dimensions.Y, dimensions.Height, HorizontalWallLines, snapThreshold, ySign, out var snappedY, out var snappedHeight);
            
            // Ensure minimum dimensions are maintained after snapping
            double minW = FloorTrace.Utilities.Constants.MinControlWidth;
            double minH = FloorTrace.Utilities.Constants.MinControlHeight;
            snappedWidth = Math.Max(minW, snappedWidth);
            snappedHeight = Math.Max(minH, snappedHeight);
            
            return (snappedX, snappedY, snappedWidth, snappedHeight);
        }

        /// <summary>
        /// Snaps an edge to the nearest wall line within threshold.
        /// </summary>
        private void SnapEdgeToLines(double position, double size, List<float> lines, double threshold, int direction, 
            out double snappedPosition, out double snappedSize)
        {
            snappedPosition = position;
            snappedSize = size;
            
            if (lines == null || lines.Count == 0)
                return;
            
            if (direction < 0)
            {
                var snappedPos = SnapToNearestLine(position, lines, threshold);
                if (snappedPos.HasValue)
                {
                    snappedSize += position - snappedPos.Value;
                    snappedPosition = snappedPos.Value;
                }
            }
            
            if (direction > 0)
            {
                var endEdge = position + size;
                var snappedEnd = SnapToNearestLine(endEdge, lines, threshold);
                if (snappedEnd.HasValue)
                {
                    snappedSize = snappedEnd.Value - position;
                }
            }
        }

        /// <summary>
        /// Updates overlay properties with new dimensions.
        /// </summary>
        private void UpdateOverlayProperties((double X, double Y, double Width, double Height) dimensions)
        {
            X = dimensions.X;
            Y = dimensions.Y;
            OverlayWidth = dimensions.Width;
            OverlayHeight = dimensions.Height;

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


