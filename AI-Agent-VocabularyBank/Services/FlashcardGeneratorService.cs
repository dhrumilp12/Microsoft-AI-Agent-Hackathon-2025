using System;
using System.Collections.Generic;
using System.Linq;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for creating flashcards from vocabulary terms.
    /// </summary>
    public class FlashcardGeneratorService : IFlashcardGeneratorService
    {
        /// <summary>
        /// Converts vocabulary terms with their definitions into flashcards for studying.
        /// </summary>
        /// <param name="terms">The list of vocabulary terms with definitions</param>
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>A list of flashcards ready for export or study</returns>
        public List<Flashcard> CreateFlashcards(
            List<VocabularyTerm> terms,
            Action<int, string> progressCallback = null)
        {
            var flashcards = new List<Flashcard>();
            int totalTerms = terms.Count;
            
            // Report initial progress
            progressCallback?.Invoke(0, $"Creating {totalTerms} flashcards...");
            
            for (int i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                
                flashcards.Add(new Flashcard
                {
                    Term = term.Term,
                    Definition = term.Definition,
                    Example = term.Example,
                    Context = term.Context
                });
                
                // Report progress
                int percentComplete = (int)((double)(i + 1) / totalTerms * 100);
                progressCallback?.Invoke(
                    percentComplete, 
                    $"Created {i + 1}/{totalTerms} flashcards ({percentComplete}%)"
                );
                
                // Add a small delay for visual effect
                if (i < terms.Count - 1 && totalTerms > 20)
                    System.Threading.Thread.Sleep(50);
            }
            
            // Report completion
            progressCallback?.Invoke(100, $"Created all {totalTerms} flashcards");
            
            return flashcards;
        }
    }
}
