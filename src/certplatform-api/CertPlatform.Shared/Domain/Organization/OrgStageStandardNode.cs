using System;
using System.Collections.Generic;

namespace CertPlatform.Shared.Domain.Organization;

/// <summary>
/// ★★ <b>「机构 → 标准 → 阶段」树的唯一内部表示</b>（IR，Intermediate Representation）。
///
/// <para><b>★ 为什么需要它</b>（用户 2026-10-12 原话）：
/// 「<i>将核心体系认证系统的核心数据（主要是检索），统一到一个通用方法中，作为公共方法，
/// 这样避免不同逻辑中，查看到的结果不一致</i>」。</para>
///
/// <para><b>⚠️ 它是内部结构，⛔ 不是对外 DTO</b> —— 这是「前端零改动」的关键：
/// 各端点用 <see cref="OrgStandardTreeAssembler"/> 装配出本结构后，
/// <b>再投影</b>回它自己原来的响应形状（<c>Enterprises</c> / <c>Nodes</c> /
/// <c>Dictionary&lt;string,object&gt;</c> / 强类型 <c>StandardNode</c> …）。
/// ⛔ <b>不要</b>把本类型直接序列化给前端。</para>
///
/// <para><b>★ 为什么 <see cref="Extra"/> 是字典而不是强类型字段</b>：
/// 各端点对外字段差异极大 —— T1 要 <c>configCode</c> / <c>phaseDefinitionCode</c>；
/// T4 要 <c>FileCount</c> / <c>AnalyzingCount</c>；T5 要 <c>FillCompletion</c> / <c>InstanceState</c>。
/// 若做成强类型，会造出 40+ 字段的「万能类」，且每加一个端点就要改本契约。
/// <see cref="Extra"/> 让<b>契约稳定、投影自由</b>。</para>
///
/// <para>⚠️ <b>投影侧注意</b>：从 <see cref="Extra"/> 取值时请用 <c>TryGetValue</c> 并断言键存在
/// （键拼错会静默取到 <c>null</c>，与「值本来就是 null」无法区分）—— 建议在投影处配单测。</para>
///
/// <para><b>★ 排序口径</b>：⛔ 不要在本类型里写排序 —— 统一走
/// <see cref="OrgStandardTreeAssembler.OrderStandards{T}"/> /
/// <see cref="OrgStandardTreeAssembler.OrderStages{T}"/> /
/// <see cref="OrgStandardTreeAssembler.OrderOrgs{T}"/> /
/// <see cref="OrgStandardTreeAssembler.OrderEnterprises{T}"/>（口径只定义一次）。</para>
/// </summary>
public sealed class OrgStageStandardNode
{
    /// <summary>节点唯一 Code（机构 Code / 标准 Code / 阶段 Code / 文件夹 Code / 文件 Code）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>显示名（各端点的 <c>label</c> / <c>Name</c> / <c>StageName</c> 等的来源）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 父节点 Code —— 用于 <see cref="OrgStandardTreeAssembler.Assemble"/> 的父子上溯。
    /// <para>根节点 = <c>null</c> 或空串，或父不在同一批节点内。</para>
    /// </summary>
    public string? ParentCode { get; set; }

    /// <summary>节点类型（见 <see cref="OrgNodeKind"/>）。</summary>
    public OrgNodeKind Kind { get; set; }

    /// <summary>排序主键（各表 <c>Sort</c> 列）。</summary>
    public int Sort { get; set; }

    /// <summary>排序兜底 ①（标准节点的 <c>VersionYear</c>）—— 解决「<c>Sort</c> 全 0 ⇒ 结果不稳定」。</summary>
    public int VersionYear { get; set; }

    /// <summary>排序兜底 ②（标准 <c>StandardCode</c>）+ 关联键。</summary>
    public string? StandardCode { get; set; }

    /// <summary>子节点（由 <see cref="OrgStandardTreeAssembler.Assemble"/> 装配）。</summary>
    public List<OrgStageStandardNode> Children { get; } = new();

    /// <summary>
    /// 派生列（各端点投影时取用）。
    /// <para>例：<c>["configCode"]</c> · <c>["phaseDefinitionCode"]</c> · <c>["fileCount"]</c> ·
    /// <c>["standardCount"]</c> · <c>["fillCompletion"]</c> …</para>
    /// </summary>
    public Dictionary<string, object?> Extra { get; } = new(StringComparer.Ordinal);

    /// <summary>便捷方法：写派生列（键为 <c>null</c>/空白 ⇒ 忽略，避免脏键）。</summary>
    public OrgStageStandardNode With(string key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key)) Extra[key] = value;
        return this;
    }

    /// <summary>便捷方法：读派生列（键缺失返回 <c>null</c>）。</summary>
    public object? Get(string key)
        => Extra.TryGetValue(key, out var v) ? v : null;

    /// <summary>便捷方法：读派生列并转 <typeparamref name="T"/>（缺失/类型不符 ⇒ <c>default</c>）。</summary>
    public T? GetAs<T>(string key)
    {
        var v = Get(key);
        if (v is T t) return t;
        if (v == null) return default;
        try { return (T)Convert.ChangeType(v, typeof(T)); }
        catch { return default; }
    }
}
