using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloorTrace.Models;
using FloorTrace.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using FloorTrace.Utilities;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;

namespace FloorTrace.ViewModels
{
    /// <summary>
    /// ViewModel for the main window that coordinates all floor plan analysis operations.
    /// This class implements the MVVM pattern and serves as the primary business logic layer,
    /// orchestrating image processing, room detection, scale calculation, perimeter tracing, and area calculation.
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IScaleCalculationService _scaleCalculationService;
        private readonly IAreaCalculationService _areaCalculationService;
        private readonly IStorageService _storageService;
        private readonly IDialogService _dialogService;
        private readonly IConfiguration _configuration;
        
        // Manual mode state
        [ObservableProperty]
        private bool isManualModeActive;

        public ObservableCollection<OcrDimensionLabel> ManualModeLabels { get; } = new();
        
        /// <summary>
        /// Gets or sets the current sketch being analyzed.
        /// </summary>
        [ObservableProperty]
        private Sketch currentSketch;
        
        /// <summary>
        /// The current floor plan image being displayed and analyzed.
        /// </summary>
        private BitmapImage? _currentImage;
        
        /// <summary>
        /// Collection of previously saved sketches for quick access.
        /// </summary>
        private ObservableCollection<Sketch> _priorSketches = new();
        
        /// <summary>
        /// Gets or sets whether the prior sketches panel is visible in the UI.
        /// </summary>
        [ObservableProperty]
        private bool isPriorSketchesVisible = false;
        
        /// <summary>
        /// Gets or sets whether to use inner wall edges for perimeter detection (accounting for wall thickness).
        /// </summary>
        [ObservableProperty]
        private bool useInnerWallEdge;
        
        
        partial void OnUseInnerWallEdgeChanged(bool value)
        {
            // Automatically re-trace perimeter when the setting changes
            _ = OnWallEdgePreferenceChangedAsync();
        }
        
        
        /// <summary>
        /// Gets whether sketch saving is enabled based on configuration.
        /// </summary>
        public bool IsSavingEnabled { get; }
        
        /// <summary>
        /// Gets the maximum number of sketches to save when saving is enabled.
        /// </summary>
        public int MaxSavedSketches { get; }
        
        /// <summary>
        /// Initializes a new instance of the MainWindowViewModel class.
        /// </summary>
        /// <param name="logger">The logger instance for this ViewModel.</param>
        /// <param name="imageProcessingService">The service for image processing operations.</param>
        /// <param name="scaleCalculationService">The service for scale calculation operations.</param>
        /// <param name="areaCalculationService">The service for area calculation operations.</param>
        /// <param name="storageService">The service for sketch persistence operations.</param>
        /// <param name="dialogService">The service for displaying dialogs and notifications.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public MainWindowViewModel(
            ILogger<MainWindowViewModel> logger,
            IImageProcessingService imageProcessingService,
            IScaleCalculationService scaleCalculationService,
            IAreaCalculationService areaCalculationService,
            IStorageService storageService,
            IDialogService dialogService,
            IConfiguration configuration)
        {
            _logger = logger;
            _imageProcessingService = imageProcessingService;
            _scaleCalculationService = scaleCalculationService;
            _areaCalculationService = areaCalculationService;
            _storageService = storageService;
            _dialogService = dialogService;
            _configuration = configuration;
            
            IsSavingEnabled = _configuration.GetValue<bool>("ApplicationSettings:IsSavingEnabled");
            MaxSavedSketches = _configuration.GetValue<int>("ApplicationSettings:MaxSavedSketches");
            UseInnerWallEdge = _configuration.GetValue<bool>("ApplicationSettings:UseInnerWallEdge", true);
            
            CurrentSketch = new Sketch();
            PriorSketches = _priorSketches;
            
            _ = LoadPriorSketchesAsync();
        }
        
        // Commands
        [RelayCommand]
        private async Task ActivateManualModeAsync()
        {
            if (CurrentImage == null)
            {
                await _dialogService.ShowWarningAsync("Please load an image first before using Manual Mode.", "No Image Loaded");
                return;
            }

            // If overlays exist, prompt to clear
            bool hasRoomOverlay = CurrentSketch?.SelectedRoomForScale != null;
            bool hasPerimeter = CurrentSketch?.PerimeterPoints?.Count >= 3;
            if (hasRoomOverlay || hasPerimeter)
            {
                var confirm = await _dialogService.ShowConfirmationAsync("Manual Mode will clear existing overlays. Continue?", "Confirm Manual Mode");
                if (!confirm)
                    return;

                // Clear overlays
                if (CurrentSketch != null)
                {
                    CurrentSketch.SelectedRoomForScale = null;
                    CurrentSketch.PerimeterPoints.Clear();
                    OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
                    OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                }
            }

            try
            {
                // Ensure wall lines are available for snapping later
                if (CurrentSketch != null && (CurrentSketch.HorizontalWallLines.Count == 0 || CurrentSketch.VerticalWallLines.Count == 0))
                {
                    var (h, v) = await _imageProcessingService.DetectWallLinesAsync(CurrentImage);
                    CurrentSketch.HorizontalWallLines = h;
                    CurrentSketch.VerticalWallLines = v;
                }

                var labels = await _scaleCalculationService.DetectDimensionLabelsAsync(CurrentImage, horizontalOnly: true);
                ManualModeLabels.Clear();
                foreach (var l in labels)
                    ManualModeLabels.Add(l);

                if (ManualModeLabels.Count == 0)
                {
                    await _dialogService.ShowInfoAsync("No horizontal dimension labels found. Try a clearer image or zoom.", "No Labels Found");
                    IsManualModeActive = false;
                    return;
                }

                IsManualModeActive = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating Manual Mode");
                await _dialogService.ShowErrorAsync("Failed to run OCR for Manual Mode.", "Manual Mode Error");
            }
        }

        [RelayCommand]
        private async Task SelectManualLabelAsync(OcrDimensionLabel label)
        {
            try
            {
                IsManualModeActive = false;
                ManualModeLabels.Clear();

                if (CurrentSketch == null || CurrentImage == null)
                    return;

                // Create room overlay 400x400 centered at label
                int roomSize = Constants.ManualRoomOverlaySize;
                int perimSize = Constants.ManualPerimeterOverlaySize;

                var cx = label.LabelBounds.Left + label.LabelBounds.Width / 2f;
                var cy = label.LabelBounds.Top + label.LabelBounds.Height / 2f;

                // Clamp within image bounds
                double imgW = CurrentImage.PixelWidth;
                double imgH = CurrentImage.PixelHeight;

                var roomX = Math.Max(0, Math.Min(imgW - roomSize, cx - roomSize / 2.0));
                var roomY = Math.Max(0, Math.Min(imgH - roomSize, cy - roomSize / 2.0));

                var room = new Room
                {
                    Name = "Manual Room",
                    Dimensions = label.Text,
                    WidthFeet = label.WidthFeet,
                    HeightFeet = label.HeightFeet,
                    Bounds = new System.Drawing.RectangleF((float)roomX, (float)roomY, roomSize, roomSize),
                    IsSelected = true
                };

                CurrentSketch.SelectedRoomForScale = room;
                OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));

                // Create perimeter 800x800 centered at label
                var perX = Math.Max(0, Math.Min(imgW - perimSize, cx - perimSize / 2.0));
                var perY = Math.Max(0, Math.Min(imgH - perimSize, cy - perimSize / 2.0));

                var left = (float)perX; var top = (float)perY;
                var right = (float)Math.Min(imgW, perX + perimSize);
                var bottom = (float)Math.Min(imgH, perY + perimSize);

                CurrentSketch.PerimeterPoints = new List<System.Drawing.PointF>
                {
                    new System.Drawing.PointF(left, top),
                    new System.Drawing.PointF(right, top),
                    new System.Drawing.PointF(right, bottom),
                    new System.Drawing.PointF(left, bottom)
                };
                CurrentSketch.CurrentState = WorkflowState.PerimeterTraced;
                OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));

                // Recalculate scale then area
                await CalculateScaleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting manual label");
                await _dialogService.ShowErrorAsync("Failed to apply manual selection.", "Manual Mode Error");
            }
        }
        /// <summary>
        /// Command to load an image from a file dialog.
        /// </summary>
        [RelayCommand]
        private async Task LoadImageAsync()
        {
            try
            {
                var filePath = _imageProcessingService.ShowOpenFileDialog();
                if (string.IsNullOrEmpty(filePath))
                    return;
                
                await LoadImageFromPathAsync(filePath);
                CurrentSketch.CurrentState = WorkflowState.ImageLoaded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading image");
                await _dialogService.ShowErrorAsync(
                    "Failed to load the image. Please ensure the file exists and is a valid image format.",
                    "Load Image Error");
            }
        }
        
        /// <summary>
        /// Command to load an image from the clipboard.
        /// </summary>
        [RelayCommand]
        private async Task PasteImageAsync()
        {
            try
            {
                _logger.LogInformation("PasteImage command started");
                
                // Load the image from clipboard
                var image = await _imageProcessingService.LoadImageFromClipboardAsync();
                _logger.LogInformation("Image loaded from clipboard: {Width}x{Height}", image?.PixelWidth, image?.PixelHeight);
                
                // Create thumbnail for the sidebar
                var thumbnail = image != null ? await _imageProcessingService.CreateThumbnailAsync(image, Utilities.Constants.ThumbnailMaxWidth, Utilities.Constants.ThumbnailMaxHeight) : null;
                _logger.LogInformation("Thumbnail created: {Width}x{Height}", thumbnail?.PixelWidth, thumbnail?.PixelHeight);
                
                // Update current sketch
                CurrentSketch = new Sketch
                {
                    Name = $"Pasted Image {DateTime.Now:yyyy-MM-dd HH:mm}",
                    ImagePath = "Clipboard", // Special indicator for pasted images
                    Thumbnail = thumbnail,
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now,
                    CurrentState = WorkflowState.ImageLoaded
                };
                
                // Store the full image for display
                _currentImage = image;
                _logger.LogDebug("Image stored in _currentImage");
                
                // Notify UI of changes
                OnPropertyChanged(nameof(CurrentImage));
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                
                _logger.LogInformation("PasteImage command completed successfully");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "No image in clipboard");
                await _dialogService.ShowWarningAsync(
                    "No image found in the clipboard. Please copy an image first.",
                    "Paste Image");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pasting image");
                await _dialogService.ShowErrorAsync(
                    "Failed to paste the image from clipboard. Please try again.",
                    "Paste Image Error");
            }
        }
        
        private async Task LoadImageFromPathAsync(string filePath)
        {
            var (image, thumbnail) = await LoadImageAndCreateThumbnail(filePath);
            
            CurrentSketch = CreateSketchFromImage(filePath, image, thumbnail);
            
            _currentImage = image;
            
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(ScaleText));
            OnPropertyChanged(nameof(SideLengthsText));
            OnPropertyChanged(nameof(AreaText));
        }

        /// <summary>
        /// Loads an image and creates a thumbnail for it.
        /// </summary>
        private async Task<(BitmapImage Image, BitmapImage Thumbnail)> LoadImageAndCreateThumbnail(string filePath)
        {
            // Load the image
            var image = await _imageProcessingService.LoadImageAsync(filePath);
            
            // Create thumbnail for the sidebar
            var thumbnail = await _imageProcessingService.CreateThumbnailAsync(image, Utilities.Constants.ThumbnailMaxWidth, Utilities.Constants.ThumbnailMaxHeight);
            
            return (image, thumbnail);
        }

        /// <summary>
        /// Creates a sketch object from image data.
        /// </summary>
        private Sketch CreateSketchFromImage(string filePath, BitmapImage image, BitmapImage thumbnail)
        {
            return new Sketch
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                ImagePath = filePath,
                Thumbnail = thumbnail,
                DateCreated = DateTime.Now,
                DateModified = DateTime.Now,
                CurrentState = WorkflowState.ImageLoaded
            };
        }
        
        /// <summary>
        /// Command to detect rooms with dimensions in the current image.
        /// </summary>
        [RelayCommand]
        private async Task DetectRoomAsync()
        {
            if (CurrentImage == null)
            {
                _logger.LogWarning("No image loaded to detect rooms from.");
                await _dialogService.ShowWarningAsync(
                    "Please load an image first before detecting rooms.",
                    "No Image Loaded");
                return;
            }

            try
            {
                await DetectWallLinesForSnapping();
                
                var detectedRooms = await _scaleCalculationService.DetectRoomsAsync(CurrentImage);

                if (detectedRooms.Any())
                {
                    await ProcessDetectedRooms(detectedRooms);
                }
                else
                {
                    await HandleNoRoomsDetected();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting rooms");
                await _dialogService.ShowErrorAsync(
                    "Failed to detect rooms in the image. Please try again or check if the image quality is sufficient.",
                    "Room Detection Error");
            }
        }

        /// <summary>
        /// Detects wall lines for UI snapping assistance.
        /// </summary>
        private async Task DetectWallLinesForSnapping()
        {
            var (horizontalLines, verticalLines) = await _imageProcessingService.DetectWallLinesAsync(CurrentImage);
            CurrentSketch.HorizontalWallLines = horizontalLines;
            CurrentSketch.VerticalWallLines = verticalLines;
            _logger.LogInformation("Detected {HCount} horizontal and {VCount} vertical wall lines for snapping", 
                horizontalLines.Count, verticalLines.Count);
        }

        /// <summary>
        /// Processes successfully detected rooms.
        /// </summary>
        private async Task ProcessDetectedRooms(List<Room> detectedRooms)
        {
            CurrentSketch.Rooms = detectedRooms;
            CurrentSketch.SelectedRoomForScale = detectedRooms.First();
            CurrentSketch.CurrentState = WorkflowState.RoomDetected;
            
            // Automatically calculate scale when room is detected
            await CalculateScaleAsync();
            
            _logger.LogInformation("Detected room: {Room} with dimensions {Dims}", CurrentSketch.SelectedRoomForScale.Name, CurrentSketch.SelectedRoomForScale.Dimensions);
            OnPropertyChanged(nameof(CurrentSketch.Rooms));
            OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
            
            // Auto-save after room detection
            await AutoSaveSketchAsync();
        }

        /// <summary>
        /// Handles the case when no rooms are detected.
        /// </summary>
        private async Task HandleNoRoomsDetected()
        {
            _logger.LogInformation("No rooms detected.");
            await _dialogService.ShowInfoAsync(
                "No rooms with dimension labels were detected in the image. You may need to adjust the image or add dimensions manually.",
                "No Rooms Detected");
            
            CurrentSketch.Rooms.Clear();
            CurrentSketch.SelectedRoomForScale = null;
            OnPropertyChanged(nameof(CurrentSketch.Rooms));
            OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
        }
        
        private async Task CalculateScaleAsync()
        {
            try
            {
                if (CurrentImage == null || CurrentSketch?.SelectedRoomForScale == null)
                    return;

                var scale = await _scaleCalculationService.CalculateScaleFromRoomAsync(CurrentSketch.SelectedRoomForScale, CurrentImage);
                CurrentSketch.Scale = scale;
                OnPropertyChanged(nameof(ScaleText));
                _logger.LogInformation("Scale calculated to {Scale:F2} px/ft", scale);
                
                // Auto-calculate area when scale changes
                await TryAutoCalculateAreaAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating scale");
            }
        }

        public async void RecalculateScale()
        {
            await CalculateScaleAsync();
        }
        
        private async Task OnWallEdgePreferenceChangedAsync()
        {
            // Only re-trace if we already have a traced perimeter
            if (CurrentImage != null && CurrentSketch?.CurrentState >= WorkflowState.PerimeterTraced)
            {
                try
                {
                    _logger.LogInformation("Wall edge preference changed to {UseInner}, re-tracing perimeter", UseInnerWallEdge);
                    
                    var perimeterPoints = await _imageProcessingService.DetectPerimeterAsync(CurrentImage, UseInnerWallEdge);
                    
                    CurrentSketch.PerimeterPoints = perimeterPoints;
                    
                    OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                    
                    await TryAutoCalculateAreaAsync();
                    
                    // Auto-save after perimeter re-traced
                    await AutoSaveSketchAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error re-tracing perimeter after wall edge preference change");
                }
            }
        }
        
        /// <summary>
        /// Command to trace the perimeter of the floor plan automatically.
        /// </summary>
        [RelayCommand]
        private async Task TracePerimeterAsync()
        {
            if (CurrentImage == null)
            {
                _logger.LogWarning("No image loaded to trace perimeter.");
                await _dialogService.ShowWarningAsync(
                    "Please load an image first before tracing the perimeter.",
                    "No Image Loaded");
                return;
            }

            try
            {
                var perimeterPoints = await _imageProcessingService.DetectPerimeterAsync(CurrentImage, UseInnerWallEdge);
                
                CurrentSketch.PerimeterPoints = perimeterPoints;
                CurrentSketch.CurrentState = WorkflowState.PerimeterTraced;
                
                _logger.LogInformation("Traced perimeter with {Count} points (inner edge: {UseInner})", 
                    perimeterPoints.Count, UseInnerWallEdge);
                
                OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                
                // Auto-save after perimeter traced
                await AutoSaveSketchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracing perimeter");
                await _dialogService.ShowErrorAsync(
                    "Failed to trace the perimeter automatically. The perimeter detection may have failed.",
                    "Perimeter Tracing Error");
            }
        }
        
        /// <summary>
        /// Command to calculate the area of the traced perimeter.
        /// </summary>
        [RelayCommand]
        private async Task CalculateAreaAsync()
        {
            if (CurrentSketch?.PerimeterPoints == null || CurrentSketch.PerimeterPoints.Count < 3)
            {
                _logger.LogWarning("No perimeter points to calculate area.");
                await _dialogService.ShowWarningAsync(
                    "Please trace the perimeter first before calculating the area.",
                    "No Perimeter Traced");
                return;
            }

            if (CurrentSketch.Scale <= 0)
            {
                _logger.LogWarning("Scale not set. Cannot calculate area.");
                await _dialogService.ShowWarningAsync(
                    "Please set the scale first by detecting a room and its dimensions.",
                    "Scale Not Set");
                return;
            }

            await TryAutoCalculateAreaAsync();
        }
        
        /// <summary>
        /// Automatically calculates the area if all prerequisites are met (perimeter traced and scale set).
        /// This method is called automatically when perimeter or scale changes.
        /// </summary>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        public async Task TryAutoCalculateAreaAsync()
        {
            // Validate prerequisites for area calculation
            if (!ValidateCalculationPrerequisites())
            {
                ResetAreaCalculation();
                return;
            }

            try
            {
                await PerformAreaCalculation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-calculating area");
                // Silently fail for auto-calculation to avoid disrupting user workflow
            }
        }

        /// <summary>
        /// Validates that all prerequisites for area calculation are met.
        /// </summary>
        private bool ValidateCalculationPrerequisites()
        {
            if (CurrentSketch?.PerimeterPoints == null || CurrentSketch.PerimeterPoints.Count < 3)
            {
                return false;
            }

            if (CurrentSketch.Scale <= 0)
            {
                return false;
            }

            // Check if room dimensions exist - area calculation should not update without room dimensions
            if (CurrentSketch.SelectedRoomForScale == null || 
                (CurrentSketch.SelectedRoomForScale.WidthFeet <= 0 || CurrentSketch.SelectedRoomForScale.HeightFeet <= 0))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resets area calculation values to zero.
        /// </summary>
        private void ResetAreaCalculation()
        {
            if (CurrentSketch != null)
            {
                CurrentSketch.AreaSquareFeet = 0.0;
                CurrentSketch.SideLengths.Clear();
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(SideLengthsText));
            }
        }

        /// <summary>
        /// Performs the actual area calculation and side length conversion.
        /// </summary>
        private async Task PerformAreaCalculation()
        {
            var areaInPixels = await _areaCalculationService.CalculateAreaUsingGreensTheoremAsync(CurrentSketch.PerimeterPoints);
            
            var areaInSquareFeet = areaInPixels / (CurrentSketch.Scale * CurrentSketch.Scale);
            CurrentSketch.AreaSquareFeet = areaInSquareFeet;
            
            var sideLengthsInFeet = await ConvertSideLengthsToFeet();
            CurrentSketch.SideLengths = sideLengthsInFeet;
            CurrentSketch.CurrentState = WorkflowState.AreaCalculated;
            
            _logger.LogInformation("Auto-calculated area: {Area:F2} sq ft", areaInSquareFeet);
            
            OnPropertyChanged(nameof(ScaleText));
            OnPropertyChanged(nameof(SideLengthsText));
            OnPropertyChanged(nameof(AreaText));
            OnPropertyChanged(nameof(CurrentSketch.CurrentState));
            
            // Auto-save after area calculated to preserve the analysis results
            await AutoSaveSketchAsync();
        }

        /// <summary>
        /// Converts side lengths from pixels to feet.
        /// </summary>
        private async Task<List<double>> ConvertSideLengthsToFeet()
        {
            var sideLengthsInPixels = await _areaCalculationService.CalculateSideLengthsAsync(CurrentSketch.PerimeterPoints);
            
            var sideLengthsInFeet = new List<double>();
            foreach (var lengthInPixels in sideLengthsInPixels)
            {
                var lengthInFeet = await _areaCalculationService.ConvertPixelsToFeetAsync(lengthInPixels, CurrentSketch.Scale);
                sideLengthsInFeet.Add(lengthInFeet);
            }
            
            return sideLengthsInFeet;
        }

        private async Task AutoSaveSketchAsync()
        {
            try
            {
                if (!IsSavingEnabled)
                {
                    _logger.LogDebug("Skipping auto-save, saving is disabled");
                    return;
                }
                
                if (CurrentSketch.CurrentState < WorkflowState.RoomDetected)
                {
                    _logger.LogDebug("Skipping auto-save, no room detected yet");
                    return;
                }
                
                CurrentSketch.DateModified = DateTime.Now;
                
                await _storageService.SaveSketchAsync(CurrentSketch, _currentImage, CurrentSketch.Thumbnail);
                
                _logger.LogInformation("Sketch {SketchId} auto-saved", CurrentSketch.Id);
                
                await LoadPriorSketchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-saving sketch");
            }
        }
        
        [RelayCommand]
        private async Task LoadPriorSketchAsync(string sketchId)
        {
            try
            {
                if (CurrentSketch.CurrentState >= WorkflowState.RoomDetected)
                {
                    await AutoSaveSketchAsync();
                }
                
                var loadedSketch = await _storageService.LoadSketchAsync(sketchId);
                
                BitmapImage? fullImage = null;
                if (!string.IsNullOrEmpty(loadedSketch.ImagePath) && File.Exists(loadedSketch.ImagePath))
                {
                    fullImage = await _storageService.LoadImageFromPathAsync(loadedSketch.ImagePath);
                }
                
                CurrentSketch = loadedSketch;
                _currentImage = fullImage;
                
                OnPropertyChanged(nameof(CurrentImage));
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(CurrentSketch));
                OnPropertyChanged(nameof(CurrentSketch.Rooms));
                OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
                OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));
                
                _logger.LogInformation("Loaded sketch {SketchId}", sketchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sketch {SketchId}", sketchId);
                await _dialogService.ShowErrorAsync(
                    "Failed to load the sketch. The sketch file may be corrupted or the image file may be missing.",
                    "Load Sketch Error");
            }
        }
        

        [RelayCommand]
        public void OpenLogsFolder()
        {
            try
            {
                var folder = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%/FloorTrace/Logs");
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open logs folder");
            }
        }
        
        [RelayCommand]
        public void TogglePriorSketches()
        {
            IsPriorSketchesVisible = !IsPriorSketchesVisible;
        }
        
        
        // Properties
        /// <summary>
        /// Gets the collection of previously saved sketches for quick access.
        /// </summary>
        public ObservableCollection<Sketch> PriorSketches { get; }
        
        /// <summary>
        /// Gets the current floor plan image being displayed and analyzed.
        /// </summary>
        public BitmapImage? CurrentImage => _currentImage;
        
        /// <summary>
        /// Gets a formatted string displaying the current scale factor.
        /// </summary>
        public string ScaleText => CurrentSketch != null ? $"Scale: {CurrentSketch.Scale:F2} px/ft" : "Scale: Not set";
        
        /// <summary>
        /// Gets a formatted string displaying the calculated side lengths.
        /// </summary>
        public string SideLengthsText => CurrentSketch?.SideLengths.Count > 0 ? $"Side Lengths: {string.Join(", ", CurrentSketch.SideLengths.Select(l => $"{l:F2} ft"))}" : "Side Lengths: N/A";
        
        /// <summary>
        /// Gets a formatted string displaying the calculated area in square feet.
        /// Returns an empty string if no area has been calculated yet.
        /// </summary>
        public string AreaText
        {
            get
            {
                if (CurrentSketch == null)
                    return string.Empty;

                // Treat values <= 0 as not yet calculated
                if (CurrentSketch.AreaSquareFeet <= 0.0)
                    return string.Empty;

                // Round to nearest whole number
                var rounded = Math.Round(CurrentSketch.AreaSquareFeet, 0, MidpointRounding.AwayFromZero);
                return $"{rounded:0} sq ft";
            }
        }
        
        // Private methods
        private async Task LoadPriorSketchesAsync()
        {
            try
            {
                if (!IsSavingEnabled)
                {
                    PriorSketches.Clear();
                    _logger.LogInformation("Saving is disabled, clearing prior sketches list");
                    return;
                }
                
                var sketches = await _storageService.LoadRecentSketchesAsync(MaxSavedSketches);
                
                PriorSketches.Clear();
                foreach (var sketch in sketches)
                {
                    PriorSketches.Add(sketch);
                }
                
                _logger.LogInformation("Loaded {Count} prior sketches", sketches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load prior sketches");
            }
        }
        
    }
}
