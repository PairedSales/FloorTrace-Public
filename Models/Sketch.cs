using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace FloorTrace.Models
{
    /// <summary>
    /// Represents the current state of the floor plan analysis workflow.
    /// </summary>
    public enum WorkflowState
    {
        /// <summary>No image has been loaded yet.</summary>
        NoImage,
        /// <summary>An image has been loaded and is ready for analysis.</summary>
        ImageLoaded,
        /// <summary>A room with dimensions has been detected and scale can be calculated.</summary>
        RoomDetected,
        /// <summary>The perimeter has been traced and is ready for area calculation.</summary>
        PerimeterTraced,
        /// <summary>The area has been calculated and results are available.</summary>
        AreaCalculated
    }

    /// <summary>
    /// Represents a floor plan sketch with all associated analysis data including rooms, perimeter, and calculated measurements.
    /// This is the main data model that encapsulates all information about a floor plan analysis session.
    /// </summary>
    public class Sketch
    {
        /// <summary>
        /// Gets or sets the unique identifier for this sketch.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the display name of the sketch.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the date and time when this sketch was first created.
        /// </summary>
        public DateTime DateCreated { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets the date and time when this sketch was last modified.
        /// </summary>
        public DateTime DateModified { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets a value indicating whether this sketch should be permanently saved (not subject to auto-cleanup).
        /// </summary>
        public bool IsPermanent { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the file path to the original floor plan image.
        /// Special value "Clipboard" indicates the image was pasted from clipboard.
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the file path to the thumbnail image for this sketch.
        /// </summary>
        public string ThumbnailPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the thumbnail image for display in the UI.
        /// This property is ignored during JSON serialization to avoid circular references.
        /// </summary>
        [JsonIgnore]
        public BitmapImage? Thumbnail { get; set; }
        
        /// <summary>
        /// Gets or sets the scale factor in pixels per foot, calculated from detected room dimensions.
        /// </summary>
        public double Scale { get; set; } = 1.0;
        
        /// <summary>
        /// Gets or sets the list of detected rooms in the floor plan.
        /// </summary>
        public List<Room> Rooms { get; set; } = new List<Room>();
        
        /// <summary>
        /// Gets or sets the list of points that define the traced perimeter of the floor plan.
        /// </summary>
        public List<PointF> PerimeterPoints { get; set; } = new List<PointF>();
        
        /// <summary>
        /// Gets or sets the calculated area of the floor plan in square feet.
        /// </summary>
        public double AreaSquareFeet { get; set; } = 0.0;
        
        /// <summary>
        /// Gets or sets the calculated side lengths of the perimeter in feet.
        /// </summary>
        public List<double> SideLengths { get; set; } = new List<double>();
        
        /// <summary>
        /// Gets or sets the detected horizontal wall lines for UI snapping assistance.
        /// </summary>
        public List<float> HorizontalWallLines { get; set; } = new List<float>();
        
        /// <summary>
        /// Gets or sets the detected vertical wall lines for UI snapping assistance.
        /// </summary>
        public List<float> VerticalWallLines { get; set; } = new List<float>();
        
        /// <summary>
        /// Gets or sets the room currently selected for scale calculation.
        /// </summary>
        public Room? SelectedRoomForScale { get; set; }
        
        /// <summary>
        /// Gets or sets the current state of the analysis workflow.
        /// </summary>
        public WorkflowState CurrentState { get; set; } = WorkflowState.NoImage;
    }
    
    /// <summary>
    /// Represents a detected room within a floor plan with its dimensions and bounds.
    /// </summary>
    public class Room
    {
        /// <summary>
        /// Gets or sets the unique identifier for this room.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the display name of the room (e.g., "Living Room", "Bedroom 1").
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the bounding rectangle of the room in image coordinates.
        /// </summary>
        public RectangleF Bounds { get; set; }
        
        /// <summary>
        /// Gets or sets the original dimension text as detected from the image (e.g., "12x15", "10' x 12'").
        /// </summary>
        public string Dimensions { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the width of the room in feet.
        /// </summary>
        public double WidthFeet { get; set; }
        
        /// <summary>
        /// Gets or sets the height of the room in feet.
        /// </summary>
        public double HeightFeet { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this room is currently selected in the UI.
        /// </summary>
        public bool IsSelected { get; set; } = false;
    }
}
