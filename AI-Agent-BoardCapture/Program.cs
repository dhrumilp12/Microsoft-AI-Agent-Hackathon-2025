using ClassroomBoardCapture.Models;
using ClassroomBoardCapture.Services;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ClassroomBoardCapture
{
    /// <summary>
    /// Main entry point for the Classroom Board Capture application
    /// This application captures whiteboard content, performs OCR, translates text,
    /// and analyzes image content using various AI services.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Load environment variables from .env file (for local development)
            Env.Load();
            
            // Build and configure the host
            using var host = CreateHostBuilder(args).Build();
            
            Console.WriteLine("Classroom Board Capture and Analysis System");
            Console.WriteLine("------------------------------------------");
            
            // Get the capture service from DI container
            var captureService = host.Services.GetRequiredService<IImageCaptureService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var config = host.Services.GetRequiredService<AppSettings>();

            // Change the target language basd on the arguments
            if (args.Length > 0)
            {
                config.TargetLanguage = args[0];
            }
            
            // Display configuration information
            Console.WriteLine($"Images will be saved to: {config.CaptureFolder}");
            Console.WriteLine($"Capture interval: {config.CaptureIntervalSeconds} seconds");
            Console.WriteLine($"Translation: {config.SourceLanguage} -> {config.TargetLanguage}");
            Console.WriteLine("Press ESC to stop the application");
            
            // Start the capture service
            await captureService.StartCaptureAsync(token => 
            {
                // Check for ESC key press to stop the service
                while (Console.ReadKey(true).Key != ConsoleKey.Escape)
                {
                    // Continue waiting for ESC
                }
                
                logger.LogInformation("User requested application termination");
                return Task.CompletedTask;
            });
            
            Console.WriteLine("Application terminated.");
        }
        
        /// <summary>
        /// Configure the host builder with services and configuration
        /// </summary>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    // Add configuration sources
                    config.AddJsonFile("appsettings.json", optional: false)
                          .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Bind configuration to settings model
                    var appSettings = new AppSettings();
                    hostContext.Configuration.Bind("AppSettings", appSettings);
                    services.AddSingleton(appSettings);
                    
                    // Register services
                    services.AddHttpClient();
                    services.AddSingleton<IImageCaptureService, ImageCaptureService>();
                    services.AddSingleton<IOcrService, OcrService>();
                    services.AddSingleton<ITranslationService, TranslationService>();
                    services.AddSingleton<IImageAnalysisService, ImageAnalysisService>();
                    
                    // Configure Serilog for file logging
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log"), rollingInterval: RollingInterval.Day)
                        .CreateLogger();

                    // Configure logging
                    services.AddLogging(builder =>
                    {
                        builder.AddSerilog()
                    });
                });
    }
}