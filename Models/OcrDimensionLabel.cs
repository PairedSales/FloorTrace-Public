using System.Drawing;

namespace FloorTrace.Models
{
    /// <summary>
    /// Represents a detected OCR dimension label and its parsed values.
    /// </summary>
    public class OcrDimensionLabel
    {
        public string Text { get; set; } = string.Empty;
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }
        public RectangleF LabelBounds { get; set; }
    }
}


