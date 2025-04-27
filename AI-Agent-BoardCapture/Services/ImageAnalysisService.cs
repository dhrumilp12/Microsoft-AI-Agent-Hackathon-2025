using ClassroomBoardCapture.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Service for analyzing image content using Azure Computer Vision API
    /// </summary>
    public class ImageAnalysisService : IImageAnalysisService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ImageAnalysisService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITranslationService _translationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ImageAnalysisService(
            AppSettings settings,
            ILogger<ImageAnalysisService> logger,
            IHttpClientFactory httpClientFactory,
            ITranslationService translationService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        }
        
        /// <summary>
        /// Analyzes image content for objects, categories, and descriptions
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="sourceLanguage">Source language code</param>
        /// <param name="targetLanguage">Target language code for translation</param>
        /// <returns>Task representing the analysis operation</returns>
        public async Task AnalyzeImageContentAsync(string imagePath, string sourceLanguage, string targetLanguage)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _settings.VisionApi.ApiKey);
                
                // Read and convert the image file to byte array
                byte[] imageData = await File.ReadAllBytesAsync(imagePath);
                
                // Set the analyze URL with features to detect
                string analyzeUrl = $"{_settings.VisionApi.Endpoint}vision/v3.2/analyze?visualFeatures=Objects,Categories,Description";
                
                // Create a ByteArrayContent with the image data
                using var content = new ByteArrayContent(imageData);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                
                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(analyzeUrl, content);
                response.EnsureSuccessStatusCode();
                
                // Process the response
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(jsonResponse);
                var root = jsonDocument.RootElement;
                
                // Create analysis results file
                string analysisFilename = Path.ChangeExtension(imagePath, ".analysis.txt");
                using var writer = new StreamWriter(analysisFilename);
                
                await writer.WriteLineAsync($"Image Analysis Results for {Path.GetFileName(imagePath)}");
                await writer.WriteLineAsync("===========================================");
                await writer.WriteLineAsync();
                
                // Extract and display description
                if (root.TryGetProperty("description", out var description) && 
                    description.TryGetProperty("captions", out var captions) &&
                    captions.GetArrayLength() > 0)
                {
                    var caption = captions[0];
                    string captionText = caption.GetProperty("text").GetString() ?? string.Empty;
                    double confidence = caption.GetProperty("confidence").GetDouble();
                    
                    await writer.WriteLineAsync($"Content Summary (Confidence: {confidence:P2}):");
                    await writer.WriteLineAsync(captionText);
                    
                    // Translate the summary
                    string translatedSummary = await _translationService.TranslateTextAsync(
                        captionText, 
                        sourceLanguage, 
                        targetLanguage);
                    
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync($"Translated Summary ({targetLanguage}):");
                    await writer.WriteLineAsync(translatedSummary);
                    
                    _logger.LogInformation("Content summary detected: {Summary}", captionText);
                }
                else
                {
                    await writer.WriteLineAsync("No meaningful content description detected.");
                    _logger.LogInformation("No content description detected");
                }
                
                // Extract and display objects
                if (root.TryGetProperty("objects", out var objects) && objects.GetArrayLength() > 0)
                {
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("Objects Detected:");
                    
                    for (int i = 0; i < objects.GetArrayLength(); i++)
                    {
                        var obj = objects[i];
                        string objectName = obj.GetProperty("object").GetString() ?? string.Empty;
                        double confidence = obj.GetProperty("confidence").GetDouble();
                        
                        await writer.WriteLineAsync($"- {objectName} (confidence: {confidence:P2})");
                    }
                    
                    _logger.LogInformation("Objects detected in the image");
                }
                else
                {
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("No objects detected.");
                }
                
                // Extract and display categories
                if (root.TryGetProperty("categories", out var categories) && categories.GetArrayLength() > 0)
                {
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("Categories:");
                    
                    for (int i = 0; i < categories.GetArrayLength(); i++)
                    {
                        var category = categories[i];
                        string name = category.GetProperty("name").GetString() ?? string.Empty;
                        double score = category.GetProperty("score").GetDouble();
                        
                        await writer.WriteLineAsync($"- {name} (score: {score:P2})");
                    }
                }
                
                _logger.LogInformation("Analysis data written to {AnalysisFile}", analysisFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Content analysis failed for {ImagePath}", imagePath);
            }
        }
    }
}