using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FloorTrace.Models;
using FloorTrace.Utilities;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace FloorTrace.Services
{
    /// <summary>
    /// Represents candidate wall lines grouped by direction for room bounds detection.
    /// </summary>
    internal class CandidateLines
    {
        public List<float> Left { get; }
        public List<float> Right { get; }
        public List<float> Top { get; }
        public List<float> Bottom { get; }

        public CandidateLines(List<float> left, List<float> right, List<float> top, List<float> bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

    /// <summary>
    /// Represents the result of OCR processing including the software bitmap and OCR result.
    /// </summary>
    internal class OcrProcessingResult
    {
        public SoftwareBitmap SoftwareBitmap { get; }
        public OcrResult Result { get; }

        public OcrProcessingResult(SoftwareBitmap softwareBitmap, OcrResult result)
        {
            SoftwareBitmap = softwareBitmap;
            Result = result;
        }
    }

    /// <summary>
    /// Represents clustered wall lines for room detection.
    /// </summary>
    internal class ClusteredWallLines
    {
        public List<float> HorizontalLines { get; }
        public List<float> VerticalLines { get; }

        public ClusteredWallLines(List<float> horizontalLines, List<float> verticalLines)
        {
            HorizontalLines = horizontalLines;
            VerticalLines = verticalLines;
        }
    }

    public class ScaleCalculationService : IScaleCalculationService
    {
        private readonly IImageProcessingService _imageProcessingService;
        
        public ScaleCalculationService(IImageProcessingService imageProcessingService)
        {
            _imageProcessingService = imageProcessingService;
        }
        
        public async Task<List<Room>> DetectRoomsAsync(BitmapImage image)
        {
            if (image == null) return new List<Room>();

            // Perform OCR on the image
            var ocrResult = await PerformOcrOnImage(image).ConfigureAwait(false);
            
            // Detect and cluster wall lines once per image
            var wallLines = await DetectAndClusterWallLines(image).ConfigureAwait(false);

            // Parse dimension candidates from OCR results
            var roomCandidates = await ParseDimensionCandidates(ocrResult, wallLines).ConfigureAwait(false);

            return SelectFirstDetectedRoom(roomCandidates);
        }

        /// <summary>
        /// Performs OCR on the image to extract text.
        /// </summary>
        private async Task<OcrProcessingResult> PerformOcrOnImage(BitmapImage image)
        {
            var softwareBitmap = await ConvertToSoftwareBitmapAsync(image);
            var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            var result = await engine.RecognizeAsync(softwareBitmap);
            return new OcrProcessingResult(softwareBitmap, result);
        }

        /// <summary>
        /// Detects and clusters wall lines from the image.
        /// </summary>
        private async Task<ClusteredWallLines> DetectAndClusterWallLines(BitmapImage image)
        {
            var (rawHorizontalLines, rawVerticalLines) = await _imageProcessingService.DetectWallLinesAsync(image).ConfigureAwait(false);
            var horizontalLines = ClusterLines(rawHorizontalLines, 5f);
            var verticalLines = ClusterLines(rawVerticalLines, 5f);
            return new ClusteredWallLines(horizontalLines, verticalLines);
        }

        /// <summary>
        /// Parses OCR results to find dimension candidates and create room objects.
        /// </summary>
        private async Task<List<(Room Room, double Top, double Left)>> ParseDimensionCandidates(
            OcrProcessingResult ocrResult, 
            ClusteredWallLines wallLines)
        {
            var candidates = new List<(Room Room, double Top, double Left)>();
            
            foreach (var line in ocrResult.Result.Lines)
            {
                var text = line.Text?.Trim() ?? string.Empty;
                if (!DimensionParser.IsDimensionString(text)) continue;
                if (!DimensionParser.TryParseDimensionsFeet(text, out var widthFeet, out var heightFeet)) continue;

                // Union all word bounding rects to approximate the line rectangle
                var textRect = line.Words.Select(w => w.BoundingRect).Aggregate((a, b) => Union(a, b));

                // Try to infer bounds from detected wall lines (axis-aligned)
                var inferredBounds = await FindRoomBoundsFromLabelAsync(
                    ocrResult.SoftwareBitmap,
                    textRect,
                    wallLines.HorizontalLines,
                    wallLines.VerticalLines,
                    widthFeet,
                    heightFeet).ConfigureAwait(false);

                var bounds = inferredBounds.Width > 0 && inferredBounds.Height > 0
                    ? inferredBounds
                    : await EstimateRoomBoundsAsync(ocrResult.SoftwareBitmap, textRect).ConfigureAwait(false);

                var room = new Room
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Detected Room",
                    Bounds = bounds,
                    Dimensions = text,
                    WidthFeet = widthFeet,
                    HeightFeet = heightFeet,
                    IsSelected = true
                };
                candidates.Add((room, textRect.Top, textRect.Left));
            }

            return candidates;
        }

        /// <summary>
        /// Selects the first detected room from candidates, ordered by position.
        /// </summary>
        private static List<Room> SelectFirstDetectedRoom(List<(Room Room, double Top, double Left)> candidates)
        {
            return candidates
                .OrderBy(c => c.Top)
                .ThenBy(c => c.Left)
                .Select(c => c.Room)
                .Take(1)
                .ToList();
        }
        
        public async Task<double> CalculateScaleFromRoomAsync(Room room, BitmapImage image)
        {
            return await Task.Run(() =>
            {
                if (room.WidthFeet <= 0 || room.HeightFeet <= 0)
                    throw new ArgumentException("Room dimensions must be positive");
                
                var roomWidthPixels = room.Bounds.Width;
                var roomHeightPixels = room.Bounds.Height;
                
                var scaleFromWidth = roomWidthPixels / room.WidthFeet;
                var scaleFromHeight = roomHeightPixels / room.HeightFeet;
                
                // Return average of width and height scale for better accuracy
                return (scaleFromWidth + scaleFromHeight) / 2.0;
            });
        }
        
        public async Task<bool> ValidateRoomDimensionsAsync(string dimensions)
        {
            return await Task.Run(() => DimensionParser.IsDimensionString(dimensions)).ConfigureAwait(false);
        }
        
        public async Task<Room> UpdateRoomOverlayAsync(Room room, BitmapImage image)
        {
            return await Task.Run(() =>
            {
                return room;
            });
        }


        private static Windows.Foundation.Rect Union(Windows.Foundation.Rect first, Windows.Foundation.Rect second)
        {
            var left = Math.Min(first.Left, second.Left);
            var top = Math.Min(first.Top, second.Top);
            var right = Math.Max(first.Right, second.Right);
            var bottom = Math.Max(first.Bottom, second.Bottom);
            return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
        }

        private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(BitmapImage bitmapImage)
        {
            using var ms = new MemoryStream();
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapImage));
            encoder.Save(ms);
            ms.Position = 0;
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream()).AsTask().ConfigureAwait(false);
            return await decoder.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(false);
        }

        private static Task<System.Drawing.RectangleF> EstimateRoomBoundsAsync(SoftwareBitmap sb, Windows.Foundation.Rect textRect)
        {
            const float margin = Constants.RoomEstimationMargin;
            var left = Math.Max(0, (float)textRect.Left - margin);
            var top = Math.Max(0, (float)textRect.Top - margin);
            var right = Math.Min(sb.PixelWidth, (int)(textRect.Right + margin));
            var bottom = Math.Min(sb.PixelHeight, (int)(textRect.Bottom + margin));
            return Task.FromResult(new System.Drawing.RectangleF(left, top, right - left, bottom - top));
        }

        private static List<float> ClusterLines(IEnumerable<float> lines, float threshold)
        {
            var sorted = lines?.OrderBy(v => v).ToList() ?? new List<float>();
            if (sorted.Count == 0) return new List<float>();

            var clusters = new List<List<float>>();
            var current = new List<float>();
            foreach (var v in sorted)
            {
                if (current.Count == 0)
                {
                    current.Add(v);
                    continue;
                }

                if (Math.Abs(v - current[^1]) <= threshold)
                {
                    current.Add(v);
                }
                else
                {
                    clusters.Add(current);
                    current = new List<float> { v };
                }
            }
            if (current.Count > 0) clusters.Add(current);

            // Use the median value of each cluster as the representative line
            var medians = new List<float>(clusters.Count);
            foreach (var cluster in clusters)
            {
                cluster.Sort();
                var mid = cluster.Count / 2;
                medians.Add(cluster[mid]);
            }
            return medians;
        }

        private static Task<System.Drawing.RectangleF> FindRoomBoundsFromLabelAsync(
            SoftwareBitmap softwareBitmap,
            Windows.Foundation.Rect textRect,
            List<float> horizontalLines,
            List<float> verticalLines,
            double widthFeet,
            double heightFeet)
        {
            // Image dimensions
            var imageWidth = softwareBitmap.PixelWidth;
            var imageHeight = softwareBitmap.PixelHeight;

            // Partition candidate lines around the label rectangle
            var candidateLines = PartitionCandidateLines(textRect, horizontalLines, verticalLines, imageWidth, imageHeight);
            
            if (!HasValidCandidateLines(candidateLines))
            {
                return Task.FromResult(System.Drawing.RectangleF.Empty);
            }

            // Limit candidates to reduce combinatorics
            var limitedCandidates = LimitCandidateLines(candidateLines);

            // Calculate the best bounding box
            var bestRect = CalculateBestBoundingBox(limitedCandidates, textRect, widthFeet, heightFeet);

            return Task.FromResult(bestRect);
        }

        /// <summary>
        /// Partitions wall lines into candidate groups around the text rectangle.
        /// </summary>
        private static CandidateLines PartitionCandidateLines(
            Windows.Foundation.Rect textRect,
            List<float> horizontalLines,
            List<float> verticalLines,
            int imageWidth,
            int imageHeight)
        {
            // Search radii (25% of image dimension)
            double radiusX = imageWidth * 0.25;
            double radiusY = imageHeight * 0.25;

            var leftCandidates = verticalLines
                .Where(x => x < textRect.Left && (textRect.Left - x) <= radiusX)
                .OrderByDescending(x => x)
                .ToList();

            var rightCandidates = verticalLines
                .Where(x => x > textRect.Right && (x - textRect.Right) <= radiusX)
                .OrderBy(x => x)
                .ToList();

            var topCandidates = horizontalLines
                .Where(y => y < textRect.Top && (textRect.Top - y) <= radiusY)
                .OrderByDescending(y => y)
                .ToList();

            var bottomCandidates = horizontalLines
                .Where(y => y > textRect.Bottom && (y - textRect.Bottom) <= radiusY)
                .OrderBy(y => y)
                .ToList();

            return new CandidateLines(leftCandidates, rightCandidates, topCandidates, bottomCandidates);
        }

        /// <summary>
        /// Checks if we have valid candidate lines for all directions.
        /// </summary>
        private static bool HasValidCandidateLines(CandidateLines candidates)
        {
            return candidates.Left.Count > 0 && 
                   candidates.Right.Count > 0 && 
                   candidates.Top.Count > 0 && 
                   candidates.Bottom.Count > 0;
        }

        /// <summary>
        /// Limits the number of candidate lines to reduce computational complexity.
        /// </summary>
        private static CandidateLines LimitCandidateLines(CandidateLines candidates)
        {
            const int maxCandidates = 12;
            
            return new CandidateLines(
                candidates.Left.Take(maxCandidates).ToList(),
                candidates.Right.Take(maxCandidates).ToList(),
                candidates.Top.Take(maxCandidates).ToList(),
                candidates.Bottom.Take(maxCandidates).ToList()
            );
        }

        /// <summary>
        /// Calculates the best bounding box from candidate lines using scoring algorithm.
        /// </summary>
        private static System.Drawing.RectangleF CalculateBestBoundingBox(
            CandidateLines candidates,
            Windows.Foundation.Rect textRect,
            double widthFeet,
            double heightFeet)
        {
            bool hasAspectRatio = widthFeet > 0 && heightFeet > 0;
            double targetAspect = hasAspectRatio ? (widthFeet / heightFeet) : 0.0;
            const double aspectTolerance = 0.25; // 25%

            System.Drawing.RectangleF bestRect = System.Drawing.RectangleF.Empty;
            double bestScore = double.PositiveInfinity;
            bool foundWithinAspect = false;

            // Minimum room pixel size from UI constants
            float minWidth = (float)Constants.MinControlWidth;
            float minHeight = (float)Constants.MinControlHeight;

            foreach (var left in candidates.Left)
            {
                foreach (var right in candidates.Right)
                {
                    float width = (float)(right - left);
                    if (width < minWidth) continue;

                    foreach (var top in candidates.Top)
                    {
                        foreach (var bottom in candidates.Bottom)
                        {
                            float height = (float)(bottom - top);
                            if (height < minHeight) continue;

                            if (!IsLabelInsideBounds(left, right, top, bottom, textRect))
                                continue;

                            var score = ScoreBoundingBox(left, right, top, bottom, textRect, targetAspect, hasAspectRatio);
                            var aspectOk = hasAspectRatio && IsAspectRatioValid(width, height, targetAspect, aspectTolerance);

                            if (hasAspectRatio)
                            {
                                if (aspectOk)
                                {
                                    if (!foundWithinAspect || score < bestScore)
                                    {
                                        bestScore = score;
                                        bestRect = new System.Drawing.RectangleF((float)left, (float)top, width, height);
                                        foundWithinAspect = true;
                                    }
                                }
                                else if (!foundWithinAspect && score < bestScore)
                                {
                                    // Only consider out-of-aspect if no in-aspect found yet
                                    bestScore = score;
                                    bestRect = new System.Drawing.RectangleF((float)left, (float)top, width, height);
                                }
                            }
                            else
                            {
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestRect = new System.Drawing.RectangleF((float)left, (float)top, width, height);
                                }
                            }
                        }
                    }
                }
            }

            return bestRect;
        }

        /// <summary>
        /// Checks if the label rectangle is inside the candidate bounds.
        /// </summary>
        private static bool IsLabelInsideBounds(double left, double right, double top, double bottom, Windows.Foundation.Rect textRect)
        {
            return left <= textRect.Left && 
                   right >= textRect.Right && 
                   top <= textRect.Top && 
                   bottom >= textRect.Bottom;
        }

        /// <summary>
        /// Scores a bounding box based on closeness to the label and aspect ratio.
        /// </summary>
        private static double ScoreBoundingBox(
            double left, double right, double top, double bottom,
            Windows.Foundation.Rect textRect, double targetAspect, bool hasAspectRatio)
        {
            double closenessScore = (textRect.Left - left) + (right - textRect.Right) + (textRect.Top - top) + (bottom - textRect.Bottom);
            double score = closenessScore;

            if (hasAspectRatio)
            {
                float width = (float)(right - left);
                float height = (float)(bottom - top);
                var pixelAspect = width / height;
                var relativeError = Math.Abs(pixelAspect - targetAspect) / targetAspect;
                // Weight ratio error significantly to break ties
                score += relativeError * 1000.0;
            }

            return score;
        }

        /// <summary>
        /// Checks if the aspect ratio is within acceptable tolerance.
        /// </summary>
        private static bool IsAspectRatioValid(float width, float height, double targetAspect, double tolerance)
        {
            var pixelAspect = width / height;
            var relativeError = Math.Abs(pixelAspect - targetAspect) / targetAspect;
            return relativeError <= tolerance;
        }
    }
}

