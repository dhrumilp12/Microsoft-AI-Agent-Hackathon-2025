#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using VocabularyBank.Models;
using DotNetEnv;

namespace VocabularyBank.Services
{
    public class M365ExportService
    {
        private readonly IConfiguration _configuration;
        private GraphServiceClient? _graphClient;
        private bool _isM365Available = false;

        public M365ExportService(IConfiguration configuration)
        {
            _configuration = configuration;
            InitializeGraphClient();
        }

        private void InitializeGraphClient()
        {
            try
            {
                // Load .env file once at the start - using absolute path to ensure correct location
                //string envPath = Path.GetFullPath(".env");
                //DotNetEnv.Env.Load(envPath);
                //DotNetEnv.Env.TraversePath().Load();
                Env.Load(Path.Combine(System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).FullName, ".env"));

                // Force reload environment variables into current process
                foreach (var key in new[] { "M365_CLIENT_ID", "M365_TENANT_ID", "M365_CLIENT_SECRET" })
                {
                    var value = DotNetEnv.Env.GetString(key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }

                // Get credentials directly from environment after ensuring they're loaded
                string? clientId = Environment.GetEnvironmentVariable("M365_CLIENT_ID");
                string? tenantId = Environment.GetEnvironmentVariable("M365_TENANT_ID");
                string? clientSecret = Environment.GetEnvironmentVariable("M365_CLIENT_SECRET");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientSecret))
                {
                    Console.WriteLine("❌ M365 authentication details are missing. Export to M365 will not be available.");
                    _isM365Available = false;
                    return;
                }

                // Initialize the MSAL client
                IConfidentialClientApplication msalClient = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                    .WithClientSecret(clientSecret)
                    .Build();

                // Test token acquisition
                try
                {
                    string[] scopes = new[] { "https://graph.microsoft.com/.default" };
                    var result = msalClient.AcquireTokenForClient(scopes).ExecuteAsync().GetAwaiter().GetResult();
                }
                catch (Exception tokenEx)
                {
                    Console.WriteLine($"❌ Failed to acquire token: {tokenEx.Message}");

                    if (tokenEx.Message.Contains("AADSTS7000215") || tokenEx.Message.Contains("Invalid client secret"))
                    {
                        Console.WriteLine();
                        Console.WriteLine("=====================================================");
                        Console.WriteLine("ERROR: INVALID CLIENT SECRET");
                        Console.WriteLine("=====================================================");
                        Console.WriteLine("The client secret in your .env file appears to be invalid.");
                        Console.WriteLine("It looks like you might be using the Client Secret ID rather");
                        Console.WriteLine("than the actual Client Secret Value.");
                        Console.WriteLine();
                        Console.WriteLine("To fix this:");
                        Console.WriteLine("1. Go to the Azure Portal: https://portal.azure.com");
                        Console.WriteLine("2. Navigate to Azure Active Directory > App Registrations");
                        Console.WriteLine("3. Select your app (ID: a61cb602-a128-4937-ade6-c134aff3e217)");
                        Console.WriteLine("4. Click on 'Certificates & secrets'");
                        Console.WriteLine("5. Create a new client secret");
                        Console.WriteLine("6. COPY THE VALUE (not the ID) of the new secret");
                        Console.WriteLine("7. Update your .env file with this new value");
                        Console.WriteLine("=====================================================");
                    }

                    if (tokenEx.InnerException != null)
                    {
                        Console.WriteLine($"  Inner exception: {tokenEx.InnerException.Message}");
                    }
                    _isM365Available = false;
                    return;
                }

                // Create Graph client
                var authProvider = new MicrosoftAuthenticationProvider(msalClient);
                _graphClient = new GraphServiceClient(authProvider);
                _isM365Available = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing Microsoft Graph client: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                }
                _graphClient = null;
                _isM365Available = false;
            }
        }

        public bool IsM365Available()
        {
            return _isM365Available && _graphClient != null;
        }

        /// <summary>
        /// Exports flashcards to Microsoft 365 and shares with the specified user.
        /// </summary>
        public async Task<string> ExportFlashcardsAsync(List<Flashcard> flashcards, string userEmail)
        {
            if (_graphClient == null || !_isM365Available)
            {
                Console.WriteLine("Microsoft 365 integration not available. Creating local files instead.");
                return await CreateLocalFilesAsync(flashcards);
            }

            try
            {
                Console.WriteLine($"Exporting {flashcards.Count} flashcards to Microsoft 365 for user {userEmail}...");

                // Step 1: Create folder in OneDrive
                string folderName = $"VocabularyBank_Flashcards_{DateTime.Now:yyyyMMdd_HHmmss}";
                var folderId = await CreateOneDriveFolderAsync(folderName);

                if (string.IsNullOrEmpty(folderId))
                {
                    Console.WriteLine("Failed to create folder in OneDrive. Falling back to local files.");
                    return await CreateLocalFilesAsync(flashcards);
                }

                // Step 2: Upload files to OneDrive
                bool uploadSuccess = await UploadFilesToOneDriveAsync(flashcards, folderId, folderName);

                if (!uploadSuccess)
                {
                    Console.WriteLine("Failed to upload files to OneDrive. Falling back to local files.");
                    return await CreateLocalFilesAsync(flashcards);
                }

                // Step 3: Share folder with the user
                string sharingUrl = await ShareOneDriveFolderAsync(folderId, userEmail);

                if (string.IsNullOrEmpty(sharingUrl))
                {
                    Console.WriteLine("Files uploaded successfully but sharing failed. Folder can be accessed through OneDrive.");
                }
                else
                {
                    Console.WriteLine("Files successfully uploaded and shared via Microsoft 365.");
                }

                return sharingUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Microsoft 365 export: {ex.Message}");
                return await CreateLocalFilesAsync(flashcards);
            }
        }

        /// <summary>
        /// Creates a folder in OneDrive.
        /// </summary>
        private async Task<string> CreateOneDriveFolderAsync(string folderName)
        {
            try
            {
                // With application permissions, we need to use a shared drive or specific user's drive
                // Rather than the '/me' endpoint which requires delegated permissions

                // First try to get the drives available to the application
                var drives = await _graphClient.Drives
                    .Request()
                    .GetAsync();

                if (drives == null || drives.Count == 0)
                {
                    Console.WriteLine("No drives available to this application.");
                    return string.Empty;
                }

                // Use the first available drive
                var driveId = drives[0].Id;
                Console.WriteLine($"Using drive: {drives[0].Name} (ID: {driveId})");

                var driveItem = new DriveItem
                {
                    Name = folderName,
                    Folder = new Folder()
                };

                // Create the folder in the drive root rather than in /me/drive
                var newFolder = await _graphClient.Drives[driveId].Root.Children
                    .Request()
                    .AddAsync(driveItem);

                Console.WriteLine($"Created folder: {folderName} (ID: {newFolder?.Id})");
                return newFolder?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create folder in OneDrive: {ex.Message}");
                // Add more detailed error info
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Uploads files to OneDrive folder.
        /// </summary>
        private async Task<bool> UploadFilesToOneDriveAsync(List<Flashcard> flashcards, string folderId, string folderName)
        {
            try
            {
                // Need to know which drive we're working with
                var drives = await _graphClient.Drives
                    .Request()
                    .GetAsync();

                if (drives == null || drives.Count == 0)
                {
                    Console.WriteLine("No drives available for file upload.");
                    return false;
                }

                // Use the first available drive
                var driveId = drives[0].Id;

                // Prepare content for different file formats
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Don't escape Unicode characters
                };
                string jsonContent = JsonSerializer.Serialize(flashcards, jsonOptions);
                string csvContent = ConvertFlashcardsToCsv(flashcards);
                string htmlContent = ConvertFlashcardsToHtml(flashcards);

                // Upload JSON file
                await UploadFileAsync(driveId, folderId, "flashcards.json", jsonContent);

                // Upload CSV file
                await UploadFileAsync(driveId, folderId, "flashcards.csv", csvContent);

                // Upload HTML file
                await UploadFileAsync(driveId, folderId, "flashcards.html", htmlContent);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload files to OneDrive: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Uploads a single file to OneDrive.
        /// </summary>
        private async Task UploadFileAsync(string driveId, string folderId, string fileName, string content)
        {
            try
            {
                if (_graphClient == null)
                {
                    throw new InvalidOperationException("Graph client is not initialized");
                }

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

                // Upload using Microsoft Graph v4 API style but with specific drive
                var requestResult = await _graphClient.Drives[driveId].Items[folderId].ItemWithPath(fileName).Content
                    .Request()
                    .PutAsync<DriveItem>(stream);

                Console.WriteLine($"Successfully uploaded {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload {fileName}: {ex.Message}");
                throw; // Re-throw to be handled by the calling method
            }
        }

        /// <summary>
        /// Shares a folder with a specific user email.
        /// </summary>
        private async Task<string> ShareOneDriveFolderAsync(string folderId, string userEmail)
        {
            try
            {
                if (_graphClient == null)
                {
                    throw new InvalidOperationException("Graph client is not initialized");
                }

                // First find which drive we're working with
                var drives = await _graphClient.Drives
                    .Request()
                    .GetAsync();

                if (drives == null || drives.Count == 0)
                {
                    Console.WriteLine("No drives available for sharing.");
                    return string.Empty;
                }

                // Use the first available drive
                var driveId = drives[0].Id;

                // Create a sharing link using the available API in v4 with specific drive
                var permission = await _graphClient.Drives[driveId].Items[folderId]
                    .CreateLink("view", "anonymous")
                    .Request()
                    .PostAsync();

                // Extract the sharing URL from the permission object
                string sharingUrl = permission?.Link?.WebUrl ?? string.Empty;

                // Log successful sharing
                if (!string.IsNullOrEmpty(sharingUrl))
                {
                    Console.WriteLine($"Sharing link created for {userEmail}: {sharingUrl}");
                }

                return sharingUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to share OneDrive folder: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return string.Empty;
            }
        }
        /// <summary>
        /// Creates local files as a fallback when Microsoft 365 integration fails.
        /// </summary>
        private async Task<string> CreateLocalFilesAsync(List<Flashcard> flashcards)
        {
            // Create a local folder for the flashcards as a fallback
            string folderName = $"VocabularyBank_Flashcards_{DateTime.Now:yyyyMMdd_HHmmss}";
            string folderPath = Path.Combine(Path.GetTempPath(), folderName);
            System.IO.Directory.CreateDirectory(folderPath);

            // Create JSON, CSV, and HTML files
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Don't escape Unicode characters 
            };
            string jsonContent = JsonSerializer.Serialize(flashcards, jsonOptions);
            await System.IO.File.WriteAllTextAsync(Path.Combine(folderPath, "flashcards.json"), jsonContent);

            string csvContent = ConvertFlashcardsToCsv(flashcards);
            await System.IO.File.WriteAllTextAsync(Path.Combine(folderPath, "flashcards.csv"), csvContent);

            string htmlContent = ConvertFlashcardsToHtml(flashcards);
            await System.IO.File.WriteAllTextAsync(Path.Combine(folderPath, "flashcards.html"), htmlContent);

            Console.WriteLine($"Files have been saved locally to: {folderPath}");

            // For Windows systems, try to open the folder automatically
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Could not open folder automatically: {ex.Message}");
            }

            return $"file://{folderPath}";
        }

        private string ConvertFlashcardsToCsv(List<Flashcard> flashcards)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Term,Definition,Example,Context,CreatedDate");
            foreach (var card in flashcards)
            {
                sb.AppendLine(
                    $"\"{EscapeCsvField(card.Term)}\"," +
                    $"\"{EscapeCsvField(card.Definition)}\"," +
                    $"\"{EscapeCsvField(card.Example)}\"," +
                    $"\"{EscapeCsvField(card.Context)}\"," +
                    $"\"{card.CreatedDate:yyyy-MM-dd}\"");
            }
            return sb.ToString();
        }

        private string ConvertFlashcardsToHtml(List<Flashcard> flashcards, List<Flashcard>? translatedFlashcards = null, string? translatedLanguage = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <title>Vocabulary Flashcards</title>");
            sb.AppendLine("  <link rel=\"stylesheet\" href=\"../styles.css\">");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("    h1 { color: #2c3e50; }");
            sb.AppendLine("    h2 { color: #3498db; margin-top: 30px; }");
            sb.AppendLine("    .flashcard-container { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 20px; }");
            sb.AppendLine("    .flashcard { border: 1px solid #ddd; padding: 15px; margin-bottom: 15px; border-radius: 5px; width: 300px; }");
            sb.AppendLine("    .term { font-size: 1.2em; color: #3498db; font-weight: bold; }");
            sb.AppendLine("    .definition { margin-top: 10px; }");
            sb.AppendLine("    .example { margin-top: 10px; font-style: italic; color: #555; }");
            sb.AppendLine("    .context { margin-top: 5px; color: #777; font-size: 0.9em; }");
            sb.AppendLine("    .date { color: #999; font-size: 0.8em; text-align: right; }");
            sb.AppendLine("    .language-indicator { display: inline-block; padding: 2px 6px; font-size: 0.8em; border-radius: 3px; margin-left: 10px; }");
            sb.AppendLine("    .original { background-color: #e8f4f8; color: #3498db; }");
            sb.AppendLine("    .translated { background-color: #f8e8f4; color: #9b59b6; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <h1>Vocabulary Flashcards</h1>");

            bool hasBothLanguages = translatedFlashcards != null && translatedFlashcards.Count > 0;

            // For original flashcards
            if (hasBothLanguages)
            {
                sb.AppendLine("  <h2>Original Language (English)</h2>");
            }

            sb.AppendLine("  <div class=\"flashcard-container\">");
            foreach (var card in flashcards)
            {
                sb.AppendLine("    <div class=\"flashcard\">");
                sb.AppendLine($"      <div class=\"term\">{HtmlEncode(card.Term)}");
                if (hasBothLanguages)
                {
                    sb.AppendLine($"      <span class=\"language-indicator original\">EN</span>");
                }
                sb.AppendLine("      </div>");
                sb.AppendLine($"      <div class=\"definition\">{HtmlEncode(card.Definition)}</div>");

                if (!string.IsNullOrEmpty(card.Example))
                    sb.AppendLine($"      <div class=\"example\">Example: {HtmlEncode(card.Example)}</div>");

                if (!string.IsNullOrEmpty(card.Context))
                    sb.AppendLine($"      <div class=\"context\">Context: {HtmlEncode(card.Context)}</div>");

                sb.AppendLine($"      <div class=\"date\">{card.CreatedDate:yyyy-MM-dd}</div>");
                sb.AppendLine("    </div>");
            }
            sb.AppendLine("  </div>");

            // For translated flashcards
            if (hasBothLanguages)
            {
                sb.AppendLine($"  <h2>Translated Language ({translatedLanguage})</h2>");
                sb.AppendLine("  <div class=\"flashcard-container\">");

                foreach (var card in translatedFlashcards)
                {
                    sb.AppendLine("    <div class=\"flashcard\">");
                    sb.AppendLine($"      <div class=\"term\">{HtmlEncode(card.Term)}");
                    sb.AppendLine($"      <span class=\"language-indicator translated\">{translatedLanguage}</span>");
                    sb.AppendLine("      </div>");
                    sb.AppendLine($"      <div class=\"definition\">{HtmlEncode(card.Definition)}</div>");

                    if (!string.IsNullOrEmpty(card.Example))
                        sb.AppendLine($"      <div class=\"example\">Example: {HtmlEncode(card.Example)}</div>");

                    if (!string.IsNullOrEmpty(card.Context))
                        sb.AppendLine($"      <div class=\"context\">Context: {HtmlEncode(card.Context)}</div>");

                    sb.AppendLine($"      <div class=\"date\">{card.CreatedDate:yyyy-MM-dd}</div>");
                    sb.AppendLine("    </div>");
                }

                sb.AppendLine("  </div>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private string EscapeCsvField(string field)
        {
            return string.IsNullOrEmpty(field) ? string.Empty : field.Replace("\"", "\"\"");
        }

        private string HtmlEncode(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : System.Net.WebUtility.HtmlEncode(text);
        }
    }

    /// <summary>
    /// Authentication provider for Microsoft Graph SDK v4
    /// </summary>
    internal class MicrosoftAuthenticationProvider : Microsoft.Graph.IAuthenticationProvider
    {
        private readonly IConfidentialClientApplication _msalClient;

        public MicrosoftAuthenticationProvider(IConfidentialClientApplication msalClient)
        {
            _msalClient = msalClient;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            try
            {
                string[] scopes = new[] { "https://graph.microsoft.com/.default" };
                var result = await _msalClient.AcquireTokenForClient(scopes).ExecuteAsync();
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication error: {ex.Message}");
                throw;
            }
        }
    }
}