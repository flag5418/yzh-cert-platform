# 2026-08-12 YZHBaseEntity 审计字段映射全面分析与修复

> **问题**：`Unknown column 'w.CreateDate'` / `Unknown column 'c.org_code'` 反复出现，影响 PromptTemplate、ISOStandard、DirectoryConfig 等多个页面
> **根因**：YZHBaseEntity 的 `[Column]` 映射与数据库实际列名风格不匹配
> **状态**：✅ 已修复

---

## 📊 一、数据库审计字段全面分析

### 1.1 表命名风格统计

| 风格 | 数量 | 说明 |
|------|------|------|
| **PascalCase 列名** | ~40 张表 | Vol 框架原有风格，列名与 C# 属性名一致 |
| **snake_case 列名** | 7 张表 | 后期创建，列名用小写下划线 |

### 1.2 PascalCase 审计列表（40+ 张）

以下表的审计列使用 **PascalCase**（与 C# 属性名一致，**不需要** `[Column]` 映射）：

```
cert_iso_standard, cert_certification_body, cert_directory_template,
cert_enterprise, cert_extraction_field, cert_extraction_rule,
cert_file_requirement, cert_iso_clause, cert_phase_definition,
cert_report_template, cert_standard_directory_config,
cert_standard_directory_file, cert_standard_directory_folder,
cert_org_stage, cert_org_standard, cert_cert_stage,
audit_evidence, audit_finding, audit_task, audit_nonconformity,
audit_checklist_item, audit_rectification, audit_project,
ent_enterprise, ent_enterprise_document, ent_enterprise_file,
ent_extraction_result, ent_file_compliance_check,
ent_file_pre_check_result, ent_file_version, ent_table_extraction_result,
rpt_audit_report, rpt_report_section, rpt_report_section_source,
rpt_report_task, cert_validation_rule, cert_validation_rule_source,
cert_standard_phase_config, wf_field_label_mapping, wf_skill,
wf_workflow_definition, wf_workflow_execution_log
```

### 1.3 snake_case 审计列表（7 张）

以下表的审计列使用 **snake_case**（**必须**在实体中添加 `[Column]` 映射）：

| 表名 | 实体类 | 特殊说明 |
|------|--------|---------|
| `wf_prompt_template` | `PromptTemplate` | 完整 snake_case 审计列 |
| `cert_ai_config` | `AIConfig` | 完整 snake_case 审计列 |
| `cert_doc_extraction_rule` | `CertDocExtractionRule` | 有 `update_id/update_date` 替代 `modify_*` |
| `cert_doc_field_def` | `CertDocFieldDef` | 无 `org_code`/`status`/`enable`/`sort` |
| `cert_doc_table_def` | `CertDocTableDef` | 无 `org_code`/`status`/`enable`/`sort` |
| `cert_doc_table_field_def` | `CertDocTableFieldDef` | 无 `org_code`/`status`/`enable`/`sort` |
| `cert_message` | `CertMessage` | 继承 `BaseEntity`（非 YZHBaseEntity） |
| `cert_sys_config` | `SysConfig` | 继承 `BaseEntity`（非 YZHBaseEntity） |

---

## 🔍 二、根因分析

### 2.1 EF Core 的列名映射规则

```
EF Core 默认行为：
  C# 属性名 → SQL 列名（直接映射，不做任何转换）

  例：public string CreateDate  →  SELECT ... CreateDate ...
  例：public string Code       →  SELECT ... Code ...
```

### 2.2 历史问题链

```
问题 1（2026-08-07）：yzh_* 配置表 snake_case → 给 YzhPageConfig/YzhFieldConfig 加 [Column] ✅

问题 2（2026-08-11）：wf_prompt_template snake_case
  → 给 YZHBaseEntity 加 [Column("create_id")] 等映射
  → 破坏了 cert_iso_standard 等 PascalCase 表 ❌

问题 3（2026-08-12）：再次出现 Unknown column 'w.CreateDate'
  → 原因是 YZHBaseEntity 的 [Column] 映射对 PascalCase 表不兼容
```

### 2.3 核心矛盾

| 问题 | 原因 |
|------|------|
| YZHBaseEntity 有 `[Column("create_id")]` | 为了适配 snake_case 表 |
| 但 PascalCase 表的列名是 `CreateID` | `[Column("create_id")]` 让 EF 生成错误 SQL |
| 去掉 `[Column]` | snake_case 表又报错 |

---

## ✅ 三、最终解决方案

### 3.1 设计原则

> **YZHBaseEntity 不携带任何 `[Column]` 映射**（对齐 Vol 框架 BaseEntity 模式）
> 
> **例外**：snake_case 表对应的实体，在该实体类内部用 `new` + `[Column]` 覆盖

### 3.2 YZHBaseEntity 最终结构

```csharp
// ✅ 正确：无 [Column] 映射（与 Vol 框架对齐）
public abstract class YZHBaseEntity : BaseEntity
{
    [Key]
    [Column(TypeName = "bigint")]  // ← 唯一保留的 [Column]，仅指定类型
    public long Id { get; set; }

    [MaxLength(100)]
    public string Code { get; set; }          // → 映射到 Code 或 code（由 EF 根据表决定）

    [MaxLength(50)]
    public string OrgCode { get; set; }       // → 映射到 OrgCode 或 org_code

    [NotMapped]  // ← 审计字段标记为 NotMapped，由子类覆盖
    public int? CreateID { get; set; }
    // ... 其余审计字段同理
}
```

### 3.3 snake_case 实体的覆盖模式

```csharp
// ✅ 正确：在实体类内部用 new + [Column] 覆盖
[Table("wf_prompt_template")]
public class PromptTemplate : YZHBaseEntity
{
    // 审计字段覆盖
    [Column("create_id")] public new int? CreateID { get; set; }
    [Column("creator")]   [MaxLength(50)] public new string Creator { get; set; }
    [Column("create_date")] public new DateTime? CreateDate { get; set; }
    // ... modify/delete 字段同理

    // 通用字段覆盖
    [Column("code")]        public new string Code { get; set; }
    [Column("org_code")]    public new string OrgCode { get; set; }
    [Column("status")]      public new string Status { get; set; }
    [Column("enable")]      public new bool Enable { get; set; }
    [Column("sort")]        public new int Sort { get; set; }
    [Column("remark")]      public new string Remark { get; set; }

    // 业务字段
    [Column("prompt_code")] public string PromptCode { get; set; }
    // ...
}
```

### 3.4 需要 `[Column]` 覆盖的 6 个实体

| 实体 | 表 | 需要覆盖的字段 |
|------|-----|--------------|
| `PromptTemplate` | `wf_prompt_template` | 全部基类字段 |
| `AIConfig` | `cert_ai_config` | 全部基类字段 |
| `CertDocExtractionRule` | `cert_doc_extraction_rule` | 全部基类字段 + Enable/Sort 标 NotMapped |
| `CertDocFieldDef` | `cert_doc_field_def` | code/create_* 映射，其余 NotMapped |
| `CertDocTableDef` | `cert_doc_table_def` | code/create_* 映射，其余 NotMapped |
| `CertDocTableFieldDef` | `cert_doc_table_field_def` | code/create_* 映射，其余 NotMapped |

---

## 📝 四、文档修正

### 4.1 需要更新的文档

| 文档 | 问题 | 修正 |
|------|------|------|
| `数据库表设计-V2.md` | 审计字段定义为 snake_case | 改为：Cert 业务表用 PascalCase，yzh_* 配置表用 snake_case |
| `YZH-V3.0-架构设计文档.md` | 描述所有表用 snake_case | 修正为双轨制 |
| `08-Vol框架实战速查手册.md` | 列名映射策略描述不准确 | 补充双轨制说明 |
| `09-常见错误对照表.md` | 缺少本次问题的记录 | 新增条目 |

### 4.2 正确的审计字段约定

```
┌──────────────────────────────────────────────────────────────┐
│              审计字段命名双轨制（已确认）                        │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  【PascalCase 轨】Cert 业务表（40+ 张）                        │
│  ─────────────────────────────────                           │
│  • 列名：CreateID, Creator, CreateDate, ModifyID...           │
│  • 实体：YZHBaseEntity（无 [Column] 映射）                    │
│  • 子类：无需任何覆盖                                        │
│                                                              │
│  【snake_case 轨】配置/工作流表（7 张）                         │
│  ─────────────────────────────────                           │
│  • 列名：create_id, creator, create_date, modify_id...        │
│  • 实体：在子类中用 new + [Column("snake_case")] 覆盖         │
│  • 涉及表：wf_prompt_template, cert_ai_config,                │
│            cert_doc_*, cert_message, cert_sys_config          │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 🔧 五、预防措施

### 5.1 新表创建检查清单

```
□ 1. 确定表命名风格
   • 新 Cert 业务表 → PascalCase 列名（推荐）
   • 新 yzh_* 配置表 → snake_case 列名（已有约定）

□ 2. 创建数据库表
   • PascalCase 轨：CREATE TABLE xxx (CreateID int, ...)
   • snake_case 轨：CREATE TABLE xxx (create_id int, ...)

□ 3. 创建 C# 实体
   • PascalCase 轨：继承 YZHBaseEntity，无需额外映射
   • snake_case 轨：继承 YZHBaseEntity，添加 new + [Column] 覆盖

□ 4. 验证
   • docker exec yzh-mysql mysql -pYzh123456. yzh_cert_platform \
       -e "SHOW COLUMNS FROM 表名;" | grep -i create
   • 确认列名风格与实体映射一致
```

### 5.2 快速诊断命令

```bash
# 检查表的审计列名风格
docker exec yzh-mysql mysql -pYzh123456. yzh_cert_platform \
  -e "SELECT COLUMN_NAME FROM information_schema.COLUMNS 
      WHERE TABLE_SCHEMA='yzh_cert_platform' 
      AND TABLE_NAME='你的表名' 
      AND COLUMN_NAME REGEXP '^(create|modify|delete)[_A-Z]';"

# 输出 CreateID → PascalCase 轨（无需 [Column]）
# 输出 create_id → snake_case 轨（需要 [Column] 覆盖）
```

### 5.3 已知问题对照

| 错误信息 | 原因 | 解决方案 |
|---------|------|---------|
| `Unknown column 'w.CreateDate'` | PascalCase 表被 YZHBaseEntity 的 `[Column("create_date")]` 破坏 | 移除 YZHBaseEntity 上的 `[Column]`，在 snake_case 实体中单独覆盖 |
| `Unknown column 'c.org_code'` | PascalCase 表的列名是 `OrgCode` | 同上 |
| `Unknown column 'y.CheckboxSelection'` | snake_case 表缺少 `[Column]` 映射 | 在实体中添加 `[Column("checkbox_selection")]` |

---

## 📚 六、参考文档

- Vol 官方文档：http://v3.volcore.xyz/docs/cs/dev/db.html
- EF Core 列映射：https://learn.microsoft.com/en-us/ef/core/modeling/column-attribute
- 本项目基类设计：`docs/20-架构决策/CertPlatform基类架构设计-V1.0.md`

---

## 附录：2026-08-12 目录管理页两个UI问题修复

### 问题1：进入页面右侧显示"目录配置列表"

**现象**：进入 `/CertPlatform/Standard/DirectoryConfig` 时，右侧显示"目录配置列表"表格。

**期望**：显示提示"请选择左侧阶段，进行文件管理"。

**根因**：`ConfigTab.vue` 无阶段选中时也始终渲染配置管理表格。

**修复**：
- 新增 `hasActiveConfig` 计算属性，判断是否有启用中的配置
- 无配置时显示图标+提示文字的空状态
- 有配置时才显示配置管理表格

**修改文件**：`DirectoryManager/components/ConfigTab.vue`

---

### 问题2：右侧只显示文件，不显示文件夹层次

**现象**：选择阶段后，右侧只显示文件列表，文件夹不显示或只显示根节点。

**根因**：`loadCurrentContent()` 中根级别folder加载逻辑错误：
- API返回树形结构：`[{Depth=1, Children:[{Depth=2, ...}]}]`
- 原代码用 `extractFoldersAtLevel(data, 1)` 只提取Depth=1的节点
- Depth=1的根节点没有`FolderCode`，无法点击导航
- 实际的子文件夹在Depth=2，被过滤掉了

**修复**：根级别时遍历tree根节点的`Children`数组，提取Depth=2的文件夹作为当前层显示。

**修改文件**：`DirectoryManager/index.vue` — `loadCurrentContent()` 方法

---

## 附录B：根级别文件夹/文件混合显示问题

### 现象
选择阶段后，右侧同时显示4个文件夹和171个文件（混在一起），而实际这171个文件分散在子文件夹中。

### 根因
`loadCurrentContent()` 根级别时调用了 `directory-files?directoryCode=xxx` API，该API返回**整个目录下所有层级的文件**（171个），而非仅当前层级的文件。

### 修复
根级别过滤文件：只保留 `FolderCode` 以根文件夹code（L01）开头的文件：
```javascript
const rootFolderPrefix = rootChildren[0]?.FolderCode || ''
currentFiles.value = allFiles.filter(f => {
  const fc = f.FolderCode || f.folderCode || ''
  return f.IsValid !== false && (!rootFolderPrefix || fc.startsWith(rootFolderPrefix))
})
```

### 结果
- 根级别：4个文件夹 + 2个文件（仅L01根文件夹下）
- 子文件夹级别：正常显示该文件夹下的文件和子文件夹

---

## 附录C：目录管理页文件夹/文件混合显示问题（第二轮修复）

### 问题现象
根级别显示文件夹和文件混在一起（共175项：4文件夹+171文件），而非只显示根级别内容（4文件夹+2文件）。

### 根因分析

**API设计特点**：
| API | 返回内容 |
|-----|---------|
| `GET configs/{dirCode}/folders` | 完整树形结构（含嵌套Children） |
| `GET directory-files?directoryCode=xxx` | 整个目录下所有层级的文件（扁平） |
| `GET folders/{folderCode}/files` | 指定文件夹下的所有文件（扁平，不含子文件夹） |

**原代码问题**：
1. 根级别用 `extractFoldersAtLevel(data, 1)` 提取Depth=1节点 → 得到根节点（无FolderCode，不可点击）
2. 根级别文件用 `directory-files` API 获取全部171个文件 → 未过滤层级，全部显示

### 修复方案

1. **根级别folder**：遍历tree根节点的`Children`数组，提取Depth=2的子文件夹
2. **根级别file**：用根节点`FolderCode`（L01）作为前缀过滤 `directory-files` 结果
3. **子文件夹**：通过 `configs/{dirCode}/folders` 的tree结构获取子文件夹（`extractChildFolders`），通过 `folders/{folderCode}/files` 获取文件

### 关键代码

```javascript
// 根级别：取tree根节点的直接子节点（Depth=2的文件夹）
const rootChildren = []
for (const root of (Array.isArray(data) ? data : [data])) {
  if (root.Children) rootChildren.push(...root.Children)
}
currentFolders.value = rootChildren.map(f => normalizeItem(f, 'folder'))

// 根级别文件：用L01根节点code作为前缀过滤
const rootFolderPrefix = (data[0] && (data[0].FolderCode || data[0].folderCode)) || ''
currentFiles.value = allFiles.filter(f => {
  const fc = f.FolderCode || f.folderCode || ''
  return f.IsValid !== false && (!rootFolderPrefix || fc.startsWith(rootFolderPrefix))
})
```

### 验证结果

| 场景 | 文件夹 | 文件 | 总计 |
|------|--------|------|------|
| 根级别(L01) | 4 | 2 | 6 ✓ |
| 进入L02/S001 | 0 | 6 | 6 ✓ |
| 进入L02/S003 | 2 | 2 | 4 ✓ |

---

## 附录D：新建根文件夹后不显示的修复

### 现象
在STAGE03创建新根文件夹后，右侧不显示新建的文件夹。

### 根因
`loadCurrentContent()` 根级别逻辑固定取 `root.Children`（Depth=2），但新建的根文件夹本身就是Depth=1节点且无子节点，导致取空数组。

```javascript
// 修复前（有bug）
const rootChildren = []
for (const root of data) {
  if (root.Children) rootChildren.push(...root.Children)  // STAGE03的root.Children为空
}
currentFolders.value = rootChildren  // → []，不显示！
```

### 修复
判断根节点是否有子节点：
- 有子节点 → 用子节点（STAGE01的正常树结构）
- 无子节点 → 用根节点本身（新建的根文件夹）

```javascript
// 修复后
for (const root of data) {
  if (root.Children && root.Children.length > 0) {
    rootChildren.push(...root.Children)  // 正常树：显示子文件夹
  } else {
    rootChildren.push(root)  // 新建根文件夹：直接显示
  }
}
```

同时修复文件过滤前缀的获取逻辑，确保使用正确的FolderCode。

---

## 附录E：新建根文件夹后不显示的完整修复

### 问题链
1. **根级别folder不显示**：`extractFoldersAtLevel(data, 1)` 只取Depth=1节点，但这些是容器节点（无FolderCode）
2. **新建根文件夹不显示**：修复1后，根节点无子节点时 `root.Children` 为空数组
3. **根级别文件错误过滤**：用 `data[0].FolderCode` 作为前缀，但新建文件夹后 `data[0]` 可能是新文件夹而非原容器

### 最终修复逻辑

```javascript
// 根级别文件夹：有子节点用子节点，无子节点用根节点本身
const rootChildren = []
for (const root of data) {
  const ch = root.Children || []
  if (ch.length > 0) {
    rootChildren.push(...ch)  // 正常树结构：显示子文件夹
  } else if (root.FolderName || root.folderName) {
    rootChildren.push(root)   // 新建根文件夹：直接显示
  }
}

// 根级别文件：排除L02+层级的文件（仅显示根文件夹下的）
currentFiles.value = allFiles.filter(f => {
  const fc = f.FolderCode || f.folderCode || ''
  return f.IsValid !== false && !fc.includes('|L02|') && !fc.includes('|L03|')
})
```

### 数据验证

| 场景 | 文件夹 | 文件 | 状态 |
|------|--------|------|------|
| STAGE01根级别 | 5个（含新建3223+原4个） | 3个（L01下） | ✓ |
| STAGE03根级别 | 1个（新建3223） | 0个 | ✓ |
| L02/S001 | 0个子文件夹 | 6个文件 | ✓ |
| L02/S003 | 2个子文件夹 | 2个文件 | ✓ |

---

## 附录F：GetMaxSequence序号计算错误导致重复键冲突

### 现象
创建根文件夹时报错：`Duplicate entry 'FD-SDC-ISO134852016|STAGE01|L01|S002' for key 'uk_folder_code'`

### 根因
`GetMaxSequence()` 从 `FolderCode` 中提取序号的索引计算错误：
- FolderCode格式：`FD-{DirCode}|L{Level}|S{Sequence}`（5段，索引0-4）
- 代码用 `seqIndex = isRoot ? 2 : 3`，取的是 `parts[2]`（如"STAGE01"），而非 `parts[4]`（如"S002"）
- "STAGE01"去除"L"后为"TAGE01"，`int.TryParse`失败，maxSeq始终为0
- 导致每次新建都生成S001，与已有根文件夹冲突

### 修复
```csharp
// 修复前（错误）
var seqIndex = isRoot ? 2 : 3;
var seqStr = seqIndex < parts.Length ? parts[seqIndex] : "S001";
var numStr = seqStr.Replace(isRoot ? "L" : "S", "");

// 修复后（正确）
var seqStr = parts.Length > 0 ? parts[parts.Length - 1] : "S001";  // 直接取最后一段"S00x"
var numStr = seqStr.Replace("S", "");  // "001" → 1
```

### 验证
创建成功后返回 `FD-SDC-ISO134852016|STAGE01|L01|S003`（正确递增）

---

## 附录G：创建子文件夹后前端不显示

### 问题
在子文件夹内创建新文件夹后，API返回成功但前端页面不显示新建的文件夹。

### 根因
`extractChildFolders()` 函数在遍历树形结构时存在逻辑错误：
```javascript
// 错误代码
for (const root of tree) findAndExtract(root.Children || [root])
```
当 `root.Children` 为空数组 `[]` 时，`[] || [root]` 返回 `[root]`（因为空数组是truthy），
导致函数错误地将root自身作为搜索目标，跳过了对root.Children的检查。

### 修复
```javascript
// 正确代码
for (const root of tree) findAndExtract(root.Children || [])
// 并且始终递归检查Children（不依赖||回退）
if (node.Children) {
  if (findAndExtract(node.Children)) return true
}
```

### 附带修复
同时修复了 `submitFolder` 中depth的计算，确保子文件夹使用正确的depth值：
```javascript
depth: currentFolderCode.value ? (currentFolderCode.value.includes('|L0') ? 2 : 1) : 1
```

---

## 附录H：子文件夹创建后不显示的完整修复

### 问题现象
在子文件夹内点击"新建文件夹"并成功后，右侧页面不显示新建的文件夹。

### 根因分析（两个独立bug）

**Bug 1: depth计算错误**
- 前端 `submitFolder` 硬编码 `depth: 1`
- 在子文件夹（如S003）内新建时应为 `depth: 2`
- 导致新建文件夹的FolderCode为 `L01|S00x` 而非 `L02|S00x`
- 树结构中该文件夹成为根级别节点，不在父文件夹的Children中

**Bug 2: extractChildFolders搜索逻辑错误**
- 原代码：`for (const root of tree) findAndExtract(root.Children || [root])`
- 当 `root.Children` 为空数组 `[]` 时，`[] || [root]` 返回 `[root]`（空数组truthy）
- 导致错误地将root自身传入搜索，而非其子节点
- 正确逻辑：应同时检查root本身是否匹配parentCode

### 修复代码

```javascript
// 修复1: submitFolder depth自动计算
depth: currentFolderCode.value ? (currentFolderCode.value.includes('|L0') ? 2 : 1) : 1,

// 修复2: extractChildFolders同时检查根节点
if (Array.isArray(tree)) {
  for (const root of tree) {
    const code = root.FolderCode || root.folderCode
    if (code === parentCode) {
      if (root.Children) result.push(...root.Children)
    } else if (root.Children) {
      findAndExtract(root.Children)
    }
  }
}
```
