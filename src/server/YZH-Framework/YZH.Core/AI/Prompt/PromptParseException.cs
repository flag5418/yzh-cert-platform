using System;

namespace YZH.Core.AI.Prompt
{
    /// <summary>
    /// 提示词解析异常（JSON 结构不合法且无法恢复）。
    /// </summary>
    public class PromptParseException : Exception
    {
        public string? RawText { get; }

        public PromptParseException(string message, string? rawText = null, Exception? inner = null)
            : base(message, inner)
        {
            RawText = rawText;
        }
    }
}
