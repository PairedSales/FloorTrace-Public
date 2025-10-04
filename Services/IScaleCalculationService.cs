using System.Collections.Generic;
using System.Threading.Tasks;
using FloorTrace.Models;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    public interface IScaleCalculationService
    {
        Task<List<Room>> DetectRoomsAsync(BitmapImage image);
        Task<double> CalculateScaleFromRoomAsync(Room room, BitmapImage image);
        Task<bool> ValidateRoomDimensionsAsync(string dimensions);
        Task<Room> UpdateRoomOverlayAsync(Room room, BitmapImage image);
    }
}
