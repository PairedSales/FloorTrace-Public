# FloorTrace

**FloorTrace** is a professional-grade Windows application for calculating the area of real estate floor plan sketches that only show room dimensions. It automatically reads the room dimensions, finds the rooms, sets the scale, traces the perimeter, and calculates precise floor plan area using advanced computer vision algorithms.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [System Requirements](#system-requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [User Guide](#user-guide)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Development](#development)
- [API Documentation](#api-documentation)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Overview

FloorTrace leverages computer vision and geometric algorithms to automatically analyze floor plan images and calculate precise area measurements. The application uses OpenCV for image processing, implements Green's theorem for accurate polygon area calculation, and provides an intuitive MVVM-based user interface.

### Key Technologies

- **.NET 8.0** - Modern, cross-platform runtime
- **WPF** - Rich desktop user interface
- **OpenCV** - Computer vision and image processing
- **SkiaSharp** - Advanced graphics rendering
- **MVVM Pattern** - Clean separation of concerns
- **Dependency Injection** - Modular, testable architecture

See `ExampleFloorplan.png` for a sample floorplan sketch.

## Features

### Core Functionality
- 📸 **Image Loading** - Support for JPG, PNG, BMP, TIF, TIFF, and WebP floor plan images
- 🔍 **Automatic Dimension Detection** - OCR-based room dimension recognition using computer vision
- 📏 **Scale Calculation** - Automatic scale calculation from detected room dimensions
- ✏️ **Manual Scale Adjustment** - Edit detected room dimensions and scale manually with real-time updates
- 🖊️ **Automatic Perimeter Tracing** - Advanced edge detection with inner/outer wall options
- 📐 **Area Calculation** - Precise square footage calculation using Green's theorem
- 💾 **Auto-Save** - Configurable auto-save (disabled by default, max 25 sketches)
- ⭐ **Permanent Storage** - Save important sketches forever (when saving enabled)

### Advanced Features
- 🎯 **Zoom-to-Point** - Mouse wheel zoom that centers on cursor position
- 📐 **Wall Line Detection** - Automatic detection of horizontal and vertical wall lines for UI snapping
- 🔄 **Real-time Updates** - Automatic area recalculation when perimeter or scale changes
- 📊 **Detailed Measurements** - Side lengths, area, and scale information
- 🖱️ **Interactive Editing** - Drag and drop perimeter points, double-click to add, right-click to remove

### User Interface
- Intuitive toolbar with all essential functions
- Large canvas area for working with floor plans
- Toggleable side panel showing recent sketches with thumbnails
- Mouse wheel zoom with fit-to-window functionality
- Keyboard shortcuts for common actions

## System Requirements

- **Operating System**: Windows 10/11 (x64)
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 4GB RAM minimum (8GB recommended)
- **Storage**: 100MB disk space
- **Graphics**: DirectX 11 compatible graphics card

## Installation

### Option 1: Download Release
1. Download the latest release from the [Releases page](https://github.com/your-repo/FloorTrace/releases)
2. Extract the ZIP file to your desired location
3. Run `FloorTrace.exe`

### Option 2: Build from Source
1. Clone the repository: `git clone https://github.com/your-repo/FloorTrace.git`
2. Install .NET 8.0 SDK
3. Open `FloorTrace.sln` in Visual Studio 2022 or later
4. Build and run the solution

## Quick Start

1. **Launch FloorTrace**
2. **Load an Image**: Press `Ctrl+O` or click "Load Image"
3. **Detect Room**: Click "Detect Room" to automatically find dimensions
4. **Trace Perimeter**: Click "Trace Perimeter" to outline the floor plan
5. **View Results**: Area is calculated automatically and displayed

## User Guide

1. **Load an Image**
   - Pasting an image with 'Ctrl+V' is quickest way to import a sketch (Important: The user is only allowed to paste an image if there is no other image currently loaded)
   - Otherwise, click "Load Image", or press `Ctrl+O`
   - Select a floor plan image file
   - The image will appear in the canvas area

2. **Set Scale**
   - Click "Detect Room" to automatically detect the first room for scaling.
   - The app will analyze the image and add a transparent overlay to the detected room.
   - The overlay will show the detected room dimensions and the scale is calculated automatically.
   - The user can modify the overlay's size and placement along with the room dimensions.
   - The scale recalculates automatically whenever the overlay or dimensions are modified.

3. **Trace Perimeter**
   - The "Trace Perimeter" button automatically finds the exterior edges of the floorplan sketch.
   - The sketches are polygons and in very rare cases there are curved walls.
   - The traced area will be highlighted (Important: The room overlays from the previous step will disappear for later use)
   - The user is able to modify the automatic outline by clicking and dragging vertices
   - Double click adds a new point.  Right click removes the point.

4. **Calculate Area**
   - Once the user is satisfied with the perimeter trace, "Calculate Area" is pressed
   - Further editing of the perimeter is prevented.  In the Calculate Area state, only panning the image with the mouse and zooming is allowed
   - Two buttons appear: "Edit Room" and "Edit Perimeter" for revisions
   - Results appear showing:
     - Scale (pixels per foot)
     - Side Lengths (feet)
     - Total Area (square feet)


5. **Save and Recall Past Sketches** 
   - When saving is enabled, sketches are saved automatically and displayed in the side panel
   - The number of saved sketches is configurable (default: 25)
   - Thumbnails show a preview of each floor plan with area and date information
   - Click any sketch in the "Prior Sketches" panel to reload it
   - The side panel can be toggled on/off using the "Prior Sketches" button
   - Note: Saving is disabled by default and can be enabled in appsettings.json

### Keyboard Shortcuts

- `Ctrl+V` - Load Image from Clipboard
- `Ctrl+O` - Load Image
- Mouse Wheel - Zoom in/out on image
- Fit to Window button - Auto-fit image to viewport

## Configuration

The application can be configured by modifying `appsettings.json`:

```json
{
  "ApplicationSettings": {
    "IsSavingEnabled": false,
    "MaxSavedSketches": 25
  }
}
```

### Configuration Options

- **IsSavingEnabled**: Enable/disable saving functionality (default: false)
- **MaxSavedSketches**: Maximum number of sketches to save when saving is enabled (default: 25)

To enable saving, set `"IsSavingEnabled": true` in the configuration file and restart the application.

## Project Structure

```
FloorTrace/
├── App.xaml                    # Application definition and resources
├── App.xaml.cs                 # Application code-behind
├── MainWindow.xaml             # Main window UI layout
├── MainWindow.xaml.cs          # Main window code-behind
├── FloorTrace.csproj           # Project file with dependencies
├── appsettings.json            # Application configuration
├── Models/
│   ├── Sketch.cs               # Data models for sketches and rooms
│   └── WindowSettings.cs       # Window state persistence
├── ViewModels/
│   └── MainWindowViewModel.cs  # Main window business logic
├── Services/
│   ├── IImageProcessingService.cs      # Image loading and processing interface
│   ├── ImageProcessingService.cs       # Image loading and processing implementation
│   ├── IScaleCalculationService.cs     # Scale calculation interface
│   ├── ScaleCalculationService.cs      # Scale calculation implementation
│   ├── IAreaCalculationService.cs      # Area calculation interface
│   ├── AreaCalculationService.cs       # Area calculation implementation
│   ├── IStorageService.cs              # Storage interface
│   ├── StorageService.cs               # Storage implementation
│   ├── IDialogService.cs               # Dialog service interface
│   └── DialogService.cs                # Dialog service implementation
└── Utilities/
    ├── Constants.cs             # Application constants
    └── DimensionParser.cs       # Dimension parsing utilities
```

## Architecture

FloorTrace follows the **MVVM (Model-View-ViewModel)** pattern with dependency injection for clean separation of concerns and testability:

### Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│      Views       │    │   ViewModels      │    │     Models       │
│   (XAML UI)      │◄──►│  (Business Logic) │◄──►│  (Data Structures)│
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │     Services     │
                       │ (Cross-cutting)  │
                       └──────────────────┘
```

### Key Components

#### Models
- **`Sketch`**: Main data model containing all floor plan analysis data
- **`Room`**: Represents detected rooms with dimensions and bounds
- **`WorkflowState`**: Enumeration of analysis workflow states

#### ViewModels
- **`MainWindowViewModel`**: Primary business logic coordinator
- Implements `INotifyPropertyChanged` for data binding
- Uses `RelayCommand` for UI command handling
- Orchestrates all analysis operations

#### Services

##### Image Processing Service (`IImageProcessingService`)
- **Purpose**: Computer vision and image manipulation operations
- **Key Features**:
  - Multi-format image loading (JPG, PNG, BMP, TIF, TIFF, WebP)
  - Clipboard image support
  - Thumbnail generation
  - Perimeter detection using OpenCV edge detection
  - Wall line detection for UI snapping assistance
- **Algorithms**: Canny edge detection, morphological operations, contour analysis

##### Scale Calculation Service (`IScaleCalculationService`)
- **Purpose**: Room detection and scale factor calculation
- **Key Features**:
  - OCR-based room dimension detection
  - Scale calculation from detected room dimensions
  - Room overlay management
  - Dimension validation
- **Current Limitation**: Single room support (designed for future multi-room expansion)

##### Area Calculation Service (`IAreaCalculationService`)
- **Purpose**: Geometric calculations and unit conversions
- **Key Features**:
  - Green's theorem implementation for accurate polygon area calculation
  - Side length calculations
  - Pixel-to-feet unit conversions
- **Algorithms**: Green's theorem, Euclidean distance calculations

##### Storage Service (`IStorageService`)
- **Purpose**: Data persistence and sketch management
- **Key Features**:
  - JSON-based sketch serialization
  - Image file management
  - Automatic cleanup of old sketches
  - Permanent vs. temporary sketch handling
- **Storage Location**: `%LOCALAPPDATA%/FloorTrace/`

##### Dialog Service (`IDialogService`)
- **Purpose**: User notifications and confirmations
- **Key Features**:
  - Error, warning, and information dialogs
  - Confirmation dialogs
  - Non-blocking async operations

## Development

### Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK
- Git

### Building the Project
```bash
git clone https://github.com/your-repo/FloorTrace.git
cd FloorTrace
dotnet restore
dotnet build
dotnet run
```

### Running Tests
```bash
dotnet test FloorTrace.Tests/
```

### Project Structure
```
FloorTrace/
├── App.xaml                    # Application definition and resources
├── App.xaml.cs                 # Application startup and configuration
├── MainWindow.xaml             # Main window UI layout
├── MainWindow.xaml.cs          # Main window code-behind
├── FloorTrace.csproj           # Project file with dependencies
├── appsettings.json            # Application configuration
├── Models/                     # Data models
│   ├── Sketch.cs               # Main sketch data model
│   └── WindowSettings.cs       # Window state persistence
├── ViewModels/                 # MVVM ViewModels
│   └── MainWindowViewModel.cs  # Main window business logic
├── Services/                   # Service layer
│   ├── IImageProcessingService.cs      # Image processing interface
│   ├── ImageProcessingService.cs       # Image processing implementation
│   ├── IScaleCalculationService.cs     # Scale calculation interface
│   ├── ScaleCalculationService.cs      # Scale calculation implementation
│   ├── IAreaCalculationService.cs      # Area calculation interface
│   ├── AreaCalculationService.cs       # Area calculation implementation
│   ├── IStorageService.cs              # Storage interface
│   ├── StorageService.cs               # Storage implementation
│   ├── IDialogService.cs               # Dialog service interface
│   └── DialogService.cs                # Dialog service implementation
├── Controls/                   # Custom UI controls
│   ├── PerimeterOverlayControl.xaml    # Perimeter editing control
│   ├── PerimeterOverlayControl.xaml.cs
│   ├── RoomOverlayControl.xaml         # Room editing control
│   └── RoomOverlayControl.xaml.cs
└── Utilities/                  # Utility classes
    ├── Constants.cs             # Application constants
    ├── DimensionParser.cs       # Dimension parsing utilities
    └── PathUtils.cs             # Path utility functions
```

### Core Dependencies
- **.NET 8.0** - Runtime framework
- **WPF (Windows Presentation Foundation)** - UI framework
- **CommunityToolkit.Mvvm** - MVVM framework for commands and data binding
- **System.Text.Json** - JSON serialization for data persistence
- **Microsoft.Extensions.Hosting** - Generic host and dependency injection
- **Serilog** - Structured logging to rolling log files
- **OpenCvSharp4** - Computer vision and image processing
- **SkiaSharp** - Advanced graphics rendering

## API Documentation

### Service Interfaces

#### IImageProcessingService
```csharp
/// <summary>
/// Defines the contract for image processing operations including loading, saving, and computer vision analysis.
/// </summary>
public interface IImageProcessingService
{
    Task<BitmapImage> LoadImageAsync(string filePath);
    Task<BitmapImage> LoadImageFromClipboardAsync();
    Task<BitmapImage> CreateThumbnailAsync(BitmapImage sourceImage, int maxWidth, int maxHeight);
    Task<bool> SaveImageAsync(BitmapImage image, string filePath);
    string ShowOpenFileDialog();
    Task<List<PointF>> DetectPerimeterAsync(BitmapImage image, bool useInnerEdge = true);
    Task<(List<float> HorizontalLines, List<float> VerticalLines)> DetectWallLinesAsync(BitmapImage image);
}
```

#### IScaleCalculationService
```csharp
/// <summary>
/// Defines the contract for scale calculation operations based on detected room dimensions.
/// </summary>
public interface IScaleCalculationService
{
    Task<List<Room>> DetectRoomsAsync(BitmapImage image);
    Task<double> CalculateScaleFromRoomAsync(Room room, BitmapImage image);
    Task<bool> ValidateRoomDimensionsAsync(string dimensions);
    Task<Room> UpdateRoomOverlayAsync(Room room, BitmapImage image);
}
```

#### IAreaCalculationService
```csharp
/// <summary>
/// Defines the contract for area calculation operations using geometric algorithms.
/// </summary>
public interface IAreaCalculationService
{
    Task<List<PointF>> TracePerimeterAsync(BitmapImage image);
    Task<double> CalculateAreaUsingGreensTheoremAsync(List<PointF> perimeterPoints);
    Task<List<double>> CalculateSideLengthsAsync(List<PointF> perimeterPoints);
    Task<double> ConvertPixelsToFeetAsync(double pixels, double scale);
    Task<double> ConvertFeetToPixelsAsync(double feet, double scale);
}
```

### Data Models

#### Sketch
```csharp
/// <summary>
/// Represents a floor plan sketch with all associated analysis data including rooms, perimeter, and calculated measurements.
/// </summary>
public class Sketch
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsPermanent { get; set; }
    public string ImagePath { get; set; }
    public double Scale { get; set; } // pixels per foot
    public List<Room> Rooms { get; set; }
    public List<PointF> PerimeterPoints { get; set; }
    public double AreaSquareFeet { get; set; }
    public List<double> SideLengths { get; set; }
    public WorkflowState CurrentState { get; set; }
}
```

## Troubleshooting

### Common Issues

#### Image Loading Problems
- **Issue**: "Failed to load image" error
- **Solution**: Ensure the image file is not corrupted and is in a supported format (JPG, PNG, BMP, TIF, TIFF, WebP)

#### Room Detection Issues
- **Issue**: No rooms detected
- **Solution**: 
  - Ensure the image has clear dimension labels
  - Try adjusting image contrast/brightness
  - Check that dimension text is readable and not too small

#### Perimeter Tracing Problems
- **Issue**: Incorrect perimeter detection
- **Solution**:
  - Toggle between inner/outer wall edge detection
  - Manually adjust perimeter points by dragging
  - Ensure the floor plan has clear wall boundaries

#### Performance Issues
- **Issue**: Slow image processing
- **Solution**:
  - Reduce image resolution before loading
  - Close other applications to free up memory
  - Ensure graphics drivers are up to date

### Logs and Diagnostics

- **Log Location**: `%LOCALAPPDATA%/FloorTrace/Logs/log-<date>.txt`
- **Log Retention**: 7 days with daily rolling
- **Log Levels**: Debug, Information, Warning, Error
- **Access Logs**: Use "Open Logs Folder" command in the application

### Getting Help

1. Check the logs for error details
2. Try the troubleshooting steps above
3. Create an issue on GitHub with:
   - Description of the problem
   - Steps to reproduce
   - Log file contents
   - System information

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

### Code Style
- Follow C# coding conventions
- Use XML documentation comments for public APIs
- Write unit tests for new features
- Ensure code is properly commented

## Future Features
- [ ] Multiple room selection and analysis
- [ ] Measurement annotation tools
- [ ] Multi-floor support (multiple area calculations for the same image)
- [ ] Export functionality (PDF, CAD formats)
- [ ] Batch processing of multiple floor plans
- [ ] Advanced OCR for better dimension detection
- [ ] 3D visualization capabilities

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
