# FloorTrace

**FloorTrace** is a modern Windows application for calculating the area of real estate floor plan sketches that only show room dimensions. It automatically reads the room dimensions, finds the rooms, sets the scale, traces the perimeter, and calculates precise floor plan area.

### Core Functionality
- 📸 **Image Loading** - Support for JPG, PNG, BMP, TIF, and TIFF floor plan images
- 🔍 **Automatic Dimension Detection** - OCR-based room dimension recognition
- 📏 **Scale Calculation** - Automatic scale calculation from detected room dimensions
- ✏️ **Manual Scale Adjustment** - Edit detected room dimensions and scale manually
- 🖊️ **Automatic Perimeter Tracing** - Automatic perimeter tracing using Hough Transform
- 📐 **Area Calculation** - Precise square footage calculation using Green's Theorem
- 💾 **Auto-Save** - Last 25 sketches saved automatically
- ⭐ **Permanent Storage** - Save important sketches forever

### User Interface
- Intuitive toolbar with all essential functions
- Large canvas area for working with floor plans
- Side panel showing recent sketches with thumbnails
- Real-time results display (scale, perimeter, area)
- Keyboard shortcuts for common actions

### Basic Workflow

1. **Load an Image**
   - Pasting an image with 'Ctrl+V' is quickest way to import a sketch (Important: The user is only allowed to paste an image if there is no other image currently loaded)
   - Otherwise, click "Load Image", or press `Ctrl+O`
   - Select a floor plan image file
   - The image will appear in the canvas area

2. **Set Scale**
   - Click "Detect Room" to automatically detect the first room for scaling.
   - The app will analyze the image and add a transparent overlay to the detected room.
   - The overlay will show the detected room dimensions.
   - The user will be able to modify the overlay's size and placement along with the room dimensions.
   - The user will select a room (the first one detected) and click "Use Selected Room for Scale" button to finalize.

3. **Trace Perimeter**
   - The "Trace Perimeter" button automatically finds the exterior edges or curves
   - The traced area will be highlighted (Important: The room overlays from the previous step will disappear for later use if necessary)
   - The user is able to modify the outline if there are errors
   - Click "Calculate Area" to finalize

4. **Calculate Area**
   - Results appear showing:
     - Scale (pixels per foot)
     - Side Lengths (feet)
     - Total Area (square feet)

5. **Save and Recall Past Sketches** 
   - The last 25 sketches are saved automatically and displayed in the side panel
   - Thumbnails show a preview of each floor plan.
   - Click any sketch in the "Prior Sketches" panel to reload it
   - To keep a sketch permanently, hit `Ctrl+S' or select the save button
   - The sketch will be marked as permanent and won't be auto-deleted

### Keyboard Shortcuts

- `Ctrl+V` - Load Image from Clipboard
- `Ctrl+O` - Load Image
- `Ctrl+S` - Save Sketch Permanently

## Project Structure

```
FloorTrace/
├── App.xaml                    # Application definition and resources
├── App.xaml.cs                 # Application code-behind
├── MainWindow.xaml             # Main window UI layout
├── MainWindow.xaml.cs          # Main window code-behind
├── FloorTrace.csproj           # Project file with dependencies
├── Models/
│   └── Sketch.cs               # Data models for sketches and rooms
├── ViewModels/
│   └── MainWindowViewModel.cs  # Main window business logic
└── Services/
    ├── IImageProcessingService.cs      # Image loading and processing interface
    ├── ImageProcessingService.cs       # Image loading and processing implementation
    ├── IScaleCalculationService.cs     # Scale calculation interface
    ├── ScaleCalculationService.cs      # Scale calculation implementation
    ├── IAreaCalculationService.cs      # Area calculation interface
    ├── AreaCalculationService.cs       # Area calculation implementation
    ├── IStorageService.cs              # Storage interface
    └── StorageService.cs               # Storage implementation
```

## Architecture

FloorTrace follows the **MVVM (Model-View-ViewModel)** pattern for clean separation of concerns:

- **Models**: Data structures for sketches, rooms, and geometry
- **Views**: XAML-based UI components
- **ViewModels**: Business logic and data binding layer
- **Services**: Reusable services for storage, image processing, and calculations

### Key Components

#### Image Processing Service
- OCR-based text detection
- Room dimension parsing using regex patterns
- Thumbnail generation for sketch previews

#### Scale Calculation Service
- Scans the sketch for the first room dimensions and location
- Overlays the first room with the read room dimensions
- Manual Room overlay modification capability
- Currently supports only a single room, but designed for future multi-room support
- Sets the pixels-per-foot scale based on the user selected room

#### Area Calculation Service
- **Green's Theorem** for area calculation
- Automatic pixel-to-feet conversion using scale
- Also calculates side lengths for display

#### Storage Service
- JSON-based sketch persistence
- Automatic cleanup of old non-permanent sketches
- Separate storage for permanent sketches

## Algorithms

### Area Calculation (Green’s theorem)

The app uses Green’s theorem to calculate the area of the floorplan traced by the user.

### Scale Calculation

Scale is calculated from room dimensions:

\[
\text{Scale} = \frac{\text{Pixel Distance}}{\text{Real Distance (feet)}}
\]

### Features
- [ ] Full OCR integration using Windows.Media.Ocr
- [ ] Machine learning-based floor plan element detection
- [ ] Automatic perimeter detection
- [ ] Room-by-room area breakdown

### Future Features
- [ ] Multi-floor support (multiple area calculations for the same image)
- [ ] Measurement annotation tools

## Dependencies

### Core Dependencies
- **.NET 8.0** - Runtime framework
- **WPF (Windows Presentation Foundation)** - UI framework
- **Microsoft.Toolkit.Mvvm** - MVVM framework for commands and data binding
- **System.Text.Json** - JSON serialization for data persistence

### Future Dependencies (to be added)
- **Windows.Media.Ocr** - Optical Character Recognition for room dimension detection
- **OpenCV.NET** or **Emgu.CV** - Computer vision for image processing and perimeter detection
- **Microsoft.Extensions.DependencyInjection** - Dependency injection container
- **Microsoft.Extensions.Logging** - Logging framework

### System Requirements
- Windows 10/11 (x64)
- .NET 8.0 Runtime
- 4GB RAM minimum (8GB recommended)
- 100MB disk space

## License

This project is licensed under the MIT License - see the LICENSE file for details.
