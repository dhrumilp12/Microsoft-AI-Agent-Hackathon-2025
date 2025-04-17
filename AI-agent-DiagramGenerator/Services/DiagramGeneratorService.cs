using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using DiagramGenerator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DiagramGenerator.Services
{
    public class DiagramGeneratorService : IDiagramGeneratorService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiagramGeneratorService> _logger;
        private readonly AzureOpenAIClientService _azureOpenAIClient;

        public DiagramGeneratorService(
            IConfiguration configuration, 
            ILogger<DiagramGeneratorService> logger,
            AzureOpenAIClientService azureOpenAIClient)
        {
            _configuration = configuration;
            _logger = logger;
            _azureOpenAIClient = azureOpenAIClient;
        }

        public async Task<Diagram> GenerateDiagramAsync(List<string> concepts, string transcript)
        {
            var conceptsText = string.Join(", ", concepts);
            _logger.LogInformation($"Generating diagram for concepts: {conceptsText}");
            
            try
            {
                // System prompt with specific instructions to ensure valid JSON
                string systemPrompt = 
                    "You are an expert at creating visual diagrams from concepts. Analyze the provided concepts " +
                    "and transcript to determine the most suitable diagram type (Flowchart, MindMap, EntityRelationship, " +
                    "SequenceDiagram, ClassDiagram, etc.) and create a structured diagram representation. " +
                    "Return your response as a VALID AND COMPLETE JSON object with the following structure: " +
                    "{ \"title\": \"Diagram title\", \"type\": \"DiagramType\", \"elements\": [Array of diagram elements with connections] }. " +
                    "Ensure your JSON is complete, properly closed, and does not exceed length limits.";
                
                string userPrompt = $"Create a diagram based on these concepts: {conceptsText}. Additional context from the transcript: {transcript}";
                
                // Get response from Azure OpenAI
                var diagramJson = await _azureOpenAIClient.GetChatCompletionAsync(systemPrompt, userPrompt, 0.3, 2000);
                
                // If response starts with error, log it and return generic diagram
                if (diagramJson.StartsWith("Error:"))
                {
                    _logger.LogError(diagramJson);
                    return new Diagram("Error Diagram", DiagramType.Generic);
                }
                
                // Clean up the JSON if needed
                diagramJson = CleanJsonResponse(diagramJson);
                
                // Parse JSON to create diagram
                try
                {
                    var diagramData = JsonSerializer.Deserialize<JsonElement>(diagramJson);
                    var title = diagramData.GetProperty("title").GetString() ?? "Untitled Diagram";
                    var typeStr = diagramData.GetProperty("type").GetString() ?? "Generic";
                    
                    if (!Enum.TryParse<DiagramType>(typeStr, true, out var diagramType))
                    {
                        diagramType = DiagramType.Generic;
                    }
                    
                    var diagram = new Diagram(title, diagramType)
                    {
                        RawContent = diagramJson
                    };
                    
                    // Process elements
                    var elementsArray = diagramData.GetProperty("elements").EnumerateArray();
                    foreach (var element in elementsArray)
                    {
                        var elementObj = ProcessDiagramElement(element);
                        diagram.Elements.Add(elementObj);
                    }
                    
                    _logger.LogInformation($"Generated {diagram.Type} diagram with {diagram.Elements.Count} elements");
                    return diagram;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Failed to parse diagram JSON: {diagramJson}");
                    return new Diagram("Error Diagram", DiagramType.Generic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating diagram");
                return new Diagram("Error Diagram", DiagramType.Generic);
            }
        }

        private string CleanJsonResponse(string json)
        {
            try
            {
                _logger.LogDebug("Original JSON length: " + json.Length);

                // Try to extract just the JSON portion of the response using regex
                var match = Regex.Match(json, @"\{[\s\S]*\}");
                if (match.Success)
                {
                    json = match.Value;
                }
                
                // Advanced repair for malformed JSON structure
                json = FixMalformedJson(json);
                
                _logger.LogDebug("Repaired JSON length: " + json.Length);
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while cleaning JSON response");
                return json; // Return original if repair fails
            }
        }

        private string FixMalformedJson(string json)
        {
            // Find the last valid object or array closing position
            int lastValidBracePosition = -1;
            int lastValidBracketPosition = -1;
            int braceDepth = 0;
            int bracketDepth = 0;
            bool inString = false;
            bool escapeChar = false;
            
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                
                // Handle string literals
                if (c == '"' && !escapeChar)
                {
                    inString = !inString;
                }
                
                escapeChar = c == '\\' && !escapeChar;
                
                // Only count braces and brackets outside of strings
                if (!inString)
                {
                    if (c == '{')
                    {
                        braceDepth++;
                    }
                    else if (c == '}')
                    {
                        braceDepth--;
                        if (braceDepth >= 0)
                        {
                            lastValidBracePosition = i;
                        }
                        else
                        {
                            // Unmatched closing brace - this is an error
                            // Consider truncating here
                            _logger.LogWarning($"Unmatched closing brace at position {i}");
                            return json.Substring(0, i);
                        }
                    }
                    else if (c == '[')
                    {
                        bracketDepth++;
                    }
                    else if (c == ']')
                    {
                        bracketDepth--;
                        if (bracketDepth >= 0)
                        {
                            lastValidBracketPosition = i;
                        }
                        else
                        {
                            // Unmatched closing bracket - this is an error
                            _logger.LogWarning($"Unmatched closing bracket at position {i}");
                            return json.Substring(0, i);
                        }
                    }
                }
            }
            
            // Check for unclosed structures and repair them
            if (braceDepth > 0 || bracketDepth > 0)
            {
                _logger.LogInformation($"Repairing JSON: unclosed braces({braceDepth}), brackets({bracketDepth})");
                
                // Find the last valid position based on structure
                int truncateAt = json.Length;
                if (lastValidBracePosition > 0 && braceDepth > 0 && lastValidBracketPosition > 0 && bracketDepth > 0)
                {
                    // Determine which structure should be closed first based on nesting
                    truncateAt = Math.Min(lastValidBracePosition + 1, lastValidBracketPosition + 1);
                }
                else if (lastValidBracePosition > 0 && braceDepth > 0)
                {
                    truncateAt = lastValidBracePosition + 1;
                }
                else if (lastValidBracketPosition > 0 && bracketDepth > 0)
                {
                    truncateAt = lastValidBracketPosition + 1;
                }
                
                // Truncate to a valid position
                if (truncateAt < json.Length)
                {
                    json = json.Substring(0, truncateAt);
                }
                
                // Find incomplete objects/arrays and repair them
                string[] patterns = new[] {
                    @",\s*\}$", // trailing comma before closing brace
                    @",\s*\]$", // trailing comma before closing bracket
                    @"\{\s*$",  // open brace at the end
                    @"\[\s*$",  // open bracket at the end
                };
                
                foreach (var pattern in patterns)
                {
                    var patternMatch = Regex.Match(json, pattern);
                    if (patternMatch.Success)
                    {
                        if (pattern == @"\{\s*$")
                        {
                            json = json.Substring(0, patternMatch.Index) + "{}";
                        }
                        else if (pattern == @"\[\s*$")
                        {
                            json = json.Substring(0, patternMatch.Index) + "[]";
                        }
                        else
                        {
                            json = json.Substring(0, patternMatch.Index) + (pattern.Contains('}') ? "}" : "]");
                        }
                    }
                }
                
                // Now add closing braces/brackets as needed
                json += new string('}', braceDepth) + new string(']', bracketDepth);
            }
            
            // Fix specific errors found in the JSON
            // Remove any trailing commas in arrays or objects (common issue)
            json = Regex.Replace(json, @",(\s*[\}\]])", "$1");
            
            // Fix double closing braces or brackets that aren't properly nested
            json = Regex.Replace(json, @"\}\}(\s*\])", "}}]");
            
            return json;
        }

        private DiagramElement ProcessDiagramElement(JsonElement element)
        {
            var label = element.GetProperty("label").GetString() ?? "Unnamed";
            var typeStr = element.GetProperty("type").GetString() ?? "Node";
            
            if (!Enum.TryParse<ElementType>(typeStr, true, out var elementType))
            {
                elementType = ElementType.Node;
            }
            
            var diagramElement = new DiagramElement(label, elementType);
            
            // Process properties if they exist
            if (element.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    diagramElement.Properties[property.Name] = property.Value.ToString();
                }
            }
            
            // Process connections if they exist
            if (element.TryGetProperty("connections", out var connections))
            {
                foreach (var connection in connections.EnumerateArray())
                {
                    diagramElement.ConnectedToIds.Add(connection.GetString() ?? string.Empty);
                }
            }
            
            // Process children if they exist
            if (element.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    diagramElement.Children.Add(ProcessDiagramElement(child));
                }
            }
            
            return diagramElement;
        }

        public async Task<string> GenerateDiagramMarkupAsync(Diagram diagram)
        {
            try
            {
                // System prompt that instructs the model on its role
                string systemPrompt = "You are an expert at creating Mermaid.js diagram markup. Convert the provided diagram structure into valid Mermaid.js syntax.";
                
                // User prompt that contains the actual content to process
                string userPrompt = $"Convert this diagram to Mermaid.js markup: {diagram.RawContent}";
                
                // Get response from Azure OpenAI
                var mermaidMarkup = await _azureOpenAIClient.GetChatCompletionAsync(systemPrompt, userPrompt, 0.1, 1000);
                
                // If response starts with error, log it and return empty string
                if (mermaidMarkup.StartsWith("Error:"))
                {
                    _logger.LogError(mermaidMarkup);
                    return string.Empty;
                }
                
                return mermaidMarkup;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Mermaid markup");
                return string.Empty;
            }
        }
    }
}
