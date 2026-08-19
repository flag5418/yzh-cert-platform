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
using YZH.Core.Workflow;

namespace VOL.Builder.Services.CertPlatform
{
    public class WfSkillService : IWfSkillService
    {
        private readonly IWfSkillRepository _repository;
        private readonly SkillExecutor _executor;

        public WfSkillService(IWfSkillRepository repository, SkillExecutor executor)
        {
            _repository = repository;
            _executor = executor;
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
                x.Id, x.Code, x.SkillCode, x.SkillName, x.Category,
                x.Description, x.IsActive,
                x.SortOrder,
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

            return new SkillDetailDto
            {
                Id = main.Id, Code = main.Code, SkillCode = main.SkillCode,
                SkillName = main.SkillName, Category = main.Category,
                Description = main.Description, IsActive = main.IsActive,
                SortOrder = main.SortOrder,
                Inputs = inputs, Outputs = outputs, Reflection = reflection
            };
        }

        // ==================== 保存（含反射验证 + 唯一校验） ====================

        public async Task<(bool ok, string message)> SaveAsync(SkillDetailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillCode)) return (false, "Skill 编码不能为空");
            if (dto.Reflection == null || string.IsNullOrWhiteSpace(dto.Reflection.ClassPath))
                return (false, "实现类全名必填");

            var classPath = dto.Reflection.ClassPath;
            var methodName = string.IsNullOrWhiteSpace(dto.Reflection.MethodName)
                ? "ExecuteAsync" : dto.Reflection.MethodName;

            // ① 反射验证：确认 classPath + methodName 可反射分析
            var metadata = _executor.Analyze(classPath, methodName);
            if (metadata == null)
                return (false, $"反射验证失败：找不到类型 {classPath} 或方法 {methodName}，或缺少 [Skill] 特性");

            // ② 唯一性校验：classPath + methodName 在数据库中唯一
            var db = _repository.DbContext;
            var dupReflection = await db.Set<WfSkillReflection>()
                .Where(r => r.ClassPath == classPath && r.MethodName == methodName && r.Enable == true)
                .Join(db.Set<Skill>(), r => r.SkillCode, s => s.SkillCode, (r, s) => new { r.SkillCode, s.Id })
                .Where(x => x.Id != dto.Id)
                .AnyAsync();
            if (dupReflection)
                return (false, $"反射验证失败：{classPath}.{methodName} 已被其他 Skill 注册");

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                Skill main;
                if (dto.Id > 0)
                {
                    main = await _repository.FindFirstAsync(x => x.Id == dto.Id);
                    if (main == null) return (false, "Skill 不存在");
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode && x.Id != dto.Id))
                        return (false, $"Skill 编码 {dto.SkillCode} 已存在");

                    main.SkillName = metadata.Name;  // 从反射同步
                    main.Description = metadata.Description;  // 从反射同步
                    main.Category = dto.Category;
                    main.IsActive = dto.IsActive;
                    main.SortOrder = dto.SortOrder;
                    main.ModifyDate = DateTime.Now; main.Modifier = UserContext.Current?.UserName;
                    _repository.Update(main, new[]
                    {
                        "SkillName", "Category", "Description", "IsActive",
                        "SortOrder", "ModifyDate", "Modifier"
                    }, false);
                }
                else
                {
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode))
                        return (false, $"Skill 编码 {dto.SkillCode} 已存在");

                    main = new Skill
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        SkillCode = dto.SkillCode,
                        SkillName = metadata.Name,  // 从反射同步
                        SkillType = "method",
                        Category = string.IsNullOrWhiteSpace(dto.Category) ? "data_process" : dto.Category,
                        Description = metadata.Description,  // 从反射同步
                        IsActive = dto.IsActive,
                        SortOrder = dto.SortOrder,
                        Enable = true, Status = "active",
                        CreateDate = DateTime.Now, Creator = UserContext.Current?.UserName
                    };
                    await _repository.AddAsync(main);
                }
                await _repository.SaveChangesAsync();

                // ③ 同步反射信息到子表
                await ReplaceChildrenAsync(dto, main.SkillCode, metadata);
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

        /// <summary>
        /// 子表同步：反射信息写入 wf_skill_reflection，端口镜像写入 wf_skill_input / wf_skill_output（只读镜像）。
        /// </summary>
        private async Task ReplaceChildrenAsync(SkillDetailDto dto, string skillCode, SkillMetadata metadata)
        {
            var dbSet = _repository.DbContext;
            dbSet.Set<WfSkillInput>().Where(x => x.SkillCode == skillCode).ExecuteDelete();
            dbSet.Set<WfSkillOutput>().Where(x => x.SkillCode == skillCode).ExecuteDelete();
            dbSet.Set<WfSkillReflection>().Where(x => x.SkillCode == skillCode).ExecuteDelete();

            var now = DateTime.Now;
            var operatorName = UserContext.Current?.UserName;

            // 反射信息
            await dbSet.Set<WfSkillReflection>().AddAsync(new WfSkillReflection
            {
                Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                ClassPath = dto.Reflection.ClassPath,
                MethodName = string.IsNullOrWhiteSpace(dto.Reflection.MethodName) ? "ExecuteAsync" : dto.Reflection.MethodName,
                ParamBinding = dto.Reflection?.ParamBinding,
                Enable = true, Status = "active",
                CreateDate = now, Creator = operatorName
            });

            // 输入端口镜像（从反射分析结果写入）
            var existingInputs = dto.Inputs ?? new List<WfSkillInput>();
            for (int i = 0; i < metadata.InputPorts.Count; i++)
            {
                var port = metadata.InputPorts[i];
                var existing = existingInputs.FirstOrDefault(x => x.InputName == port.Name);
                await dbSet.Set<WfSkillInput>().AddAsync(new WfSkillInput
                {
                    Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                    InputName = port.Name,
                    InputLabel = existing?.InputLabel ?? port.Description,
                    InputType = port.Type,
                    IsRequired = port.Required,
                    DefaultValue = port.DefaultValue,
                    BindMode = port.BindMode,
                    EnumSource = port.EnumSource,
                    SortOrder = i + 1,
                    Enable = true, Status = "active",
                    CreateDate = now, Creator = operatorName
                });
            }

            // 输出端口镜像：标准输出 result + 业务自定义输出
            var existingOutputs = dto.Outputs ?? new List<WfSkillOutput>();
            // result 端口
            await dbSet.Set<WfSkillOutput>().AddAsync(new WfSkillOutput
            {
                Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                OutputName = "result",
                OutputType = metadata.ReturnType,
                Description = "执行结果",
                SortOrder = 1,
                Enable = true, Status = "active",
                CreateDate = now, Creator = operatorName
            });
        }

        // ==================== 删除 / 启停 ====================

        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return false;
            var db = _repository.DbContext;
            db.Set<WfSkillInput>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            db.Set<WfSkillOutput>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
            db.Set<WfSkillReflection>().Where(x => x.SkillCode == entity.SkillCode).ExecuteDelete();
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

        // ==================== 功能节点目录 ====================

        public async Task<List<object>> GetCatalogAsync()
        {
            var db = _repository.DbContext;
            var skills = await db.Set<Skill>()
                .Where(s => s.IsActive && s.Enable)
                .OrderBy(s => s.SortOrder)
                .Join(db.Set<WfSkillReflection>().Where(r => r.Enable),
                    s => s.SkillCode, r => r.SkillCode,
                    (s, r) => new { s, r })
                .ToListAsync();

            var result = new List<object>();
            foreach (var item in skills)
            {
                var metadata = _executor.Analyze(item.r.ClassPath,
                    string.IsNullOrWhiteSpace(item.r.MethodName) ? "ExecuteAsync" : item.r.MethodName);

                if (metadata == null) continue;

                result.Add(new
                {
                    classCode = metadata.Code,
                    className = metadata.Name,
                    category = item.s.Category,
                    description = metadata.Description,
                    returnType = metadata.ReturnType,
                    classPath = item.r.ClassPath,
                    methodName = item.r.MethodName,
                    inputPorts = metadata.InputPorts.Select(p => new
                    {
                        name = p.Name,
                        type = p.Type,
                        required = p.Required,
                        defaultValue = p.DefaultValue,
                        description = p.Description,
                        bindMode = p.BindMode,
                        enumSource = p.EnumSource
                    }),
                    outputPorts = new[]
                    {
                        new { name = "success", type = "boolean", description = "是否执行成功" },
                        new { name = "error", type = "string", description = "失败时的错误信息" },
                        new { name = "result", type = metadata.ReturnType, description = "执行结果" }
                    }
                });
            }
            return result;
        }
    }
}
