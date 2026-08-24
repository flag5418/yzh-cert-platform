using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Ent;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 提取结果保存服务实现
    /// 核心关联：standard_file_code → cert_file_requirement.code
    /// 冗余字段：org_code / standard_code / phase_code 方便过滤和工作流引用
    /// </summary>
    public class ExtractionResultService : IExtractionResultService
    {
        private readonly VOLContext _db;

        public ExtractionResultService(VOLContext dbContext)
        {
            _db = dbContext;
        }

        /// <summary>
        /// 保存提取结果（字段级 + 表格级）
        /// 写入 standard_file_code/org_code/standard_code/phase_code 完整冗余字段
        /// </summary>
        public async Task<WebResponseContent> SaveExtractionResultAsync(
            string fileCode, string enterpriseCode, string standardFileCode, string ruleCode,
            string orgCode, string standardCode, string phaseCode,
            Dictionary<string, object> fields,
            Dictionary<string, List<Dictionary<string, object>>> tables)
        {
            if (string.IsNullOrEmpty(fileCode))
                return new WebResponseContent().Error("文件编码不能为空");

            // 1. 获取当前文件版本号
            var fileInfo = await _db.Set<EnterpriseFile>()
                .Where(x => x.Code == fileCode)
                .Select(x => new { x.CurrentVersion })
                .FirstOrDefaultAsync();
            var versionNumber = fileInfo?.CurrentVersion ?? 1;

            // 2. 保存字段级提取结果
            if (fields != null && fields.Count > 0)
            {
                foreach (var field in fields)
                {
                    var result = new ExtractionResult
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        EnterpriseCode = enterpriseCode,
                        StandardFileCode = standardFileCode,
                        StandardCode = standardCode,
                        PhaseCode = phaseCode,
                        OrgCode = orgCode,
                        FileCode = fileCode,
                        VersionNumber = versionNumber,
                        RuleCode = ruleCode,
                        FieldCode = field.Key,
                        FieldName = field.Key, // 兜底用 field_code，后续由定义表回填中文名
                        ExtractedValue = field.Value?.ToString(),
                        Confidence = 0.95m,
                        IsManualEdited = false,
                        ExtractedAt = DateTime.Now
                    };
                    _db.Set<ExtractionResult>().Add(result);
                }
            }

            // 3. 保存表格级提取结果
            if (tables != null && tables.Count > 0)
            {
                var tableIndex = 1;
                foreach (var table in tables)
                {
                    var result = new TableExtractionResult
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        EnterpriseCode = enterpriseCode,
                        StandardFileCode = standardFileCode,
                        StandardCode = standardCode,
                        PhaseCode = phaseCode,
                        OrgCode = orgCode,
                        FileCode = fileCode,
                        VersionNumber = versionNumber,
                        RuleCode = ruleCode,
                        TableIndex = tableIndex++,
                        ExtractedJson = JsonConvert.SerializeObject(table.Value),
                        Confidence = 0.90m,
                        ExtractedAt = DateTime.Now
                    };
                    _db.Set<TableExtractionResult>().Add(result);
                }
            }

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("提取结果保存成功");
        }

        /// <summary>
        /// 按文件编码获取提取结果
        /// </summary>
        public async Task<(List<object> fields, List<object> tables)> GetByFileCodeAsync(string fileCode)
        {
            var fieldResults = await _db.Set<ExtractionResult>()
                .Where(x => x.FileCode == fileCode)
                .OrderByDescending(x => x.ExtractedAt)
                .Select(x => new
                {
                    x.Code,
                    x.StandardFileCode,
                    x.RuleCode,
                    x.FieldCode,
                    x.FieldName,
                    x.ExtractedValue,
                    x.Confidence,
                    x.IsManualEdited,
                    x.ExtractedAt
                })
                .ToListAsync();

            var tableResults = await _db.Set<TableExtractionResult>()
                .Where(x => x.FileCode == fileCode)
                .OrderByDescending(x => x.ExtractedAt)
                .Select(x => new
                {
                    x.Code,
                    x.StandardFileCode,
                    x.RuleCode,
                    x.TableIndex,
                    x.ExtractedJson,
                    x.Confidence,
                    x.ExtractedAt
                })
                .ToListAsync();

            return (fieldResults.Cast<object>().ToList(), tableResults.Cast<object>().ToList());
        }
    }
}
