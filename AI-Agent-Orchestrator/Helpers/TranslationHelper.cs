using System.Collections.Concurrent;
using System.Globalization;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace AI_Agent_Orchestrator.Services
{
    public static class TranslationHelper
    {
        private static string _targetLanguage = "en";
        private static string _sourceLanguage = "";
        private static AzureTranslationService? _translationService;
        private static ConcurrentDictionary<string, string> _markupCache = new ConcurrentDictionary<string, string>();
        
        public static void Initialize(AzureTranslationService translationService, string targetLanguage, string sourceLanguage = "")
        {
            _translationService = translationService;
            _targetLanguage = targetLanguage;
            _sourceLanguage = sourceLanguage;
        }
        
        /// <summary>
        /// Translates a string and returns the translated text
        /// </summary>
        public static async Task<string> TranslateAsync(string text)
        {
            if (_translationService == null || string.IsNullOrWhiteSpace(text) || _targetLanguage == "en")
                return text;
                
            return await _translationService.TranslateTextAsync(text, _targetLanguage, _sourceLanguage);
        }
        
        /// <summary>
        /// Translates and displays text with Spectre.Console Markup
        /// </summary>
        public static async Task MarkupLineAsync(string markupText)
        {
            try
            {
                // If no translation service or English target, just display original
                if (_translationService == null || _targetLanguage == "en")
                {
                    AnsiConsole.MarkupLine(markupText);
                    return;
                }
                
                // Check cache first
                string cacheKey = $"{markupText}|{_targetLanguage}";
                if (_markupCache.TryGetValue(cacheKey, out string? cachedTranslation))
                {
                    AnsiConsole.MarkupLine(cachedTranslation);
                    return;
                }
                
                // Process and translate the text while protecting markup
                string translated = await TranslateMarkupTextAsync(markupText);
                
                // Cache the result
                _markupCache[cacheKey] = translated;
                
                // Display translated text
                AnsiConsole.MarkupLine(translated);
            }
            catch (Exception ex)
            {
                // Log or print the error if logging is available
                Console.WriteLine($"Error translating markup: {ex.Message}");
                // Fallback to original text if anything goes wrong
                AnsiConsole.MarkupLine(markupText);
            }
        }
        
        /// <summary>
        /// Translates and displays text with Spectre.Console Markup
        /// </summary>
        public static async Task MarkupAsync(string markupText)
        {
            try
            {
                // If no translation service or English target, just display original
                if (_translationService == null || _targetLanguage == "en")
                {
                    AnsiConsole.Markup(markupText);
                    return;
                }
                
                // Check cache first
                string cacheKey = $"{markupText}|{_targetLanguage}";
                if (_markupCache.TryGetValue(cacheKey, out string? cachedTranslation))
                {
                    AnsiConsole.Markup(cachedTranslation);
                    return;
                }
                
                // Process and translate the text while protecting markup
                string translated = await TranslateMarkupTextAsync(markupText);
                
                // Cache the result
                _markupCache[cacheKey] = translated;
                
                // Display translated text
                AnsiConsole.Markup(translated);
            }
            catch (Exception ex)
            {
                // Log or print the error if logging is available
                Console.WriteLine($"Error translating markup: {ex.Message}");
                // Fallback to original text if anything goes wrong
                AnsiConsole.Markup(markupText);
            }
        }
        
        /// <summary>
        /// Processes and translates markup text while preserving all markup tags intact
        /// </summary>
        private static async Task<string> TranslateMarkupTextAsync(string markupText)
        {
            try
            {
                // Special case: if the text contains Hindi style tag like '[बोल्ड]'
                // it will cause errors in Spectre.Console
                if (markupText.Contains("बोल्ड") || markupText.Contains("साहसिक"))
                {
                    // For Hindi text with known style issues, strip markup and just translate the content
                    string plainText = Regex.Replace(markupText, @"\[.*?\]", "");
                    return await _translationService.TranslateTextAsync(plainText, _targetLanguage, _sourceLanguage);
                }

                // This regex matches complete markup tags including brackets: [bold], [/], [yellow], etc.
                var regex = new Regex(@"(\[[\w\s\/#-]*\])");
                var matches = regex.Matches(markupText);
                
                if (matches.Count == 0)
                {
                    // No markup tags, just translate the whole text
                    return await _translationService.TranslateTextAsync(markupText, _targetLanguage, _sourceLanguage);
                }
                
                // Split into text segments and markup segments
                List<string> segments = new List<string>();
                List<bool> isMarkup = new List<bool>(); // true if segment is markup

                int lastPos = 0;

                foreach (Match match in matches)
                {
                    // Add text before this markup tag
                    if (match.Index > lastPos)
                    {
                        segments.Add(markupText.Substring(lastPos, match.Index - lastPos));
                        isMarkup.Add(false); // Not markup
                    }
                    
                    // Add the markup tag
                    segments.Add(match.Value);
                    isMarkup.Add(true); // Is markup
                    
                    lastPos = match.Index + match.Length;
                }
                
                // Add remaining text after last markup tag
                if (lastPos < markupText.Length)
                {
                    segments.Add(markupText.Substring(lastPos));
                    isMarkup.Add(false);
                }
                
                // Translate only non-markup segments
                List<string> translatedSegments = new List<string>();
                List<string> textsToTranslate = new List<string>();
                List<int> textIndices = new List<int>();
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (!isMarkup[i] && !string.IsNullOrWhiteSpace(segments[i]))
                    {
                        textsToTranslate.Add(segments[i]);
                        textIndices.Add(i);
                    }
                }
                
                // Batch translate all text segments
                var translatedTexts = await _translationService.TranslateTextBatchAsync(textsToTranslate, _targetLanguage, _sourceLanguage);
                
                // Build the final result with markup preserved
                string result = "";
                int translatedIndex = 0;

                for (int i = 0; i < segments.Count; i++)
                {
                    if (isMarkup[i])
                    {
                        // Keep markup as-is
                        result += segments[i];
                    }
                    else if (!string.IsNullOrWhiteSpace(segments[i]))
                    {
                        // Use translated text
                        result += translatedTexts[translatedIndex++];
                    }
                    else
                    {
                        // Keep empty/whitespace segments as-is
                        result += segments[i];
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during markup translation: {ex.Message}");
                // Fall back to just translating without markup
                string plainText = Regex.Replace(markupText, @"\[.*?\]", "");
                return await _translationService.TranslateTextAsync(plainText, _targetLanguage, _sourceLanguage);
            }
        }

        /// <summary>
        /// Safe version of MarkupLine that handles failed translation
        /// </summary>
        public static async Task SafeMarkupLineAsync(string markupText)
        {
            try
            {
                // For safety, extract the plain text without markup
                string plainText = Regex.Replace(markupText, @"\[.*?\]", "");
                
                // Just translate the plain text
                string translatedText = await TranslateAsync(plainText);
                
                // Display without any markup
                Console.WriteLine(translatedText);
            }
            catch (Exception ex)
            {
                // If all else fails, just write the original text
                Console.WriteLine(markupText);
            }
        }

        /// <summary>
        /// Special handling for titles and prompts that shouldn't have markup in translation
        /// </summary>
        public static async Task<string> TranslateMenuText(string text)
        {
            // Remove any markup for menu items
            string plainText = Regex.Replace(text, @"\[.*?\]", "");
            return await TranslateAsync(plainText); 
        }

        /// <summary>
        /// Translates and returns a list of strings
        /// </summary>
        public static async Task<List<string>> TranslateListAsync(List<string> texts)
        {
            if (_translationService == null || texts == null || texts.Count == 0 || _targetLanguage == "en")
                return texts?.ToList() ?? new List<string>();
                
            return await _translationService.TranslateTextBatchAsync(texts, _targetLanguage, _sourceLanguage);
        }
        
        /// <summary>
        /// Translates a prompt for SelectionPrompt and returns translated choices
        /// </summary>
        public static async Task<List<string>> TranslateChoicesAsync(string title, List<string> choices)
        {
            if (_translationService == null || _targetLanguage == "en")
                return choices;
                
            var translatedTitle = await TranslateAsync(title);
            var translatedChoices = await TranslateListAsync(choices);
            
            return translatedChoices;
        }
    }
}
