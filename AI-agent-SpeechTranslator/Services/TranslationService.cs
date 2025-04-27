using Azure;
using Azure.AI.Translation.Text;
using Azure.Core;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SpeechTranslator.Services
{
    public class TranslationService
    {
        private readonly TextTranslationClient _client;

        private readonly ILogger _logger;

        public TranslationService(string translatorKey, string translatorEndpoint, string translatorRegion, ILogger logger)
        {
            _client = new TextTranslationClient(new AzureKeyCredential(translatorKey), new Uri(translatorEndpoint), translatorRegion);
            _logger = logger;
        }

        /// <summary>
        /// Asynchronously translates text from source language to target language.
        /// </summary>
        /// <param name="sourceLang">The source language code.</param>
        /// <param name="targetLanguage">The target language code.</param>
        /// <param name="text">The text to translate.</param>
        /// <returns>The translated text.</returns>
        /// <remarks>
        /// This method uses the Azure Text Translation API to translate text.
        /// 
        /// Note: The translation service may have limitations on the number of characters that can be translated in a single request.
        /// </remarks>
        public async Task<string> TranslateTextAsync(string sourceLang, string targetLanguage, string text)
        {
            if (sourceLang.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var response = await _client.TranslateAsync([targetLanguage], [text], sourceLanguage: sourceLang);
            return response.Value[0].Translations[0].Text;
        }

        /// <summary>
        /// Asynchronously translates a stream of text from source language to target language.
        /// </summary>
        /// <param name="sourceLang">The source language code.</param>
        /// <param name="targetLanguage">The target language code.</param>
        /// <param name="textStream">The stream of text to translate.</param>
        /// <returns>An async enumerable of translated text.</returns>
        /// <remarks>
        /// This method uses IAsyncEnumerable to allow for streaming translation of text chunks.
        /// 
        /// Note: The translation service may have limitations on the number of characters that can be translated in a single request.
        /// </remarks>
        public async IAsyncEnumerable<string> TranslateTextStreamAsync(string sourceLang, string targetLanguage, IAsyncEnumerable<string> textStream)
        {
            await foreach (var text in textStream)
            {
                if (sourceLang.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    yield return text;
                }
                else
                {
                    var response = await _client.TranslateAsync([targetLanguage], [text], sourceLanguage: sourceLang);
                    yield return response.Value[0].Translations[0].Text;
                }
            }
        }
    }
}