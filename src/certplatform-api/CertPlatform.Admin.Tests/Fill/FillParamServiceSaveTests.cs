using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Shared.Domain.Scope;
using Xunit;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;
using Enterprise = CertPlatform.Shared.Entities.Cert.Enterprise;
using FillParamDef = CertPlatform.Shared.Entities.Cert.FillParamDef;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// ★ <c>IFillParamService.SaveAsync</c> 单测（`66` V2.1 第 0 批 0-6）。
///
/// <para><b>★ 覆盖面与「为什么不全」</b>：本方法可分三段 ——
/// <list type="number">
///   <item><b>前置校验</b>（空入参 / 企业不存在 / 越权 / 未定义编码）—— 只走
///         <see cref="ICoreResourceQuery"/>，<b>本文件全覆盖</b>。</item>
///   <item><b>auto 项跳过</b> + <b>返回载荷契约</b> —— 只走 <c>defMap</c> 与 <c>tx</c>，
///         <b>本文件覆盖</b>（<c>Save_AutoItem_...</c>）。</item>
///   <item><b>upsert</b>（查重 → 复活 / 新建）—— 走
///         <c>_db.Client.Queryable&lt;FillParamValue&gt;()</c>。⚠️ <c>IDbOrm.Client</c> 的静态类型是
///         <b>具体类 <c>SqlSugarClient</c></b>（<c>ISugarQueryable</c> 亦然）⇒
///         <see cref="DispatchProxy"/> <b>无法代理</b>，且本测试工程<b>无数据库连接</b>
///         ⇒ <b>该段只能由端点 A/B 覆盖</b>（已做 17 例逐字段一致，另含
///         「写入 → 读回 → 改写 → 再读回」与「软删行复活」两组端到端场景）。
///         见 <c>Save_ManualItem_ReachesWriteStage_AndFailsLoudly</c>：它把「已到达该段」
///         与「该段异常 ⇒ 响亮失败」两件事固化成断言。</item>
/// </list></para>
///
/// <para><b>★ 为什么必须补单测</b>：前置校验是<b>补参弹窗（第 2 批）</b>直接消费的错误面 ——
/// 「为什么这次保存被拒」必须由后端给出确切原因，而不是前端猜。这些分支在端点级
/// 也<b>难以穷举</b>（要造越权/未定义编码等状态），单测是唯一低成本验证手段。</para>
/// </summary>
public class FillParamServiceSaveTests
{
    private const string Ent = "ENT-CODE";
    private const string Ws = "WS-CODE";

    // ════════════════════════════════════════════════════════════════════
    //  夹具
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 造一个被测 Service。
    /// <para>⚠️ 假实现<b>只预置各用例真正需要的成员</b>，其余一律抛
    /// <see cref="NotSupportedException"/>（⛔ 不静默 <c>default</c>）——
    /// 这样「不该被调到的东西被调了」会立刻炸出来，而不是伪装成通过。</para>
    /// </summary>
    private static FillParamService Build(
        Enterprise? ent,
        IEnumerable<FillParamDef>? defs = null,
        IDbTransaction? tx = null)
    {
        var defList = (defs ?? Enumerable.Empty<FillParamDef>()).ToList();

        var core = Fake.Of<ICoreResourceQuery>((m, _) => m.Name switch
        {
            "GetEnterpriseByCodeAsync" => Task.FromResult(ent),
            "GetFillParamDefsAsync" => Task.FromResult(defList),
            _ => throw new NotSupportedException($"未预置假实现：{m.Name}"),
        });

        var db = Fake.Of<IDbOrm>((m, _) => m.Name switch
        {
            // 事务本身不是被测对象（本文件不覆盖 upsert 段）⇒ 给个无害的空事务
            "BeginTransaction" => tx ?? NoopTransaction(),
            // ⚠️ SqlSugarClient 是具体类，DispatchProxy 代理不了 ⇒ 到此为止。
            //    标记名固定，供 Save_ManualItem_ReachesWriteStage_AndFailsLoudly 断言。
            "get_Client" => throw new NotSupportedException("UPSERT_SEAM"),
            _ => throw new NotSupportedException($"未预置假实现：{m.Name}"),
        });

        return new FillParamService(db, core);
    }

    /// <summary>
    /// 空事务假实现（<c>Commit</c>/<c>Rollback</c>/<c>Dispose</c> 全是 void，返回 <c>null</c> 即可）。
    /// <para>⚠️ 必须显式写 <c>Func&lt;string, …&gt;</c> —— <see cref="Fake"/> 有
    /// <c>Func&lt;MethodInfo,…&gt;</c> / <c>Func&lt;string,…&gt;</c> 两个重载，
    /// 裸 lambda <c>(_, _) =&gt; null</c> 会 <c>CS0121</c> 二义。</para>
    /// </summary>
    private static IDbTransaction NoopTransaction()
        => Fake.Of<IDbTransaction>((Func<string, object?[]?, object?>)((_, _) => null));

    private static FillParamScope Scope(string workspaceCode = Ws, string enterpriseCode = Ent)
        => new(WorkspaceCode: workspaceCode, EnterpriseCode: enterpriseCode,
               StandardCode: string.Empty, StageCode: string.Empty);

    private static Enterprise Ent1(string orgCode = Ws, string? code = Ent)
        => new() { Code = code, Name = "测试企业", OrgCode = orgCode };

    private static FillParamDef Def(
        string paramCode, string maintainMode = "manual", string? name = null)
        => new()
        {
            Code = paramCode + "-code",
            ParamCode = paramCode,
            ParamName = name ?? ("参数-" + paramCode),
            GroupName = "基础信息",
            ValueType = "text",
            MaintainMode = maintainMode,
            StandardCode = string.Empty,
            StageCode = string.Empty,
            IsValid = 1,
            IsDeleted = false,
        };

    private static FillParamSaveItem Item(string paramCode, string? value = "v")
        => new() { ParamCode = paramCode, ParamValue = value };

    // ════════════════════════════════════════════════════════════════════
    //  ① 前置校验
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Save_EmptyItems_Fails_AndNeverTouchesAnyDependency()
    {
        // 两个假实现都「未预置任何成员」⇒ 一旦被调用就抛 ⇒ 本用例同时证明「早退」
        var svc = Build(ent: null);

        var r = await svc.SaveAsync(Scope(), Array.Empty<FillParamSaveItem>(), "USER");

        Assert.False(r.Success);
        Assert.Equal("没有需要保存的内容", r.Error);
        Assert.Null(r.Data);
    }

    [Fact]
    public async Task Save_NullItems_Fails()
    {
        var svc = Build(ent: null);

        var r = await svc.SaveAsync(Scope(), null!, "USER");

        Assert.False(r.Success);
        Assert.Equal("没有需要保存的内容", r.Error);
    }

    [Fact]
    public async Task Save_EnterpriseNotFound_Fails()
    {
        var svc = Build(ent: null, defs: new[] { Def("company_profile") });

        var r = await svc.SaveAsync(Scope(), new[] { Item("company_profile") }, "USER");

        Assert.False(r.Success);
        Assert.Equal("企业不存在或已删除", r.Error);
    }

    [Fact]
    public async Task Save_WrongWorkspace_Fails()
    {
        // 企业归属 = 另一个工作区 ⇒ 越权
        var svc = Build(ent: Ent1(orgCode: "OTHER-WS"), defs: new[] { Def("company_profile") });

        var r = await svc.SaveAsync(Scope(workspaceCode: Ws), new[] { Item("company_profile") }, "USER");

        Assert.False(r.Success);
        Assert.Equal("无权修改其他工作区的企业参数", r.Error);
    }

    [Fact]
    public async Task Save_UnknownParamCode_Fails_AndNamesTheOffendingCodes()
    {
        var svc = Build(ent: Ent1(), defs: new[] { Def("company_profile") });

        var r = await svc.SaveAsync(Scope(),
            new[] { Item("company_profile"), Item("NOT_A_PARAM"), Item("ALSO_UNKNOWN") }, "USER");

        Assert.False(r.Success);
        // ★ 错误信息必须**点名**，前端才能直接展示「哪几个编码不认识」
        Assert.Contains("NOT_A_PARAM", r.Error);
        Assert.Contains("ALSO_UNKNOWN", r.Error);
        Assert.DoesNotContain("company_profile", r.Error);
    }

    // ════════════════════════════════════════════════════════════════════
    //  ② auto 项跳过 + 返回载荷契约
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Save_AutoItem_IsSkipped_AndReportedInMessage()
    {
        // 全部为 auto ⇒ 循环内 `continue`，⛔ 一次都不碰 _db.Client
        var svc = Build(ent: Ent1(),
            defs: new[] { Def("auto_a", maintainMode: "auto", name: "企业全称"),
                          Def("auto_b", maintainMode: "auto", name: "统一社会信用代码") });

        var r = await svc.SaveAsync(Scope(),
            new[] { Item("auto_a"), Item("auto_b") }, "USER");

        Assert.True(r.Success);
        var d = r.Data!;
        Assert.Equal(0, d.savedCount);
        Assert.Equal(new[] { "企业全称", "统一社会信用代码" }, d.ignoredAuto);
        Assert.Contains("已保存 0 项", d.message);
        Assert.Contains("2 项由企业档案自动带出", d.message);
    }

    /// <summary>
    /// ★ <b>载荷属性名 = 对外 JSON 契约</b>：原实现是匿名对象
    /// <c>new { savedCount, ignoredAuto, message }</c> ⇒ 序列化即 camelCase，前端按
    /// <c>res.savedCount</c> 读。具名化时若「顺手规范命名」成 PascalCase ⇒ 前端静默读到
    /// <c>undefined</c>。本用例把这条契约钉死（反射 + JSON 两层）。
    /// </summary>
    [Fact]
    public void SaveResult_PropertyNames_AreLowerCamelContract()
    {
        var names = typeof(FillParamSaveResult).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "ignoredAuto", "message", "savedCount" }, names);

        var json = System.Text.Json.JsonSerializer.Serialize(new FillParamSaveResult
        {
            savedCount = 3,
            ignoredAuto = new List<string> { "A" },
            message = "m",
        });
        Assert.Contains("\"savedCount\":3", json);
        Assert.Contains("\"ignoredAuto\"", json);
        Assert.Contains("\"message\":\"m\"", json);
    }

    /// <summary>
    /// ★ <b>写入段：到达性 + 响亮失败</b>（<c>manual</c> 支）。
    ///
    /// <para><b>钉住两件事</b>：</para>
    /// <list type="number">
    ///   <item><b>到达性</b>：<c>manual</c> 项⛔ 不得被 <c>auto</c> 判定误跳过 ——
    ///         若有人把条件写反 / 改错 <c>ModeAuto</c> 常量，<b>所有项都会被静默跳过</b>
    ///         而接口仍报成功（本服务最危险的一类回归）。
    ///         证据 = 错误信息里出现假实现埋的 <c>UPSERT_SEAM</c> 标记。</item>
    ///   <item><b>响亮失败</b>：写入段抛出的异常被 <c>catch</c> 归一为
    ///         <c>Fail("保存失败：…")</c> ⇒ <c>success:false</c> + <c>err</c> 非空
    ///         （⛔ <b>不是</b>「静默成功」）。这正是本批修掉的那个坑的<b>反面</b>。</item>
    /// </list>
    ///
    /// <para>⚠️ <b>本用例覆盖不到的那一行</b>：<c>if (!ins.Success) throw …</c> /
    /// <c>if (!upd.Success) throw …</c>（<c>InsertAsync</c>/<c>UpdateAsync</c> 返回
    /// <c>Fail</c> 而非抛异常的分支）。要走到那里必须让 <c>_db.Client</c> 真的能查库 ——
    /// 本测试工程无数据库连接，故该分支由<b>端点 A/B</b> 覆盖
    /// （含「软删行复活」与「写入→读回→改写→再读回」两组端到端场景）。</para>
    /// </summary>
    [Fact]
    public async Task Save_ManualItem_ReachesWriteStage_AndFailsLoudly()
    {
        var svc = Build(ent: Ent1(), defs: new[] { Def("company_profile", maintainMode: "manual") });

        var r = await svc.SaveAsync(Scope(), new[] { Item("company_profile", "v1") }, "USER");

        Assert.False(r.Success);
        Assert.Contains("保存失败：", r.Error);
        Assert.Contains("UPSERT_SEAM", r.Error);   // ← 到达写入段的证据
        Assert.Null(r.Data);
    }

    /// <summary>
    /// ★ <b>负向对照</b>：<c>both</c> = 「档案可带出、也允许人工覆盖」⇒ ⛔ <b>不能</b>按 <c>auto</c> 跳过。
    ///
    /// <para>与上一例构成一对：<c>auto</c>（跳过，见 <c>Save_AutoItem_IsSkipped_…</c>）
    /// vs <c>manual</c>/<c>both</c>（不跳过）。三者必须区分开 ——
    /// 把 <c>both</c> 误当 <c>auto</c> 会让「允许覆盖」的参数永远存不进去。</para>
    /// </summary>
    [Fact]
    public async Task Save_BothMode_IsNotSkippedLikeAuto()
    {
        var svc = Build(ent: Ent1(), defs: new[] { Def("company_profile", maintainMode: "both") });

        var r = await svc.SaveAsync(Scope(), new[] { Item("company_profile", "v1") }, "USER");

        Assert.False(r.Success);
        Assert.Contains("UPSERT_SEAM", r.Error);   // ← 到达写入段的证据（未被当 auto 跳过）
    }
}
