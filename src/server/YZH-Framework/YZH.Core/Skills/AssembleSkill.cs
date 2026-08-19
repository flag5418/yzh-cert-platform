using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 文本拼接：将任意数量片段按序拼接为一个字符串。纯函数，无副作用。
    /// </summary>
    [Skill(
        Code = "assemble",
        Name = "文本拼接",
        ReturnType = "string",
        Description = "将任意数量片段（常量/变量按序混合）拼接成一个字符串"
    )]
    public static class AssembleSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "片段数组（任意数量，常量/变量按序混合）")]
            object? parts = null,
            [SkillParam(Description = "连接符（空=直接拼接）")]
            string? joiner = null,
            CancellationToken ct = default)
        {
            var partList = ToStrings(parts);
            var sep = joiner ?? string.Empty;
            var result = string.Join(sep, partList);
            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["assembled_text"] = result
            }));
        }

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
                    var trimmed = s.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(trimmed);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                list.AddRange(doc.RootElement.EnumerateArray().Select(e => e.ToString()));
                                return list;
                            }
                        }
                        catch (JsonException) { }
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
