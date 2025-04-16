using System;
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
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>A list of flashcards ready for study</returns>
        List<Flashcard> CreateFlashcards(
            List<VocabularyTerm> terms,
            Action<int, string> progressCallback = null);
    }
}
