using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FloorTrace.Models;
using Microsoft.Extensions.Logging;
using FloorTrace.Utilities;

namespace FloorTrace.Services
{
    /// <summary>
    /// Provides sketch persistence and storage operations.
    /// 
    /// NOTE: Storage functionality is currently disabled in v1.0.0-alpha and reserved for future releases.
    /// The service is fully implemented and tested, ready to enable when saving features are added to the UI.
    /// </summary>
    public class StorageService : IStorageService
    {
        private readonly ILogger<StorageService> _logger;
        private readonly string _dataDirectory;
        private readonly string _sketchesDirectory;
        private readonly string _permanentSketchesDirectory;
        private readonly string _imagesDirectory;
        
        public StorageService(ILogger<StorageService> logger)
        {
            _logger = logger;
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloorTrace");
            _sketchesDirectory = Path.Combine(_dataDirectory, "Sketches");
            _permanentSketchesDirectory = Path.Combine(_dataDirectory, "Permanent");
            _imagesDirectory = Path.Combine(_dataDirectory, "Images");
            
            EnsureDirectoriesExist();
        }
        
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public async Task SaveSketchAsync(Sketch sketch, BitmapImage? fullImage = null, BitmapImage? thumbnail = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Save images if provided
                    SaveSketchImages(sketch, fullImage, thumbnail);
                    
                    // Save sketch metadata
                    SaveSketchMetadata(sketch);
                    
                    _logger.LogInformation("Saved sketch {SketchId} to {Path}", sketch.Id, 
                        PathUtils.MakeRelativeToAppData(GetSketchFilePath(sketch)));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save sketch {SketchId}", sketch?.Id);
                    throw;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves sketch images (full image and thumbnail) to the appropriate directory.
        /// </summary>
        private void SaveSketchImages(Sketch sketch, BitmapImage? fullImage, BitmapImage? thumbnail)
        {
            if (fullImage == null && thumbnail == null)
                return;

            var imageDirectory = Path.Combine(_imagesDirectory, sketch.Id);
            Directory.CreateDirectory(imageDirectory);
            
            if (fullImage != null)
            {
                var imagePath = Path.Combine(imageDirectory, "image.png");
                SaveBitmapImageToPng(fullImage, imagePath);
                // Store relative path
                sketch.ImagePath = PathUtils.MakeRelativeToAppData(imagePath);
                _logger.LogInformation("Saved full image to {Path}", PathUtils.MakeRelativeToAppData(imagePath));
            }
            
            if (thumbnail != null)
            {
                var thumbnailPath = Path.Combine(imageDirectory, "thumbnail.png");
                SaveBitmapImageToPng(thumbnail, thumbnailPath);
                // Store relative path
                sketch.ThumbnailPath = PathUtils.MakeRelativeToAppData(thumbnailPath);
                _logger.LogInformation("Saved thumbnail to {Path}", PathUtils.MakeRelativeToAppData(thumbnailPath));
            }
        }

        /// <summary>
        /// Saves sketch metadata to JSON file.
        /// </summary>
        private void SaveSketchMetadata(Sketch sketch)
        {
            var filePath = GetSketchFilePath(sketch);
            var json = JsonSerializer.Serialize(sketch, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Gets the file path for a sketch based on its permanence setting.
        /// </summary>
        private string GetSketchFilePath(Sketch sketch)
        {
            var directory = sketch.IsPermanent ? _permanentSketchesDirectory : _sketchesDirectory;
            return Path.Combine(directory, $"{sketch.Id}.json");
        }

        /// <summary>
        /// Attempts to load a sketch from permanent or regular sketches directory.
        /// </summary>
        private Sketch? TryLoadSketchFromPath(string sketchId)
        {
            // Try permanent sketches first
            var permanentPath = Path.Combine(_permanentSketchesDirectory, $"{sketchId}.json");
            if (File.Exists(permanentPath))
            {
                return LoadSketchFromFile(permanentPath, sketchId);
            }
            
            // Try regular sketches
            var regularPath = Path.Combine(_sketchesDirectory, $"{sketchId}.json");
            if (File.Exists(regularPath))
            {
                return LoadSketchFromFile(regularPath, sketchId);
            }
            
            return null;
        }

        /// <summary>
        /// Loads a sketch from a specific file path.
        /// </summary>
        private Sketch LoadSketchFromFile(string filePath, string sketchId)
        {
            var json = File.ReadAllText(filePath);
            var sketch = JsonSerializer.Deserialize<Sketch>(json, _jsonOptions);
            _logger.LogInformation("Loaded sketch {SketchId} from {Path}", sketchId, filePath);
            return sketch!;
        }

        /// <summary>
        /// Normalizes sketch paths to relative paths.
        /// </summary>
        private void NormalizeSketchPaths(Sketch sketch)
        {
            if (!string.IsNullOrEmpty(sketch.ImagePath))
            {
                sketch.ImagePath = PathUtils.MakeRelativeToAppData(sketch.ImagePath);
            }
            if (!string.IsNullOrEmpty(sketch.ThumbnailPath))
            {
                sketch.ThumbnailPath = PathUtils.MakeRelativeToAppData(sketch.ThumbnailPath);
            }
        }

        /// <summary>
        /// Loads and attaches thumbnail to sketch if available.
        /// </summary>
        private void LoadAndAttachThumbnail(Sketch sketch)
        {
            if (!string.IsNullOrEmpty(sketch.ThumbnailPath))
            {
                var thumbFull = PathUtils.ResolveToAppData(sketch.ThumbnailPath);
                if (File.Exists(thumbFull))
                {
                    sketch.Thumbnail = LoadBitmapImageFromFile(thumbFull);
                    _logger.LogInformation("Loaded thumbnail from {Path}", PathUtils.MakeRelativeToAppData(thumbFull));
                }
            }
        }
        
        public async Task<Sketch> LoadSketchAsync(string sketchId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to load sketch from file
                    var sketch = TryLoadSketchFromPath(sketchId);
                    if (sketch == null)
                    {
                        throw new FileNotFoundException($"Sketch with ID {sketchId} not found");
                    }
                    
                    // Normalize paths and load thumbnail
                    NormalizeSketchPaths(sketch);
                    LoadAndAttachThumbnail(sketch);
                    
                    return sketch;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load sketch {SketchId}", sketchId);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        
        public async Task<List<Sketch>> LoadAllSketchesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sketches = new List<Sketch>();
                    
                    // Load permanent sketches
                    sketches.AddRange(LoadSketchesFromDirectory(_permanentSketchesDirectory));
                    
                    // Load regular sketches
                    sketches.AddRange(LoadSketchesFromDirectory(_sketchesDirectory));
                    
                    return sketches.OrderByDescending(s => s.DateModified).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load all sketches");
                    throw;
                }
            }).ConfigureAwait(false);
        }
        
        public async Task<List<Sketch>> LoadRecentSketchesAsync(int count = 25)
        {
            var allSketches = await LoadAllSketchesAsync().ConfigureAwait(false);
            return allSketches.Take(count).ToList();
        }
        
        public async Task DeleteSketchAsync(string sketchId)
        {
            await Task.Run(() =>
            {
                try
                {
                    var permanentPath = Path.Combine(_permanentSketchesDirectory, $"{sketchId}.json");
                    var regularPath = Path.Combine(_sketchesDirectory, $"{sketchId}.json");
                    
                    if (File.Exists(permanentPath))
                    {
                        File.Delete(permanentPath);
                        _logger.LogInformation("Deleted permanent sketch {SketchId}", sketchId);
                    }
                    else if (File.Exists(regularPath))
                    {
                        File.Delete(regularPath);
                        _logger.LogInformation("Deleted sketch {SketchId}", sketchId);
                    }
                    
                    // Delete associated images
                    var imageDirectory = Path.Combine(_imagesDirectory, sketchId);
                    if (Directory.Exists(imageDirectory))
                    {
                        Directory.Delete(imageDirectory, true);
                        _logger.LogInformation("Deleted image directory for sketch {SketchId}", sketchId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete sketch {SketchId}", sketchId);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        
        public async Task CleanupOldSketchesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Keep only the most recent non-permanent sketches
                    var sketchFiles = Directory.GetFiles(_sketchesDirectory, "*.json")
                        .Select(f => new { Path = f, LastWrite = File.GetLastWriteTime(f), SketchId = Path.GetFileNameWithoutExtension(f) })
                        .OrderByDescending(f => f.LastWrite)
                        .ToList();
                    
                    if (sketchFiles.Count > Utilities.Constants.MaxRecentSketches)
                    {
                        var filesToDelete = sketchFiles.Skip(Utilities.Constants.MaxRecentSketches);
                        foreach (var file in filesToDelete)
                        {
                            File.Delete(file.Path);
                            _logger.LogInformation("Cleaned up old sketch file {Path}", PathUtils.MakeRelativeToAppData(file.Path));
                            
                            // Delete associated images
                            var imageDirectory = Path.Combine(_imagesDirectory, file.SketchId);
                            if (Directory.Exists(imageDirectory))
                            {
                                Directory.Delete(imageDirectory, true);
                                _logger.LogInformation("Cleaned up old image directory for sketch {SketchId}", file.SketchId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cleanup old sketches");
                    throw;
                }
            }).ConfigureAwait(false);
        }
        
        private List<Sketch> LoadSketchesFromDirectory(string directory)
        {
            var sketches = new List<Sketch>();
            
            if (!Directory.Exists(directory))
                return sketches;
            
            var jsonFiles = Directory.GetFiles(directory, "*.json");
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var sketch = JsonSerializer.Deserialize<Sketch>(json, _jsonOptions);
                    if (sketch != null)
                    {
                        // Load thumbnail if path exists
                        if (!string.IsNullOrEmpty(sketch.ThumbnailPath))
                        {
                            try
                            {
                                // Ensure paths are relative and resolve for IO
                                sketch.ThumbnailPath = PathUtils.MakeRelativeToAppData(sketch.ThumbnailPath);
                                var thumbFull = PathUtils.ResolveToAppData(sketch.ThumbnailPath);
                                if (File.Exists(thumbFull))
                                {
                                    sketch.Thumbnail = LoadBitmapImageFromFile(thumbFull);
                                }
                            }
                            catch (Exception thumbEx)
                            {
                                _logger.LogWarning(thumbEx, "Failed to load thumbnail for sketch {SketchId}", sketch.Id);
                            }
                        }
                        
                        // Normalize image path to relative as well
                        if (!string.IsNullOrEmpty(sketch.ImagePath))
                        {
                            sketch.ImagePath = PathUtils.MakeRelativeToAppData(sketch.ImagePath);
                        }

                        sketches.Add(sketch);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue loading other sketches
                    _logger.LogError(ex, "Error loading sketch from {Path}", PathUtils.MakeRelativeToAppData(file));
                }
            }
            
            return sketches;
        }
        
        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_dataDirectory);
            Directory.CreateDirectory(_sketchesDirectory);
            Directory.CreateDirectory(_permanentSketchesDirectory);
            Directory.CreateDirectory(_imagesDirectory);
        }
        
        private void SaveBitmapImageToPng(BitmapImage image, string filePath)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
        
        private BitmapImage LoadBitmapImageFromFile(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        
        public async Task<BitmapImage?> LoadImageFromPathAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var resolved = PathUtils.ResolveToAppData(imagePath);
                    if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
                    {
                        _logger.LogWarning("Image file not found at {Path}", PathUtils.RedactUserPath(imagePath));
                        return null;
                    }
                    
                    return LoadBitmapImageFromFile(resolved);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load image from {Path}", PathUtils.RedactUserPath(imagePath));
                    return null;
                }
            }).ConfigureAwait(false);
        }
    }
}
