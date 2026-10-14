using System.Collections.Generic;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    ///     ★ 参数作用域 —— 「哪个工作区 × 哪个企业 × 哪个标准 × 哪个阶段」。
    ///
    ///     <para><b>★ 铁律：全部只用 <c>Code</c></b>（`AGENTS.md` ④ 双关键字准则 A），
    ///     ⛔ 无 <c>Id</c> —— 定位 / 关联 / 前后端传参一律 <c>Code</c>。</para>
    ///
    ///     <para><b>为什么不是「机构 Code」</b>：参数<b>定义</b>归属 = 平台标准级（<c>OrgCode</c> 恒空串），
    ///     参数<b>值</b>归属 = <b>工作区</b> Code（见 `WorkspaceContextService` 对照表）。
    ///     本 record 承载的是「值」那一侧的作用域。</para>
    /// </summary>
    public sealed record FillParamScope(
        string WorkspaceCode,
        string EnterpriseCode,
        string StandardCode,
        string StageCode);

    /// <summary>
    ///     ★ <c>merge-list</c> 的载荷 —— 「后台定义 × 企业已填值」合并后的清单。
    ///
    ///     <para>⚠️ <b>属性顺序与命名 = 对外 JSON 契约</b>（<c>Program.cs</c> 设
    ///     <c>PropertyNamingPolicy = null</c> ⇒ PascalCase 原样输出）。
    ///     本类由原匿名对象具名化而来，⛔ <b>不要重排字段、不要改大小写</b> ——
    ///     前端按 <c>Groups[].Items[].ParamCode</c> 逐字读，改一处即静默渲染成空。</para>
    /// </summary>
    public sealed class FillParamListResult
    {
        /// <summary>参数定义归属（平台标准级，恒空串）</summary>
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>企业归属 = 工作区 Code</summary>
        public string WorkspaceCode { get; set; } = string.Empty;

        public string StandardCode { get; set; } = string.Empty;

        public string StageCode { get; set; } = string.Empty;

        /// <summary>企业档案快照（供顶部展示）</summary>
        public FillParamEnterpriseSnapshot Enterprise { get; set; } = new();

        public List<FillParamGroup> Groups { get; set; } = new();

        public FillParamProgress Progress { get; set; } = new();
    }

    /// <summary>
    ///     企业档案快照（<c>merge-list</c> 的 <c>Enterprise</c> 段）。
    ///     <para>⚠️ 字段顺序 = 原匿名对象顺序，⛔ 不要重排。</para>
    ///     <para>⚠️ <c>Code</c> 声明为 <c>string?</c>：原匿名对象的 <c>Code = ent.Code</c>
    ///     推断出的就是 <c>string?</c>（<c>BaseEntity.Code</c> 可空）—— 改成 <c>string</c>
    ///     只会多一条 CS8601 告警，⛔ JSON 无差别。</para>
    /// </summary>
    public sealed class FillParamEnterpriseSnapshot
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? CreditCode { get; set; }
        public string? LegalPerson { get; set; }
        public string? Address { get; set; }
        public string? IndustryType { get; set; }
        public int? EmployeeCount { get; set; }
        public string? CertScope { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? EnterpriseNo { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
    }

    /// <summary>参数分组（<c>merge-list</c> 的 <c>Groups</c> 元素）。</summary>
    public sealed class FillParamGroup
    {
        public string GroupName { get; set; } = string.Empty;

        public List<MergedItem> Items { get; set; } = new();
    }

    /// <summary>完成度（<c>merge-list</c> 的 <c>Progress</c> 段）。</summary>
    public sealed class FillParamProgress
    {
        public int Total { get; set; }
        public int Filled { get; set; }
        public int Empty { get; set; }

        /// <summary>★ 0~1 的<b>比值</b>（不是百分数），前端负责 ×100</summary>
        public double Completion { get; set; }

        public int Required { get; set; }
        public int RequiredFilled { get; set; }
    }

    /// <summary>
    ///     合并后的参数条目（<c>merge-list</c> 的 <c>Groups[].Items[]</c>）。
    ///
    ///     <para>⚠️ 序列化为 <b>PascalCase</b>（<c>Program.cs</c> 明确 <c>PropertyNamingPolicy = null</c>），
    ///     前端按 <c>item.ParamCode</c> 读，⛔ 不要写成 <c>item.paramCode</c>（会渲染成空且不报错）。</para>
    ///
    ///     <para>★ 由 <c>FillParamValueController.MergedItem</c> 原样下沉（2026-10-14 第 0 批通用 Service 抽取），
    ///     ⛔ 字段名 / 顺序 / 默认值一律未改。</para>
    /// </summary>
    public sealed class MergedItem
    {
        public string ParamCode { get; set; } = string.Empty;
        public string ParamName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "text";
        public string? EnumOptions { get; set; }
        public string MaintainMode { get; set; } = "auto";
        public string SourceKind { get; set; } = "global";
        public string? SourceExpr { get; set; }
        public bool IsRequired { get; set; }
        public bool IsBuiltin { get; set; }
        public int Sort { get; set; }
        public string? Description { get; set; }
        public string? Placeholder { get; set; }

        public string? ParamValue { get; set; }

        /// <summary>auto | manual | default | empty</summary>
        public string ValueSource { get; set; } = "empty";

        public string SourceRef { get; set; } = string.Empty;
        public bool IsManualEdited { get; set; }
        public bool IsFilled { get; set; }
        public bool Editable { get; set; }
    }

    /// <summary>单条补录项（<c>save</c> 的入参元素）。</summary>
    public sealed class FillParamSaveItem
    {
        public string ParamCode { get; set; } = string.Empty;
        public string? ParamValue { get; set; }

        // ⛔ 这里刻意**没有** `ValueSource`：本页值来源恒 `manual`
        //    （2026-10-07 用户裁决：信息由人填写，不由 AI 分析产出），
        //    由服务端写死，不接受客户端指定。
    }

    /// <summary>
    ///     <c>save</c> 的载荷。
    ///
    ///     <para>⚠️⚠️ <b>属性名必须是小写开头</b>（<c>savedCount</c> / <c>ignoredAuto</c> / <c>message</c>）——
    ///     原实现是匿名对象 <c>new { savedCount, ignoredAuto, message }</c>，序列化后就是 camelCase。
    ///     改成 PascalCase = <b>破坏前端契约</b>（前端按 <c>res.savedCount</c> 读）。
    ///     ⛔ <b>不要「顺手规范命名」</b>。</para>
    /// </summary>
    public sealed class FillParamSaveResult
    {
        public int savedCount { get; set; }
        public List<string> ignoredAuto { get; set; } = new();
        public string message { get; set; } = string.Empty;
    }
}
