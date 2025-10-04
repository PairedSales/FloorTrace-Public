using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FloorTrace.Models;

namespace FloorTrace.Services
{
    public class StorageService : IStorageService
    {
        private readonly string _dataDirectory;
        private readonly string _sketchesDirectory;
        private readonly string _permanentSketchesDirectory;
        
        public StorageService()
        {
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloorTrace");
            _sketchesDirectory = Path.Combine(_dataDirectory, "Sketches");
            _permanentSketchesDirectory = Path.Combine(_dataDirectory, "Permanent");
            
            EnsureDirectoriesExist();
        }
        
        public async Task SaveSketchAsync(Sketch sketch)
        {
            await Task.Run(() =>
            {
                var directory = sketch.IsPermanent ? _permanentSketchesDirectory : _sketchesDirectory;
                var filePath = Path.Combine(directory, $"{sketch.Id}.json");
                
                var json = JsonSerializer.Serialize(sketch, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(filePath, json);
            });
        }
        
        public async Task<Sketch> LoadSketchAsync(string sketchId)
        {
            return await Task.Run(() =>
            {
                // Try permanent sketches first
                var permanentPath = Path.Combine(_permanentSketchesDirectory, $"{sketchId}.json");
                if (File.Exists(permanentPath))
                {
                    var json = File.ReadAllText(permanentPath);
                    return JsonSerializer.Deserialize<Sketch>(json) ?? new Sketch();
                }
                
                // Try regular sketches
                var regularPath = Path.Combine(_sketchesDirectory, $"{sketchId}.json");
                if (File.Exists(regularPath))
                {
                    var json = File.ReadAllText(regularPath);
                    return JsonSerializer.Deserialize<Sketch>(json) ?? new Sketch();
                }
                
                throw new FileNotFoundException($"Sketch with ID {sketchId} not found");
            });
        }
        
        public async Task<List<Sketch>> LoadAllSketchesAsync()
        {
            return await Task.Run(() =>
            {
                var sketches = new List<Sketch>();
                
                // Load permanent sketches
                sketches.AddRange(LoadSketchesFromDirectory(_permanentSketchesDirectory));
                
                // Load regular sketches
                sketches.AddRange(LoadSketchesFromDirectory(_sketchesDirectory));
                
                return sketches.OrderByDescending(s => s.DateModified).ToList();
            });
        }
        
        public async Task<List<Sketch>> LoadRecentSketchesAsync(int count = 25)
        {
            var allSketches = await LoadAllSketchesAsync();
            return allSketches.Take(count).ToList();
        }
        
        public async Task DeleteSketchAsync(string sketchId)
        {
            await Task.Run(() =>
            {
                var permanentPath = Path.Combine(_permanentSketchesDirectory, $"{sketchId}.json");
                var regularPath = Path.Combine(_sketchesDirectory, $"{sketchId}.json");
                
                if (File.Exists(permanentPath))
                {
                    File.Delete(permanentPath);
                }
                else if (File.Exists(regularPath))
                {
                    File.Delete(regularPath);
                }
            });
        }
        
        public async Task CleanupOldSketchesAsync()
        {
            await Task.Run(() =>
            {
                // Keep only the 25 most recent non-permanent sketches
                var sketchFiles = Directory.GetFiles(_sketchesDirectory, "*.json")
                    .Select(f => new { Path = f, LastWrite = File.GetLastWriteTime(f) })
                    .OrderByDescending(f => f.LastWrite)
                    .ToList();
                
                if (sketchFiles.Count > 25)
                {
                    var filesToDelete = sketchFiles.Skip(25);
                    foreach (var file in filesToDelete)
                    {
                        File.Delete(file.Path);
                    }
                }
            });
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
                    var sketch = JsonSerializer.Deserialize<Sketch>(json);
                    if (sketch != null)
                    {
                        sketches.Add(sketch);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue loading other sketches
                    System.Diagnostics.Debug.WriteLine($"Error loading sketch from {file}: {ex.Message}");
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
