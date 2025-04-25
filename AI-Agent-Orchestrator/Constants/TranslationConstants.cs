using System.Collections.Generic;

namespace AI_Agent_Orchestrator.Services
{
    /// <summary>
    /// Contains constants and common values used in translation-related services
    /// </summary>
    public static class TranslationConstants
    {
        /// <summary>
        /// Default Azure Translator endpoint
        /// </summary>
        public const string DefaultTranslatorEndpoint = "https://api.cognitive.microsofttranslator.com/";
        
        /// <summary>
        /// Returns a default set of common languages when Azure services are unavailable
        /// </summary>
        public static Dictionary<string, string> GetDefaultLanguages()
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
        
        /// <summary>
        /// List of valid Spectre.Console style names that should never be translated
        /// </summary>
        public static readonly HashSet<string> ValidStyleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Colors
            "black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
            "brightblack", "brightred", "brightgreen", "brightyellow", "brightblue", 
            "brightmagenta", "brightcyan", "brightwhite",
            // Styles
            "bold", "dim", "italic", "underline", "strikethrough", "reverse", "blink", "invisible",
            // Directions
            "left", "center", "right", "justify",
            // Common variations
            "default", "none", "normal", "reset"
        };
    }
}
