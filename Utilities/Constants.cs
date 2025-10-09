namespace FloorTrace.Utilities
{
    /// <summary>
    /// Application-wide constants
    /// </summary>
    public static class Constants
    {
        // Zoom settings
        public const double ZoomStep = 0.1;
        public const double MinZoom = 0.1;
        public const double MaxZoom = 5.0;
        public const double DefaultZoom = 1.0;

        // Room detection
        public const float RoomEstimationMargin = 120f;
        public const double MinRoomDimension = 0.1; // feet

        // Perimeter detection
        public const int CannyLowThreshold = 50;
        public const int CannyHighThreshold = 150;
        public const double GaussianBlurSigma = 1.5;
        public const double ContourApproximationEpsilon = 0.01;
        public const int MinPerimeterPoints = 3;
        public const float DefaultPerimeterMarginRatio = 0.1f;
        
        // Wall thickness detection
        public const int DefaultWallThicknessPixels = 8;
        public const int MaxWallThicknessPixels = 50;
        public const int MinWallThicknessPixels = 2;

        // Rectilinear detection
        public const int HoughLineThreshold = 80;           // Minimum votes for line detection
        public const int HoughMinLineLength = 30;           // Minimum line length in pixels
        public const int HoughMaxLineGap = 15;              // Maximum gap between line segments
        public const float ParallelLineMergeTolerance = 5.0f; // Pixels
        public const float LineClusteringDistance = 10.0f;   // Pixels for grouping parallel lines
        public const int MinWallThicknessAuto = 3;          // Minimum auto-detected wall thickness
        public const int MaxWallThicknessAuto = 60;         // Maximum auto-detected wall thickness

        // Storage
        public const int MaxRecentSketches = 25;
        public const int RetainedLogFileDays = 7;
        public const int MaxLogFileSizeBytes = 10485760; // 10MB

        // Image processing
        public const int ThumbnailMaxWidth = 150;
        public const int ThumbnailMaxHeight = 100;
        public const int MaxImageWidth = 10000;
        public const int MaxImageHeight = 10000;

        // UI
        public const double MinControlWidth = 20;
        public const double MinControlHeight = 20;
        public const double VertexHandleSize = 18;
        public const double VertexHandleOffset = 9;

        // Manual mode overlay sizes
        public const int ManualRoomOverlaySize = 400;  // pixels
        public const int ManualPerimeterOverlaySize = 800; // pixels

        // Snapping
        public const float SnapToIntersectionDistance = 10f; // Distance in pixels to snap to intersection points
        public const float SecondaryAlignmentDistance = 10f; // Distance in pixels to align nearby vertices

        // File paths
        public const string AppDataFolderName = "FloorTrace";
        public const string SketchesFolderName = "Sketches";
        public const string PermanentSketchesFolderName = "Permanent";
        public const string SettingsFileName = "settings.json";
        public const string LogsFolderName = "Logs";
    }
}

