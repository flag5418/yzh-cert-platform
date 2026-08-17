using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 文本拼接 Skill：assemble（报告/NC 输出核心）。
    /// parts 数组支持**任意数量**片段，常量（固定文案）与变量（{{nX.port}} 引用，引擎已解析）**按顺序混合**，
    /// 拼接成一个符合要求的字符串；joiner 指定连接符（空=直接拼接）。
    /// 纯函数（SideEffect=false）。
    /// </summary>
    public class AssembleSkill : SkillBase
    {
        public override string SkillCode => "assemble";
        public override string SkillName => "文本拼接";
        public override string Category => "data_process";
        public override bool SideEffect => false;
        public override string ReturnType => "string";

        public override IReadOnlyList<SkillParam> InputDecls { get; } = new[]
        {
            new SkillParam { Name = "parts", Type = "json", Required = true, Description = "片段数组（任意数量，常量/变量按序混合）" },
            new SkillParam { Name = "joiner", Type = "string", Required = false, Description = "连接符（空=直接拼接）" }
        };

        public override IReadOnlyList<SkillParam> OutputDecls { get; } = new[]
        {
            new SkillParam { Name = "assembled_text", Type = "string", Required = true, Description = "拼接结果" }
        };

        protected override Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct)
        {
            var parts = ToStrings(context.Inputs.TryGetValue("parts", out var p) ? p : null);
            var joiner = context.Inputs.TryGetValue("joiner", out var j) ? j?.ToString() ?? string.Empty : string.Empty;

            var result = string.Join(joiner, parts);
            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object> { ["assembled_text"] = result }));
        }

        /// <summary>把 parts 输入归一为有序字符串列表：支持数组（推荐）、单值、逗号分隔字符串</summary>
        private static List<string> ToStrings(object? parts)
        {
            var list = new List<string>();

            switch (parts)
            {
                case null:
                    return list;
                case IEnumerable<object> objs:
                    list.AddRange(objs.Select(x => x?.ToString() ?? string.Empty));
                    return list;
                case string s:
                {
                    // 兼容两种形式：JSON 数组字符串 或 普通字符串（当作单片段/逗号分隔）
                    var trimmed = s.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                list.AddRange(doc.RootElement.EnumerateArray().Select(e => e.ToString()));
                                return list;
                            }
                        }
                        catch (System.Text.Json.JsonException) { /* 非 JSON 数组，按字符串处理 */ }
                    }
                    list.Add(s);
                    return list;
                }
                default:
                    list.Add(parts.ToString() ?? string.Empty);
                    return list;
            }
        }
    }
}
