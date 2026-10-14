using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Shared.Domain.Scope;
using Xunit;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;
using FillParamDef = CertPlatform.Shared.Entities.Cert.FillParamDef;
using FillParamValue = CertPlatform.Auditor.Entities.Cert.FillParamValue;
using ISOStandard = CertPlatform.Shared.Entities.Cert.ISOStandard;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// ★ <c>IFillParamService.GetGapListAsync</c> 单测（`66` V2.1 第 0 批 0-6）。
///
/// <para><b>为什么只有这个方法有单测</b>：另两个方法（<c>GetMergedListAsync</c> / <c>SaveAsync</c>）
/// 的数据访问走 <c>_db.Client.Queryable&lt;T&gt;()</c>（SqlSugar 的 <c>ISugarQueryable</c> 流式 API，
/// 是<b>具体类</b>，DispatchProxy 无法代理）⇒ 只能用<b>端点 A/B</b> 证明（已做 17 例，逐字段一致）。
/// 而 <see cref="FillParamService.GetGapListAsync"/> 走的是
/// <see cref="IDbOrm.GetListAsync{T}"/> + <see cref="ICoreResourceQuery"/>（都是接口）⇒ 可假实现。</para>
///
/// <para><b>★ 为什么必须补单测</b>：缺参预检的唯一入口是 <c>plan</c> / <c>run</c>，而
/// <c>plan</c> 的前置门要求「全库有已发布的空白模板」——<b>当前实测为 0 份</b> ⇒ 该分支
/// <b>不可达</b>，端点 A/B <b>无法</b>覆盖它。单测是这段逻辑的唯一验证手段。</para>
///
/// <para><b>★ 其中两例是「既有缺陷」的回归测试</b>（`66` V2.1 §1.4 / §2.6 显式口径）：
/// <list type="number">
///   <item><c>DefQuery_UsesPlatformLevelScope_...</c> —— 定义过滤必须是平台级（⛔ 不是模板机构 Code）；
///         旧写法因 <c>cert_fill_param_def.OrgCode</c> 恒空串而<b>恒取不到定义</b> ⇒ 缺参清单恒空。</item>
///   <item><c>Required_Missing_TranslatesStandardNameByCode</c> —— 标准名必须按<b>标准行 Code</b> 翻译；
///         旧写法按业务编号（<c>iso13485</c>）查 ⇒ 恒回退「未登记标准」。</item>
/// </list></para>
/// </summary>
public class FillParamServiceGapTests
{
    private const string Ent = "ENT";
    private const string Stg = "STG";
    private const string StdA = "STD-A";
    private const string StdB = "STD-B";

    // ════════════════════════════════════════════════════════════════════
    //  测试夹具
    // ════════════════════════════════════════════════════════════════════

    private sealed class Harness
    {
        public FillParamService Service { get; set; } = null!;

        /// <summary><c>GetFillParamDefsAsync</c> 的实参记录（回归「平台级口径」用）。</summary>
        public List<(string? Std, string? Stage)> DefCalls { get; } = new();
    }

    /// <summary>
    /// 造一个被测 Service。假实现只预置 <c>GetFillParamDefsAsync</c> / <c>GetStandardsByCodesAsync</c>
    /// / <c>GetListAsync</c> 三个成员；其余一律抛（⛔ 不静默 default）。
    /// </summary>
    private static Harness Build(
        IEnumerable<FillParamDef> defs,
        IEnumerable<FillParamValue>? saved = null,
        IEnumerable<ISOStandard>? standards = null)
    {
        var defList = defs.ToList();
        var savedList = (saved ?? Enumerable.Empty<FillParamValue>()).ToList();
        var stdList = (standards ?? Enumerable.Empty<ISOStandard>()).ToList();

        var h = new Harness();

        var core = Fake.Of<ICoreResourceQuery>((m, a) =>
        {
            switch (m.Name)
            {
                case "GetFillParamDefsAsync":
                {
                    var sc = (string?)a![0];
                    var stc = (string?)a![1];
                    h.DefCalls.Add((sc, stc));
                    // 镜像 CoreResourceQuery.GetFillParamDefsAsync 的谓词：
                    //   OrgCode == "" ∧ (StandardCode == "" || == sc) ∧ (StageCode == "" || == stc)
                    return Task.FromResult(defList
                        .Where(d => (d.StandardCode == "" || d.StandardCode == sc)
                                    && (d.StageCode == "" || d.StageCode == stc))
                        .ToList());
                }

                case "GetStandardsByCodesAsync":
                    return Task.FromResult(stdList);

                default:
                    throw new NotSupportedException($"未预置假实现：{m.Name}");
            }
        });

        var db = Fake.Of<IDbOrm>((m, a) =>
        {
            if (m.Name != "GetListAsync") throw new NotSupportedException($"未预置假实现：{m.Name}");

            // ★ 真谓词求值（内存执行同一条 Expression）—— 让「企业/阶段/标准」三个过滤条件也进断言范围。
            //   ⚠️ 框架额外加的 IsDeleted==0 ∧ IsValid==1 不在此 Expression 内（由 SqlSugar 注入），
            //      故测试数据一律构造为有效行。
            var predicate = (Expression<Func<FillParamValue, bool>>)a![0]!;
            var f = predicate.Compile();
            return Task.FromResult(Result<List<FillParamValue>>.Ok(savedList.Where(f).ToList()));
        });

        h.Service = new FillParamService(db, core);
        return h;
    }

    private static FillParamScope Scope(string standardCode = "", string stageCode = Stg)
        => new(WorkspaceCode: "WS", EnterpriseCode: Ent, StandardCode: standardCode, StageCode: stageCode);

    private static FillParamDef Def(
        string paramCode, bool required = true, string standardCode = "", string stageCode = "",
        string? name = null, string? groupName = "基础信息", string valueType = "text")
        => new()
        {
            Code = paramCode + "-code",
            ParamCode = paramCode,
            ParamName = name ?? ("参数-" + paramCode),
            GroupName = groupName,
            ValueType = valueType,
            IsRequired = required,
            StandardCode = standardCode,
            StageCode = stageCode,
            IsValid = 1,
            IsDeleted = false,
        };

    private static FillParamValue Val(
        string paramCode, string? value, string standardCode = "", string stageCode = "")
        => new()
        {
            Code = Guid.NewGuid().ToString("N"),
            EnterpriseCode = Ent,
            StandardCode = standardCode,
            StageCode = stageCode,
            ParamCode = paramCode,
            ParamValue = value,
            IsValid = 1,
            IsDeleted = false,
        };

    private static ISOStandard Std(string code, string name, string no)
        => new() { Code = code, StandardName = name, StandardCode = no, IsValid = 1, IsDeleted = false };

    // ════════════════════════════════════════════════════════════════════
    //  1. 空输入（早退，零副作用）
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EmptyStandardCodes_ReturnsEmpty()
    {
        var h = Build(new[] { Def("a") });

        var r = await h.Service.GetGapListAsync(Scope(), Array.Empty<string>(), includeOptional: false);

        Assert.Empty(r);
        Assert.Empty(h.DefCalls);   // ⛔ 空入参不得触发取定义
    }

    [Fact]
    public async Task EmptyEnterpriseCode_ReturnsEmpty()
    {
        var h = Build(new[] { Def("a") });

        var r = await h.Service.GetGapListAsync(
            new FillParamScope("WS", "", "", Stg), new[] { StdA }, includeOptional: false);

        Assert.Empty(r);
    }

    [Fact]
    public async Task BlankStandardCodes_AreFilteredOut()
    {
        var h = Build(new[] { Def("a") });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { "", "  ", StdA }, includeOptional: false);

        var g = Assert.Single(r);
        Assert.Equal(StdA, g.StandardCode);
        Assert.Single(h.DefCalls);   // 空串已被剔除 ⇒ 只对 StdA 取一次
    }

    // ════════════════════════════════════════════════════════════════════
    //  2. ★ 缺陷② 回归：标准名按「标准行 Code」翻译
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Required_Missing_TranslatesStandardNameByCode()
    {
        var h = Build(
            new[] { Def("company_profile", required: true) },
            standards: new[] { Std(StdA, "医疗器械质量管理体系", "iso13485") });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdA }, includeOptional: false);

        var g = Assert.Single(r);
        Assert.Equal(StdA, g.StandardCode);
        // ★ 按「标准行 Code」翻译 ⇒ 命中 StandardName；
        //   ⛔ 若按业务编号（iso13485）建字典再用 Code 查，会恒回退成「未登记标准（STD-A）」
        Assert.Equal("医疗器械质量管理体系", g.StandardName);

        var item = Assert.Single(g.Items);
        Assert.Equal("company_profile", item.ParamCode);
        Assert.True(item.IsRequired);
    }

    [Fact]
    public async Task UnknownStandard_FallsBackToShortCodeLabel()
    {
        var h = Build(new[] { Def("a", required: true) });   // 主数据查不到

        var r = await h.Service.GetGapListAsync(
            Scope(), new[] { "dfca5483-f83c-4999-b773-3624de5f7cf7" }, includeOptional: false);

        var g = Assert.Single(r);
        // ★ 绝不回退裸 GUID（用户看到「系统坏了」而不是「数据缺了」）⇒ 短码占位
        Assert.Equal("未登记标准（dfca5483）", g.StandardName);
    }

    // ════════════════════════════════════════════════════════════════════
    //  3. 通配 / 标准专属 的作用域
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WildcardDef_AppliesToEveryStandard()
    {
        var h = Build(new[] { Def("doc_prefix", required: true) });   // StandardCode = ""

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdA, StdB }, includeOptional: false);

        Assert.Equal(2, r.Count);
        Assert.All(r, g => Assert.Equal("doc_prefix", Assert.Single(g.Items).ParamCode));
    }

    [Fact]
    public async Task StandardSpecificDef_AppliesOnlyToItsOwnStandard()
    {
        var h = Build(new[] { Def("only_a", required: true, standardCode: StdA) });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdA, StdB }, includeOptional: false);

        var g = Assert.Single(r);
        Assert.Equal(StdA, g.StandardCode);
        Assert.Equal("only_a", Assert.Single(g.Items).ParamCode);
    }

    [Fact]
    public async Task StageSpecificDef_IsInvisibleInAnotherStage()
    {
        var h = Build(new[] { Def("a", required: true, stageCode: "OTHER-STG") });

        Assert.Empty(await h.Service.GetGapListAsync(Scope(stageCode: Stg), new[] { StdA }, false));
        Assert.Single(await h.Service.GetGapListAsync(Scope(stageCode: "OTHER-STG"), new[] { StdA }, false));
    }

    // ════════════════════════════════════════════════════════════════════
    //  4. includeOptional 开关（D-4-3：弹窗口径 vs run 拦截口径）
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task IncludeOptionalFalse_OnlyRequiredDefs()
    {
        var h = Build(new[] { Def("req", required: true), Def("opt", required: false) });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdA }, includeOptional: false);

        Assert.Equal("req", Assert.Single(Assert.Single(r).Items).ParamCode);
    }

    [Fact]
    public async Task IncludeOptionalTrue_ReturnsOptionalWithRealIsRequired()
    {
        var h = Build(new[] { Def("req", required: true), Def("opt", required: false) });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdA }, includeOptional: true);

        var items = Assert.Single(r).Items;
        Assert.Equal(2, items.Count);
        Assert.True(items.Single(i => i.ParamCode == "req").IsRequired);
        // ★ 原实现硬编码 IsRequired=true（只取必填定义）⇒ 开选填后必须回传真实值，
        //   否则弹窗无法区分必填/选填（「显示范围 ≠ 拦截判据」的落点）
        Assert.False(items.Single(i => i.ParamCode == "opt").IsRequired);
    }

    // ════════════════════════════════════════════════════════════════════
    //  5. 「已填」判定（值表 → 差集）
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FilledNonBlank_IsNotAGap()
    {
        var h = Build(new[] { Def("a", required: true) }, new[] { Val("a", "已填值") });

        Assert.Empty(await h.Service.GetGapListAsync(Scope(), new[] { StdA }, false));
    }

    [Fact]
    public async Task FilledButWhitespace_IsStillAGap()
    {
        var h = Build(new[] { Def("a", required: true) }, new[] { Val("a", "   ") });

        Assert.Single(await h.Service.GetGapListAsync(Scope(), new[] { StdA }, false));
    }

    [Fact]
    public async Task WildcardValueRow_CoversEveryStandard()
    {
        // 值行 StandardCode = "" ⇒ 对 STD-A / STD-B 都算已填
        var h = Build(new[] { Def("a", required: true) }, new[] { Val("a", "V") });

        Assert.Empty(await h.Service.GetGapListAsync(Scope(), new[] { StdA, StdB }, false));
    }

    [Fact]
    public async Task WildcardStageValueRow_CountsAsFilledInCurrentStage()
    {
        var h = Build(new[] { Def("a", required: true) }, new[] { Val("a", "V", stageCode: "") });

        Assert.Empty(await h.Service.GetGapListAsync(Scope(stageCode: Stg), new[] { StdA }, false));
    }

    [Fact]
    public async Task AllFilled_ProducesNoGroup()
    {
        var h = Build(
            new[] { Def("a", required: true), Def("b", required: true) },
            new[] { Val("a", "1"), Val("b", "2") });

        Assert.Empty(await h.Service.GetGapListAsync(Scope(), new[] { StdA }, false));
    }

    [Fact]
    public async Task PartialFilled_OnlyMissingSurvive()
    {
        var h = Build(
            new[] { Def("a", required: true), Def("b", required: true) },
            new[] { Val("a", "1") });

        var items = Assert.Single(await h.Service.GetGapListAsync(Scope(), new[] { StdA }, false)).Items;

        Assert.Equal("b", Assert.Single(items).ParamCode);
    }

    // ════════════════════════════════════════════════════════════════════
    //  6. 多标准：顺序 = 入参顺序
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MultipleStandards_PreserveInputOrder()
    {
        var h = Build(new[]
        {
            Def("a", required: true, standardCode: StdA),
            Def("b", required: true, standardCode: StdB),
        });

        var r = await h.Service.GetGapListAsync(Scope(), new[] { StdB, StdA }, includeOptional: false);

        Assert.Equal(new[] { StdB, StdA }, r.Select(g => g.StandardCode).ToArray());
    }

    // ════════════════════════════════════════════════════════════════════
    //  7. 字段映射
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ItemFields_AreMappedFromDef()
    {
        var h = Build(new[]
        {
            Def("a", required: true, name: "参数A", groupName: null, valueType: "number"),
        });

        var item = Assert.Single(Assert.Single(
            await h.Service.GetGapListAsync(Scope(), new[] { StdA }, includeOptional: true)).Items);

        Assert.Equal("a", item.ParamCode);
        Assert.Equal("参数A", item.ParamName);
        Assert.Equal("", item.GroupName);       // null → 空串（前端按组渲染）
        Assert.Equal("number", item.ValueType);
    }

    // ════════════════════════════════════════════════════════════════════
    //  8. ★ 缺陷① 回归：定义查询走「平台级」口径
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>回归既有缺陷</b>：旧 <c>BuildParamGapsAsync</c> 的定义过滤是
    /// <c>d.OrgCode == orgCode</c>，而 <c>orgCode</c> 取的是<b>模板所属机构 Code</b>（非空）；
    /// 但 <c>cert_fill_param_def.OrgCode</c> 被 OnBeforeAdd/Update <b>强制置空串</b>
    /// （FillParamDef.cs:25-29）⇒ 两者永不相等 ⇒ <c>defs.Count == 0</c>
    /// ⇒ <b>缺参清单恒为空 ⇒ run 的缺参拦截是死代码</b>。
    ///
    /// <para>本测断言：取定义的实参是 <c>(标准行 Code, 阶段 Code)</c>，
    /// <b>⛔ 不包含任何机构 Code</b>（口径见 `66` V2.1 §1.4「取定义谓词」= <c>OrgCode == ''</c>）。</para>
    /// </summary>
    [Fact]
    public async Task DefQuery_UsesPlatformLevelScope_NotOrgCode()
    {
        var h = Build(new[] { Def("a", required: true) });

        await h.Service.GetGapListAsync(Scope(), new[] { StdA, StdB }, includeOptional: false);

        // 每个标准各取一次（GetFillParamDefsAsync 只支持单个标准）
        Assert.Equal(new[] { StdA, StdB }, h.DefCalls.Select(c => c.Std).ToArray());
        Assert.All(h.DefCalls, c => Assert.Equal(Stg, c.Stage));
        // ⛔ 旧实现会把「机构 Code」塞进 Std 位 —— 这里断言它绝不出现
        Assert.DoesNotContain(h.DefCalls, c => c.Std is null or "");
    }
}
