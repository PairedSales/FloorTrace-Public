using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FloorTrace
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static IHost AppHost { get; private set; } = null!;
        
        public static bool IsDarkMode { get; private set; } = true; // Default to dark mode

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog((context, services, loggerCfg) =>
                    loggerCfg.ReadFrom.Configuration(context.Configuration)
                             .ReadFrom.Services(services)
                             .Enrich.FromLogContext())
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddSingleton<Services.IImageProcessingService, Services.ImageProcessingService>();
                    services.AddSingleton<Services.IScaleCalculationService, Services.ScaleCalculationService>();
                    services.AddSingleton<Services.IAreaCalculationService, Services.AreaCalculationService>();
                    services.AddSingleton<Services.IStorageService, Services.StorageService>();
                    services.AddSingleton<Services.IDialogService, Services.DialogService>();
                    
                    // Register ViewModels and Views
                    services.AddTransient<ViewModels.MainWindowViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }


        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost.StopAsync();
            AppHost?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterGlobalExceptionHandlers();
            
            // Start the host
            await AppHost.StartAsync();
            
            // Load theme preference from configuration
            var configuration = AppHost.Services.GetRequiredService<IConfiguration>();
            IsDarkMode = configuration.GetValue<bool>("ApplicationSettings:IsDarkMode", true);
            
            // Apply initial theme
            ApplyTheme(IsDarkMode);
            
            // Get MainWindow from DI container and show it
            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                Log.Logger.Error(exArgs.Exception, "DispatcherUnhandledException");
                exArgs.Handled = true;
                
                var errorMessage = exArgs.Exception is InvalidOperationException 
                    ? $"Operation failed: {exArgs.Exception.Message}" 
                    : "An unexpected error occurred. Please check the logs for details.";
                    
                System.Windows.MessageBox.Show(
                    errorMessage, 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            };
            
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                if (exArgs.ExceptionObject is Exception ex)
                {
                    Log.Logger.Fatal(ex, "UnhandledException - Application will terminate");
                }
            };
            
            TaskScheduler.UnobservedTaskException += (s, exArgs) =>
            {
                Log.Logger.Error(exArgs.Exception, "UnobservedTaskException");
                exArgs.SetObserved();
            };
        }

        /// <summary>
        /// Applies the specified theme by updating all color resources
        /// </summary>
        /// <param name="isDarkMode">True for dark mode, false for light mode</param>
        public static void ApplyTheme(bool isDarkMode)
        {
            IsDarkMode = isDarkMode;
            
            var resources = Current.Resources;
            
            if (isDarkMode)
            {
                // Apply dark mode colors
                resources["Primary"] = resources["PrimaryDark"];
                resources["PrimaryContainer"] = resources["PrimaryContainerDark"];
                resources["OnPrimary"] = resources["OnPrimaryDark"];
                resources["OnPrimaryContainer"] = resources["OnPrimaryContainerDark"];
                
                resources["Secondary"] = resources["SecondaryDark"];
                resources["SecondaryContainer"] = resources["SecondaryContainerDark"];
                resources["OnSecondary"] = resources["OnSecondaryDark"];
                resources["OnSecondaryContainer"] = resources["OnSecondaryContainerDark"];
                
                resources["Tertiary"] = resources["TertiaryDark"];
                resources["TertiaryContainer"] = resources["TertiaryContainerDark"];
                resources["OnTertiary"] = resources["OnTertiaryDark"];
                resources["OnTertiaryContainer"] = resources["OnTertiaryContainerDark"];
                
                resources["Surface"] = resources["SurfaceDark"];
                resources["SurfaceVariant"] = resources["SurfaceVariantDark"];
                resources["Background"] = resources["BackgroundDark"];
                resources["OnSurface"] = resources["OnSurfaceDark"];
                resources["OnSurfaceVariant"] = resources["OnSurfaceVariantDark"];
                resources["Outline"] = resources["OutlineDark"];
                resources["OutlineVariant"] = resources["OutlineVariantDark"];
                
                resources["HoverOverlay"] = resources["HoverOverlayDark"];
                resources["PressedOverlay"] = resources["PressedOverlayDark"];
                resources["FocusOverlay"] = resources["FocusOverlayDark"];
                
                resources["Elevation1"] = resources["Elevation1Dark"];
                resources["Elevation2"] = resources["Elevation2Dark"];
                resources["Elevation3"] = resources["Elevation3Dark"];
                resources["Elevation4"] = resources["Elevation4Dark"];
                resources["Elevation5"] = resources["Elevation5Dark"];
            }
            else
            {
                // Apply light mode colors (reset to original values)
                resources["Primary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F51B5"));
                resources["PrimaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8EAF6"));
                resources["OnPrimary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                resources["OnPrimaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A237E"));
                
                resources["Secondary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                resources["SecondaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                resources["OnSecondary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                resources["OnSecondaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0D47A1"));
                
                resources["Tertiary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#009688"));
                resources["TertiaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0F2F1"));
                resources["OnTertiary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                resources["OnTertiaryContainer"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#004D40"));
                
                resources["Surface"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FAFAFA"));
                resources["SurfaceVariant"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EEEEEE"));
                resources["Background"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                resources["OnSurface"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#212121"));
                resources["OnSurfaceVariant"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#616161"));
                resources["Outline"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BDBDBD"));
                resources["OutlineVariant"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"));
                
                resources["HoverOverlay"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0A000000"));
                resources["PressedOverlay"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A000000"));
                resources["FocusOverlay"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F3F51B5"));
                
                resources["Elevation1"] = new DropShadowEffect { BlurRadius = 2, ShadowDepth = 1, Opacity = 0.15, Color = Colors.Black };
                resources["Elevation2"] = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 2, Opacity = 0.18, Color = Colors.Black };
                resources["Elevation3"] = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 3, Opacity = 0.20, Color = Colors.Black };
                resources["Elevation4"] = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 4, Opacity = 0.22, Color = Colors.Black };
                resources["Elevation5"] = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 5, Opacity = 0.24, Color = Colors.Black };
            }
        }


    }

}
