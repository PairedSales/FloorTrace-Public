using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloorTrace.ViewModels;
using FloorTrace.Services;
using FloorTrace.Models;

namespace FloorTrace
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private System.Windows.Point _lastMousePosition;
        private double _zoomFactor = 1.0;
        private const double _zoomStep = 0.1;
        private const double _minZoom = 0.1;
        private const double _maxZoom = 5.0;

        public MainWindow()
        {
            InitializeComponent();
            LoadWindowSettings();
            
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
            
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
        }

        private string GetSettingsFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "FloorTrace");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "settings.json");
        }

        private void SaveWindowSettings()
        {
            var settings = new WindowSettings
            {
                Top = this.Top,
                Left = this.Left,
                Height = this.Height,
                Width = this.Width,
                WindowState = this.WindowState
            };

            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText(GetSettingsFilePath(), json);
        }

        private void LoadWindowSettings()
        {
            string settingsFile = GetSettingsFilePath();
            if (File.Exists(settingsFile))
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<WindowSettings>(json);

                // Add logic to ensure the window is visible on a screen
                bool isWithinScreenBounds = false;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var screenBounds = new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
                    if (screenBounds.Contains(new System.Windows.Point(settings.Left, settings.Top)))
                    {
                        isWithinScreenBounds = true;
                        break;
                    }
                }

                if (isWithinScreenBounds)
                {
                    this.Top = settings.Top;
                    this.Left = settings.Left;
                    this.Height = settings.Height;
                    this.Width = settings.Width;
                    this.WindowState = settings.WindowState;
                }
                else
                {
                    // Window is off-screen, load with default position
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
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

        private void PanningCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && PanningCanvas.IsMouseCaptured)
            {
                var currentPosition = e.GetPosition(ImageScrollViewer);
                var delta = new System.Windows.Point(
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
