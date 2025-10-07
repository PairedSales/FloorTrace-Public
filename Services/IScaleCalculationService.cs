using System.Collections.Generic;
using System.Threading.Tasks;
using FloorTrace.Models;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    /// <summary>
    /// Defines the contract for scale calculation operations based on detected room dimensions.
    /// This service handles room detection and scale calculation for accurate area measurements.
    /// </summary>
    public interface IScaleCalculationService
    {
        /// <summary>
        /// Detects rooms with dimension labels in the floor plan image using OCR and computer vision.
        /// </summary>
        /// <param name="image">The floor plan image to analyze for room detection.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a list of detected rooms.</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null.</exception>
        Task<List<Room>> DetectRoomsAsync(BitmapImage image);
        
        /// <summary>
        /// Calculates the scale factor (pixels per foot) based on a detected room's dimensions and bounds.
        /// </summary>
        /// <param name="room">The room containing dimension information and bounds.</param>
        /// <param name="image">The floor plan image for reference.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the scale factor in pixels per foot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when room or image is null.</exception>
        /// <exception cref="ArgumentException">Thrown when room dimensions are invalid or bounds are empty.</exception>
        Task<double> CalculateScaleFromRoomAsync(Room room, BitmapImage image);
        
        /// <summary>
        /// Validates that the provided dimension string is in a recognized format and can be parsed.
        /// </summary>
        /// <param name="dimensions">The dimension string to validate (e.g., "12x15", "10' x 12'").</param>
        /// <returns>A Task that represents the asynchronous operation and returns true if valid, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when dimensions is null.</exception>
        Task<bool> ValidateRoomDimensionsAsync(string dimensions);
        
        /// <summary>
        /// Updates a room's overlay information based on the current image analysis.
        /// This method can be used to refresh room detection data when the image changes.
        /// </summary>
        /// <param name="room">The room to update.</param>
        /// <param name="image">The floor plan image for reference.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the updated room.</returns>
        /// <exception cref="ArgumentNullException">Thrown when room or image is null.</exception>
        Task<Room> UpdateRoomOverlayAsync(Room room, BitmapImage image);
    }
}
