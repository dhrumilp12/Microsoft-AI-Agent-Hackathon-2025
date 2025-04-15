using System.Text.Json.Serialization;

namespace ClassroomBoardCapture.Models
{
    /// <summary>
    /// Result model for OCR responses from Azure Computer Vision API
    /// </summary>
    public class OcrResult
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;
        
        [JsonPropertyName("textAngle")]
        public float TextAngle { get; set; }
        
        [JsonPropertyName("orientation")]
        public string Orientation { get; set; } = string.Empty;
        
        [JsonPropertyName("regions")]
        public Region[] Regions { get; set; } = Array.Empty<Region>();
    }

    /// <summary>
    /// Region model for OCR results
    /// </summary>
    public class Region
    {
        [JsonPropertyName("boundingBox")]
        public string BoundingBox { get; set; } = string.Empty;
        
        [JsonPropertyName("lines")]
        public Line[] Lines { get; set; } = Array.Empty<Line>();
    }

    /// <summary>
    /// Line model for OCR results
    /// </summary>
    public class Line
    {
        [JsonPropertyName("boundingBox")]
        public string BoundingBox { get; set; } = string.Empty;
        
        [JsonPropertyName("words")]
        public Word[] Words { get; set; } = Array.Empty<Word>();
    }

    /// <summary>
    /// Word model for OCR results
    /// </summary>
    public class Word
    {
        [JsonPropertyName("boundingBox")]
        public string BoundingBox { get; set; } = string.Empty;
        
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}