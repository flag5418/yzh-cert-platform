# 条款属性存储设计：字段字典与 DDL（V1）

> 2026-10-26 ｜ 回答「条款有哪些属性、每个属性的值怎么存」
> 前置：[63-标准条款属性字典](63-标准条款属性字典与两逻辑主线-V1.md)
> 落地脚本：`scripts/db/20261026_clause_attr_schema_V1.sql`（★ **草案，未执行**）
> 一句话：**16 个属性只用 3 种存法** —— 独立列（可筛选）/ JSON 列（只渲染）/ 子表（要被引用）。判据只有一条：**这个属性会不会被拿来筛选或当外键用**。

---

## 一、先看结论：一张图看清 16 个属性落在哪

```
cert_iso_clause（已有，不动）
   ClauseNumber · Title · Description(原文)
        │ Code
        ▼
cert_iso_clause_attr（新建 · 1 条款 1 行 · 14 个业务属性）
   ├─ 独立列（8）：Intent · Obligation · Applicability · EvidenceKinds
   │                CheckModes · Depth · Severity · Question
   ├─ JSON 列（5）：ApplicabilityConditionJson · RequirementItemsJson
   │                EvidencePointsJson · CriteriaJson · RelatedClausesJson
   └─ 治理列（5）：Source · Confidence · ReviewStatus · ReviewBy · ReviewTime
        │ ClauseCode
        ▼
cert_iso_clause_artifact（新建 · 1 条款 N 行 · C2 应有产物）
   ArtifactName · ArtifactType · IsMandatory · EvidenceKind · FormSchemaCode
        │ FormSchemaCode
        ▼
cert_doc_form_schema（新建 · 可复用 · C3 必备字段）
   SchemaName · FieldsJson · Version
```

**16 个属性的分配**：

| 属性 | 存在哪 |
|---|---|
| A1 标识 | `cert_iso_clause_attr.ClauseCode` + `StandardCode`（列） |
| **A2 Title** | ❌ **不存** —— 直接读 `cert_iso_clause.Title` |
| **A3 Text** | ❌ **不存** —— 直接读 `cert_iso_clause.Description` |
| A4 `Intent` | 列 `Intent` |
| A5 `RelatedClauses` | JSON 列 `RelatedClausesJson` |
| B1 `Obligation` | 列 `Obligation` |
| B2 `Applicability` | 列 `Applicability` |
| B3 `ApplicabilityCondition` | JSON 列 `ApplicabilityConditionJson` |
| C1 `RequirementItems` | JSON 列 `RequirementItemsJson` |
| **C2 `Artifacts`** | **子表** `cert_iso_clause_artifact` |
| **C3 `Fields`** | **子表** `cert_doc_form_schema.FieldsJson` |
| C4 `EvidencePoints` | JSON 列 `EvidencePointsJson` |
| D1 `EvidenceKind` | 列 `EvidenceKinds`（逗号串） |
| D2 `CheckMode` | 列 `CheckModes`（逗号串） |
| D3 `Criteria` | JSON 列 `CriteriaJson` |
| D4 `Depth` | 列 `Depth` |
| D5 `Severity` | 列 `Severity` |
| D6 `Question` | 列 `Question` |
| E1–E4 治理 | 5 个列 |

> 所以：**16 个属性 → 主表 14 个 + 子表 2 个 + 不存 2 个**。

---

## 二、6 条存储判据（"为什么这么存"）

| # | 判据 | 落到哪种存法 | 反例 |
|---|---|---|---|
| **1** | **能算出来的不存** | Title / Text 已在条款表 ⇒ 不复制 | 复制一份标题，改标题时两处不一致 |
| **2** | **要拿来筛选/排序/统计的** | 独立列 + 索引 | `Severity` 若塞 JSON，就没法"按严重度排检查单" |
| **3** | **多值枚举** | 逗号串 varchar（几百行，`FIND_IN_SET` 足够） | 建 3 张关联表 = 过度设计 |
| **4** | **一对多、且要被别人按 Code 引用** | 子表 | `Artifacts` 塞 JSON ⇒ 证据单元没法挂到"某一份产物"上 |
| **5** | **结构固定、只渲染不检索** | JSON 列 | `RequirementItems` 建子表 = 多一张表只为存 a/b/c |
| **6** | **稀疏** | 全部可空、**不给默认值** | 见 §五，这是最容易踩的坑 |

---

## 三、主表字段字典（逐列）

### 3.1 业务列 —— 独立列（8 个，可索引可筛选）

| 属性 | 列名 | 类型 | 可空 | 取值 / 格式 | 真实例（7.1.5） |
|---|---|---|---|:--:|---|
| A4 | `Intent` | varchar(200) | ✅ | 一句话 ≤60 字 | `保证用于判定产品符合性的测量结果可信，即设备受控、按期校准、可溯源` |
| B1 | `Obligation` | varchar(10) | ✅ | `shall` / `should` / `may` / `note` | `shall` |
| B2 | `Applicability` | varchar(20) | ✅ | `universal` / `conditional` / `scope` / `na_by_org` | `universal` |
| D1 | `EvidenceKinds` | varchar(100) | ✅ | 逗号串：`doc,record,field,site,person,system` | `record,site` |
| D2 | `CheckModes` | varchar(100) | ✅ | 逗号串：`existence,completeness,content,timeliness,consistency,traceability` | `completeness,timeliness` |
| D4 | `Depth` | varchar(10) | ✅ | `single` / `sample` / `all` / `census` | `sample` |
| D5 | `Severity` | varchar(20) | ✅ | `major` / `minor` / `observation` / `none` | `major` |
| D6 | `Question` | varchar(500) | ✅ | 检查单提问句 | `请提供本年度监视和测量设备台账及校准/检定证书…` |

> 为什么 `EvidenceKinds` / `CheckModes` 用逗号串而不是 JSON 数组：**为了 `FIND_IN_SET` 能直接筛**。表只有几百行，全扫也快；将来多标准扩到上万行，再改 JSON + 多值索引（MySQL 8.0.17+）也不迟。

### 3.2 业务列 —— JSON 列（5 个，只渲染不检索）

| 属性 | 列名 | 类型 | JSON 结构 | 真实例 |
|---|---|---|---|---|
| B3 | `ApplicabilityConditionJson` | varchar(500) | `{"事实字段":值}` | `{"has_design_activity":true}` |
| C1 | `RequirementItemsJson` | text | `[{key,text,mandatory}]` | `[{"key":"7.1.5.2","text":"测量设备对照能溯源到…进行校准或检定","mandatory":true}]` |
| C4 | `EvidencePointsJson` | text | `["..."]` | `["设备台账是否覆盖全部在用设备","校准记录有效期是否覆盖审核期"]` |
| D3 | `CriteriaJson` | text | `[{type,expr,desc}]` | `[{"type":"interval","expr":"calibration_valid_at_audit_date","desc":"校准有效期须覆盖审核日"}]` |
| A5 | `RelatedClausesJson` | varchar(500) | `["条款号"]` | `["7.1.3"]` |

### 3.3 治理列（5 个）

| 属性 | 列名 | 类型 | 默认 | 取值 |
|---|---|---|---|---|
| E1 | `Source` | varchar(20) | `'ai'` | `ai` / `expert` / `case` / `import` |
| E2 | `Confidence` | decimal(3,2) | NULL | `0.00` ~ `1.00` |
| E3 | `ReviewStatus` | varchar(20) | `'draft'` | `draft` → `validated` → `reviewed` → `locked` |
| E4 | `ReviewBy` | varchar(50) | NULL | 复核人 Code |
| E4 | `ReviewTime` | datetime | NULL | 复核时间 |

### 3.4 基础列（与全库一致，不用想）

`Id` `Code` `OrgCode`(恒空) `CreateBy` `CreateTime` `UpdateBy` `UpdateTime` `DeleteBy` `DeleteTime` `IsDeleted` `Status` `IsValid` `Sort` `Remark`

---

## 四、两张子表

### 4.1 `cert_iso_clause_artifact`（C2 应有产物）

| 列 | 类型 | 说明 |
|---|---|---|
| `ClauseCode` | varchar(36) | 所属条款 |
| `ArtifactName` | varchar(200) | 如《监视和测量设备控制程序》 |
| `ArtifactType` | varchar(20) | `procedure` 程序 / `record` 记录 / `report` 报告 / `plan` 计划 / `list` 清单 / `mark` 标识 / `site` 现场状态 |
| `IsMandatory` | tinyint(1) | 1=必须有；0=有则查 |
| `EvidenceKind` | varchar(20) | 该产物的证据形态（一般 `doc`/`record`/`site`） |
| `FormSchemaCode` | varchar(36) | → `cert_doc_form_schema.Code` |
| `DocPatternJson` | varchar(500) | 兜底文件名匹配 `["*计量*","*校准*"]` |

**为什么是子表不是 JSON**（关键）：产物要被**证据单元按 Code 引用** —— "第 3 页这个字段缺失"必须能指向"某一份具体产物"，塞进 JSON 就没有 Code 可引用了。

### 4.2 `cert_doc_form_schema`（C3 必备字段）

| 列 | 类型 | 说明 |
|---|---|---|
| `SchemaName` | varchar(200) | 如「特殊岗位资格清单」 |
| `FieldsJson` | text | `[{"key":"cert_no","label":"证书编号","required":true,"type":"string","rule":""}]` |
| `Version` | varchar(20) | 换版时**新增行**，不改旧行 |

**为什么要单独一张表**：同一条款 7.3，A 企业用《设计开发控制程序》+ 附表 1~5，B 企业用《设计输入清单》《验证报告》各自独立。字段是**产物的属性**，同一 schema 能被多个条款/多个产物复用。

---

## 五、★ 两个必须提醒你的坑

### 5.1 `JudgeMode` 这个名已被占用，且语义不同

实测 `cert_validation_rule.JudgeMode varchar(20) NOT NULL DEFAULT 'auto'`，取值是 **`auto` / `manual` / `semi`** —— 它表示「**谁来判**」。

而 63 号里我想表达的 `JudgeMode` 是 **`existence` / `completeness` / `content` / `timeliness` / `consistency` / `traceability`** —— 表示「**判什么**」。

**同名不同义，必然有人填错。** 所以新表里改名为 **`CheckModes`**（检查方式）。这条要在 DDL 评审时特别注意，否则将来做"规则 > 属性"的覆盖时，两个 `JudgeMode` 会撞在一起。

### 5.2 一律不给默认值 —— 否则稀疏模型立刻失效

这是稀疏原则的直接后果：

```sql
-- ❌ 错：给了默认值
`Obligation` varchar(10) NOT NULL DEFAULT 'shall'

-- ✅ 对：可空，无默认
`Obligation` varchar(10) DEFAULT NULL
```

因为一旦有默认值，数据库里**所有行都显示 `shall`**，你就再也分不清：

- 「这条款确实是应」→ 已确认
- 「这条款还没提取」→ 待办

而这两者在工作流里完全不同：前者已完成，后者要进待办清单。**给了默认值，覆盖率永远算不准，属性维护必然烂尾。**

三种"空"的判读规则（写进代码）：

| 库里看到 | 含义 | 下一步 |
|---|---|---|
| `NULL` | 未设置 | 进待办 |
| `''` / `[]` / `'none'` | 明确不要求 | 已完成 |
| `Applicability='na_by_org'` | 整条不适用 | 已完成 |

---

## 六、索引与三个真实查询

### 6.1 索引（6 个）

```sql
UNIQUE KEY uk_clause_code (ClauseCode)        -- 1 条款 1 行
KEY idx_standard_code (StandardCode)          -- 按标准批量
KEY idx_obligation (Obligation)               -- 筛 shall
KEY idx_applicability (Applicability)         -- 筛适用性
KEY idx_severity (Severity)                   -- 按严重度排
KEY idx_review_status (ReviewStatus)          -- 待复核清单
```

### 6.2 三个查询（这就是"独立列"的价值）

```sql
-- ① 二阶段要查哪些条款（疑点渠道分派）
SELECT c.ClauseNumber, c.Title
FROM cert_iso_clause_attr a JOIN cert_iso_clause c ON c.Code = a.ClauseCode
WHERE a.IsDeleted = 0 AND a.Applicability <> 'na_by_org'
  AND FIND_IN_SET('site', a.EvidenceKinds);

-- ② 哪些条款缺失算 major（NC 排序/风险权重）
SELECT c.ClauseNumber, a.Intent FROM cert_iso_clause_attr a
JOIN cert_iso_clause c ON c.Code = a.ClauseCode
WHERE a.IsDeleted = 0 AND a.Obligation = 'shall' AND a.Severity = 'major';

-- ③ 待办清单（NULL = 未设置，直接就是工作队列）
SELECT c.ClauseNumber, c.Title, a.ReviewStatus
FROM cert_iso_clause c LEFT JOIN cert_iso_clause_attr a ON a.ClauseCode = c.Code AND a.IsDeleted = 0
WHERE c.IsDeleted = 0 AND c.StandardCode = 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62'
  AND (a.Id IS NULL OR a.Obligation IS NULL OR a.Intent IS NULL)
ORDER BY c.Sort;
```

> 第三个查询顺带解决一件事：**"哪些条款还没填"不需要另做报表** —— 一条 LEFT JOIN 就是待办清单。

---

## 七、重建条款树会不会丢属性？不会

`cert_iso_clause.Code = MD5(slug|条款号)`，**确定性**。所以：

- 重跑解析器 ⇒ 同一条款号 ⇒ **同一 Code** ⇒ 属性行完全不受影响
- 只有被删掉的条款才 `ON DELETE CASCADE` 删属性 —— 这正是期望行为

> 这也回答了我之前在 60 号里的担心（"属性不要写进条款主表，否则重建会丢"）：**独立成表 + 确定性 Code，两头都安全**。

---

## 八、状态

- 脚本：`scripts/db/20261026_clause_attr_schema_V1.sql` —— **已写好，未执行**
- 内容：3 张 `CREATE TABLE` + 2 条真实样例 INSERT（7.1.5 全维度 / 7.3 稀疏）+ 3 条验证查询
- 阻塞：63 号 D1–D5 未确认，按「先设计 → 审批 → 才写码」未动库
- 确认后执行顺序：① 建 3 表 → ② 跑 2 条样例验证结构 → ③ 出属性生成流水线

---

*本文档为存储设计，非最终定稿。§五 两条（`JudgeMode` 改名、禁止默认值）建议无条件接受。*
