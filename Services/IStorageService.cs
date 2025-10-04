using System.Collections.Generic;
using System.Threading.Tasks;
using FloorTrace.Models;

namespace FloorTrace.Services
{
    public interface IStorageService
    {
        Task SaveSketchAsync(Sketch sketch);
        Task<Sketch> LoadSketchAsync(string sketchId);
        Task<List<Sketch>> LoadAllSketchesAsync();
        Task<List<Sketch>> LoadRecentSketchesAsync(int count = 25);
        Task DeleteSketchAsync(string sketchId);
        Task CleanupOldSketchesAsync();
    }
}
