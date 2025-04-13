using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for exporting flashcards to various formats.
    /// </summary>
    public class ExportService : IExportService
    {
        /// <summary>
        /// Exports flashcards to a file in the specified format.
        /// </summary>
        /// <param name="flashcards">The flashcards to export</param>
        /// <param name="outputPath">The path where the file should be saved</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task ExportFlashcardsAsync(List<Flashcard> flashcards, string outputPath)
        {
            string extension = Path.GetExtension(outputPath).ToLower();
            string content;
            
            if (extension == ".csv")
            {
                content = await ExportAsCsv(flashcards);
                Console.WriteLine("Exporting flashcards in CSV format...");
            }
            else
            {
                // Default to JSON
                content = await ExportAsJson(flashcards);
                Console.WriteLine("Exporting flashcards in JSON format...");
                
                // If no extension was provided, append .json
                if (string.IsNullOrEmpty(extension))
                {
                    outputPath += ".json";
                }
            }
            
            // Make sure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            // Write the content to the file
            await File.WriteAllTextAsync(outputPath, content);
        }
        
        /// <summary>
        /// Exports the flashcards as JSON.
        /// </summary>
        /// <param name="flashcards">The flashcards to export</param>
        /// <returns>JSON string representation of the flashcards</returns>
        public async Task<string> ExportAsJson(List<Flashcard> flashcards)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            return await Task.FromResult(JsonSerializer.Serialize(flashcards, options));
        }
        
        /// <summary>
        /// Exports the flashcards as CSV.
        /// </summary>
        /// <param name="flashcards">The flashcards to export</param>
        /// <returns>CSV string representation of the flashcards</returns>
        public async Task<string> ExportAsCsv(List<Flashcard> flashcards)
        {
            var sb = new StringBuilder();
            
            // Add CSV header
            sb.AppendLine("Term,Definition,Example,Context,CreatedDate");
            
            foreach (var card in flashcards)
            {
                sb.AppendLine(
                    $"\"{EscapeCsvField(card.Term)}\"," +
                    $"\"{EscapeCsvField(card.Definition)}\"," +
                    $"\"{EscapeCsvField(card.Example)}\"," +
                    $"\"{EscapeCsvField(card.Context)}\"," +
                    $"\"{card.CreatedDate:yyyy-MM-dd}\"");
            }
            
            return await Task.FromResult(sb.ToString());
        }
        
        /// <summary>
        /// Escapes special characters in CSV fields.
        /// </summary>
        /// <param name="field">The field to escape</param>
        /// <returns>Escaped field value</returns>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
                
            return field.Replace("\"", "\"\"");
        }
    }
}
