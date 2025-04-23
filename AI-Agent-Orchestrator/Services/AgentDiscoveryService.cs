using AI_Agent_Orchestrator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Reflection;

namespace AI_Agent_Orchestrator.Services;

public class AgentDiscoveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentDiscoveryService> _logger;
    private List<AgentInfo> _discoveredAgents = new();
    private List<AgentWorkflow> _discoveredWorkflows = new();

    public AgentDiscoveryService(IConfiguration configuration, ILogger<AgentDiscoveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<AgentInfo>> DiscoverAgentsAsync(string targetLanguage)
    {
        _logger.LogInformation("Discovering available AI agents...");
        
        // Get the root directory - go up from the current assembly location to find the solution root
        string? currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        // Make sure currentDir is not null before using it
        if (currentDir == null)
        {
            _logger.LogError("Could not determine assembly location");
            return _discoveredAgents;
        }
        
        string rootDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
        
        _logger.LogInformation($"Root directory: {rootDir}");
        
        // Load predefined agents from configuration
        var agentsSection = _configuration.GetSection("Agents");
        if (agentsSection.Exists())
        {
            // Load from configuration if available
            _discoveredAgents = agentsSection.Get<List<AgentInfo>>() ?? new List<AgentInfo>();
            _logger.LogInformation($"Loaded {_discoveredAgents.Count} agents from configuration");
        }
        else
        {
            // Define hardcoded agents as fallback
            _discoveredAgents = new List<AgentInfo>
            {
                new AgentInfo 
                {
                    Name = "Vocabulary Bank & Flashcards Generator",
                    Description = "Creates flashcards from educational content with definitions and examples",
                    ExecutablePath = "dotnet",
                    WorkingDirectory = Path.GetFullPath(Path.Combine(rootDir, "..", "AI-Agent-VocabularyBank")),
                    Arguments = new List<string> { "run", "--project", "." },
                    Keywords = new[] { "vocabulary", "flashcards", "education", "learning", "terms" }
                },
                new AgentInfo 
                {
                    Name = "AI Summarization Agent",
                    Description = "Summarizes text content automatically",
                    ExecutablePath = "dotnet",
                    WorkingDirectory = Path.GetFullPath(Path.Combine(rootDir, "..", "AI-Summarization-agent")),
                    Arguments = new List<string> { "run", "--project", "." },
                    Keywords = new[] { "summary", "summarize", "text", "condense" }
                },
                new AgentInfo 
                {
                    Name = "Speech Translator",
                    Description = "Translates spoken language in real-time",
                    ExecutablePath = "dotnet",
                    WorkingDirectory = Path.GetFullPath(Path.Combine(rootDir, "..", "AI-agent-SpeechTranslator")),
                    Arguments = new List<string> { "run", "--project", ".", "--", targetLanguage },
                    Keywords = new[] { "speech", "translate", "language", "audio", "record", "recording" }
                },
                new AgentInfo 
                {
                    Name = "Diagram Generator",
                    Description = "Generates visual diagrams from text content",
                    ExecutablePath = "dotnet",
                    WorkingDirectory = Path.GetFullPath(Path.Combine(rootDir, "..", "AI-agent-DiagramGenerator")),
                    Arguments = new List<string> { "run", "--project", "." },
                    Keywords = new[] { "diagram", "visual", "chart", "mindmap", "flowchart" }
                },
                new AgentInfo 
                {
                    Name = "Classroom Board Capture",
                    Description = "Captures, analyzes, and translates whiteboard content",
                    ExecutablePath = "dotnet",
                    WorkingDirectory = Path.GetFullPath(Path.Combine(rootDir, "..", "AI-Agent-BoardCapture")),
                    Arguments = new List<string> { "run", "--project", ".", "--", targetLanguage },
                    Keywords = new[] { "whiteboard", "capture", "classroom", "ocr", "image" }
                }
            };
            
            _logger.LogInformation($"Created {_discoveredAgents.Count} default agents");
            
            // Verify all working directories exist
            foreach (var agent in _discoveredAgents)
            {
                if (!Directory.Exists(agent.WorkingDirectory))
                {
                    _logger.LogWarning($"Working directory not found for {agent.Name}: {agent.WorkingDirectory}");
                    
                    // Try an alternative path format
                    string altPath = Path.GetFullPath(Path.Combine(rootDir, agent.Name));
                    if (Directory.Exists(altPath))
                    {
                        _logger.LogInformation($"Found alternative path for {agent.Name}: {altPath}");
                        agent.WorkingDirectory = altPath;
                    }
                    else
                    {
                        _logger.LogWarning($"Alternative path not found either: {altPath}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Verified path for {agent.Name}: {agent.WorkingDirectory}");
                }
            }
            
            // Save discovered agents to a local file for future use
            var agentsFile = Path.Combine(AppContext.BaseDirectory, "agents.json");
            await File.WriteAllTextAsync(agentsFile, JsonSerializer.Serialize(_discoveredAgents, new JsonSerializerOptions { WriteIndented = true }));
        }
        
        return _discoveredAgents;
    }
    
    public async Task<List<AgentWorkflow>> DiscoverWorkflowsAsync(string targetLanguage)
    {
        _logger.LogInformation("Discovering available AI agent workflows...");
        
        // First ensure we have all agents loaded
        if (_discoveredAgents.Count == 0)
        {
            await DiscoverAgentsAsync(targetLanguage);
        }
        
        // Load predefined workflows from configuration
        var workflowsSection = _configuration.GetSection("Workflows");
        if (workflowsSection.Exists())
        {
            // Load from configuration if available
            _discoveredWorkflows = workflowsSection.Get<List<AgentWorkflow>>() ?? new List<AgentWorkflow>();
            _logger.LogInformation($"Loaded {_discoveredWorkflows.Count} workflows from configuration");
        }
        else
        {
            // Create predefined workflows based on the diagram
            _discoveredWorkflows = new List<AgentWorkflow>();
            
            // Find the Speech Translator and Vocabulary Bank agents
            var speechTranslator = _discoveredAgents.FirstOrDefault(a => 
                a.Name.Contains("Speech Translator", StringComparison.OrdinalIgnoreCase));
            var vocabularyBank = _discoveredAgents.FirstOrDefault(a => 
                a.Name.Contains("Vocabulary Bank", StringComparison.OrdinalIgnoreCase));
            
            if (speechTranslator != null && vocabularyBank != null)
            {
                // Create a workflow that connects them with more comprehensive keywords
                _discoveredWorkflows.Add(new AgentWorkflow
                {
                    Name = "Audio Translation to Vocabulary",
                    Description = "Records and translates speech, then generates vocabulary flashcards",
                    Agents = new List<AgentInfo> { speechTranslator, vocabularyBank },
                    OutputMappings = new Dictionary<string, string> { 
                        { "Speech Translator", "Output/recognized_transcript.txt" } 
                    },
                    Keywords = new[] { 
                        "audio", "speech", "translate", "translator", 
                        "vocabulary", "flashcards", "flashcard", "word", 
                        "language", "learning", "education"
                    },
                    Category = "Language Learning"
                });
                
                _logger.LogInformation("Created Speech-to-Vocabulary workflow");
            }
            
            // Save discovered workflows to a local file for future use
            var workflowsFile = Path.Combine(AppContext.BaseDirectory, "workflows.json");
            await File.WriteAllTextAsync(workflowsFile, JsonSerializer.Serialize(_discoveredWorkflows, new JsonSerializerOptions { WriteIndented = true }));
        }
        
        return _discoveredWorkflows;
    }
}
