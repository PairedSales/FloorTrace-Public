using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FloorTrace.Controls
{
    public partial class PerimeterOverlayControl : System.Windows.Controls.UserControl
    {
        private List<PointF> _points = new List<PointF>();
        private Polygon? _polygon;
        private List<Ellipse> _vertices = new List<Ellipse>();
        private Ellipse? _draggedVertex = null;
        private int _draggedVertexIndex = -1;
        private bool _isEditable = true;

        public event EventHandler? PerimeterChanged;

        public bool IsEditable
        {
            get => _isEditable;
            set
            {
                _isEditable = value;
                UpdateVertexVisibility();
            }
        }

        public PerimeterOverlayControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void SetPoints(List<PointF> points)
        {
            _points = new List<PointF>(points);
            RenderPerimeter();
        }

        public List<PointF> GetPoints()
        {
            return new List<PointF>(_points);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RenderPerimeter();
        }

        private void RenderPerimeter()
        {
            PerimeterCanvas.Children.Clear();
            _vertices.Clear();

            if (_points.Count < 3)
                return;

            // Create the polygon
            _polygon = new Polygon
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 0, 120, 215)), // Semi-transparent blue
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)), // Blue
                StrokeThickness = 3,
                Points = new PointCollection(_points.Select(p => new System.Windows.Point(p.X, p.Y)))
            };

            _polygon.MouseDown += Polygon_MouseDown;
            PerimeterCanvas.Children.Add(_polygon);

            // Create vertex thumbs
            for (int i = 0; i < _points.Count; i++)
            {
                var vertex = CreateVertex(_points[i], i);
                _vertices.Add(vertex);
                PerimeterCanvas.Children.Add(vertex);
            }
        }

        private Ellipse CreateVertex(PointF point, int index)
        {
            var vertex = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)), // Blue
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = index
            };

            Canvas.SetLeft(vertex, point.X - 6);
            Canvas.SetTop(vertex, point.Y - 6);

            vertex.MouseDown += Vertex_MouseDown;
            vertex.MouseMove += Vertex_MouseMove;
            vertex.MouseUp += Vertex_MouseUp;
            vertex.MouseRightButtonDown += Vertex_RightClick;

            return vertex;
        }

        private void Vertex_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditable)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _draggedVertex = sender as Ellipse;
                _draggedVertexIndex = (int)_draggedVertex!.Tag;
                _draggedVertex.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Vertex_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_draggedVertex != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(PerimeterCanvas);
                
                // Update the point
                _points[_draggedVertexIndex] = new PointF((float)position.X, (float)position.Y);
                
                // Update the vertex position
                Canvas.SetLeft(_draggedVertex, position.X - 6);
                Canvas.SetTop(_draggedVertex, position.Y - 6);
                
                // Update the polygon
                if (_polygon != null)
                {
                    _polygon.Points[_draggedVertexIndex] = position;
                }
                
                OnPerimeterChanged();
                e.Handled = true;
            }
        }

        private void Vertex_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedVertex != null)
            {
                _draggedVertex.ReleaseMouseCapture();
                _draggedVertex = null;
                _draggedVertexIndex = -1;
            }
        }

        private void Vertex_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditable)
            {
                e.Handled = true;
                return;
            }

            // Don't allow removing if we have only 3 vertices
            if (_points.Count <= 3)
            {
                e.Handled = true;
                return;
            }

            var vertex = sender as Ellipse;
            var index = (int)vertex!.Tag;
            
            // Remove the point
            _points.RemoveAt(index);
            
            // Re-render the perimeter
            RenderPerimeter();
            
            OnPerimeterChanged();
            e.Handled = true;
        }

        private void Polygon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditable)
                return;

            // Double-click to add a vertex
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(PerimeterCanvas);
                
                // Find the closest edge to insert the new point
                int insertIndex = FindClosestEdge(new PointF((float)position.X, (float)position.Y));
                
                // Insert the new point
                _points.Insert(insertIndex + 1, new PointF((float)position.X, (float)position.Y));
                
                // Re-render the perimeter
                RenderPerimeter();
                
                OnPerimeterChanged();
                e.Handled = true;
            }
        }

        private int FindClosestEdge(PointF point)
        {
            double minDistance = double.MaxValue;
            int closestEdgeIndex = 0;

            for (int i = 0; i < _points.Count; i++)
            {
                int j = (i + 1) % _points.Count;
                var p1 = _points[i];
                var p2 = _points[j];

                // Calculate distance from point to line segment
                double distance = DistanceToLineSegment(point, p1, p2);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestEdgeIndex = i;
                }
            }

            return closestEdgeIndex;
        }

        private double DistanceToLineSegment(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            
            if (dx == 0 && dy == 0)
                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));

            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double nearestX = lineStart.X + t * dx;
            double nearestY = lineStart.Y + t * dy;

            return Math.Sqrt(Math.Pow(point.X - nearestX, 2) + Math.Pow(point.Y - nearestY, 2));
        }

        private void OnPerimeterChanged()
        {
            PerimeterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateVertexVisibility()
        {
            foreach (var vertex in _vertices)
            {
                vertex.Visibility = _isEditable ? Visibility.Visible : Visibility.Collapsed;
                vertex.Cursor = _isEditable ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
            }
        }
    }
}

