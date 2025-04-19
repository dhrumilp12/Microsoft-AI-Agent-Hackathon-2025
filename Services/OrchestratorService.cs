using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using VocabularyBank.Services;
using SpeechTranslator.Services;
using Microsoft.Extensions.Configuration;

namespace AI_Agent_Orchestrator.Services
{
    public class OrchestratorService
    {
        private readonly Kernel _kernel;

        public OrchestratorService(KernelService kernelService, IConfiguration configuration, AzureOpenAIService openAIService)
        {
            _kernel = kernelService.GetKernel();

            string kernelSpeechKey = kernelService.GetSpeechKey();
            string kernelSpeechRegion = kernelService.GetSpeechEndpoint();
            string kernelTranslatorKey = kernelService.GetTranslatorKey();
            string kernelTranslatorEndpoint = kernelService.GetTranslatorEndpoint();
            string kernelTranslatorRegion = kernelService.GetTranslatorRegion();

            // Register skills
            _kernel.Plugins.Add(new KernelPluginWrapper("SpeechToText", new SpeechToTextService(kernelSpeechKey, kernelSpeechEndpoint)));
            _kernel.Plugins.Add(new KernelPluginWrapper("Translation", new TranslationService(kernelTranslatorKey, kernelTranslatorEndpoint, kernelTranslatorRegion)));
            _kernel.Plugins.Add(new KernelPluginWrapper("Vocabulary", new VocabularyExtractorService(configuration, openAIService)));
            _kernel.Plugins.Add(new KernelPluginWrapper("FlashcardGenerator", new FlashcardGeneratorService()));
            _kernel.Plugins.Add(new KernelPluginWrapper("VocabTranslator", new AzureTranslationService(configuration)));
            _kernel.Plugins.Add(new KernelPluginWrapper("DefinitionGenerator", new DefinitionGeneratorService(openAIService, new AzureTranslationService(configuration))));
        }

        public async Task RunAsync(string userChoice, string audioOrVideoFilePath = null, string sourceLanguage = null, string targetLanguage = null, string text = null)
        {
            try
            {
                if (userChoice == "1")
                {
                    if (string.IsNullOrEmpty(audioOrVideoFilePath) || string.IsNullOrEmpty(sourceLanguage) || string.IsNullOrEmpty(targetLanguage))
                    {
                        throw new ArgumentException("Missing required parameters for Speech-to-Text and Translation.");
                    }

                    // Step 1: Convert speech to text
                    string extractedText;
                    if (audioOrVideoFilePath.EndsWith(".wav"))
                    {
                        extractedText = await _speechToTextService.ConvertSpeechToTextAsync(audioOrVideoFilePath);
                    }
                    else
                    {
                        extractedText = await _speechToTextService.ConvertSpeechToTextFromVideoAsync(audioOrVideoFilePath);
                    }

                    Console.WriteLine($"Extracted Text: {extractedText}");

                    // Step 2: Translate text if needed
                    string translatedText = await _translationService.TranslateTextAsync(sourceLanguage, targetLanguage, extractedText);
                    Console.WriteLine($"Translated Text: {translatedText}");
                }
                else if (userChoice == "2")
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        throw new ArgumentException("Missing required parameter: text for Vocabulary Extraction.");
                    }

                    // Step 3: Extract vocabulary terms
                    var vocabularyTerms = _vocabularyExtractorService.ExtractVocabulary(text);
                    Console.WriteLine("Extracted Vocabulary Terms:");
                    foreach (var term in vocabularyTerms)
                    {
                        Console.WriteLine(term);
                    }

                    // Step 4: Generate flashcards
                    var flashcards = _flashcardGeneratorService.GenerateFlashcards(vocabularyTerms);
                    Console.WriteLine("Generated Flashcards:");
                    foreach (var flashcard in flashcards)
                    {
                        Console.WriteLine($"Term: {flashcard.Term}, Definition: {flashcard.Definition}");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid choice. Please restart the application and choose either 1 or 2.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    // Removed the override keyword from the Description property as the base class 'KernelPlugin' does not mark it as virtual, abstract, or override.
    public class KernelPluginWrapper : KernelPlugin
    {
        public KernelPluginWrapper(string name, object plugin) : base(name, $"Wrapper for {plugin.GetType().Name}")
        {
            Plugin = plugin;
        }

        public object Plugin { get; }

        public override bool TryGetFunction(string name, out KernelFunction? function)
        {
            // Ensure the nullability of the 'function' parameter matches the overridden member.
            function = null;
            return false;
        }

        public override IEnumerator<KernelFunction> GetEnumerator()
        {
            // Implement logic to enumerate functions  
            return new List<KernelFunction>().GetEnumerator();
        }

        public override int FunctionCount => 0;
    }
}