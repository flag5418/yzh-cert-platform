namespace YZH.Core.AI.Clients.Models
{
    /// <summary>单条消息（system / user / assistant）</summary>
    public class LlmMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
}
