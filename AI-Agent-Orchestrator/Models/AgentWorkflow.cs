namespace AI_Agent_Orchestrator.Models;

public class AgentWorkflow
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<AgentInfo> Agents { get; set; } = new();
    public Dictionary<string, List<string>> OutputMappings { get; set; } = new();
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = string.Empty;
}
