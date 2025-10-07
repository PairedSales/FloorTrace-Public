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
                    _logger.LogInformation("Image saved successfully to {FilePath}", PathUtils.RedactUserPath(filePath));
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save image to {FilePath}", PathUtils.RedactUserPath(filePath));
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
        
        public async Task<List<PointF>> DetectPerimeterAsync(BitmapImage image, bool useInnerEdge = true)
        {
            // Try rectilinear detection first (new approach optimized for rectangular floor plans)
            var result = await TryDetectRectilinearPerimeterAsync(image, useInnerEdge);
            
            if (result != null && result.Count >= Constants.MinPerimeterPoints)
            {
                _logger.LogInformation("Successfully detected rectilinear perimeter with {Count} vertices", result.Count);
                return result;
            }
            
            // Fall back to original contour-based method
            _logger.LogWarning("Rectilinear detection failed, falling back to contour method");
            return await DetectPerimeterUsingContoursAsync(image, useInnerEdge);
        }

        /// <summary>
        /// Detects perimeter using the original contour-based approach.
        /// This method serves as a fallback when the rectilinear detection fails.
        /// </summary>
        private async Task<List<PointF>> DetectPerimeterUsingContoursAsync(BitmapImage image, bool useInnerEdge = true)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert BitmapImage to System.Drawing.Bitmap for OpenCV processing
                    Bitmap bitmap = BitmapImageToBitmap(image);
                    
                    // Convert to OpenCV Mat format for computer vision operations
                    using var mat = BitmapConverter.ToMat(bitmap);
                    using var gray = new Mat();
                    using var blurred = new Mat();
                    using var edges = new Mat();
                    
                    // Convert to grayscale - simplifies edge detection and reduces processing overhead
                    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                    
                    // Apply Gaussian blur to reduce noise and smooth the image
                    // This helps eliminate small artifacts that could interfere with edge detection
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), Constants.GaussianBlurSigma);
                    
                    // Apply Canny edge detection to find strong edges in the image
                    // This identifies the boundaries of walls and other structural elements
                    Cv2.Canny(blurred, edges, Constants.CannyLowThreshold, Constants.CannyHighThreshold);
                    
                    // Apply morphological operations to close gaps in detected edges
                    // This helps connect broken wall lines and creates more complete contours
                    using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    using var closed = new Mat();
                    Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel);
                    
                    // Find contours - these represent the boundaries of objects in the image
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(closed, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    
                    if (contours.Length == 0)
                    {
                        // No contours found, return a default rectangle covering most of the image
                        return CreateDefaultPerimeter(mat.Width, mat.Height);
                    }
                    
                    // Find the largest contour - this should be the floor plan perimeter
                    // We assume the floor plan occupies the largest area in the image
                    var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    
                    // If using inner edge, erode the contour to account for wall thickness
                    // This shifts the perimeter inward to measure usable floor space
                    OpenCvSharp.Point[] processedContour = largestContour;
                    if (useInnerEdge)
                    {
                        processedContour = ErodeContourForInnerEdge(largestContour, gray, mat.Size());
                    }
                    
                    // Approximate the contour to a polygon with fewer vertices
                    // This simplifies the shape while preserving the essential perimeter
                    var epsilon = Constants.ContourApproximationEpsilon * Cv2.ArcLength(processedContour, true);
                    var approxPolygon = Cv2.ApproxPolyDP(processedContour, epsilon, true);
                    
                    // Convert OpenCV points to .NET PointF objects for use in the application
                    var perimeterPoints = new List<PointF>();
                    foreach (var point in approxPolygon)
                    {
                        perimeterPoints.Add(new PointF(point.X, point.Y));
                    }
                    
                    // Ensure we have at least 3 points to form a valid polygon
                    if (perimeterPoints.Count < Constants.MinPerimeterPoints)
                    {
                        _logger.LogWarning("Detected perimeter has fewer than {MinPoints} points, using default", Constants.MinPerimeterPoints);
                        return CreateDefaultPerimeter(mat.Width, mat.Height);
                    }
                    
                    _logger.LogInformation("Successfully detected perimeter with {PointCount} points using contour method (inner edge: {UseInner})", 
                        perimeterPoints.Count, useInnerEdge);
                    bitmap.Dispose();
                    return perimeterPoints;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting perimeter, returning default rectangle");
                    // Return a default rectangle on error to ensure the application continues to function
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
        
        /// <summary>
        /// Erodes a contour inward to find the inner edge of walls, accounting for wall thickness.
        /// This algorithm shifts each vertex inward along its normal vector until it reaches the inner wall surface.
        /// </summary>
        /// <param name="contour">The outer wall contour to erode.</param>
        /// <param name="grayImage">The grayscale image for wall thickness measurement.</param>
        /// <param name="imageSize">The size of the image for bounds checking.</param>
        /// <returns>The eroded contour representing the inner wall edge.</returns>
        private OpenCvSharp.Point[] ErodeContourForInnerEdge(OpenCvSharp.Point[] contour, Mat grayImage, OpenCvSharp.Size imageSize)
        {
            try
            {
                // Approximate the contour to get clean polygon vertices
                var approxPolygon = ApproximateContourToPolygon(contour);
                if (approxPolygon == null)
                    return contour;
                
                // Calculate polygon centroid for inward direction determination
                var centroid = ComputePolygonCentroid(approxPolygon);
                
                // Process each vertex to shift it inward
                var (innerVertices, shiftDistances) = ShiftVerticesInward(approxPolygon, centroid, grayImage, imageSize);
                
                // Validate and return the result
                return ValidateInnerContour(contour, approxPolygon, innerVertices, shiftDistances);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing inner edge contour, using original");
                return contour;
            }
        }

        /// <summary>
        /// Approximates a contour to a simplified polygon with fewer vertices.
        /// </summary>
        private OpenCvSharp.Point[]? ApproximateContourToPolygon(OpenCvSharp.Point[] contour)
        {
            var epsilon = Constants.ContourApproximationEpsilon * Cv2.ArcLength(contour, true);
            var approxPolygon = Cv2.ApproxPolyDP(contour, epsilon, true);
            
            _logger.LogDebug("Approximated outer contour to {Count} vertices for inner edge detection", approxPolygon.Length);
            
            if (approxPolygon.Length < Constants.MinPerimeterPoints)
            {
                _logger.LogWarning("Too few vertices after approximation, using original contour");
                return null;
            }
            
            return approxPolygon;
        }

        /// <summary>
        /// Computes the centroid of a polygon.
        /// </summary>
        private static OpenCvSharp.Point2f ComputePolygonCentroid(OpenCvSharp.Point[] polygon)
        {
            double centroidX = 0;
            double centroidY = 0;
            
            foreach (var vertex in polygon)
            {
                centroidX += vertex.X;
                centroidY += vertex.Y;
            }
            
            centroidX /= polygon.Length;
            centroidY /= polygon.Length;
            
            return new OpenCvSharp.Point2f((float)centroidX, (float)centroidY);
        }

        /// <summary>
        /// Shifts all vertices inward along their normal vectors.
        /// </summary>
        private (OpenCvSharp.Point[], List<int>) ShiftVerticesInward(
            OpenCvSharp.Point[] approxPolygon, 
            OpenCvSharp.Point2f centroid, 
            Mat grayImage, 
            OpenCvSharp.Size imageSize)
        {
            var innerVertices = new OpenCvSharp.Point[approxPolygon.Length];
            var shiftDistances = new List<int>();
            
            for (int vertexIndex = 0; vertexIndex < approxPolygon.Length; vertexIndex++)
            {
                var currentVertex = approxPolygon[vertexIndex];
                
                // Calculate inward normal direction
                var inwardNormal = CalculateInwardNormal(approxPolygon, vertexIndex, centroid);
                if (inwardNormal == null)
                {
                    // Degenerate case, keep original vertex
                    innerVertices[vertexIndex] = currentVertex;
                    continue;
                }
                
                // Find wall thickness and shift vertex inward
                int shiftDistance = FindWallThicknessAlongNormal(currentVertex, inwardNormal.Value, grayImage);
                
                if (shiftDistance > 0)
                {
                    shiftDistances.Add(shiftDistance);
                    innerVertices[vertexIndex] = ShiftVertexInward(currentVertex, inwardNormal.Value, shiftDistance, imageSize);
                }
                else
                {
                    // Couldn't find clear wall edge, keep original vertex
                    innerVertices[vertexIndex] = currentVertex;
                    _logger.LogDebug("Could not detect wall thickness at vertex {Index}, keeping original position", vertexIndex);
                }
            }
            
            return (innerVertices, shiftDistances);
        }

        /// <summary>
        /// Calculates the inward normal vector for a vertex.
        /// </summary>
        private static OpenCvSharp.Point2f? CalculateInwardNormal(OpenCvSharp.Point[] polygon, int vertexIndex, OpenCvSharp.Point2f centroid)
        {
            int prevIdx = (vertexIndex - 1 + polygon.Length) % polygon.Length;
            int nextIdx = (vertexIndex + 1) % polygon.Length;
            
            var prevVertex = polygon[prevIdx];
            var nextVertex = polygon[nextIdx];
            var currentVertex = polygon[vertexIndex];
            
            // Calculate tangent vector (direction along the polygon edge)
            double tangentX = nextVertex.X - prevVertex.X;
            double tangentY = nextVertex.Y - prevVertex.Y;
            
            // Normal is perpendicular to tangent (rotate 90 degrees)
            double normalX = -tangentY;
            double normalY = tangentX;
            
            // Normalize the normal vector to unit length
            double normalLength = Math.Sqrt(normalX * normalX + normalY * normalY);
            if (normalLength < 0.001)
            {
                return null; // Degenerate case
            }
            
            normalX /= normalLength;
            normalY /= normalLength;
            
            // Ensure the normal points inward (toward the centroid)
            double toCentroidX = centroid.X - currentVertex.X;
            double toCentroidY = centroid.Y - currentVertex.Y;
            
            double dotProduct = normalX * toCentroidX + normalY * toCentroidY;
            if (dotProduct < 0)
            {
                // Normal points outward, flip it to point inward
                normalX = -normalX;
                normalY = -normalY;
            }
            
            return new OpenCvSharp.Point2f((float)normalX, (float)normalY);
        }

        /// <summary>
        /// Shifts a vertex inward by the specified distance along the normal vector.
        /// </summary>
        private static OpenCvSharp.Point ShiftVertexInward(
            OpenCvSharp.Point currentVertex, 
            OpenCvSharp.Point2f inwardNormal, 
            int shiftDistance, 
            OpenCvSharp.Size imageSize)
        {
            int newX = (int)(currentVertex.X + inwardNormal.X * shiftDistance);
            int newY = (int)(currentVertex.Y + inwardNormal.Y * shiftDistance);
            
            // Clamp to image bounds
            newX = Math.Max(0, Math.Min(imageSize.Width - 1, newX));
            newY = Math.Max(0, Math.Min(imageSize.Height - 1, newY));
            
            return new OpenCvSharp.Point(newX, newY);
        }

        /// <summary>
        /// Validates the inner contour and logs statistics.
        /// </summary>
        private OpenCvSharp.Point[] ValidateInnerContour(
            OpenCvSharp.Point[] originalContour,
            OpenCvSharp.Point[] approxPolygon,
            OpenCvSharp.Point[] innerVertices,
            List<int> shiftDistances)
        {
            double originalArea = Cv2.ContourArea(approxPolygon);
            double innerArea = Cv2.ContourArea(innerVertices);
            
            // Log statistics for debugging and monitoring
            if (shiftDistances.Count > 0)
            {
                double avgShift = shiftDistances.Average();
                double maxShift = shiftDistances.Max();
                double minShift = shiftDistances.Min();
                _logger.LogDebug("Inner edge detection: avg shift {Avg:F1}px, min {Min}px, max {Max}px, area reduction {AreaReduction:F1}%",
                    avgShift, minShift, maxShift, (1 - innerArea / originalArea) * 100);
            }
            
            // Validate the inner contour has a reasonable area (at least 20% of original)
            const double minAreaRatio = 0.2;
            if (innerArea > originalArea * minAreaRatio && innerArea < originalArea)
            {
                _logger.LogDebug("Successfully computed inner edge contour from {OrigArea:F0} to {InnerArea:F0} pixels", 
                    originalArea, innerArea);
                return innerVertices;
            }
            else
            {
                _logger.LogWarning("Inner edge contour has invalid area ({InnerArea:F0} vs {OrigArea:F0}), using original contour", 
                    innerArea, originalArea);
                return originalContour;
            }
        }
        
        private int DetectWallThickness(OpenCvSharp.Point[] contour, Mat grayImage)
        {
            try
            {
                var thicknesses = new List<int>();
                
                // Sample points along the contour to measure wall thickness
                int sampleCount = Math.Min(20, contour.Length);
                int step = Math.Max(1, contour.Length / sampleCount);
                
                for (int i = 0; i < contour.Length; i += step)
                {
                    var point = contour[i];
                    
                    // Calculate inward normal direction
                    int prevIdx = (i - 1 + contour.Length) % contour.Length;
                    int nextIdx = (i + 1) % contour.Length;
                    
                    var tangentX = contour[nextIdx].X - contour[prevIdx].X;
                    var tangentY = contour[nextIdx].Y - contour[prevIdx].Y;
                    
                    // Normal is perpendicular to tangent (rotate 90 degrees), pointing inward
                    double normalX = -tangentY;
                    double normalY = tangentX;
                    
                    // Normalize the normal vector
                    double normalLength = Math.Sqrt(normalX * normalX + normalY * normalY);
                    if (normalLength < 0.001) continue;
                    
                    normalX /= normalLength;
                    normalY /= normalLength;
                    
                    // Sample along the normal to find wall thickness
                    int thickness = FindWallThicknessAlongNormal(point, new OpenCvSharp.Point2f((float)normalX, (float)normalY), grayImage);
                    if (thickness > 0)
                    {
                        thicknesses.Add(thickness);
                    }
                }
                
                // Return median thickness, or default if no valid measurements
                if (thicknesses.Count > 0)
                {
                    thicknesses.Sort();
                    int median = thicknesses[thicknesses.Count / 2];
                    
                    // Clamp to reasonable range
                    median = Math.Max(Constants.MinWallThicknessPixels, 
                                     Math.Min(Constants.MaxWallThicknessPixels, median));
                    
                    return median;
                }
                
                return Constants.DefaultWallThicknessPixels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting wall thickness");
                return Constants.DefaultWallThicknessPixels;
            }
        }
        
        private int FindWallThicknessAlongNormal(OpenCvSharp.Point point, OpenCvSharp.Point2f inwardNormal, Mat grayImage)
        {
            try
            {
                // Sample pixels along the normal direction to find where darkness ends
                const int maxDistance = Constants.MaxWallThicknessPixels;
                const int darkThreshold = 127; // Consider pixels darker than this as part of the wall
                const int requiredConsecutive = 2; // Need 2 consecutive bright pixels to confirm wall edge
                
                int consecutiveBright = 0;
                
                for (int distance = 1; distance <= maxDistance; distance++)
                {
                    int x = (int)(point.X + inwardNormal.X * distance);
                    int y = (int)(point.Y + inwardNormal.Y * distance);
                    
                    // Check bounds
                    if (!IsPointWithinImageBounds(x, y, grayImage.Width, grayImage.Height))
                    {
                        break;
                    }
                    
                    // Get pixel intensity
                    byte intensity = grayImage.At<byte>(y, x);
                    
                    // If pixel is bright (not part of wall), increment counter
                    if (intensity > darkThreshold)
                    {
                        consecutiveBright++;
                        if (consecutiveBright >= requiredConsecutive)
                        {
                            return distance - requiredConsecutive + 1;
                        }
                    }
                    else
                    {
                        consecutiveBright = 0;
                    }
                }
                
                return 0; // Couldn't find clear wall edge
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Checks if a point is within image bounds.
        /// </summary>
        private static bool IsPointWithinImageBounds(int x, int y, int imageWidth, int imageHeight)
        {
            return x >= 0 && x < imageWidth && y >= 0 && y < imageHeight;
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
                    
                    // Process image to detect edges
                    var edges = ProcessImageForEdgeDetection(bitmap);
                    
                    // Detect line segments using Hough Line Transform
                    var detectedLines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 50, 50, 10);
                    
                    // Classify lines as horizontal or vertical
                    var (horizontalLines, verticalLines) = ClassifyLinesByOrientation(detectedLines);
                    
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

        /// <summary>
        /// Processes an image for edge detection using OpenCV operations.
        /// </summary>
        private static Mat ProcessImageForEdgeDetection(Bitmap bitmap)
        {
            // Convert to OpenCV Mat
            using var mat = BitmapConverter.ToMat(bitmap);
            using var gray = new Mat();
            using var blurred = new Mat();
            var edges = new Mat();
            
            // Convert to grayscale
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            
            // Apply Gaussian blur to reduce noise
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), Constants.GaussianBlurSigma);
            
            // Apply Canny edge detection
            Cv2.Canny(blurred, edges, Constants.CannyLowThreshold, Constants.CannyHighThreshold);
            
            return edges;
        }

        /// <summary>
        /// Classifies detected lines as horizontal or vertical based on their orientation.
        /// </summary>
        private static (HashSet<float> HorizontalLines, HashSet<float> VerticalLines) ClassifyLinesByOrientation(OpenCvSharp.LineSegmentPoint[] detectedLines)
        {
            var horizontalLines = new HashSet<float>();
            var verticalLines = new HashSet<float>();
            
            // Filter lines for horizontal and vertical only (within 5 degrees of axis-aligned)
            const double angleThreshold = 5.0 * Math.PI / 180.0; // 5 degrees in radians
            
            foreach (var line in detectedLines)
            {
                var orientation = ClassifyLineOrientation(line, angleThreshold);
                
                switch (orientation)
                {
                    case LineOrientation.Horizontal:
                        var avgY = (line.P1.Y + line.P2.Y) / 2.0f;
                        horizontalLines.Add(avgY);
                        break;
                    case LineOrientation.Vertical:
                        var avgX = (line.P1.X + line.P2.X) / 2.0f;
                        verticalLines.Add(avgX);
                        break;
                    case LineOrientation.Diagonal:
                        // Skip diagonal lines
                        break;
                }
            }
            
            return (horizontalLines, verticalLines);
        }

        /// <summary>
        /// Classifies a line segment as horizontal, vertical, or diagonal.
        /// </summary>
        private static LineOrientation ClassifyLineOrientation(OpenCvSharp.LineSegmentPoint line, double angleThreshold)
        {
            var dx = line.P2.X - line.P1.X;
            var dy = line.P2.Y - line.P1.Y;
            var angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx));
            
            // Check if line is horizontal (angle close to 0)
            if (angle < angleThreshold)
            {
                return LineOrientation.Horizontal;
            }
            // Check if line is vertical (angle close to 90 degrees)
            else if (angle > (Math.PI / 2 - angleThreshold))
            {
                return LineOrientation.Vertical;
            }
            else
            {
                return LineOrientation.Diagonal;
            }
        }

        #region Rectilinear Perimeter Detection

        /// <summary>
        /// Attempts to detect a rectilinear (90-degree angles only) perimeter using line intersection method.
        /// This is the primary detection method optimized for rectangular, grid-aligned floor plans.
        /// </summary>
        private async Task<List<PointF>?> TryDetectRectilinearPerimeterAsync(BitmapImage image, bool useInnerEdge)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert BitmapImage to System.Drawing.Bitmap for OpenCV processing
                    Bitmap bitmap = BitmapImageToBitmap(image);
                    
                    // Process image to detect edges
                    using var mat = BitmapConverter.ToMat(bitmap);
                    using var edges = ProcessImageForEdgeDetection(bitmap);
                    
                    // Detect horizontal and vertical lines
                    var (horizontalLines, verticalLines) = DetectRectilinearWalls(edges);
                    
                    if (horizontalLines.Count < 2 || verticalLines.Count < 2)
                    {
                        _logger.LogWarning("Insufficient lines detected: {HCount} horizontal, {VCount} vertical", 
                            horizontalLines.Count, verticalLines.Count);
                        return null;
                    }
                    
                    _logger.LogInformation("Detected {HCount} horizontal lines and {VCount} vertical lines", 
                        horizontalLines.Count, verticalLines.Count);
                    
                    // Detect wall thickness for inner edge adjustment
                    int wallThickness = DetectWallThicknessFromLines(horizontalLines, verticalLines);
                    _logger.LogInformation("Detected wall thickness: {Thickness} pixels", wallThickness);
                    
                    // Offset lines inward if using inner edge mode
                    if (useInnerEdge)
                    {
                        (horizontalLines, verticalLines) = OffsetLinesInward(horizontalLines, verticalLines, wallThickness, mat.Size());
                    }
                    
                    // Build polygon from line intersections
                    var polygon = BuildRectilinearPolygonFromLines(horizontalLines, verticalLines, mat.Size());
                    
                    bitmap.Dispose();
                    
                    if (polygon == null || polygon.Count < Constants.MinPerimeterPoints)
                    {
                        _logger.LogWarning("Failed to build valid polygon from lines");
                        return null;
                    }
                    
                    _logger.LogInformation("Successfully built rectilinear polygon with {Count} vertices", polygon.Count);
                    return polygon;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in rectilinear perimeter detection");
                    return null;
                }
            });
        }

        /// <summary>
        /// Detects horizontal and vertical wall lines using Hough Line Transform.
        /// Returns full line segments with clustering applied.
        /// </summary>
        private (List<Line> HorizontalLines, List<Line> VerticalLines) DetectRectilinearWalls(Mat edges)
        {
            // Detect line segments using Hough Line Transform
            var detectedLines = Cv2.HoughLinesP(
                edges, 
                1, 
                Math.PI / 180, 
                Constants.HoughLineThreshold,
                Constants.HoughMinLineLength, 
                Constants.HoughMaxLineGap);
            
            var horizontalLines = new List<Line>();
            var verticalLines = new List<Line>();
            
            // Filter and convert lines to horizontal/vertical only
            const double angleThreshold = 5.0 * Math.PI / 180.0; // 5 degrees in radians
            
            foreach (var line in detectedLines)
            {
                var orientation = ClassifyLineOrientation(line, angleThreshold);
                
                if (orientation == LineOrientation.Horizontal)
                {
                    float avgY = (line.P1.Y + line.P2.Y) / 2.0f;
                    float minX = Math.Min(line.P1.X, line.P2.X);
                    float maxX = Math.Max(line.P1.X, line.P2.X);
                    horizontalLines.Add(new Line(new PointF(minX, avgY), new PointF(maxX, avgY), true));
                }
                else if (orientation == LineOrientation.Vertical)
                {
                    float avgX = (line.P1.X + line.P2.X) / 2.0f;
                    float minY = Math.Min(line.P1.Y, line.P2.Y);
                    float maxY = Math.Max(line.P1.Y, line.P2.Y);
                    verticalLines.Add(new Line(new PointF(avgX, minY), new PointF(avgX, maxY), false));
                }
            }
            
            // Merge and cluster parallel lines
            horizontalLines = MergeParallelLines(horizontalLines, Constants.LineClusteringDistance);
            verticalLines = MergeParallelLines(verticalLines, Constants.LineClusteringDistance);
            
            return (horizontalLines, verticalLines);
        }

        /// <summary>
        /// Merges parallel lines that are close together (within tolerance).
        /// </summary>
        private List<Line> MergeParallelLines(List<Line> lines, float tolerance)
        {
            if (lines.Count == 0)
                return lines;
            
            // Sort by position (Y for horizontal, X for vertical)
            var sortedLines = lines.OrderBy(l => l.Position).ToList();
            var mergedLines = new List<Line>();
            
            var currentGroup = new List<Line> { sortedLines[0] };
            
            for (int i = 1; i < sortedLines.Count; i++)
            {
                var line = sortedLines[i];
                var lastLine = currentGroup[currentGroup.Count - 1];
                
                // Check if this line is close enough to be in the same group
                if (Math.Abs(line.Position - lastLine.Position) <= tolerance)
                {
                    currentGroup.Add(line);
                }
                else
                {
                    // Merge the current group into one line
                    mergedLines.Add(MergeLineGroup(currentGroup));
                    currentGroup = new List<Line> { line };
                }
            }
            
            // Don't forget the last group
            if (currentGroup.Count > 0)
            {
                mergedLines.Add(MergeLineGroup(currentGroup));
            }
            
            return mergedLines;
        }

        /// <summary>
        /// Merges a group of parallel lines into a single representative line.
        /// </summary>
        private Line MergeLineGroup(List<Line> group)
        {
            if (group.Count == 1)
                return group[0];
            
            bool isHorizontal = group[0].IsHorizontal;
            
            if (isHorizontal)
            {
                // Average Y position, extend from min X to max X
                float avgY = group.Average(l => l.Position);
                float minX = group.Min(l => Math.Min(l.Start.X, l.End.X));
                float maxX = group.Max(l => Math.Max(l.Start.X, l.End.X));
                return new Line(new PointF(minX, avgY), new PointF(maxX, avgY), true);
            }
            else
            {
                // Average X position, extend from min Y to max Y
                float avgX = group.Average(l => l.Position);
                float minY = group.Min(l => Math.Min(l.Start.Y, l.End.Y));
                float maxY = group.Max(l => Math.Max(l.Start.Y, l.End.Y));
                return new Line(new PointF(avgX, minY), new PointF(avgX, maxY), false);
            }
        }

        /// <summary>
        /// Detects wall thickness by finding pairs of parallel lines that are close together.
        /// </summary>
        private int DetectWallThicknessFromLines(List<Line> horizontalLines, List<Line> verticalLines)
        {
            var distances = new List<int>();
            
            // Check horizontal line pairs
            for (int i = 0; i < horizontalLines.Count - 1; i++)
            {
                float distance = Math.Abs(horizontalLines[i + 1].Position - horizontalLines[i].Position);
                if (distance >= Constants.MinWallThicknessAuto && distance <= Constants.MaxWallThicknessAuto)
                {
                    distances.Add((int)distance);
                }
            }
            
            // Check vertical line pairs
            for (int i = 0; i < verticalLines.Count - 1; i++)
            {
                float distance = Math.Abs(verticalLines[i + 1].Position - verticalLines[i].Position);
                if (distance >= Constants.MinWallThicknessAuto && distance <= Constants.MaxWallThicknessAuto)
                {
                    distances.Add((int)distance);
                }
            }
            
            if (distances.Count == 0)
            {
                _logger.LogWarning("Could not auto-detect wall thickness, using default");
                return Constants.DefaultWallThicknessPixels;
            }
            
            // Return median distance
            distances.Sort();
            int median = distances[distances.Count / 2];
            
            _logger.LogDebug("Wall thickness candidates: [{Distances}], median: {Median}", 
                string.Join(", ", distances.Take(10)), median);
            
            return median;
        }

        /// <summary>
        /// Offsets lines inward by half the wall thickness to detect inner edges.
        /// </summary>
        private (List<Line> HorizontalLines, List<Line> VerticalLines) OffsetLinesInward(
            List<Line> horizontalLines, 
            List<Line> verticalLines, 
            int wallThickness,
            OpenCvSharp.Size imageSize)
        {
            float halfThickness = wallThickness / 2.0f;
            float centerY = imageSize.Height / 2.0f;
            float centerX = imageSize.Width / 2.0f;
            
            var offsetHorizontal = new List<Line>();
            var offsetVertical = new List<Line>();
            
            // Offset horizontal lines (shift Y coordinate toward center)
            foreach (var line in horizontalLines)
            {
                float newY = line.Position < centerY 
                    ? line.Position + halfThickness  // Top edge, move down
                    : line.Position - halfThickness; // Bottom edge, move up
                
                offsetHorizontal.Add(new Line(
                    new PointF(line.Start.X, newY), 
                    new PointF(line.End.X, newY), 
                    true));
            }
            
            // Offset vertical lines (shift X coordinate toward center)
            foreach (var line in verticalLines)
            {
                float newX = line.Position < centerX 
                    ? line.Position + halfThickness  // Left edge, move right
                    : line.Position - halfThickness; // Right edge, move left
                
                offsetVertical.Add(new Line(
                    new PointF(newX, line.Start.Y), 
                    new PointF(newX, line.End.Y), 
                    false));
            }
            
            return (offsetHorizontal, offsetVertical);
        }

        /// <summary>
        /// Builds a rectilinear polygon from the intersections of horizontal and vertical lines.
        /// </summary>
        private List<PointF>? BuildRectilinearPolygonFromLines(
            List<Line> horizontalLines, 
            List<Line> verticalLines,
            OpenCvSharp.Size imageSize)
        {
            // Find all intersections
            var intersections = new List<PointF>();
            
            foreach (var hLine in horizontalLines)
            {
                foreach (var vLine in verticalLines)
                {
                    var intersection = FindLineIntersection(hLine, vLine);
                    if (intersection.HasValue)
                    {
                        var point = intersection.Value;
                        // Check if intersection is within image bounds
                        if (point.X >= 0 && point.X < imageSize.Width && 
                            point.Y >= 0 && point.Y < imageSize.Height)
                        {
                            intersections.Add(point);
                        }
                    }
                }
            }
            
            if (intersections.Count < Constants.MinPerimeterPoints)
            {
                _logger.LogWarning("Not enough valid intersections: {Count}", intersections.Count);
                return null;
            }
            
            // Remove duplicate points
            intersections = RemoveDuplicatePoints(intersections, Constants.ParallelLineMergeTolerance);
            
            // Order vertices to form a closed polygon
            var polygon = OrderVerticesAsPolygon(intersections);
            
            if (polygon == null || polygon.Count < Constants.MinPerimeterPoints)
            {
                _logger.LogWarning("Failed to order vertices into valid polygon");
                return null;
            }
            
            return polygon;
        }

        /// <summary>
        /// Finds the intersection point between a horizontal and vertical line.
        /// </summary>
        private PointF? FindLineIntersection(Line line1, Line line2)
        {
            if (line1.IsHorizontal == line2.IsHorizontal)
                return null; // Parallel lines don't intersect
            
            Line hLine = line1.IsHorizontal ? line1 : line2;
            Line vLine = line1.IsHorizontal ? line2 : line1;
            
            float x = vLine.Position;
            float y = hLine.Position;
            
            // Check if intersection is within line segments
            bool withinH = x >= Math.Min(hLine.Start.X, hLine.End.X) && x <= Math.Max(hLine.Start.X, hLine.End.X);
            bool withinV = y >= Math.Min(vLine.Start.Y, vLine.End.Y) && y <= Math.Max(vLine.Start.Y, vLine.End.Y);
            
            if (withinH && withinV)
            {
                return new PointF(x, y);
            }
            
            return null;
        }

        /// <summary>
        /// Removes duplicate points within tolerance distance.
        /// </summary>
        private List<PointF> RemoveDuplicatePoints(List<PointF> points, float tolerance)
        {
            var unique = new List<PointF>();
            
            foreach (var point in points)
            {
                bool isDuplicate = false;
                foreach (var existing in unique)
                {
                    float distance = (float)Math.Sqrt(
                        Math.Pow(point.X - existing.X, 2) + 
                        Math.Pow(point.Y - existing.Y, 2));
                    
                    if (distance < tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                
                if (!isDuplicate)
                {
                    unique.Add(point);
                }
            }
            
            return unique;
        }

        /// <summary>
        /// Orders vertices to form a closed polygon using convex hull approach.
        /// For rectilinear polygons, we use the boundary of all intersection points.
        /// </summary>
        private List<PointF>? OrderVerticesAsPolygon(List<PointF> points)
        {
            if (points.Count < 3)
                return null;
            
            try
            {
                // Convert to OpenCV format
                var cvPoints = points.Select(p => new OpenCvSharp.Point2f(p.X, p.Y)).ToArray();
                
                // Use convex hull to order points
                var hull = Cv2.ConvexHullIndices(cvPoints);
                
                var orderedPoints = new List<PointF>();
                foreach (var index in hull)
                {
                    orderedPoints.Add(points[index]);
                }
                
                return orderedPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ordering vertices");
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Enumeration of line orientations for classification.
        /// </summary>
        private enum LineOrientation
        {
            Horizontal,
            Vertical,
            Diagonal
        }

        /// <summary>
        /// Represents a line segment for rectilinear detection.
        /// </summary>
        private class Line
        {
            public PointF Start { get; set; }
            public PointF End { get; set; }
            public bool IsHorizontal { get; set; }
            public float Position { get; set; } // Y for horizontal, X for vertical
            
            public Line(PointF start, PointF end, bool isHorizontal)
            {
                Start = start;
                End = end;
                IsHorizontal = isHorizontal;
                Position = isHorizontal ? start.Y : start.X;
            }
        }
    }
}
