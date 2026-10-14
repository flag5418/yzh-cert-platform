using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// AI 节点参数表的一行 —— 对应设计器「参数」段的一条记录，落库形态是 <c>rule_json</c> 里的
    /// <c>config.params[]</c>（用户 2026-10-14 裁决 ①：进 rule_json，零迁移）。
    /// </summary>
    public sealed class AiParamDecl
    {
        /// <summary>★ 短 ASCII 标识符（<c>p1</c>/<c>p2</c>），提示词里写 <c>{{name}}</c> 引用</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>中文说明 —— 给人看，也给模型看（第 3 批注释式参数区原样带上）</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>来源类型：<c>node</c>（画布节点输出）/ <c>constant</c> / <c>skill</c></summary>
        public string SourceType { get; set; } = "node";

        /// <summary>★ 依赖边的 From：<c>sourceType=node</c> 时非空，且必须是真实存在的节点 ID</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>源节点输出端口，缺省 <c>result</c></summary>
        public string PortName { get; set; } = "result";

        /// <summary><c>sourceType=constant</c> 时的常量值</summary>
        public object? Value { get; set; }

        /// <summary>是否一条「节点来源」参数（= 一条隐式依赖边）</summary>
        public bool IsNodeSource =>
            string.Equals(SourceType, "node", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SourceType, "link", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AI 节点参数表唯一口径（解析 + 依赖边收集）。
    ///
    /// <para><b>★ 与前端的对应关系（三套口径必须同步，不一致是本次改造的病根）：</b></para>
    /// <list type="bullet">
    /// <item>占位符正则：<see cref="PromptRenderer.Placeholder"/>（C#）= <c>PLACEHOLDER_PATTERN</c>（aiParams.ts）</item>
    /// <item>参数表解析：<c>AiParams.ParseDeclaredParams</c>（C#）= <c>parseAiParams</c>（aiParams.ts）</item>
    /// <item>执行顺序：本类 <see cref="CollectSourceNodeIds"/> = 前端 <c>topology.ts</c> 的 <c>implicitEdges</c></item>
    /// </list>
    ///
    /// <para><b>★ 参数即边（用户裁决 ②）</b>：<c>params[].nodeId → aiNode</c> 就是一条隐式依赖边。
    /// 它只决定「执行顺序 / 预执行」，<b>⛔ 不参与路径枚举</b>（路径只看物理边）——
    /// 否则会产生伪路径（如 <c>start → docField → ai</c>），让「执行了几条路径」失真。</para>
    ///
    /// <para>⚠️ 本类是<b>纯解析、零副作用</b>的静态工具：不碰 DB、不抛业务异常。
    /// 解析失败一律返回空列表（与前端 <c>parseAiParams</c> 同口径），
    /// 由调用方按「正则兜底」继续走，⛔ 不许在这里打断主流程。</para>
    /// </summary>
    public static class AiParams
    {
        /// <summary>参数表在 <c>node.Config</c> 里的键（前端 panelSchema 同名，逐字一致）</summary>
        public const string ConfigKey = "params";

        /// <summary>提示词模板键（★ M1 踩坑：是 promptTemplate，⛔ 不是 prompt）</summary>
        public const string PromptKey = "promptTemplate";

        private static readonly JsonSerializerOptions CaseInsensitive = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// 读 <c>config.params</c> —— 前端落库前是数组，DB/请求里也可能变成 JSON 字符串，
        /// 两种形态都要吃下（与前端 <c>parseAiParams</c> 的「数组或字符串化 JSON 均可」同口径）。
        /// </summary>
        /// <returns>参数声明列表；无参数 / 解析失败 ⇒ 空列表，⛔ 不抛</returns>
        public static List<AiParamDecl> ParseDeclaredParams(WorkflowNodeConfig? node)
        {
            var result = new List<AiParamDecl>();

            if (node?.Config == null ||
                !node.Config.TryGetValue(ConfigKey, out var raw) ||
                raw == null)
                return result;

            var json = ToJsonText(raw);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                var items = JsonSerializer.Deserialize<List<AiParamDto>>(json, CaseInsensitive);
                if (items == null) return result;

                foreach (var item in items)
                {
                    if (item == null) continue;
                    var name = (item.Name ?? string.Empty).Trim();
                    if (name.Length == 0) continue;   // 无名参数取不到值，直接丢弃（保存期已被 §2.7 拦截）

                    result.Add(new AiParamDecl
                    {
                        Name = name,
                        Label = item.Label?.Trim() ?? string.Empty,
                        SourceType = string.IsNullOrWhiteSpace(item.SourceType) ? "node" : item.SourceType.Trim(),
                        NodeId = item.NodeId?.Trim() ?? string.Empty,
                        PortName = string.IsNullOrWhiteSpace(item.PortName) ? "result" : item.PortName.Trim(),
                        Value = item.Value
                    });
                }
            }
            catch (Exception)
            {
                // 解析失败按「无参数表」处理 —— 调用方会走正则兜底，不打断执行
                return new List<AiParamDecl>();
            }

            return result;
        }

        /// <summary>
        /// ★ 「参数即边」的后端入口：收集这个 AI 节点<b>必须先跑</b>的来源节点 ID（去重、保持声明序）。
        ///
        /// <para>两条来源（与前端 §2.7.1 的兼容策略完全对应）：</para>
        /// <list type="number">
        /// <item><b>参数表优先</b>：<c>params[].nodeId</c>（sourceType=node）</item>
        /// <item><b>正则兜底</b>：<c>promptTemplate</c> 里每个 <c>{{refKey.port}}</c>，
        ///   经 <paramref name="resolveNodeId"/> 解析成<b>节点 ID</b>（refKey 可能是节点 ID、也可能是
        ///   <b>节点标题</b>，如 <c>{{文档字段.result}}</c>）⇒ 也算一条依赖。
        ///   ⚠️ 已登记为参数名的 <c>refKey</c> 不再当节点（否则标题恰好叫 p1 的节点会被误当来源）。</item>
        /// </list>
        ///
        /// <para>⚠️ 本方法只给<b>候选</b>；节点是否还在图里，由调用方预执行时再判一次
        /// （存进 rule_json 之后节点可能已被删除）。</para>
        /// </summary>
        /// <param name="resolveNodeId">refKey（节点 ID 或节点标题）→ 节点 ID；查不到返回 null</param>
        public static List<string> CollectSourceNodeIds(
            WorkflowNodeConfig? node,
            Func<string, string?>? resolveNodeId = null)
        {
            var ids = new List<string>();
            void Add(string? id)
            {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id!))
                    ids.Add(id!);
            }

            var declared = ParseDeclaredParams(node);
            var declaredNames = new HashSet<string>(declared.Select(d => d.Name), StringComparer.Ordinal);

            foreach (var d in declared)
            {
                if (d.IsNodeSource) Add(d.NodeId);
            }

            // 正则兜底：{{节点标题.端口}} / {{nodeId.端口}} 的老写法
            if (resolveNodeId != null && node!.Config != null &&
                node.Config.TryGetValue(PromptKey, out var promptObj))
            {
                var template = promptObj?.ToString() ?? string.Empty;
                foreach (System.Text.RegularExpressions.Match m in PromptRenderer.Placeholder.Matches(template))
                {
                    var refKey = SplitRef(m.Groups[1].Value).RefKey;

                    if (declaredNames.Contains(refKey)) continue;  // 参数名 ≠ 节点
                    if (ids.Contains(refKey)) continue;

                    var nodeId = resolveNodeId(refKey);
                    if (!string.IsNullOrEmpty(nodeId)) Add(nodeId);
                }
            }

            return ids;
        }

        /// <summary>拆 <c>文档字段.result</c> → (refKey, portName)；无点号则 portName 为空</summary>
        public static (string RefKey, string PortName) SplitRef(string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey)) return (string.Empty, string.Empty);

            var lastDot = fullKey.LastIndexOf('.');
            if (lastDot < 0) return (fullKey, string.Empty);

            return (fullKey[..lastDot], fullKey[(lastDot + 1)..]);
        }

        /// <summary>
        /// 把 <c>Config[params]</c> 的多种运行时形态统一成可反序列化的 JSON 文本。
        /// <para>System.Text.Json 把 <c>Dictionary&lt;string,object&gt;</c> 的值反成 <c>JsonElement</c>，
        /// 代码里手工构造的又可能是 <c>List&lt;object&gt;</c> / 普通字符串 —— 三种都要吃得下。</para>
        /// </summary>
        private static string? ToJsonText(object? raw) => raw switch
        {
            null => null,
            string s => s,
            JsonElement el => el.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => null,
                System.Text.Json.JsonValueKind.String => el.GetString(),
                _ => el.GetRawText()
            },
            System.Collections.IEnumerable => JsonSerializer.Serialize(raw),
            _ => raw.ToString()
        };

        /// <summary>参数表 DTO（camelCase / PascalCase 皆可命中，大小写不敏感）</summary>
        private sealed class AiParamDto
        {
            public string? Name { get; set; }
            public string? Label { get; set; }
            public string? SourceType { get; set; }
            public string? NodeId { get; set; }
            public string? PortName { get; set; }
            public object? Value { get; set; }
        }
    }
}
