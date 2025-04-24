#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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
        
        public AzureTranslationService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            
            // Determine whether to use environment variables or appsettings.json
            bool useEnvVars = configuration.GetSection("AzureTranslator").GetValue<bool>("UseEnvironmentVariables", false);
            if (useEnvVars)
            {
                // Get credentials from environment variables
                _subscriptionKey = Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_KEY") ?? "";
                _endpoint = Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_ENDPOINT") ?? "";
                _region = Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_REGION") ?? "";
            }
            else
            {
                // Fall back to appsettings.json
                _subscriptionKey = configuration["AzureTranslator:Key"] ?? "";
                _endpoint = configuration["AzureTranslator:Endpoint"] ?? "";
                _region = configuration["AzureTranslator:Region"] ?? "";
            }
            
            // Remove trailing slash if it exists for proper URL construction
            if (_endpoint?.EndsWith('/') == true)
            {
                _endpoint = _endpoint.TrimEnd('/');
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
            
            try
            {
                // If we don't have valid Azure translator credentials, return a basic set of languages
                if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_endpoint))
                {
                    return GetDefaultLanguages();
                }
                
                string route = "/languages?api-version=3.0&scope=translation";
                
                // Add required headers
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(_subscriptionKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                }
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
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
                return GetDefaultLanguages();
            }
        }
        
        /// <summary>
        /// Returns a default set of common languages when Azure services are unavailable
        /// </summary>
        private Dictionary<string, string> GetDefaultLanguages()
        {
            return new Dictionary<string, string>
            {
                { "af", "Afrikaans" }, { "sq", "Albanian" }, { "am", "Amharic" },
                { "ar", "Arabic" }, { "hy", "Armenian" }, { "as", "Assamese" },
                { "az", "Azerbaijani" }, { "bn", "Bangla" }, { "ba", "Bashkir" },
                { "eu", "Basque" }, { "be", "Belarusian" }, { "bg", "Bulgarian" },
                { "ca", "Catalan" }, { "zh-Hans", "Chinese Simplified" }, { "zh-Hant", "Chinese Traditional" },
                { "hr", "Croatian" }, { "cs", "Czech" }, { "da", "Danish" },
                { "nl", "Dutch" }, { "en", "English" }, { "et", "Estonian" },
                { "fi", "Finnish" }, { "fr", "French" }, { "fr-CA", "French (Canada)" },
                { "de", "German" }, { "el", "Greek" }, { "gu", "Gujarati" },
                { "hi", "Hindi" }, { "hu", "Hungarian" }, { "is", "Icelandic" },
                { "id", "Indonesian" }, { "ga", "Irish" }, { "it", "Italian" },
                { "ja", "Japanese" }, { "kn", "Kannada" }, { "kk", "Kazakh" },
                { "ko", "Korean" }, { "lv", "Latvian" }, { "lt", "Lithuanian" },
                { "ms", "Malay" }, { "ml", "Malayalam" }, { "mt", "Maltese" },
                { "mr", "Marathi" }, { "nb", "Norwegian" }, { "fa", "Persian" },
                { "pl", "Polish" }, { "pt", "Portuguese (Brazil)" }, { "pt-PT", "Portuguese (Portugal)" },
                { "pa", "Punjabi" }, { "ro", "Romanian" }, { "ru", "Russian" },
                { "sr-Cyrl", "Serbian (Cyrillic)" }, { "sr-Latn", "Serbian (Latin)" },
                { "sk", "Slovak" }, { "sl", "Slovenian" }, { "es", "Spanish" },
                { "sw", "Swahili" }, { "sv", "Swedish" }, { "ta", "Tamil" },
                { "te", "Telugu" }, { "th", "Thai" }, { "tr", "Turkish" },
                { "uk", "Ukrainian" }, { "ur", "Urdu" }, { "vi", "Vietnamese" }
            };
        }
    }
}
