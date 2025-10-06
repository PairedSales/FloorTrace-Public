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
using Microsoft.Extensions.DependencyInjection;

namespace FloorTrace
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private System.Windows.Point _lastMousePosition;
        private double _zoomFactor = FloorTrace.Utilities.Constants.DefaultZoom;
        private Controls.PerimeterOverlayControl? _perimeterOverlay = null;
        private bool _roomOverlaysHidden = false;
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            
            // Hide test button in release builds
#if !DEBUG
            TestButton.Visibility = Visibility.Collapsed;
#endif
            
            LoadWindowSettings();
            
            // Initialize prior sketches visibility to hidden by default
            UpdatePriorSketchesVisibility(_viewModel.IsPriorSketchesVisible);

            // Add a handler for when a new image is loaded to center it
            var vm = _viewModel;
            if (vm != null)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.CurrentImage) && vm.CurrentImage != null)
                    {
                        // Use Dispatcher to wait for the layout to update after the image loads
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FitImageToWindow();
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
                    
                    if (args.PropertyName == nameof(vm.IsPriorSketchesVisible))
                    {
                        UpdatePriorSketchesVisibility(vm.IsPriorSketchesVisible);
                    }
                };
            }
            
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
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
            if (!File.Exists(settingsFile))
                return;

            try
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                
                if (settings == null)
                    return;

                // Check if window is mostly visible on any screen (at least 50% of window area)
                bool isWindowVisible = IsWindowVisibleOnAnyScreen(settings);

                if (isWindowVisible)
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
            catch (Exception)
            {
                // If settings are corrupted, use default position
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private bool IsWindowVisibleOnAnyScreen(WindowSettings settings)
        {
            var windowRect = new Rect(settings.Left, settings.Top, settings.Width, settings.Height);
            
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var screenBounds = new Rect(
                    screen.WorkingArea.Left, 
                    screen.WorkingArea.Top, 
                    screen.WorkingArea.Width, 
                    screen.WorkingArea.Height);

                // Calculate intersection area
                var intersection = Rect.Intersect(windowRect, screenBounds);
                if (!intersection.IsEmpty)
                {
                    // Check if at least 50% of window is visible
                    double windowArea = windowRect.Width * windowRect.Height;
                    double visibleArea = intersection.Width * intersection.Height;
                    if (visibleArea / windowArea >= 0.5)
                    {
                        return true;
                    }
                }
            }

            return false;
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

            var newZoom = _zoomFactor + (e.Delta > 0 ? FloorTrace.Utilities.Constants.ZoomStep : -FloorTrace.Utilities.Constants.ZoomStep);
            _zoomFactor = Math.Max(FloorTrace.Utilities.Constants.MinZoom, Math.Min(FloorTrace.Utilities.Constants.MaxZoom, newZoom));

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

        

        private void ApplyCenterZoom()
        {
            ZoomTransform.CenterX = ImageScrollViewer.ViewportWidth / 2;
            ZoomTransform.CenterY = ImageScrollViewer.ViewportHeight / 2;
            ZoomTransform.ScaleX = _zoomFactor;
            ZoomTransform.ScaleY = _zoomFactor;
        }

        private void FitImageToWindow()
        {
            if (FloorPlanImage.Source != null)
            {
                // Get the actual image dimensions
                var imageWidth = FloorPlanImage.Source.Width;
                var imageHeight = FloorPlanImage.Source.Height;
                
                // Get the available viewport space
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                
                // Calculate zoom factors to fit both width and height
                var zoomX = viewportWidth / imageWidth;
                var zoomY = viewportHeight / imageHeight;
                
                // Use the smaller zoom factor to ensure the image fits completely
                _zoomFactor = Math.Min(zoomX, zoomY);
                
                // Apply some padding (10% smaller)
                _zoomFactor *= 0.9;
                
                // Ensure we stay within zoom limits
                _zoomFactor = Math.Max(FloorTrace.Utilities.Constants.MinZoom, 
                                      Math.Min(FloorTrace.Utilities.Constants.MaxZoom, _zoomFactor));
                
                ApplyCenterZoom();
                CenterImageInView();
            }
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitImageToWindow();
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
                OverlayHeight = room.Bounds.Height,
                HorizontalWallLines = vm.CurrentSketch.HorizontalWallLines ?? new List<float>(),
                VerticalWallLines = vm.CurrentSketch.VerticalWallLines ?? new List<float>()
            };

            overlay.OverlayChanged += (s, e) =>
            {
                // Sync position and size back into model
                room.Bounds = new System.Drawing.RectangleF((float)overlay.X, (float)overlay.Y, (float)overlay.OverlayWidth, (float)overlay.OverlayHeight);
                // Recalculate scale when overlay changes
                _viewModel.RecalculateScale();
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
            var room = _viewModel?.CurrentSketch?.SelectedRoomForScale;
            if (room == null) return;

            var dimsText = RoomDimensionsTextBox.Text ?? string.Empty;
            
            if (FloorTrace.Utilities.DimensionParser.TryParseDimensionsFeet(dimsText, out var w, out var h))
            {
                room.WidthFeet = w;
                room.HeightFeet = h;
                room.Dimensions = dimsText;
                // Recalculate scale when dimensions change
                _viewModel.RecalculateScale();
            }
            
            await Task.CompletedTask; // Keep method async for future enhancements
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
                vm!.CurrentSketch.PerimeterPoints = _perimeterOverlay.GetPoints();
            };

            // Set the overlay to fill the canvas
            _perimeterOverlay.Width = FloorPlanImage.Source?.Width ?? 0;
            _perimeterOverlay.Height = FloorPlanImage.Source?.Height ?? 0;
            
            OverlayCanvas.Children.Add(_perimeterOverlay);
        }

        private void UpdatePriorSketchesVisibility(bool isVisible)
        {
            if (isVisible)
            {
                SplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
                PriorSketchesColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                SplitterColumn.Width = new GridLength(0);
                PriorSketchesColumn.Width = new GridLength(0);
            }
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

        private async void SketchItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string sketchId)
            {
                await _viewModel.LoadPriorSketchCommand.ExecuteAsync(sketchId);
            }
        }
    }
}
