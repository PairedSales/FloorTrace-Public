using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace FloorTrace.Services
{
    public interface IAreaCalculationService
    {
        Task<List<PointF>> TracePerimeterAsync(BitmapImage image);
        Task<double> CalculateAreaUsingGreensTheoremAsync(List<PointF> perimeterPoints);
        Task<List<double>> CalculateSideLengthsAsync(List<PointF> perimeterPoints);
        Task<double> ConvertPixelsToFeetAsync(double pixels, double scale);
        Task<double> ConvertFeetToPixelsAsync(double feet, double scale);
    }
}
