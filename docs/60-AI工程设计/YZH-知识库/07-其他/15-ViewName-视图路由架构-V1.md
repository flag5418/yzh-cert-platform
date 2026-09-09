# ViewName 视图路由架构 V1

> 状态：已实施（2026-09-05）
> 作者：AI 架构设计

## 一、架构概述

YZH Framework 引入 **双映射路由** 机制：表用于增删改，视图用于查询。

```
�───────────────────────────────────────────────�
│  ISOStandard Entity                         │
│  [Table("cert_iso_standard")]   ← 物理表     │
│  [ViewName("v_iso_standard")]  ← 查询视图   │
│                                             │
│  标准字段 (标准/表共有):                     │
│    StandardCode  → PascalCase 列            │
│    StandardName                             │
│    VersionYear                              │
│                                             │
│  视图字段 ([NotMapped]):                    │
│    CategoryName  ← 字典翻译，仅查询填充     │
│    StatusName    ← 字典翻译，仅查询填充     │
└────────────────────────�────────────────────�
                         │
            ┌────────────┼────────────┐
            │                         │
       GetPageData              SaveChanges
            │                         │
            ▼                         ▼
  ┌──────────────────┐    ┌──────────────────┐
  │ MySQL VIEW       │    │ MySQL TABLE      │
  │ v_iso_standard   │    │ cert_iso_standard│
  │ + CategoryName   │    │ (标准业务字段)    │
  │   (LEFT JOIN)    │    │                  │
  └──────────────────�    └──────────────────┘
```

## 二、三层问题解答

### Q1：Property 名 → Column 名？

**默认约定（EF Core）：** Property 名 == Column 名（PascalCase=PascalCase，MySQL ci 不区分大小写）

**EntityBase（基类）：** 无 `[Column]` 注解 → 默认映射 `CreateID` → `CreateID` 列 ✅

**cert_* 表（物理列 PascalCase）：** 实体中也应无 `[Column]`（已修复）

**wf_*/rpt_*/audit_* 表（物理列 snake_case）：** 子类用 `new + [Column("xxx")]` 显式覆盖 ✅

### Q2：后端 JSON 格式？

**新默认（System.Text.Json）：** camelCase
- `StandardCode` → `"standardCode"` 
- `CategoryName` → `"categoryName"`

**Vol 兼容模式（JsonNormal）：** PascalCase（保留原样）
- 前端 `normalizeKeys()` 已支持双向转换

**决策：** certplatform-web 使用 camelCase（标准做法）

### Q3：[NotMapped] 视图字段的含义？

```csharp
[NotMapped]   // ← 仅标记此属性不参与 EF 数据库操作
public string CategoryName { get; set; }
```

**EF Core 行为：**
- 查询 `SELECT * FROM v_iso_standard` 时 → EF 按名称匹配填充 `[NotMapped]` 属性 ✅
- `SaveChanges()` 时 → `[NotMapped]` 字段自动忽略，不生成 INSERT/UPDATE ✅
- 视图中的额外列（CategoryName）可安全填充，不会触发 "Unknown column" ✅

### Q4：单个实体同时映射表+视图？

**EF Core 限制：** 一个 CLR 类型只能映射一个表或视图

**YZH 解决方案：** 
- `DbSet<ISOStandard>` 映射物理表（用于 CRUD）
- 查询时通过 `FromSqlInterpolated($"SELECT * FROM {viewName}")` 路由到视图
- `[NotMapped]` 字段从视图结果填充，但 SaveChanges 忽略它们

## 三、Attribute 体系

### EntityAttribute（已有，扩展）

```csharp
[Entity(
    TableName = "cert_iso_standard",    // 物理表名
    ViewName = "v_iso_standard",        // 查询视图名（新增）
    DBServer = "VOLContext"
)]
[Table("cert_iso_standard")]
public class ISOStandard : EntityBase { ... }
```

### ViewNameAttribute（独立标记）

```csharp
[ViewName("v_iso_standard")]
public class ISOStandard : EntityBase { ... }
```

## 四、运行时流程

### 查询（GetPageData）

```csharp
// Partial Service
public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
{
    // 自动检测 ViewName → 走视图
    var query = db.Set<ISOStandard>().UseViewIfExists();
    // 等效 SQL:
    // SELECT * FROM (SELECT * FROM v_iso_standard) AS x 
    // WHERE x.Enable = 1 
    // ORDER BY x.StandardCode 
    // LIMIT 20 OFFSET 0
    
    var total = query.Count();
    var list = query.Skip((page-1)*rows).Take(rows).ToList();
    // 每条记录的 CategoryName 自动从视图 LEFT JOIN 填充
}
```

### 增删改（Add/Update/Delete）

```csharp
// 标准 Vol 基类调用 → DbSet 映射到 TABLE
public override WebResponseContent Add(SaveModel saveDataModel)
{
    repository.Add(mainEntity);    // INSERT INTO cert_iso_standard
    repository.SaveChanges();       // [NotMapped] 字段自动忽略
}
```

### 字段行为矩阵

| 操作 | 表字段 (Id, StandardCode...) | 视图字段 ([NotMapped] CategoryName) |
|------|------|------|
| SELECT from VIEW | ✅ 填充 | ✅ 填充（按列名匹配） |
| INSERT into TABLE | ✅ 写入 | ❌ 忽略（EF 不追踪 [NotMapped]） |
| UPDATE TABLE | ✅ 写入 | ❌ 忽略 |
| WHERE/ORDER BY | ✅ 支持 | ✅ 支持（视图列可过滤排序） |

## 五、目录结构

```
vol.api/
├── YZH.Entity/
│   └── Admin/Platform/Cert/
│       ├── ISOStandard.cs           ← 修复：移除 [Column]、加 [NotMapped] 视图字段
│       ├── ISOClause.cs             ← 修复：移除错误 [Column]
│       └── CertificationBody.cs     ← 修复：移除错误 [Column]
│
├── YZH.Core/
│   ├── EntityAttribute              ← 扩展：新增 ViewName 属性
│   └── Extensions/
│       └── ViewNameExtensions.cs    ← 新增：UseViewIfExists/GetViewName
│
└── Cert.Platform/Services/Admin/Platform/Partial/
    └── ISOStandardService.cs        ← 更新：GetPageData 使用 UseViewIfExists()

DB/mysql/
└── cert_iso_standard_view.sql       ← MySQL VIEW 定义（已存在）
```

## 六、实施清单

| 步骤 | 文件 | 变更 | 状态 |
|------|------|------|------|
| 1 | EntityAttribute.cs | + ViewName 属性 | ✅ |
| 2 | ISOStandard.cs | - [Column]、+ [NotMapped] 视图字段、+ ViewName | ✅ |
| 3 | ISOClause.cs | - 错误 [Column] | ✅ |
| 4 | CertificationBody.cs | - 错误 [Column] | ✅ |
| 5 | VOLContext.cs | + 视图路由说明注释 | ✅ |
| 6 | ViewNameExtensions.cs | 新增 UseViewIfExists / GetViewName | ✅ |
| 7 | Partial ISOStandardService.cs | + UseViewIfExists 路由 | ✅ |
| 8 | cert_iso_standard_view.sql | MySQL VIEW（已存在，确认列别名 PascalCase） | ✅ |

## 七、后续待处理

- [ ] 其他 cert_* 表实体（AuditorProfile、ValidationRule 等）
- [ ] 对应 MySQL VIEW 创建脚本
- [ ] FileRequirement、DirectoryTemplate 等 PascalCase 表修复
- [ ] Vol Controller 自动生成器适配（Partial/ISOStandardController 加 ViewName 路由）
- [ ] 批量删除/导入时确认视图操作不影响（视图只读）
