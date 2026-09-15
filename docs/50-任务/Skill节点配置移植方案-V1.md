# Skill 节点配置移植方案

> **版本**：V1.0 | **日期**：2026-09-15 | **状态**：设计稿
>
> 本文档定义了从历史项目（Vol框架）移植Skill节点配置到新架构（YZH.Core）的完整方案。

---

## 一、移植范围分析

### 1.1 历史项目Skill体系结构

| 层级 | 组件 | 文件位置 | 说明 |
|------|------|---------|------|
| **数据库** | 5张核心表 | `DB/mysql/` | wf_skill、wf_skill_reflection、wf_skill_input、wf_skill_output、wf_skill_category |
| **实体定义** | 5个实体类 | `YZH.Entity/Admin/Platform/Wf/` | Skill、WfSkillReflection、WfSkillInput、WfSkillOutput、WfSkillCategory |
| **服务层** | 2个核心服务 | `Cert.Platform/Services/Admin/Platform/Wf/` | WfSkillService、CertSkillRegistry |
| **框架层** | 3个核心类 | `YZH.Core/WorkFlow/` | SkillExecutor、SkillBase、SkillAttributes |
| **控制器** | 1个API控制器 | `YZH.WebApi/Controllers/Admin/Platform/` | WfSkillController |
| **前端页面** | 1个管理页面 | `vol.web/src/views/cert/admin/business/Standard/SkillManage/` | Skill管理页面 |
| **前端组件** | 1个面板组件 | `vol.web/src/components/workflow-designer/` | SkillPanel.vue（工作流设计器节点面板） |

### 1.2 当前新架构状态

| 层级 | 组件 | 状态 | 位置 |
|------|------|------|------|
| **实体定义** | 5个实体类 | ✅ 已存在 | `CertPlatform.Shared/Entities/Wf/` |
| **前端页面** | Skill管理页面 | ✅ 已存在 | `cert-admin/src/pages/workflow/job-skill/index.vue` |
| **前端API** | API定义 | ✅ 已存在 | `cert-share/src/api/workflow/job-skill.ts` |
| **服务层** | WfSkillService | ❌ 缺失 | 需要创建 |
| **框架层** | SkillExecutor | ❌ 缺失 | 需要创建 |
| **控制器** | WfSkillController | ❌ 缺失 | 需要创建 |
| **数据库** | 5张表 | ❌ 缺失 | 需要创建 |

### 1.3 关键差异分析

| 差异项 | 历史项目 | 新架构 | 解决方案 |
|--------|---------|--------|---------|
| **审计字段** | snake_case（create_by/create_date） | PascalCase（CreateBy/CreateTime） | 新实体已使用PascalCase，数据库也用PascalCase |
| **基类** | EntityBase（VOL.Entity） | EntityBase（YZH.Entity.Admin.Platform） | 已有兼容存根 |
| **依赖注入** | Autofac | 标准DI | 使用Microsoft.Extensions.DependencyInjection |
| **ORM** | EntityFramework + SqlSugar | SqlSugar | 统一使用SqlSugar |
| **前端框架** | Vue 3 + Element Plus | Vue 3 + Element Plus + YzhTable | 已有页面使用新架构组件 |

---

## 二、数据库迁移方案

### 2.1 目标位置

`scripts/db/001_skill_tables_V1.sql`

### 2.2 表结构设计

遵循项目全局规则§十六命名铁律，所有字段使用PascalCase：

```sql
-- ============================================================
-- Skill 节点配置表结构
-- 版本：V1.0
-- 日期：2026-09-15
-- ============================================================

-- 1. wf_skill（Skill主表）
CREATE TABLE IF NOT EXISTS wf_skill (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    Code VARCHAR(36) NOT NULL COMMENT 'GUID业务键',
    SkillCode VARCHAR(100) NOT NULL COMMENT 'Skill编码，唯一',
    SkillName VARCHAR(200) NOT NULL COMMENT '中文名',
    SkillType VARCHAR(20) NOT NULL DEFAULT 'method' COMMENT '类型：method（反射执行）',
    Category VARCHAR(50) DEFAULT 'data_process' COMMENT '分类编码',
    SideEffect TINYINT(1) DEFAULT 0 COMMENT '副作用标记：0=纯函数 1=有副作用',
    Description TEXT COMMENT '功能说明',
    SkillPrompt TEXT COMMENT 'Skill提示词（AI节点用）',
    IsActive TINYINT(1) DEFAULT 1 COMMENT '启用状态',
    OutputStrict TINYINT(1) DEFAULT 1 COMMENT '输出强约束：1=按wf_skill_output校验 0=放行',
    ReturnType VARCHAR(20) DEFAULT 'json' COMMENT '返回类型：string/number/date/boolean/json',
    Version VARCHAR(20) DEFAULT '1.0' COMMENT '版本号',
    Icon VARCHAR(50) COMMENT '图标',
    Color VARCHAR(20) COMMENT '颜色',
    SortOrder INT DEFAULT 0 COMMENT '排序号',
    OrgCode VARCHAR(50) COMMENT '机构码（多租户隔离）',
    CreateBy VARCHAR(50) COMMENT '创建人',
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UpdateBy VARCHAR(50) COMMENT '更新人',
    UpdateTime DATETIME COMMENT '更新时间',
    DeleteBy VARCHAR(50) COMMENT '删除人',
    DeleteTime DATETIME COMMENT '删除时间',
    IsDeleted TINYINT(1) DEFAULT 0 COMMENT '软删除标记',
    IsValid INT DEFAULT 1 COMMENT '有效标志：1=有效 0=无效',
    Status VARCHAR(50) DEFAULT 'active' COMMENT '状态',
    Enable TINYINT(1) DEFAULT 1 COMMENT '启用',
    Remark VARCHAR(500) COMMENT '备注',
    UNIQUE KEY uk_skill_code (SkillCode),
    INDEX idx_category (Category),
    INDEX idx_is_active (IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Skill主表';

-- 2. wf_skill_reflection（反射配置表）
CREATE TABLE IF NOT EXISTS wf_skill_reflection (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    Code VARCHAR(36) NOT NULL COMMENT 'GUID业务键',
    SkillCode VARCHAR(100) NOT NULL COMMENT 'Skill编码',
    ClassPath VARCHAR(500) NOT NULL COMMENT '实现类全名',
    MethodName VARCHAR(200) DEFAULT 'ExecuteAsync' COMMENT '方法名',
    ParamBinding TEXT COMMENT '参数绑定JSON：{"输入项名":"方法参数名"}',
    Enable TINYINT(1) DEFAULT 1 COMMENT '启用',
    CreateBy VARCHAR(50) COMMENT '创建人',
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UpdateBy VARCHAR(50) COMMENT '更新人',
    UpdateTime DATETIME COMMENT '更新时间',
    DeleteBy VARCHAR(50) COMMENT '删除人',
    DeleteTime DATETIME COMMENT '删除时间',
    IsDeleted TINYINT(1) DEFAULT 0 COMMENT '软删除标记',
    IsValid INT DEFAULT 1 COMMENT '有效标志',
    Status VARCHAR(50) DEFAULT 'active' COMMENT '状态',
    Remark VARCHAR(500) COMMENT '备注',
    UNIQUE KEY uk_class_method (ClassPath, MethodName),
    INDEX idx_skill_code (SkillCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Skill反射配置表';

-- 3. wf_skill_input（输入端口镜像表）
CREATE TABLE IF NOT EXISTS wf_skill_input (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    Code VARCHAR(36) NOT NULL COMMENT 'GUID业务键',
    SkillCode VARCHAR(100) NOT NULL COMMENT 'Skill编码',
    InputName VARCHAR(100) NOT NULL COMMENT '输入参数名',
    InputLabel VARCHAR(200) COMMENT '输入标签（中文描述）',
    InputType VARCHAR(20) DEFAULT 'text' COMMENT '输入类型：text/number/date/boolean/enum/json',
    BindMode VARCHAR(20) DEFAULT 'LinkOrConstant' COMMENT '绑定模式：Link/LinkOrConstant/Enum',
    EnumSource VARCHAR(100) COMMENT '字典编码（BindMode=Enum时必填）',
    IsRequired TINYINT(1) DEFAULT 0 COMMENT '是否必填',
    DefaultValue VARCHAR(500) COMMENT '默认值',
    SortOrder INT DEFAULT 0 COMMENT '排序号',
    Enable TINYINT(1) DEFAULT 1 COMMENT '启用',
    CreateBy VARCHAR(50) COMMENT '创建人',
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UpdateBy VARCHAR(50) COMMENT '更新人',
    UpdateTime DATETIME COMMENT '更新时间',
    DeleteBy VARCHAR(50) COMMENT '删除人',
    DeleteTime DATETIME COMMENT '删除时间',
    IsDeleted TINYINT(1) DEFAULT 0 COMMENT '软删除标记',
    IsValid INT DEFAULT 1 COMMENT '有效标志',
    Status VARCHAR(50) DEFAULT 'active' COMMENT '状态',
    Remark VARCHAR(500) COMMENT '备注',
    UNIQUE KEY uk_skill_input (SkillCode, InputName),
    INDEX idx_skill_code (SkillCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Skill输入端口镜像表';

-- 4. wf_skill_output（输出端口镜像表）
CREATE TABLE IF NOT EXISTS wf_skill_output (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    Code VARCHAR(36) NOT NULL COMMENT 'GUID业务键',
    SkillCode VARCHAR(100) NOT NULL COMMENT 'Skill编码',
    OutputName VARCHAR(100) NOT NULL COMMENT '输出参数名',
    OutputType VARCHAR(20) DEFAULT 'json' COMMENT '输出类型：string/number/date/boolean/json',
    OutputPrompt TEXT COMMENT '输出提示词',
    Description VARCHAR(500) COMMENT '描述',
    SortOrder INT DEFAULT 0 COMMENT '排序号',
    Enable TINYINT(1) DEFAULT 1 COMMENT '启用',
    CreateBy VARCHAR(50) COMMENT '创建人',
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UpdateBy VARCHAR(50) COMMENT '更新人',
    UpdateTime DATETIME COMMENT '更新时间',
    DeleteBy VARCHAR(50) COMMENT '删除人',
    DeleteTime DATETIME COMMENT '删除时间',
    IsDeleted TINYINT(1) DEFAULT 0 COMMENT '软删除标记',
    IsValid INT DEFAULT 1 COMMENT '有效标志',
    Status VARCHAR(50) DEFAULT 'active' COMMENT '状态',
    Remark VARCHAR(500) COMMENT '备注',
    UNIQUE KEY uk_skill_output (SkillCode, OutputName),
    INDEX idx_skill_code (SkillCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Skill输出端口镜像表';

-- 5. wf_skill_category（Skill分类表）
CREATE TABLE IF NOT EXISTS wf_skill_category (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    Code VARCHAR(36) NOT NULL COMMENT 'GUID业务键',
    CategoryCode VARCHAR(50) NOT NULL COMMENT '分类编码',
    CategoryName VARCHAR(100) NOT NULL COMMENT '分类名称',
    Icon VARCHAR(50) COMMENT '图标',
    Color VARCHAR(20) COMMENT '颜色',
    SortOrder INT DEFAULT 0 COMMENT '排序号',
    OrgCode VARCHAR(50) COMMENT '机构码',
    CreateBy VARCHAR(50) COMMENT '创建人',
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UpdateBy VARCHAR(50) COMMENT '更新人',
    UpdateTime DATETIME COMMENT '更新时间',
    DeleteBy VARCHAR(50) COMMENT '删除人',
    DeleteTime DATETIME COMMENT '删除时间',
    IsDeleted TINYINT(1) DEFAULT 0 COMMENT '软删除标记',
    IsValid INT DEFAULT 1 COMMENT '有效标志',
    Status VARCHAR(50) DEFAULT 'active' COMMENT '状态',
    Enable TINYINT(1) DEFAULT 1 COMMENT '启用',
    Remark VARCHAR(500) COMMENT '备注',
    UNIQUE KEY uk_category_code (CategoryCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Skill分类表';

-- ============================================================
-- 初始数据
-- ============================================================

-- 插入默认分类
INSERT INTO wf_skill_category (Code, CategoryCode, CategoryName, Color, SortOrder, Enable, Status, CreateTime)
VALUES
    (UUID(), 'data_access', '数据获取', '#409EFF', 1, 1, 'active', NOW()),
    (UUID(), 'data_process', '数据处理', '#67C23A', 2, 1, 'active', NOW()),
    (UUID(), 'ai_judge', 'AI判断', '#E6A23C', 3, 1, 'active', NOW()),
    (UUID(), 'ai_generate', 'AI生成', '#F56C6C', 4, 1, 'active', NOW()),
    (UUID(), 'output', '输出', '#909399', 5, 1, 'active', NOW())
ON DUPLICATE KEY UPDATE CategoryName = VALUES(CategoryName);

-- 插入初始Skill（compare和assemble）
INSERT INTO wf_skill (Code, SkillCode, SkillName, SkillType, Category, Description, IsActive, ReturnType, Enable, Status, CreateTime)
VALUES
    (UUID(), 'compare', '值比较', 'method', 'data_process', '确定性比较：支持数值比较和日期比较', 1, 'boolean', 1, 'active', NOW()),
    (UUID(), 'assemble', '文本拼接', 'method', 'data_process', '前后两段文本按连接符拼接', 1, 'string', 1, 'active', NOW())
ON DUPLICATE KEY UPDATE SkillName = VALUES(SkillName);

-- 插入反射配置
INSERT INTO wf_skill_reflection (Code, SkillCode, ClassPath, MethodName, Enable, Status, CreateTime)
VALUES
    (UUID(), 'compare', 'YZH.Core.Skills.CompareSkill', 'ExecuteAsync', 1, 'active', NOW()),
    (UUID(), 'assemble', 'YZH.Core.Skills.AssembleSkill', 'ExecuteAsync', 1, 'active', NOW())
ON DUPLICATE KEY UPDATE ClassPath = VALUES(ClassPath);

-- 插入标准输出端口（每个Skill都有success/error/result）
INSERT INTO wf_skill_output (Code, SkillCode, OutputName, OutputType, Description, SortOrder, Enable, Status, CreateTime)
SELECT UUID(), s.SkillCode, 'success', 'boolean', '是否执行成功', 1, 1, 'active', NOW()
FROM wf_skill s
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_output o WHERE o.SkillCode = s.SkillCode AND o.OutputName = 'success');

INSERT INTO wf_skill_output (Code, SkillCode, OutputName, OutputType, Description, SortOrder, Enable, Status, CreateTime)
SELECT UUID(), s.SkillCode, 'error', 'string', '失败时的错误信息', 2, 1, 'active', NOW()
FROM wf_skill s
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_output o WHERE o.SkillCode = s.SkillCode AND o.OutputName = 'error');

INSERT INTO wf_skill_output (Code, SkillCode, OutputName, OutputType, Description, SortOrder, Enable, Status, CreateTime)
SELECT UUID(), s.SkillCode, 'result', s.ReturnType, '执行结果（业务数据）', 3, 1, 'active', NOW()
FROM wf_skill s
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_output o WHERE o.SkillCode = s.SkillCode AND o.OutputName = 'result');
```

---

## 三、后端迁移方案

### 3.1 目标位置

| 组件 | 目标位置 |
|------|---------|
| **接口定义** | `CertPlatform.Shared/IServices/Workflow/` |
| **服务实现** | `CertPlatform.Shared/Services/Workflow/` |
| **控制器** | `CertPlatform.Admin/Controllers/Workflow/` |
| **框架层** | `CertPlatform.Shared/WorkflowEngine/` |
| **仓储层** | `CertPlatform.Shared/IRepositories/Workflow/` + `CertPlatform.Shared/Repositories/Workflow/` |

### 3.2 接口定义

#### 3.2.1 IWfSkillService

```csharp
// CertPlatform.Shared/IServices/Workflow/IWfSkillService.cs
using YZH.Entity.DomainModels;
using YZH.Entity.Admin.Platform.Wf;

namespace CertPlatform.Shared.IServices.Workflow
{
    public interface IWfSkillService
    {
        /// <summary>分页查询Skill列表</summary>
        Task<PageGridData<dynamic>> GetPageDataAsync(PageDataOptions options, string keyword = null, string category = null);

        /// <summary>获取Skill详情（含反射信息、输入输出端口）</summary>
        Task<SkillDetailDto> GetDetailAsync(string skillCode);

        /// <summary>获取所有启用的Skill列表</summary>
        Task<List<SkillDetailDto>> GetActiveSkillsAsync();

        /// <summary>保存Skill（含反射验证、唯一性校验）</summary>
        Task<(bool ok, string message)> SaveAsync(SkillDetailDto dto);

        /// <summary>删除Skill（级联删除子表）</summary>
        Task<bool> DeleteAsync(long id);

        /// <summary>切换启用状态</summary>
        Task<bool> ToggleActiveAsync(long id);

        /// <summary>获取功能节点目录（工作流设计器用）</summary>
        Task<List<object>> GetCatalogAsync();
    }
}
```

#### 3.2.2 IWfSkillCategoryService

```csharp
// CertPlatform.Shared/IServices/Workflow/IWfSkillCategoryService.cs
using YZH.Entity.DomainModels;
using YZH.Entity.Admin.Platform.Wf;

namespace CertPlatform.Shared.IServices.Workflow
{
    public interface IWfSkillCategoryService
    {
        /// <summary>获取分类列表</summary>
        Task<List<WfSkillCategory>> GetListAsync();

        /// <summary>保存分类</summary>
        Task<(bool ok, string message)> SaveAsync(WfSkillCategory category);

        /// <summary>删除分类</summary>
        Task<bool> DeleteAsync(long id);

        /// <summary>切换启用状态</summary>
        Task<bool> ToggleActiveAsync(long id);
    }
}
```

#### 3.2.3 ISkillRegistry

```csharp
// CertPlatform.Shared/WorkflowEngine/ISkillRegistry.cs
using YZH.Core.Workflow;

namespace CertPlatform.Shared.WorkflowEngine
{
    /// <summary>
    /// Skill注册表接口（V2静态方法版）
    /// </summary>
    public interface ISkillRegistry
    {
        /// <summary>加载Skill元数据</summary>
        Task<SkillMetadata?> LoadAsync(string skillCode, CancellationToken ct = default);

        /// <summary>执行Skill</summary>
        Task<SkillResult> ExecuteAsync(string skillCode, SkillContext context, CancellationToken ct = default);
    }
}
```

### 3.3 服务实现

#### 3.3.1 WfSkillService

```csharp
// CertPlatform.Shared/Services/Workflow/WfSkillService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YZH.Core.BaseProvider;
using YZH.Core.ManageUser;
using YZH.Core.Utilities;
using YZH.Entity.Admin.Platform.Wf;
using YZH.Entity.DomainModels;
using CertPlatform.Shared.IRepositories.Workflow;
using CertPlatform.Shared.IServices.Workflow;
using YZH.Core.Workflow;

namespace CertPlatform.Shared.Services.Workflow
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
                x.CreateTime, x.UpdateTime
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

        public async Task<(bool ok, string message)> SaveAsync(SkillDetailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillCode)) return (false, "Skill编码不能为空");
            if (dto.Reflection == null || string.IsNullOrWhiteSpace(dto.Reflection.ClassPath))
                return (false, "实现类全名必填");

            var classPath = dto.Reflection.ClassPath;
            var methodName = string.IsNullOrWhiteSpace(dto.Reflection.MethodName)
                ? "ExecuteAsync" : dto.Reflection.MethodName;

            // ① 反射验证
            var metadata = _executor.Analyze(classPath, methodName);
            if (metadata == null)
                return (false, $"反射验证失败：找不到类型 {classPath} 或方法 {methodName}，或缺少 [Skill] 特性");

            // ② 唯一性校验
            var db = _repository.DbContext;
            var dupReflection = await db.Set<WfSkillReflection>()
                .Where(r => r.ClassPath == classPath && r.MethodName == methodName && r.Enable == true)
                .Join(db.Set<Skill>(), r => r.SkillCode, s => s.SkillCode, (r, s) => new { r.SkillCode, s.Id })
                .Where(x => x.Id != dto.Id)
                .AnyAsync();
            if (dupReflection)
                return (false, $"反射验证失败：{classPath}.{methodName} 已被其他Skill注册");

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                Skill main;
                if (dto.Id > 0)
                {
                    main = await _repository.FindFirstAsync(x => x.Id == dto.Id);
                    if (main == null) return (false, "Skill不存在");
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode && x.Id != dto.Id))
                        return (false, $"Skill编码 {dto.SkillCode} 已存在");

                    main.SkillName = string.IsNullOrWhiteSpace(dto.SkillName) ? metadata.Name : dto.SkillName;
                    main.Description = string.IsNullOrWhiteSpace(dto.Description) ? metadata.Description : dto.Description;
                    main.Category = dto.Category;
                    main.IsActive = dto.IsActive;
                    main.SortOrder = dto.SortOrder;
                    main.UpdateTime = DateTime.Now; main.UpdateBy = UserContext.Current?.UserName;
                    _repository.Update(main, new[]
                    {
                        "SkillName", "Category", "Description", "IsActive",
                        "SortOrder", "UpdateTime", "UpdateBy"
                    }, false);
                }
                else
                {
                    if (await _repository.ExistsAsync(x => x.SkillCode == dto.SkillCode))
                        return (false, $"Skill编码 {dto.SkillCode} 已存在");

                    main = new Skill
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        SkillCode = dto.SkillCode,
                        SkillName = string.IsNullOrWhiteSpace(dto.SkillName) ? metadata.Name : dto.SkillName,
                        SkillType = "method",
                        Category = string.IsNullOrWhiteSpace(dto.Category) ? "data_process" : dto.Category,
                        Description = string.IsNullOrWhiteSpace(dto.Description) ? metadata.Description : dto.Description,
                        IsActive = dto.IsActive,
                        SortOrder = dto.SortOrder,
                        Enable = true, Status = "active",
                        CreateTime = DateTime.Now, CreateBy = UserContext.Current?.UserName
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
                CreateTime = now, CreateBy = operatorName
            });

            // 输入端口镜像
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
                    CreateTime = now, CreateBy = operatorName
                });
            }

            // 输出端口镜像：标准输出 success + error + result
            await dbSet.Set<WfSkillOutput>().AddAsync(new WfSkillOutput
            {
                Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                OutputName = "success", OutputType = "boolean",
                Description = "是否执行成功",
                SortOrder = 1, Enable = true, Status = "active",
                CreateTime = now, CreateBy = operatorName
            });
            await dbSet.Set<WfSkillOutput>().AddAsync(new WfSkillOutput
            {
                Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                OutputName = "error", OutputType = "string",
                Description = "失败时的错误信息",
                SortOrder = 2, Enable = true, Status = "active",
                CreateTime = now, CreateBy = operatorName
            });
            await dbSet.Set<WfSkillOutput>().AddAsync(new WfSkillOutput
            {
                Code = Guid.NewGuid().ToString("N"), SkillCode = skillCode,
                OutputName = "result", OutputType = metadata.ReturnType,
                Description = "执行结果",
                SortOrder = 3, Enable = true, Status = "active",
                CreateTime = now, CreateBy = operatorName
            });
        }

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
            entity.UpdateTime = DateTime.Now;
            entity.UpdateBy = UserContext.Current?.UserName;
            _repository.Update(entity, new[] { "IsActive", "UpdateTime", "UpdateBy" }, true);
            return true;
        }

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
                    skillCode = metadata.Code,
                    skillName = metadata.Name,
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
```

#### 3.3.2 SkillExecutor

```csharp
// CertPlatform.Shared/WorkflowEngine/SkillExecutor.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.Workflow;

namespace CertPlatform.Shared.WorkflowEngine
{
    /// <summary>
    /// Skill执行器：反射调用静态方法 + 标准输出包装
    /// </summary>
    public class SkillExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SkillExecutor> _logger;

        public SkillExecutor(IServiceProvider serviceProvider, ILogger<SkillExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>执行Skill</summary>
        public async Task<SkillResult> ExecuteAsync(
            string classPath, string methodName,
            SkillContext context, CancellationToken ct = default)
        {
            var standardOutputs = new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = string.Empty,
                ["result"] = new Dictionary<string, object>()
            };

            try
            {
                var type = ResolveType(classPath);
                if (type == null)
                {
                    standardOutputs["error"] = $"无法找到类型: {classPath}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                var skillAttr = type.GetCustomAttribute<SkillAttribute>();
                if (skillAttr == null)
                {
                    standardOutputs["error"] = $"类型 {classPath} 缺少 [Skill] 特性";
                    return SkillResult.Ok(standardOutputs, null);
                }

                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    standardOutputs["error"] = $"类型 {classPath} 中找不到静态方法: {methodName}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                var args = BindArguments(method.GetParameters(), context, ct);
                var missing = ValidateRequired(method.GetParameters(), context);
                if (missing.Count > 0)
                {
                    standardOutputs["error"] = $"{skillAttr.Code} 缺少必填入参: {string.Join(", ", missing)}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                var result = await (Task<SkillResult>)method.Invoke(null, args)!;

                if (result.Success)
                {
                    standardOutputs["success"] = true;
                    standardOutputs["result"] = result.Outputs ?? new Dictionary<string, object>();
                    if (result.Outputs != null)
                        foreach (var kv in result.Outputs)
                            standardOutputs[kv.Key] = kv.Value;
                }
                else
                {
                    standardOutputs["error"] = result.Error ?? string.Empty;
                }

                var final = SkillResult.Ok(standardOutputs, result.Confidence);
                final.PromptTokens = result.PromptTokens;
                final.CompletionTokens = result.CompletionTokens;
                return final;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Skill执行异常: {ClassPath}.{MethodName}", classPath, methodName);
                standardOutputs["error"] = $"执行异常: {ex.Message}";
                return SkillResult.Ok(standardOutputs, null);
            }
        }

        /// <summary>反射分析Skill元数据</summary>
        public SkillMetadata? Analyze(string classPath, string methodName = "ExecuteAsync")
        {
            var type = ResolveType(classPath);
            if (type == null) return null;

            var skillAttr = type.GetCustomAttribute<SkillAttribute>();
            if (skillAttr == null) return null;

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) return null;

            var inputPorts = new List<SkillPortInfo>();
            foreach (var p in method.GetParameters())
            {
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                if (p.ParameterType == typeof(CancellationToken) ||
                    p.ParameterType == typeof(CancellationToken?)) continue;

                var paramAttr = p.GetCustomAttribute<SkillParamAttribute>();
                inputPorts.Add(new SkillPortInfo
                {
                    Name = p.Name ?? string.Empty,
                    Type = MapCSharpType(p.ParameterType),
                    Required = !p.HasDefaultValue,
                    DefaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                    Description = paramAttr?.Description ?? string.Empty,
                    BindMode = paramAttr?.BindMode.ToString() ?? "LinkOrConstant",
                    EnumSource = paramAttr?.EnumSource
                });
            }

            return new SkillMetadata
            {
                Code = skillAttr.Code,
                Name = skillAttr.Name,
                ReturnType = skillAttr.ReturnType,
                Description = skillAttr.Description,
                ClassPath = classPath,
                MethodName = methodName,
                InputPorts = inputPorts
            };
        }

        private static Type? ResolveType(string classPath)
        {
            var type = Type.GetType(classPath);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetType(classPath, false);
                    if (type != null) return type;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static string MapCSharpType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(string)) return "string";
            if (underlying == typeof(bool)) return "boolean";
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
                return "date";
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(double) ||
                underlying == typeof(decimal) || underlying == typeof(float))
                return "number";
            return "json";
        }

        private object?[] BindArguments(ParameterInfo[] parameters, SkillContext context, CancellationToken ct)
        {
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.GetCustomAttribute<FromServiceAttribute>() != null)
                    args[i] = _serviceProvider.GetService(p.ParameterType);
                else if (p.ParameterType == typeof(CancellationToken))
                    args[i] = ct;
                else
                {
                    if (context.Inputs != null && context.Inputs.TryGetValue(p.Name!, out var val))
                        args[i] = ConvertValue(val, p.ParameterType);
                    else if (p.HasDefaultValue)
                        args[i] = p.DefaultValue;
                    else
                        args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                }
            }
            return args;
        }

        private static List<string> ValidateRequired(ParameterInfo[] parameters, SkillContext context)
        {
            var missing = new List<string>();
            foreach (var p in parameters)
            {
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                if (p.ParameterType == typeof(CancellationToken)) continue;
                if (p.HasDefaultValue) continue;
                if (context.Inputs == null ||
                    !context.Inputs.TryGetValue(p.Name!, out var v) ||
                    v == null ||
                    string.IsNullOrWhiteSpace(v?.ToString()))
                    missing.Add(p.Name!);
            }
            return missing;
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying == typeof(string)) return value.ToString();
            if (underlying == typeof(bool)) return Convert.ToBoolean(value);
            if (underlying == typeof(int)) return Convert.ToInt32(value);
            if (underlying == typeof(long)) return Convert.ToInt64(value);
            if (underlying == typeof(double)) return Convert.ToDouble(value);
            if (underlying == typeof(decimal)) return Convert.ToDecimal(value);
            if (underlying == typeof(DateTime)) return Convert.ToDateTime(value);
            if (underlying == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value.ToString()!);
            return value;
        }
    }
}
```

### 3.4 控制器实现

```csharp
// CertPlatform.Admin/Controllers/Workflow/WfSkillController.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.BaseProvider;
using YZH.Entity.Admin.Platform.Wf;
using YZH.Entity.DomainModels;
using CertPlatform.Shared.IServices.Workflow;
using YZH.Core.Workflow;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// Skill管理（V2静态方法版）
    /// </summary>
    [Route("api/skill")]
    [Authorize]
    public class WfSkillController : ControllerBase
    {
        private readonly IWfSkillService _service;
        private readonly SkillExecutor _executor;

        public WfSkillController(IWfSkillService service, SkillExecutor executor)
        {
            _service = service;
            _executor = executor;
        }

        [HttpPost("page")]
        public async Task<IActionResult> GetPage([FromBody] PageDataOptions options,
            [FromQuery] string keyword = null,
            [FromQuery] string category = null)
        {
            var result = await _service.GetPageDataAsync(options, keyword, category);
            return Ok(new { status = true, data = result });
        }

        [HttpGet("list-active")]
        public async Task<IActionResult> GetActiveSkills()
        {
            var list = await _service.GetActiveSkillsAsync();
            return Ok(new { status = true, data = list });
        }

        [HttpGet("query-nodes")]
        public async Task<IActionResult> GetNodeCatalog()
        {
            var catalog = await _service.GetCatalogAsync();
            return Ok(new { status = true, data = catalog });
        }

        [HttpPost("analyze")]
        public IActionResult Analyze([FromBody] AnalyzeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ClassPath))
                return Ok(new { status = false, message = "请填写实现类全名" });

            var methodName = string.IsNullOrWhiteSpace(req.MethodName) ? "ExecuteAsync" : req.MethodName;
            var metadata = _executor.Analyze(req.ClassPath, methodName);

            if (metadata == null)
                return Ok(new { status = false, message = $"反射失败：找不到类型 {req.ClassPath} 或方法 {methodName}，或缺少 [Skill] 特性" });

            return Ok(new { status = true, data = metadata });
        }

        [HttpGet("{skillCode}")]
        public async Task<IActionResult> GetDetail(string skillCode)
        {
            var detail = await _service.GetDetailAsync(skillCode);
            if (detail == null) return Ok(new { status = false, message = "Skill不存在" });
            return Ok(new { status = true, data = detail });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SkillDetailDto dto)
        {
            var (ok, message) = await _service.SaveAsync(dto);
            return Ok(new { status = ok, message });
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return Ok(new { status = result, message = result ? "删除成功" : "删除失败" });
        }

        [HttpPost("toggle-active/{id}")]
        public async Task<IActionResult> ToggleActive(long id)
        {
            var result = await _service.ToggleActiveAsync(id);
            return Ok(new { status = result, message = result ? "操作成功" : "操作失败" });
        }
    }

    public class AnalyzeRequest
    {
        public string ClassPath { get; set; } = string.Empty;
        public string MethodName { get; set; } = "ExecuteAsync";
    }
}
```

### 3.5 依赖注入注册

```csharp
// CertPlatform.Shared/Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using CertPlatform.Shared.IServices.Workflow;
using CertPlatform.Shared.Services.Workflow;
using CertPlatform.Shared.IRepositories.Workflow;
using CertPlatform.Shared.Repositories.Workflow;
using CertPlatform.Shared.WorkflowEngine;

namespace CertPlatform.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowServices(this IServiceCollection services)
        {
            // Skill服务
            services.AddScoped<IWfSkillService, WfSkillService>();
            services.AddScoped<IWfSkillCategoryService, WfSkillCategoryService>();
            services.AddScoped<IWfSkillRepository, WfSkillRepository>();
            services.AddScoped<IWfSkillCategoryRepository, WfSkillCategoryRepository>();

            // Skill执行器
            services.AddSingleton<SkillExecutor>();
            services.AddSingleton<ISkillRegistry, CertSkillRegistry>();

            return services;
        }
    }
}
```

---

## 四、前端迁移方案

### 4.1 当前状态

前端已经基本完成迁移：

| 组件 | 状态 | 位置 |
|------|------|------|
| **Skill管理页面** | ✅ 已存在 | `cert-admin/src/pages/workflow/job-skill/index.vue` |
| **API定义** | ✅ 已存在 | `cert-share/src/api/workflow/job-skill.ts` |
| **类型定义** | ✅ 已存在 | Skill、SkillCategory、AnalyzedSkill等接口 |

### 4.2 需要补充的前端组件

#### 4.2.1 SkillPanel组件（工作流设计器节点面板）

**目标位置**：`cert-admin/src/components/workflow-designer/SkillPanel.vue`

该组件已从历史项目迁移，需要适配新架构的API调用方式。

### 4.3 API路由适配

历史项目API路由 vs 新架构API路由：

| 功能 | 历史项目路由 | 新架构路由 |
|------|------------|----------|
| 分页查询 | `POST /api/skill/page` | `POST /api/skill/page` |
| 获取详情 | `GET /api/skill/{skillCode}` | `GET /api/skill/{skillCode}` |
| 保存 | `POST /api/skill` | `POST /api/skill` |
| 删除 | `POST /api/skill/delete/{id}` | `POST /api/skill/delete/{id}` |
| 启停 | `POST /api/skill/toggle-active/{id}` | `POST /api/skill/toggle-active/{id}` |
| 反射验证 | `POST /api/skill/analyze` | `POST /api/skill/analyze` |
| 节点目录 | `GET /api/skill/query-nodes` | `GET /api/skill/query-nodes` |
| 分类列表 | `POST /api/skill-category/list` | `POST /api/skill-category/list` |

---

## 五、实施步骤

### 第一阶段：数据库准备（预计1小时）

1. 创建数据库迁移脚本：`scripts/db/001_skill_tables_V1.sql`
2. 执行脚本创建表结构
3. 验证表结构和初始数据

### 第二阶段：后端框架层（预计2小时）

1. 创建`CertPlatform.Shared/WorkflowEngine/`目录
2. 实现SkillExecutor类
3. 实现ISkillRegistry接口
4. 实现CertSkillRegistry类

### 第三阶段：后端服务层（预计3小时）

1. 创建接口定义文件
2. 实现WfSkillService服务
3. 实现WfSkillCategoryService服务
4. 创建Repository层代码

### 第四阶段：后端控制器（预计1小时）

1. 创建WfSkillController
2. 创建WfSkillCategoryController
3. 配置路由和权限

### 第五阶段：依赖注入配置（预计0.5小时）

1. 在Startup.cs中注册服务
2. 验证依赖注入配置

### 第六阶段：前端适配（预计1小时）

1. 验证现有前端页面与新API的兼容性
2. 补充SkillPanel组件（如需要）
3. 测试前端功能

### 第七阶段：测试验证（预计2小时）

1. 单元测试skill执行逻辑
2. 集成测试API接口
3. 端到端测试完整流程

---

## 六、文件清单

### 6.1 后端文件

| 文件类型 | 目标位置 | 说明 |
|---------|---------|------|
| SQL脚本 | `scripts/db/001_skill_tables_V1.sql` | 数据库迁移 |
| 接口 | `CertPlatform.Shared/IServices/Workflow/IWfSkillService.cs` | Skill服务接口 |
| 接口 | `CertPlatform.Shared/IServices/Workflow/IWfSkillCategoryService.cs` | 分类服务接口 |
| 服务 | `CertPlatform.Shared/Services/Workflow/WfSkillService.cs` | Skill服务实现 |
| 服务 | `CertPlatform.Shared/Services/Workflow/WfSkillCategoryService.cs` | 分类服务实现 |
| 控制器 | `CertPlatform.Admin/Controllers/Workflow/WfSkillController.cs` | Skill API |
| 控制器 | `CertPlatform.Admin/Controllers/Workflow/WfSkillCategoryController.cs` | 分类API |
| 执行器 | `CertPlatform.Shared/WorkflowEngine/SkillExecutor.cs` | Skill执行引擎 |
| 注册表 | `CertPlatform.Shared/WorkflowEngine/ISkillRegistry.cs` | 注册表接口 |
| 注册表 | `CertPlatform.Shared/WorkflowEngine/CertSkillRegistry.cs` | 注册表实现 |
| 仓储 | `CertPlatform.Shared/IRepositories/Workflow/IWfSkillRepository.cs` | 仓储接口 |
| 仓储 | `CertPlatform.Shared/Repositories/Workflow/WfSkillRepository.cs` | 仓储实现 |
| 扩展 | `CertPlatform.Shared/Extensions/ServiceCollectionExtensions.cs` | DI注册 |

### 6.2 前端文件（已存在）

| 文件类型 | 位置 | 状态 |
|---------|------|------|
| 管理页面 | `cert-admin/src/pages/workflow/job-skill/index.vue` | ✅ 已存在 |
| API定义 | `cert-share/src/api/workflow/job-skill.ts` | ✅ 已存在 |
| SkillPanel | `cert-admin/src/components/workflow-designer/SkillPanel.vue` | 需适配 |

---

## 七、风险与注意事项

### 7.1 风险点

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 审计字段命名差异 | 数据查询失败 | 新实体已使用PascalCase，数据库也用PascalCase |
| 反射执行找不到类型 | Skill无法执行 | 确保程序集正确加载，添加详细日志 |
| 前端API路由不匹配 | 前端调用失败 | 保持API路由与历史项目一致 |
| 依赖注入未注册 | 服务无法解析 | 检查Startup.cs中的DI配置 |

### 7.2 注意事项

1. **命名规范**：严格遵循项目全局规则§十六，所有字段使用PascalCase
2. **审计字段**：使用新架构的审计字段命名（CreateBy/CreateTime等）
3. **基类使用**：继承`YZH.Entity.Admin.Platform.EntityBase`
4. **依赖注入**：使用标准DI容器，不依赖Autofac
5. **静态方法**：遵循Skill清单-V1.md规范，所有Skill实现为静态方法
6. **反射验证**：保存Skill时必须先验证反射信息
7. **唯一性校验**：classPath + methodName必须唯一

---

## 八、验收标准

### 8.1 功能验收

- [ ] 可以创建、编辑、删除Skill
- [ ] 可以验证反射信息
- [ ] 可以管理Skill分类
- [ ] 工作流设计器可以加载Skill节点面板
- [ ] 可以执行Skill并返回标准输出

### 8.2 技术验收

- [ ] 所有API接口正常工作
- [ ] 数据库表结构正确
- [ ] 依赖注入配置正确
- [ ] 反射执行正常
- [ ] 前端页面与后端API对接正常

---

**文档版本**：V1.0
**创建时间**：2026-09-15
**最后更新**：2026-09-15
