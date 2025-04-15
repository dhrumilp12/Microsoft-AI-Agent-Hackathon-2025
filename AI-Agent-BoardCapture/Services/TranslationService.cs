using ClassroomBoardCapture.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Service for translating text using Azure Translator API
    /// </summary>
    public class TranslationService : ITranslationService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<TranslationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public TranslationService(
            AppSettings settings,
            ILogger<TranslationService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }
        
        /// <summary>
        /// Translates text from one language to another using Azure Translator API
        /// </summary>
        /// <param name="text">Text to translate</param>
        /// <param name="sourceLanguage">Source language code</param>
        /// <param name="targetLanguage">Target language code</param>
        /// <returns>Translated text or empty string if translation failed</returns>
        public async Task<string> TranslateTextAsync(string text, string sourceLanguage, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
                
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _settings.TranslatorApi.ApiKey);
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _settings.TranslatorApi.Region);
                
                // Prepare the request URL
                string route = $"/translate?api-version=3.0&from={sourceLanguage}&to={targetLanguage}";
                string translateUrl = _settings.TranslatorApi.Endpoint + route;
                
                // Prepare the request data
                var body = new[] { new { Text = text } };
                var requestBody = JsonSerializer.Serialize(body);
                
                // Send the translation request
                using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(translateUrl, content);
                response.EnsureSuccessStatusCode();
                
                // Parse the response
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                // Extract the translated text
                var translatedText = root[0].GetProperty("translations")[0].GetProperty("text").GetString();
                return translatedText ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Translation failed");
                return $"[Translation error: {ex.Message}]";
            }
        }
    }
}