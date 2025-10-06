using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace FloorTrace.Models
{
    public enum WorkflowState
    {
        NoImage,
        ImageLoaded,
        RoomDetected,
        ScaleSet,
        PerimeterTraced,
        AreaCalculated
    }

    public class Sketch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public DateTime DateModified { get; set; } = DateTime.Now;
        public bool IsPermanent { get; set; } = false;
        
        // Image data
        public string ImagePath { get; set; } = string.Empty;
        public BitmapImage? Thumbnail { get; set; }
        
        // Analysis results
        public double Scale { get; set; } = 1.0; // pixels per foot
        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<PointF> PerimeterPoints { get; set; } = new List<PointF>();
        public double AreaSquareFeet { get; set; } = 0.0;
        public List<double> SideLengths { get; set; } = new List<double>();
        
        // Detected wall lines for snapping
        public List<float> HorizontalWallLines { get; set; } = new List<float>();
        public List<float> VerticalWallLines { get; set; } = new List<float>();
        
        // Selected room for scale calculation
        public Room? SelectedRoomForScale { get; set; }
        
        // Workflow state
        public WorkflowState CurrentState { get; set; } = WorkflowState.NoImage;
    }
    
    public class Room
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public RectangleF Bounds { get; set; }
        public string Dimensions { get; set; } = string.Empty; // e.g., "12x15"
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }
        public bool IsSelected { get; set; } = false;
    }
}
