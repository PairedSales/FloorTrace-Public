using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FloorTrace.Models;
using Microsoft.Extensions.Logging;

namespace FloorTrace.Services
{
    public class StorageService : IStorageService
    {
        private readonly ILogger<StorageService> _logger;
        private readonly string _dataDirectory;
        private readonly string _sketchesDirectory;
        private readonly string _permanentSketchesDirectory;
        
        public StorageService(ILogger<StorageService> logger)
        {
            _logger = logger;
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloorTrace");
            _sketchesDirectory = Path.Combine(_dataDirectory, "Sketches");
            _permanentSketchesDirectory = Path.Combine(_dataDirectory, "Permanent");
            
            EnsureDirectoriesExist();
        }
        
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public async Task SaveSketchAsync(Sketch sketch)
        {
            await Task.Run(() =>
            {
                try
                {
                    var directory = sketch.IsPermanent ? _permanentSketchesDirectory : _sketchesDirectory;
                    var filePath = Path.Combine(directory, $"{sketch.Id}.json");
                    
                    var json = JsonSerializer.Serialize(sketch, _jsonOptions);
                    
                    File.WriteAllText(filePath, json);
                    _logger.LogInformation("Saved sketch {SketchId} to {Path}", sketch.Id, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save sketch {SketchId}", sketch?.Id);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        
        public async Task<Sketch> LoadSketchAsync(string sketchId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try permanent sketches first
                    var permanentPath = Path.Combine(_permanentSketchesDirectory, $"{sketchId}.json");
                    if (File.Exists(permanentPath))
                    {
                        var json = File.ReadAllText(permanentPath);
                        _logger.LogInformation("Loaded sketch {SketchId} from {Path}", sketchId, permanentPath);
                        return JsonSerializer.Deserialize<Sketch>(json, _jsonOptions) ?? new Sketch();
                    }
                    
                    // Try regular sketches
                    var regularPath = Path.Combine(_sketchesDirectory, $"{sketchId}.json");
                    if (File.Exists(regularPath))
                    {
                        var json = File.ReadAllText(regularPath);
                        _logger.LogInformation("Loaded sketch {SketchId} from {Path}", sketchId, regularPath);
                        return JsonSerializer.Deserialize<Sketch>(json, _jsonOptions) ?? new Sketch();
                    }
                    
                    throw new FileNotFoundException($"Sketch with ID {sketchId} not found");
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
                        .Select(f => new { Path = f, LastWrite = File.GetLastWriteTime(f) })
                        .OrderByDescending(f => f.LastWrite)
                        .ToList();
                    
                    if (sketchFiles.Count > Utilities.Constants.MaxRecentSketches)
                    {
                        var filesToDelete = sketchFiles.Skip(Utilities.Constants.MaxRecentSketches);
                        foreach (var file in filesToDelete)
                        {
                            File.Delete(file.Path);
                            _logger.LogInformation("Cleaned up old sketch file {Path}", file.Path);
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
                        sketches.Add(sketch);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue loading other sketches
                    _logger.LogError(ex, "Error loading sketch from {Path}", file);
                }
            }
            
            return sketches;
        }
        
        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_dataDirectory);
            Directory.CreateDirectory(_sketchesDirectory);
            Directory.CreateDirectory(_permanentSketchesDirectory);
        }
    }
}
