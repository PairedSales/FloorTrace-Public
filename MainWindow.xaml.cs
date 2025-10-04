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

            // Add a handler for when a new image is loaded to center it
            var vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.CurrentImage) && vm.CurrentImage != null)
                    {
                        // Use Dispatcher to wait for the layout to update after the image loads
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CenterImageInView();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                };
            }
        }

        private void CenterImageInView()
        {
            var newHorizontalOffset = (PanningCanvas.Width - ImageScrollViewer.ViewportWidth) / 2;
            var newVerticalOffset = (PanningCanvas.Height - ImageScrollViewer.ViewportHeight) / 2;
            ImageScrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
            ImageScrollViewer.ScrollToVerticalOffset(newVerticalOffset);
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FloorPlanImage.Source == null) return;

            var newZoom = _zoomFactor + (e.Delta > 0 ? _zoomStep : -_zoomStep);
            _zoomFactor = Math.Max(_minZoom, Math.Min(_maxZoom, newZoom));

            var mousePosition = e.GetPosition(ImageGrid);
            ZoomTransform.CenterX = mousePosition.X;
            ZoomTransform.CenterY = mousePosition.Y;
            ZoomTransform.ScaleX = _zoomFactor;
            ZoomTransform.ScaleY = _zoomFactor;
            
            e.Handled = true;
        }

        private void PanningCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PanningCanvas.CaptureMouse();
            _isDragging = true;
            _lastMousePosition = e.GetPosition(ImageScrollViewer);
        }

        private void PanningCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && PanningCanvas.IsMouseCaptured)
            {
                var currentPosition = e.GetPosition(ImageScrollViewer);
                var delta = new Point(
                    currentPosition.X - _lastMousePosition.X,
                    currentPosition.Y - _lastMousePosition.Y);

                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - delta.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - delta.Y);

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        private void PanningCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            PanningCanvas.ReleaseMouseCapture();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = Math.Min(_maxZoom, _zoomFactor + _zoomStep);
            ApplyCenterZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = Math.Max(_minZoom, _zoomFactor - _zoomStep);
            ApplyCenterZoom();
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = 1.0;
            ApplyCenterZoom();
        }
        
        private void ApplyCenterZoom()
        {
            ZoomTransform.CenterX = ImageScrollViewer.ViewportWidth / 2;
            ZoomTransform.CenterY = ImageScrollViewer.ViewportHeight / 2;
            ZoomTransform.ScaleX = _zoomFactor;
            ZoomTransform.ScaleY = _zoomFactor;
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (FloorPlanImage.Source != null)
            {
                _zoomFactor = 1.0;
                ApplyCenterZoom();
                CenterImageInView(); // Re-center after fitting
            }
        }
    }
}
