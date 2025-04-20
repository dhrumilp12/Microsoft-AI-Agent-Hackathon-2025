namespace AI_Agent_Orchestrator.Models;

public class AgentInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<string> Arguments { get; set; } = new();
    
    // For Semantic Kernel integration
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = string.Empty; 
}
