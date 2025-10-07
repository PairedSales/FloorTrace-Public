# FloorTrace v1.0.0-alpha Release Notes

## 🎉 First Alpha Release

This is the first public alpha release of FloorTrace - a professional-grade Windows application for calculating the area of real estate floor plan sketches using advanced computer vision algorithms.

## ⚠️ Alpha Disclaimer

**This is alpha software.** While functional, it may contain bugs and is not recommended for production use. Please report any issues you encounter.

## 🚀 What's New

### Core Features
- **Automatic Room Detection** - OCR-based dimension recognition
- **Intelligent Scale Calculation** - Automatic scale from detected room dimensions  
- **Advanced Perimeter Tracing** - Computer vision edge detection with inner/outer wall options
- **Precise Area Calculation** - Green's theorem implementation for accurate square footage
- **Interactive Editing** - Drag perimeter points, double-click to add, right-click to remove
- **Real-time Updates** - Automatic recalculation when parameters change

### User Experience
- **Modern UI** - Material Design 3 inspired interface
- **Keyboard Shortcuts** - Ctrl+O (load), Ctrl+V (paste), mouse wheel zoom
- **Comprehensive Logging** - Detailed logs for troubleshooting

### Technical Highlights
- **.NET 8.0** - Modern, cross-platform runtime
- **OpenCV Integration** - Advanced computer vision for image processing
- **MVVM Architecture** - Clean separation of concerns
- **Dependency Injection** - Modular, testable design
- **Async/Await** - Responsive UI with proper async patterns

## 📋 System Requirements

- **OS**: Windows 10/11 (x64)
- **Runtime**: .NET 8.0 Desktop Runtime (download from Microsoft)
- **RAM**: 4GB minimum (8GB recommended)
- **Storage**: 100MB free space
- **Graphics**: DirectX 11 compatible

## 🛠️ Installation

### Option 1: Framework-Dependent (Recommended)
1. Install [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Download and extract the release ZIP
3. Run `FloorTrace.exe`

### Option 2: Self-Contained
1. Download and extract the release ZIP
2. Run `FloorTrace.exe` (no additional installation required)

## 🎯 Quick Start Guide

1. **Launch FloorTrace**
2. **Load Image**: Press `Ctrl+O` or click "Load Image"
3. **Detect Room**: Click "Detect Room" to automatically find dimensions
4. **Trace Perimeter**: Click "Trace Perimeter" to outline the floor plan
5. **View Results**: Area is calculated automatically

## 🔧 Configuration

Edit `appsettings.json` to customize:
```json
{
  "ApplicationSettings": {
    "UseInnerWallEdge": true
  }
}
```

## 📊 Supported Image Formats

- JPG/JPEG
- PNG
- BMP
- TIF/TIFF
- WebP

## 🐛 Known Issues

- Single room detection only (multi-room support planned)
- Some edge cases in perimeter detection may require manual adjustment
- Large images (>10MB) may process slowly
- Dimension parsing works best with clear, readable text

## 🔮 Planned Features

- Multiple room selection and analysis
- Measurement annotation tools
- Multi-floor support
- Export functionality (PDF, CAD)
- Batch processing
- Advanced OCR improvements
- 3D visualization

## 📞 Support & Feedback

### Reporting Issues
1. Check the logs in `%LOCALAPPDATA%/FloorTrace/Logs/`
2. Create an issue on [GitHub](https://github.com/PairedSales/FloorTrace-Public/issues)
3. Include:
   - Description of the problem
   - Steps to reproduce
   - Log file contents
   - System information

### Getting Help
- Check the [README.md](README.md) for detailed documentation
- Review the [troubleshooting section](README.md#troubleshooting)
- Open an issue for feature requests or bug reports

## 🏗️ Development

### Building from Source
```bash
git clone https://github.com/PairedSales/FloorTrace-Public.git
cd FloorTrace-Public
dotnet restore
dotnet build
dotnet run
```

### Running Tests
```bash
dotnet test FloorTrace.Tests/
```

## 📄 License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **OpenCV** - Computer vision and image processing
- **SkiaSharp** - Advanced graphics rendering
- **CommunityToolkit.Mvvm** - MVVM framework
- **Serilog** - Structured logging

---

**Version**: 1.0.0-alpha  
**Release Date**: January 2025  
**Repository**: [FloorTrace-Public](https://github.com/PairedSales/FloorTrace-Public)

Thank you for testing FloorTrace! Your feedback helps make this tool better for everyone.


