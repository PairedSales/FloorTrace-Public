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
using System.IO;
using System.Reflection;

namespace FloorTrace
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static IHost AppHost { get; private set; } = null!;
        

        public App()
        {
            // Required to support packaged third-party DLLs in distribution builds
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            
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
                    services.AddSingleton<Services.IImageProcessingService, Services.ImageProcessingService>();
                    services.AddSingleton<Services.IScaleCalculationService, Services.ScaleCalculationService>();
                    services.AddSingleton<Services.IAreaCalculationService, Services.AreaCalculationService>();
                    services.AddSingleton<Services.IStorageService, Services.StorageService>();
                    services.AddSingleton<Services.IDialogService, Services.DialogService>();
                    
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
            
            await AppHost.StartAsync();
            
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

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var libsDirectory = Path.Combine(appDirectory, "libs");
            
            if (Directory.Exists(libsDirectory))
            {
                var assemblyPath = Path.Combine(libsDirectory, $"{assemblyName.Name}.dll");
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }
            
            return null;
        }


    }

}
