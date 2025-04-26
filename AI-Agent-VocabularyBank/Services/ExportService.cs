using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VocabularyBank.Models;

#nullable enable

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service for exporting flashcards to various formats.
    /// </summary>
    public class ExportService : IExportService
    {
        private readonly IConfiguration _configuration;
        private readonly M365ExportService _m365ExportService;
        private bool _isM365AvailabilityCached = false;
        private bool _isM365Available = false;

        public ExportService(IConfiguration configuration, M365ExportService m365ExportService)
        {
            _configuration = configuration;
            _m365ExportService = m365ExportService;
        }

        /// <summary>
        /// Exports flashcards to a file in an appropriate format based on file extension.
        /// </summary>
        public async Task ExportFlashcardsAsync(List<Flashcard> flashcards, string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty", nameof(outputPath));
            }

            string extension = Path.GetExtension(outputPath).ToLowerInvariant();
            
            string content;
            switch (extension)
            {
                case ".json":
                    content = ExportAsJson(flashcards);
                    break;
                case ".csv":
                    content = ExportAsCsv(flashcards);
                    break;
                case ".html":
                    content = ExportAsHtml(flashcards);
                    break;
                default:
                    throw new NotSupportedException($"Export format {extension} is not supported");
            }

            // Create directory if it doesn't exist
            string? directory = Path.GetDirectoryName(outputPath);
            Console.WriteLine($"Service output path: {directory}");
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, content);
        }

        /// <summary>
        /// Exports flashcards to JSON format.
        /// </summary>
        public string ExportAsJson(List<Flashcard> flashcards)
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Don't escape Unicode characters
            };
            return JsonSerializer.Serialize(flashcards, options);
        }

        /// <summary>
        /// Exports flashcards to CSV format.
        /// </summary>
        public string ExportAsCsv(List<Flashcard> flashcards)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Term,Definition,Example,Context,CreatedDate");
            
            foreach (var flashcard in flashcards)
            {
                string term = EscapeCsvField(flashcard.Term);
                string definition = EscapeCsvField(flashcard.Definition);
                string example = EscapeCsvField(flashcard.Example);
                string context = EscapeCsvField(flashcard.Context);
                
                sb.AppendLine($"{term},{definition},{example},{context},{flashcard.CreatedDate:yyyy-MM-dd}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Exports flashcards to HTML format.
        /// </summary>
        public string ExportAsHtml(List<Flashcard> flashcards)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>Vocabulary Flashcards</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { background-color: #f9f9f9; padding: 20px; font-family: Arial, sans-serif; }");
            sb.AppendLine("    header { text-align: center; margin-bottom: 40px; }");
            sb.AppendLine("    h1 { color: #3498db; font-size: 28px; }");
            sb.AppendLine("    .flashcard-container { display: flex; flex-wrap: wrap; gap: 20px; }");
            sb.AppendLine("    .flashcard { width: 300px; min-height: 200px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); padding: 20px; background-color: #fff; transition: transform 0.3s ease, box-shadow 0.3s ease; }");
            sb.AppendLine("    .flashcard:hover { transform: translateY(-5px); box-shadow: 0 6px 12px rgba(0,0,0,0.15); }");
            sb.AppendLine("    .flashcard-term { font-size: 18px; font-weight: bold; color: #2c3e50; margin-bottom: 10px; padding-bottom: 5px; border-bottom: 1px solid #e0e0e0; }");
            sb.AppendLine("    .flashcard-definition { font-size: 15px; margin-bottom: 15px; color: #34495e; }");
            sb.AppendLine("    .flashcard-example { font-size: 14px; font-style: italic; color: #7f8c8d; margin-bottom: 15px; }");
            sb.AppendLine("    .flashcard-context { font-size: 13px; color: #95a5a6; }");
            sb.AppendLine("    .flashcard-date { font-size: 12px; color: #bdc3c7; text-align: right; margin-top: 15px; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <header>");
            sb.AppendLine("    <h1>Vocabulary Flashcards</h1>");
            sb.AppendLine("  </header>");
            
            sb.AppendLine("  <div class=\"flashcard-container\">");
            
            foreach (var card in flashcards)
            {
                sb.AppendLine("    <div class=\"flashcard\">");
                sb.AppendLine($"      <div class=\"flashcard-term\">{HtmlEncode(card.Term)}</div>");
                sb.AppendLine($"      <div class=\"flashcard-definition\">{HtmlEncode(card.Definition)}</div>");
                
                if (!string.IsNullOrEmpty(card.Example))
                    sb.AppendLine($"      <div class=\"flashcard-example\">Example: {HtmlEncode(card.Example)}</div>");
                    
                if (!string.IsNullOrEmpty(card.Context))
                    sb.AppendLine($"      <div class=\"flashcard-context\">Context: {HtmlEncode(card.Context)}</div>");
                    
                sb.AppendLine($"      <div class=\"flashcard-date\">{card.CreatedDate:yyyy-MM-dd}</div>");
                sb.AppendLine("    </div>");
            }
            
            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        /// <summary>
        /// Exports both original and translated flashcards to a file.
        /// </summary>
        public async Task ExportCombinedFlashcardsAsync(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage, 
            string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty", nameof(outputPath));
            }

            string extension = Path.GetExtension(outputPath).ToLowerInvariant();
            
            string content;
            switch (extension)
            {
                case ".json":
                    content = ExportCombinedAsJson(originalFlashcards, translatedFlashcards, translatedLanguage);
                    break;
                case ".csv":
                    content = ExportCombinedAsCsv(originalFlashcards, translatedFlashcards, translatedLanguage);
                    break;
                case ".html":
                    content = ExportCombinedAsHtml(originalFlashcards, translatedFlashcards, translatedLanguage);
                    break;
                default:
                    throw new NotSupportedException($"Export format {extension} is not supported");
            }

            // Create directory if it doesn't exist
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, content);
        }

        /// <summary>
        /// Exports a combined set of original and translated flashcards to JSON format.
        /// </summary>
        public string ExportCombinedAsJson(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage)
        {
            var combinedData = new
            {
                originalLanguage = "English",
                translatedLanguage = translatedLanguage,
                flashcards = new
                {
                    original = originalFlashcards,
                    translated = translatedFlashcards
                }
            };
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(combinedData, options);
        }
        
        /// <summary>
        /// Exports a combined set of original and translated flashcards to CSV format.
        /// </summary>
        public string ExportCombinedAsCsv(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Term,Definition,Example,Context,Language");
            
            foreach (var flashcard in originalFlashcards)
            {
                string term = EscapeCsvField(flashcard.Term);
                string definition = EscapeCsvField(flashcard.Definition);
                string example = EscapeCsvField(flashcard.Example);
                string context = EscapeCsvField(flashcard.Context);
                
                sb.AppendLine($"{term},{definition},{example},{context},English");
            }
            
            foreach (var flashcard in translatedFlashcards)
            {
                string term = EscapeCsvField(flashcard.Term);
                string definition = EscapeCsvField(flashcard.Definition);
                string example = EscapeCsvField(flashcard.Example);
                string context = EscapeCsvField(flashcard.Context);
                
                sb.AppendLine($"{term},{definition},{example},{context},{translatedLanguage}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Exports a combined set of original and translated flashcards to HTML format.
        /// </summary>
        public string ExportCombinedAsHtml(
            List<Flashcard> originalFlashcards, 
            List<Flashcard> translatedFlashcards, 
            string translatedLanguage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>Vocabulary Flashcards</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { background-color: #f9f9f9; padding: 20px; font-family: Arial, sans-serif; }");
            sb.AppendLine("    header { text-align: center; margin-bottom: 40px; }");
            sb.AppendLine("    h1 { color: #3498db; font-size: 28px; }");
            sb.AppendLine("    h2 { color: #2c3e50; font-size: 22px; margin-top: 30px; margin-bottom: 20px; }");
            sb.AppendLine("    .language-switcher { display: flex; justify-content: center; gap: 10px; margin-bottom: 30px; }");
            sb.AppendLine("    .language-btn { padding: 8px 16px; background-color: #ecf0f1; border: none; border-radius: 4px; cursor: pointer; }");
            sb.AppendLine("    .language-btn.active { background-color: #3498db; color: white; font-weight: bold; }");
            sb.AppendLine("    .flashcard-container { display: flex; flex-wrap: wrap; gap: 20px; }");
            sb.AppendLine("    .flashcard { width: 300px; min-height: 200px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); padding: 20px; background-color: #fff; transition: transform 0.3s ease, box-shadow 0.3s ease; }");
            sb.AppendLine("    .flashcard:hover { transform: translateY(-5px); box-shadow: 0 6px 12px rgba(0,0,0,0.15); }");
            sb.AppendLine("    .flashcard-term { font-size: 18px; font-weight: bold; color: #2c3e50; margin-bottom: 10px; padding-bottom: 5px; border-bottom: 1px solid #e0e0e0; }");
            sb.AppendLine("    .flashcard-definition { font-size: 15px; margin-bottom: 15px; color: #34495e; }");
            sb.AppendLine("    .flashcard-example { font-size: 14px; font-style: italic; color: #7f8c8d; margin-bottom: 15px; }");
            sb.AppendLine("    .flashcard-context { font-size: 13px; color: #95a5a6; }");
            sb.AppendLine("    .flashcard-date { font-size: 12px; color: #bdc3c7; text-align: right; margin-top: 15px; }");
            sb.AppendLine("    .language-tag { font-size: 12px; padding: 3px 8px; border-radius: 3px; display: inline-block; margin-left: 10px; }");
            sb.AppendLine("    .original-tag { background-color: #e8f4f8; color: #3498db; }");
            sb.AppendLine("    .translated-tag { background-color: #f8e8f4; color: #9b59b6; }");
            sb.AppendLine("    .language-section { margin-bottom: 40px; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <header>");
            sb.AppendLine("    <h1>Vocabulary Flashcards</h1>");
            sb.AppendLine("  </header>");
            
            sb.AppendLine("  <div class=\"language-switcher\">");
            sb.AppendLine("    <button class=\"language-btn active\" onclick=\"showLanguage('original')\">English</button>");
            sb.AppendLine($"    <button class=\"language-btn\" onclick=\"showLanguage('translated')\">{translatedLanguage}</button>");
            sb.AppendLine("    <button class=\"language-btn\" onclick=\"showLanguage('both')\">Both Languages</button>");
            sb.AppendLine("  </div>");
            
            sb.AppendLine("  <div id=\"original-section\" class=\"language-section\">");
            sb.AppendLine("    <h2>Original Language (English)</h2>");
            sb.AppendLine("    <div class=\"flashcard-container\">");
            
            foreach (var card in originalFlashcards)
            {
                sb.AppendLine("      <div class=\"flashcard\">");
                sb.AppendLine($"        <div class=\"flashcard-term\">{HtmlEncode(card.Term)}<span class=\"language-tag original-tag\">EN</span></div>");
                sb.AppendLine($"        <div class=\"flashcard-definition\">{HtmlEncode(card.Definition)}</div>");
                
                if (!string.IsNullOrEmpty(card.Example))
                    sb.AppendLine($"        <div class=\"flashcard-example\">Example: {HtmlEncode(card.Example)}</div>");
                    
                if (!string.IsNullOrEmpty(card.Context))
                    sb.AppendLine($"        <div class=\"flashcard-context\">Context: {HtmlEncode(card.Context)}</div>");
                    
                sb.AppendLine($"        <div class=\"flashcard-date\">{card.CreatedDate:yyyy-MM-dd}</div>");
                sb.AppendLine("      </div>");
            }
            
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
            
            sb.AppendLine("  <div id=\"translated-section\" class=\"language-section\" style=\"display:none;\">");
            sb.AppendLine($"    <h2>Translated Language ({translatedLanguage})</h2>");
            sb.AppendLine("    <div class=\"flashcard-container\">");
            
            foreach (var card in translatedFlashcards)
            {
                sb.AppendLine("      <div class=\"flashcard\">");
                sb.AppendLine($"        <div class=\"flashcard-term\">{HtmlEncode(card.Term)}<span class=\"language-tag translated-tag\">{translatedLanguage}</span></div>");
                sb.AppendLine($"        <div class=\"flashcard-definition\">{HtmlEncode(card.Definition)}</div>");
                
                if (!string.IsNullOrEmpty(card.Example))
                    sb.AppendLine($"        <div class=\"flashcard-example\">Example: {HtmlEncode(card.Example)}</div>");
                    
                if (!string.IsNullOrEmpty(card.Context))
                    sb.AppendLine($"        <div class=\"flashcard-context\">Context: {HtmlEncode(card.Context)}</div>");
                    
                sb.AppendLine($"        <div class=\"flashcard-date\">{card.CreatedDate:yyyy-MM-dd}</div>");
                sb.AppendLine("      </div>");
            }
            
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
            
            sb.AppendLine("  <script>");
            sb.AppendLine("    function showLanguage(language) {");
            sb.AppendLine("      const originalSection = document.getElementById('original-section');");
            sb.AppendLine("      const translatedSection = document.getElementById('translated-section');");
            sb.AppendLine("      const buttons = document.querySelectorAll('.language-btn');");
            sb.AppendLine("      ");
            sb.AppendLine("      buttons.forEach(btn => btn.classList.remove('active'));");
            sb.AppendLine("      ");
            sb.AppendLine("      if (language === 'original') {");
            sb.AppendLine("        originalSection.style.display = 'block';");
            sb.AppendLine("        translatedSection.style.display = 'none';");
            sb.AppendLine("        buttons[0].classList.add('active');");
            sb.AppendLine("      } else if (language === 'translated') {");
            sb.AppendLine("        originalSection.style.display = 'none';");
            sb.AppendLine("        translatedSection.style.display = 'block';");
            sb.AppendLine("        buttons[1].classList.add('active');");
            sb.AppendLine("      } else {");
            sb.AppendLine("        originalSection.style.display = 'block';");
            sb.AppendLine("        translatedSection.style.display = 'block';");
            sb.AppendLine("        buttons[2].classList.add('active');");
            sb.AppendLine("      }");
            sb.AppendLine("    }");
            sb.AppendLine("  </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        /// <summary>
        /// Exports flashcards to Microsoft 365 Learning Management System.
        /// </summary>
        public async Task<string> ExportToM365Async(List<Flashcard> flashcards, string userEmail)
        {
            if (!IsM365ExportAvailable())
            {
                throw new InvalidOperationException("Microsoft 365 export is not available. Check configuration.");
            }
            
            return await _m365ExportService.ExportFlashcardsAsync(flashcards, userEmail);
        }

        /// <summary>
        /// Checks if M365 export capability is configured.
        /// </summary>
        public bool IsM365ExportAvailable()
        {
            if (_isM365AvailabilityCached)
            {
                return _isM365Available;
            }
            
            string? clientId = _configuration["M365:ClientId"];
            string? tenantId = _configuration["M365:TenantId"];
            string? clientSecret = _configuration["M365:ClientSecret"];
            
            if (string.IsNullOrEmpty(clientId))
            {
                var m365Section = _configuration.GetSection("M365");
                clientId = m365Section["ClientId"];
                tenantId = m365Section["TenantId"];
                clientSecret = m365Section["ClientSecret"];
            }
            
            clientId = clientId ?? Environment.GetEnvironmentVariable("M365_CLIENT_ID");
            tenantId = tenantId ?? Environment.GetEnvironmentVariable("M365_TENANT_ID");
            clientSecret = clientSecret ?? Environment.GetEnvironmentVariable("M365_CLIENT_SECRET");
            
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientSecret))
            {
                try
                {
                    string envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "../.env");
                    if (File.Exists(envFilePath))
                    {
                        Console.WriteLine($"Reading credentials directly from .env file at {envFilePath}");
                        var envVars = File.ReadAllLines(envFilePath)
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("//"))
                            .Select(line => line.Trim())
                            .ToDictionary(
                                line => line.Split('=')[0].Trim(),
                                line => line.Substring(line.IndexOf('=') + 1).Trim('"', ' ')
                            );

                        if (string.IsNullOrEmpty(clientId) && envVars.ContainsKey("M365_CLIENT_ID"))
                        {
                            clientId = envVars["M365_CLIENT_ID"];
                        }
                        
                        if (string.IsNullOrEmpty(tenantId) && envVars.ContainsKey("M365_TENANT_ID"))
                        {
                            tenantId = envVars["M365_TENANT_ID"];
                        }
                        
                        if (string.IsNullOrEmpty(clientSecret) && envVars.ContainsKey("M365_CLIENT_SECRET"))
                        {
                            clientSecret = envVars["M365_CLIENT_SECRET"];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading .env file: {ex.Message}");
                }
            }

            Console.WriteLine("Checking M365 credentials availability:");
            Console.WriteLine($"  - ClientId: {(string.IsNullOrEmpty(clientId) ? "Not found" : "Available")}");
            Console.WriteLine($"  - TenantId: {(string.IsNullOrEmpty(tenantId) ? "Not found" : "Available")}");
            Console.WriteLine($"  - ClientSecret: {(string.IsNullOrEmpty(clientSecret) ? "Not found" : "Available")}");
            
            _isM365AvailabilityCached = true;
            _isM365Available = !string.IsNullOrEmpty(clientId) && 
                              !string.IsNullOrEmpty(tenantId) && 
                              !string.IsNullOrEmpty(clientSecret);
            
            return _isM365Available;
        }

        /// <summary>
        /// Escapes special characters in CSV fields.
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }
            
            bool needsQuoting = field.Contains(",") || field.Contains("\"") || field.Contains("\n");
            if (needsQuoting)
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            
            return field;
        }

        /// <summary>
        /// HTML-encodes a string for safe output in HTML.
        /// </summary>
        private string HtmlEncode(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : System.Net.WebUtility.HtmlEncode(text);
        }
    }
}