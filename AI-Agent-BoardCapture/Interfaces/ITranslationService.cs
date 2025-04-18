using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Interface for text translation services
    /// </summary>
    public interface ITranslationService
    {
        /// <summary>
        /// Translates text from one language to another
        /// </summary>
        /// <param name="text">Text to translate</param>
        /// <param name="sourceLanguage">Source language code</param>
        /// <param name="targetLanguage">Target language code</param>
        /// <returns>Translated text or empty string if translation failed</returns>
        Task<string> TranslateTextAsync(string text, string sourceLanguage, string targetLanguage);
    }
}