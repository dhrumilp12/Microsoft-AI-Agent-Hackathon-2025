using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that extract vocabulary terms from text.
    /// Implementations should identify important academic or technical terms
    /// from educational content.
    /// </summary>
    public interface IVocabularyExtractorService
    {
        /// <summary>
        /// Extracts key vocabulary terms from the provided transcript text.
        /// </summary>
        /// <param name="transcript">The educational content to analyze</param>
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>A list of relevant vocabulary terms found in the content</returns>
        Task<List<string>> ExtractVocabularyAsync(
            string transcript, 
            Action<int, string> progressCallback = null);
    }
}
