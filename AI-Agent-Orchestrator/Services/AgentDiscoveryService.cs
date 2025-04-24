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
            
            // Find all of the agents
            var speechTranslator = _discoveredAgents.FirstOrDefault(a => 
                a.Name.Contains("Speech Translator", StringComparison.OrdinalIgnoreCase));
            var vocabularyBank = _discoveredAgents.FirstOrDefault(a => 
                a.Name.Contains("Vocabulary Bank", StringComparison.OrdinalIgnoreCase));
            var classroomBoardCapture = _discoveredAgents.FirstOrDefault(a => 
                a.Name.Contains("Classroom Board Capture", StringComparison.OrdinalIgnoreCase));
            var diagramGenerator = _discoveredAgents.FirstOrDefault(a =>
                a.Name.Contains("Diagram Generator", StringComparison.OrdinalIgnoreCase));
            var summarizationAgent = _discoveredAgents.FirstOrDefault(a =>
                a.Name.Contains("AI Summarization Agent", StringComparison.OrdinalIgnoreCase));

            // Create the complete workflow with summarization if all three agents are available
            if (speechTranslator != null && vocabularyBank != null && summarizationAgent != null && diagramGenerator != null)
            {
                // Create a comprehensive workflow that connects all four agents
                _discoveredWorkflows.Add(new AgentWorkflow
                {
                    Name = "Complete Audio Learning Assistant",
                    Description = "Records and translates speech, generates vocabulary flashcards, creates a summarized version of the content, and finally generates a visual diagram of key concepts",
                    Agents = new List<AgentInfo> { speechTranslator, vocabularyBank, summarizationAgent, diagramGenerator },
                    OutputMappings = new Dictionary<string, List<string>> { 
                        { "Speech Translator", new List<string> {
                            "Output/recognized_transcript.txt",
                            "Output/translated_transcript.txt"
                        } },
                        { "Vocabulary Bank & Flashcards Generator", new List<string> {
                            "Output/recognized_transcript_flashcards.json"
                        } },
                        { "AI Summarization Agent", new List<string> {
                            "data/outputs/summary_*.json"
                        } }
                    },
                    Keywords = new[] { 
                        "audio", "speech", "translate", "vocabulary", "summary", 
                        "summarize", "flashcards", "comprehensive", "complete", 
                        "learning", "education", "assistant", "diagram", "visual"
                    },
                    Category = "Education Assistant"
                });
                
                // Update the summarization agent's arguments
                if (_discoveredWorkflows.Count > 0)
                {
                    var workflow = _discoveredWorkflows.FirstOrDefault(w => w.Name.Contains("Complete Audio Learning Assistant"));
                    if (workflow != null)
                    {
                        var summaryAgent = workflow.Agents.FirstOrDefault(a => a.Name.Contains("AI Summarization Agent"));
                        if (summaryAgent != null)
                        {
                            summaryAgent.Arguments = new List<string> { 
                                "run", "--project", ".",
                                "--", 
                                "../AI-agent-SpeechTranslator/Output/translated_transcript.txt",
                                "../AI-agent-SpeechTranslator/Output/recognized_transcript_flashcards.json" 
                            };
                            _logger.LogInformation("Updated summarization agent arguments to use translated transcript and vocabulary data");
                        }
                        
                        // Update the diagram generator arguments
                        var diagramAgent = workflow.Agents.FirstOrDefault(a => a.Name.Contains("Diagram Generator"));
                        if (diagramAgent != null)
                        {
                            diagramAgent.Arguments = new List<string> { 
                                "run", "--project", ".",
                                "--", 
                                "../AI-agent-SpeechTranslator/Output/translated_transcript.txt",
                                "../AI-Summarization-agent/data/outputs/latest_summary.json" 
                            };
                            _logger.LogInformation("Updated diagram generator arguments to use translated transcript and summary output");
                        }
                    }
                }
                
                _logger.LogInformation("Created comprehensive audio learning workflow with translation, vocabulary, summarization, and diagram generation");
            }

            if (classroomBoardCapture != null && diagramGenerator != null && summarizationAgent != null)
            {
                // Create a list of agents making sure to check for null values
                var agents = new List<AgentInfo>();
                if (classroomBoardCapture != null) agents.Add(classroomBoardCapture);
                if (vocabularyBank != null) agents.Add(vocabularyBank);
                if (summarizationAgent != null) agents.Add(summarizationAgent);
                if (diagramGenerator != null) agents.Add(diagramGenerator);
                
                // Create a workflow that connects the classroom board capture, summarization, and diagram generator
                _discoveredWorkflows.Add(new AgentWorkflow
                {
                    Name = "Complete Whiteboard Capture, Summarization, and Diagram Generation",
                    Description = "Captures whiteboard content, summarizes it, and generates visual diagrams",
                    Agents = agents,
                    OutputMappings = new Dictionary<string, List<string>> { 
                        { "Classroom Board Capture", new List<string> {
                            "Captures/capture_*.txt",
                        }},
                        { "Vocabulary Bank & Flashcards Generator", new List<string> {
                            "../AI-agent-SpeechTranslator/Output/recognized_transcript_flashcards.json"
                        } },
                        { "AI Summarization Agent", new List<string> {
                            "data/output/summary_*.json"
                        }}
                    },
                    Keywords = new[] { 
                        "whiteboard", "capture", "classroom", "diagram", 
                        "visual", "chart", "mindmap", "flowchart", "summary", "summarization"
                    },
                    Category = "Classroom Assistant"
                });

                if (_discoveredWorkflows.Count > 1)
                {
                    var workflow = _discoveredWorkflows.FirstOrDefault(w => w.Name.Contains("Complete Whiteboard Capture"));
                    if (workflow != null)
                    {
                        var summaryAgent = workflow.Agents.FirstOrDefault(a => a.Name.Contains("AI Summarization Agent"));
                        if (summaryAgent != null)
                        {
                            summaryAgent.Arguments = new List<string> { 
                                "run", "--project", ".",
                                "--", 
                                "../AI-Agent-BoardCapture/Captures/capture_*.txt",
                                "../AI-agent-SpeechTranslator/Output/recognized_transcript_flashcards.json"
                            };
                            _logger.LogInformation("Updated summarization agent arguments to use classroom board capture and vocabulary data");
                        }

                        var diagramAgent = workflow.Agents.FirstOrDefault(a => a.Name.Contains("Diagram Generator"));
                        if (diagramAgent != null)
                        {
                            diagramAgent.Arguments = new List<string> { 
                                "run", "--project", ".",
                                "--", 
                                "../AI-Agent-BoardCapture/Captures/latest_captured_image_text.txt",
                                "../AI-Summarization-agent/data/outputs/latest_summary.json" 
                            };
                            _logger.LogInformation("Updated diagram generator arguments to use classroom board capture and summary output");
                        }
                    }
                }
                
                _logger.LogInformation("Created workflow for classroom board capture, summarization, and diagram generation");
            }
            // Save discovered workflows to a local file for future use
            var workflowsFile = Path.Combine(AppContext.BaseDirectory, "workflows.json");
            await File.WriteAllTextAsync(workflowsFile, JsonSerializer.Serialize(_discoveredWorkflows, new JsonSerializerOptions { WriteIndented = true }));
        }
        
        return _discoveredWorkflows;
    }
}
