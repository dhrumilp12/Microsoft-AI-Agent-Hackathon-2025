namespace AI_Agent_Orchestrator.Models;

public class LLMConversation
{
    public int ConversationId { get; set; }
    public string UserId { get; set; }
    public string UserQuery { get; set; }
    public string BotResponse { get; set; }
    public DateTime Timestamp { get; set; }
}