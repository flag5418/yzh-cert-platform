using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Sys;

namespace YZH.CertPlatform.Services
{
    /// <summary>
    /// YZH V3.0 页面 UI 配置服务实现
    ///
    /// 职责：
    /// 1. 从数据库查询页面级配置（YzhPageConfig）
    /// 2. 查询字段级配置（YzhFieldConfig）并组装为 DTO
    /// 3. 解析 JSON 字段（如 VisibleButtons）
    ///
    /// 设计原则：
    /// - 纯业务逻辑，不依赖 ASP.NET Core HTTP 上下文
    /// - 可在 Framework 层独立测试
    /// - Controller 只做 HTTP 适配（参数解析、状态码返回）
    /// </summary>
    public class YzhPageConfigService : IYzhPageConfigService
    {
        private readonly VOLContext _db;

        public YzhPageConfigService(VOLContext db)
        {
            _db = db;
        }

        public async Task<PageConfigResult> GetPageConfigAsync(string pageKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey))
            {
                return new PageConfigResult { Success = false, Message = "pageKey 不能为空" };
            }

            var pageMeta = await _db.Set<YzhPageConfig>()
                .FirstOrDefaultAsync(c => c.PageKey == pageKey && c.IsActive == 1);

            if (pageMeta == null)
            {
                return new PageConfigResult { Success = false, Message = $"未找到页面配置: {pageKey}" };
            }

            var fieldEntities = await _db.Set<YzhFieldConfig>()
                .Where(c => c.PageKey == pageKey)
                .OrderBy(c => c.Id)
                .ToListAsync();

            // 复用构建逻辑
            return await BuildPageConfig(pageMeta, fieldEntities);
        }

        public async Task<List<PageConfigSummary>> GetAllPageConfigsAsync()
        {
            return await _db.Set<YzhPageConfig>()
                .Where(c => c.IsActive == 1)
                .Select(c => new PageConfigSummary
                {
                    Id = c.Id,
                    PageKey = c.PageKey,
                    PageTitle = c.PageTitle,
                    EntityName = c.EntityName,
                    ControllerName = c.ControllerName,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .OrderByDescending(c => c.Id)
                .ToListAsync();
        }

        /// <summary>
        /// 批量获取所有页面的完整配置（前端启动时全量加载）
        /// 版本号基于 yzh_field_config 最大 updated_at 生成
        /// </summary>
        public async Task<AllConfigsResult> GetAllConfigsFullAsync()
        {
            try
            {
                // 1. 获取所有活跃页面
                var pages = await _db.Set<YzhPageConfig>()
                    .Where(c => c.IsActive == 1)
                    .ToListAsync();

                if (!pages.Any())
                {
                    return new AllConfigsResult { Success = true, Message = "暂无页面配置", Version = "0", Configs = new() };
                }

                // 2. 获取版本号（基于字段配置的最新更新时间）
                var versionDate = await _db.Set<YzhFieldConfig>()
                    .MaxAsync(f => (DateTime?)f.UpdatedAt);
                string version = versionDate?.ToString("yyyyMMddHHmmss") ?? DateTime.Now.ToString("yyyyMMddHHmmss");

                // 3. 获取所有字段配置（一次性查询，避免 N+1）
                var allFields = await _db.Set<YzhFieldConfig>()
                    .ToListAsync();

                // 4. 按页面分组组装
                var configs = new Dictionary<string, PageConfigData>();
                foreach (var page in pages)
                {
                    var pageResult = await BuildPageConfig(page, allFields.Where(f => f.PageKey == page.PageKey).ToList());
                    configs[page.PageKey] = new PageConfigData
                    {
                        PageMeta = pageResult.PageMeta,
                        FieldConfigs = pageResult.FieldConfigs
                    };
                }

                return new AllConfigsResult
                {
                    Success = true,
                    Message = $"OK, 共 {configs.Count} 个页面",
                    Version = version,
                    Configs = configs
                };
            }
            catch (Exception ex)
            {
                return new AllConfigsResult { Success = false, Message = ex.Message, Version = "0", Configs = new() };
            }
        }

        /// <summary>
        /// 构建单个页面的完整配置（复用逻辑，避免 GetPageConfigAsync 和 GetAllConfigsFullAsync 重复代码）
        /// </summary>
        private async Task<PageConfigResult> BuildPageConfig(YzhPageConfig pageMeta, List<YzhFieldConfig> fieldEntities)
        {
            // 解析 visible_buttons JSON
            object visibleButtons = null;
            if (!string.IsNullOrEmpty(pageMeta.VisibleButtons))
            {
                try { visibleButtons = JsonSerializer.Deserialize<object>(pageMeta.VisibleButtons); } catch { }
            }

            // 组装页面元数据
            var metaObj = new
            {
                pageKey = pageMeta.PageKey,
                pageTitle = pageMeta.PageTitle,
                entityName = pageMeta.EntityName,
                tableName = pageMeta.TableName,
                controllerName = pageMeta.ControllerName,
                keyField = pageMeta.KeyField,
                keyFieldType = pageMeta.KeyFieldType,
                sortField = pageMeta.SortField ?? "Id",
                sortOrder = pageMeta.SortOrder ?? "desc",
                dialogWidth = pageMeta.DialogWidth,
                dialogMaxHeight = pageMeta.DialogMaxHeight ?? "85vh",
                dialogLabelWidth = pageMeta.DialogLabelWidth,
                rowHeight = pageMeta.RowHeight ?? "default",
                stripe = pageMeta.Stripe == 1,
                showRowNumber = pageMeta.ShowRowNumber == 1,
                searchMode = pageMeta.SearchMode ?? "fixed",
                visibleButtons = visibleButtons ?? new[] { "add", "refresh", "batchDelete", "columnSetting" },
                showActionColumn = pageMeta.ShowActionColumn == 1,
                checkboxSelection = pageMeta.CheckboxSelection == 1,
                incrementalUpdate = pageMeta.IncrementalUpdate == 1
            };

            // 组装字段配置列表
            var fieldDtos = fieldEntities.Select(MapFieldToDto).ToList();

            return new PageConfigResult
            {
                Success = true,
                Message = "OK",
                PageMeta = metaObj,
                FieldConfigs = fieldDtos
            };
        }

        /// <summary>
        /// 将数据库实体映射为 DTO（复用方法）
        /// </summary>
        private static FieldConfigDto MapFieldToDto(YzhFieldConfig f) => new()
        {
            FieldName = f.FieldName,
            FieldAlias = !string.IsNullOrEmpty(f.FieldAlias) ? f.FieldAlias : f.FieldName,

            // 表格列
            XsFlag = f.XsFlag == 1,
            ColumnSxh = f.ColumnSxh,
            ColumnTitle = f.ColumnTitle,
            ColumnWidth = f.ColumnWidth,
            ColumnFixed = f.ColumnFixed,
            Sortable = f.Sortable == 1,
            ColumnFormatter = f.ColumnFormatter,
            ShowOverflow = f.ShowOverflow == 1,
            Align = f.Align ?? "left",

            // 弹窗表单
            BcFlag = f.BcFlag == 1,
            FormTitle = !string.IsNullOrEmpty(f.FormTitle) ? f.FormTitle : f.ColumnTitle,
            ControlType = f.ControlType ?? "input",
            GridRow = f.GridRow,
            GridCol = f.GridCol,
            GridRowSpan = f.GridRowSpan,
            GridColSpan = f.GridColSpan,
            Required = f.Required == 1,
            MaxLength = f.MaxLength,
            Placeholder = f.Placeholder,
            DefaultValue = f.DefaultValue,
            Readonly = f.Readonly == 1,
            Disabled = f.Disabled == 1,
            Precision = f.Precision,
            MinVal = f.MinVal,
            MaxVal = f.MaxVal,
            TextareaRows = f.TextareaRows,

            // 数据源
            DataKey = f.DataKey,
            RemoteUrl = f.RemoteUrl,

            // 业务控制
            GroupIndex = f.GroupIndex,

            // 搜索区
            SearchFlag = f.SearchFlag == 1,
            SearchTitle = !string.IsNullOrEmpty(f.SearchTitle) ? f.SearchTitle : f.FormTitle,
            SearchPlaceholder = f.SearchPlaceholder,
            SearchControlType = f.SearchControlType ?? f.ControlType,
            SearchWidth = f.SearchWidth
        };
    }
}
