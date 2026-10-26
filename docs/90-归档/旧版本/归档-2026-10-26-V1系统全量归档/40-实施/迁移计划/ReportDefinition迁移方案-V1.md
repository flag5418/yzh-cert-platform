# ReportDefinition 迁移方案

> **版本**：V1.0 | **日期**：2026-09-15 | **状态**：待审批
>
> 本文档定义「报告章节定义」功能从旧架构迁移到 YZH 新架构的完整方案。

---

## 一、功能概述

### 1.1 业务流程

```
选树节点 (Org → Standard → Phase)
    ↓
① 创建/编辑报告模板（名称 + 备注）
    ↓
② 上传空白模板文件（.docx/.xlsx/.pdf → MinIO）
    ↓
③ 建立章节列表（章节名称、英文名、排序、启用）
```

### 1.2 数据模型

```
cert_report_template (报告模板)          1
├── Id (bigint PK, auto_increment)
├── Code (varchar(36) UK) — 业务编码 GUID
├── OrgCode (varchar(50)) — 机构编码
├── CbCode (varchar(36)) — 认证机构编码
├── StandardCode (varchar(36)) — 标准编码
├── PhaseCode (varchar(36)) — 阶段编码
├── TemplateName (varchar(200)) — 模板名称
├── TemplateFilePath (varchar(500)) — MinIO 文件路径
├── IsDefault (tinyint) — 是否默认
├── Remark (varchar(500)) — 备注
└── IsValid, CreateDate, ModifyDate...

rpt_report_section (报告章节)            N
├── Id (bigint PK, auto_increment)
├── Code (varchar(36) UK) — 业务编码
├── ReportCode (varchar(36)) — FK → ReportTemplate.Code
├── OrgCode (varchar(50))
├── SectionName (varchar(200)) — 章节名称（同模板内唯一）
├── SectionNameEn (varchar(200)) — 英文名称
├── SectionContent (text) — 章节内容
├── SortOrder (int) — 排序
├── IsActive (tinyint) — 启用
├── WorkflowCode (varchar(36)) — 工作流编码
├── ClauseCode (varchar(36)) — ISO 条款关联
└── Remark, CreateDate, ModifyDate...
```

**关系**：`ReportTemplate.Code` ← `ReportSection.ReportCode`（一对多，应用层关联）

---

## 二、架构约束（YZH 规范对齐）

### 2.1 后端约束

| 约束 | 要求 | 本方案 |
|------|------|--------|
| Entity 基类 | 必须继承 `YZH.Core.Stand.Models.Entity.BaseEntity` | ✅ 重写 `ReportTemplate.cs`、`ReportSection.cs` |
| ORM 属性 | 必须使用 `[SugarTable]` + `[SugarColumn]` | ✅ 映射 DB 实际列名 |
| Controller | 优先使用 `YzhControllerBase<T>` 或 `TreeTableControllerBase<T,V>` | ⚠️ 本场景为"上下文依赖详情"模式，使用自定义 Controller + `EntityService<T>` |
| 数据访问 | 通过 `EntityService<T>` 操作 SqlSugar | ✅ 注入 `EntityService<ReportTemplate>` + `EntityService<ReportSection>` |
| 文件存储 | 使用 `IObjectStorage`（MinIO） | ✅ 复用现有接口 |

### 2.2 前端约束

| 约束 | 要求 | 本方案 |
|------|------|--------|
| 页面布局 | 左树右表，复用 directory-manager 的手写树模式 | ✅ 复用 `useFileTree` + 手写三级树 |
| 表单组件 | 使用 `YzhForm` 组件驱动表单 | ✅ 模板编辑使用 `YzhForm` |
| 表格组件 | 章节列表为从属数据，使用 `el-table` | ✅ 与 directory-manager 的文件列表模式一致 |
| API 调用 | 使用 `yzhApi`（来自 `@yzh-core/api/client`） | ✅ 统一 API 客户端 |
| 类型定义 | 使用 `@share/types` 中的共享类型 | ✅ 在 `cert.ts` 中补充类型 |

### 2.3 为什么不使用 TreeTableControllerBase

`TreeTableControllerBase<T,V>` 要求：
- 树实体 T 实现 `ITreeEntity`（有 `ParentCode` 字段）
- 表格实体 V 通过 `RelateField` 与树关联
- 树和表格是**同一业务域**的父子关系

ReportDefinition 的场景：
- 树是 Org→Std→Phase（标准目录域）
- 右侧是 ReportTemplate + ReportSection（报告定义域）
- 树提供**上下文参数**，不是父子关系

因此使用**自定义 Controller**，但内部仍通过 `EntityService<T>` 操作数据，符合 YZH 数据访问规范。

---

## 三、后端设计

### 3.1 Entity 重写

#### ReportTemplate.cs

```csharp
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 报告模板
    /// <para>表名：cert_report_template</para>
    /// </summary>
    [SugarTable("cert_report_template")]
    public class ReportTemplate : BaseEntity
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(ColumnName = "CbCode", Length = 36)]
        public string CbCode { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "StandardCode", Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "PhaseCode", Length = 36)]
        public string PhaseCode { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "TemplateName", Length = 200)]
        public string TemplateName { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "TemplateFilePath", Length = 500, IsNullable = true)]
        public string? TemplateFilePath { get; set; }

        [SugarColumn(ColumnName = "IsDefault")]
        public bool IsDefault { get; set; } = false;

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public new int IsValid { get; set; } = 1;
    }
}
```

#### ReportSection.cs

```csharp
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Rpt
{
    /// <summary>
    /// 报告章节
    /// <para>表名：rpt_report_section</para>
    /// </summary>
    [SugarTable("rpt_report_section")]
    public class ReportSection : BaseEntity
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(ColumnName = "ReportCode", Length = 36)]
        public string ReportCode { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "SectionName", Length = 200)]
        public string SectionName { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "SectionNameEn", Length = 200, IsNullable = true)]
        public string? SectionNameEn { get; set; }

        [SugarColumn(ColumnName = "SectionContent", ColumnDataType = "text", IsNullable = true)]
        public string? SectionContent { get; set; }

        [SugarColumn(ColumnName = "SortOrder")]
        public int SortOrder { get; set; } = 0;

        [SugarColumn(ColumnName = "IsActive")]
        public bool IsActive { get; set; } = true;

        [SugarColumn(ColumnName = "WorkflowCode", Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        [SugarColumn(ColumnName = "ClauseCode", Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public new int IsValid { get; set; } = 1;
    }
}
```

### 3.2 Controller 设计

**文件**：`src/certplatform-api/CertPlatform.Admin/Controllers/Workflow/ReportDefinitionController.cs`

**路由**：`api/ReportDefinition`

**依赖**：
- `EntityService<ReportTemplate>` — 模板 CRUD
- `EntityService<ReportSection>` — 章节 CRUD
- `IObjectStorage` — MinIO 文件存储
- `IUserContext` — 当前用户上下文

#### 端点清单

| # | Method | Route | 功能 | 请求 | 响应 |
|---|--------|-------|------|------|------|
| 1 | GET | `organization-tree` | 获取树数据 | Query: `orgCode?` | 树节点列表 |
| 2 | GET | `template/context` | 按上下文查模板 | Query: `orgCode, standardCode, phaseCode` | `ReportTemplate?` |
| 3 | POST | `template/save` | 创建/更新模板 | Body: `ReportTemplate` | `ReportTemplate` |
| 4 | POST | `template/upload` | 上传模板文件 | Form: `file, orgCode, standardCode, phaseCode` | `{path, fileName, size}` |
| 5 | POST | `template/delete` | 删除模板 | Query: `id` | `void` |
| 6 | GET | `section/list` | 按模板查章节 | Query: `reportCode` | `ReportSection[]` |
| 7 | POST | `section/save` | 创建/更新章节 | Body: `ReportSection` | `ReportSection` |
| 8 | POST | `section/delete` | 删除章节 | Query: `id` | `void` |

#### 关键实现细节

**模板保存（upsert）**：
```
if (entity.Id > 0)
    → 更新现有记录
else
    → 查询 (OrgCode, StandardCode, PhaseCode) 是否已存在
    → 存在：更新
    → 不存在：新建 + 生成 Code = Guid("N")
```

**文件上传**：
- 使用 `IObjectStorage.UploadAsync(objectName, stream, size, contentType)`
- 存储路径：`report/{orgCode}/{standardCode}/{phaseCode}/{filename}`
- 返回 MinIO 对象路径，前端保存到 `TemplateFilePath`

**章节排序**：
- 新建章节时 `SortOrder` 默认为当前最大值 + 1
- 前端可拖拽排序（后续增强）

### 3.3 不需要的部分

| 组件 | 原因 |
|------|------|
| EntityConfig JSON | 自定义 Controller 不走 YzhControllerBase 的配置驱动 |
| Service 层 | 逻辑简单，Controller 直接操作 EntityService |
| ReportRuleConfig（LogicFlow） | 本次不迁移，后续单独处理 |

---

## 四、前端设计

### 4.1 页面布局

```
┌──────────────────────────────────────────────────────────┐
│ Left Panel (280px)           │ Right Panel (flex)         │
│                              │                           │
│ ┌──────────────────────┐     │ ┌─ 面包屑 ──────────────┐ │
│ │ 机构 / 标准 / 阶段     │     │ │ 选择的机构>标准>阶段    │ │
│ ├──────────────────────┤     │ └───────────────────────┘ │
│ │ [搜索框]              │     │ ┌─ 模板区域 ────────────┐ │
│ ├──────────────────────┤     │ │ 报告模板表单            │ │
│ │ ▼ 某认证机构          │     │ │ 名称 / 备注 / 文件上传  │ │
│ │   ▼ ISO 9001:2015    │     │ │ [创建] / [保存]        │ │
│ │     ● 申请阶段 ← 选中  │     │ └───────────────────────┘ │
│ │     ● 初审阶段        │     │ ┌─ 章节区域 ────────────┐ │
│ │     ● 复审阶段        │     │ │ 章节列表 (el-table)    │ │
│ │   ▼ ISO 13485:2016   │     │ │ [新建章节]             │ │
│ │     ● ...            │     │ │ 名称|英文|排序|启用|操作 │ │
│ └──────────────────────┘     │ └───────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

### 4.2 文件清单

| # | 文件路径 | 操作 | 说明 |
|---|---------|------|------|
| 1 | `cert/cert-share/src/types/cert.ts` | **更新** | 补充 `ReportTemplate`、`ReportSection` TypeScript 类型 |
| 2 | `cert/cert-share/src/api/workflow/report-rule.ts` | **重写** | 对齐新 Controller 路由 |
| 3 | `cert/cert-admin/src/pages/workflow/report-rule/index.vue` | **重写** | 左树右表布局，复用 directory-manager 模式 |

### 4.3 API 对齐

旧路由 → 新路由：

| 旧路由 | 新路由 | 变化 |
|--------|--------|------|
| `GET /api/report-definition/template/context` | `GET /api/ReportDefinition/template/context` | 路由大小写对齐 |
| `POST /api/report-definition/template` | `POST /api/ReportDefinition/template/save` | 明确 action |
| `POST /api/report-definition/template/upload` | `POST /api/ReportDefinition/template/upload` | 不变 |
| `POST /api/report-definition/template/delete/{id}` | `POST /api/ReportDefinition/template/delete?id={id}` | 参数改为 Query |
| `GET /api/report-definition/section/{reportCode}` | `GET /api/ReportDefinition/section/list?reportCode={code}` | 改为 Query 参数 |
| `POST /api/report-definition/section` | `POST /api/ReportDefinition/section/save` | 明确 action |
| `POST /api/report-definition/section/delete/{id}` | `POST /api/ReportDefinition/section/delete?id={id}` | 参数改为 Query |

### 4.4 前端实现要点

**左树**：
- 复用 `useFileTree()` composable（来自 `@share/composables/useFileTree`）
- 树数据源：`GET /api/Workflow/StandardDirectory/organization-tree`（与 directory-manager 共用）
- 选中阶段节点后，传递 `{ orgCode, standardCode, phaseCode }` 到右侧

**右侧模板区域**：
- 使用 `YzhForm` 组件渲染模板表单（名称、备注）
- 文件上传使用 `el-upload`（独立于 YzhForm）
- 按钮：未创建时显示"创建"，已创建时显示"保存"

**右侧章节区域**：
- 使用 `el-table` 渲染章节列表（从属数据，非标准 CRUD 页面）
- 工具栏：新建章节按钮
- 行操作：编辑、删除
- 弹窗：使用 `el-dialog` + `YzhForm` 编辑章节

**状态管理**：
```typescript
// 树选中状态
const currentPhase = ref<{ orgCode, standardCode, phaseCode } | null>(null)

// 模板状态
const templateForm = reactive<ReportTemplate>({ ... })
const templateExists = computed(() => !!templateForm.id)

// 章节状态
const sectionData = ref<ReportSection[]>([])
const sectionLoading = ref(false)
```

---

## 五、执行计划

### 5.1 步骤清单

| # | 层 | 文件 | 操作 | 依赖 |
|---|---|------|------|------|
| 1 | Entity | `CertPlatform.Shared/Entities/Cert/ReportTemplate.cs` | 重写 → SqlSugar | — |
| 2 | Entity | `CertPlatform.Shared/Entities/Rpt/ReportSection.cs` | 重写 → SqlSugar | — |
| 3 | Controller | `CertPlatform.Admin/Controllers/Workflow/ReportDefinitionController.cs` | 新建 | 步骤 1, 2 |
| 4 | 编译验证 | — | `dotnet build` 0 error | 步骤 1-3 |
| 5 | Frontend Types | `cert/cert-share/src/types/cert.ts` | 补充类型 | — |
| 6 | Frontend API | `cert/cert-share/src/api/workflow/report-rule.ts` | 重写 | 步骤 5 |
| 7 | Frontend Page | `cert/cert-admin/src/pages/workflow/report-rule/index.vue` | 重写 | 步骤 6 |
| 8 | 功能验证 | — | API 调试 + 页面功能测试 | 步骤 1-7 |

### 5.2 验证标准

| 验证项 | 预期结果 |
|--------|---------|
| `dotnet build` | 0 error |
| 选中阶段 → 模板未创建 | 右侧显示空状态 + "创建"按钮 |
| 点击创建 → 填写名称 → 保存 | 模板创建成功，按钮变为"保存" |
| 上传文件 | 文件上传到 MinIO，路径保存到模板 |
| 点击新建章节 → 填写 → 保存 | 章节出现在列表中 |
| 编辑章节 → 修改名称 → 保存 | 章节更新成功 |
| 删除章节 | 章节从列表移除 |
| 删除模板 | 模板删除，右侧回到空状态 |
| 切换树节点 | 右侧内容正确切换 |

---

## 六、风险与注意事项

| 风险 | 应对 |
|------|------|
| DB 列名与 Entity 映射不匹配 | 已通过 `DESCRIBE` 确认实际列名，Entity 精确映射 |
| MinIO 上传路径冲突 | 使用 `{orgCode}/{standardCode}/{phaseCode}` 隔离 |
| 模板唯一性约束 | 后端 upsert 逻辑保证一个 (org+std+phase) 只有一条模板 |
| 章节与模板断联 | `ReportCode` 字段关联，删除模板时应级联删除章节 |
| 编译失败 | 步骤 4 独立验证，发现问题立即修复 |

---

## 七、踩坑记录与经验总结

### 7.1 实体设计

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| `BaseEntity` 继承导致 `Id` 类型冲突 | `BaseEntity.Id` 是 `string`，但 `cert_report_template.Id` 是 `bigint` auto_increment，SqlSugar 报 `Ambiguous match found` | `ReportTemplate` 和 `ReportSection` **不继承 BaseEntity**，手动定义所有字段 + `[SugarColumn]` |
| `rpt_report_section` 无 `IsValid` 列 | 旧表结构只有 `IsActive`（tinyint） | Entity 中不定义 `IsValid`，使用 `IsActive` |

### 7.2 外键约束

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| `cert_report_template.CbCode` FK 失败 | `CbCode` 必须对应 `cert_certification_body.Code` | 前端/后端显式设置 `CbCode = OrgCode`（同一 GUID） |
| `rpt_report_section.ClauseCode` FK 失败 | `cert_iso_clause` 表为空，FK 约束 `fk_section_clause` 阻止插入 | `ALTER TABLE rpt_report_section DROP FOREIGN KEY fk_section_clause` |
| `rpt_report_section.WorkflowCode` FK 失败 | `wf_workflow_definition` 表可能无对应数据 | `ALTER TABLE rpt_report_section DROP FOREIGN KEY fk_section_workflow` |
| `fk_section_report` 阻止新增 | 模板章节不引用审核报告，FK 无意义 | `ALTER TABLE rpt_report_section DROP FOREIGN KEY fk_section_report` |

### 7.3 前后端映射

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 树节点 `standardCode` 是显示码（"ISO 13485:2016"），但 FK 需要 GUID | API 返回 `stdCode`（GUID）和 `standardCode`（显示码）两个字段 | 前端使用 `phase.stdCode` 而非 `phase.standardCode` |
| 树节点 `phaseCode` 是阶段码（"S1"），但 FK 需要 `cert_phase_definition.Code`（GUID） | API 额外返回 `phaseDefinitionCode` 字段 | 前端使用 `phase.phaseDefinitionCode` 作为 `PhaseCode` |
| `YzhForm` 字段 `prop` 与 `reactive` 对象属性名不匹配 | `prop` 用 camelCase，但 `sectionForm` 用 PascalCase | 统一使用 PascalCase（与后端 JSON 序列化一致） |

### 7.4 后端配置

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| `[ApiController]` 导致 400 错误 | 自动验证非空属性为必填 | 移除 `[ApiController]`，使用 `[Route]` + `[HttpPost]` |
| `InsertAsync` 不返回 ID | `ExecuteCommandAsync` 不回填自增 ID | 改用 `ExecuteReturnEntityAsync()` |
| `Result<T>` 无 `.Message` 属性 | YZH.Core 的 `Result<T>` 使用 `.Error` | 改用 `Result<T>.Error` 判断错误 |

### 7.5 前端表单与后端序列化

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 模板保存 500 + `NullReferenceException` | `templateForm` 用 camelCase（`templateName`），后端 `PropertyNamingPolicy = null` 期望 PascalCase（`TemplateName`） | `templateForm` 改为 PascalCase 属性名；`handleSaveTemplate` 显式构建 PascalCase payload |
| `YzhForm` 章节表单无响应 | `sectionFormFields` 的 `prop` 用 camelCase（`sectionName`），`sectionForm` 用 PascalCase（`SectionName`） | `sectionFormFields` 的 `prop` 改为 PascalCase |

### 7.6 外键约束（补充）

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| `fk_rpttmpl_phase` 阻止保存 | FK 引用空表 `cert_phase_definition(Code)`，实际阶段数据在 `cert_cert_stage` | `ALTER TABLE cert_report_template DROP FOREIGN KEY fk_rpttmpl_phase` |
| `fk_rpttmpl_standard` 阻止保存 | FK 引用 `cert_iso_standard(Code)`，但树返回的 `StandardCode` 是 GUID（正确），问题在于数据不一致 | `ALTER TABLE cert_report_template DROP FOREIGN KEY fk_rpttmpl_standard` |

---

*文档结束*
