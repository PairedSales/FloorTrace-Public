using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FloorTrace.Models;

namespace FloorTrace.Services
{
    /// <summary>
    /// Defines the contract for sketch persistence and storage operations.
    /// This service handles saving, loading, and managing floor plan sketches and their associated images.
    /// 
    /// NOTE: Storage functionality is currently disabled in v1.0.0-alpha and reserved for future releases.
    /// The service implementation is complete and ready for when saving features are enabled.
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Saves a sketch and its associated images to persistent storage.
        /// </summary>
        /// <param name="sketch">The sketch to save.</param>
        /// <param name="fullImage">The full-resolution image (optional).</param>
        /// <param name="thumbnail">The thumbnail image (optional).</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sketch is null.</exception>
        /// <exception cref="IOException">Thrown when storage operations fail.</exception>
        Task SaveSketchAsync(Sketch sketch, BitmapImage? fullImage = null, BitmapImage? thumbnail = null);
        
        /// <summary>
        /// Loads a sketch by its unique identifier.
        /// </summary>
        /// <param name="sketchId">The unique identifier of the sketch to load.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the loaded sketch.</returns>
        /// <exception cref="ArgumentException">Thrown when sketchId is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the sketch file does not exist.</exception>
        /// <exception cref="IOException">Thrown when file operations fail.</exception>
        Task<Sketch> LoadSketchAsync(string sketchId);
        
        /// <summary>
        /// Loads all sketches from storage.
        /// </summary>
        /// <returns>A Task that represents the asynchronous operation and returns a list of all sketches.</returns>
        /// <exception cref="IOException">Thrown when storage operations fail.</exception>
        Task<List<Sketch>> LoadAllSketchesAsync();
        
        /// <summary>
        /// Loads the most recent sketches up to the specified count.
        /// </summary>
        /// <param name="count">The maximum number of recent sketches to load.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a list of recent sketches.</returns>
        /// <exception cref="ArgumentException">Thrown when count is negative.</exception>
        /// <exception cref="IOException">Thrown when storage operations fail.</exception>
        Task<List<Sketch>> LoadRecentSketchesAsync(int count = 25);
        
        /// <summary>
        /// Deletes a sketch and its associated files from storage.
        /// </summary>
        /// <param name="sketchId">The unique identifier of the sketch to delete.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when sketchId is null or empty.</exception>
        /// <exception cref="IOException">Thrown when deletion operations fail.</exception>
        Task DeleteSketchAsync(string sketchId);
        
        /// <summary>
        /// Removes old sketches that exceed the configured maximum count, keeping only permanent sketches.
        /// </summary>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        /// <exception cref="IOException">Thrown when cleanup operations fail.</exception>
        Task CleanupOldSketchesAsync();
        
        /// <summary>
        /// Loads an image from the specified file path.
        /// </summary>
        /// <param name="imagePath">The path to the image file to load.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the loaded image, or null if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when imagePath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the image file does not exist.</exception>
        /// <exception cref="IOException">Thrown when file operations fail.</exception>
        Task<BitmapImage?> LoadImageFromPathAsync(string imagePath);
    }
}
