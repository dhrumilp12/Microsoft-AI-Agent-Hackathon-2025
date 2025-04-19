using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DiagramGenerator.Helpers;
using System.Collections.Concurrent;
using DiagramGenerator.Constants;

namespace DiagramGenerator.Services
{
    /// <summary>
    /// Service responsible for communicating with Azure OpenAI API directly via HTTP
    /// </summary>
    public class AzureOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _deploymentName = string.Empty;
        private readonly string _apiVersion;
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly int _maxRetries;
        private readonly int _initialRetryDelay;
        private readonly int _timeoutSeconds;
        
        // Simple in-memory cache for API responses
        private readonly ConcurrentDictionary<string, string> _responseCache = new();
        
        public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger)
        {
            _logger = logger;
            
            // Determine whether to use environment variables or appsettings.json
            string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                configuration["Azure:OpenAI:Endpoint"];
            
            string? apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") ?? 
                configuration["Azure:OpenAI:Key"];
            
            _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? 
                configuration["Azure:OpenAI:DeploymentName"] ?? "o3-mini";
            
            // CRITICAL: Use the required API version
            _apiVersion = "2024-12-01-preview";
            
            // Get configuration 
            _maxRetries = configuration.GetValue<int>("Azure:OpenAI:MaxRetries", 3);
            _initialRetryDelay = configuration.GetValue<int>("Azure:OpenAI:InitialRetryDelayMs", 2000);
            _timeoutSeconds = configuration.GetValue<int>("Azure:OpenAI:TimeoutSeconds", 60);
            
            // Validate required parameters
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentNullException(nameof(endpoint), "Azure OpenAI endpoint not configured");

            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException(nameof(apiKey), "Azure OpenAI API key not configured");
            
            // Remove trailing slash if it exists
            if (endpoint.EndsWith('/'))
            {
                endpoint = endpoint.TrimEnd('/');
            }
            
            _logger.LogInformation($"Initializing Azure OpenAI service with endpoint: {endpoint}, deployment: {_deploymentName}");
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint),
                Timeout = TimeSpan.FromSeconds(_timeoutSeconds)
            };
            
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public async Task<string> GetSystemCompletion(string systemPrompt, string userPrompt, double temperature = 0.3, int maxTokens = 4000, IProgress<int>? progress = null)
        {
            // Generate a cache key based on inputs
            string cacheKey = GenerateCacheKey(systemPrompt, userPrompt, maxTokens);
            
            // Check if we have a cached response
            if (_responseCache.TryGetValue(cacheKey, out string? cachedResponse))
            {
                _logger.LogInformation("Using cached response");
                return cachedResponse;
            }
            
            progress?.Report(10); // Report initial progress
            
            string result = await RetryHelper.ExecuteWithRetryAsync(
                async () => 
                {
                    progress?.Report(30); // Report progress before API call
                    var response = await ExecuteSystemCompletion(systemPrompt, userPrompt, temperature, maxTokens);
                    progress?.Report(90); // Report progress after API call
                    return response;
                },
                _maxRetries,
                _initialRetryDelay,
                progress
            );
            
            // Cache the response for future use
            _responseCache.TryAdd(cacheKey, result);
            
            progress?.Report(100); // Report completion
            return result;
        }
        
        public async Task<List<ConceptNode>> ExtractConcepts(string transcript, IProgress<int>? progress = null)
        {
            _logger.LogInformation("Extracting concepts from transcript using direct API");
            progress?.Report(10);
            
            string systemPrompt = PromptConstants.ConceptExtractionSystemPrompt;
            string userPrompt = PromptConstants.GetConceptExtractionPrompt(transcript);
            
            progress?.Report(20);
            string conceptsJson = await GetSystemCompletion(systemPrompt, userPrompt, progress: new Progress<int>(p => 
            {
                // Scale the progress from 20-80% range
                progress?.Report(20 + (p * 60 / 100));
            }));
            
            progress?.Report(80);
            
            try
            {
                // Log the raw response for debugging
                _logger.LogDebug("Raw JSON response: {JSON}", conceptsJson);
                
                // Extract the JSON part if the model includes explanations
                string extractedJson = ExtractJsonArrayFromText(conceptsJson);
                
                _logger.LogDebug("Extracted JSON: {JSON}", extractedJson);
                
                // Use permissive options for JSON parsing
                var options = new JsonSerializerOptions { 
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                
                var concepts = JsonSerializer.Deserialize<List<ConceptNode>>(extractedJson, options);
                
                if (concepts == null || concepts.Count == 0)
                {
                    _logger.LogWarning("No concepts extracted from transcript");
                    progress?.Report(90);
                    var fallbackConcepts = FallbackConceptExtraction(transcript);
                    progress?.Report(100);
                    return fallbackConcepts;
                }
                
                // Enhance concepts with additional metadata
                EnhanceConceptsWithMetadata(concepts);
                
                progress?.Report(100);
                return concepts;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse concepts JSON");
                progress?.Report(85);
                
                // Attempt to fix common JSON issues and retry parsing
                try {
                    string fixedJson = FixCommonJsonIssues(conceptsJson);
                    _logger.LogDebug("Attempting to parse with fixed JSON: {JSON}", fixedJson);
                    
                    var options = new JsonSerializerOptions { 
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    var concepts = JsonSerializer.Deserialize<List<ConceptNode>>(fixedJson, options);
                    if (concepts != null && concepts.Count > 0) {
                        _logger.LogInformation("Successfully parsed JSON after fixing");
                        progress?.Report(95);
                        EnhanceConceptsWithMetadata(concepts);
                        progress?.Report(100);
                        return concepts;
                    }
                }
                catch (Exception fixEx) {
                    _logger.LogError(fixEx, "Failed to parse even after fixing JSON");
                }
                
                // Fall back to manually creating some basic concepts from the transcript
                _logger.LogWarning("Falling back to manual concept extraction");
                progress?.Report(90);
                var fallbackConcepts = FallbackConceptExtraction(transcript);
                progress?.Report(100);
                return fallbackConcepts;
            }
        }
        
        private void EnhanceConceptsWithMetadata(List<ConceptNode> concepts)
        {
            // Add styling or default values if needed
            foreach (var concept in concepts)
            {
                // If a concept doesn't have a style property, add one based on importance
                if (string.IsNullOrEmpty(concept.Style))
                {
                    concept.Style = concept.Importance switch
                    {
                        5 => "fill:#f96,stroke:#333,stroke-width:2px",
                        4 => "fill:#9cf,stroke:#333,stroke-width:1.5px",
                        3 => "fill:#cfc,stroke:#333,stroke-width:1px",
                        _ => "fill:#fcf,stroke:#333,stroke-width:1px"
                    };
                }
            }
        }
        
        private string ExtractJsonArrayFromText(string text)
        {
            // Look for a JSON array in the text
            int startIndex = text.IndexOf("[");
            int endIndex = text.LastIndexOf("]");
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            // If no valid JSON array markers found, return the original text
            // This will likely fail JSON parsing but with a clearer error
            return text;
        }
        
        private string FixCommonJsonIssues(string json)
        {
            // Extract just the array part if it exists
            string extractedJson = ExtractJsonArrayFromText(json);
            
            // Check for unclosed array
            if (extractedJson.Count(c => c == '[') > extractedJson.Count(c => c == ']'))
            {
                extractedJson += "]";
            }
            
            // Check for unclosed objects
            int openBraces = extractedJson.Count(c => c == '{');
            int closeBraces = extractedJson.Count(c => c == '}');
            
            if (openBraces > closeBraces)
            {
                extractedJson += new string('}', openBraces - closeBraces);
            }
            
            // Fix trailing commas before closing brackets
            extractedJson = Regex.Replace(extractedJson, @",(\s*[\}\]])", "$1");
            
            // Fix missing quotes around property names
            extractedJson = Regex.Replace(extractedJson, @"([{,]\s*)(\w+)(\s*:)", m => 
                $"{m.Groups[1].Value}\"{m.Groups[2].Value}\"{m.Groups[3].Value}");
            
            return extractedJson;
        }
        
        private List<ConceptNode> FallbackConceptExtraction(string transcript)
        {
            // Create a minimal set of concepts based on the transcript text
            var concepts = new List<ConceptNode>();
            
            // Split the transcript into lines
            string[] lines = transcript.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Use the first line as the main concept if it's not empty
            string title = lines.Length > 0 ? lines[0].Trim() : "Main Topic";
            
            var mainConcept = new ConceptNode
            {
                Id = "main-concept",
                Name = title,
                Description = "Main topic of the lecture",
                Importance = 5,
                Style = "fill:#f96,stroke:#333,stroke-width:2px",
                Relationships = new List<ConceptRelationship>()
            };
            
            concepts.Add(mainConcept);
            
            // Improved pattern recognition - look for various headers, list items, and important terms
            var subtopicPatterns = new List<Regex>
            {
                new Regex(@"^\s*(\d+\.\s+|\*\s+|[-•]\s+|[A-Z][a-z]+ \d+:\s+)(.+)$"), // Numbered/bulleted lists
                new Regex(@"^([A-Z][A-Za-z ]+):"), // Term definitions
                new Regex(@"^(The [A-Za-z ]+) (is|are|refers to)"), // Explanatory phrases
                new Regex(@"^([A-Z][A-Za-z ]+) (is|are) (a|an|the)") // Concept definitions
            };
            
            int idCounter = 1;
            
            // Analyze paragraph structure to find important concepts
            foreach (var line in lines)
            {
                bool matched = false;
                foreach (var pattern in subtopicPatterns)
                {
                    var match = pattern.Match(line);
                    if (match.Success)
                    {
                        string subtopicName;
                        if (match.Groups.Count > 2)
                        {
                            subtopicName = match.Groups[2].Value.Trim();
                            // If it's an empty group, use the whole match minus any leading markers
                            if (string.IsNullOrWhiteSpace(subtopicName))
                            {
                                subtopicName = match.Groups[1].Value.Trim();
                            }
                        }
                        else
                        {
                            subtopicName = match.Groups[1].Value.Trim();
                        }
                        
                        // Skip very short or common phrases
                        if (subtopicName.Length < 3 || IsCommonPhrase(subtopicName))
                            continue;
                        
                        string subtopicId = $"concept-{idCounter++}";
                        
                        var subtopic = new ConceptNode
                        {
                            Id = subtopicId,
                            Name = subtopicName,
                            Description = line.Trim(),
                            Importance = 4,
                            Style = "fill:#9cf,stroke:#333,stroke-width:1.5px",
                            Relationships = new List<ConceptRelationship>()
                        };
                        
                        // Add relationship from main concept to this subtopic
                        mainConcept.Relationships.Add(new ConceptRelationship
                        {
                            Type = "contains",
                            Target = subtopicId
                        });
                        
                        concepts.Add(subtopic);
                        matched = true;
                        break;
                    }
                }
                
                // If we already found a pattern match, move to the next line
                if (matched) continue;
                
                // Look for keywords in this line if we haven't found a pattern match
                foreach (var keyword in ExtractKeywords(line))
                {
                    // Skip if already a concept with this name
                    if (concepts.Any(c => c.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                        continue;
                        
                    string keywordId = $"concept-{idCounter++}";
                    
                    var keywordConcept = new ConceptNode
                    {
                        Id = keywordId,
                        Name = keyword,
                        Description = $"Related to {mainConcept.Name}",
                        Importance = 3,
                        Style = "fill:#cfc,stroke:#333,stroke-width:1px",
                        Relationships = new List<ConceptRelationship>()
                    };
                    
                    mainConcept.Relationships.Add(new ConceptRelationship
                    {
                        Type = "related",
                        Target = keywordId
                    });
                    
                    concepts.Add(keywordConcept);
                }
            }
            
            return concepts;
        }
        
        private bool IsCommonPhrase(string text)
        {
            string[] commonPhrases = { "the", "a", "an", "this", "these", "those", "is", "are", "and", "or", "but" };
            return commonPhrases.Contains(text.ToLower());
        }
        
        private IEnumerable<string> ExtractKeywords(string text)
        {
            // Try to extract important technical terms or concepts
            var potentialKeywords = new HashSet<string>();
            
            // Look for capitalized multi-word phrases (potential technical terms)
            var termPattern = new Regex(@"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)");
            foreach (Match match in termPattern.Matches(text))
            {
                potentialKeywords.Add(match.Value);
            }
            
            // Look for words in quotes (often defined terms)
            var quotedPattern = new Regex(@"[""']([^""']+)[""']");
            foreach (Match match in quotedPattern.Matches(text))
            {
                potentialKeywords.Add(match.Groups[1].Value);
            }
            
            // Return only reasonable length keywords (not too short, not too long)
            return potentialKeywords.Where(k => k.Length > 3 && k.Length < 40).Take(3);
        }
        
        public async Task<string> GenerateDiagram(List<ConceptNode> concepts, string diagramType, IProgress<int>? progress = null)
        {
            _logger.LogInformation($"Generating {diagramType} diagram using direct API");
            progress?.Report(10);
            
            var conceptsJson = JsonSerializer.Serialize(concepts);
            string systemPrompt = PromptConstants.DiagramGenerationSystemPrompt;
            string userPrompt = PromptConstants.GetDiagramGenerationPrompt(conceptsJson, diagramType);
            
            progress?.Report(20);
            string mermaidDiagram = await GetSystemCompletion(systemPrompt, userPrompt, progress: new Progress<int>(p => 
            {
                // Scale the progress from 20-80% range
                progress?.Report(20 + (p * 60 / 100));
            }));
            
            progress?.Report(80);
            
            // Extract just the mermaid content if needed
            if (mermaidDiagram.Contains("```mermaid") && mermaidDiagram.Contains("```"))
            {
                int startIndex = mermaidDiagram.IndexOf("```mermaid") + 10;
                int endIndex = mermaidDiagram.LastIndexOf("```");
                mermaidDiagram = mermaidDiagram.Substring(startIndex, endIndex - startIndex).Trim();
            }
            
            // Enhance the diagram with better styling
            mermaidDiagram = EnhanceDiagramFormatting(mermaidDiagram, diagramType);
            
            progress?.Report(100);
            return $"```mermaid\n{mermaidDiagram}\n```";
        }
        
        private string EnhanceDiagramFormatting(string diagram, string diagramType)
        {
            // Add title if not present
            if (diagramType == "flowchart" && !diagram.Contains("flowchart"))
            {
                diagram = $"flowchart TD\n{diagram}";
            }
            
            // Add styling and formatting based on diagram type
            switch (diagramType.ToLower())
            {
                case "mindmap":
                    // Add custom styling for mindmap nodes if not present
                    if (!diagram.Contains("style"))
                    {
                        StringBuilder enhancedDiagram = new StringBuilder(diagram);
                        enhancedDiagram.AppendLine();
                        enhancedDiagram.AppendLine("  %% Styling for mindmap nodes");
                        enhancedDiagram.AppendLine("  style root fill:#f96,stroke:#333,stroke-width:2px");
                        diagram = enhancedDiagram.ToString();
                    }
                    break;
                    
                case "flowchart":
                    // Add direction and styling if not present
                    if (!diagram.Contains("style"))
                    {
                        var lines = diagram.Split('\n').ToList();
                        // Insert styling after the first line
                        if (lines.Count > 1)
                        {
                            lines.Insert(1, "  %% Node styling");
                            lines.Insert(2, "  classDef important fill:#f96,stroke:#333,stroke-width:2px");
                            lines.Insert(3, "  classDef normal fill:#9cf,stroke:#333");
                            // Look for node definitions and add classes
                            for (int i = 3; i < lines.Count; i++)
                            {
                                if (lines[i].Contains("-->") && !lines[i].Contains("class"))
                                {
                                    // Extract node IDs
                                    var match = Regex.Match(lines[i], @"^\s*([A-Za-z0-9_]+)\s*-->");
                                    if (match.Success)
                                    {
                                        string nodeId = match.Groups[1].Value;
                                        lines.Add($"  class {nodeId} normal");
                                    }
                                }
                            }
                            diagram = string.Join('\n', lines);
                        }
                    }
                    break;
                    
                case "sequence":
                    // Add styling for sequence diagrams if not present
                    if (!diagram.Contains("participant"))
                    {
                        diagram = "sequenceDiagram\n    autonumber\n" + diagram;
                    }
                    break;
            }
            
            return diagram;
        }
        
        public async Task<string> ExpandConcept(string conceptName, ConceptNode concept, string diagramType, IProgress<int>? progress = null)
        {
            _logger.LogInformation($"Expanding concept: {conceptName}");
            progress?.Report(10);
            
            string systemPrompt = PromptConstants.ConceptExpansionSystemPrompt;
            string conceptJson = JsonSerializer.Serialize(concept);
            string userPrompt = PromptConstants.GetConceptExpansionPrompt(conceptName, conceptJson, diagramType);
            
            progress?.Report(20);
            string expandedDiagram = await GetSystemCompletion(systemPrompt, userPrompt, progress: new Progress<int>(p => 
            {
                // Scale the progress from 20-80% range
                progress?.Report(20 + (p * 60 / 100));
            }));
            
            progress?.Report(80);
            
            // Extract just the mermaid content if needed
            if (expandedDiagram.Contains("```mermaid") && expandedDiagram.Contains("```"))
            {
                int startIndex = expandedDiagram.IndexOf("```mermaid") + 10;
                int endIndex = expandedDiagram.LastIndexOf("```");
                expandedDiagram = expandedDiagram.Substring(startIndex, endIndex - startIndex).Trim();
            }
            
            // Enhance the diagram with better styling
            expandedDiagram = EnhanceDiagramFormatting(expandedDiagram, diagramType);
            
            progress?.Report(100);
            return $"```mermaid\n{expandedDiagram}\n```";
        }
        
        public async Task<string> ExportDiagram(string mermaidSyntax, string format)
        {
            // Process the mermaid diagram asynchronously
            await Task.Yield();
            
            string result;
            
            switch (format.ToLower())
            {
                case "html":
                    // Clean up and simplify the Mermaid syntax before embedding
                    string cleanSyntax = CleanMermaidSyntaxForExport(mermaidSyntax);
                    
                    // Create a more robust HTML template with error handling
                    result = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Mermaid Diagram</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@9.3.0/dist/mermaid.min.js""></script>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .container {{
            background-color: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 20px;
            margin: 0 auto;
            max-width: 1200px;
        }}
        .mermaid {{
            text-align: center;
        }}
        h1 {{
            color: #333;
            text-align: center;
        }}
        .error {{
            color: red;
            font-family: monospace;
            white-space: pre-wrap;
            text-align: left;
            padding: 10px;
            background-color: #fff;
            border: 1px solid #ddd;
            margin-top: 20px;
        }}
        .debug {{
            font-family: monospace;
            margin-top: 20px;
            padding: 10px;
            background-color: #f8f8f8;
            border: 1px solid #ddd;
            white-space: pre-wrap;
            display: none;
        }}
        .debug-toggle {{
            cursor: pointer;
            color: blue;
            text-decoration: underline;
            margin-top: 10px;
            display: block;
        }}
    </style>
    <script>
        // Initialize mermaid with error handling
        mermaid.initialize({{ 
            startOnLoad: true,
            theme: 'default',
            securityLevel: 'loose',
            logLevel: 'error',
            flowchart: {{ 
                htmlLabels: true,
                useMaxWidth: true
            }},
            er: {{
                useMaxWidth: true
            }}
        }});

        // Custom error handling for Mermaid
        document.addEventListener('DOMContentLoaded', function() {{
            try {{
                const element = document.querySelector('.mermaid');
                const syntaxElement = document.getElementById('syntax');
                syntaxElement.textContent = element.textContent.trim();
                
                // Register error handler
                mermaid.parseError = function(err, hash) {{
                    const errorElement = document.getElementById('error');
                    errorElement.style.display = 'block';
                    errorElement.textContent = 'Syntax error: ' + err + '\n\nLine: ' + (hash && hash.line);
                    
                    // Show debug view automatically on error
                    document.getElementById('debug').style.display = 'block';
                }};
                
                // Register click handler for debug toggle
                document.getElementById('debug-toggle').addEventListener('click', function() {{
                    const debug = document.getElementById('debug');
                    debug.style.display = debug.style.display === 'none' ? 'block' : 'none';
                }});
            }} catch (e) {{
                console.error('Error setting up Mermaid:', e);
            }}
        }});
    </script>
</head>
<body>
    <div class=""container"">
        <h1>Generated Diagram</h1>
        <div class=""mermaid"">
{cleanSyntax}
        </div>
        <div id=""error"" class=""error"" style=""display:none;""></div>
        <span id=""debug-toggle"" class=""debug-toggle"">Toggle Debug View</span>
        <div id=""debug"" class=""debug"">
            <strong>Raw Mermaid Syntax:</strong>
            <pre id=""syntax""></pre>
        </div>
    </div>
</body>
</html>";
                    break;
                    
                case "png":
                case "svg":
                case "pdf":
                    result = $"Export to {format} is not implemented yet. Please use the HTML export and take a screenshot or use browser print function.";
                    break;
                    
                default:
                    result = mermaidSyntax;
                    break;
            }
            
            return result;
        }

        private string CleanMermaidSyntaxForExport(string mermaidSyntax)
        {
            // Remove the markdown code block markers
            string cleanSyntax = mermaidSyntax
                .Replace("```mermaid", "")
                .Replace("```", "")
                .Trim();
            
            // Remove any empty lines at the beginning and end
            cleanSyntax = Regex.Replace(cleanSyntax, @"^(\s*\n)+", "");
            cleanSyntax = Regex.Replace(cleanSyntax, @"(\n\s*)+$", "");
            
            // Apply diagram-specific fixes
            if (cleanSyntax.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
            {
                // Create a simplified mindmap that works better with HTML export
                StringBuilder simplifiedMindmap = new StringBuilder("mindmap\n");
                string[] lines = cleanSyntax.Split('\n');
                
                // Process each line after the mindmap declaration
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    
                    // Skip style and class definitions
                    if (line.TrimStart().StartsWith("style") || 
                        line.TrimStart().StartsWith("classDef") ||
                        line.TrimStart().StartsWith("%%"))
                    {
                        continue;
                    }
                    
                    // Remove problematic content in each line
                    
                    // Remove style information
                    line = Regex.Replace(line, @"\[Style:.*?\]", "");
                    
                    // Remove quotes but preserve the content
                    line = Regex.Replace(line, @"""([^""]*)""", "$1");
                    
                    // Fix node format - use simpler notation
                    if (line.Contains("((") && line.Contains("))"))
                    {
                        // Simplify double parenthesis notation
                        line = Regex.Replace(line, @"\(\(([^)]+)\)\)", "[$1]");
                    }
                    
                    simplifiedMindmap.AppendLine(line);
                }
                
                cleanSyntax = simplifiedMindmap.ToString();
            }
            else if (cleanSyntax.Contains("flowchart") || cleanSyntax.StartsWith("graph", StringComparison.OrdinalIgnoreCase))
            {
                // Fix for flowcharts - existing code...
            }
            else if (cleanSyntax.Contains("sequenceDiagram"))
            {
                // Fix for sequence diagrams - existing code...
            }
            
            // ...existing code...
            
            return cleanSyntax;
        }
        
        private string GenerateCacheKey(string systemPrompt, string userPrompt, int maxTokens)
        {
            // Create a simple hash key from the inputs
            return $"{systemPrompt.GetHashCode()}_{userPrompt.GetHashCode()}_{maxTokens}";
        }
        
        private async Task<string> ExecuteSystemCompletion(string systemPrompt, string userPrompt, double temperature, int maxTokens)
        {
            try
            {
                var requestUrl = $"openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";
                
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_completion_tokens = maxTokens  // Using only max_completion_tokens, removed temperature
                };
                
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var stringContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug($"Sending request to {_httpClient.BaseAddress}{requestUrl}");
                
                // Use a cancellation token with timeout for the request
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
                var response = await _httpClient.PostAsync(requestUrl, stringContent, cts.Token);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API Error: {(int)response.StatusCode} {response.StatusCode}");
                    _logger.LogError($"Response details: {responseContent}");
                    throw new HttpRequestException($"Azure OpenAI API returned {(int)response.StatusCode}: {responseContent}");
                }
                
                var responseJson = JsonSerializer.Deserialize<JsonDocument>(responseContent);
                
                if (responseJson == null)
                {
                    throw new InvalidOperationException("Failed to parse API response as JSON");
                }
                
                string resultContent = responseJson.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
                
                if (string.IsNullOrWhiteSpace(resultContent))
                {
                    return "Error: The model returned an empty response.";
                }
                
                return resultContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in API request");
                throw;
            }
        }
    }
}
