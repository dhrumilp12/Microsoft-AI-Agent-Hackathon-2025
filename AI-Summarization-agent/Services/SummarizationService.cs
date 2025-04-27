namespace AI_Summarization_agent.Services
{
    public class SummarizationService
    {
        private readonly CustomOpenAIClient _openAIClient;

        public SummarizationService(CustomOpenAIClient openAIClient)
        {
            _openAIClient = openAIClient;
        }

        public async Task<string> SummarizeTextAsync(string input)
        {
            try
            {
                return await _openAIClient.GetSummaryAsync(input);
            }
            catch (Exception ex)
            {
                return $"Error generating summary: {ex.Message}";
            }
        }
    }
}
