using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;
using Microsoft.Win32;

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
                if (!Clipboard.ContainsImage())
                    throw new InvalidOperationException("No image in clipboard");
                
                var bitmapSource = Clipboard.GetImage();
                if (bitmapSource == null)
                    throw new InvalidOperationException("Failed to get image from clipboard");
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
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
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Floor Plan Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp|" +
                        "JPEG Images|*.jpg;*.jpeg|" +
                        "PNG Images|*.png|" +
                        "Bitmap Images|*.bmp|" +
                        "TIFF Images|*.tif;*.tiff|" +
                        "WebP Images|*.webp|" +
                        "All Files|*.*",
                FilterIndex = 1
            };
            
            var result = openFileDialog.ShowDialog();
            return result == true ? openFileDialog.FileName : null;
        }
    }
}
