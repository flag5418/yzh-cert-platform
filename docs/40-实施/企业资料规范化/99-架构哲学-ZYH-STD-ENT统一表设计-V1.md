# YZH-STD-ENT 统一表架构哲学

> **版本**：V1 | **日期**：2026-09-27 | **状态**：架构决策记录
> **相关文档**：`14-需求深化讨论收敛与增补设计-V1.md` §2、`README.md` D11

---

## 核心原则

> **同一套表结构，通过 `EnterpriseCode` 列区分"标准侧模板"与"企业侧实际数据"，避免双轨设计带来的代码冗余与维护负担。**

```text
┌─────────────────────────────────────────────────────────────┐
│                    YZH-STD-ENT 架构模式                       │
│                                                             │
│  cert_standard_directory_file                              │
│  ├── EnterpriseCode = 'YZH-STD-ENT'                        │
│  │       → 标准侧：后台配置、提取规则、契约定义               │
│  │                                                          │
│  └── EnterpriseCode = 真实企业 Code                         │
│              → 企业侧：实际上传的文件、画像、匹配结果          │
│                                                             │
│  同一张表 = 同一套字段 = 同一套CRUD逻辑                       │
│  唯一区别：EnterpriseCode 列的值                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 为什么这样设计

### 问题：双轨设计的陷阱

如果为"标准侧"和"企业侧"分别建两套表（如 `cert_standard_directory_file` + `cert_enterprise_file`），会带来：

| 问题 | 说明 |
|------|------|
| **代码冗余** | 同样的 CRUD 逻辑要写两遍，维护成本翻倍 |
| **数据一致性** | 标准文件改名时，需要同步更新两处；企业文件迁移时，两边都要改 |
| **查询复杂** | 匹配时需要 JOIN 两张表，或建视图简化 |
| **扩展困难** | 新字段要加两次，新 API 要写两套 |

### 解决：统一表 + 角色标识符

用 `EnterpriseCode` 作为"角色标识符"：

- **标准侧行**：`EnterpriseCode = YzhVirtualEnterprise.Code`（`'YZH-STD-ENT'`）—— ⛔ **不再判 `NULL`**（见下方勘误）
- **企业侧行**：`EnterpriseCode = cert_enterprise.Code`（真实企业 Code；⛔ **不是** `EB-日期-随机` 编码）

这样：
- 同一张表存储所有数据
- 通过查询条件过滤出"标准侧"或"企业侧"
- CRUD 逻辑只有一套
- 扩展字段只需改一处

---

## 应用场景

### 1. 标准目录文件

```sql
-- 查询标准侧（模板）
SELECT * FROM cert_standard_directory_file
WHERE EnterpriseCode = 'YZH-STD-ENT'
  AND StandardCode = 'ISO9001'
  AND IsValid = 1;

-- 查询企业侧（用户上传）
SELECT * FROM cert_standard_directory_file
WHERE EnterpriseCode = '<真实企业 Code（GUID）>'
  AND StandardCode = 'ISO9001'
  AND IsValid = 1;
```

> **勘误（2026-09-30，R-02 / R-04）**：`EnterpriseCode` 已改为 **`NOT NULL DEFAULT ''`**（P13，2026-09-28），存量 `NULL` 已回填为 `'YZH-STD-ENT'`；**`IS NULL` 分支已死，不得再写**。企业侧值是 `cert_enterprise.Code`（GUID），**不是 `EB-日期-随机`**（该编码已在 2026-09-26 摒弃）。

### 2. 目录配置

```sql
-- 标准侧配置（机构级）
SELECT * FROM cert_standard_directory_config
WHERE EnterpriseCode = 'YZH-STD-ENT'
  AND OrgCode = '<机构 Code>';

-- 企业侧配置（企业级）
SELECT * FROM cert_standard_directory_config
WHERE EnterpriseCode = '<真实企业 Code>'
  AND StandardCode = 'ISO9001';
```

> config 的唯一键是**四列** `uk_enterprise_std_stage(OrgCode, EnterpriseCode, StandardCode, StageCode)`（P16）——因此机构维度必须参与查询，企业行与模板行才不互撞。

### 3. 提取规则与验证数据

工作流引擎的 `get_field` / `get_table` Skill 使用 `YZH-STD-ENT` 作为测试数据锚点：

```csharp
// 工作流执行时，注入 enterprise_code='YZH-STD-ENT' 获取标准验证数据
var source = enterpriseCode == YZH_STANDARD_ENTERPRISE_CODE 
    ? "sample_data" 
    : "enterprise_doc";
```

---

## 常量定义

```csharp
// CertPlatform.Shared/Constants/YzhVirtualEnterprise.cs（★ 已存在，勿新建）
public static class YzhVirtualEnterprise
{
    public const string Code = "YZH-STD-ENT";
}
```

前端对应常量：`cert/cert-share/src/constants/virtualEnterprise.ts` → `YZH_VIRTUAL_ENTERPRISE_CODE`。

**⛔ 禁止**在各 Service 中硬编码 `"YZH-STD-ENT"` 字面量（种子 SQL 除外），统一引用该常量。

> **勘误（2026-09-30，R-04）**：原稿写的 `CertPlatformConstants.YZH_STANDARD_ENTERPRISE_CODE` **在代码中不存在**（仅出现于文档）；**权威常量是上表 `YzhVirtualEnterprise.Code`**（`YzhVirtualEnterprise.cs:11` 已明令禁字面量）。

---

## 查询模式

### 标准侧查询（模板/配置）

```csharp
// 唯一合法写法：只判等于常量
.Where(x => x.EnterpriseCode == YzhVirtualEnterprise.Code)
```

> **勘误（2026-09-30，R-14）**：原稿给了「`EnterpriseCode == null || …` 双分支」与「`IsStandardSide()` 扩展方法（推荐）」—— 两者均已作废：① **`IS NULL` 分支已死**（列已改 `NOT NULL`）；② `IsStandardSide()` **代码中不存在**，仅是设想。
> **纪律**：禁止手写 `EnterpriseCode == null`；禁止手写 `"YZH-STD-ENT"` 字面量。历史教训（06 册 B1）：按 `='YZH-STD-ENT'` 查而存量全为 `NULL`，**模板查询恒空**、功能全阻。

### 企业侧查询（实际数据）

```csharp
// 唯一合法写法：只判等于具体企业 Code
.Where(x => x.EnterpriseCode == enterpriseCode)
```

### 全量查询（不区分）

```csharp
// 仅过滤 IsValid —— 仅限后台维护页/报表；业务查询必须显式限定模板行或企业行
.Where(x => x.IsValid == 1)
```

> ⛔ **业务代码禁止省略 `EnterpriseCode` 过滤** —— 否则标准模板行与企业行混在一起（模板行数有百量级，会污染候选、统计与分页）。

---

## 与既有系统的一致性

YZH-STD-ENT 模式已在以下模块应用：

| 模块 | 应用方式 | 参考文件 |
|------|---------|---------|
| 工作流引擎 | `get_field`/`get_table` Skill 使用 `YZH-STD-ENT` 作为测试数据源 | `NodeExecutor.cs:317-378` |
| NC 检查规则 | 规则定义存储在 `YZH-STD-ENT` 下 | 既有代码 |
| 报告模板 | 模板定义存储在 `YZH-STD-ENT` 下 | 既有代码 |
| 提取规则 | 规则测试数据存储在 `YZH-STD-ENT` 下 | `DocExtractionRuleService.cs:272`（⚠️ 原稿记为 `:35`，该行无常量定义；常量唯一权威 = `CertPlatform.Shared/Constants/YzhVirtualEnterprise.cs:11`） |

本模块（企业资料规范化）延续此模式，确保架构一致性。

---

## 实施注意事项

### ✅ 正确做法

1. **新建企业文件行时**：设置 `EnterpriseCode = 真实企业Code`（`cert_enterprise.Code`）
2. **新建标准模板行时**：设置 `EnterpriseCode = YzhVirtualEnterprise.Code`（⛔ **不写 `null`**）
3. **查询时**：明确指定过滤条件，不要省略 `EnterpriseCode` 过滤
4. **常量引用**：使用 `YzhVirtualEnterprise.Code`（`CertPlatform.Shared/Constants/YzhVirtualEnterprise.cs`）—— ~~`CertPlatformConstants.YZH_STANDARD_ENTERPRISE_CODE`~~（该类型不存在）
5. **写模板行时**：拒绝企业侧上下文的写入（Service 层硬校验 + 模板发布独立权限点 + 乐观锁），见 `15` 号 `T-06`

### ❌ 错误做法

1. 硬编码 `"YZH-STD-ENT"` 字符串
2. 忘记过滤 `EnterpriseCode`，导致标准数据与企业数据混合
3. 为企业文件分配 `EnterpriseCode = 'YZH-STD-ENT'`
4. 为标准模板分配真实企业 Code
5. 写 `EnterpriseCode == null` 或 `IS NULL` 判断（列已 `NOT NULL`）
6. 用企业侧上下文写入模板行（会污染**全部企业**）

---

## 历史背景

- **2026-08-15**：首次引入 `YZH-STD-ENT` 作为工作流测试数据的锚点（见 `YZH特殊企业-工作流验证数据设计-V1.md`）
- **2026-09-25**：裁决删除 `ent_*` 表，要求新表统一使用 `cert_` 前缀
- **2026-09-27**：13 号分册实施 `YZH-STD-ENT` 统一表架构；14 号文档经 8 轮讨论后确认此为故意设计
- **2026-09-28**：P13 裁定 `EnterpriseCode` **禁 NULL**（改 `NOT NULL DEFAULT ''`，存量回填常量）；P16 裁定 config 唯一键为**四列**（含 `OrgCode`）——见 `scripts/db/20260928_enterprise_file_uk4_V1.sql`
- **2026-09-30**：本册勘误（常量名 `YzhVirtualEnterprise.Code`、企业侧值 = `cert_enterprise.Code`、查询谓词去 `IS NULL` 分支），见 `15-全册评估报告-V1.md` §三 R-02 / R-04 / R-14

---

## 相关裁决

- **README D11**：YZH-STD-ENT 统一表架构（已拍板）
- **14号 §2**：路线裁决修正（维持 A 路线）
