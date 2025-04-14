using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VocabularyBank.Models;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Interface for services that generate definitions for vocabulary terms.
    /// </summary>
    public interface IDefinitionGeneratorService
    {
        /// <summary>
        /// Generates definitions, examples, and context information for vocabulary terms.
        /// </summary>
        /// <param name="terms">The list of vocabulary terms to define</param>
        /// <param name="contextText">The source text to provide context for the definitions</param>
        /// <param name="progressCallback">Optional callback to report progress</param>
        /// <returns>A list of vocabulary terms with their definitions and related information</returns>
        Task<List<VocabularyTerm>> GenerateDefinitionsAsync(
            List<string> terms, 
            string contextText, 
            Action<int, string> progressCallback = null);
    }
}
