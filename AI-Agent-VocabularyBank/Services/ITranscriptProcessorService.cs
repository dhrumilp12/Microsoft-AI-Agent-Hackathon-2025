using System.Threading.Tasks;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that process transcript files.
    /// </summary>
    public interface ITranscriptProcessorService
    {
        /// <summary>
        /// Loads a transcript file from the specified path.
        /// </summary>
        /// <param name="filePath">Path to the transcript file</param>
        /// <returns>The content of the transcript file as a string</returns>
        Task<string> LoadTranscriptAsync(string filePath);
    }
}
