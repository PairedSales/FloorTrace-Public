using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace FloorTrace.Utilities
{
    /// <summary>
    /// Utility class for geometric calculations and operations.
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// Finds the closest edge to a given point in a polygon defined by a list of points.
        /// </summary>
        /// <param name="point">The point to find the closest edge to.</param>
        /// <param name="polygonPoints">The points that define the polygon.</param>
        /// <returns>The index of the edge that is closest to the point.</returns>
        public static int FindClosestEdge(PointF point, List<PointF> polygonPoints)
        {
            double minDistance = double.MaxValue;
            int closestEdgeIndex = 0;

            for (int i = 0; i < polygonPoints.Count; i++)
            {
                int j = (i + 1) % polygonPoints.Count;
                var p1 = polygonPoints[i];
                var p2 = polygonPoints[j];

                // Calculate distance from point to line segment
                double distance = DistanceToLineSegment(point, p1, p2);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestEdgeIndex = i;
                }
            }

            return closestEdgeIndex;
        }

        /// <summary>
        /// Calculates the distance from a point to a line segment.
        /// </summary>
        /// <param name="point">The point to measure distance from.</param>
        /// <param name="lineStart">The start point of the line segment.</param>
        /// <param name="lineEnd">The end point of the line segment.</param>
        /// <returns>The shortest distance from the point to the line segment.</returns>
        public static double DistanceToLineSegment(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            
            if (dx == 0 && dy == 0)
                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));

            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double nearestX = lineStart.X + t * dx;
            double nearestY = lineStart.Y + t * dy;

            return Math.Sqrt(Math.Pow(point.X - nearestX, 2) + Math.Pow(point.Y - nearestY, 2));
        }

        /// <summary>
        /// Calculates the centroid (center of mass) of a polygon defined by a list of points.
        /// </summary>
        /// <param name="points">The points that define the polygon.</param>
        /// <returns>The centroid of the polygon.</returns>
        public static PointF CalculatePolygonCentroid(List<PointF> points)
        {
            if (points == null || points.Count == 0)
                return PointF.Empty;

            float sumX = 0;
            float sumY = 0;

            foreach (var point in points)
            {
                sumX += point.X;
                sumY += point.Y;
            }

            return new PointF(sumX / points.Count, sumY / points.Count);
        }

        /// <summary>
        /// Calculates the area of a polygon using the shoelace formula.
        /// </summary>
        /// <param name="points">The points that define the polygon.</param>
        /// <returns>The area of the polygon.</returns>
        public static double CalculatePolygonArea(List<PointF> points)
        {
            if (points == null || points.Count < 3)
                return 0.0;

            double area = 0.0;
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area) / 2.0;
        }

        /// <summary>
        /// Calculates the perimeter length of a polygon.
        /// </summary>
        /// <param name="points">The points that define the polygon.</param>
        /// <returns>The perimeter length of the polygon.</returns>
        public static double CalculatePolygonPerimeter(List<PointF> points)
        {
            if (points == null || points.Count < 2)
                return 0.0;

            double perimeter = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                double dx = points[j].X - points[i].X;
                double dy = points[j].Y - points[i].Y;
                perimeter += Math.Sqrt(dx * dx + dy * dy);
            }

            return perimeter;
        }

        /// <summary>
        /// Checks if a point is inside a polygon using the ray casting algorithm.
        /// </summary>
        /// <param name="point">The point to test.</param>
        /// <param name="polygonPoints">The points that define the polygon.</param>
        /// <returns>True if the point is inside the polygon, false otherwise.</returns>
        public static bool IsPointInsidePolygon(PointF point, List<PointF> polygonPoints)
        {
            if (polygonPoints == null || polygonPoints.Count < 3)
                return false;

            bool inside = false;
            int n = polygonPoints.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygonPoints[i].Y > point.Y) != (polygonPoints[j].Y > point.Y)) &&
                    (point.X < (polygonPoints[j].X - polygonPoints[i].X) * (point.Y - polygonPoints[i].Y) / 
                     (polygonPoints[j].Y - polygonPoints[i].Y) + polygonPoints[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Calculates the bounding rectangle of a set of points.
        /// </summary>
        /// <param name="points">The points to calculate bounds for.</param>
        /// <returns>The bounding rectangle of the points.</returns>
        public static RectangleF CalculateBoundingRectangle(List<PointF> points)
        {
            if (points == null || points.Count == 0)
                return RectangleF.Empty;

            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
