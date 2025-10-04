using System.Windows;
using FloorTrace.ViewModels;
using FloorTrace.Services;

namespace FloorTrace
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services and ViewModel
            var imageProcessingService = new ImageProcessingService();
            var scaleCalculationService = new ScaleCalculationService(imageProcessingService);
            var areaCalculationService = new AreaCalculationService();
            var storageService = new StorageService();
            
            DataContext = new MainWindowViewModel(
                imageProcessingService,
                scaleCalculationService,
                areaCalculationService,
                storageService);
        }
    }
}
