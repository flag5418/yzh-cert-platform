# YZHBaseEntity 移除 OrgCode 字段

> **日期**：2026-08-15  
> **类型**：架构修正  
> **影响范围**：48 个实体类 + 46 张数据库表

## 问题现象

通过框架默认 `ServiceBase.Add` 新增认证机构、ISO 标准等记录时报错：
```
Column 'org_code' cannot be null
```

## 根本原因

1. `YZHBaseEntity` 基类定义了 `OrgCode` 属性，注释说"由 [YZHMultiTenant] 特性自动填充"
2. `[YZHMultiTenant]` 特性**尚未实现**，框架的 `ServiceBase.Add` 不会自动填充 `OrgCode`
3. `UserContext` / `UserInfo` 没有 `OrgCode` 属性
4. 6 张表的 `org_code` 列是 `NOT NULL`，导致报错

## 设计缺陷分析

系统分三种角色：
- **管理员/维护人员**：管理所有认证机构，不需要机构隔离
- **审核员**：只管理自己审核的企业，需要机构隔离
- **企业**：属于特定机构

`YZHBaseEntity` 作为公共基类包含 `OrgCode` 是错误的设计——全局基础资料表（如认证阶段定义、ISO 条款等）根本不需要机构编码。

## 修复方案

### 1. 从 YZHBaseEntity 移除 OrgCode

- 删除 `OrgCode` 属性定义（第 53-58 行）
- 删除 `FillCreateInfo` 方法的 `orgCode` 参数

### 2. 数据库表分类处理

**删除 org_code 列的表（19 张）**：
- 17 张全局基础资料表（管理员维护）
- `cert_certification_body`（自己就是机构，org_code 冗余）
- `cert_iso_standard`（ISO 标准是全局的）

**保留 org_code 列的表（29 张）**：
- 机构级数据表（企业、审核任务、审核报告等）
- 需要在子类实体中显式声明 `OrgCode` 属性

### 3. 实体类修改

需要 `OrgCode` 的子类显式声明：
```csharp
[StringLength(50)]
[Column("org_code")]
public string OrgCode { get; set; }
```

已有声明但需去掉 `new` 关键字的实体：
- `AIConfig.cs` - 去掉 `new`（基类已无此属性）
- `PromptTemplate.cs` - 去掉 `new`

### 4. Service 层修改

调用 `FillCreateInfo` 时不再传 `orgCode` 参数：
- `EnterpriseService.cs` - `entity.FillCreateInfo(userId, userName)` + 手动设 `entity.OrgCode`
- `EnterpriseFileService.cs` - `file.OrgCode = enterprise.OrgCode` + `file.FillCreateInfo(userId, userName)`
- `AuditorService.cs` - `profile.OrgCode = orgCode` 已在实体构造时设置 + `profile.FillCreateInfo(userId, userName)`

### 5. DirectoryTemplateService.cs 特殊处理

`StandardPhaseConfig` 和 `FileRequirement` 是全局表，不再有 `OrgCode`。
标准目录模板 OSS 路径中使用 `"GLOBAL"` 作为占位符。

## 验证

- 编译成功：0 个错误
- 后端启动正常：`Now listening on: http://[::]:9992`
- `cert_certification_body` 表已无 `org_code` 列
- 剩余 29 张机构级数据表仍保留 `org_code` 列

## 后续影响：StandardDirectoryService.cs 修复

### 问题现象（2026-08-15）

标准目录页面 (`/CertPlatform/Standard/DirectoryConfig`) 只加载了"机构"和"标准"，没有加载"阶段"，导致无法进行文件上传。

### 根因分析

`StandardDirectoryService.cs` 中的 `GetOrganizationTree` 方法仍在查询已被删除的 `org_code` 列：

1. **第 133-136 行**：从 `cert_org_stage` 表查询时过滤 `x.OrgCode`
2. **第 156-158 行**：从 `cert_org_standard` 表查询时过滤 `x.OrgCode`

这两张表的 `org_code` 列已在之前的改造中被删除，导致 EF Core 查询失败。

### 修复方案

移除对 `org_code` 字段的查询和过滤：

```csharp
// 修改前（错误）
var stages = await _db.Set<OrgStage>()
    .Where(x => x.OrgCode == orgCode && x.Enable)
    .ToListAsync();

// 修改后（正确）
var stages = await _db.Set<OrgStage>()
    .Where(x => x.Enable)
    .ToListAsync();
```

同样处理 `cert_org_standard` 表的查询。

### 影响范围

- 文件：`src/server/Vue.NetCore/vol.api/VOL.Builder/Services/CertPlatform/StandardDirectoryService.cs`
- 影响：前端标准目录页面可正常显示完整的机构→标准→阶段树形结构

## 后续影响（2026-08-15 补充）：CertDocExtractionRule 实体残留 org_code 映射

### 问题现象

文档提取规则页面查询时报错：
```
Unknown column 'c.org_code' in 'field list'
    at VOL.Builder.Services.CertPlatform.DocExtractionRuleService.GetRuleDetailAsync
```

### 根因

`CertDocExtractionRule` 实体在迁移后仍声明了 `[Column("org_code")] OrgCode` 属性（迁移脚本 `remove_orgcode_from_global_tables.sql` 已从 `cert_doc_extraction_rule` 表删除该列）。EF Core 物化实体时会把所有映射列加入 SELECT，导致 SQL 引用不存在的列。

> 注：迁移涉及的 19 张表中，其余 18 张对应的实体均已同步移除 OrgCode 属性，仅此一张遗漏。

### 修复方案

1. `VOL.Entity/CertPlatform/DocExtraction/CertDocExtractionRule.cs`：删除 `OrgCode` 属性（含 `[Column("org_code")]`）
2. `DocExtractionRuleService.cs`：移除 3 处引用（`SaveExtractionRuleAsync` 新建/更新、`GetRuleDetailAsync` 响应赋值）

### 影响范围

- 文件：`CertDocExtractionRule.cs`、`DocExtractionRuleService.cs`
- 影响：文档提取规则页面的保存/查询恢复正常

## SQL 脚本

`DB/mysql/remove_orgcode_from_global_tables.sql`

## 相关文档

- [删除确认弹窗重复问题修复](./2026-08-15_删除确认弹窗重复问题修复.md)
- [Phase 2 实施方案](../../../历史文档/归档-2026-08-15-执行文档清理/Phase2-实体snake_case映射与业务服务重建-V1.md)
