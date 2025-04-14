using System.Collections.Generic;
using System.Threading.Tasks;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that provide translation functionality.
    /// </summary>
    public interface ITranslationService
    {
        /// <summary>
        /// Translates text to the specified target language.
        /// </summary>
        /// <param name="text">The text to be translated</param>
        /// <param name="targetLanguage">The language code to translate to</param>
        /// <returns>The translated text</returns>
        Task<string> TranslateTextAsync(string text, string targetLanguage);
        
        /// <summary>
        /// Gets available language options for translation.
        /// </summary>
        /// <returns>A dictionary of language codes and their display names</returns>
        Task<Dictionary<string, string>> GetAvailableLanguagesAsync();
        
        /// <summary>
        /// Detects the language of a text.
        /// </summary>
        /// <param name="text">The text to analyze</param>
        /// <returns>The detected language code</returns>
        Task<string> DetectLanguageAsync(string text);
    }
}
