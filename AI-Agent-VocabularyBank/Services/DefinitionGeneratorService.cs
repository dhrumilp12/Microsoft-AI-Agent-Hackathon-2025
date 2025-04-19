using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VocabularyBank.Models;
using VocabularyBank.Helpers;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for generating definitions for vocabulary terms.
    /// Uses Azure OpenAI to generate definitions, examples, and context information.
    /// </summary>
    public class DefinitionGeneratorService : IDefinitionGeneratorService
    {
        private readonly AzureOpenAIService _openAIService;
        private readonly ITranslationService _translationService;
        
        /// <summary>
        /// Initializes a new instance of DefinitionGeneratorService.
        /// </summary>
        /// <param name="openAIService">Service for making Azure OpenAI API calls</param>
        /// <param name="translationService">Service for translation functionality</param>
        public DefinitionGeneratorService(AzureOpenAIService openAIService, ITranslationService translationService)
        {
            _openAIService = openAIService;
            _translationService = translationService;
        }
        
        /// <summary>
        /// Generates definitions for a list of vocabulary terms.
        /// </summary>
        /// <param name="terms">List of terms to define</param>
        /// <param name="contextText">Original text for context</param>
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>List of vocabulary terms with their definitions</returns>
        public async Task<List<VocabularyTerm>> GenerateDefinitionsAsync(
            List<string> terms, 
            string contextText,
            Action<int, string> progressCallback = null)
        {
            var results = new List<VocabularyTerm>();
            
            // Detect script and language of context
            string script = ScriptHelper.DetectScript(contextText);
            string detectedLanguage = await _translationService.DetectLanguageAsync(contextText);
            
            // Process terms in batches to avoid rate limits
            int batchSize = 5;
            int totalTerms = terms.Count;
            int completedTerms = 0;
            
            // Report initial progress
            progressCallback?.Invoke(0, $"Generating definitions for {totalTerms} terms...");
            
            for (int i = 0; i < terms.Count; i += batchSize)
            {
                // Get the current batch
                var batch = terms.Skip(i).Take(batchSize).ToList();
                
                // Process each term in the batch
                var batchTasks = batch.Select(term => GenerateDefinitionForTermAsync(term, contextText, script, detectedLanguage));
                var batchResults = await Task.WhenAll(batchTasks);
                
                // Add the results to our collection
                results.AddRange(batchResults);
                
                // Update completion count and report progress
                completedTerms += batch.Count;
                int percentComplete = (int)((double)completedTerms / totalTerms * 100);
                progressCallback?.Invoke(
                    percentComplete, 
                    $"Processed {completedTerms}/{totalTerms} terms ({percentComplete}%)"
                );
                
                // Add delay to avoid rate limiting for subsequent batches
                if (i + batchSize < terms.Count)
                    await Task.Delay(1000);
            }
            
            // Report completion
            progressCallback?.Invoke(100, $"Completed all {totalTerms} definitions");
            
            return results;
        }
        
        /// <summary>
        /// Generates a definition for a single vocabulary term.
        /// </summary>
        /// <param name="term">The term to define</param>
        /// <param name="contextText">Original text for context</param>
        /// <param name="script">The script of the text</param>
        /// <param name="detectedLanguage">The detected language</param>
        /// <returns>A vocabulary term with definition and related information</returns>
        private async Task<VocabularyTerm> GenerateDefinitionForTermAsync(
            string term, 
            string contextText, 
            string script, 
            string detectedLanguage)
        {
            try 
            {
                // Create a shortened context (first 300 chars + portion around the term)
                string shortenedContext = CreateShortenedContext(contextText, term, 300);
                
                // Handle scripts that may cause issues with the AI
                string termForPrompt = ScriptHelper.PrepareTextForAI(term);
                bool isNonLatin = script != "Latin" && script != "Unknown";
                
                // Create an appropriate prompt based on the script
                string prompt;
                
                if (isNonLatin)
                {
                    // Use the safe prompt generator for non-Latin scripts
                    prompt = ScriptHelper.CreateSafePrompt(term, shortenedContext, detectedLanguage);
                }
                else
                {
                    // For Latin scripts, use the standard prompt
                    prompt = $@"Define the term '{term}' in the context of the following text snippet:
    
Context: {shortenedContext}

Return ONLY a valid JSON with these exact fields:
{{
  ""definition"": ""clear definition of {term}"",
  ""example"": ""a usage example sentence for {term}"",
  ""context"": ""brief description of {term} in this specific context""
}}

Be concise. No need for additional text or explanation.";
                }
                
                // Call the OpenAI service
                string content = await _openAIService.GetCompletionAsync(prompt, 0.5, 1000);
                
                // Check if the content starts with an error message
                if (content.StartsWith("Error:"))
                {
                    // For non-Latin scripts that fail, try a fallback approach
                    if (isNonLatin)
                    {
                        Console.WriteLine($"Attempting alternative approach for non-Latin term '{term}'...");
                        return await GenerateDefinitionWithTransliterationAsync(term, contextText, script, detectedLanguage);
                    }
                    
                    return CreateFallbackTerm(term, "Definition could not be generated due to token limit", contextText);
                }
                
                // Try to clean the JSON response - sometimes AI adds extra text
                string jsonContent = ExtractJsonFromResponse(content);
                
                // Parse the JSON response
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;
                    
                    return new VocabularyTerm
                    {
                        Term = term,
                        Definition = root.GetProperty("definition").GetString() ?? "[Definition not available]",
                        Example = root.GetProperty("example").GetString() ?? "[Example not available]",
                        Context = root.GetProperty("context").GetString() ?? "[Context not available]",
                        Occurrences = CountOccurrences(contextText, term)
                    };
                }
            }
            catch (JsonException ex)
            {
                // If JSON parsing fails for non-Latin scripts, try the fallback approach
                if (script != "Latin" && script != "Unknown")
                {
                    return await GenerateDefinitionWithTransliterationAsync(term, contextText, script, detectedLanguage);
                }
                
                // Fallback if JSON parsing fails
                return CreateFallbackTerm(term, "Definition could not be generated - JSON parsing error", contextText);
            }
            catch (Exception ex)
            {
                return CreateFallbackTerm(term, $"Definition could not be generated - {ex.GetType().Name}", contextText);
            }
        }
        
        /// <summary>
        /// Fallback method that attempts to generate a definition using transliteration
        /// </summary>
        private async Task<VocabularyTerm> GenerateDefinitionWithTransliterationAsync(
            string term, 
            string contextText, 
            string script, 
            string detectedLanguage)
        {
            try
            {
                Console.WriteLine($"Using transliteration approach for term: {term}");
                
                // Try to get an English translation of the context
                string translatedContext;
                if (detectedLanguage != "en")
                {
                    Console.WriteLine("Translating context to English for better term definition...");
                    translatedContext = await _translationService.TranslateTextAsync(
                        CreateShortenedContext(contextText, term, 300), 
                        "en");
                }
                else
                {
                    translatedContext = CreateShortenedContext(contextText, term, 300);
                }
                
                // Create a more basic prompt that focuses on explaining the term as is
                string prompt = $@"The following term appears in {detectedLanguage} text: '{term}'

Instead of defining the term directly, please explain:
1. What this term likely means in {detectedLanguage}
2. How it's used in context
3. A simple example of usage

Context where the term appears: {translatedContext}

Return your response as JSON with these fields:
{{
  ""definition"": ""explanation of what the term means"",
  ""example"": ""example usage (can be in English)"",
  ""context"": ""how this term is used in the provided context""
}}";

                // Get a response that explains the term rather than defines it directly
                string content = await _openAIService.GetCompletionAsync(prompt, 0.5, 1000);
                
                if (content.StartsWith("Error:"))
                {
                    Console.WriteLine("Transliteration approach also failed. Using fallback term.");
                    return CreateFallbackTerm(term, "Definition could not be generated due to language processing limits", contextText);
                }
                
                // Try to clean the JSON response
                string jsonContent = ExtractJsonFromResponse(content);
                
                // Parse the JSON response
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;
                    
                    return new VocabularyTerm
                    {
                        Term = term,
                        Definition = root.GetProperty("definition").GetString() ?? $"[Term in {script} script]",
                        Example = root.GetProperty("example").GetString() ?? $"[Example not available]",
                        Context = root.GetProperty("context").GetString() ?? $"[Context for {detectedLanguage} term]",
                        Occurrences = CountOccurrences(contextText, term)
                    };
                }
            }
            catch (Exception ex)
            {
                return CreateFallbackTerm(term, $"Definition for '{term}' could not be generated", contextText);
            }
        }

        /// <summary>
        /// Extracts valid JSON from a response that might contain extra text
        /// </summary>
        private string ExtractJsonFromResponse(string content)
        {
            // Look for the first { and last } to extract JSON
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            
            if (start >= 0 && end >= 0 && end > start)
            {
                return content.Substring(start, end - start + 1);
            }
            
            // If we can't find valid JSON markers, return the original content
            return content;
        }
        
        /// <summary>
        /// Creates a fallback vocabulary term when definition generation fails.
        /// </summary>
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
