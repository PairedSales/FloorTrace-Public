using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FloorTrace.Utilities
{
    public static class PathUtils
    {
        private static readonly string AppDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloorTrace");

        public static string MakeRelativeToAppData(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return absolutePath;

            try
            {
                var full = Path.GetFullPath(absolutePath);
                var appDataFull = Path.GetFullPath(AppDataRoot);

                if (full.StartsWith(appDataFull, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = Path.GetRelativePath(appDataFull, full);
                    return NormalizeDirectorySeparators(relative);
                }
            }
            catch
            {
                // fall through
            }

            return absolutePath;
        }

        public static string ResolveToAppData(string maybeRelativePath)
        {
            if (string.IsNullOrWhiteSpace(maybeRelativePath))
                return maybeRelativePath;

            try
            {
                if (Path.IsPathRooted(maybeRelativePath))
                {
                    return Path.GetFullPath(maybeRelativePath);
                }

                var combined = Path.Combine(AppDataRoot, maybeRelativePath);
                return Path.GetFullPath(combined);
            }
            catch
            {
                return maybeRelativePath;
            }
        }

        public static string RedactUserPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var redacted = path;

            try
            {
                // Redact Windows user profile paths, e.g., C:\Users\username\...
                redacted = Regex.Replace(redacted, @"(?i)\b([A-Za-z]:\\)?Users\\[^\\]+", m =>
                {
                    var prefix = m.Groups[1].Success ? m.Groups[1].Value : string.Empty;
                    return $"{prefix}Users\\<redacted>";
                });

                // Redact Unix-like user home paths, e.g., /Users/username/...
                redacted = Regex.Replace(redacted, @"(?i)/Users/[^/]+", "/Users/<redacted>");

                // Redact LocalAppData absolute root with environment token
                var appDataFull = Path.GetFullPath(AppDataRoot).Replace('\\', '/');
                var normalized = redacted.Replace('\\', '/');
                if (normalized.StartsWith(appDataFull, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = "%LOCALAPPDATA%/FloorTrace" + normalized.Substring(appDataFull.Length);
                }
                // Restore native separators for the current OS
                redacted = NormalizeDirectorySeparators(normalized);
            }
            catch
            {
                // ignore
            }

            return redacted;
        }

        private static string NormalizeDirectorySeparators(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
#if WINDOWS
            return path.Replace('/', '\\');
#else
            return path.Replace('\\', '/');
#endif
        }
    }
}


