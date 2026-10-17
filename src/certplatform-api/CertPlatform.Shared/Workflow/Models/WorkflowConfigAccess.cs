using System;
using System.Collections.Generic;

namespace CertPlatform.Shared.Workflow.Models
{
    /// <summary>
    /// 工作流节点配置的**大小写不敏感**读取入口（★ 2026-10-16 新增，P1 兼容读取先行）。
    ///
    /// <para><b>为什么需要它</b>：<c>node.Config</c> / <c>param.SourceConfig</c> 是
    /// <c>Dictionary&lt;string, object&gt;</c>，<c>GetValueOrDefault(key)</c> / <c>TryGetValue</c>
    /// 都是<b>大小写敏感</b>的 —— 键的大小写一旦与写入侧不一致，
    /// <b>不报错、只静默返回 null</b>（例如 <c>standardFileCode</c> 读不到就退化成 not_configured）。
    /// 存量 <c>rule_json</c> 的大小写本就是混用的（外层 camel、内层 Pascal 夹杂），
    /// 全靠 <c>WorkflowConfigParser.JsonOptions.PropertyNameCaseInsensitive</c> 兜住。</para>
    ///
    /// <para>本类把「精确命中 → 忽略大小写命中文」的查找收敛为单一实现，
    /// 供后端所有 config 键读取使用（B 组边界）。<b>键名口径统一为 PascalCase</b>，
    /// 但读取对旧 camelCase 数据同样有效 ⇒ 迁移期无需停机、可单独回滚。</para>
    ///
    /// <para>⛔ <b>不适用</b>于：E3 运行时载荷字典（节点输出 <c>result</c> 等）、
    /// 上下文参数（<c>WorkflowContextKeys</c>）、两个平行字典之间的动态键互查
    /// （键来自 <c>foreach</c>，忽略大小写会误匹配）。</para>
    /// </summary>
    public static class WorkflowConfigAccess
    {
        /// <summary>忽略大小写读取；精确命中优先，其次取<b>首个</b>忽略大小写相等的键。</summary>
        public static object? GetConfigValue(this IDictionary<string, object>? dict, string key)
        {
            if (dict == null || dict.Count == 0 || string.IsNullOrEmpty(key))
                return null;

            if (dict.TryGetValue(key, out var exact))
                return exact;

            foreach (var pair in dict)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }

            return null;
        }

        /// <summary>忽略大小写读取（TryGet 形态），语义同 <see cref="GetConfigValue"/>。</summary>
        public static bool TryGetConfigValue(this IDictionary<string, object>? dict, string key, out object? value)
        {
            value = dict.GetConfigValue(key);
            return value != null;
        }

        /// <summary>读取节点的 <c>Config</c> 项（忽略大小写）。</summary>
        public static object? GetConfig(this WorkflowNodeConfig? node, string key)
            => node?.Config.GetConfigValue(key);

        /// <summary>读取节点的 <c>Config</c> 项（TryGet 形态，忽略大小写）。</summary>
        public static bool TryGetConfig(this WorkflowNodeConfig? node, string key, out object? value)
        {
            if (node?.Config == null)
            {
                value = null;
                return false;
            }

            return node.Config.TryGetConfigValue(key, out value);
        }

        /// <summary>读取节点的 <c>Config</c> 项并转字符串（缺省 / 空值 → <paramref name="fallback"/>）。</summary>
        public static string GetConfigString(this WorkflowNodeConfig? node, string key, string fallback = "")
            => node?.Config.GetConfigString(key, fallback) ?? fallback;

        /// <summary>读取字典项并转字符串（缺省 / 空值 → <paramref name="fallback"/>）。</summary>
        public static string GetConfigString(this IDictionary<string, object>? dict, string key, string fallback = "")
        {
            var raw = dict.GetConfigValue(key);
            if (raw == null) return fallback;
            var text = raw.ToString();
            return string.IsNullOrEmpty(text) ? fallback : text;
        }
    }
}
