using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for generating definitions for vocabulary terms.
    /// Uses Azure OpenAI to generate definitions, examples, and context information.
    /// </summary>
    public class DefinitionGeneratorService : IDefinitionGeneratorService
    {
        private readonly AzureOpenAIService _openAIService;
        
        /// <summary>
        /// Initializes a new instance of DefinitionGeneratorService.
        /// </summary>
        /// <param name="openAIService">Service for making Azure OpenAI API calls</param>
        public DefinitionGeneratorService(AzureOpenAIService openAIService)
        {
            _openAIService = openAIService;
        }
        
        /// <summary>
        /// Generates definitions for a list of vocabulary terms.
        /// </summary>
        /// <param name="terms">List of terms to define</param>
        /// <param name="contextText">Original text for context</param>
        /// <returns>List of vocabulary terms with their definitions</returns>
        public async Task<List<VocabularyTerm>> GenerateDefinitionsAsync(List<string> terms, string contextText)
        {
            var results = new List<VocabularyTerm>();
            
            // Process terms in batches to avoid rate limits
            int batchSize = 5;
            for (int i = 0; i < terms.Count; i += batchSize)
            {
                var batch = terms.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(term => GenerateDefinitionForTermAsync(term, contextText));
                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);
                
                // Add delay to avoid rate limiting
                if (i + batchSize < terms.Count)
                    await Task.Delay(2000);
            }
            
            return results;
        }
        
        /// <summary>
        /// Generates a definition for a single vocabulary term.
        /// </summary>
        /// <param name="term">The term to define</param>
        /// <param name="contextText">Original text for context</param>
        /// <returns>A vocabulary term with definition and related information</returns>
        private async Task<VocabularyTerm> GenerateDefinitionForTermAsync(string term, string contextText)
        {
            // Create a shortened context (first 300 chars + portion around the term)
            string shortenedContext = CreateShortenedContext(contextText, term, 300);
            
            // Create a prompt for the AI to generate a definition
            string prompt = $@"Define the term '{term}' in the context of the following text snippet:
    
Context: {shortenedContext}

Return ONLY a valid JSON with these exact fields:
{{
  ""definition"": ""clear definition of {term}"",
  ""example"": ""a usage example sentence for {term}"",
  ""context"": ""brief description of {term} in this specific context""
}}

Be concise. No need for additional text or explanation.";
            
            // Call the OpenAI service
            string content = await _openAIService.GetCompletionAsync(prompt, 0.5, 1000);
            
            try
            {
                // Check if the content starts with an error message
                if (content.StartsWith("Error:"))
                {
                    Console.WriteLine(content);
                    return CreateFallbackTerm(term, "Definition could not be generated due to token limit", contextText);
                }
                
                // Parse the JSON response
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    var root = doc.RootElement;
                    
                    return new VocabularyTerm
                    {
                        Term = term,
                        Definition = root.GetProperty("definition").GetString(),
                        Example = root.GetProperty("example").GetString(),
                        Context = root.GetProperty("context").GetString(),
                        Occurrences = CountOccurrences(contextText, term)
                    };
                }
            }
            catch (JsonException)
            {
                // Fallback if JSON parsing fails
                return CreateFallbackTerm(term, "Definition could not be generated", contextText);
            }
        }
        
        /// <summary>
        /// Creates a fallback vocabulary term when definition generation fails.
        /// </summary>
        /// <param name="term">The term</param>
        /// <param name="errorMessage">Message explaining the failure</param>
        /// <param name="contextText">Original text for context</param>
        /// <returns>A vocabulary term with placeholder information</returns>
        private VocabularyTerm CreateFallbackTerm(string term, string errorMessage, string contextText)
        {
            return new VocabularyTerm
            {
                Term = term,
                Definition = errorMessage,
                Example = "Example could not be generated",
                Context = "Context information not available",
                Occurrences = CountOccurrences(contextText, term)
            };
        }
        
        /// <summary>
        /// Creates a shortened context containing the first part of text and an example usage.
        /// </summary>
        /// <param name="text">The full text</param>
        /// <param name="term">The term to find in the text</param>
        /// <param name="maxLength">Maximum length of the first part</param>
        /// <returns>A shortened context suitable for AI prompting</returns>
        private string CreateShortenedContext(string text, string term, int maxLength)
        {
            // Get first part of text
            string firstPart = text.Length <= maxLength ? text : text.Substring(0, maxLength);
            
            // Find an example sentence containing the term
            int termIndex = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (termIndex >= 0)
            {
                int sentenceStart = text.LastIndexOf('.', termIndex) + 1;
                if (sentenceStart < 0) sentenceStart = Math.Max(0, termIndex - 100);
                
                int sentenceEnd = text.IndexOf('.', termIndex);
                if (sentenceEnd < 0) sentenceEnd = Math.Min(text.Length, termIndex + 100);
                
                string contextSentence = text.Substring(sentenceStart, sentenceEnd - sentenceStart).Trim();
                
                return $"{firstPart}... Example usage: \"{contextSentence}\"";
            }
            
            return firstPart;
        }
        
        /// <summary>
        /// Counts how many times a term appears in the text.
        /// </summary>
        /// <param name="text">The text to search</param>
        /// <param name="term">The term to count</param>
        /// <returns>Number of occurrences of the term in the text</returns>
        private int CountOccurrences(string text, string term)
        {
            int count = 0;
            int index = 0;
            
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += term.Length;
            }
            
            return count;
        }
    }
}
