using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Interface for OCR text extraction services
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
        /// Extracts text from an image using OCR
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="language">Language code for OCR (e.g. 'en')</param>
        /// <returns>Extracted text or empty string if no text was found</returns>
        Task<string> ExtractTextAsync(string imagePath, string language);
        
        /// <summary>
        /// Extracts text using local Tesseract OCR
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="language">Tesseract language code (e.g. 'eng')</param>
        /// <param name="tessDataPath">Path to Tesseract data files</param>
        /// <returns>Extracted text or empty string if no text was found</returns>
        string ExtractTextUsingTesseract(string imagePath, string language, string tessDataPath);
    }
}