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
        string ExportAsJson(List<Flashcard> flashcards);
        
        /// <summary>
        /// Exports flashcards to CSV format.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <returns>The CSV representation as a string</returns>
        string ExportAsCsv(List<Flashcard> flashcards);
        
        /// <summary>
        /// Exports flashcards to HTML format.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <returns>The HTML representation as a string</returns>
        string ExportAsHtml(List<Flashcard> flashcards);
        
        /// <summary>
        /// Exports flashcards to Microsoft 365 Learning Management System.
        /// </summary>
        /// <param name="flashcards">List of flashcards to export</param>
        /// <param name="userEmail">Email address of the user to share with</param>
        /// <returns>URL to the exported resource in M365</returns>
        Task<string> ExportToM365Async(List<Flashcard> flashcards, string userEmail);
        
        /// <summary>
        /// Checks if M365 export capability is configured.
        /// </summary>
        /// <returns>True if M365 export is available, false otherwise</returns>
        bool IsM365ExportAvailable();

        /// <summary>
        /// Exports both original and translated flashcards to a file.
        /// </summary>
        /// <param name="originalFlashcards">List of original flashcards</param>
        /// <param name="translatedFlashcards">List of translated flashcards</param>
        /// <param name="translatedLanguage">Name of the translated language</param>
        /// <param name="outputPath">Path where file will be saved</param>
        Task ExportCombinedFlashcardsAsync(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage, 
            string outputPath);
            
        /// <summary>
        /// Converts original and translated flashcards to a combined JSON format.
        /// </summary>
        /// <param name="originalFlashcards">List of original flashcards</param>
        /// <param name="translatedFlashcards">List of translated flashcards</param>
        /// <param name="translatedLanguage">Name of the translated language</param>
        /// <returns>The combined JSON representation as a string</returns>
        string ExportCombinedAsJson(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage);
            
        /// <summary>
        /// Converts original and translated flashcards to a combined CSV format.
        /// </summary>
        /// <param name="originalFlashcards">List of original flashcards</param>
        /// <param name="translatedFlashcards">List of translated flashcards</param>
        /// <param name="translatedLanguage">Name of the translated language</param>
        /// <returns>The combined CSV representation as a string</returns>
        string ExportCombinedAsCsv(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage);
            
        /// <summary>
        /// Converts original and translated flashcards to a combined HTML format.
        /// </summary>
        /// <param name="originalFlashcards">List of original flashcards</param>
        /// <param name="translatedFlashcards">List of translated flashcards</param>
        /// <param name="translatedLanguage">Name of the translated language</param>
        /// <returns>The combined HTML representation as a string</returns>
        string ExportCombinedAsHtml(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage);
    }
}
