using System.Collections.Generic;

namespace YZH.Core.AI.Clients.Models
{
    /// <summary>
    /// LLM 请求。Provider / Model 由调用方指定，其余字段有默认值。
    /// </summary>
    public class LlmRequest
    {
        /// <summary>目标 Provider（qwen / ollama / mock），空则走 ILlmClient.ActiveProvider</summary>
        public string Provider { get; set; } = "qwen";

        /// <summary>模型名（qwen-turbo / qwen2.5:7b 等）</summary>
        public string Model { get; set; } = "qwen-turbo";

        /// <summary>系统 + 用户消息列表</summary>
        public List<LlmMessage> Messages { get; set; } = new();

        /// <summary>温度（提取类任务默认 0.1，控幻觉）</summary>
        public double Temperature { get; set; } = 0.1;

        /// <summary>最大生成 Token 数</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>是否请求 JSON 结构化输出</summary>
        public bool JsonMode { get; set; } = true;

        /// <summary>单次调用超时（秒）</summary>
        public int TimeoutSeconds { get; set; } = 60;
    }
}
