#nullable enable
namespace VocabularyBank.Models
{
    /// <summary>
    /// Represents content that has been translated, including both the original and translated text.
    /// </summary>
    public class TranslatedContent
    {
        /// <summary>
        /// The original text before translation.
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;
        
        /// <summary>
        /// The language code of the original text.
        /// </summary>
        public string OriginalLanguage { get; set; } = string.Empty;
        
        /// <summary>
        /// The translated text.
        /// </summary>
        public string TranslatedText { get; set; } = string.Empty;
        
        /// <summary>
        /// The target language code the text was translated to.
        /// </summary>
        public string TargetLanguage { get; set; } = string.Empty;
        
        /// <summary>
        /// The display name of the target language.
        /// </summary>
        public string TargetLanguageDisplayName { get; set; } = string.Empty;
    }
}
