#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_Agent_Orchestrator.Services
{
    /// <summary>
    /// Service that provides translation functionality using Azure Translator.
    /// </summary>
    public class AzureTranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _subscriptionKey = "";
        private readonly string _endpoint = "";
        private readonly string _region = "";
        private Dictionary<string, string>? _cachedLanguages = null;
        private Dictionary<string, string> _translationCache = new Dictionary<string, string>();
        private readonly ILogger? _logger;
        
        public AzureTranslationService(IConfiguration configuration, ILogger<AzureTranslationService>? logger = null)
        {
            _httpClient = new HttpClient();
            _logger = logger;
            
            // Check if using environment variables (either at top level or service-specific)
            bool useEnvVars = configuration.GetValue<bool>("UseEnvironmentVariables", false) || 
                              configuration.GetSection("AzureTranslator").GetValue<bool>("UseEnvironmentVariables", false);

            // Set values with appropriate fallbacks based on configuration preference
            _subscriptionKey = useEnvVars 
                ? Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY") ?? ""
                : configuration["AzureTranslator:Key"] ?? "";

            _endpoint = useEnvVars
                ? Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT") ?? TranslationConstants.DefaultTranslatorEndpoint
                : configuration["AzureTranslator:Endpoint"] ?? TranslationConstants.DefaultTranslatorEndpoint;

            _region = useEnvVars
                ? Environment.GetEnvironmentVariable("TRANSLATOR_REGION") ?? ""
                : configuration["AzureTranslator:Region"] ?? "";
            
            // Always ensure we're using the correct Azure Translator endpoint
            // If the configured endpoint doesn't look like the translator API endpoint,
            // override it with the default endpoint
            if (!_endpoint.Contains("translator.com", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning($"Configured endpoint '{_endpoint}' doesn't appear to be a valid translator endpoint. " +
                                    $"Using default endpoint: {TranslationConstants.DefaultTranslatorEndpoint}");
                _endpoint = TranslationConstants.DefaultTranslatorEndpoint;
            }
            
            // Remove trailing slash if it exists for proper URL construction
            if (_endpoint?.EndsWith('/') == true)
            {
                _endpoint = _endpoint.TrimEnd('/');
            }
            
            _logger?.LogInformation($"Initialized Azure Translation Service with endpoint: {_endpoint}");
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
            
            try
            {
                // If we don't have valid Azure translator credentials, return a basic set of languages
                if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_endpoint))
                {
                    _logger?.LogWarning("Missing Azure Translator credentials. Using default language list.");
                    return TranslationConstants.GetDefaultLanguages();
                }
                
                string route = "/languages?api-version=3.0&scope=translation";
                
                // Add required headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(_subscriptionKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                }
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                _logger?.LogInformation($"Getting available languages from {_endpoint}{route}");
                var response = await _httpClient.GetAsync($"{_endpoint}{route}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"Failed to get languages: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return TranslationConstants.GetDefaultLanguages();
                }
                
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
                _logger?.LogInformation($"Fetched {languages.Count} languages successfully");
                return languages;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching languages");
                Console.WriteLine($"Error fetching languages: {ex.Message}");
                return TranslationConstants.GetDefaultLanguages();
            }
        }
        
        /// <summary>
        /// Translates the given text to the target language
        /// </summary>
        /// <param name="text">Text to translate</param>
        /// <param name="targetLanguageCode">Target language code</param>
        /// <param name="sourceLanguageCode">Optional source language code</param>
        /// <returns>Translated text or original text if translation fails</returns>
        public async Task<string> TranslateTextAsync(string text, string targetLanguageCode, string sourceLanguageCode = "")
        {
            // If the text is empty or target language is English, return the original text
            if (string.IsNullOrWhiteSpace(text) || targetLanguageCode == "en")
            {
                return text;
            }

            // Create a cache key to avoid repetitive translations
            string cacheKey = $"{text}|{targetLanguageCode}|{sourceLanguageCode}";
            
            // Check if we have a cached translation
            if (_translationCache.TryGetValue(cacheKey, out string? cachedTranslation))
            {
                return cachedTranslation;
            }

            try
            {
                // If we don't have valid Azure translator credentials, return the original text
                if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_endpoint))
                {
                    _logger?.LogWarning("Missing Azure Translator credentials. Cannot translate text.");
                    return text;
                }

                string route = "/translate?api-version=3.0";
                route += $"&to={targetLanguageCode}";
                
                if (!string.IsNullOrEmpty(sourceLanguageCode))
                {
                    route += $"&from={sourceLanguageCode}";
                }

                // Create request body
                var body = new object[] { new { Text = text } };
                var requestBody = JsonSerializer.Serialize(body);

                // Add required headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(_subscriptionKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                }
                if (!string.IsNullOrEmpty(_region))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _region);
                }
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Send request
                _logger?.LogDebug($"Translating text to {targetLanguageCode}: {text.Substring(0, Math.Min(50, text.Length))}...");
                using var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                    RequestUri = new Uri($"{_endpoint}{route}")
                };
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError($"Translation failed: {response.StatusCode} - {errorContent}");
                    Console.WriteLine($"Error translating text: {response.StatusCode} ({errorContent})");
                    return text;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse the JSON response
                using var document = JsonDocument.Parse(responseContent);
                var translations = document.RootElement[0].GetProperty("translations");
                var translation = translations[0].GetProperty("text").GetString() ?? text;

                // Cache the translation
                _translationCache[cacheKey] = translation;
                
                return translation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error translating text");
                Console.WriteLine($"Error translating text: {ex.Message}");
                return text;
            }
        }
        
        /// <summary>
        /// Translates multiple strings in a batch to improve performance
        /// </summary>
        public async Task<List<string>> TranslateTextBatchAsync(List<string> texts, string targetLanguageCode, string sourceLanguageCode = "")
        {
            // If no texts or target language is English, return the original texts
            if (texts == null || texts.Count == 0 || targetLanguageCode == "en")
            {
                return texts?.ToList() ?? new List<string>();
            }

            var results = new List<string>(texts.Count);
            var textsToTranslate = new List<(string Text, int Index)>();
            
            // Check which texts are already in cache
            for (int i = 0; i < texts.Count; i++)
            {
                string text = texts[i];
                string cacheKey = $"{text}|{targetLanguageCode}|{sourceLanguageCode}";
                
                if (_translationCache.TryGetValue(cacheKey, out string? cachedTranslation))
                {
                    results.Add(cachedTranslation);
                }
                else
                {
                    // Collect texts that need translation and their indexes
                    textsToTranslate.Add((text, i));
                    // Add a placeholder in results
                    results.Add("");
                }
            }
            
            // If all texts were cached, return results
            if (textsToTranslate.Count == 0)
            {
                return results;
            }

            try
            {
                // If we don't have valid Azure translator credentials, return the original texts
                if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_endpoint))
                {
                    _logger?.LogWarning("Missing Azure Translator credentials. Cannot batch translate texts.");
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (string.IsNullOrEmpty(results[i]))
                        {
                            results[i] = texts[i];
                        }
                    }
                    return results;
                }

                string route = "/translate?api-version=3.0";
                route += $"&to={targetLanguageCode}";
                
                if (!string.IsNullOrEmpty(sourceLanguageCode))
                {
                    route += $"&from={sourceLanguageCode}";
                }

                // Create request body
                var body = textsToTranslate.Select(t => new { Text = t.Text }).ToArray();
                var requestBody = JsonSerializer.Serialize(body);

                // Add required headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(_subscriptionKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                }
                if (!string.IsNullOrEmpty(_region))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _region);
                }
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Send request
                _logger?.LogDebug($"Batch translating {textsToTranslate.Count} texts to {targetLanguageCode}");
                using var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                    RequestUri = new Uri($"{_endpoint}{route}")
                };
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError($"Batch translation failed: {response.StatusCode} - {errorContent}");
                    Console.WriteLine($"Error batch translating texts: {response.StatusCode} ({errorContent})");
                    
                    // Use original texts if translation failed
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (string.IsNullOrEmpty(results[i]))
                        {
                            results[i] = texts[i];
                        }
                    }
                    
                    return results;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse the JSON response
                using var document = JsonDocument.Parse(responseContent);
                
                // Process each translation and update the results
                for (int i = 0; i < document.RootElement.GetArrayLength(); i++)
                {
                    var translations = document.RootElement[i].GetProperty("translations");
                    var translation = translations[0].GetProperty("text").GetString() ?? textsToTranslate[i].Text;
                    
                    // Get the original index to place this translation
                    int originalIndex = textsToTranslate[i].Index;
                    results[originalIndex] = translation;
                    
                    // Cache the translation
                    string cacheKey = $"{textsToTranslate[i].Text}|{targetLanguageCode}|{sourceLanguageCode}";
                    _translationCache[cacheKey] = translation;
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error batch translating texts");
                Console.WriteLine($"Error batch translating texts: {ex.Message}");
                
                // Use original texts if translation failed
                for (int i = 0; i < results.Count; i++)
                {
                    if (string.IsNullOrEmpty(results[i]))
                    {
                        results[i] = texts[i];
                    }
                }
                
                return results;
            }
        }
    }
}
