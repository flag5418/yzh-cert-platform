using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 是否为空：判断上游节点的返回值是否为空（★ 通用判空，2026-10-11 用户裁定新增）。
    ///
    /// <para><b>为什么需要它（设计背景）</b>：</para>
    /// <list type="bullet">
    /// <item>原 <c>is_field_empty</c> / <c>is_table_empty</c> 两个 Skill 是<b>绑定具体数据源</b>的判空，
    ///       与 <c>docField</c> / <c>docTable</c> 节点功能重叠 ⇒ 已删除。</item>
    /// <item>改为「取数」与「判空」<b>两个正交动作</b>：
    ///       取数节点（docField / docTable / get_field / get_table）<b>取不到就返回空</b>，
    ///       本 Skill 负责判断空不空，再接 <c>branch</c> 分流。</item>
    /// </list>
    ///
    /// <para><b>输出契约</b>（★ 决定它能不能直接接 branch）：</para>
    /// <list type="bullet">
    /// <item><c>result</c> —— <b>bool，true = 空</b>。这是<b>唯一</b>会被
    ///       <c>NodeExecutor.ExecuteBranch</c> 识别的键（它从字典里优先取 <c>result</c> 且要求是 bool）
    ///       ⇒ 本节点输出<b>可以直连</b> <c>branch</c> 的 <c>condition</c> 端口。</item>
    /// <item><c>state</c> —— <c>"empty"</c> / <c>"not_empty"</c>（给 compare / 日志用）。</item>
    /// <item><c>kind</c> —— 判空命中的具体形态（<c>null</c>/<c>blank</c>/<c>json_empty</c>/
    ///       <c>empty_list</c>/<c>empty_dict</c>），便于排障。</item>
    /// </list>
    ///
    /// <para><b>⛔ 参数为什么用 <c>object</c> 而不是 <c>string</c></b>：
    /// <c>SkillExecutor.ConvertValue</c> 对 <c>string</c> 形参会执行 <c>value.ToString()</c> ——
    /// 上游若是字典/数组，会先被转成 <c>"System.Collections.Generic.Dictionary`2[...]"</c>
    /// 这类垃圾串，判空<b>必然误判</b>。用 <c>object</c> 才能拿到原始对象。</para>
    /// </summary>
    [Skill(
        Code = "is_empty",
        Name = "是否为空",
        ReturnType = "json",
        Description = "判断上游返回值是否为空：null / 空白 / 空集合 / 空对象 都算空，输出 result(bool) 可直连分支节点"
    )]
    public static class IsEmptySkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "待判断的值（通常连线上游节点输出）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            object? value = null,
            CancellationToken ct = default)
        {
            var (isEmpty, kind) = Judge(value);

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                // ★ result 必须是 bool —— ExecuteBranch 只认这个键
                ["result"] = isEmpty,
                ["state"] = isEmpty ? "empty" : "not_empty",
                ["kind"] = kind
            }));
        }

        /// <summary>
        /// 通用判空。返回（是否为空, 命中形态）。
        /// <para>口径：<b>只有「没有可用内容」才算空</b> ——
        /// <c>false</c> / <c>0</c> / <c>"0"</c> / <c>"否"</c> 都是<b>有值</b>，⛔ 不算空。</para>
        /// </summary>
        private static (bool IsEmpty, string Kind) Judge(object? value)
        {
            if (value == null) return (true, "null");

            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return (true, "blank");
                // JSON 空字面量（上游把空集合/空对象序列化成了字符串）
                var t = s.Trim();
                if (t is "[]" or "{}" or "null" or "\"\"" or "''") return (true, "json_empty");
                return (false, "string");
            }

            if (value is IDictionary dict) return (dict.Count == 0, dict.Count == 0 ? "empty_dict" : "dict");

            // ⚠️ string 已在上面拦掉，这里是非字符串的可枚举（数组/List）
            if (value is IEnumerable en)
            {
                var hasAny = en.Cast<object?>().Any();
                return (!hasAny, hasAny ? "list" : "empty_list");
            }

            // bool / 数字 / 日期 / 其他值类型 ⇒ 都是「有值」
            return (false, "value");
        }
    }
}
