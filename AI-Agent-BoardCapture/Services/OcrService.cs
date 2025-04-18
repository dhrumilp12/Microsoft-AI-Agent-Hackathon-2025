using ClassroomBoardCapture.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tesseract;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Service for extracting text from images using OCR
    /// </summary>
    public class OcrService : IOcrService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<OcrService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public OcrService(
            AppSettings settings,
            ILogger<OcrService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }
        
        /// <summary>
        /// Extracts text from an image using Azure Computer Vision API
        /// Falls back to Tesseract if API fails and fallback is enabled
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="language">Language code for OCR (e.g. 'en')</param>
        /// <returns>Extracted text or empty string if no text was found</returns>
        public async Task<string> ExtractTextAsync(string imagePath, string language)
        {
            try
            {
                // Try Azure Vision API first
                var result = await ExtractTextUsingAzureVisionAsync(imagePath, language);
                
                // If no text was found and Tesseract fallback is enabled, try Tesseract
                if (string.IsNullOrWhiteSpace(result) && _settings.Tesseract.UseTesseractFallback)
                {
                    _logger.LogInformation("No text found with Azure Vision API, trying Tesseract OCR");
                    
                    // Map language code to Tesseract format (e.g. 'en' -> 'eng')
                    string tessLanguage = MapLanguageCodeToTesseract(language);
                    
                    result = ExtractTextUsingTesseract(
                        imagePath, 
                        tessLanguage, 
                        _settings.Tesseract.TessDataPath);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image {ImagePath}", imagePath);
                
                // If Azure Vision fails and Tesseract fallback is enabled, try Tesseract
                if (_settings.Tesseract.UseTesseractFallback)
                {
                    _logger.LogInformation("Azure Vision API failed, trying Tesseract OCR");
                    
                    // Map language code to Tesseract format (e.g. 'en' -> 'eng')
                    string tessLanguage = MapLanguageCodeToTesseract(language);
                    
                    return ExtractTextUsingTesseract(
                        imagePath, 
                        tessLanguage, 
                        _settings.Tesseract.TessDataPath);
                }
                
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Maps ISO language codes to Tesseract language codes
        /// </summary>
        /// <param name="languageCode">ISO language code (e.g. 'en')</param>
        /// <returns>Tesseract language code (e.g. 'eng')</returns>
        private string MapLanguageCodeToTesseract(string languageCode)
        {
            return languageCode.ToLowerInvariant() switch
            {
                "en" => "eng",
                "zh" => "chi_sim",
                "es" => "spa",
                "fr" => "fra",
                "de" => "deu",
                "it" => "ita",
                "ja" => "jpn",
                "ko" => "kor",
                "pt" => "por",
                "ru" => "rus",
                _ => languageCode
            };
        }
        
        /// <summary>
        /// Extracts text from an image using Azure Computer Vision API
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="language">Language code for OCR (e.g. 'en')</param>
        /// <returns>Extracted text or empty string if no text was found</returns>
        private async Task<string> ExtractTextUsingAzureVisionAsync(string imagePath, string language)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _settings.VisionApi.ApiKey);
                
                // Read and convert the image file to byte array
                byte[] imageData = await File.ReadAllBytesAsync(imagePath);
                
                // Set the OCR URL with language parameter
                string ocrUrl = $"{_settings.VisionApi.Endpoint}vision/v3.2/ocr?language={language}&detectOrientation=true";
                
                // Create a ByteArrayContent with the image data
                using var content = new ByteArrayContent(imageData);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                
                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(ocrUrl, content);
                response.EnsureSuccessStatusCode();
                
                // Process the response
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OcrResult>(jsonResponse);
                
                // Extract text from the OCR result
                StringBuilder sb = new StringBuilder();
                
                if (result != null && result.Regions != null)
                {
                    foreach (var region in result.Regions)
                    {
                        foreach (var line in region.Lines)
                        {
                            foreach (var word in line.Words)
                            {
                                sb.Append(word.Text).Append(" ");
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                    }
                }
                
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Vision API text extraction failed");
                throw;
            }
        }
        
        /// <summary>
        /// Extracts text using local Tesseract OCR
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="language">Tesseract language code (e.g. 'eng')</param>
        /// <param name="tessDataPath">Path to Tesseract data files</param>
        /// <returns>Extracted text or empty string if no text was found</returns>
        public string ExtractTextUsingTesseract(string imagePath, string language, string tessDataPath)
        {
            try
            {
                using var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);

                return page.GetText().Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tesseract OCR failed");
                return string.Empty;
            }
        }
    }
}