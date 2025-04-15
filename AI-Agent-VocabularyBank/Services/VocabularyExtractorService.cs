using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for extracting key vocabulary terms from a text transcript.
    /// Uses natural language processing techniques and Azure OpenAI to identify important terms.
    /// </summary>
    public class VocabularyExtractorService : IVocabularyExtractorService
    {
        private readonly IConfiguration _configuration;
        private readonly AzureOpenAIService _openAIService;
        private readonly List<string> _commonWords;
        
        /// <summary>
        /// Initializes a new instance of the VocabularyExtractorService.
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="openAIService">Service for making Azure OpenAI API calls</param>
        public VocabularyExtractorService(IConfiguration configuration, AzureOpenAIService openAIService)
        {
            _configuration = configuration;
            _openAIService = openAIService;
            
            // Load common words to filter out
            _commonWords = LoadCommonWords();
        }
        
        /// <summary>
        /// Extracts vocabulary terms from a provided transcript.
        /// </summary>
        /// <param name="transcript">The text transcript to analyze</param>
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>A list of key vocabulary terms</returns>
        public async Task<List<string>> ExtractVocabularyAsync(
            string transcript, 
            Action<int, string> progressCallback = null)
        {
            // Report initial progress
            progressCallback?.Invoke(0, "Starting vocabulary extraction...");
            
            // First extract potential key terms using basic NLP techniques
            progressCallback?.Invoke(10, "Identifying potential terms...");
            var basicTerms = ExtractBasicTerms(transcript);
            Console.WriteLine($"Initial extraction found {basicTerms.Count} potential terms");
            
            // Update progress
            progressCallback?.Invoke(40, $"Found {basicTerms.Count} potential terms");
            
            // Then use Azure OpenAI to refine the list to the most relevant terms
            progressCallback?.Invoke(50, "Refining terms using AI...");
            Console.WriteLine("Refining terms using AI...");
            
            // Get refined terms
            var refinedTerms = await RefineTermsWithAzureOpenAIAsync(basicTerms, transcript);
            
            // Report completion
            progressCallback?.Invoke(100, $"Extracted {refinedTerms.Count} key vocabulary terms");
            
            return refinedTerms;
        }
        
        /// <summary>
        /// Performs basic term extraction using frequency analysis and filtering.
        /// </summary>
        /// <param name="transcript">The text transcript to analyze</param>
        /// <returns>A list of potential vocabulary terms</returns>
        private List<string> ExtractBasicTerms(string transcript)
        {
            // Tokenize the text and filter words
            var words = Regex.Split(transcript.ToLower(), @"\W+")
                .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length > 3) // Filter out short words
                .Where(w => !_commonWords.Contains(w))                     // Filter out common words
                .GroupBy(w => w)                                           // Group by word
                .Select(g => new { Word = g.Key, Count = g.Count() })      // Count occurrences
                .Where(item => item.Count >= 2)                            // Only words that appear multiple times
                .OrderByDescending(item => item.Count)                     // Sort by frequency
                .Take(50)                                                  // Take top 50 candidate terms
                .Select(item => item.Word)
                .ToList();
                
            return words;
        }
        
        /// <summary>
        /// Uses Azure OpenAI to refine the list of vocabulary terms to the most relevant ones.
        /// </summary>
        /// <param name="candidateTerms">List of potential vocabulary terms</param>
        /// <param name="transcript">The original transcript for context</param>
        /// <returns>Refined list of vocabulary terms</returns>
        private async Task<List<string>> RefineTermsWithAzureOpenAIAsync(List<string> candidateTerms, string transcript)
        {
            // Create a more focused prompt for Azure OpenAI
            string subject = DetermineSubject(transcript);
            string prompt = $@"Based on the following transcript about {subject}, identify exactly 20 most important technical or domain-specific terms.
            
Here are some candidate terms I've extracted: {string.Join(", ", candidateTerms)}.

IMPORTANT: Return ONLY the terms as a comma-separated list, without any additional text or explanations. 
Do not number the terms or add any other formatting.
If you cannot identify 20 terms, return as many as you can find.

Example of expected format:
term1, term2, term3, term4, term5, term6...";
            
            // Call Azure OpenAI using the Chat Completions API
            string content = await _openAIService.GetCompletionAsync(prompt, 0.3, 1000);
            
            // Check if the content starts with an error message
            if (content.StartsWith("Error:"))
            {
                Console.WriteLine(content);
                // Fall back to basic terms if we encounter an error
                return candidateTerms.Take(20).ToList();
            }
            
            // Parse and return the refined terms
            var terms = content.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            
            if (terms.Count == 0)
            {
                Console.WriteLine("Warning: No terms were extracted from the API response. Falling back to basic terms.");
                return candidateTerms.Take(20).ToList();
            }
            
            return terms;
        }
        
        /// <summary>
        /// Attempts to determine the subject of the transcript to provide better context for the AI.
        /// </summary>
        /// <param name="transcript">The text transcript to analyze</param>
        /// <returns>The determined subject or a default value</returns>
        private string DetermineSubject(string transcript)
        {
            // Simple heuristic to determine subject - in a real app this could be more sophisticated
            string[] firstParagraphs = transcript.Split('.', StringSplitOptions.RemoveEmptyEntries).Take(3).ToArray();
            string sampleText = string.Join(". ", firstParagraphs);
            
            // For a real application, this could call another LLM prompt to determine the subject
            return "the given subject";
        }
        
        /// <summary>
        /// Loads a list of common words to filter out from vocabulary extraction.
        /// </summary>
        /// <returns>List of common words</returns>
        private List<string> LoadCommonWords()
        {
            // In a production environment, this would load from a file or database
            // For simplicity, we include a small subset directly in the code
            return new List<string> 
            { 
                "the", "and", "that", "have", "for", "not", "with", "you", "this", 
                "but", "his", "from", "they", "say", "she", "will", "one", "all", 
                "would", "there", "their", "what", "about", "who", "get", "which", 
                "when", "make", "like", "time", "just", "know", "take", "people", 
                "year", "your", "good", "some", "could", "them", "see", "other", "than",
                "then", "now", "look", "only", "come", "over", "think", "also", "back" 
            };
        }
    }
}
