using Spectre.Console;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI_Agent_Orchestrator.Services
{
    /// <summary>
    /// Helper class for creating translated UI prompts without markup translation issues
    /// </summary>
    public static class TranslatedPrompts
    {
        /// <summary>
        /// Creates a selection prompt with translated options while avoiding markup issues
        /// </summary>
        public static async Task<string> SelectionPromptAsync(string title, List<string> choices)
        {
            // First translate the title and choices safely without markup
            string translatedTitle = await TranslationHelper.TranslateAsync(title);
            
            // Display the title outside the prompt to avoid markup issues
            Console.WriteLine();
            Console.WriteLine(translatedTitle);
            Console.WriteLine();
            
            // Translate choices
            var translatedChoices = await TranslationHelper.TranslateListAsync(choices);
            
            // Create mapping to original choices
            var choiceMap = choices.Zip(translatedChoices, (original, translated) => 
                                    new { Original = original, Translated = translated })
                                   .ToDictionary(x => x.Translated, x => x.Original);
            
            // Create a selection prompt without translated markup in the title
            var prompt = new SelectionPrompt<string>()
                .Title("") // Empty title since we already displayed it
                .PageSize(15)
                .HighlightStyle(new Style().Foreground(Color.Green))
                .AddChoices(translatedChoices);
            
            var selection = AnsiConsole.Prompt(prompt);
            
            // Map back to original choice
            return choiceMap.TryGetValue(selection, out string? original) ? original : selection;
        }
        
        /// <summary>
        /// Displays a translated message safely without markup issues
        /// </summary>
        public static async Task WriteLineAsync(string message)
        {
            string translated = await TranslationHelper.TranslateAsync(message);
            Console.WriteLine(translated);
        }
    }
}
