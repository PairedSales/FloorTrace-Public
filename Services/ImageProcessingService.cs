using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Microsoft.Extensions.Logging;
using FloorTrace.Utilities;

namespace FloorTrace.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        // Parameterless constructor for fallback scenarios
        public ImageProcessingService()
        {
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageProcessingService>.Instance;
        }
        public async Task<BitmapImage> LoadImageAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Handle WebP files using SkiaSharp
                if (extension == ".webp")
                {
                    return LoadWebPImage(filePath);
                }
                
                // Handle other formats using standard WPF
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            });
        }
        
        private BitmapImage LoadWebPImage(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var skiaStream = new SKManagedStream(stream);
            using var skiaImage = SKImage.FromEncodedData(skiaStream);
            
            // Create a memory stream from the SkiaSharp bitmap
            using var memoryStream = new MemoryStream();
            var data = skiaImage.Encode(SKEncodedImageFormat.Png, 100);
            memoryStream.Write(data.ToArray());
            memoryStream.Position = 0;
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memoryStream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        public async Task<BitmapImage> LoadImageFromClipboardAsync()
        {
            return await Task.Run(() =>
            {
                // Clipboard operations must be on UI thread
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!System.Windows.Clipboard.ContainsImage())
                        throw new InvalidOperationException("No image in clipboard");
                    
                    var bitmapSource = System.Windows.Clipboard.GetImage();
                    if (bitmapSource == null)
                        throw new InvalidOperationException("Failed to get image from clipboard");
                    
                    // Convert BitmapSource to BitmapImage
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    
                    using (var stream = new MemoryStream())
                    {
                        encoder.Save(stream);
                        stream.Position = 0;
                        
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                });
            });
        }
        
        public async Task<BitmapImage> CreateThumbnailAsync(BitmapImage sourceImage, int maxWidth, int maxHeight)
        {
            return await Task.Run(() =>
            {
                var scaleX = (double)maxWidth / sourceImage.PixelWidth;
                var scaleY = (double)maxHeight / sourceImage.PixelHeight;
                var scale = Math.Min(scaleX, scaleY);
                
                var thumbnail = new TransformedBitmap(sourceImage, new ScaleTransform(scale, scale));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            });
        }
        
        public async Task<bool> SaveImageAsync(BitmapImage image, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }
                    _logger.LogInformation("Image saved successfully to {FilePath}", filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save image to {FilePath}", filePath);
                    return false;
                }
            }).ConfigureAwait(false);
        }
        
        public string ShowOpenFileDialog()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Floor Plan Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp|All files (*.*)|*.*",
                FilterIndex = 1
            };
            
            var result = openFileDialog.ShowDialog();
            return result == true ? openFileDialog.FileName : string.Empty;
        }
        
        public async Task<List<PointF>> DetectPerimeterAsync(BitmapImage image)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert BitmapImage to System.Drawing.Bitmap
                    Bitmap bitmap = BitmapImageToBitmap(image);
                    
                    // Convert to OpenCV Mat
                    using var mat = BitmapConverter.ToMat(bitmap);
                    using var gray = new Mat();
                    using var blurred = new Mat();
                    using var edges = new Mat();
                    
                    // Convert to grayscale
                    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                    
                    // Apply Gaussian blur to reduce noise
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), Constants.GaussianBlurSigma);
                    
                    // Apply Canny edge detection
                    Cv2.Canny(blurred, edges, Constants.CannyLowThreshold, Constants.CannyHighThreshold);
                    
                    // Apply morphological operations to close gaps
                    using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    using var closed = new Mat();
                    Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel);
                    
                    // Find contours
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(closed, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    
                    if (contours.Length == 0)
                    {
                        // No contours found, return a default rectangle
                        return CreateDefaultPerimeter(mat.Width, mat.Height);
                    }
                    
                    // Find the largest contour (assuming it's the floor plan perimeter)
                    var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    
                    // Approximate the contour to a polygon
                    var epsilon = Constants.ContourApproximationEpsilon * Cv2.ArcLength(largestContour, true);
                    var approxPolygon = Cv2.ApproxPolyDP(largestContour, epsilon, true);
                    
                    // Convert to List<PointF>
                    var perimeterPoints = new List<PointF>();
                    foreach (var point in approxPolygon)
                    {
                        perimeterPoints.Add(new PointF(point.X, point.Y));
                    }
                    
                    // Ensure we have at least 3 points
                    if (perimeterPoints.Count < Constants.MinPerimeterPoints)
                    {
                        _logger.LogWarning("Detected perimeter has fewer than {MinPoints} points, using default", Constants.MinPerimeterPoints);
                        return CreateDefaultPerimeter(mat.Width, mat.Height);
                    }
                    
                    _logger.LogInformation("Successfully detected perimeter with {PointCount} points", perimeterPoints.Count);
                    bitmap.Dispose();
                    return perimeterPoints;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting perimeter, returning default rectangle");
                    // Return a default rectangle on error
                    return CreateDefaultPerimeter(image.PixelWidth, image.PixelHeight);
                }
            }).ConfigureAwait(false);
        }
        
        private Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
        {
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                return new Bitmap(memoryStream);
            }
        }
        
        private List<PointF> CreateDefaultPerimeter(int width, int height)
        {
            // Create a rectangle with some margin from the edges
            var margin = Math.Min(width, height) * Constants.DefaultPerimeterMarginRatio;
            _logger.LogDebug("Creating default perimeter with margin {Margin} for image {Width}x{Height}", margin, width, height);
            return new List<PointF>
            {
                new PointF(margin, margin),
                new PointF(width - margin, margin),
                new PointF(width - margin, height - margin),
                new PointF(margin, height - margin)
            };
        }

        public async Task<(List<float> HorizontalLines, List<float> VerticalLines)> DetectWallLinesAsync(BitmapImage image)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert BitmapImage to System.Drawing.Bitmap
                    Bitmap bitmap = BitmapImageToBitmap(image);
                    
                    // Convert to OpenCV Mat
                    using var mat = BitmapConverter.ToMat(bitmap);
                    using var gray = new Mat();
                    using var blurred = new Mat();
                    using var edges = new Mat();
                    
                    // Convert to grayscale
                    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                    
                    // Apply Gaussian blur to reduce noise
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), Constants.GaussianBlurSigma);
                    
                    // Apply Canny edge detection
                    Cv2.Canny(blurred, edges, Constants.CannyLowThreshold, Constants.CannyHighThreshold);
                    
                    // Detect line segments using Hough Line Transform
                    var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 50, 50, 10);
                    
                    var horizontalLines = new HashSet<float>();
                    var verticalLines = new HashSet<float>();
                    
                    // Filter lines for horizontal and vertical only (within 5 degrees of axis-aligned)
                    const double angleThreshold = 5.0 * Math.PI / 180.0; // 5 degrees in radians
                    
                    foreach (var line in lines)
                    {
                        var dx = line.P2.X - line.P1.X;
                        var dy = line.P2.Y - line.P1.Y;
                        var angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx));
                        
                        // Check if line is horizontal (angle close to 0)
                        if (angle < angleThreshold)
                        {
                            // Add the average Y position of this horizontal line
                            var avgY = (line.P1.Y + line.P2.Y) / 2.0f;
                            horizontalLines.Add(avgY);
                        }
                        // Check if line is vertical (angle close to 90 degrees)
                        else if (angle > (Math.PI / 2 - angleThreshold))
                        {
                            // Add the average X position of this vertical line
                            var avgX = (line.P1.X + line.P2.X) / 2.0f;
                            verticalLines.Add(avgX);
                        }
                    }
                    
                    bitmap.Dispose();
                    
                    _logger.LogInformation("Detected {HCount} horizontal lines and {VCount} vertical lines", 
                        horizontalLines.Count, verticalLines.Count);
                    
                    return (horizontalLines.OrderBy(y => y).ToList(), verticalLines.OrderBy(x => x).ToList());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting wall lines");
                    return (new List<float>(), new List<float>());
                }
            }).ConfigureAwait(false);
        }
    }
}
