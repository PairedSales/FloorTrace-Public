using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FloorTrace.Utilities;

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

            // Create the polygon with Material Design styling
            _polygon = CreatePolygonWithStyling();
            _polygon.MouseDown += Polygon_MouseDown;
            PerimeterCanvas.Children.Add(_polygon);

            // Create vertex thumbs
            CreateAndAddVertices();
        }

        /// <summary>
        /// Creates a polygon with Material Design styling and effects.
        /// </summary>
        private Polygon CreatePolygonWithStyling()
        {
            var polygon = new Polygon
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(26, 33, 150, 243)), // Semi-transparent Material Blue (#2196F3 at 10% opacity)
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)), // Material Design Secondary Blue (#2196F3)
                StrokeThickness = 3,
                Points = new PointCollection(_points.Select(p => new System.Windows.Point(p.X, p.Y)))
            };
            
            // Add subtle shadow effect
            polygon.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 2,
                Opacity = 0.2,
                Color = System.Windows.Media.Color.FromRgb(33, 150, 243)
            };

            return polygon;
        }

        /// <summary>
        /// Creates and adds vertex handles to the canvas.
        /// </summary>
        private void CreateAndAddVertices()
        {
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
                Width = FloorTrace.Utilities.Constants.VertexHandleSize,
                Height = FloorTrace.Utilities.Constants.VertexHandleSize,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)), // Material Design Secondary Blue (#2196F3)
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = index
            };
            
            // Add Material Design elevation shadow to vertices
            vertex.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.25,
                Color = System.Windows.Media.Colors.Black
            };

            Canvas.SetLeft(vertex, point.X - FloorTrace.Utilities.Constants.VertexHandleOffset);
            Canvas.SetTop(vertex, point.Y - FloorTrace.Utilities.Constants.VertexHandleOffset);

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
                Canvas.SetLeft(_draggedVertex, position.X - FloorTrace.Utilities.Constants.VertexHandleOffset);
                Canvas.SetTop(_draggedVertex, position.Y - FloorTrace.Utilities.Constants.VertexHandleOffset);
                
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
                int insertIndex = GeometryHelper.FindClosestEdge(new PointF((float)position.X, (float)position.Y), _points);
                
                // Insert the new point
                _points.Insert(insertIndex + 1, new PointF((float)position.X, (float)position.Y));
                
                // Re-render the perimeter
                RenderPerimeter();
                
                OnPerimeterChanged();
                e.Handled = true;
            }
        }

        private void PerimeterCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditable)
                return;

            // Only handle clicks that target the canvas itself (not vertices or polygon)
            if (e.OriginalSource is not Canvas)
                return;

            // Require double-click to add a point
            if (!(e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed))
                return;

            var position = e.GetPosition(PerimeterCanvas);

            if (_points == null || _points.Count < 3)
            {
                // Build up points until we have a valid polygon
                _points.Add(new PointF((float)position.X, (float)position.Y));
                RenderPerimeter();
                OnPerimeterChanged();
                e.Handled = true;
                return;
            }

            // Insert at the closest edge to the click position
            int insertIndex = GeometryHelper.FindClosestEdge(new PointF((float)position.X, (float)position.Y), _points);
            _points.Insert(insertIndex + 1, new PointF((float)position.X, (float)position.Y));

            RenderPerimeter();
            OnPerimeterChanged();
            e.Handled = true;
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

