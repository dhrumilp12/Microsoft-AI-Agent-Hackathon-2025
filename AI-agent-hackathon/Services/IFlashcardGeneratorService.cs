using System.Collections.Generic;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that generate flashcards from vocabulary terms.
    /// </summary>
    public interface IFlashcardGeneratorService
    {
        /// <summary>
        /// Creates flashcards from vocabulary terms with their definitions.
        /// </summary>
        /// <param name="terms">The vocabulary terms with definitions and examples</param>
        /// <returns>A list of flashcards ready for study</returns>
        List<Flashcard> CreateFlashcards(List<VocabularyTerm> terms);
    }
}
