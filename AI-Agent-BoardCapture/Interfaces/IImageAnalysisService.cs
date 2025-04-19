using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Interface for image analysis services
    /// </summary>
    public interface IImageAnalysisService
    {
        /// <summary>
        /// Analyzes image content for objects, categories, and descriptions
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="sourceLanguage">Source language code</param>
        /// <param name="targetLanguage">Target language code for translation</param>
        /// <returns>Task representing the analysis operation</returns>
        Task AnalyzeImageContentAsync(string imagePath, string sourceLanguage, string targetLanguage);
    }
}