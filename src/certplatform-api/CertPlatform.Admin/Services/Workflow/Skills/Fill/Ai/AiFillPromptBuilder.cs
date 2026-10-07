using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// <b>批量装配的上下文</b> —— 「这份文档要填哪些锚点 + 有哪些企业资料」。
    ///
    /// <para>★ <see cref="Anchors"/> 由<b>锚点表</b>产出（42 号 §3.3 第 3 条：
    /// 清单必须由结构化数据给，⛔ 不由提示词手写 —— 否则漏一个锚点不会报错）。</para>
    /// </summary>
    public sealed class AiFillBuildContext
    {
        /// <summary>文档名（给模型一个「这是哪份表」的语境）</summary>
        public string DocumentName { get; set; } = string.Empty;

        /// <summary>标准编码（如 9001），可为空</summary>
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>★ 待填锚点清单（有序：先 semantic、再 field、最后 table）</summary>
        public List<AiFillAnchorSpec> Anchors { get; set; } = new();

        /// <summary>★ 已按相关性过滤的企业文档 markdown（由 <c>IEnterpriseDocRetriever</c> 产出）</summary>
        public string EnterpriseDocs { get; set; } = string.Empty;
    }

    /// <summary>
    /// <b>AI 填充提示词的唯一装配器</b>（39 号 §12.2）。
    ///
    /// <para><b>★ 为什么必须唯一</b>：38 号 §15.4 已警告 —— 两套 prompt 组装逻辑<b>必然漂移</b>，
    /// 症状是「页面上试跑是对的，正式跑却不对」。故单项（<c>BuildSingleAsync</c>，试跑 / 缺项回退）
    /// 与批量（<c>BuildBatchAsync</c>，★ 生产路径）<b>共用同一份渲染与默认模板</b>。</para>
    ///
    /// <para><b>★ 用户确认的定位（2026-10-04）</b>：
    /// 「全局规则，就是自动分析当前锚点中有哪些需要 AI 进行自动判断和执行的，
    /// 通过筛选的 markdown 文件，形成对应的锚点数值，<b>这样我们的提示词才好编写成比较有规律固定的提示词</b>。」
    /// ⇒ 正因为<b>清单由引擎注入</b>（<c>{{__FILL__.anchors}}</c>），
    /// 提示词才能是一份<b>稳定的、可复用的</b>模板，而不是每份文档手写一遍。</para>
    ///
    /// <para><b>★ 无提示词时给内置默认模板</b>：实测 <c>cert_doc_fill_prompt</c> 两行的
    /// <c>UserTemplate</c> 都是 <c>NULL</c>。若这里直接报「提示词为空」，则
    /// <b>Skill 永远跑不起来</b>，用户必须先手工建提示词 —— 典型的「先有鸡还是先有蛋」。
    /// 故缺省时回落到 <see cref="DefaultUserTemplate"/>（= 39 号 §12.3 原文），
    /// 让 Skill <b>开箱可跑</b>，用户再按需覆盖。</para>
    /// </summary>
    public static class AiFillPromptBuilder
    {
        /// <summary>渲染占位符前缀（⛔ 与锚点语法 <c>{{ }}</c> 同形，但带 <c>__FILL__.</c> 命名空间，不会冲突）</summary>
        public const string Prefix = "{{__FILL__.";

        /// <summary>
        /// ★ 内置默认用户提示词模板（= 39 号 §12.3 原文）。
        /// <para>刻意<b>逐字保留</b>该文档的措辞 —— 它已被「纸面实验」验证过（25 号）。</para>
        /// </summary>
        public const string DefaultUserTemplate =
@"你是认证文档填写助手。请根据「待填锚点」与「企业文档」，为每个锚点给出值。

## 待填锚点
{{__FILL__.anchors}}

## 企业文档（已按相关性过滤）
{{__FILL__.enterprise_docs}}

## 要求
1. 只使用上面企业文档中出现的事实，⛔ 不要编造。
2. 找不到依据的锚点，value 填 null，并在 note 里说明「资料中未提及」。
3. 严格按以下 JSON 结构输出，⛔ 不要输出解释文字。

## 输出结构
{{__FILL__.output_schema}}";

        /// <summary>内置默认系统提示词（角色设定）</summary>
        public const string DefaultSystemPrompt =
            "你是严谨的认证文档填写助手。你只依据给定的企业资料作答，绝不编造事实；" +
            "无法从资料中确定的值一律返回 null 并说明原因。";

        /// <summary>
        /// ★ 内置默认输出结构（= 39 号 §12.4 原文）。
        /// <para>三段：<c>semantic</c>（锚点 → 改写文本）/ <c>fields</c>（锚点 → 值对象）/
        /// <c>tables</c>（表格标签 → 行数组）。编排器按段分派给 3 个 Skill（39 号 §12.5）。</para>
        /// </summary>
        public const string DefaultOutputSchema =
@"{
  ""type"": ""object"",
  ""required"": [""semantic"", ""fields"", ""tables""],
  ""properties"": {
    ""semantic"": {
      ""type"": ""object"",
      ""description"": ""锚点编码 → 改写后的文本"",
      ""additionalProperties"": { ""type"": ""string"" }
    },
    ""fields"": {
      ""type"": ""object"",
      ""description"": ""锚点编码 → { value, confidence, source_doc, note }"",
      ""additionalProperties"": {
        ""type"": ""object"",
        ""properties"": {
          ""value"": {},
          ""confidence"": { ""type"": ""number"", ""minimum"": 0, ""maximum"": 1 },
          ""source_doc"": { ""type"": ""string"" },
          ""note"": { ""type"": ""string"" }
        }
      }
    },
    ""tables"": {
      ""type"": ""object"",
      ""description"": ""表格标签 → 行数组"",
      ""additionalProperties"": {
        ""type"": ""array"",
        ""items"": { ""type"": ""object"" }
      }
    }
  }
}";

        /// <summary>
        /// <b>单项</b>：一个锚点一次请求（试跑 / 批量里某段整体缺失时的回退重试）。
        /// </summary>
        /// <param name="db">数据访问（读 <c>cert_doc_fill_prompt</c>）</param>
        /// <param name="promptCode">提示词编码；空 ⇒ 直接用内置默认模板</param>
        /// <param name="skillCode">发起方 Skill 编码（<c>src_semantic</c> / <c>src_ai_field</c> / <c>src_ai_table</c>）</param>
        /// <param name="anchor">待填锚点（单条）</param>
        /// <param name="enterpriseDocs">已过滤的企业文档 markdown</param>
        /// <param name="orgCode">机构编码（空 ⇒ 只命中全局提示词）</param>
        /// <param name="ct">取消令牌</param>
        public static Task<AiFillInvokeRequest> BuildSingleAsync(
            IDbOrm db,
            string? promptCode,
            string skillCode,
            AiFillAnchorSpec anchor,
            string? enterpriseDocs = null,
            string? orgCode = null,
            CancellationToken ct = default)
        {
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));

            // 单锚点的 section 由调用方给定（各 Skill 只关心自己那一段）
            var ctx = new AiFillBuildContext
            {
                Anchors = new List<AiFillAnchorSpec> { anchor },
                EnterpriseDocs = enterpriseDocs ?? string.Empty,
            };

            return BuildAsync(db, promptCode, skillCode, ctx, orgCode, ct);
        }

        /// <summary>
        /// <b>批量</b>：一份文档一次请求（★ 生产路径）。
        /// <para>一次产出 <c>semantic</c> / <c>fields</c> / <c>tables</c> 三段，
        /// 编排器按段分派给 3 个 Skill（39 号 §12.5）。</para>
        /// </summary>
        public static Task<AiFillInvokeRequest> BuildBatchAsync(
            IDbOrm db,
            string? promptCode,
            AiFillBuildContext ctx,
            string? orgCode = null,
            CancellationToken ct = default)
            => BuildAsync(db, promptCode, skillCode: "batch", ctx ?? new AiFillBuildContext(), orgCode, ct);

        /// <summary>单项与批量的公共实现（★ 唯一渲染点，见类注释）</summary>
        private static async Task<AiFillInvokeRequest> BuildAsync(
            IDbOrm db,
            string? promptCode,
            string skillCode,
            AiFillBuildContext ctx,
            string? orgCode,
            CancellationToken ct)
        {
            var prompt = await LoadPromptAsync(db, promptCode, orgCode, ct);

            var userTemplate = prompt?.UserTemplate;
            if (string.IsNullOrWhiteSpace(userTemplate))
                userTemplate = DefaultUserTemplate;   // ★ 开箱可跑（见类注释）

            var systemPrompt = prompt?.SystemPrompt;
            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = DefaultSystemPrompt;

            var outputSchema = prompt?.OutputSchema;
            if (string.IsNullOrWhiteSpace(outputSchema))
                outputSchema = DefaultOutputSchema;

            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["anchors"] = RenderAnchors(ctx.Anchors),
                ["semantic_anchors"] = RenderAnchors(Filter(ctx.Anchors, "semantic")),
                ["field_anchors"] = RenderAnchors(Filter(ctx.Anchors, "field")),
                ["table_anchors"] = RenderAnchors(Filter(ctx.Anchors, "table")),
                ["enterprise_docs"] = ctx.EnterpriseDocs ?? string.Empty,
                ["output_schema"] = outputSchema,
                ["document_name"] = ctx.DocumentName ?? string.Empty,
                ["standard_code"] = ctx.StandardCode ?? string.Empty,
            };

            // ★ 动态补充自定义提示词组（group_XXX）
            var groups = ctx.Anchors
                .Where(a => !string.IsNullOrWhiteSpace(a.PromptGroup))
                .GroupBy(a => a.PromptGroup!)
                .ToList();
            foreach (var g in groups)
            {
                values[$"group_{g.Key}"] = RenderAnchors(g.ToList());
            }

            // ★ 单项调用时补「单锚点便捷占位符」—— 让提示词可以直接写
            //   {{__FILL__.instruction}}，不必绕道 {{__FILL__.anchors}}（39 号 §6.2 / §7.2 的写法）。
            //   ⛔ 只在恰好一个锚点时才补：批量时若也补，模型会以为「只有第一个锚点要填」。
            if (ctx.Anchors.Count == 1)
            {
                var only = ctx.Anchors[0];
                values["anchor_code"] = only.AnchorCode ?? string.Empty;
                values["instruction"] = only.Instruction ?? string.Empty;
                values["value_kind"] = string.IsNullOrWhiteSpace(only.ValueKind) ? "text" : only.ValueKind;
            }

            // 39 号 §6.2 用的是 enterprise_context，与 enterprise_docs 同物 ⇒ 两个键都给，避免提示词写错就取不到
            values["enterprise_context"] = ctx.EnterpriseDocs ?? string.Empty;

            return new AiFillInvokeRequest
            {
                SystemPrompt = Render(systemPrompt!, values),
                UserPrompt = Render(userTemplate!, values),
                OutputSchema = Render(outputSchema!, values),
                // ★ 低温可复现（39 号 §12.1）；prompt 行有配置则用配置
                Temperature = prompt?.Temperature ?? 0m,
                MaxTokens = prompt?.MaxTokens is > 0 ? prompt!.MaxTokens : AiFillInvoker.DefaultMaxTokens,
            };
        }

        /// <summary>
        /// 取<b>生效</b>的提示词行。
        ///
        /// <para><b>选取口径</b>（与 <c>DocFillPromptController.resolve</c> 一致）：
        /// 机构专属（<c>OrgCode</c> 命中）优先于全局（<c>OrgCode=''</c>）；
        /// 同组内 <c>IsDefault=1</c> 优先，其次 <c>Version</c> 最大。</para>
        ///
        /// <para>⚠️ <c>GetListAsync</c> 默认加 <c>IsValid=1</c>（记忆 §二十②）——
        /// 这里<b>正是想要的</b>语义（停用的提示词不该生效），故不改成 IgnoreValid。</para>
        /// </summary>
        private static async Task<DocFillPrompt?> LoadPromptAsync(
            IDbOrm db, string? promptCode, string? orgCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(promptCode)) return null;

            var code = promptCode!.Trim();
            var org = (orgCode ?? string.Empty).Trim();

            var rows = (await db.GetListAsync<DocFillPrompt>(x => x.PromptCode == code)).Data;
            if (rows == null || rows.Count == 0) return null;

            // 机构专属优先；没有机构专属再退回全局（OrgCode=''）
            var scoped = rows.Where(r => !string.IsNullOrEmpty(org)
                                         && string.Equals(r.OrgCode, org, StringComparison.Ordinal)).ToList();
            if (scoped.Count == 0)
                scoped = rows.Where(r => string.IsNullOrEmpty(r.OrgCode)).ToList();
            if (scoped.Count == 0) return null;

            return scoped
                .OrderByDescending(r => r.IsDefault)
                .ThenByDescending(r => r.Version)
                .ThenByDescending(r => r.CreateTime)
                .FirstOrDefault();
        }

        /// <summary>按 section 过滤（保持原顺序）</summary>
        private static List<AiFillAnchorSpec> Filter(List<AiFillAnchorSpec> all, string section)
            => all.Where(a => string.Equals(a.Section, section, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// 把锚点清单渲染成<b>人话 + 类型</b>的编号列表。
        ///
        /// <para>★ 类型必须出现在这里（39 号 §7.3）：模型只负责「值是什么」，
        /// <b>⛔ 不负责「值是什么类型」</b> —— 类型来自锚点属性，在这里<b>告知</b>模型，
        /// 让它把日期写成 <c>2026-03-11</c> 而不是 <c>2026年3月11日</c>。</para>
        /// </summary>
        public static string RenderAnchors(List<AiFillAnchorSpec>? anchors)
        {
            if (anchors == null || anchors.Count == 0) return "（本份文档没有需要 AI 填写的锚点）";

            var sb = new StringBuilder();

            var semantic = anchors.Where(a => Eq(a.Section, "semantic")).ToList();
            var fields = anchors.Where(a => Eq(a.Section, "field")).ToList();
            var tables = anchors.Where(a => Eq(a.Section, "table")).ToList();

            if (semantic.Count > 0)
            {
                sb.AppendLine("【semantic 段】把每个锚点改写成通顺的文本（只给字符串）");
                foreach (var a in semantic)
                {
                    sb.AppendLine($"- {a.AnchorCode}：{Desc(a)}");
                    // ★ 待改写的原文（39 号 §6.1 的核心输入）。⚠️ 防御性截断：
                    //   若调用方误把整篇文档当原文传进来，会把上下文预算吃光。
                    if (!string.IsNullOrWhiteSpace(a.SourceText))
                        sb.AppendLine($"  待改写原文：{Truncate(a.SourceText!, 4000)}");
                }
                sb.AppendLine();
            }

            if (fields.Count > 0)
            {
                sb.AppendLine("【fields 段】给每个锚点一个值（给 { value, confidence, source_doc, note }）");
                foreach (var a in fields) sb.AppendLine($"- {a.AnchorCode}：{Desc(a)}{KindOf(a)}");
                sb.AppendLine();
            }

            if (tables.Count > 0)
            {
                sb.AppendLine("【tables 段】给每个表格填行（给对象数组，键 = 列字段编码）");
                foreach (var a in tables)
                {
                    sb.AppendLine($"- {a.AnchorCode}：{Desc(a)}");
                    if (a.Columns is { Count: > 0 })
                    {
                        var cols = string.Join(" ｜ ", a.Columns.Select(c =>
                            $"{c.FieldCode}({c.Title},{c.ValueKind})"));
                        sb.AppendLine($"  列（按此顺序）：{cols}");
                    }
                    else
                    {
                        sb.AppendLine("  列：未声明（请按企业资料中该表的实际列输出）");
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>防御性截断（长文本进提示词会吃光上下文预算）</summary>
        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..max] + "\n…（原文过长，已截断）";

        private static string Desc(AiFillAnchorSpec a)
            => string.IsNullOrWhiteSpace(a.Instruction) ? a.AnchorCode : a.Instruction!;

        private static string KindOf(AiFillAnchorSpec a)
        {
            var kind = string.IsNullOrWhiteSpace(a.ValueKind) ? "text" : a.ValueKind;
            return string.IsNullOrWhiteSpace(a.NumberFormat)
                ? $"（类型：{kind}）"
                : $"（类型：{kind}，格式：{a.NumberFormat}）";
        }

        /// <summary>
        /// 渲染 <c>{{__FILL__.key}}</c> 占位符。
        /// <para>⚠️ 用<b>精确串替换</b>而非正则：占位符集合是<b>封闭</b>的（就上面那几个），
        /// 正则只会引入「把用户提示词里别的 <c>{{ }}</c> 也吃掉」的风险
        /// （提示词里可能合法地写着 <c>{{ENT_NAME}}</c> 做说明）。</para>
        /// </summary>
        public static string Render(string template, IDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            var result = template;
            foreach (var kv in values)
                result = result.Replace(Prefix + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.Ordinal);

            return result;
        }
    }
}
