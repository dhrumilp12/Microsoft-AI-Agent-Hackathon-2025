#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service that provides translation functionality using Azure Translator.
    /// </summary>
    public class AzureTranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _subscriptionKey = "";
        private readonly string _endpoint = "";
        private readonly string _region = "";
        private readonly bool _debug;
        private Dictionary<string, string>? _cachedLanguages = null;
        
        public AzureTranslationService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            
            // Determine whether to use environment variables or appsettings.json
            bool useEnvVars = configuration.GetSection("AzureTranslator").GetValue<bool>("UseEnvironmentVariables", false);
            if (useEnvVars)
            {
                // Get credentials from environment variables
                _subscriptionKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY") ?? "";
                _endpoint = Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT") ?? "";
                _region = Environment.GetEnvironmentVariable("TRANSLATOR_REGION") ?? "";
            }
            else
            {
                // Fall back to appsettings.json
                _subscriptionKey = configuration["AzureTranslator:Key"] ?? "";
                _endpoint = configuration["AzureTranslator:Endpoint"] ?? "";
                _region = configuration["AzureTranslator:Region"] ?? "";
            }
            
            _debug = configuration.GetSection("AzureTranslator").GetValue<bool>("Debug", false);
            
            // Remove trailing slash if it exists for proper URL construction
            if (_endpoint?.EndsWith('/') == true)
            {
                _endpoint = _endpoint.TrimEnd('/');
            }
        }
        
        /// <summary>
        /// Translates text to the specified target language.
        /// </summary>
        public async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            try
            {
                // Build the request URL with query parameters
                string route = $"/translate?api-version=3.0&to={targetLanguage}";
                string requestUri = $"{_endpoint}{route}";

                // Only show minimal output about what's happening
                if (_debug && false) // Disable debug output completely
                {
                    Console.WriteLine($"Debug: Translation request URL: {requestUri}");
                    Console.WriteLine($"Debug: Translating content of length {text.Length} to {targetLanguage}");
                }
                else
                {
                    // Instead of a debug log, show a simple message
                    Console.WriteLine($"Translating text to {targetLanguage}...");
                }

                // Create the request body with the text to be translated
                var body = new[] { new { Text = text } };
                var requestBody = JsonSerializer.Serialize(body);

                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(requestUri);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", _region);

                    // Send the request to the API
                    var response = await client.SendAsync(request);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Log the response details only in debug mode
                    if (_debug && false) // Disable debug output completely
                    {
                        Console.WriteLine($"Debug: Response status: {response.StatusCode} {response.ReasonPhrase}");
                        Console.WriteLine($"Debug: Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
                        Console.WriteLine($"Debug: Raw response: {responseContent}");
                    }

                    // Handle errors
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Translation API Error: {response.StatusCode}");
                        Console.WriteLine($"Details: {responseContent}");
                        return string.Empty;
                    }

                    // Parse the response to get the translated text
                    using (JsonDocument doc = JsonDocument.Parse(responseContent))
                    {
                        var translations = doc.RootElement[0].GetProperty("translations");
                        string translatedText = translations[0].GetProperty("text").GetString() ?? string.Empty;
                        
                        if (!string.IsNullOrEmpty(translatedText))
                        {
                            Console.WriteLine("Successfully translated to " + targetLanguage);
                        }
                        return translatedText;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation error: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Gets all available languages for translation.
        /// </summary>
        public async Task<Dictionary<string, string>> GetAvailableLanguagesAsync()
        {
            // Use cached languages if available
            if (_cachedLanguages != null)
            {
                return _cachedLanguages;
            }
            
            string route = "/languages?api-version=3.0&scope=translation";
            
            // Add required headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            try
            {
                var response = await _httpClient.GetAsync($"{_endpoint}{route}");
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse the JSON response
                using var document = JsonDocument.Parse(responseContent);
                var translation = document.RootElement.GetProperty("translation");
                
                var languages = new Dictionary<string, string>();
                
                foreach (var lang in translation.EnumerateObject())
                {
                    string languageCode = lang.Name;
                    string displayName = lang.Value.GetProperty("name").GetString() ?? languageCode;
                    languages.Add(languageCode, displayName);
                }
                
                // Cache the languages
                _cachedLanguages = languages;
                return languages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching languages: {ex.Message}");
                
                // Return a minimal set of common languages as fallback
                return new Dictionary<string, string>
                {
                    { "en", "English" },
                    { "es", "Spanish" },
                    { "fr", "French" },
                    { "de", "German" },
                    { "zh-Hans", "Chinese Simplified" },
                    { "ja", "Japanese" },
                    { "ru", "Russian" },
                    { "ar", "Arabic" },
                    { "pt", "Portuguese" },
                    { "it", "Italian" }
                };
            }
        }
        
        /// <summary>
        /// Detects the language of a text.
        /// </summary>
        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "en"; // Default to English
            }
            
            string route = "/detect?api-version=3.0";
            
            // Take a sample of text to save on API costs
            string sampleText = text.Length > 500 ? text.Substring(0, 500) : text;
            
            var requestBody = new[] { new { Text = sampleText } };
            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            
            // Add required headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _region);
            
            try
            {
                var response = await _httpClient.PostAsync($"{_endpoint}{route}", requestContent);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse the JSON response
                var detectionResult = JsonSerializer.Deserialize<List<LanguageDetectionResult>>(responseContent);
                
                if (detectionResult != null && detectionResult.Count > 0 && detectionResult[0].Language != null)
                {
                    return detectionResult[0].Language;
                }
                
                return "en"; // Default to English
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Language detection error: {ex.Message}");
                return "en"; // Default to English on error
            }
        }
        
        // Helper classes for JSON deserialization
        private class TranslationResult
        {
            public List<Translation> Translations { get; set; } = new List<Translation>();
        }
        
        private class Translation
        {
            public string Text { get; set; } = "";
            public string To { get; set; } = "";
        }
        
        private class LanguageDetectionResult
        {
            public string Language { get; set; } = "";
            public float Score { get; set; }
        }
    }
}
