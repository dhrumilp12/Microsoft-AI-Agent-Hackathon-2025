using System;
using System.IO;

namespace ClassroomBoardCapture.Models
{
    /// <summary>
    /// Application settings loaded from configuration
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Directory where captured images will be saved
        /// </summary>
        public string CaptureFolder { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../AgentData/Captures");
        
        /// <summary>
        /// Interval between image captures in seconds
        /// </summary>
        public int CaptureIntervalSeconds { get; set; } = 20;
        
        /// <summary>
        /// Camera device ID to use for capture (default: 0 for primary camera)
        /// </summary>
        public int CameraDeviceId { get; set; } = 0;
        
        /// <summary>
        /// Vision API settings
        /// </summary>
        public VisionApiSettings VisionApi { get; set; } = new VisionApiSettings();
        
        /// <summary>
        /// Translation API settings
        /// </summary>
        public TranslatorApiSettings TranslatorApi { get; set; } = new TranslatorApiSettings();
        
        /// <summary>
        /// Tesseract OCR settings
        /// </summary>
        public TesseractSettings Tesseract { get; set; } = new TesseractSettings();
        
        /// <summary>
        /// Source language for OCR and translation
        /// </summary>
        public string SourceLanguage { get; set; } = "en";
        
        /// <summary>
        /// Target language for translation
        /// </summary>
        public string TargetLanguage { get; set; } = "zh";
    }
    
    /// <summary>
    /// Settings for Azure Computer Vision API
    /// </summary>
    public class VisionApiSettings
    {
        /// <summary>
        /// API key for Azure Computer Vision service
        /// </summary>
        public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("VISION_API_KEY") ?? string.Empty;
        
        /// <summary>
        /// Endpoint URL for Azure Computer Vision service
        /// </summary>
        public string Endpoint { get; set; } = Environment.GetEnvironmentVariable("VISION_ENDPOINT") ?? 
                                              "https://studybuddyocr.cognitiveservices.azure.com/";
    }
    
    /// <summary>
    /// Settings for Azure Translator service
    /// </summary>
    public class TranslatorApiSettings
    {
        /// <summary>
        /// API key for Azure Translator service
        /// </summary>
        public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY") ?? string.Empty;
        
        /// <summary>
        /// Endpoint URL for Azure Translator service
        /// </summary>
        public string Endpoint { get; set; } = Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT") ?? 
                                              "https://api.cognitive.microsofttranslator.com/";
        
        /// <summary>
        /// Region for Azure Translator service
        /// </summary>
        public string Region { get; set; } = Environment.GetEnvironmentVariable("TRANSLATOR_REGION") ?? "eastus";
    }
    
    /// <summary>
    /// Settings for Tesseract OCR
    /// </summary>
    public class TesseractSettings
    {
        /// <summary>
        /// Path to Tesseract data files
        /// </summary>
        public string TessDataPath { get; set; } = "tessdata";
        
        /// <summary>
        /// Tesseract language pack to use
        /// </summary>
        public string Language { get; set; } = "eng";
        
        /// <summary>
        /// Whether to use Tesseract as fallback OCR
        /// </summary>
        public bool UseTesseractFallback { get; set; } = true;
    }
}