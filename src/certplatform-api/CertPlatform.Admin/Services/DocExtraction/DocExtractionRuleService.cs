
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Extensions;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Entities.Ent;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// 文档提取规则核心服务（业务层 #17 移植，partial：主体 + AI 编排）
/// <para>对照旧 DocExtractionRuleService.cs（1,094 行）业务编排完整保留，ORM 改写为 IDbOrm</para>
/// <para>规则键：StandardFileCode = 实际文件 FileCode（FL-xxx）或模板 Code（FR-xxx）</para>
/// </summary>
public partial class DocExtractionRuleService
{
    private readonly IDbOrm _db;
    private readonly ILogger<DocExtractionRuleService> _logger;
    protected readonly IConfiguration _configuration;
    protected readonly CertPlatform.Shared.DocExtraction.DocumentConvertClient _convertClient;
    protected readonly CertPlatform.Shared.DocExtraction.LlmInvokeService _llm;
    protected readonly IObjectStorage _storage;

    /// <summary>YZH 标准企业编码（提取结果落库目标，对照旧 CertPlatformConstants）</summary>
    public const string YzhStandardEnterpriseCode = "YZH-STD-ENT";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DocExtractionRuleService(
        IDbOrm db,
        IObjectStorage storage,
        CertPlatform.Shared.DocExtraction.DocumentConvertClient convertClient,
        CertPlatform.Shared.DocExtraction.LlmInvokeService llm,
        IConfiguration configuration,
        ILogger<DocExtractionRuleService> logger)
    {
        _db = db;
        _storage = storage;
        _convertClient = convertClient;
        _llm = llm;
        _configuration = configuration;
        _logger = logger;
    }

    // ========================================================
    // 技能定义（对照旧 _skills 静态列表）
    // ========================================================

    private static readonly List<SkillInfo> Skills = new()
    {
        new SkillInfo
        {
            Code = "word",
            Name = "Word文档提取",
            Description = "提取Word文档内容",
            SupportedExtensions = new List<string> { ".docx", ".doc" }
        },
        new SkillInfo
        {
            Code = "excel",
            Name = "Excel表格提取",
            Description = "提取Excel表格数据",
            SupportedExtensions = new List<string> { ".xlsx", ".xls", ".csv" }
        },
        new SkillInfo
        {
            Code = "pdf",
            Name = "PDF文档提取",
            Description = "提取PDF文档文本内容",
            SupportedExtensions = new List<string> { ".pdf" }
        }
    };

    public List<SkillInfo> GetSkills() => Skills;

    /// <summary>按文件扩展名权威推导技能类型（单一约束原则：不依赖前端传入）</summary>
    public static string ResolveSkill(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "word";
        var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant() ?? "";
        return ext switch
        {
            "docx" or "doc" => "word",
            "xlsx" or "xls" or "csv" => "excel",
            "pdf" => "pdf",
            _ => "word"
        };
    }

    // ========================================================
    // 保存（对照旧 SaveExtractionRuleAsync L353-518：单事务）
    // ========================================================

    /// <summary>
    /// 保存提取规则（单事务：规则 + 字段定义 + 表格定义 + 同步提取值）
    /// </summary>
    public async Task<(bool Success, string Message)> SaveExtractionRuleAsync(SaveExtractionRuleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileCode))
            return (false, "文件编码不能为空");

        // 1. 查找或创建规则（按 StandardFileCode）
        var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == request.FileCode)).Data;

        if (rule == null)
        {
            rule = new DocExtractionRule
            {
                StandardFileCode = request.FileCode,
                StandardCode = request.StandardCode,
                PhaseCode = request.PhaseCode,
                CreateTime = DateTime.Now
            };
        }
        else
        {
            rule.StandardCode = request.StandardCode;
            rule.PhaseCode = request.PhaseCode;
        }

        // 2. 更新规则信息（技能类型后端权威推导）
        rule.Skill = ResolveSkill(request.FileCode);
        rule.Prompt = request.Prompt;
        rule.DocIsValid = request.IsValid;
        rule.Status = request.IsValid ? "configured" : "failed";
        rule.UpdateTime = DateTime.Now;

        using var tx = _db.BeginTransaction();
        try
        {
            if (rule.Id == 0)
            {
                var add = await _db.InsertAsync(rule);
                if (!add.Success) { tx.Rollback(); return (false, add.Error ?? "创建规则失败"); }
            }
            else
            {
                var upd = await _db.UpdateAsync(rule);
                if (!upd.Success) { tx.Rollback(); return (false, upd.Error ?? "更新规则失败"); }
            }

            // 3. 删除旧字段定义（rule_code 关联），再写入新定义
            var oldFields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
            foreach (var f in oldFields)
                await _db.DeleteByCodeAsync<DocFieldDef>(f.Code);

            if (request.Fields?.Any() == true)
            {
                var sortOrder = 0;
                foreach (var fieldDto in request.Fields)
                {
                    var field = new DocFieldDef
                    {
                        RuleCode = rule.Code,
                        FieldName = fieldDto.Name,
                        FieldCode = string.IsNullOrEmpty(fieldDto.Code) ? ToCamel(fieldDto.Name) : fieldDto.Code,
                        DataType = fieldDto.DataType,
                        Description = fieldDto.Description,
                        IsManual = fieldDto.IsManual,
                        IsAiRecommended = fieldDto.IsAiRecommended,
                        Sort = sortOrder++,
                        CreateTime = DateTime.Now
                    };
                    var r = await _db.InsertAsync(field);
                    if (!r.Success) { tx.Rollback(); return (false, r.Error ?? "保存字段定义失败"); }
                }
            }

            // 4. 删除旧表格定义 + 表格字段定义，再写入新定义
            var oldTables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
            foreach (var t in oldTables)
            {
                var oldCols = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new();
                foreach (var c in oldCols)
                    await _db.DeleteByCodeAsync<DocTableFieldDef>(c.Code);
                await _db.DeleteByCodeAsync<DocTableDef>(t.Code);
            }

            if (request.Tables?.Any() == true)
            {
                var tableSortOrder = 0;
                foreach (var tableDto in request.Tables)
                {
                    // 表格定义 Code 为 GUID；TableCode 同时作为表格字段定义外键（对照旧逻辑）
                    var tableCode = Guid.NewGuid().ToString("N");
                    var table = new DocTableDef
                    {
                        Code = tableCode,
                        RuleCode = rule.Code,
                        TableName = tableDto.Name,
                        TableCode = string.IsNullOrEmpty(tableDto.Code) ? ToCamel(tableDto.Name) : tableDto.Code,
                        Description = tableDto.Description,
                        Sort = tableSortOrder++,
                        CreateTime = DateTime.Now
                    };
                    var rt = await _db.InsertAsync(table);
                    if (!rt.Success) { tx.Rollback(); return (false, rt.Error ?? "保存表格定义失败"); }

                    if (tableDto.Columns?.Any() == true)
                    {
                        var colSortOrder = 0;
                        foreach (var colDto in tableDto.Columns)
                        {
                            var col = new DocTableFieldDef
                            {
                                TableCode = tableCode,
                                ColumnName = colDto.Name,
                                ColumnCode = string.IsNullOrEmpty(colDto.Code) ? ToCamel(colDto.Name) : colDto.Code,
                                DataType = colDto.DataType,
                                Sort = colSortOrder++,
                                CreateTime = DateTime.Now
                            };
                            var rc = await _db.InsertAsync(col);
                            if (!rc.Success) { tx.Rollback(); return (false, rc.Error ?? "保存表格字段失败"); }
                        }
                    }
                }
            }

            // 5. 同步提取结果到 B-08/B-09（YZH-STD-ENT），供工作流验证
            await SyncExtractionResultToB08B09Async(rule, request);

            tx.Commit();
            _logger.LogInformation("[DocExtractionRule] 保存成功: {FileCode}, fields={F}, tables={T}",
                request.FileCode, request.Fields?.Count ?? 0, request.Tables?.Count ?? 0);
            return (true, "保存成功");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "[DocExtractionRule] 保存失败: {FileCode}", request.FileCode);
            return (false, $"保存失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 将规则保存请求中的提取数据同步到 B-08/B-09（YZH 标准企业）。
    /// <para>规则对照旧 SyncExtractionResultToB08B09Async（L528-612）：</para>
    /// <para>1. extractionData 为空 → 跳过；2. 物理删除旧结果（绕过软删拦截 + 唯一约束兜底）；</para>
    /// <para>3. 一致性过滤（只写定义中存在的 code）；4. 空值/空表格不写入</para>
    /// </summary>
    private async Task SyncExtractionResultToB08B09Async(DocExtractionRule rule, SaveExtractionRuleRequest request)
    {
        var extractionData = request.ExtractionData;
        if (extractionData == null) return;

        var now = DateTime.Now;
        var fileCode = rule.StandardFileCode ?? "";

        // 已保存定义中的合法 code 集合 + fieldCode→中文名映射
        var fieldNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var validFieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in request.Fields ?? new())
        {
            var code = string.IsNullOrEmpty(f.Code) ? ToCamel(f.Name) : f.Code;
            if (string.IsNullOrEmpty(code)) continue;
            validFieldCodes.Add(code);
            fieldNameMap[code] = f.Name ?? code;
        }
        var validTableCodes = (request.Tables ?? new())
            .Select(t => string.IsNullOrEmpty(t.Code) ? ToCamel(t.Name) : t.Code)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. 物理删除旧结果（对照旧注释：软删残留会与唯一约束冲突，必须原生 SQL）
        await _db.Client.Deleteable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == fileCode)
            .ExecuteCommandAsync();
        await _db.Client.Deleteable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == fileCode)
            .ExecuteCommandAsync();

        // 2. 字段级 → B-08（LabelTag = field_code，对照 V4 评审报告 §7）
        if (extractionData.Fields != null)
        {
            foreach (var kv in extractionData.Fields)
            {
                if (!validFieldCodes.Contains(kv.Key)) continue;
                var value = kv.Value?.ToString();
                if (string.IsNullOrWhiteSpace(value)) continue;

                var er = new ExtractionResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    EnterpriseCode = YzhStandardEnterpriseCode,
                    StandardFileCode = fileCode,
                    StandardCode = rule.StandardCode,
                    PhaseCode = rule.PhaseCode,
                    FileCode = fileCode,
                    VersionNumber = 1,
                    RuleCode = rule.Code,
                    FieldCode = kv.Key,
                    FieldName = fieldNameMap.TryGetValue(kv.Key, out var fn) ? fn : kv.Key,
                    LabelTag = kv.Key,
                    ExtractedValue = value,
                    ExtractedAt = now,
                    CreateTime = DateTime.Now
                };
                await _db.Client.Insertable(er).ExecuteCommandAsync();
            }
        }

        // 3. 表格级 → B-09（每表格一条，extracted_json = 行数组 JSON）
        if (extractionData.Tables != null)
        {
            var tableIndex = 1;
            foreach (var kv in extractionData.Tables)
            {
                if (!validTableCodes.Contains(kv.Key)) continue;
                var rows = kv.Value;
                if (rows == null || rows.Count == 0) continue;

                var tr = new TableExtractionResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    EnterpriseCode = YzhStandardEnterpriseCode,
                    StandardFileCode = fileCode,
                    StandardCode = rule.StandardCode,
                    PhaseCode = rule.PhaseCode,
                    FileCode = fileCode,
                    VersionNumber = 1,
                    RuleCode = rule.Code,
                    TableIndex = tableIndex++,
                    ExtractedJson = System.Text.Json.JsonSerializer.Serialize(rows, JsonOptions),
                    ExtractedAt = now,
                    CreateTime = DateTime.Now
                };
                await _db.Client.Insertable(tr).ExecuteCommandAsync();
            }
        }
    }

    // ========================================================
    // 详情（对照旧 GetRuleDetailAsync L624-738）
    // ========================================================

    /// <summary>
    /// 获取规则详情（含字段/表格定义 + 已保存的提取值回显）
    /// </summary>
    public async Task<RuleDetailResponse?> GetRuleDetailAsync(string standardFileCode)
    {
        var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == standardFileCode)).Data;
        if (rule == null) return null;

        // 读取 YZH 标准企业提取结果（B-08 字段值 / B-09 表格行），保存后重新进入可完整回显
        var b08Rows = await _db.Client.Queryable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == standardFileCode && !x.IsDeleted)
            .Select(x => new B08Row { FieldCode = x.FieldCode, ExtractedValue = x.ExtractedValue })
            .ToListAsync();
        var b08Map = b08Rows.GroupBy(x => x.FieldCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ExtractedValue ?? "", StringComparer.OrdinalIgnoreCase);

        var b09Rows = await _db.Client.Queryable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == standardFileCode)
            .OrderBy(x => x.TableIndex)
            .Select(x => new B09Row { TableIndex = x.TableIndex, ExtractedJson = x.ExtractedJson })
            .ToListAsync();
        var b09List = b09Rows;

        // 字段定义（rule_code 关联，按 Sort 排序）
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        var fieldDtos = fields.OrderBy(x => x.Sort).Select(x => new FieldDefDto
        {
            Name = x.FieldName,
            // NameEn 与 Code 同源（DB 只有一列 FieldCode）——不填会让前端「英文名」输入框回显为空
            NameEn = x.FieldCode,
            Code = x.FieldCode,
            DataType = x.DataType,
            Description = x.Description,
            IsManual = x.IsManual,
            IsAiRecommended = x.IsAiRecommended ?? true
        }).ToList();
        foreach (var f in fieldDtos)
        {
            if (!string.IsNullOrEmpty(f.Code) && b08Map.TryGetValue(f.Code, out var val))
                f.ExtractedValue = val;
        }

        // 表格定义 + 列定义
        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        var tableDtos = new List<TableDefDto>();
        foreach (var table in tables.OrderBy(x => x.Sort))
        {
            var columns = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == table.Code)).Data ?? new();
            tableDtos.Add(new TableDefDto
            {
                Name = table.TableName,
                NameEn = table.TableCode,
                Code = table.TableCode,
                Description = table.Description,
                Columns = columns.OrderBy(x => x.Sort).Select(x => new TableColumnDto
                {
                    Name = x.ColumnName,
                    NameEn = x.ColumnCode,
                    Code = x.ColumnCode,
                    DataType = x.DataType
                }).ToList()
            });
        }

        // 表格提取行数据回显（B-09 按 TableIndex 与表格定义顺序一一对应）
        for (int i = 0; i < tableDtos.Count && i < b09List.Count; i++)
        {
            try
            {
                var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(b09List[i].ExtractedJson ?? "[]", JsonOptions);
                if (rows != null) tableDtos[i].ExtractedData = rows;
            }
            catch { /* JSON 损坏时跳过该行回显 */ }
        }

        return new RuleDetailResponse
        {
            Id = rule.Id,
            Code = rule.Code,
            StandardFileCode = rule.StandardFileCode ?? "",
            StandardCode = rule.StandardCode ?? "",
            PhaseCode = rule.PhaseCode ?? "",
            Skill = rule.Skill,
            Prompt = rule.Prompt,
            IsValid = rule.DocIsValid,
            Status = rule.Status ?? "none",
            Fields = fieldDtos,
            Tables = tableDtos,
            CreateTime = rule.CreateTime,
            UpdateTime = rule.UpdateTime
        };
    }

    // ========================================================
    // 删除（对照旧 DeleteRuleAsync L740-775：级联删定义）
    // ========================================================

    public async Task<bool> DeleteRuleAsync(string standardFileCode)
    {
        var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == standardFileCode)).Data;
        if (rule == null) return false;

        // 级联删除字段和表格定义
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        foreach (var f in fields)
            await _db.DeleteByCodeAsync<DocFieldDef>(f.Code);

        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        foreach (var t in tables)
        {
            var cols = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new();
            foreach (var c in cols)
                await _db.DeleteByCodeAsync<DocTableFieldDef>(c.Code);
            await _db.DeleteByCodeAsync<DocTableDef>(t.Code);
        }

        // 提取结果同步删除（物理删除，与 save 逻辑对齐）
        await _db.Client.Deleteable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == standardFileCode)
            .ExecuteCommandAsync();
        await _db.Client.Deleteable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == YzhStandardEnterpriseCode && x.StandardFileCode == standardFileCode)
            .ExecuteCommandAsync();

        await _db.DeleteByCodeAsync<DocExtractionRule>(rule.Code);
        return true;
    }

    // ========================================================
    // 已配置规则列表（对照旧 GetConfiguredRulesAsync L853-879）
    // ========================================================

    /// <summary>获取已配置提取规则的文档列表（供工作流配置页面选择文档）</summary>
    /// <remarks>
    /// ⚠️ 列名对照真实表结构（2026-09-19 snake→Pascal 迁移后校准）：
    /// cert_doc_extraction_rule / cert_standard_directory_file 均已是 PascalCase 列名。
    /// 另：SqlSugarDbOrm.SqlQueryAsync 失败时返回 Result.Fail 而非抛异常（被 ORM 吞掉），
    /// SQL 列名错误只会表现为“接口 200 + 空数组”，需结合后端日志排查。
    /// </remarks>
    public async Task<List<object>> GetConfiguredRulesAsync()
    {
        var result = await _db.GetListAsync<ConfiguredRuleView>();
        return result.Data?.Cast<object>().ToList() ?? new();
    }

    // ========================================================
    // 字段/表格定义查询（对照旧 GetFieldsAndTablesAsync L881-913）
    // ========================================================

    /// <summary>获取规则的字段和表格定义（供 docField/docTable 节点选择）</summary>
    public async Task<object?> GetFieldsAndTablesAsync(string ruleCode)
    {
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == ruleCode)).Data ?? new();

        return new
        {
            fields = fields.OrderBy(x => x.Sort).Select(x => new
            {
                fieldCode = x.FieldCode,
                fieldName = x.FieldName,
                dataType = x.DataType,
                description = x.Description
            }).ToList(),
            tables = tables.OrderBy(x => x.Sort).Select(x => new
            {
                tableCode = x.TableCode,
                tableName = x.TableName,
                description = x.Description
            }).ToList()
        };
    }

    // ========================================================
    // 辅助
    // ========================================================

    /// <summary>中文名 → 英文驼峰（对照旧 ToPascalCase 后首字母小写的实际效果）</summary>
    private static string ToCamel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return name.ToCamelCase();
    }

    // raw SQL 行 DTO
    private class B08Row
    {
        public string FieldCode { get; set; } = "";
        public string? ExtractedValue { get; set; }
    }

    private class B09Row
    {
        public int TableIndex { get; set; }
        public string? ExtractedJson { get; set; }
    }

    /// <summary>
    /// 已配置规则行 DTO（供工作流设计器 docField/docTable 下拉）
    /// <para>序列化约定（D-5 契约）：显式 camelCase — 旧后端 GetConfiguredRulesAsync 返回匿名对象 camelCase
    /// （ruleCode/fileName/standardFileCode），前端 NodePropertyForm 按 camelCase 消费，
    /// 迁移为 DTO 类时若依赖默认序列化会变 PascalCase 导致下拉 label/value 为空。</para>
    /// </summary>
    private class ConfiguredRuleRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("ruleCode")]
        public string RuleCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("standardFileCode")]
        public string StandardFileCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("standardCode")]
        public string StandardCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("phaseCode")]
        public string PhaseCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("skill")]
        public string Skill { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("isValid")]
        public bool DocIsValid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("createDate")]
        public DateTime? CreateTime { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("modifyDate")]
        public DateTime? UpdateTime { get; set; }
    }
}
