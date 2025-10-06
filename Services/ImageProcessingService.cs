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

namespace FloorTrace.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
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
            using var skiaBitmap = SKBitmap.FromImage(skiaImage);
            
            // Convert SkiaSharp bitmap to WPF BitmapImage
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            // Create a memory stream from the SkiaSharp bitmap
            using var memoryStream = new MemoryStream();
            var data = skiaImage.Encode(SKEncodedImageFormat.Png, 100);
            memoryStream.Write(data.ToArray());
            memoryStream.Position = 0;
            
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
                    return true;
                }
                catch
                {
                    return false;
                }
            });
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
            return result == true ? openFileDialog.FileName : null;
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
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 1.5);
                    
                    // Apply Canny edge detection
                    Cv2.Canny(blurred, edges, 50, 150);
                    
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
                    var epsilon = 0.01 * Cv2.ArcLength(largestContour, true);
                    var approxPolygon = Cv2.ApproxPolyDP(largestContour, epsilon, true);
                    
                    // Convert to List<PointF>
                    var perimeterPoints = new List<PointF>();
                    foreach (var point in approxPolygon)
                    {
                        perimeterPoints.Add(new PointF(point.X, point.Y));
                    }
                    
                    // Ensure we have at least 3 points
                    if (perimeterPoints.Count < 3)
                    {
                        return CreateDefaultPerimeter(mat.Width, mat.Height);
                    }
                    
                    bitmap.Dispose();
                    return perimeterPoints;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error detecting perimeter: {ex.Message}");
                    // Return a default rectangle on error
                    return CreateDefaultPerimeter(image.PixelWidth, image.PixelHeight);
                }
            });
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
            var margin = Math.Min(width, height) * 0.1f;
            return new List<PointF>
            {
                new PointF(margin, margin),
                new PointF(width - margin, margin),
                new PointF(width - margin, height - margin),
                new PointF(margin, height - margin)
            };
        }
    }
}
