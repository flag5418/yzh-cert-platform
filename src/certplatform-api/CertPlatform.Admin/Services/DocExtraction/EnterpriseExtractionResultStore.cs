using CertPlatform.Shared.Entities.Doc;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// 企业提取结果版本店（10 号 §六 S2③；图 4 替换/移除/恢复的唯一读写口）。
///
/// <para><b>铁律</b>：行数永不减少 —— 任何失效只翻转 <c>IsValid</c>（0=归档 / 1=活跃），
/// ⛔ 禁止物理 DELETE。历史版本必须可被 <see cref="RehydrateVersionAsync"/> 原样回活。</para>
///
/// <para><b>活跃语义（D5 归档谓词按 VersionNumber 精确化）</b>：任一时刻同一 (企业, 文件)
/// 只有<b>一个版本</b>的行是活跃的；<c>uk_extract_active</c>（含 ActiveFlag 生成列）
/// 保证「同版本最多 1 行活跃」。</para>
///
/// <para><b>下游读取口径（§5.3）</b>：<c>OrgCode=企业 ∧ FileCode=槽位 ∧ IsValid=1
/// [∧ VersionNumber=当前版本]</c> —— 一律经 <see cref="GetActiveAsync"/>，禁止旁路查询。</para>
///
/// <para>实现走原生 <c>_db.Client</c>：YzhDbOrm 的 <c>GetList</c> 会叠加默认过滤，
/// 而归档/回活需要读到 <c>IsValid=0</c> 的历史行。</para>
/// </summary>
public class EnterpriseExtractionResultStore
{
    private readonly IDbOrm _db;

    public EnterpriseExtractionResultStore(IDbOrm db) => _db = db;

    /// <summary>
    /// ★ 归档某条规则的活跃结果（2026-09-30 用户裁决 J4：<b>1 文件 = 1 RuleCode</b>）。
    /// </summary>
    /// <para><b>为什么按 RuleCode 而不是 FileCode</b>：</para>
    /// <list type="number">
    ///   <item>DB 层 <c>cert_doc_extraction_rule.uk_rule_scope(OrgCode, StandardCode, StageCode, StandardFileCode)</c>
    ///         是唯一索引 ⇒ 1 文件恒对应 1 规则，两种清法等价；</item>
    ///   <item>★ 补录行（<c>ValueSource='manual'</c>）的 <c>FileCode</c> 填的是规则声明的
    ///         <c>StandardFileCode</c>，<b>不是</b>文件槽位 Code ⇒ 按 <c>FileCode</c> 清理会
    ///         <b>漏掉</b>它们；</item>
    ///   <item>同名 <c>FieldCode</c> 可能跨规则定义，RuleCode 天然收窄作用域。</item>
    /// </list>
    /// <para>⚠️ <b>连带后果（J4 的自然推论）</b>：同一规则下的<b>人工补录值会一起被归档</b>。
    /// 覆盖文件 = 该规则数据整体重算。需要由调用方弹窗提示专家重新补录
    /// （05 号 §5.3 的 <c>ChangeAction='archive'</c>，见 25 号 §七 步骤 7）。</para>
    /// <para>⛔ 仍是<b>软归档</b>（<c>IsValid=0</c>），行数不减 —— 与本类铁律一致。</para>
    /// </summary>
    /// <param name="enterpriseCode">企业 Code（注意：<c>cert_extraction_result.OrgCode</c> 列存的就是它）</param>
    /// <param name="ruleCode">规则 Code（<c>cert_doc_extraction_rule.Code</c>）</param>
    /// <param name="upToVersion">只归档 <c>VersionNumber &lt;=</c> 该版本的行；<c>null</c> = 该规则全部活跃行</param>
    /// <returns>被归档的行数（字段 + 表格）</returns>
    public async Task<int> InvalidateByRuleAsync(string enterpriseCode, string ruleCode, int? upToVersion = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(ruleCode)) return 0;
        var now = System.DateTime.Now;

        var f = _db.Client.Updateable<ExtractionResult>()
            .SetColumns(x => new ExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.RuleCode == ruleCode && x.IsValid == 1);
        if (upToVersion.HasValue) f = f.Where(x => x.VersionNumber <= upToVersion.Value);
        var fieldRows = await f.ExecuteCommandAsync();

        var t = _db.Client.Updateable<TableExtractionResult>()
            .SetColumns(x => new TableExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.RuleCode == ruleCode && x.IsValid == 1);
        if (upToVersion.HasValue) t = t.Where(x => x.VersionNumber <= upToVersion.Value);
        var tableRows = await t.ExecuteCommandAsync();

        return fieldRows + tableRows;
    }

    /// <summary>
    /// 列出某条规则下被归档的人工补录值（覆盖文件后用于弹窗提示专家重新补录）。
    /// <para>★ 对应 05 号 §5.3：重新提取覆盖了人工值 ⇒ 必须留痕 + 提示。</para>
    /// </summary>
    public async Task<(List<ExtractionResult> Fields, List<TableExtractionResult> Tables)>
    ListArchivedManualAsync(string enterpriseCode, string ruleCode, int limit = 200)
    {
        var empty = (new List<ExtractionResult>(), new List<TableExtractionResult>());
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(ruleCode)) return empty;

        var f = await _db.Client.Queryable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode
                        && x.RuleCode == ruleCode
                        && x.IsValid == 0
                        && x.ValueSource == ExtractionValueSource.Manual)
            .OrderByDescending(x => x.UpdateTime)
            .Take(limit)
            .ToListAsync();

        var t = await _db.Client.Queryable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode
                        && x.RuleCode == ruleCode
                        && x.IsValid == 0
                        && x.ValueSource == ExtractionValueSource.Manual)
            .OrderByDescending(x => x.UpdateTime)
            .Take(limit)
            .ToListAsync();

        return (f, t);
    }

    /// <summary>
    /// 归档活跃结果（图 4：替换 / 移除 ⇒ 读取口径为空）。
    /// <para>⚠️ <b>文件版本链专用</b>（服务 <see cref="RehydrateVersionAsync"/> 的按文件回活）。
    /// 业务上的"覆盖文件 / 移除文件"应调 <see cref="InvalidateByRuleAsync"/>（裁决 J4）。</para>
    /// </summary>
    /// <param name="upToVersion">只归档 <c>VersionNumber &lt;=</c> 该版本的行；<c>null</c> = 该文件全部活跃行</param>
    /// <returns>被归档的行数（B-08 + B-09）</returns>
    public async Task<int> InvalidateActiveAsync(string enterpriseCode, string fileCode, int? upToVersion = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(fileCode)) return 0;
        var now = System.DateTime.Now;

        var f = _db.Client.Updateable<ExtractionResult>()
            .SetColumns(x => new ExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1);
        if (upToVersion.HasValue) f = f.Where(x => x.VersionNumber <= upToVersion.Value);
        var fieldRows = await f.ExecuteCommandAsync();

        var t = _db.Client.Updateable<TableExtractionResult>()
            .SetColumns(x => new TableExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1);
        if (upToVersion.HasValue) t = t.Where(x => x.VersionNumber <= upToVersion.Value);
        var tableRows = await t.ExecuteCommandAsync();

        return fieldRows + tableRows;
    }

    /// <summary>
    /// 回活指定版本、归档其余版本（图 4：恢复到 vN ⇒ vN 的字段/表格原样回来）。
    /// </summary>
    /// <returns>(回活行数, 归档行数)</returns>
    public async Task<(int Rehydrated, int Archived)> RehydrateVersionAsync(
        string enterpriseCode, string fileCode, int versionNumber)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(fileCode)) return (0, 0);
        var now = System.DateTime.Now;
        int re = 0, ar = 0;

        re += await _db.Client.Updateable<ExtractionResult>()
            .SetColumns(x => new ExtractionResult { IsValid = 1, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode
                        && x.VersionNumber == versionNumber && x.IsValid == 0)
            .ExecuteCommandAsync();
        ar += await _db.Client.Updateable<ExtractionResult>()
            .SetColumns(x => new ExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode
                        && x.VersionNumber != versionNumber && x.IsValid == 1)
            .ExecuteCommandAsync();

        re += await _db.Client.Updateable<TableExtractionResult>()
            .SetColumns(x => new TableExtractionResult { IsValid = 1, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode
                        && x.VersionNumber == versionNumber && x.IsValid == 0)
            .ExecuteCommandAsync();
        ar += await _db.Client.Updateable<TableExtractionResult>()
            .SetColumns(x => new TableExtractionResult { IsValid = 0, UpdateTime = now })
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode
                        && x.VersionNumber != versionNumber && x.IsValid == 1)
            .ExecuteCommandAsync();

        return (re, ar);
    }

    /// <summary>
    /// 读取活跃结果（§5.3 下游唯一口径；带 <c>VersionNumber</c> 精确过滤）。
    /// </summary>
    /// <param name="versionNumber">槽位当前版本；<c>null</c> = 不限版本（仍只取活跃行）</param>
    public async Task<(List<ExtractionResult> Fields, List<TableExtractionResult> Tables)>
        GetActiveAsync(string enterpriseCode, string fileCode, int? versionNumber = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(fileCode))
            return (new List<ExtractionResult>(), new List<TableExtractionResult>());

        var fq = _db.Client.Queryable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1);
        if (versionNumber.HasValue) fq = fq.Where(x => x.VersionNumber == versionNumber.Value);
        var fields = await fq.ToListAsync();

        var tq = _db.Client.Queryable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1);
        if (versionNumber.HasValue) tq = tq.Where(x => x.VersionNumber == versionNumber.Value);
        var tables = await tq.ToListAsync();

        return (fields, tables);
    }

    /// <summary>当前活跃结果的总行数（验收⑥「行数不减」的计数口径）</summary>
    public async Task<int> CountActiveAsync(string enterpriseCode, string fileCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(fileCode)) return 0;
        var n = await _db.Client.Queryable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1)
            .CountAsync();
        var m = await _db.Client.Queryable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.FileCode == fileCode && x.IsValid == 1)
            .CountAsync();
        return n + m;
    }
}
