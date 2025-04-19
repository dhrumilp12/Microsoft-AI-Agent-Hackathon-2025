using System;
using System.IO;
using System.Threading.Tasks;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for loading and processing transcript files.
    /// </summary>
    public class TranscriptProcessorService : ITranscriptProcessorService
    {
        /// <summary>
        /// Loads a transcript file from disk asynchronously.
        /// </summary>
        /// <param name="filePath">The full path to the transcript file</param>
        /// <returns>The content of the transcript file as a string</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
        public async Task<string> LoadTranscriptAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
                
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Transcript file not found.", filePath);
                
            return await File.ReadAllTextAsync(filePath);
        }
    }
}
