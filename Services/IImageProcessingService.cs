using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    /// <summary>
    /// Defines the contract for image processing operations including loading, saving, and computer vision analysis.
    /// This service handles all image-related operations for the FloorTrace application.
    /// </summary>
    public interface IImageProcessingService
    {
        /// <summary>
        /// Loads an image from the specified file path asynchronously.
        /// </summary>
        /// <param name="filePath">The path to the image file to load.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the loaded BitmapImage.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when the file format is not supported.</exception>
        Task<BitmapImage> LoadImageAsync(string filePath);
        
        /// <summary>
        /// Loads an image from the system clipboard asynchronously.
        /// </summary>
        /// <returns>A Task that represents the asynchronous operation and returns the loaded BitmapImage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no image is available in the clipboard.</exception>
        Task<BitmapImage> LoadImageFromClipboardAsync();
        
        /// <summary>
        /// Creates a thumbnail image from the source image with the specified maximum dimensions.
        /// </summary>
        /// <param name="sourceImage">The source image to create a thumbnail from.</param>
        /// <param name="maxWidth">The maximum width of the thumbnail in pixels.</param>
        /// <param name="maxHeight">The maximum height of the thumbnail in pixels.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the thumbnail BitmapImage.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sourceImage is null.</exception>
        Task<BitmapImage> CreateThumbnailAsync(BitmapImage sourceImage, int maxWidth, int maxHeight);
        
        /// <summary>
        /// Saves the specified image to the given file path asynchronously.
        /// </summary>
        /// <param name="image">The image to save.</param>
        /// <param name="filePath">The path where the image should be saved.</param>
        /// <returns>A Task that represents the asynchronous operation and returns true if successful, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
        Task<bool> SaveImageAsync(BitmapImage image, string filePath);
        
        /// <summary>
        /// Shows a file open dialog to allow the user to select an image file.
        /// </summary>
        /// <returns>The selected file path, or an empty string if no file was selected.</returns>
        string ShowOpenFileDialog();
        
        /// <summary>
        /// Detects the perimeter of a floor plan in the image using computer vision techniques.
        /// </summary>
        /// <param name="image">The floor plan image to analyze.</param>
        /// <param name="useInnerEdge">If true, detects the inner edge of walls accounting for wall thickness; if false, detects the outer edge.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a list of points defining the perimeter.</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null.</exception>
        Task<List<PointF>> DetectPerimeterAsync(BitmapImage image, bool useInnerEdge = true);
        
        /// <summary>
        /// Detects horizontal and vertical wall lines in the image for UI snapping assistance.
        /// </summary>
        /// <param name="image">The floor plan image to analyze.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a tuple containing lists of horizontal and vertical line positions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null.</exception>
        Task<(List<float> HorizontalLines, List<float> VerticalLines)> DetectWallLinesAsync(BitmapImage image);
    }
}
