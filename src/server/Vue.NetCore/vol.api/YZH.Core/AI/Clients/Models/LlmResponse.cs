namespace YZH.Core.AI.Clients.Models
{
    /// <summary>
    /// LLM 响应。Success = false 时 Error 包含失败原因。
    /// </summary>
    public class LlmResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? RawJson { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public long DurationMs { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}
