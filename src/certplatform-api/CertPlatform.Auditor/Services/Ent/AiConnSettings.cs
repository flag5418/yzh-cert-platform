using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSugar;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// <b>AI 连接配置</b>（专家端版）—— 从 <c>cert_sys_config</c>（<c>Category='ai_model'</c>）读键。
    ///
    /// <para><b>⚠️ 这是第 5 处同款读取，⛔ 不是设计意图</b>：后台端已有
    /// <c>Admin.Services.Workflow.Skills.Fill.Ai.AiFillSettings</c> 等 4 处，口径逐字一致。
    /// 本该有一份共享实现，但 <c>CertPlatform.Shared</c> <b>不引 <c>YZH.Core.Api</c></b>
    /// （只有 <c>Stand + DataBase</c>），拿不到 <c>SysConfig</c> 实体 ⇒ 只能先落在专家端。
    /// <b>后续合并的唯一正确方向</b>：把 <c>AiFillSettings</c> 下沉到
    /// <c>Shared/Services/Sys/</c>（那里允许查库），两端口径才真正合一。
    /// 在那之前：<b>键名与默认值必须与 <c>AiFillSettings</c> 逐字一致</b>，
    /// 否则「系统参数里配的模型」与实际调用的模型分叉，费用与排查都会错。</para>
    ///
    /// <para><b>★ 为什么不抛异常</b>：读不到就用默认值，让失败发生在
    /// <see cref="CertPlatform.Shared.DocExtraction.LlmInvokeService"/> 的
    /// 「AI 端点未配置」提示上 —— 那里的话术对用户更准确。</para>
    /// </summary>
    public sealed class AiConnSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        public string Model { get; set; } = "qwen-turbo";

        public int MaxTokens { get; set; } = 4096;

        public float Temperature { get; set; } = 0.7f;

        /// <summary>配置是否可用（缺 key / 缺 base_url ⇒ false，调用方须给「先去配 AI 参数」的人话引导）</summary>
        public bool IsUsable => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

        /// <summary>从 <c>cert_sys_config</c> 读键（<c>Category='ai_model'</c>）</summary>
        public static async Task<AiConnSettings> ReadAsync(IDbOrm db, CancellationToken ct = default)
        {
            var s = new AiConnSettings();
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
                // 读库失败 ⇒ 用默认值（见类注释：让 LlmInvokeService 报「未配置」更准确）
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
