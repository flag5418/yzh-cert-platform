using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Prompt.Models;

namespace YZH.Core.AI.Prompt
{
    public interface IPromptInterpreter
    {
        string Render(string template, IDictionary<string, object> context);
        Task<ParseResult<T>> ParseAsync<T>(string llmOutput, CancellationToken ct = default) where T : class;
    }
}
