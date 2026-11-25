using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 文档侧「责任部门」归集（RM3 部门轴的文档键）—— 认证检查装配 / 专家任务投影**唯一实现**。
///
/// <para>来源 = 文档自身提取产物 <c>ModeOutput</c>：优先 <c>DeptNames</c>（由 <c>DocPdcaClassifier</c>
/// 归集 = OwnerDept + SupportDepts），缺失则退回 <c>OwnerDept</c>。</para>
///
/// <para>★ 2 层结构：只在文档内部取**已提取字段**，⛔ 不在装配阶段二次解析企业文档原文。</para>
/// </summary>
public static class DocDeptNames
{
    /// <summary>部门轴锚点：文档责任部门落在检查项部门内 ⇒ 该部门的执行记录（RM3 记录类）。</summary>
    public const string DeptRecordAnchor = "同部门记录";

    /// <summary>部门轴锚点：同部门的程序 / 制度 / 文件（RM3 非记录类）。</summary>
    public const string DeptFileAnchor = "同部门文件";

    /// <summary>从 <c>ModeOutput</c> 取部门名（去重，保持出现顺序）。</summary>
    public static List<string> Collect(JsonElement modeOutput)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        CollectNamedStrings(modeOutput, "DeptNames", set);
        if (set.Count == 0) CollectNamedStrings(modeOutput, "OwnerDept", set);
        var list = new List<string>();
        foreach (var d in set)
            if (d.Length > 0) list.Add(d);
        return list;
    }

    /// <summary>把「部门串」（逗号 / 顿号 / 分号 / 竖线 / 斜杠分隔）拆成部门名，并入 <paramref name="sink"/>。</summary>
    public static void AddText(string? raw, HashSet<string> sink)
    {
        foreach (var seg in (raw ?? string.Empty)
                     .Split(new[] { ',', '，', '、', ';', '；', '|', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = seg.Trim();
            if (t.Length > 0) sink.Add(t);
        }
    }

    /// <summary>递归收集同名属性：数组逐项取串；字符串按中英文分隔符拆成多个部门名。</summary>
    private static void CollectNamedStrings(JsonElement el, string name, HashSet<string> set)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    AddValue(p.Value, set);
                else
                    CollectNamedStrings(p.Value, name, set);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in el.EnumerateArray()) CollectNamedStrings(x, name, set);
        }
    }

    private static void AddValue(JsonElement value, HashSet<string> set)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in value.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) AddText(x.GetString(), set);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            AddText(value.GetString(), set);
        }
    }
}
