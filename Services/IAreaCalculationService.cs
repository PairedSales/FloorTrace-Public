using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    /// <summary>
    /// Defines the contract for area calculation operations using geometric algorithms.
    /// This service handles perimeter tracing, area calculation, and unit conversions.
    /// </summary>
    public interface IAreaCalculationService
    {
        /// <summary>
        /// Traces the perimeter of a floor plan in the image using computer vision techniques.
        /// </summary>
        /// <param name="image">The floor plan image to analyze.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a list of points defining the perimeter.</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null.</exception>
        Task<List<PointF>> TracePerimeterAsync(BitmapImage image);
        
        /// <summary>
        /// Calculates the area enclosed by a polygon using Green's theorem for accurate results.
        /// </summary>
        /// <param name="perimeterPoints">The list of points that define the polygon perimeter.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the area in square pixels.</returns>
        /// <exception cref="ArgumentNullException">Thrown when perimeterPoints is null.</exception>
        /// <exception cref="ArgumentException">Thrown when perimeterPoints has fewer than 3 points.</exception>
        Task<double> CalculateAreaUsingGreensTheoremAsync(List<PointF> perimeterPoints);
        
        /// <summary>
        /// Calculates the length of each side of the perimeter polygon.
        /// </summary>
        /// <param name="perimeterPoints">The list of points that define the polygon perimeter.</param>
        /// <returns>A Task that represents the asynchronous operation and returns a list of side lengths in pixels.</returns>
        /// <exception cref="ArgumentNullException">Thrown when perimeterPoints is null.</exception>
        /// <exception cref="ArgumentException">Thrown when perimeterPoints has fewer than 3 points.</exception>
        Task<List<double>> CalculateSideLengthsAsync(List<PointF> perimeterPoints);
        
        /// <summary>
        /// Converts a measurement from pixels to feet using the provided scale factor.
        /// </summary>
        /// <param name="pixels">The measurement in pixels to convert.</param>
        /// <param name="scale">The scale factor in pixels per foot.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the measurement in feet.</returns>
        /// <exception cref="ArgumentException">Thrown when scale is zero or negative.</exception>
        Task<double> ConvertPixelsToFeetAsync(double pixels, double scale);
        
        /// <summary>
        /// Converts a measurement from feet to pixels using the provided scale factor.
        /// </summary>
        /// <param name="feet">The measurement in feet to convert.</param>
        /// <param name="scale">The scale factor in pixels per foot.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the measurement in pixels.</returns>
        /// <exception cref="ArgumentException">Thrown when scale is zero or negative.</exception>
        Task<double> ConvertFeetToPixelsAsync(double feet, double scale);
    }
}
