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
        private Controls.PerimeterOverlayControl? _perimeterOverlay = null;
        private bool _roomOverlaysHidden = false;

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

                    if (args.PropertyName == nameof(vm.CurrentSketch))
                    {
                        // Sketch replaced; clear any overlays
                        OverlayCanvas.Children.Clear();
                        _roomOverlaysHidden = false;
                    }

                    if (args.PropertyName == nameof(vm.CurrentSketch.SelectedRoomForScale) || args.PropertyName == nameof(vm.CurrentSketch.Rooms))
                    {
                        if (!_roomOverlaysHidden)
                        {
                            RenderSelectedRoomOverlay(vm);
                            UpdateDimensionsTextBox(vm);
                        }
                    }
                    
                    if (args.PropertyName == nameof(vm.CurrentSketch.PerimeterPoints))
                    {
                        RenderPerimeterOverlay(vm);
                    }

                    if (args.PropertyName == nameof(vm.CurrentSketch.CurrentState))
                    {
                        HandleWorkflowStateChange(vm);
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

        private void RenderSelectedRoomOverlay(MainWindowViewModel vm)
        {
            OverlayCanvas.Children.Clear();
            var room = vm?.CurrentSketch?.SelectedRoomForScale;
            if (room == null) return;

            var overlay = new Controls.RoomOverlayControl
            {
                X = room.Bounds.X,
                Y = room.Bounds.Y,
                OverlayWidth = room.Bounds.Width,
                OverlayHeight = room.Bounds.Height
            };

            overlay.OverlayChanged += (s, e) =>
            {
                // Sync position and size back into model
                room.Bounds = new System.Drawing.RectangleF((float)overlay.X, (float)overlay.Y, (float)overlay.OverlayWidth, (float)overlay.OverlayHeight);
            };

            OverlayCanvas.Children.Add(overlay);
        }

        private void UpdateDimensionsTextBox(MainWindowViewModel vm)
        {
            var room = vm?.CurrentSketch?.SelectedRoomForScale;
            if (room == null)
            {
                DimensionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            DimensionsPanel.Visibility = Visibility.Visible;
            RoomDimensionsTextBox.Text = string.IsNullOrWhiteSpace(room.Dimensions)
                ? $"{room.WidthFeet:F1} x {room.HeightFeet:F1}"
                : room.Dimensions;

            // Add text changed handler
            RoomDimensionsTextBox.TextChanged -= OnDimensionsTextChanged;
            RoomDimensionsTextBox.TextChanged += OnDimensionsTextChanged;
        }

        private async void OnDimensionsTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            var room = vm?.CurrentSketch?.SelectedRoomForScale;
            if (room == null) return;

            var dimsText = RoomDimensionsTextBox.Text ?? string.Empty;
            var svc = new ScaleCalculationService(new ImageProcessingService());
            if (await svc.ValidateRoomDimensionsAsync(dimsText))
            {
                // Extract numbers
                ParseDimensionsFeet(dimsText, out var w, out var h);
                room.WidthFeet = w;
                room.HeightFeet = h;
                room.Dimensions = dimsText;
            }
        }

        private static void ParseDimensionsFeet(string text, out double widthFeet, out double heightFeet)
        {
            // Supports: 12x15, 12'x15', 12 ft x 15 ft, 12 5\" x 10 3\"
            widthFeet = 0;
            heightFeet = 0;
            string Normalize(string s) => s.Replace("\u2032", "'").Replace("\u2033", "\"");

            text = Normalize(text);
            var parts = text.ToLower().Split('x', '×');
            if (parts.Length != 2) return;

            double ParseOne(string p)
            {
                p = p.Trim();
                // feet and inches like 12' 6"
                var feetIdx = p.IndexOf("'");
                if (feetIdx >= 0)
                {
                    var feetPart = p.Substring(0, feetIdx).Trim();
                    var rest = p.Substring(feetIdx + 1);
                    double feet = double.TryParse(feetPart, out var f) ? f : 0;
                    double inches = 0;
                    var quoteIdx = rest.IndexOf('"');
                    if (quoteIdx >= 0)
                    {
                        var inchesPart = rest.Substring(0, quoteIdx).Trim();
                        inches = double.TryParse(inchesPart, out var i) ? i : 0;
                    }
                    return feet + inches / 12.0;
                }

                // simple number optionally with 'ft'
                p = p.Replace("feet", string.Empty).Replace("ft", string.Empty).Trim();
                return double.TryParse(p, out var val) ? val : 0;
            }

            widthFeet = ParseOne(parts[0]);
            heightFeet = ParseOne(parts[1]);
        }

        private void RenderPerimeterOverlay(MainWindowViewModel vm)
        {
            // Hide room overlays and dimensions panel when showing perimeter
            _roomOverlaysHidden = true;
            DimensionsPanel.Visibility = Visibility.Collapsed;
            
            // Remove all existing room overlays
            var roomOverlays = OverlayCanvas.Children.OfType<Controls.RoomOverlayControl>().ToList();
            foreach (var overlay in roomOverlays)
            {
                OverlayCanvas.Children.Remove(overlay);
            }

            // Remove existing perimeter overlay if any
            if (_perimeterOverlay != null)
            {
                OverlayCanvas.Children.Remove(_perimeterOverlay);
                _perimeterOverlay = null;
            }

            var points = vm?.CurrentSketch?.PerimeterPoints;
            if (points == null || points.Count < 3)
                return;

            // Create new perimeter overlay
            _perimeterOverlay = new Controls.PerimeterOverlayControl();
            _perimeterOverlay.SetPoints(points);
            
            // Set editability based on workflow state
            bool isEditable = vm?.CurrentSketch?.CurrentState != Models.WorkflowState.AreaCalculated;
            _perimeterOverlay.IsEditable = isEditable;
            
            // Handle perimeter changes
            _perimeterOverlay.PerimeterChanged += (s, e) =>
            {
                // Sync the perimeter points back to the model
                vm.CurrentSketch.PerimeterPoints = _perimeterOverlay.GetPoints();
            };

            // Set the overlay to fill the canvas
            _perimeterOverlay.Width = FloorPlanImage.Source?.Width ?? 0;
            _perimeterOverlay.Height = FloorPlanImage.Source?.Height ?? 0;
            
            OverlayCanvas.Children.Add(_perimeterOverlay);
        }

        private void HandleWorkflowStateChange(MainWindowViewModel vm)
        {
            if (vm?.CurrentSketch == null)
                return;

            var state = vm.CurrentSketch.CurrentState;
            System.Diagnostics.Debug.WriteLine($"Workflow state changed to: {state}");

            switch (state)
            {
                case Models.WorkflowState.RoomDetected:
                    // Show room overlays and dimensions panel
                    _roomOverlaysHidden = false;
                    RenderSelectedRoomOverlay(vm);
                    UpdateDimensionsTextBox(vm);
                    
                    // Remove perimeter overlay if any
                    if (_perimeterOverlay != null)
                    {
                        OverlayCanvas.Children.Remove(_perimeterOverlay);
                        _perimeterOverlay = null;
                    }
                    break;

                case Models.WorkflowState.PerimeterTraced:
                    // Re-enable perimeter editing
                    if (_perimeterOverlay != null)
                    {
                        _perimeterOverlay.IsEditable = true;
                    }
                    break;

                case Models.WorkflowState.AreaCalculated:
                    // Disable perimeter editing
                    if (_perimeterOverlay != null)
                    {
                        _perimeterOverlay.IsEditable = false;
                    }
                    break;
            }
        }
    }
}
