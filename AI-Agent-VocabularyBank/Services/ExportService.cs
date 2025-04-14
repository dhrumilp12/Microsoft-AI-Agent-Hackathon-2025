using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VocabularyBank.Models;

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
                    content = await ExportAsJson(flashcards);
                    break;
                case ".csv":
                    content = await ExportAsCsv(flashcards);
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
        /// Exports flashcards to JSON format.
        /// </summary>
        public async Task<string> ExportAsJson(List<Flashcard> flashcards)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(flashcards, options);
        }

        /// <summary>
        /// Exports flashcards to CSV format.
        /// </summary>
        public async Task<string> ExportAsCsv(List<Flashcard> flashcards)
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
            // Use cached value if already checked
            if (_isM365AvailabilityCached)
            {
                return _isM365Available;
            }
            
            // Try multiple ways to get the M365 credentials
            // 1. First try direct from configuration with section:key format
            string? clientId = _configuration["M365:ClientId"];
            string? tenantId = _configuration["M365:TenantId"];
            string? clientSecret = _configuration["M365:ClientSecret"];
            
            // 2. Try from GetSection approach if the above didn't work
            if (string.IsNullOrEmpty(clientId))
            {
                var m365Section = _configuration.GetSection("M365");
                clientId = m365Section["ClientId"];
                tenantId = m365Section["TenantId"];
                clientSecret = m365Section["ClientSecret"];
            }
            
            // 3. Try from environment variables if still not found
            clientId = clientId ?? Environment.GetEnvironmentVariable("M365_CLIENT_ID");
            tenantId = tenantId ?? Environment.GetEnvironmentVariable("M365_TENANT_ID");
            clientSecret = clientSecret ?? Environment.GetEnvironmentVariable("M365_CLIENT_SECRET");
            
            // 4. As a last resort, try to read directly from .env file
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientSecret))
            {
                try
                {
                    string envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
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

            // Debug output to help diagnose the issue
            Console.WriteLine("Checking M365 credentials availability:");
            Console.WriteLine($"  - ClientId: {(string.IsNullOrEmpty(clientId) ? "Not found" : "Available")}");
            Console.WriteLine($"  - TenantId: {(string.IsNullOrEmpty(tenantId) ? "Not found" : "Available")}");
            Console.WriteLine($"  - ClientSecret: {(string.IsNullOrEmpty(clientSecret) ? "Not found" : "Available")}");
            
            // Cache the result
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
    }
}