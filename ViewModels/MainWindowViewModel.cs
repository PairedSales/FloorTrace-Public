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

namespace FloorTrace.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IScaleCalculationService _scaleCalculationService;
        private readonly IAreaCalculationService _areaCalculationService;
        private readonly IStorageService _storageService;
        
        [ObservableProperty]
        private Sketch currentSketch;
        private BitmapImage _currentImage;
        private ObservableCollection<Sketch> _priorSketches;

        [ObservableProperty]
        private bool isResultsPanelVisible = false;
        
        public MainWindowViewModel(
            IImageProcessingService imageProcessingService,
            IScaleCalculationService scaleCalculationService,
            IAreaCalculationService areaCalculationService,
            IStorageService storageService)
        {
            _imageProcessingService = imageProcessingService;
            _scaleCalculationService = scaleCalculationService;
            _areaCalculationService = areaCalculationService;
            _storageService = storageService;
            
            CurrentSketch = new Sketch();
            PriorSketches = new ObservableCollection<Sketch>();
            
            LoadPriorSketches();
        }
        
        // Commands
        [RelayCommand]
        public async void LoadImage()
        {
            try
            {
                var filePath = _imageProcessingService.ShowOpenFileDialog();
                if (string.IsNullOrEmpty(filePath))
                    return;
                
                await LoadImageFromPath(filePath);
                IsResultsPanelVisible = false;
                CurrentSketch.CurrentState = WorkflowState.ImageLoaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                // TODO: Show error dialog to user
            }
        }
        
        [RelayCommand]
        public async void PasteImage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("PasteImage command started");
                
                // Load the image from clipboard
                var image = await _imageProcessingService.LoadImageFromClipboardAsync();
                System.Diagnostics.Debug.WriteLine($"Image loaded from clipboard: {image?.PixelWidth}x{image?.PixelHeight}");
                
                // Create thumbnail for the sidebar
                var thumbnail = await _imageProcessingService.CreateThumbnailAsync(image, 150, 100);
                System.Diagnostics.Debug.WriteLine($"Thumbnail created: {thumbnail?.PixelWidth}x{thumbnail?.PixelHeight}");
                
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
                System.Diagnostics.Debug.WriteLine("Image stored in _currentImage");
                
                // Notify UI of changes
                OnPropertyChanged(nameof(CurrentImage));
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(IsResultsPanelVisible));
                
                System.Diagnostics.Debug.WriteLine("PasteImage command completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pasting image: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // TODO: Show error dialog to user
            }
        }
        
        private async Task LoadImageFromPath(string filePath)
        {
            // Load the image
            var image = await _imageProcessingService.LoadImageAsync(filePath);
            
            // Create thumbnail for the sidebar
            var thumbnail = await _imageProcessingService.CreateThumbnailAsync(image, 150, 100);
            
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
            OnPropertyChanged(nameof(IsResultsPanelVisible));
        }
        
        [RelayCommand]
        public async void DetectRoom()
        {
            if (CurrentImage == null)
            {
                // TODO: Show a message to the user that an image needs to be loaded first
                System.Diagnostics.Debug.WriteLine("No image loaded to detect rooms from.");
                return;
            }

            try
            {
                // Detect rooms
                var detectedRooms = await _scaleCalculationService.DetectRoomsAsync(CurrentImage);

                if (detectedRooms.Any())
                {
                    // For now, we only care about the first detected room
                    CurrentSketch.Rooms = detectedRooms;
                    CurrentSketch.SelectedRoomForScale = detectedRooms.First();
                    CurrentSketch.CurrentState = WorkflowState.RoomDetected;
                    System.Diagnostics.Debug.WriteLine($"Detected room: {CurrentSketch.SelectedRoomForScale.Name} with dimensions {CurrentSketch.SelectedRoomForScale.Dimensions}");
                    OnPropertyChanged(nameof(CurrentSketch.Rooms));
                    OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No rooms detected.");
                    // Clear any previously detected rooms if none are found now
                    CurrentSketch.Rooms.Clear();
                    CurrentSketch.SelectedRoomForScale = null;
                    OnPropertyChanged(nameof(CurrentSketch.Rooms));
                    OnPropertyChanged(nameof(CurrentSketch.SelectedRoomForScale));
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting rooms: {ex.Message}");
                // TODO: Show error dialog to user
            }
        }
        
        [RelayCommand]
        public async void SetScale()
        {
            try
            {
                if (CurrentImage == null || CurrentSketch?.SelectedRoomForScale == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot set scale: no image or no selected room.");
                    return;
                }

                var scale = await _scaleCalculationService.CalculateScaleFromRoomAsync(CurrentSketch.SelectedRoomForScale, CurrentImage);
                CurrentSketch.Scale = scale;
                CurrentSketch.CurrentState = WorkflowState.ScaleSet;
                OnPropertyChanged(nameof(ScaleText));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting scale: {ex.Message}");
            }
        }
        
        [RelayCommand]
        public async void TracePerimeter()
        {
            if (CurrentImage == null)
            {
                System.Diagnostics.Debug.WriteLine("No image loaded to trace perimeter.");
                return;
            }

            try
            {
                // Detect the perimeter automatically
                var perimeterPoints = await _imageProcessingService.DetectPerimeterAsync(CurrentImage);
                
                // Store in the current sketch
                CurrentSketch.PerimeterPoints = perimeterPoints;
                CurrentSketch.CurrentState = WorkflowState.PerimeterTraced;
                
                System.Diagnostics.Debug.WriteLine($"Traced perimeter with {perimeterPoints.Count} points");
                
                // Notify UI of changes
                OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error tracing perimeter: {ex.Message}");
                // TODO: Show error dialog to user
            }
        }
        
        [RelayCommand]
        public async void CalculateArea()
        {
            if (CurrentSketch?.PerimeterPoints == null || CurrentSketch.PerimeterPoints.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine("No perimeter points to calculate area.");
                return;
            }

            if (CurrentSketch.Scale <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Scale not set. Cannot calculate area.");
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
                
                System.Diagnostics.Debug.WriteLine($"Calculated area: {areaInSquareFeet:F2} sq ft");
                
                // Update UI
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));
                IsResultsPanelVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating area: {ex.Message}");
                // TODO: Show error dialog to user
            }
        }
        
        [RelayCommand]
        public void EditRoom()
        {
            try
            {
                if (CurrentSketch == null)
                    return;

                // Return to RoomDetected state
                CurrentSketch.CurrentState = WorkflowState.RoomDetected;
                IsResultsPanelVisible = false;
                
                // Clear perimeter and area data
                CurrentSketch.PerimeterPoints.Clear();
                CurrentSketch.AreaSquareFeet = 0.0;
                CurrentSketch.SideLengths.Clear();
                
                // Notify UI
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));
                OnPropertyChanged(nameof(CurrentSketch.PerimeterPoints));
                OnPropertyChanged(nameof(ScaleText));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                
                System.Diagnostics.Debug.WriteLine("Edit Room: Returned to RoomDetected state");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EditRoom: {ex.Message}");
            }
        }

        [RelayCommand]
        public void EditPerimeter()
        {
            try
            {
                if (CurrentSketch == null)
                    return;

                // Return to PerimeterTraced state
                CurrentSketch.CurrentState = WorkflowState.PerimeterTraced;
                IsResultsPanelVisible = false;
                
                // Clear area data but keep perimeter
                CurrentSketch.AreaSquareFeet = 0.0;
                CurrentSketch.SideLengths.Clear();
                
                // Notify UI
                OnPropertyChanged(nameof(CurrentSketch.CurrentState));
                OnPropertyChanged(nameof(SideLengthsText));
                OnPropertyChanged(nameof(AreaText));
                
                System.Diagnostics.Debug.WriteLine("Edit Perimeter: Returned to PerimeterTraced state");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EditPerimeter: {ex.Message}");
            }
        }

        [RelayCommand]
        public async void SaveSketch()
        {
            // Placeholder for save logic
            await Task.Delay(100);
            try
            {
                CurrentSketch.IsPermanent = true;
                CurrentSketch.DateModified = DateTime.Now;
                
                // TODO: Implement saving to storage
                LoadPriorSketches(); // Refresh the list
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving sketch: {ex.Message}");
            }
        }
        
        [RelayCommand]
        public async void LoadTestImage()
        {
            try
            {
                // Get the path to ExampleFloorplan.png in the project root
                string testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExampleFloorplan.png");
                
                // Check if the file exists
                if (!File.Exists(testImagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Test image not found at: {testImagePath}");
                    return;
                }
                
                await LoadImageFromPath(testImagePath);
                IsResultsPanelVisible = false;
                System.Diagnostics.Debug.WriteLine("Test image loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading test image: {ex.Message}");
            }
        }
        
        // Properties
        public ObservableCollection<Sketch> PriorSketches { get; }
        
        public BitmapImage CurrentImage => _currentImage;
        
        public string ScaleText => CurrentSketch != null ? $"Scale: {CurrentSketch.Scale:F2} px/ft" : "Scale: Not set";
        
        public string SideLengthsText => CurrentSketch?.SideLengths.Count > 0 ? $"Side Lengths: {string.Join(", ", CurrentSketch.SideLengths.Select(l => $"{l:F2} ft"))}" : "Side Lengths: N/A";
        
        public string AreaText => CurrentSketch != null ? $"Area: {CurrentSketch.AreaSquareFeet:F2} sq ft" : "Area: N/A";
        
        // Private methods
        private void LoadPriorSketches()
        {
            PriorSketches.Clear();
            // TODO: Load from storage service
        }
    }
}
