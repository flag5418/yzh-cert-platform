
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Entities.Wf;

namespace CertPlatform.Admin.Services.Workflow;

/// <summary>
/// Prompt 模板服务（业务层移植）
/// <para>对照旧 Cert.Platform/Services/Admin/Platform/Wf/PromptTemplateService.cs（EF Core 实现），</para>
/// <para>职责不变：列表筛选（类型 / 适用技能）+ 按编码读取 + 取生效模板 + 保存（编码幂等 upsert）+ 逻辑删除 + 激活；</para>
/// <para>ORM 改写为新架构 IDbOrm（旧 EF DbContext 已不在新架构中）。</para>
/// </summary>
public class PromptTemplateService
{
    private readonly IDbOrm _db;
    private readonly ILogger<PromptTemplateService> _logger;

    public PromptTemplateService(IDbOrm db, ILogger<PromptTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 获取提示词列表（可按类型 / 适用技能筛选）
    /// <para>对照旧实现：skillTarget 命中「指定技能」或「无技能限制（null）」的记录</para>
    /// </summary>
    public async Task<List<PromptTemplate>> GetListAsync(string? promptType = null, string? skillTarget = null)
    {
        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.IsValid == 1
            && (string.IsNullOrEmpty(promptType) || x.PromptType == promptType)
            && (string.IsNullOrEmpty(skillTarget) || x.SkillTarget == skillTarget || x.SkillTarget == null));

        if (!result.Success)
        {
            _logger.LogWarning("Prompt 模板列表查询失败：{Error}", result.Error);
            return new List<PromptTemplate>();
        }

        return result.Data!
            .OrderBy(x => x.PromptType)
            .ThenBy(x => x.PromptCode)
            .ToList();
    }

    /// <summary>根据 prompt_code 获取单条提示词</summary>
    public async Task<PromptTemplate?> GetByCodeAsync(string promptCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode)) return null;
        var result = await _db.GetOneAsync<PromptTemplate>(
            x => x.PromptCode == promptCode && x.IsValid == 1);
        return result.Success ? result.Data : null;
    }

    /// <summary>
    /// 获取指定类型当前生效的提示词（适用技能优先精确匹配，回退到「通用 / 无技能限制」）
    /// </summary>
    public async Task<PromptTemplate?> GetActiveAsync(string promptType, string? skillTarget = null)
    {
        if (string.IsNullOrWhiteSpace(promptType)) return null;

        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.PromptType == promptType && x.IsActive == true && x.IsValid == 1);

        if (!result.Success || result.Data == null) return null;

        var candidates = result.Data;
        if (!string.IsNullOrWhiteSpace(skillTarget))
        {
            var specific = candidates.FirstOrDefault(x => x.SkillTarget == skillTarget);
            if (specific != null) return specific;
        }

        return candidates.FirstOrDefault(x => x.SkillTarget == null || x.SkillTarget == "all");
    }

    /// <summary>
    /// 创建或更新提示词（按 prompt_code 幂等匹配；更新时版本号 +1 并置为生效）
    /// </summary>
    public async Task<(bool Success, string Message)> SaveAsync(PromptTemplate entity)
    {
        if (string.IsNullOrWhiteSpace(entity.PromptCode))
            return (false, "prompt_code 不能为空");
        if (string.IsNullOrWhiteSpace(entity.PromptType))
            return (false, "prompt_type 不能为空");
        if (string.IsNullOrWhiteSpace(entity.Template))
            return (false, "template 不能为空");

        var existing = await GetByCodeAsync(entity.PromptCode);

        if (existing == null)
        {
            // 准则 A：新增 — Id 不参与分流（保持实体默认 0）
            entity.Code = Guid.NewGuid().ToString("N");
            entity.Version = 1;
            entity.IsActive = true;
            entity.IsValid = 1;
            entity.CreateTime = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(entity.Status)) entity.Status = "active";

            var inserted = await _db.InsertAsync(entity);
            if (!inserted.Success)
                return (false, inserted.Error ?? "保存失败");
            return (true, "保存成功");
        }

        // 更新：靠 Code 定位（禁止回填 Id 作 WHERE / 分流键）
        if (string.IsNullOrWhiteSpace(existing.Code))
            return (false, "更新失败：缺少业务键 Code");

        entity.Code = existing.Code;
        entity.Version = existing.Version + 1;
        entity.IsActive = true;
        entity.IsValid = 1;
        entity.CreateTime = existing.CreateTime;
        entity.CreateBy = existing.CreateBy;
        entity.UpdateTime = DateTime.Now;
        entity.DeleteBy = null;
        entity.DeleteTime = null;

        var updated = await _db.UpdateAsync(entity);
        if (!updated.Success)
            return (false, updated.Error ?? "保存失败");
        return (true, "保存成功");
    }

    /// <summary>删除提示词（逻辑禁用：IsValid = 0）</summary>
    public async Task<bool> DeleteAsync(string promptCode)
    {
        var entity = await GetByCodeAsync(promptCode);
        if (entity == null) return false;

        entity.IsValid = 0;
        entity.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(entity);
        return result.Success;
    }

    /// <summary>
    /// 激活提示词（同类型的其他提示词置为不生效，对齐旧实现的一次仅一条生效语义）
    /// </summary>
    public async Task<bool> ActivateAsync(string promptCode)
    {
        var target = await GetByCodeAsync(promptCode);
        if (target == null) return false;

        var siblings = await _db.GetListAsync<PromptTemplate>(x =>
            x.PromptType == target.PromptType && x.PromptCode != promptCode && x.IsValid == 1);

        if (siblings.Success && siblings.Data != null)
        {
            foreach (var item in siblings.Data)
            {
                item.IsActive = false;
                item.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(item);
            }
        }

        target.IsActive = true;
        target.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(target);
        return result.Success;
    }
}
