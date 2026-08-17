using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class WfSkillService : IWfSkillService
    {
        private readonly IWfSkillRepository _repository;

        public WfSkillService(IWfSkillRepository repository)
        {
            _repository = repository;
        }

        public static IWfSkillService Instance =>
            AutofacContainerModule.GetService<IWfSkillService>();

        // ==================== 查询 ====================

        public async Task<PageGridData<dynamic>> GetPageDataAsync(PageDataOptions options, string keyword = null, string category = null)
        {
            var query = _repository.FindAsIQueryable(x => x.Enable);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.SkillCode.Contains(keyword) || x.SkillName.Contains(keyword));
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => x.Category == category);
            }
            int totalCount = await query.CountAsync();
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = await query
                .OrderBy(x => x.SortOrder).ThenBy(x => x.SkillCode)
                .Skip((page - 1) * rows).Take(rows)
                .ToListAsync();

            var resultList = list.Select(x => (object)new
            {
                x.Id, x.Code, x.SkillCode, x.SkillName, x.SkillType, x.Category,
                x.SideEffect, x.Description, x.IsActive, x.OutputStrict, x.ReturnType,
                x.Version, x.Icon, x.Color, x.SortOrder, x.Remark,
                x.CreateDate, x.ModifyDate
            }).ToList();
            return new PageGridData<dynamic> { rows = resultList, total = totalCount };
        }

        public async Task<SkillDetailDto> GetDetailAsync(string skillCode)
        {
            var main = await _repository.FindFirstAsync(x => x.SkillCode == skillCode);
            if (main == null) return null;
            return await BuildDetailAsync(main);
        }

        public async Task<List<SkillDetailDto>> GetActiveSkillsAsync()
        {
            var mains = await _repository.FindAsIQueryable(x => x.IsActive && x.Enable)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.SkillCode)
                .ToListAsync();
            var result = new List<SkillDetailDto>();
            foreach (var main in mains)
            {
                result.Add(await BuildDetailAsync(main));
            }
            return result;
        }

        private async Task<SkillDetailDto> BuildDetailAsync(Skill main)
        {
            var db = _repository.DbContext;
            var inputs = await db.Set<WfSkillInput>()
                .Where(x => x.SkillCode == main.SkillCode)
                .OrderBy(x => x.SortOrder).ToListAsync();
            var outputs = await db.Set<WfSkillOutput>()
                .Where(x => x.SkillCode == main.SkillCode)
                .OrderBy(x => x.SortOrder).ToListAsync();
            var reflection = await db.Set<WfSkillReflection>()
                .FirstOrDefaultAsync(x => x.SkillCode == main.SkillCode);
            var api = await db.Set<WfSkillApi>()
                .FirstOrDefaultAsync(x => x.SkillCode == main.SkillCode);

            return new SkillDetailDto
            {
                Id = main.Id, Code = main.Code, SkillCode = main.SkillCode,
                SkillName = main.SkillName, SkillType = main.SkillType, Category = main.Category,
                SideEffect = main.SideEffect, Description = main.Description,
                SkillPrompt = main.SkillPrompt, IsActive = main.IsActive,
                OutputStrict = main.OutputStrict, ReturnType = main.ReturnType,
                Version = main.Version, Icon = main.Icon, Color = main.Color,
                SortOrder = main.SortOrder, Remark = main.Remark,
                Inputs = inputs, Outputs = outputs, Reflection = reflection, Api = api
            };
        }

        // ==================== 保存（主子表事务） ====================

        public async Task<(bool ok, string message)> SaveAsync(SkillDetailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillCode)) return (false, "Skill 编码不能为空");
            if (string.IsNullOrWhiteSpace(dto.SkillName)) return (false, "Skill 名称不能为空");

            await using var tx = await _repository.DbContext.Database.BeginTransactionAsync();
            try
            {
                Skill main;
                if (dto.Id > 0)
                {
                    main = await _repository.FindFirstAsync(x => x.Id == dto.Id);
                    if (main == null) return (false, "Skill 不存在");
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode && x.Id != dto.Id))
                        return (false, $"Skill 编码 {dto.SkillCode} 已存在");

                    main.SkillCode = dto.SkillCode; main.SkillName = dto.SkillName;
                    main.SkillType = dto.SkillType; main.Category = dto.Category;
                    main.SideEffect = dto.SideEffect; main.Description = dto.Description;
                    main.SkillPrompt = dto.SkillPrompt; main.IsActive = dto.IsActive;
                    main.OutputStrict = dto.OutputStrict; main.ReturnType = dto.ReturnType;
                    main.Version = dto.Version; main.Icon = dto.Icon; main.Color = dto.Color;
                    main.SortOrder = dto.SortOrder; main.Remark = dto.Remark;
                    main.ModifyDate = DateTime.Now; main.Modifier = UserContext.Current?.UserName;
                    _repository.Update(main, new[]
                    {
                        "SkillCode", "SkillName", "SkillType", "Category", "SideEffect",
                        "Description", "SkillPrompt", "IsActive", "OutputStrict", "ReturnType",
                        "Version", "Icon", "Color", "SortOrder", "Remark", "ModifyDate", "Modifier"
                    }, false);
                }
                else
                {
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode))
                        return (false, $"Skill 编码 {dto.SkillCode} 已存在");

                    main = new Skill
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        SkillCode = dto.SkillCode, SkillName = dto.SkillName,
                        SkillType = string.IsNullOrWhiteSpace(dto.SkillType) ? "method" : dto.SkillType,
                        Category = string.IsNullOrWhiteSpace(dto.Category) ? "data_process" : dto.Category,
                        SideEffect = dto.SideEffect, Description = dto.Description,
                        SkillPrompt = dto.SkillPrompt, IsActive = dto.IsActive,
                        OutputStrict = dto.OutputStrict,
                        ReturnType = string.IsNullOrWhiteSpace(dto.ReturnType) ? "json" : dto.ReturnType,
                        Version = string.IsNullOrWhiteSpace(dto.Version) ? "1.0" : dto.Version,
                        Icon = dto.Icon, Color = dto.Color, SortOrder = dto.SortOrder,
                        Remark = dto.Remark, Enable = true, Status = "active",
                        CreateDate = DateTime.Now, Creator = UserContext.Current?.UserName
                    };
                    await _repository.AddAsync(main);
                }
                await _repository.SaveChangesAsync();

                // 子表全量替换（先删后插）
                await ReplaceChildrenAsync(dto, main.SkillCode);
                await _repository.SaveChangesAsync();

                await tx.CommitAsync();
                return (true, "保存成功");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, ex.Message);
            }
        }

        private async Task ReplaceChildrenAsync(SkillDetailDto dto, string skillCode)
        {
            var db = _repository.DbContext;
            // 删除旧子表（ExecuteDelete 立即执行，不依赖 SaveChanges）
            db.Set<WfSkillInput>().Where(x => x.SkillCode == skillCode).ExecuteDelete();
            db.Set<WfSkillOutput>().Where(x => x.SkillCode == skillCode).ExecuteDelete();
            db.Set<WfSkillReflection>().Where(x => x.SkillCode == skillCode).ExecuteDelete();
            db.Set<WfSkillApi>().Where(x => x.SkillCode == skillCode).ExecuteDelete();

            var now = DateTime.Now;
            var operatorName = UserContext.Current?.UserName;

            // 输入模板
            var inputs = (dto.Inputs ?? new List<WfSkillInput>())
                .Where(x => !string.IsNullOrWhiteSpace(x.InputName))
                .Select(x => new WfSkillInput
                {
                    Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                    InputName = x.InputName, InputLabel = x.InputLabel,
                    InputType = string.IsNullOrWhiteSpace(x.InputType) ? "text" : x.InputType,
                    EnumValues = x.EnumValues, IsRequired = x.IsRequired,
                    DefaultValue = x.DefaultValue, SortOrder = x.SortOrder,
                    Enable = true, Status = "active",
                    CreateDate = now, Creator = operatorName
                }).ToList();
            if (inputs.Count > 0) await db.Set<WfSkillInput>().AddRangeAsync(inputs);

            // 输出契约
            var outputs = (dto.Outputs ?? new List<WfSkillOutput>())
                .Where(x => !string.IsNullOrWhiteSpace(x.OutputName))
                .Select(x => new WfSkillOutput
                {
                    Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                    OutputName = x.OutputName,
                    OutputType = string.IsNullOrWhiteSpace(x.OutputType) ? "json" : x.OutputType,
                    OutputPrompt = x.OutputPrompt, Description = x.Description,
                    SortOrder = x.SortOrder, Enable = true, Status = "active",
                    CreateDate = now, Creator = operatorName
                }).ToList();
            if (outputs.Count > 0) await db.Set<WfSkillOutput>().AddRangeAsync(outputs);

            // 反射信息
            if (dto.Reflection != null && !string.IsNullOrWhiteSpace(dto.Reflection.ClassPath))
            {
                await db.Set<WfSkillReflection>().AddAsync(new WfSkillReflection
                {
                    Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                    ClassPath = dto.Reflection.ClassPath,
                    MethodName = string.IsNullOrWhiteSpace(dto.Reflection.MethodName) ? "ExecuteAsync" : dto.Reflection.MethodName,
                    ParamBinding = dto.Reflection.ParamBinding,
                    Enable = true, Status = "active",
                    CreateDate = now, Creator = operatorName
                });
            }

            // API 信息
            if (dto.Api != null && !string.IsNullOrWhiteSpace(dto.Api.Url))
            {
                await db.Set<WfSkillApi>().AddAsync(new WfSkillApi
                {
                    Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                    Url = dto.Api.Url,
                    HttpMethod = string.IsNullOrWhiteSpace(dto.Api.HttpMethod) ? "POST" : dto.Api.HttpMethod,
                    Headers = dto.Api.Headers, AuthConfig = dto.Api.AuthConfig,
                    ParamMapping = dto.Api.ParamMapping, ResponseMapping = dto.Api.ResponseMapping,
                    TimeoutSeconds = dto.Api.TimeoutSeconds > 0 ? dto.Api.TimeoutSeconds : 30,
                    Enable = true, Status = "active",
                    CreateDate = now, Creator = operatorName
                });
            }
        }

        // ==================== 删除 / 启停 ====================

        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return false;
            // 先删子表（wf_skill_reflection 有 FK 约束，必须先删）
            var db = _repository.DbContext;
            db.Set<WfSkillInput>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            db.Set<WfSkillOutput>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            db.Set<WfSkillReflection>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            db.Set<WfSkillApi>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            _repository.Delete(entity, true);
            return true;
        }

        public async Task<bool> ToggleActiveAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return false;
            entity.IsActive = !entity.IsActive;
            entity.ModifyDate = DateTime.Now;
            entity.Modifier = UserContext.Current?.UserName;
            _repository.Update(entity, new[] { "IsActive", "ModifyDate", "Modifier" }, true);
            return true;
        }
    }
}
