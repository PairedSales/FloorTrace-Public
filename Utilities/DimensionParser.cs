using System;
using System.Text.RegularExpressions;

namespace FloorTrace.Utilities
{
    /// <summary>
    /// Utility class for parsing room dimension strings
    /// </summary>
    public static class DimensionParser
    {
        /// <summary>
        /// Checks if a string matches a dimension pattern (e.g., "12x15", "12'x15'", "12 ft x 15 ft")
        /// </summary>
        public static bool IsDimensionString(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var patterns = new[]
            {
                @"^\s*(\d+(?:\.\d+)?)\s*[x×]\s*(\d+(?:\.\d+)?)\s*$",
                @"^\s*(\d+)\s*'\s*(\d+)?\s*(?:""|″)?\s*[x×]\s*(\d+)\s*'\s*(\d+)?\s*(?:""|″)?\s*$",
                @"^\s*(\d+(?:\.\d+)?)\s*ft\s*[x×]\s*(\d+(?:\.\d+)?)\s*ft\s*$"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse dimension string into feet values
        /// </summary>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseDimensionsFeet(string text, out double widthFeet, out double heightFeet)
        {
            widthFeet = 0;
            heightFeet = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = NormalizeText(text);
            var parts = text.Split(new[] { 'x', '×' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length != 2)
                return false;

            widthFeet = ParseSingleDimension(parts[0].Trim());
            heightFeet = ParseSingleDimension(parts[1].Trim());

            return widthFeet > 0 && heightFeet > 0;
        }

        /// <summary>
        /// Normalizes special characters in dimension text
        /// </summary>
        private static string NormalizeText(string text)
        {
            // Replace Unicode prime and double prime with ASCII equivalents
            return text.Replace("\u2032", "'").Replace("\u2033", "\"");
        }

        /// <summary>
        /// Parses a single dimension value (width or height)
        /// </summary>
        private static double ParseSingleDimension(string text)
        {
            text = text.ToLowerInvariant();

            // Try parsing "12 ft" format
            var ftMatch = Regex.Match(text, @"^(?<ft>\d+(?:\.\d+)?)\s*ft");
            if (ftMatch.Success)
                return double.Parse(ftMatch.Groups["ft"].Value);

            // Try parsing feet and inches like 12' 6"
            int feetIdx = text.IndexOf("'");
            if (feetIdx >= 0)
            {
                var feetPart = text.Substring(0, feetIdx).Trim();
                var rest = text.Substring(feetIdx + 1);
                
                double feet = double.TryParse(feetPart, out var f) ? f : 0;
                double inches = 0;

                var inchMatch = Regex.Match(rest, @"(?<in>\d+(?:\.\d+)?)\s*(?:""|″)");
                if (inchMatch.Success)
                {
                    inches = double.Parse(inchMatch.Groups["in"].Value);
                }

                return feet + inches / 12.0;
            }

            // Try simple number with optional "feet" or "ft"
            text = text.Replace("feet", string.Empty).Replace("ft", string.Empty).Trim();
            return double.TryParse(text, out var val) ? val : 0;
        }

        /// <summary>
        /// Formats feet value into a readable string
        /// </summary>
        public static string FormatFeet(double feet)
        {
            if (feet <= 0)
                return "0 ft";

            int wholeFeet = (int)feet;
            double remainder = feet - wholeFeet;
            int inches = (int)Math.Round(remainder * 12);

            if (inches == 0)
                return $"{wholeFeet} ft";
            else if (inches == 12)
                return $"{wholeFeet + 1} ft";
            else
                return $"{wholeFeet}' {inches}\"";
        }
    }
}

