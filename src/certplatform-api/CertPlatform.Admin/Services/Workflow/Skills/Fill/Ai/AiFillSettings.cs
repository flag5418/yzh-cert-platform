using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSugar;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// <b>AI 连接配置</b> —— 从 <c>cert_sys_config</c>（<c>Category='ai_model'</c>）读六键。
    ///
    /// <para><b>★ 为什么必须读系统配置而不是各处写死</b>（39 号 §12.1 经验 4）：
    /// 保证「实际调用模型 = 系统参数里配的模型」—— 否则费用不可控、排查时看到的模型与实际不符。
    /// 本项目已有 <c>AiNodeExecutor</c> / <c>PromptWorkbenchService</c> / <c>DocExtractionRuleService.AI</c>
    /// 三处同款读取，本类<b>是第四处，但口径逐字一致</b>（同一套键名与默认值）。</para>
    ///
    /// <para><b>★ 默认值与既有实现保持一致</b>：<c>BaseUrl</c> 默认 DashScope 兼容模式，
    /// <c>Model</c> 默认 <c>qwen-turbo</c>，<c>MaxTokens</c> 默认 4096
    /// （⚠️ 调用方 <c>AiFillInvoker</c> 会把它抬到 <b>8192</b> —— 填充输出比提取更长，4096 会截断）。</para>
    /// </summary>
    public sealed class AiFillSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        public string Model { get; set; } = "qwen-turbo";

        public int MaxTokens { get; set; } = 4096;

        public float Temperature { get; set; } = 0.7f;

        /// <summary>配置是否可用（缺 key / 缺 base_url ⇒ false，调用方应给「先去配 AI 参数」的人话引导）</summary>
        public bool IsUsable => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

        /// <summary>
        /// 从 <c>cert_sys_config</c> 读六键。
        /// <para>⛔ <b>不抛异常</b>：读不到就用默认值 —— 让失败发生在 <c>LlmInvokeService</c>
        /// 的「AI 端点未配置」提示上，那里的话术更准确。</para>
        /// </summary>
        public static async Task<AiFillSettings> ReadAsync(IDbOrm db, CancellationToken ct = default)
        {
            var s = new AiFillSettings();
            try
            {
                var rows = await db.Client.Queryable<SysConfig>()
                    .Where(x => x.Category == "ai_model" && !x.IsDeleted)
                    .Select(x => new ConfigKV { ConfigKey = x.ConfigKey, ConfigValue = x.ConfigValue })
                    .ToListAsync();

                foreach (var row in rows)
                {
                    switch (row.ConfigKey)
                    {
                        case "ai_api_key": s.ApiKey = row.ConfigValue ?? string.Empty; break;
                        case "ai_base_url":
                            if (!string.IsNullOrWhiteSpace(row.ConfigValue)) s.BaseUrl = row.ConfigValue!;
                            break;
                        case "ai_model_name":
                            if (!string.IsNullOrWhiteSpace(row.ConfigValue)) s.Model = row.ConfigValue!;
                            break;
                        case "ai_max_tokens":
                            if (int.TryParse(row.ConfigValue, out var mt) && mt > 0) s.MaxTokens = mt;
                            break;
                        case "ai_temperature":
                            if (float.TryParse(row.ConfigValue, out var tp)) s.Temperature = tp;
                            break;
                    }
                }
            }
            catch
            {
                // 读库失败 ⇒ 用默认值（见方法注释）
            }

            return s;
        }

        private sealed class ConfigKV
        {
            public string ConfigKey { get; set; } = string.Empty;
            public string? ConfigValue { get; set; }
        }
    }
}
