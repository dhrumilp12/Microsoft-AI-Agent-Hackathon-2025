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
        /// <returns>A list of flashcards ready for export or study</returns>
        public List<Flashcard> CreateFlashcards(List<VocabularyTerm> terms)
        {
            return terms.Select(term => new Flashcard
            {
                Term = term.Term,
                Definition = term.Definition,
                Example = term.Example,
                Context = term.Context
            }).ToList();
        }
    }
}
