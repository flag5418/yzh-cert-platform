using System;

namespace YZH.Core.AI
{
    /// <summary>
    /// LLM 调用异常。
    /// <para>IsTimeout = true 表示网络超时；IsUnreachable = true 表示 Provider 不可用（触发熔断/降级）。</para>
    /// </summary>
    public class LlmCallException : Exception
    {
        public bool IsTimeout { get; }
        public bool IsUnreachable { get; }

        public LlmCallException(string message, bool isUnreachable = false)
            : base(message)
        {
            IsUnreachable = isUnreachable;
            IsTimeout = isUnreachable || message.Contains("超时") || message.Contains("Timeout");
        }

        public LlmCallException(string message, bool isUnreachable, Exception inner)
            : base(message, inner)
        {
            IsUnreachable = isUnreachable;
            IsTimeout = isUnreachable || message.Contains("超时") || message.Contains("Timeout");
        }
    }
}
