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
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            
            
            LoadWindowSettings();
            

            // Add a handler for when a new image is loaded to center it
            var vm = _viewModel;
            if (vm != null)
            {
                vm.PropertyChanged += (sender, args) => HandlePropertyChanged(vm, args);
            }
            
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Handles property changed events from the view model.
        /// </summary>
        private void HandlePropertyChanged(MainWindowViewModel vm, System.ComponentModel.PropertyChangedEventArgs args)
        {
            HandleImagePropertyChanged(vm, args);
            HandleSketchPropertyChanged(vm, args);
            HandleRoomPropertyChanged(vm, args);
            HandlePerimeterPropertyChanged(vm, args);
        }

        /// <summary>
        /// Handles property changes related to the current image.
        /// </summary>
        private void HandleImagePropertyChanged(MainWindowViewModel vm, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CurrentImage) && vm.CurrentImage != null)
            {
                // Use Dispatcher to wait for the layout to update after the image loads
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitImageToWindow();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Handles property changes related to the current sketch.
        /// </summary>
        private void HandleSketchPropertyChanged(MainWindowViewModel vm, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CurrentSketch))
            {
                // Sketch replaced; clear any overlays
                OverlayCanvas.Children.Clear();
                _perimeterOverlay = null;
            }
        }

        /// <summary>
        /// Handles property changes related to room selection.
        /// </summary>
        private void HandleRoomPropertyChanged(MainWindowViewModel vm, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CurrentSketch.SelectedRoomForScale) || args.PropertyName == nameof(vm.CurrentSketch.Rooms))
            {
                RenderSelectedRoomOverlay(vm);
                UpdateDimensionsTextBox(vm);
            }
        }

        /// <summary>
        /// Handles property changes related to perimeter points.
        /// </summary>
        private void HandlePerimeterPropertyChanged(MainWindowViewModel vm, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CurrentSketch.PerimeterPoints))
            {
                RenderPerimeterOverlay(vm);
            }
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

                if (IsRectangleVisibleOnScreen(windowRect, screenBounds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a rectangle is visible on a screen with at least 50% visibility.
        /// </summary>
        private static bool IsRectangleVisibleOnScreen(Rect windowRect, Rect screenBounds)
        {
            // Calculate intersection area
            var intersection = Rect.Intersect(windowRect, screenBounds);
            if (intersection.IsEmpty)
            {
                return false;
            }

            // Check if at least 50% of window is visible
            double windowArea = windowRect.Width * windowRect.Height;
            double visibleArea = intersection.Width * intersection.Height;
            return visibleArea / windowArea >= 0.5;
        }

        private void CenterImageInView()
        {
            var newHorizontalOffset = (PanningCanvas.Width - ImageScrollViewer.ViewportWidth) / 2;
            var newVerticalOffset = (PanningCanvas.Height - ImageScrollViewer.ViewportHeight) / 2;
            ImageScrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
            ImageScrollViewer.ScrollToVerticalOffset(newVerticalOffset);
        }

        /// <summary>
        /// Handles mouse wheel events for zooming in and out on the floor plan image.
        /// Implements zoom-to-point functionality where the image zooms toward the mouse cursor position.
        /// </summary>
        /// <param name="sender">The ScrollViewer that contains the image.</param>
        /// <param name="e">Mouse wheel event arguments containing delta and position information.</param>
        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FloorPlanImage.Source == null) return;

            // Calculate new zoom factor based on wheel direction
            var oldZoom = _zoomFactor;
            var newZoom = _zoomFactor + (e.Delta > 0 ? FloorTrace.Utilities.Constants.ZoomStep : -FloorTrace.Utilities.Constants.ZoomStep);
            _zoomFactor = Math.Max(FloorTrace.Utilities.Constants.MinZoom, Math.Min(FloorTrace.Utilities.Constants.MaxZoom, newZoom));

            // Get mouse position relative to the ScrollViewer (viewport)
            // This is where the user's cursor is on the screen
            var mousePosition = e.GetPosition(ImageScrollViewer);
            
            // Calculate the point in the panning canvas that the mouse is pointing to
            // This accounts for the current scroll position
            var canvasPoint = new System.Windows.Point(
                mousePosition.X + ImageScrollViewer.HorizontalOffset,
                mousePosition.Y + ImageScrollViewer.VerticalOffset
            );
            
            // Calculate the point in the original (unzoomed) image coordinates
            // This converts from canvas coordinates to image coordinates using the old zoom factor
            var imagePoint = new System.Windows.Point(
                (canvasPoint.X - PanningCanvas.Width / 2) / oldZoom,
                (canvasPoint.Y - PanningCanvas.Height / 2) / oldZoom
            );
            
            // Apply the new zoom transformation
            ZoomTransform.ScaleX = _zoomFactor;
            ZoomTransform.ScaleY = _zoomFactor;
            
            // Calculate new scroll position to keep the mouse point visually fixed
            // This ensures the point under the cursor stays in the same screen position after zooming
            var newCanvasX = (imagePoint.X * _zoomFactor) + PanningCanvas.Width / 2;
            var newCanvasY = (imagePoint.Y * _zoomFactor) + PanningCanvas.Height / 2;
            
            // Calculate the new scroll offsets to maintain the visual position
            var newScrollX = newCanvasX - mousePosition.X;
            var newScrollY = newCanvasY - mousePosition.Y;
            
            // Apply the new scroll position with bounds checking
            ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
            ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, newScrollY));
            
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
            // Remove existing room overlays
            var existingRoomOverlays = OverlayCanvas.Children.OfType<Controls.RoomOverlayControl>().ToList();
            foreach (var existing in existingRoomOverlays)
            {
                OverlayCanvas.Children.Remove(existing);
            }
            
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

            // Ensure room overlay is always above the perimeter overlay
            System.Windows.Controls.Panel.SetZIndex(overlay, 1000);

            overlay.OverlayChanged += (s, e) =>
            {
                // Sync position and size back into model
                room.Bounds = new System.Drawing.RectangleF((float)overlay.X, (float)overlay.Y, (float)overlay.OverlayWidth, (float)overlay.OverlayHeight);
                // Recalculate scale when overlay changes (this will trigger auto area calc)
                _viewModel.RecalculateScale();
            };

            OverlayCanvas.Children.Add(overlay);
        }

        private void UpdateDimensionsTextBox(MainWindowViewModel vm)
        {
            var room = vm?.CurrentSketch?.SelectedRoomForScale;
            
            // Always show dimensions panel
            DimensionsPanel.Visibility = Visibility.Visible;
            
            if (room == null)
            {
                // Clear the text when no room is selected
                RoomDimensionsTextBox.Text = string.Empty;
                RoomDimensionsTextBox.TextChanged -= OnDimensionsTextChanged;
                return;
            }

            RoomDimensionsTextBox.Text = string.IsNullOrWhiteSpace(room.Dimensions)
                ? $"{room.WidthFeet:F1} x {room.HeightFeet:F1}"
                : room.Dimensions;

            // Add text changed handler
            RoomDimensionsTextBox.TextChanged -= OnDimensionsTextChanged;
            RoomDimensionsTextBox.TextChanged += OnDimensionsTextChanged;
        }

        private void OnDimensionsTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var room = _viewModel?.CurrentSketch?.SelectedRoomForScale;
            if (room == null) return;

            var dimsText = RoomDimensionsTextBox.Text ?? string.Empty;
            
            if (FloorTrace.Utilities.DimensionParser.TryParseDimensionsFeet(dimsText, out var w, out var h))
            {
                room.WidthFeet = w;
                room.HeightFeet = h;
                room.Dimensions = dimsText;
                // Recalculate scale when dimensions change (this will trigger auto area calc)
                _viewModel.RecalculateScale();
            }
        }


        private void RenderPerimeterOverlay(MainWindowViewModel vm)
        {
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
            
            // Perimeter is always editable when visible
            _perimeterOverlay.IsEditable = true;
            
            // Handle perimeter changes with auto-calculation
            _perimeterOverlay.PerimeterChanged += async (s, e) =>
            {
                // Sync the perimeter points back to the model
                vm!.CurrentSketch.PerimeterPoints = _perimeterOverlay.GetPoints();
                
                // Trigger auto area calculation
                await vm.TryAutoCalculateAreaAsync();
            };

            // Set the overlay to fill the canvas
            _perimeterOverlay.Width = FloorPlanImage.Source?.Width ?? 0;
            _perimeterOverlay.Height = FloorPlanImage.Source?.Height ?? 0;
            
            OverlayCanvas.Children.Add(_perimeterOverlay);
            // Push perimeter behind room overlay
            System.Windows.Controls.Panel.SetZIndex(_perimeterOverlay, 0);
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
