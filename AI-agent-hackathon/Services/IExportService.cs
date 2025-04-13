using System.Collections.Generic;
using System.Threading.Tasks;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that export flashcards to various formats.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Exports flashcards to a file in an appropriate format based on file extension.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <param name="outputPath">Path where file will be saved</param>
        Task ExportFlashcardsAsync(List<Flashcard> flashcards, string outputPath);
        
        /// <summary>
        /// Exports flashcards to JSON format.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <returns>The JSON representation as a string</returns>
        Task<string> ExportAsJson(List<Flashcard> flashcards);
        
        /// <summary>
        /// Exports flashcards to CSV format.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <returns>The CSV representation as a string</returns>
        Task<string> ExportAsCsv(List<Flashcard> flashcards);
    }
}
