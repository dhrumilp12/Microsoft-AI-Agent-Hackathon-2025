using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using DiagramGenerator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DiagramGenerator.Services
{
    public class DiagramInteractionService : IDiagramInteractionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiagramInteractionService> _logger;
        private readonly IWhiteboardIntegrationService _whiteboardService;

        public DiagramInteractionService(
            IConfiguration configuration, 
            ILogger<DiagramInteractionService> logger,
            IWhiteboardIntegrationService whiteboardService)
        {
            _configuration = configuration;
            _logger = logger;
            _whiteboardService = whiteboardService;
        }

        public async Task StartInteractionAsync(Diagram diagram)
        {
            Console.WriteLine($"Interactive mode for diagram: {diagram.Title}");
            Console.WriteLine("Available commands:");
            Console.WriteLine("- list: Show all elements in the diagram");
            Console.WriteLine("- show [elementId]: Show details about a specific element");
            Console.WriteLine("- breakdown [elementId]: Break down a specific element into more detailed components");
            Console.WriteLine("- modify [text instruction]: Modify the diagram based on natural language instructions");
            Console.WriteLine("- exit: Return to main menu");

            bool keepInteracting = true;
            while (keepInteracting)
            {
                Console.Write("\nEnter command: ");
                var command = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(command))
                    continue;

                if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    keepInteracting = false;
                }
                else if (command.Equals("list", StringComparison.OrdinalIgnoreCase))
                {
                    ListElements(diagram);
                }
                else if (command.StartsWith("show ", StringComparison.OrdinalIgnoreCase))
                {
                    var elementId = command.Substring(5).Trim();
                    ShowElementDetails(diagram, elementId);
                }
                else if (command.StartsWith("breakdown ", StringComparison.OrdinalIgnoreCase))
                {
                    var elementId = command.Substring(10).Trim();
                    diagram = await BreakdownElementAsync(diagram, elementId);
                }
                else if (command.StartsWith("modify ", StringComparison.OrdinalIgnoreCase))
                {
                    var instruction = command.Substring(7).Trim();
                    diagram = await ModifyDiagramAsync(diagram, instruction);
                }
                else
                {
                    Console.WriteLine("Unknown command. Type 'exit' to return to main menu.");
                }
            }
        }

        private void ListElements(Diagram diagram)
        {
            Console.WriteLine("\nDiagram Elements:");
            foreach (var element in diagram.Elements)
            {
                Console.WriteLine($"- [{element.Id}] {element.Label} ({element.Type})");
            }
        }

        private void ShowElementDetails(Diagram diagram, string elementId)
        {
            var element = FindElementById(diagram.Elements, elementId);
            if (element == null)
            {
                Console.WriteLine($"Element with ID {elementId} not found.");
                return;
            }

            Console.WriteLine($"\nElement: {element.Label} ({element.Type})");
            Console.WriteLine($"ID: {element.Id}");
            
            if (element.Properties.Any())
            {
                Console.WriteLine("Properties:");
                foreach (var prop in element.Properties)
                {
                    Console.WriteLine($"  {prop.Key}: {prop.Value}");
                }
            }
            
            if (element.ConnectedToIds.Any())
            {
                Console.WriteLine("Connected to:");
                foreach (var connectedId in element.ConnectedToIds)
                {
                    var connectedElement = FindElementById(diagram.Elements, connectedId);
                    Console.WriteLine($"  - {connectedElement?.Label ?? "Unknown"} ({connectedId})");
                }
            }
            
            if (element.Children.Any())
            {
                Console.WriteLine("Children:");
                foreach (var child in element.Children)
                {
                    Console.WriteLine($"  - {child.Label} ({child.Type})");
                }
            }
        }

        private DiagramElement? FindElementById(List<DiagramElement> elements, string id)
        {
            foreach (var element in elements)
            {
                if (element.Id == id)
                    return element;
                
                var childMatch = FindElementById(element.Children, id);
                if (childMatch != null)
                    return childMatch;
            }
            
            return null;
        }

        public async Task<Diagram> BreakdownElementAsync(Diagram diagram, string elementId)
        {
            var element = FindElementById(diagram.Elements, elementId);
            if (element == null)
            {
                Console.WriteLine($"Element with ID {elementId} not found.");
                return diagram;
            }

            Console.WriteLine($"Breaking down element: {element.Label}...");

            // Get API keys from environment variables (loaded from .env)
            var openAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
            var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");

            if (string.IsNullOrEmpty(openAiKey) || string.IsNullOrEmpty(openAiEndpoint) || 
                string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Environment variables not found. Falling back to configuration values.");
                
                // Fallback to configuration if environment variables are not set
                openAiKey = _configuration["Azure:OpenAI:Key"];
                openAiEndpoint = _configuration["Azure:OpenAI:Endpoint"];
                deploymentName = _configuration["Azure:OpenAI:DeploymentName"];

                if (string.IsNullOrEmpty(openAiKey) || string.IsNullOrEmpty(openAiEndpoint) || 
                    string.IsNullOrEmpty(deploymentName))
                {
                    Console.WriteLine("OpenAI configuration is missing. Unable to break down element.");
                    return diagram;
                }
            }

            // Create OpenAI client without specifying API version - will use the latest version
            _logger.LogInformation("Creating Azure OpenAI client with default API version");
            var client = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                Temperature = 0.3f,
                MaxTokens = 1000
            };
            
            // Add messages to the chat
            chatCompletionsOptions.Messages.Add(
                new ChatMessage(ChatRole.System, 
                    "You are an expert at creating detailed breakdowns of concepts. Given a higher-level concept, break it down into its sub-components.")
            );
            chatCompletionsOptions.Messages.Add(
                new ChatMessage(ChatRole.User, 
                    $"Break down this concept: '{element.Label}' into its key sub-components. Return the result as a JSON array of objects with 'label', 'type', and 'description' fields.")
            );

            try
            {
                var response = await client.GetChatCompletionsAsync(deploymentName, chatCompletionsOptions);
                var breakdownJson = response.Value.Choices[0].Message.Content;
                
                Console.WriteLine("Generated breakdown components:");
                Console.WriteLine(breakdownJson);
                
                // In a real implementation, parse the JSON and update the diagram
                // For now, we'll just add some sample child elements
                for (int i = 1; i <= 3; i++)
                {
                    element.Children.Add(new DiagramElement($"{element.Label} Component {i}", ElementType.Node)
                    {
                        Properties = { ["Description"] = $"Auto-generated component {i} of {element.Label}" }
                    });
                }
                
                await _whiteboardService.UpdateDiagramAsync(diagram);
                
                Console.WriteLine("Breakdown complete and diagram updated.");
                return diagram;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error breaking down element");
                Console.WriteLine($"Error: {ex.Message}");
                return diagram;
            }
        }

        public async Task<Diagram> ModifyDiagramAsync(Diagram diagram, string command)
        {
            Console.WriteLine($"Modifying diagram based on instruction: '{command}'...");

            // Get API keys from environment variables (loaded from .env)
            var openAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
            var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");

            if (string.IsNullOrEmpty(openAiKey) || string.IsNullOrEmpty(openAiEndpoint) ||
                string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Environment variables not found. Falling back to configuration values.");
                
                // Fallback to configuration if environment variables are not set
                openAiKey = _configuration["Azure:OpenAI:Key"];
                openAiEndpoint = _configuration["Azure:OpenAI:Endpoint"];
                deploymentName = _configuration["Azure:OpenAI:DeploymentName"];

                if (string.IsNullOrEmpty(openAiKey) || string.IsNullOrEmpty(openAiEndpoint) ||
                    string.IsNullOrEmpty(deploymentName))
                {
                    Console.WriteLine("OpenAI configuration is missing. Unable to modify diagram.");
                    return diagram;
                }
            }

            // Create OpenAI client without specifying API version - will use the latest version
            var client = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                Temperature = 0.3f,
                MaxTokens = 1000
            };
            
            chatCompletionsOptions.Messages.Add(
                new ChatMessage(ChatRole.System, 
                    "You are an expert at modifying diagrams based on natural language instructions. Given a diagram structure and a modification instruction, describe how the diagram should be changed.")
            );
            chatCompletionsOptions.Messages.Add(
                new ChatMessage(ChatRole.User, 
                    $"Current diagram structure: {diagram.RawContent}\n\nModify the diagram according to this instruction: {command}")
            );

            try
            {
                var response = await client.GetChatCompletionsAsync(deploymentName, chatCompletionsOptions);
                var modificationDescription = response.Value.Choices[0].Message.Content;
                
                Console.WriteLine("Diagram modification plan:");
                Console.WriteLine(modificationDescription);
                
                // In a real implementation, we would parse the AI's instructions and modify the diagram
                // For this example, we'll just acknowledge the change
                Console.WriteLine("In a full implementation, the diagram would be modified based on the AI's instructions.");
                await _whiteboardService.UpdateDiagramAsync(diagram);
                
                return diagram;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying diagram");
                Console.WriteLine($"Error: {ex.Message}");
                return diagram;
            }
        }
    }
}
