using System.Threading.Tasks;

namespace FloorTrace.Services
{
    /// <summary>
    /// WPF implementation of dialog service
    /// </summary>
    public class DialogService : IDialogService
    {
        public Task ShowErrorAsync(string message, string title = "Error")
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }).Task;
        }

        public Task ShowWarningAsync(string message, string title = "Warning")
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }).Task;
        }

        public Task ShowInfoAsync(string message, string title = "Information")
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }).Task;
        }

        public Task<bool> ShowConfirmationAsync(string message, string title = "Confirm")
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                return result == System.Windows.MessageBoxResult.Yes;
            }).Task;
        }
    }
}

