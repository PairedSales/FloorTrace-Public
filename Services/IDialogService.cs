using System.Threading.Tasks;

namespace FloorTrace.Services
{
    /// <summary>
    /// Service for displaying dialogs and user notifications
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        Task ShowErrorAsync(string message, string title = "Error");
        
        /// <summary>
        /// Shows a warning message to the user
        /// </summary>
        Task ShowWarningAsync(string message, string title = "Warning");
        
        /// <summary>
        /// Shows an information message to the user
        /// </summary>
        Task ShowInfoAsync(string message, string title = "Information");
        
        /// <summary>
        /// Shows a confirmation dialog and returns the user's choice
        /// </summary>
        Task<bool> ShowConfirmationAsync(string message, string title = "Confirm");
    }
}

