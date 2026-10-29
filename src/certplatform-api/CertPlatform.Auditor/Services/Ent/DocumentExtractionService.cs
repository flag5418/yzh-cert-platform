using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// <b>文档提取产物存取</b>（表 <c>cert_document_extraction</c>，粒度 = 文档 × 标准 = 一份 JSON）。
    ///
    /// <para><b>★ 唯一写入口</b>：全仓只有 <see cref="ClauseExtractExecutor"/> 调 <see cref="UpsertAsync"/>。
    /// 其它地方（替换 / 回滚 / 删除）只允许调 <see cref="MarkStaleAsync"/> 与
    /// <see cref="SoftDeleteByFileAsync"/> —— 这两个是「作废」语义，⛔ 不是「重算」。</para>
    ///
    /// <para><b>★★ 重跑判据 = <see cref="DocumentExtraction.SourceSha256"/></b>：
    /// 与 <c>cert_enterprise_doc.Sha256</c> 相等 ⇒ 字节没变 ⇒ 产物仍可信，跳过（省钱且幂等）；
    /// 不等 ⇒ 必须重跑。<c>IsStale=1</c> 只是「已知过期」的标记，<b>不是</b>重跑依据
    /// （漏标也要能靠 hash 兜住）。</para>
    ///
    /// <para><b>⚠️ <c>ClauseNumbers</c> 的查询铁律</b>：<c>FIND_IN_SET('4.2.5', ClauseNumbers)</c>。
    /// ⛔ 绝不用 <c>LIKE '%4.2.5%'</c> —— 会误中 <c>4.2.55</c> / <c>14.2.5</c>，且这类错误<b>不报错</b>，
    /// 只表现为「条款下多出几份不相干的文档」。</para>
    ///
    /// <para><b>⚠️ SqlSugar 陷阱</b>：<c>Where(x => list.Contains(x.Prop))</c> 对闭包集合翻译不可靠，
    /// 会<b>静默返回空集</b> ⇒ 本类一律「全取 + 内存过滤」，⛔ 不在 SQL 里传集合。</para>
    /// </summary>
    public sealed class DocumentExtractionService
    {
        private readonly IDbOrm _db;
        private readonly ILogger<DocumentExtractionService> _logger;

        public DocumentExtractionService(IDbOrm db, ILogger<DocumentExtractionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════
        // 读
        // ════════════════════════════════════════════════════════════════

        /// <summary>某文档的全部提取产物（含软删行，调用方自行过滤 —— 需要看到"曾经删过"时有用）</summary>
        public async Task<List<DocumentExtraction>> ListByFileAsync(string fileCode)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return new List<DocumentExtraction>();
            return await _db.Client.Queryable<DocumentExtraction>()
                       .Where(x => x.FileCode == fileCode)
                       .OrderBy(x => x.Sort)
                       .ToListAsync()
                   ?? new List<DocumentExtraction>();
        }

        /// <summary>某文档 × 某标准的一行（唯一键 <c>FileCode + StandardCode</c>）</summary>
        public async Task<DocumentExtraction?> GetAsync(string fileCode, string standardCode)
        {
            var list = await ListByFileAsync(fileCode);
            return list.FirstOrDefault(x =>
                string.Equals(x.StandardCode, standardCode, StringComparison.Ordinal));
        }

        /// <summary>
        /// ★ <b>「这个条款有哪些文档能作证」</b> —— 按条款号反查提取产物。
        ///
        /// <para>粒度是「文档 × 标准」，所以必须同时给 <paramref name="standardCode"/>
        /// （GUID）：同一份《质量手册》在 iso9001 与 iso13485 下是两行。</para>
        /// </summary>
        public async Task<List<DocumentExtraction>> ListByClauseAsync(
            string standardCode, string clauseNumber, bool onlyDone = true)
        {
            if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(clauseNumber))
                return new List<DocumentExtraction>();

            // ⚠️ FIND_IN_SET 走参数化，⛔ 不拼字符串（条款号来自标准库，但拼串习惯会传染到别处）
            var rows = await _db.Client.Queryable<DocumentExtraction>()
                .Where("StandardCode = @std AND !IsDeleted AND IsValid = 1 AND FIND_IN_SET(@clause, ClauseNumbers) > 0",
                       new { std = standardCode, clause = clauseNumber.Trim() })
                .OrderBy(x => x.Sort)
                .ToListAsync();

            var list = rows ?? new List<DocumentExtraction>();
            return onlyDone
                ? list.Where(x => x.ExtractStatus == ExtractRowStatus.Done).ToList()
                : list;
        }

        // ════════════════════════════════════════════════════════════════
        // 写
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 写入/覆盖一行产物（<c>FileCode + StandardCode</c> 唯一）。
        ///
        /// <para><b>★ 为什么不是「先删后插」</b>：删了再插会换 <c>Code</c>，
        /// 下游若已按 Code 引用（如人工批注）就全断。改为<b>原地更新</b>，<c>Code</c> 恒定。</para>
        ///
        /// <para>⚠️ 列级写回（守卫 R-A）：并发下别的列（如 <c>Remark</c>）不该被本任务覆盖。</para>
        /// </summary>
        public async Task<(bool Ok, string? Error)> UpsertAsync(
            string fileCode,
            string standardCode,
            string enterpriseCode,
            string? contentJson,
            IReadOnlyList<string> clauseNumbers,
            string sourceSha256,
            string promptCode,
            string promptVersion,
            string status,
            string? message,
            string? userCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return (false, "FileCode 为空");
            if (string.IsNullOrWhiteSpace(standardCode)) return (false, "StandardCode 为空");

            var numbers = string.Join(",", (clauseNumbers ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal));

            // ⚠️ 列宽 500：超了会被 MySQL 静默截断（strict 模式下报错）⇒ 显式截断并留痕
            if (numbers.Length > 500)
            {
                numbers = numbers[..500];
                message = string.IsNullOrWhiteSpace(message)
                    ? "条款号超过 500 字符，已截断（请减少单文档覆盖的条款数）"
                    : message + "；条款号超过 500 字符已截断";
            }

            var existing = await GetAsync(fileCode, standardCode);
            var now = DateTime.Now;

            if (existing != null && !existing.IsDeleted)
            {
                existing.EnterpriseCode = enterpriseCode;
                existing.ClauseNumbers = numbers;
                existing.ContentJson = contentJson;
                existing.ClauseCount = clauseNumbers?.Count ?? 0;
                existing.SourceSha256 = sourceSha256 ?? string.Empty;
                existing.PromptCode = promptCode ?? string.Empty;
                existing.PromptVersion = promptVersion ?? string.Empty;
                existing.ExtractStatus = status;
                existing.ExtractMessage = Truncate(message, 1000);
                existing.ExtractedAt = now;
                existing.IsStale = false;
                existing.UpdateBy = userCode;
                existing.UpdateTime = now;

                var up = await _db.UpdateAsync(existing,
                    nameof(DocumentExtraction.EnterpriseCode),
                    nameof(DocumentExtraction.ClauseNumbers),
                    nameof(DocumentExtraction.ContentJson),
                    nameof(DocumentExtraction.ClauseCount),
                    nameof(DocumentExtraction.SourceSha256),
                    nameof(DocumentExtraction.PromptCode),
                    nameof(DocumentExtraction.PromptVersion),
                    nameof(DocumentExtraction.ExtractStatus),
                    nameof(DocumentExtraction.ExtractMessage),
                    nameof(DocumentExtraction.ExtractedAt),
                    nameof(DocumentExtraction.IsStale),
                    nameof(DocumentExtraction.UpdateBy),
                    nameof(DocumentExtraction.UpdateTime));

                return up.Success ? (true, null) : (false, up.Error);
            }

            // 曾软删过的行仍占唯一键 ⇒ 复活它（⛔ 不能新插：uk 会撞，且错误文案看不出是 uk）
            if (existing is { IsDeleted: true })
            {
                existing.IsDeleted = false;
                existing.DeleteBy = null;
                existing.DeleteTime = null;
                existing.EnterpriseCode = enterpriseCode;
                existing.ClauseNumbers = numbers;
                existing.ContentJson = contentJson;
                existing.ClauseCount = clauseNumbers?.Count ?? 0;
                existing.SourceSha256 = sourceSha256 ?? string.Empty;
                existing.PromptCode = promptCode ?? string.Empty;
                existing.PromptVersion = promptVersion ?? string.Empty;
                existing.ExtractStatus = status;
                existing.ExtractMessage = Truncate(message, 1000);
                existing.ExtractedAt = now;
                existing.IsStale = false;
                existing.IsValid = 1;
                existing.UpdateBy = userCode;
                existing.UpdateTime = now;

                var revive = await _db.UpdateAsync(existing,
                    nameof(DocumentExtraction.IsDeleted),
                    nameof(DocumentExtraction.DeleteBy),
                    nameof(DocumentExtraction.DeleteTime),
                    nameof(DocumentExtraction.EnterpriseCode),
                    nameof(DocumentExtraction.ClauseNumbers),
                    nameof(DocumentExtraction.ContentJson),
                    nameof(DocumentExtraction.ClauseCount),
                    nameof(DocumentExtraction.SourceSha256),
                    nameof(DocumentExtraction.PromptCode),
                    nameof(DocumentExtraction.PromptVersion),
                    nameof(DocumentExtraction.ExtractStatus),
                    nameof(DocumentExtraction.ExtractMessage),
                    nameof(DocumentExtraction.ExtractedAt),
                    nameof(DocumentExtraction.IsStale),
                    nameof(DocumentExtraction.IsValid),
                    nameof(DocumentExtraction.UpdateBy),
                    nameof(DocumentExtraction.UpdateTime));

                return revive.Success ? (true, null) : (false, revive.Error);
            }

            var entity = new DocumentExtraction
            {
                // ⚠️ 直插必须自己给 Code（框架只在 AddCore 里自动生成）
                Code = Guid.NewGuid().ToString("N"),
                FileCode = fileCode,
                StandardCode = standardCode,
                EnterpriseCode = enterpriseCode,
                ClauseNumbers = numbers,
                ContentJson = contentJson,
                ClauseCount = clauseNumbers?.Count ?? 0,
                SourceSha256 = sourceSha256 ?? string.Empty,
                PromptCode = promptCode ?? string.Empty,
                PromptVersion = promptVersion ?? string.Empty,
                ExtractStatus = status,
                ExtractMessage = Truncate(message, 1000),
                ExtractedAt = now,
                IsStale = false,
                Sort = 0,
                IsValid = 1,
                IsDeleted = false,
                CreateBy = userCode,
                CreateTime = now,
            };

            var ins = await _db.InsertAsync(entity);
            return ins.Success ? (true, null) : (false, ins.Error);
        }

        /// <summary>
        /// 文档字节变了（替换 / 回滚）⇒ 已有产物全部置 <c>IsStale=1</c>。
        /// <para>⚠️ <b>不删行、不改 <c>ExtractStatus</c></b>：删了就看不出「曾经提过什么」；
        /// 改状态会把「已完成的旧结果」伪装成「正在跑」，前端会一直转圈。</para>
        /// </summary>
        public async Task<int> MarkStaleAsync(string fileCode)
        {
            var rows = await ListByFileAsync(fileCode);
            var n = 0;
            foreach (var row in rows.Where(x => !x.IsDeleted && !x.IsStale))
            {
                row.IsStale = true;
                row.UpdateTime = DateTime.Now;
                var r = await _db.UpdateAsync(row,
                    nameof(DocumentExtraction.IsStale),
                    nameof(DocumentExtraction.UpdateTime));
                if (r.Success) n++;
            }
            return n;
        }

        /// <summary>
        /// 文档被删除 ⇒ 产物级联软删。
        /// <para>⚠️ 与 <see cref="MarkStaleAsync"/> 的区别是语义而非程度：删文档 = 产物失去归属，
        /// 必须一起删（否则「按条款查证据」会查出已删除文件的产物）。</para>
        /// </summary>
        public async Task<int> SoftDeleteByFileAsync(string fileCode, string? userCode)
        {
            var rows = await ListByFileAsync(fileCode);
            var n = 0;
            foreach (var row in rows.Where(x => !x.IsDeleted))
            {
                row.IsDeleted = true;
                row.DeleteBy = userCode;
                row.DeleteTime = DateTime.Now;
                row.UpdateTime = DateTime.Now;
                var r = await _db.UpdateAsync(row,
                    nameof(DocumentExtraction.IsDeleted),
                    nameof(DocumentExtraction.DeleteBy),
                    nameof(DocumentExtraction.DeleteTime),
                    nameof(DocumentExtraction.UpdateTime));
                if (r.Success) n++;
                else _logger.LogWarning("[提取产物] 级联删除失败 {Code}: {Err}", row.Code, r.Error);
            }
            return n;
        }

        // ════════════════════════════════════════════════════════════════
        // 解析 ContentJson（读侧唯一入口，⛔ 别在调用方各写一份）
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 解析 <see cref="DocumentExtraction.ContentJson"/> → <see cref="ExtractedDoc"/>。
        /// <para>⚠️ 坏 JSON <b>不抛</b>：返回 <c>null</c> 并记日志 —— 让调用方按「这条没内容」处理，
        /// ⛔ 不要因为一条脏数据把整页列表打挂。</para>
        /// </summary>
        public ExtractedDoc? ParseContent(DocumentExtraction row)
        {
            if (string.IsNullOrWhiteSpace(row.ContentJson)) return null;
            try
            {
                return JsonSerializer.Deserialize<ExtractedDoc>(row.ContentJson!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("[提取产物] ContentJson 解析失败 {Code}: {Err}", row.Code, ex.Message);
                return null;
            }
        }

        private static string? Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s[..max] + "…";
        }
    }

    /// <summary><c>cert_document_extraction.ExtractStatus</c> 取值（⛔ 不许写别的字面量）</summary>
    public static class ExtractRowStatus
    {
        public const string Pending = "pending";
        public const string Running = "running";
        public const string Done = "done";
        public const string Failed = "failed";
        /// <summary>该标准下没有需要核对的条款 / 文档无相关内容 —— 跳过必须写清原因</summary>
        public const string Skipped = "skipped";
    }

    /// <summary>
    /// 提取产物 JSON 契约（<c>cert_document_extraction.ContentJson</c>）。
    /// <para>★ 铁律：<b>只产「读到什么」</b>（原文摘录 + 位置 + 置信度），⛔ 不产结论
    /// （符合/不符合由检查表判定）；找不到就交空数组，⛔ 禁推测。</para>
    /// </summary>
    public class ExtractedDoc
    {
        /// <summary>文档元信息（文件名 / 标准 / 提取时间 / 模型）</summary>
        public ExtractedDocMeta? DocMeta { get; set; }

        /// <summary>命中的条款切片</summary>
        public List<ExtractedClause> Clauses { get; set; } = new();
    }

    public class ExtractedDocMeta
    {
        public string FileName { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public string StandardName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime? ExtractedAt { get; set; }
        /// <summary>原文被切成几段送模型（&gt;1 表示长文档分段，结果已合并去重）</summary>
        public int Chunks { get; set; } = 1;
    }

    public class ExtractedClause
    {
        /// <summary>条款号 —— <b>逐字取自输入清单</b>（⛔ 不许模型自己造编号）</summary>
        public string ClauseNumber { get; set; } = string.Empty;

        /// <summary>条款标题（同清单）</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// ★ <b>锚点</b>：文档里能定位到这段的原文片段（建议 10–40 字，逐字摘录）。
        /// <para>锚点率是 P2 的出口判据之一（&gt; 80%）—— 没有锚点就等于「模型说了但指不出在哪」，
        /// 审核员无法复核，产物不可用。</para>
        /// </summary>
        public string Anchor { get; set; } = string.Empty;

        /// <summary>原文摘录（可多段，按出现顺序）</summary>
        public List<string> Excerpts { get; set; } = new();

        /// <summary>模型自评置信度 0–1（⛔ 不是「符合性结论」）</summary>
        public double Confidence { get; set; }
    }
}
