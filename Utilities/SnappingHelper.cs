using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FloorTrace.Utilities
{
    /// <summary>
    /// Utility class for snapping vertices to wall line intersection points and aligning nearby vertices.
    /// </summary>
    public static class SnappingHelper
    {
        /// <summary>
        /// Finds all intersection points from crossing horizontal and vertical wall lines.
        /// </summary>
        /// <param name="horizontalLines">List of Y-coordinates for horizontal wall lines.</param>
        /// <param name="verticalLines">List of X-coordinates for vertical wall lines.</param>
        /// <returns>List of all intersection points.</returns>
        public static List<PointF> FindAllIntersectionPoints(List<float> horizontalLines, List<float> verticalLines)
        {
            var intersections = new List<PointF>();
            
            if (horizontalLines == null || verticalLines == null)
                return intersections;

            // Create intersections at every crossing point
            foreach (var horizontalY in horizontalLines)
            {
                foreach (var verticalX in verticalLines)
                {
                    intersections.Add(new PointF(verticalX, horizontalY));
                }
            }

            return intersections;
        }

        /// <summary>
        /// Finds the nearest intersection point to a given position within the snap distance.
        /// </summary>
        /// <param name="position">The current position to snap from.</param>
        /// <param name="intersections">List of available intersection points.</param>
        /// <param name="snapDistance">Maximum distance for snapping.</param>
        /// <returns>The snapped position if an intersection is found within snap distance, otherwise null.</returns>
        public static PointF? FindNearestIntersection(PointF position, List<PointF> intersections, float snapDistance)
        {
            if (intersections == null || intersections.Count == 0)
                return null;

            PointF? nearestIntersection = null;
            float minDistance = float.MaxValue;

            foreach (var intersection in intersections)
            {
                float dx = position.X - intersection.X;
                float dy = position.Y - intersection.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDistance && distance <= snapDistance)
                {
                    minDistance = distance;
                    nearestIntersection = intersection;
                }
            }

            return nearestIntersection;
        }

        /// <summary>
        /// Applies secondary alignment to nearby vertices after a vertex has been snapped.
        /// Aligns other vertices that are within the alignment distance in either horizontal or vertical direction.
        /// </summary>
        /// <param name="points">The list of all perimeter points.</param>
        /// <param name="snappedIndex">The index of the vertex that was just snapped.</param>
        /// <param name="snappedPosition">The position the vertex was snapped to.</param>
        /// <param name="alignDistance">Maximum distance for secondary alignment.</param>
        public static void ApplySecondaryAlignment(List<PointF> points, int snappedIndex, PointF snappedPosition, float alignDistance)
        {
            if (points == null || snappedIndex < 0 || snappedIndex >= points.Count)
                return;

            // Check all other vertices for alignment opportunities
            for (int i = 0; i < points.Count; i++)
            {
                // Skip the snapped vertex itself
                if (i == snappedIndex)
                    continue;

                var point = points[i];
                bool modified = false;
                float newX = point.X;
                float newY = point.Y;

                // Check horizontal alignment (same Y coordinate)
                float verticalDistance = Math.Abs(point.Y - snappedPosition.Y);
                if (verticalDistance <= alignDistance)
                {
                    newY = snappedPosition.Y;
                    modified = true;
                }

                // Check vertical alignment (same X coordinate)
                float horizontalDistance = Math.Abs(point.X - snappedPosition.X);
                if (horizontalDistance <= alignDistance)
                {
                    newX = snappedPosition.X;
                    modified = true;
                }

                // Update the point if it was aligned
                if (modified)
                {
                    points[i] = new PointF(newX, newY);
                }
            }
        }
    }
}

