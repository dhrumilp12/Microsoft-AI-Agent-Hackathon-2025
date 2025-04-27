using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SpeechTranslator
{
    class TextStream
    {
        public static async IAsyncEnumerable<string> GetSingleTextStream(string text)
        {
            yield return text;
            await Task.CompletedTask;
        }
    }
}