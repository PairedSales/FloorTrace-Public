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

namespace FloorTrace.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IScaleCalculationService _scaleCalculationService;
        private readonly IAreaCalculationService _areaCalculationService;
        private readonly IStorageService _storageService;
        private readonly IDialogService _dialogService;
        private readonly IConfiguration _configuration;
        
        [ObservableProperty]
        private Sketch currentSketch;
        private BitmapImage? _currentImage;
        private ObservableCollection<Sketch> _priorSketches = new();
        
        [ObservableProperty]
        private bool isPriorSketchesVisible = false;
        
        [ObservableProperty]
        private bool useInnerWallEdge;
        
        partial void OnUseInnerWallEdgeChanged(bool value)
        {
            // Automatically re-trace perimeter when the setting changes
            _ = OnWallEdgePreferenceChangedAsync();
        }
        
        public bool IsSavingEnabled { get; }
        
        public int MaxSavedSketches { get; }
        
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
            
            // Read saving settings from configuration
            IsSavingEnabled = _configuration.GetValue<bool>("ApplicationSettings:IsSavingEnabled");
            MaxSavedSketches = _configuration.GetValue<int>("ApplicationSettings:MaxSavedSketches");
            UseInnerWallEdge = _configuration.GetValue<bool>("ApplicationSettings:UseInnerWallEdge", true);
            
            CurrentSketch = new Sketch();
            PriorSketches = _priorSketches;
            
            _ = LoadPriorSketchesAsync();
        }
        
        // Commands
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
            // Load the image
            var image = await _imageProcessingService.LoadImageAsync(filePath);
            
            // Create thumbnail for the sidebar
            var thumbnail = await _imageProcessingService.CreateThumbnailAsync(image, Utilities.Constants.ThumbnailMaxWidth, Utilities.Constants.ThumbnailMaxHeight);
            
            // Update current sketch
            CurrentSketch = new Sketch
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                ImagePath = filePath,
                Thumbnail = thumbnail,
                DateCreated = DateTime.Now,
                DateModified = DateTime.Now,
                CurrentState = WorkflowState.ImageLoaded
            };
            
            // Store the full image for display
            _currentImage = image;
            
            // Notify UI of changes
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(ScaleText));
            OnPropertyChanged(nameof(SideLengthsText));
            OnPropertyChanged(nameof(AreaText));
        }
        
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
                // Detect wall lines for snapping
                var (horizontalLines, verticalLines) = await _imageProcessingService.DetectWallLinesAsync(CurrentImage);
                CurrentSketch.HorizontalWallLines = horizontalLines;
                CurrentSketch.VerticalWallLines = verticalLines;
                _logger.LogInformation("Detected {HCount} horizontal and {VCount} vertical wall lines for snapping", 
                    horizontalLines.Count, verticalLines.Count);

                // Detect rooms
                var detectedRooms = await _scaleCalculationService.DetectRoomsAsync(CurrentImage);

                if (detectedRooms.Any())
                {
                    // For now, we only care about the first detected room
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
                else
                {
                    _logger.LogInformation("No rooms detected.");
                    await _dialogService.ShowInfoAsync(
                        "No rooms with dimension labels were detected in the image. You may need to adjust the image or add dimensions manually.",
                        "No Rooms Detected");
                    
                    // Clear any previously detected rooms if none are found now
                    CurrentSketch.Rooms.Clear();
                    CurrentSketch.SelectedRoomForScale = null;
                    OnPropertyChanged(nameof(CurrentSketch.Rooms));
                    OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
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
                    
                    // Re-detect the perimeter with the new setting
                    var perimeterPoints = await _imageProcessingService.DetectPerimeterAsync(CurrentImage, UseInnerWallEdge);
                    
                    // Store in the current sketch
                    CurrentSketch.PerimeterPoints = perimeterPoints;
                    
                    // Notify UI of changes
                    OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                    
                    // Re-calculate area with the new perimeter
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
                // Detect the perimeter automatically with the current wall edge preference
                var perimeterPoints = await _imageProcessingService.DetectPerimeterAsync(CurrentImage, UseInnerWallEdge);
                
                // Store in the current sketch
                CurrentSketch.PerimeterPoints = perimeterPoints;
                CurrentSketch.CurrentState = WorkflowState.PerimeterTraced;
                
                _logger.LogInformation("Traced perimeter with {Count} points (inner edge: {UseInner})", 
                    perimeterPoints.Count, UseInnerWallEdge);
                
                // Notify UI of changes
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
        
        public async Task TryAutoCalculateAreaAsync()
        {
            // Silently calculate area if prerequisites are met
            if (CurrentSketch?.PerimeterPoints == null || CurrentSketch.PerimeterPoints.Count < 3)
            {
                // Not enough perimeter points, reset area to 0
                if (CurrentSketch != null)
                {
                    CurrentSketch.AreaSquareFeet = 0.0;
                    CurrentSketch.SideLengths.Clear();
                    OnPropertyChanged(nameof(AreaText));
                    OnPropertyChanged(nameof(SideLengthsText));
                }
                return;
            }

            if (CurrentSketch.Scale <= 0)
            {
                // Scale not set, reset area to 0
                CurrentSketch.AreaSquareFeet = 0.0;
                CurrentSketch.SideLengths.Clear();
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(SideLengthsText));
                return;
            }

            try
            {
                // Calculate area in pixels using Green's theorem
                var areaInPixels = await _areaCalculationService.CalculateAreaUsingGreensTheoremAsync(CurrentSketch.PerimeterPoints);
                
                // Convert to square feet
                var areaInSquareFeet = areaInPixels / (CurrentSketch.Scale * CurrentSketch.Scale);
                CurrentSketch.AreaSquareFeet = areaInSquareFeet;
                
                // Calculate side lengths in pixels
                var sideLengthsInPixels = await _areaCalculationService.CalculateSideLengthsAsync(CurrentSketch.PerimeterPoints);
                
                // Convert to feet
                var sideLengthsInFeet = new List<double>();
                foreach (var lengthInPixels in sideLengthsInPixels)
                {
                    var lengthInFeet = await _areaCalculationService.ConvertPixelsToFeetAsync(lengthInPixels, CurrentSketch.Scale);
                    sideLengthsInFeet.Add(lengthInFeet);
                }
                CurrentSketch.SideLengths = sideLengthsInFeet;
                CurrentSketch.CurrentState = WorkflowState.AreaCalculated;
                
                _logger.LogInformation("Auto-calculated area: {Area:F2} sq ft", areaInSquareFeet);
                
                // Update UI
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));
                
                // Auto-save after area calculated
                await AutoSaveSketchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-calculating area");
                // Silently fail for auto-calculation
            }
        }
        

        private async Task AutoSaveSketchAsync()
        {
            try
            {
                // Check if saving is enabled
                if (!IsSavingEnabled)
                {
                    _logger.LogDebug("Skipping auto-save, saving is disabled");
                    return;
                }
                
                // Only auto-save if we have detected a room
                if (CurrentSketch.CurrentState < WorkflowState.RoomDetected)
                {
                    _logger.LogDebug("Skipping auto-save, no room detected yet");
                    return;
                }
                
                CurrentSketch.DateModified = DateTime.Now;
                
                // Save to storage with images (auto-saved sketches are not permanent)
                await _storageService.SaveSketchAsync(CurrentSketch, _currentImage, CurrentSketch.Thumbnail);
                
                _logger.LogInformation("Sketch {SketchId} auto-saved", CurrentSketch.Id);
                
                // Refresh the list
                await LoadPriorSketchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-saving sketch");
                // Don't show error dialog for auto-save failures
            }
        }
        
        [RelayCommand]
        private async Task LoadPriorSketchAsync(string sketchId)
        {
            try
            {
                // Save current sketch if it has a detected room
                if (CurrentSketch.CurrentState >= WorkflowState.RoomDetected)
                {
                    await AutoSaveSketchAsync();
                }
                
                // Load the selected sketch
                var loadedSketch = await _storageService.LoadSketchAsync(sketchId);
                
                // Load the full image
                BitmapImage? fullImage = null;
                if (!string.IsNullOrEmpty(loadedSketch.ImagePath) && File.Exists(loadedSketch.ImagePath))
                {
                    fullImage = await _storageService.LoadImageFromPathAsync(loadedSketch.ImagePath);
                }
                
                // Update current sketch and image
                CurrentSketch = loadedSketch;
                _currentImage = fullImage;
                
                // Notify UI of changes
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
        private async Task LoadTestImageAsync()
        {
            try
            {
                // Get the path to ExampleFloorplan.png in the project root
                string testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExampleFloorplan.png");
                
                // Check if the file exists
                if (!File.Exists(testImagePath))
                {
                    _logger.LogWarning("Test image not found at: {Path}", testImagePath);
                    await _dialogService.ShowWarningAsync(
                        $"Test image not found at: {testImagePath}",
                        "Test Image Not Found");
                    return;
                }
                
                await LoadImageFromPathAsync(testImagePath);
                _logger.LogInformation("Test image loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading test image");
                await _dialogService.ShowErrorAsync(
                    "Failed to load the test image.",
                    "Test Image Error");
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
        public ObservableCollection<Sketch> PriorSketches { get; }
        
        public BitmapImage? CurrentImage => _currentImage;
        
        public string ScaleText => CurrentSketch != null ? $"Scale: {CurrentSketch.Scale:F2} px/ft" : "Scale: Not set";
        
        public string SideLengthsText => CurrentSketch?.SideLengths.Count > 0 ? $"Side Lengths: {string.Join(", ", CurrentSketch.SideLengths.Select(l => $"{l:F2} ft"))}" : "Side Lengths: N/A";
        
        public string AreaText => CurrentSketch != null ? $"{CurrentSketch.AreaSquareFeet:F2} sq ft" : "0 sq ft";
        
        // Private methods
        private async Task LoadPriorSketchesAsync()
        {
            try
            {
                // If saving is disabled, don't load prior sketches
                if (!IsSavingEnabled)
                {
                    PriorSketches.Clear();
                    _logger.LogInformation("Saving is disabled, clearing prior sketches list");
                    return;
                }
                
                var sketches = await _storageService.LoadRecentSketchesAsync(MaxSavedSketches);
                
                // Update UI collection
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
                // Don't show error dialog here as this runs on startup
            }
        }
    }
}
