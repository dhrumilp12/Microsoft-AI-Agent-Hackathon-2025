using ClassroomBoardCapture.Models;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Service for capturing images from a camera
    /// </summary>
    public class ImageCaptureService : IImageCaptureService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ImageCaptureService> _logger;
        private readonly IOcrService _ocrService;
        private readonly ITranslationService _translationService;
        private readonly IImageAnalysisService _analysisService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ImageCaptureService(
            AppSettings settings,
            ILogger<ImageCaptureService> logger,
            IOcrService ocrService,
            ITranslationService translationService,
            IImageAnalysisService analysisService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
            
            // Create capture directory if it doesn't exist
            if (!Directory.Exists(_settings.CaptureFolder))
            {
                Directory.CreateDirectory(_settings.CaptureFolder);
                _logger.LogInformation("Created capture directory: {Directory}", _settings.CaptureFolder);
            }
        }
        
        /// <summary>
        /// Start capturing images at the configured interval
        /// </summary>
        /// <param name="waitForStopCallback">Callback to wait for stop signal</param>
        /// <returns>Task representing the capture process</returns>
        public async Task StartCaptureAsync(Func<CancellationToken, Task> waitForStopCallback)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            
            // Start the capture task
            var captureTask = CaptureImagesAsync(token);
            
            // Wait for stop signal
            await waitForStopCallback(token);
            
            // Cancel the capture task
            cancellationTokenSource.Cancel();
            
            try
            {
                await captureTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Capture task was cancelled");
            }
        }
        
        /// <summary>
        /// Captures images at regular intervals
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        private async Task CaptureImagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Initialize camera
                using var capture = new VideoCapture(_settings.CameraDeviceId);
                
                if (!capture.IsOpened())
                {
                    _logger.LogError("Could not open webcam device {DeviceId}. Please ensure a camera is connected.", 
                        _settings.CameraDeviceId);
                    return;
                }
                
                _logger.LogInformation("Camera initialized successfully");
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Capture frame
                    using var frame = new Mat();
                    capture.Read(frame);
                    
                    if (frame.Empty())
                    {
                        _logger.LogWarning("Captured frame is empty");
                        continue;
                    }
                    
                    // Save image with timestamp
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string filename = Path.Combine(_settings.CaptureFolder, $"capture_{timestamp}.jpg");
                    frame.SaveImage(filename);
                    
                    _logger.LogInformation("Image captured at {Timestamp}", DateTime.Now);
                    
                    // Analyze the image in the background to avoid blocking the next capture
                    _ = Task.Run(async () => await AnalyzeImageAsync(filename), cancellationToken);
                    
                    // Wait for the next capture interval
                    try
                    {
                        await Task.Delay(_settings.CaptureIntervalSeconds * 1000, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // This is expected when cancellation is requested
                        _logger.LogInformation("Capture delay was cancelled");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred during capture");
            }
        }
        
        /// <summary>
        /// Analyzes an image for text and content
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        private async Task AnalyzeImageAsync(string imagePath)
        {
            try
            {
                _logger.LogInformation("Analyzing image: {ImagePath}", imagePath);
                
                // 1. Extract text from the image (OCR)
                string extractedText = await _ocrService.ExtractTextAsync(imagePath, _settings.SourceLanguage);
                
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger.LogInformation("Text detected on whiteboard");
                    
                    // Translate the extracted text
                    string translatedText = await _translationService.TranslateTextAsync(
                        extractedText, 
                        _settings.SourceLanguage, 
                        _settings.TargetLanguage);
                    
                    // Save both original and translated text to a file
                    string textFilename = Path.ChangeExtension(imagePath, ".txt");
                    await File.WriteAllTextAsync(textFilename, 
                        $"ORIGINAL TEXT ({_settings.SourceLanguage}):\n{extractedText}\n\n" + 
                        $"TRANSLATED TEXT ({_settings.TargetLanguage}):\n{translatedText}");
                    
                    _logger.LogInformation("Text and translation saved to: {TextFile}", textFilename);
                }
                else
                {
                    _logger.LogInformation("No text detected on the whiteboard");
                }
                
                // 2. Analyze image content
                await _analysisService.AnalyzeImageContentAsync(imagePath, _settings.SourceLanguage, _settings.TargetLanguage);
                
                _logger.LogInformation("Analysis complete for {ImagePath}", imagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze image: {ImagePath}", imagePath);
            }
        }
    }
}