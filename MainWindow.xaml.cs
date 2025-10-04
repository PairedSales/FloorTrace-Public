using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FloorTrace.ViewModels;
using FloorTrace.Services;

namespace FloorTrace
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private double _zoomFactor = 1.0;
        private const double _zoomStep = 0.1;
        private const double _minZoom = 0.1;
        private const double _maxZoom = 5.0;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services and ViewModel
            var imageProcessingService = new ImageProcessingService();
            var scaleCalculationService = new ScaleCalculationService(imageProcessingService);
            var areaCalculationService = new AreaCalculationService();
            var storageService = new StorageService();
            
            DataContext = new MainWindowViewModel(
                imageProcessingService,
                scaleCalculationService,
                areaCalculationService,
                storageService);
        }

        private void FloorPlanImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var zoomDelta = e.Delta > 0 ? _zoomStep : -_zoomStep;
            var newZoom = Math.Max(_minZoom, Math.Min(_maxZoom, _zoomFactor + zoomDelta));
            
            if (Math.Abs(newZoom - _zoomFactor) > 0.01)
            {
                var mousePosition = e.GetPosition(FloorPlanImage);
                ZoomToPoint(newZoom, mousePosition);
            }
            
            e.Handled = true;
        }

        private void FloorPlanImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FloorPlanImage.CaptureMouse();
            _isDragging = true;
            _lastMousePosition = e.GetPosition(ImageScrollViewer);
        }

        private void FloorPlanImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && FloorPlanImage.IsMouseCaptured)
            {
                var currentPosition = e.GetPosition(ImageScrollViewer);
                var delta = new Point(
                    currentPosition.X - _lastMousePosition.X,
                    currentPosition.Y - _lastMousePosition.Y);

                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - delta.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - delta.Y);

                _lastMousePosition = currentPosition;
            }
        }

        private void FloorPlanImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            FloorPlanImage.ReleaseMouseCapture();
        }

        private void ZoomToPoint(double newZoom, Point zoomPoint)
        {
            var oldZoom = _zoomFactor;
            _zoomFactor = newZoom;
            
            // Calculate the zoom center point relative to the image
            var imageCenter = new Point(FloorPlanImage.ActualWidth / 2, FloorPlanImage.ActualHeight / 2);
            var zoomCenter = zoomPoint;
            
            // Apply zoom transform
            var transform = new ScaleTransform(_zoomFactor, _zoomFactor);
            FloorPlanImage.RenderTransform = transform;
            
            // Adjust scroll position to maintain zoom center
            var scrollCenter = new Point(
                ImageScrollViewer.HorizontalOffset + ImageScrollViewer.ViewportWidth / 2,
                ImageScrollViewer.VerticalOffset + ImageScrollViewer.ViewportHeight / 2);
            
            var newScrollX = zoomCenter.X * _zoomFactor - scrollCenter.X + ImageScrollViewer.HorizontalOffset;
            var newScrollY = zoomCenter.Y * _zoomFactor - scrollCenter.Y + ImageScrollViewer.VerticalOffset;
            
            ImageScrollViewer.ScrollToHorizontalOffset(newScrollX);
            ImageScrollViewer.ScrollToVerticalOffset(newScrollY);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            var newZoom = Math.Min(_maxZoom, _zoomFactor + _zoomStep);
            var centerPoint = new Point(FloorPlanImage.ActualWidth / 2, FloorPlanImage.ActualHeight / 2);
            ZoomToPoint(newZoom, centerPoint);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            var newZoom = Math.Max(_minZoom, _zoomFactor - _zoomStep);
            var centerPoint = new Point(FloorPlanImage.ActualWidth / 2, FloorPlanImage.ActualHeight / 2);
            ZoomToPoint(newZoom, centerPoint);
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = 1.0;
            FloorPlanImage.RenderTransform = new ScaleTransform(1.0, 1.0);
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            if (FloorPlanImage.Source != null)
            {
                var imageWidth = FloorPlanImage.Source.Width;
                var imageHeight = FloorPlanImage.Source.Height;
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                
                var scaleX = viewportWidth / imageWidth;
                var scaleY = viewportHeight / imageHeight;
                var scale = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave some margin
                
                _zoomFactor = Math.Max(_minZoom, Math.Min(_maxZoom, scale));
                FloorPlanImage.RenderTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
                
                // Center the image
                ImageScrollViewer.ScrollToHorizontalOffset((imageWidth * _zoomFactor - viewportWidth) / 2);
                ImageScrollViewer.ScrollToVerticalOffset((imageHeight * _zoomFactor - viewportHeight) / 2);
            }
        }
    }
}
