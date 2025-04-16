#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace VocabularyBank.Helpers
{
    /// <summary>
    /// Helper class for handling different writing scripts and character sets.
    /// </summary>
    public static class ScriptHelper
    {
        // Unicode ranges for different scripts
        private static readonly Dictionary<string, Tuple<int, int>[]> ScriptRanges = new Dictionary<string, Tuple<int, int>[]>
        {
            ["Latin"] = new[] { Tuple.Create(0x0020, 0x007F), Tuple.Create(0x00A0, 0x00FF) },
            ["Cyrillic"] = new[] { Tuple.Create(0x0400, 0x04FF) },
            ["Greek"] = new[] { Tuple.Create(0x0370, 0x03FF) },
            ["Arabic"] = new[] { Tuple.Create(0x0600, 0x06FF), Tuple.Create(0x0750, 0x077F) },
            ["Devanagari"] = new[] { Tuple.Create(0x0900, 0x097F) },
            ["Gujarati"] = new[] { Tuple.Create(0x0A80, 0x0AFF) },
            ["Thai"] = new[] { Tuple.Create(0x0E00, 0x0E7F) },
            ["CJK"] = new[] { Tuple.Create(0x4E00, 0x9FFF) },
            ["Hangul"] = new[] { Tuple.Create(0xAC00, 0xD7AF) }
        };

        /// <summary>
        /// Detects the primary script used in a text string.
        /// </summary>
        /// <param name="text">The text to analyze</param>
        /// <returns>The script name or "Unknown"</returns>
        public static string DetectScript(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "Unknown";
                
            // Count characters in each script range
            Dictionary<string, int> scriptCounts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, Tuple<int, int>[]> script in ScriptRanges)
            {
                scriptCounts[script.Key] = 0;
            }
            
            foreach (char c in text)
            {
                int codepoint = Convert.ToInt32(c);
                foreach (var script in ScriptRanges)
                {
                    foreach (var range in script.Value)
                    {
                        if (codepoint >= range.Item1 && codepoint <= range.Item2)
                        {
                            scriptCounts[script.Key]++;
                            break;
                        }
                    }
                }
            }
            
            // Determine the dominant script
            string dominantScript = scriptCounts.OrderByDescending(s => s.Value).First().Key;
            return scriptCounts[dominantScript] > 0 ? dominantScript : "Unknown";
        }
        
        /// <summary>
        /// Prepares text for AI processing, handling different scripts appropriately.
        /// </summary>
        /// <param name="text">The text to prepare</param>
        /// <returns>Text prepared for AI processing</returns>
        public static string PrepareTextForAI(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            string script = DetectScript(text);
            
            // Special handling for different scripts
            switch (script)
            {
                case "Gujarati":
                    // Add context for the AI model to understand this is Gujarati
                    return $"[Gujarati term: {text}]";
                
                case "Devanagari":
                    return $"[Devanagari term: {text}]";
                    
                case "Arabic":
                    return $"[Arabic term: {text}]";
                    
                case "CJK":
                    return $"[CJK term: {text}]";
                    
                default:
                    return text;
            }
        }
        
        /// <summary>
        /// Creates a safe AI prompt for handling non-Latin scripts.
        /// </summary>
        /// <param name="term">The term to define</param>
        /// <param name="contextText">The context where the term appears</param>
        /// <param name="detectedLanguage">The detected language of the text</param>
        /// <returns>A safe prompt for the AI</returns>
        public static string CreateSafePrompt(string term, string contextText, string detectedLanguage)
        {
            string script = DetectScript(term);
            StringBuilder prompt = new StringBuilder();
            
            prompt.AppendLine($"Define the following term that appears in {detectedLanguage} text:");
            
            if (script != "Latin")
            {
                prompt.AppendLine($"Term: '{term}' (written in {script} script)");
            }
            else 
            {
                prompt.AppendLine($"Term: '{term}'");
            }
            
            prompt.AppendLine($"Context: {contextText}");
            
            prompt.AppendLine("Return a JSON object with these fields:");
            prompt.AppendLine("- definition: A clear definition of the term");
            prompt.AppendLine("- example: An example sentence using the term");
            prompt.AppendLine("- context: Brief explanation of the term in this specific context");
            
            return prompt.ToString();
        }
        
        /// <summary>
        /// Transliterates text from non-Latin scripts to Latin when appropriate.
        /// </summary>
        /// <param name="text">The text to transliterate</param>
        /// <param name="script">The script of the original text</param>
        /// <returns>Transliterated text or original if transliteration not available</returns>
        public static string TransliterateToLatin(string text, string? script = null)
        {
            // Use the provided script or detect it
            script = script ?? DetectScript(text);
            
            // Simple transliteration for demonstration - in a real app would use a proper transliteration library
            // This is just a placeholder to show the concept
            if (script == "Gujarati" || script == "Devanagari")
            {
                // In a real application, we would implement proper transliteration
                // For now, just append a note that this would be transliterated
                return $"{text} [transliterated form would appear here]";
            }
            
            // For scripts we don't handle, return the original
            return text;
        }
    }
}
