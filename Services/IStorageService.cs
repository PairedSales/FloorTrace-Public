using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FloorTrace.Models;

namespace FloorTrace.Services
{
    public interface IStorageService
    {
        Task SaveSketchAsync(Sketch sketch, BitmapImage? fullImage = null, BitmapImage? thumbnail = null);
        Task<Sketch> LoadSketchAsync(string sketchId);
        Task<List<Sketch>> LoadAllSketchesAsync();
        Task<List<Sketch>> LoadRecentSketchesAsync(int count = 25);
        Task DeleteSketchAsync(string sketchId);
        Task CleanupOldSketchesAsync();
        Task<BitmapImage?> LoadImageFromPathAsync(string imagePath);
    }
}
