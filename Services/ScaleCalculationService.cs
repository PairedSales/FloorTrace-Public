using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FloorTrace.Models;
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

            var candidates = new List<(Room Room, double Top, double Left)>();
            foreach (var line in result.Lines)
            {
                var text = line.Text?.Trim() ?? string.Empty;
                if (!IsDimensionString(text)) continue;
                if (!TryParseDimensionsFeet(text, out var wFeet, out var hFeet)) continue;

                // Union all word bounding rects to approximate the line rectangle
                var rect = line.Words.Select(w => w.BoundingRect).Aggregate((a, b) => Union(a, b));

                var bounds = await EstimateRoomBoundsAsync(softwareBitmap, rect);

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
            return await Task.Run(() => IsDimensionString(dimensions));
        }
        
        public async Task<Room> UpdateRoomOverlayAsync(Room room, BitmapImage image)
        {
            return await Task.Run(() =>
            {
                return room;
            });
        }

        private static bool IsDimensionString(string text)
        {
            var patterns = new[]
            {
                "^\\s*(\\d+(?:\\.\\d+)?)\\s*[x×]\\s*(\\d+(?:\\.\\d+)?)\\s*$",
                "^\\s*(\\d+)\\s*'\\s*(\\d+)?\\s*(?:\\\"|″)?\\s*[x×]\\s*(\\d+)\\s*'\\s*(\\d+)?\\s*(?:\\\"|″)?\\s*$",
                "^\\s*(\\d+(?:\\.\\d+)?)\\s*ft\\s*[x×]\\s*(\\d+(?:\\.\\d+)?)\\s*ft\\s*$"
            };
            return patterns.Any(p => Regex.IsMatch(text ?? string.Empty, p, RegexOptions.IgnoreCase));
        }

        private static bool TryParseDimensionsFeet(string text, out double widthFeet, out double heightFeet)
        {
            widthFeet = 0; heightFeet = 0;
            string Normalize(string s) => s.Replace("\u2032", "'").Replace("\u2033", "\"");
            text = Normalize(text ?? string.Empty);
            var parts = text.Split(new[] { 'x', '×' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            double ParseOne(string p)
            {
                p = p.Trim().ToLowerInvariant();
                var fi = Regex.Match(p, "^(?<ft>\\d+(?:\\.\\d+)?)\\s*ft");
                if (fi.Success) return double.Parse(fi.Groups["ft"].Value);
                int idx = p.IndexOf("'");
                if (idx >= 0)
                {
                    var ft = p.Substring(0, idx).Trim();
                    var rest = p.Substring(idx + 1);
                    double feet = double.TryParse(ft, out var f) ? f : 0;
                    var m = Regex.Match(rest, "(?<in>\\d+(?:\\.\\d+)?)\\s*(?:\\\"|″)");
                    double inches = m.Success ? double.Parse(m.Groups["in"].Value) : 0;
                    return feet + inches / 12.0;
                }
                p = p.Replace("feet", string.Empty).Replace("ft", string.Empty).Trim();
                return double.TryParse(p, out var v) ? v : 0;
            }

            widthFeet = ParseOne(parts[0]);
            heightFeet = ParseOne(parts[1]);
            return widthFeet > 0 && heightFeet > 0;
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
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            return await decoder.GetSoftwareBitmapAsync();
        }

        private static async Task<System.Drawing.RectangleF> EstimateRoomBoundsAsync(SoftwareBitmap sb, Windows.Foundation.Rect textRect)
        {
            await Task.CompletedTask;
            const float margin = 120f;
            var left = Math.Max(0, (float)textRect.Left - margin);
            var top = Math.Max(0, (float)textRect.Top - margin);
            var right = Math.Min(sb.PixelWidth, (int)(textRect.Right + margin));
            var bottom = Math.Min(sb.PixelHeight, (int)(textRect.Bottom + margin));
            return new System.Drawing.RectangleF(left, top, right - left, bottom - top);
        }
    }
}
