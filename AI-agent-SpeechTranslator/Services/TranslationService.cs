using Azure;
using Azure.AI.Translation.Text;
using Azure.Core;
using System;
using System.Threading.Tasks;

namespace SpeechTranslator.Services
{
    public class TranslationService
    {
        private readonly TextTranslationClient _client;

        public TranslationService(string translatorKey, string translatorEndpoint)
        {
            _client = new TextTranslationClient(new AzureKeyCredential(translatorKey), new Uri(translatorEndpoint));
        }

        public async Task<string> TranslateTextAsync(string sourceLang, string targetLanguage, string text)
        {
            if (sourceLang.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var response = await _client.TranslateAsync([targetLanguage], [text], sourceLanguage: sourceLang);
            return response.Value[0].Translations[0].Text;
        }
    }
}