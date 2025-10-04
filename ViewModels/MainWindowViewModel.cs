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
        
        private Sketch _currentSketch;
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
            
            _currentSketch = new Sketch();
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
                _currentSketch = new Sketch
                {
                    Name = $"Pasted Image {DateTime.Now:yyyy-MM-dd HH:mm}",
                    ImagePath = "Clipboard", // Special indicator for pasted images
                    Thumbnail = thumbnail,
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now
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
            _currentSketch = new Sketch
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                ImagePath = filePath,
                Thumbnail = thumbnail,
                DateCreated = DateTime.Now,
                DateModified = DateTime.Now
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
        public async void DetectRooms()
        {
            // Placeholder for room detection logic
            await Task.Delay(100);
        }
        
        [RelayCommand]
        public async void SetScale()
        {
            // Placeholder for scale setting logic
            await Task.Delay(100);
        }
        
        [RelayCommand]
        public async void TracePerimeter()
        {
            // Placeholder for perimeter tracing logic
            await Task.Delay(100);
        }
        
        [RelayCommand]
        public async void CalculateArea()
        {
            // Placeholder for area calculation logic
            await Task.Delay(100);
            IsResultsPanelVisible = true;
        }
        
        [RelayCommand]
        public async void SaveSketch()
        {
            // Placeholder for save logic
            await Task.Delay(100);
            try
            {
                _currentSketch.IsPermanent = true;
                _currentSketch.DateModified = DateTime.Now;
                
                // TODO: Implement saving to storage
                LoadPriorSketches(); // Refresh the list
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving sketch: {ex.Message}");
            }
        }
        
        // Properties
        public ObservableCollection<Sketch> PriorSketches { get; }
        
        public BitmapImage CurrentImage => _currentImage;
        
        public string ScaleText => _currentSketch != null ? $"Scale: {_currentSketch.Scale:F2} px/ft" : "Scale: Not set";
        
        public string SideLengthsText => _currentSketch?.SideLengths.Count > 0 ? $"Side Lengths: {string.Join(", ", _currentSketch.SideLengths.Select(l => $"{l:F2} ft"))}" : "Side Lengths: N/A";
        
        public string AreaText => _currentSketch != null ? $"Area: {_currentSketch.AreaSquareFeet:F2} sq ft" : "Area: N/A";
        
        // Private methods
        private void LoadPriorSketches()
        {
            PriorSketches.Clear();
            // TODO: Load from storage service
        }
    }
}
