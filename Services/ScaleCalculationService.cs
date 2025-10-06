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

            var softwareBitmap = await ConvertToSoftwareBitmapAsync(image);
            var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            var result = await engine.RecognizeAsync(softwareBitmap);

            // Detect and cluster wall lines once per image
            var (rawHorizontalLines, rawVerticalLines) = await _imageProcessingService.DetectWallLinesAsync(image).ConfigureAwait(false);
            var horizontalLines = ClusterLines(rawHorizontalLines, 5f);
            var verticalLines = ClusterLines(rawVerticalLines, 5f);

            var candidates = new List<(Room Room, double Top, double Left)>();
            foreach (var line in result.Lines)
            {
                var text = line.Text?.Trim() ?? string.Empty;
                if (!DimensionParser.IsDimensionString(text)) continue;
                if (!DimensionParser.TryParseDimensionsFeet(text, out var wFeet, out var hFeet)) continue;

                // Union all word bounding rects to approximate the line rectangle
                var rect = line.Words.Select(w => w.BoundingRect).Aggregate((a, b) => Union(a, b));

                // Try to infer bounds from detected wall lines (axis-aligned)
                var inferred = await FindRoomBoundsFromLabelAsync(
                    softwareBitmap,
                    rect,
                    horizontalLines,
                    verticalLines,
                    wFeet,
                    hFeet).ConfigureAwait(false);

                var bounds = inferred.Width > 0 && inferred.Height > 0
                    ? inferred
                    : await EstimateRoomBoundsAsync(softwareBitmap, rect).ConfigureAwait(false);

                var room = new Room
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Detected Room",
                    Bounds = bounds,
                    Dimensions = text,
                    WidthFeet = wFeet,
                    HeightFeet = hFeet,
                    IsSelected = true
                };
                candidates.Add((room, rect.Top, rect.Left));
            }

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


        private static Windows.Foundation.Rect Union(Windows.Foundation.Rect a, Windows.Foundation.Rect b)
        {
            var x = Math.Min(a.Left, b.Left);
            var y = Math.Min(a.Top, b.Top);
            var r = Math.Max(a.Right, b.Right);
            var bt = Math.Max(a.Bottom, b.Bottom);
            return new Windows.Foundation.Rect(x, y, r - x, bt - y);
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
            SoftwareBitmap sb,
            Windows.Foundation.Rect textRect,
            List<float> horizontalLines,
            List<float> verticalLines,
            double wFeet,
            double hFeet)
        {
            // Image dimensions
            var imgW = sb.PixelWidth;
            var imgH = sb.PixelHeight;

            // Search radii (25% of image dimension)
            double radiusX = imgW * 0.25;
            double radiusY = imgH * 0.25;

            // Minimum room pixel size from UI constants
            float minW = (float)Constants.MinControlWidth;
            float minH = (float)Constants.MinControlHeight;

            // Partition candidate lines around the label rect
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

            if (leftCandidates.Count == 0 || rightCandidates.Count == 0 || topCandidates.Count == 0 || bottomCandidates.Count == 0)
            {
                return Task.FromResult(System.Drawing.RectangleF.Empty);
            }

            // Limit candidates to the closest few to reduce combinatorics
            int cap = 12;
            leftCandidates = leftCandidates.Take(cap).ToList();
            rightCandidates = rightCandidates.Take(cap).ToList();
            topCandidates = topCandidates.Take(cap).ToList();
            bottomCandidates = bottomCandidates.Take(cap).ToList();

            bool haveAspect = wFeet > 0 && hFeet > 0;
            double targetAspect = haveAspect ? (wFeet / hFeet) : 0.0;
            double aspectTolerance = 0.25; // 25%

            System.Drawing.RectangleF bestRect = System.Drawing.RectangleF.Empty;
            double bestScore = double.PositiveInfinity;
            bool foundWithinAspect = false;

            foreach (var L in leftCandidates)
            {
                foreach (var R in rightCandidates)
                {
                    float width = (float)(R - L);
                    if (width < minW) continue;

                    foreach (var T in topCandidates)
                    {
                        foreach (var B in bottomCandidates)
                        {
                            float height = (float)(B - T);
                            if (height < minH) continue;

                            // Ensure label rect is inside
                            if (!(L <= textRect.Left && R >= textRect.Right && T <= textRect.Top && B >= textRect.Bottom))
                                continue;

                            double scoreCloseness = (textRect.Left - L) + (R - textRect.Right) + (textRect.Top - T) + (B - textRect.Bottom);
                            double score = scoreCloseness;

                            bool aspectOk = true;
                            if (haveAspect)
                            {
                                var pxAspect = width / height;
                                var relErr = Math.Abs(pxAspect - targetAspect) / targetAspect;
                                aspectOk = relErr <= aspectTolerance;
                                // Always include a ratio component to score to break ties
                                score += relErr * 1000.0; // weight ratio error significantly
                            }

                            if (haveAspect)
                            {
                                if (aspectOk)
                                {
                                    if (!foundWithinAspect || score < bestScore)
                                    {
                                        bestScore = score;
                                        bestRect = new System.Drawing.RectangleF((float)L, (float)T, width, height);
                                        foundWithinAspect = true;
                                    }
                                }
                                else if (!foundWithinAspect && score < bestScore)
                                {
                                    // Only consider out-of-aspect if no in-aspect found yet
                                    bestScore = score;
                                    bestRect = new System.Drawing.RectangleF((float)L, (float)T, width, height);
                                }
                            }
                            else
                            {
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestRect = new System.Drawing.RectangleF((float)L, (float)T, width, height);
                                }
                            }
                        }
                    }
                }
            }

            return Task.FromResult(bestRect);
        }
    }
}
