using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    public interface IImageProcessingService
    {
        Task<BitmapImage> LoadImageAsync(string filePath);
        Task<BitmapImage> LoadImageFromClipboardAsync();
        Task<BitmapImage> CreateThumbnailAsync(BitmapImage sourceImage, int maxWidth, int maxHeight);
        Task<bool> SaveImageAsync(BitmapImage image, string filePath);
        string ShowOpenFileDialog();
        Task<List<PointF>> DetectPerimeterAsync(BitmapImage image);
    }
}
