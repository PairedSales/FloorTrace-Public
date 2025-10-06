# FloorTrace

**FloorTrace** is a modern Windows application for calculating the area of real estate floor plan sketches that only show room dimensions. It automatically reads the room dimensions, finds the rooms, sets the scale, traces the perimeter, and calculates precise floor plan area.

See ExampleFloorplan.png for a sample floorplan sketch.

### Core Functionality
- 📸 **Image Loading** - Support for JPG, PNG, BMP, TIF, and TIFF floor plan images
- 🔍 **Automatic Dimension Detection** - OCR-based room dimension recognition
- 📏 **Scale Calculation** - Automatic scale calculation from detected room dimensions
- ✏️ **Manual Scale Adjustment** - Edit detected room dimensions and scale manually
- 🖊️ **Automatic Perimeter Tracing** - Automatic perimeter tracing
- 📐 **Area Calculation** - Precise square footage calculation
- 💾 **Auto-Save** - Configurable auto-save (disabled by default, max 25 sketches)
- ⭐ **Permanent Storage** - Save important sketches forever (when saving enabled)

### User Interface
- Intuitive toolbar with all essential functions
- Large canvas area for working with floor plans
- Toggleable side panel showing recent sketches with thumbnails
- Mouse wheel zoom with fit-to-window functionality
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
- Automatic pixel-to-feet conversion using scale
- Also calculates side lengths for display

#### Storage Service
- JSON-based sketch persistence
- Automatic cleanup of old non-permanent sketches
- Separate storage for permanent sketches

### Future Features
- [ ] Multiple room selection
- [ ] Measurement annotation tools
- [ ] Multi-floor support (multiple area calculations for the same image)

### Core Dependencies
- **.NET 8.0** - Runtime framework
- **WPF (Windows Presentation Foundation)** - UI framework
- **Microsoft.Toolkit.Mvvm** - MVVM framework for commands and data binding
- **System.Text.Json** - JSON serialization for data persistence
- **Microsoft.Extensions.Hosting** - Generic host and dependency injection
- **Serilog** - Structured logging to rolling log files

### System Requirements
- Windows 10/11 (x64)
- .NET 8.0 Runtime
- 4GB RAM minimum (8GB recommended)
- 100MB disk space

## Diagnostics and Logs

- Logs are written to `%LOCALAPPDATA%/FloorTrace/Logs/log-<date>.txt` with daily rolling and 7-day retention.
- Use the "Open Logs Folder" command (if wired to UI) or browse directly to collect logs for troubleshooting.
- Unhandled exceptions are captured and logged; the app shows a friendly message.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
