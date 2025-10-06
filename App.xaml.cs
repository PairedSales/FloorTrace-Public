using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.Threading.Tasks;
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
                    services.AddSingleton<Services.IImageProcessingService, Services.ImageProcessingService>();
                    services.AddSingleton<Services.IScaleCalculationService, Services.ScaleCalculationService>();
                    services.AddSingleton<Services.IAreaCalculationService, Services.AreaCalculationService>();
                    services.AddSingleton<Services.IStorageService, Services.StorageService>();
                    services.AddTransient<ViewModels.MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }


        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            AppHost?.Dispose();
            Log.CloseAndFlush();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterGlobalExceptionHandlers();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                Log.Logger.Error(exArgs.Exception, "DispatcherUnhandledException");
                exArgs.Handled = true;
                System.Windows.MessageBox.Show("An unexpected error occurred. Details were logged.");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                if (exArgs.ExceptionObject is Exception ex)
                    Log.Logger.Fatal(ex, "UnhandledException");
            };
            TaskScheduler.UnobservedTaskException += (s, exArgs) =>
            {
                Log.Logger.Error(exArgs.Exception, "UnobservedTaskException");
                exArgs.SetObserved();
            };
        }


    }

}
