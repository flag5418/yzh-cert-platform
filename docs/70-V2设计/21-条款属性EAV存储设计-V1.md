# 条款属性：定义表 + 条款配置表（V4）

> **版本**：V4（取代 V3） ｜ **日期**：2026-10-28 ｜ **状态**：待裁决（D-J1…D-J4）
>
> **V4 只改一件事：把四层拆开。**
>
> | 层 | 回答 | 谁填 | 什么时候 |
> |---|---|---|---|
> | 属性定义 | **有哪些属性、类型是什么** | 平台 | 一次 |
> | 条款配置 | **这条款要求什么** | 专家 / AI 起草 | 一次（每标准一次） |
> | 审核计划 | **这次怎么查、条款怎么合并、谁去查** | 审核员 | 每次审核 |
> | 企业实际值 | **这家企业实际是什么、可信不可信** | 现场 / 匹配 | 每家企业 |
>
> V3 把第 1、2 层写对了，却把第 3、4 层的东西（`ClauseNumber`、`Source`、`Confidence`、`ValueState`、6 个类型列）塞进第 2 层。本文把它们全部赶回去。

---

## 一、表一：`cert_clause_attr_def`（属性定义 · 18 行）

| 字段 | 说明 |
|---|---|
| `Code` | 属性码，业务键（`Obligation`） |
| `AttrName` | 属性名（强制程度） |
| `ValueType` | 值类型（`dict`） |
| `DictCode` | 值域字典（`DICT_OBLIGATION`） |
| `Remark` | 作用，一句话 |
| `IsValid` | 是否启用 |

加标准审计列。**业务列 3 个**（`AttrName` `ValueType` `DictCode`），其余是项目通用列。

| # | `Code` | 属性名 | `ValueType` | `DictCode` |
|:--:|---|---|---|---|
| 1 | `ClauseIntent` | 条款作用 | `text` | |
| 2 | `Obligation` | 强制程度 | `dict` | `DICT_OBLIGATION` |
| 3 | `Applicability` | 适用范围 | `dict` | `DICT_APPLICABILITY` |
| 4 | `ApplicabilityCondition` | 适用条件 | `json` | |
| 5 | `RelatedClauses` | 关联条款 | `text_multi` | |
| 6 | `RequirementItems` | 分项要求 | `json` | |
| 7 | `Artifacts` | 要求的企业材料 | `json` | |
| 8 | `FormFields` | 材料必填栏目 | `json` | |
| 9 | `EvidencePoints` | 取证要点 | `text_multi` | |
| 10 | `EvidenceKinds` | 证据形态 | `dict_multi` | `DICT_EVIDENCE_KIND` |
| 11 | `CheckModes` | 检查方式 | `dict_multi` | `DICT_CHECK_MODE` |
| 12 | `Criteria` | 判据参数 | `json` | |
| 13 | `SamplingDepth` | 取样深度 | `dict` | `DICT_DEPTH` |
| 14 | `Severity` | 缺失严重度 | `dict` | `DICT_SEVERITY` |
| 15 | `QuestionHint` | 问法要点 | `text` | |
| 16 | `ProcessAreas` | 过程域 | `dict_multi` | `DICT_PROCESS_AREA` |
| 17 | `PrimaryProcessArea` | 主过程域 | `dict` | `DICT_PROCESS_AREA` |
| 18 | `MergeGroupCode` | 建议合并组 | `text` | |

---

## 二、表二：`cert_clause_attr`（条款配置 · 一条款一属性一个值）

| 字段 | 说明 |
|---|---|
| `StandardCode` | 标准 Code |
| `ClauseCode` | 条款 Code |
| `AttrCode` | 属性 Code |
| `AttrValue` | **值，就这一个字段** |
| `IsValid` | 这条配置是否生效 |

唯一键 `(StandardCode, ClauseCode, AttrCode)`。就这些。

### 2.1 为什么值只有一个字段

值类型已经在表一里声明过了（`Obligation` 是 `dict`，`Artifacts` 是 `json`）。再在表二铺 `ValueCode` / `ValueBool` / `ValueNum` / `ValueDate` / `ValueJson` 五列，等于**把同一个事实写两遍**，而且必然打架：改表一的 `ValueType` 忘了迁表二的数据，两列都留着旧值，谁都不报错。

读取时按类型解析，一处收口：

```csharp
object? Parse(string valueType, string? raw) => valueType switch {
    "bool"                     => raw is "1" or "true",
    "text" or "dict"           => raw,
    "text_multi" or "dict_multi" or "json" => raw is null ? null : JsonSerializer.Deserialize<JsonElement>(raw),
    "prompt_filter" or "prompt_judge"      => raw,
    _ => raw
};
```

**要不要按值筛选？** 只有三个属性真的需要（`Obligation` / `Severity` / `Applicability`，报表和覆盖率要用）。做法是加**一个生成列**，不是铺六列：

```sql
ALTER TABLE cert_clause_attr
  ADD COLUMN `FilterCode` VARCHAR(100)
    GENERATED ALWAYS AS (
      CASE WHEN `AttrCode` IN ('Obligation','Severity','Applicability','SamplingDepth','PrimaryProcessArea')
           THEN `AttrValue` END) STORED,
  ADD KEY `idx_filter` (`AttrCode`,`FilterCode`);
```

需要筛的属性多一个，改一次 `CASE` 就行；不需要筛的，一列都不占。

### 2.2 三种空，两种就够

| 状态 | 表现 | 含义 |
|---|---|---|
| **未配置** | 没有这一行 | 还没提这条属性 |
| **明确无** | 有行，`AttrValue` 为空 | 提取过了，这条款确实不要求（7.3 没有任何材料） |
| ~~不适用~~ | — | **不属于这一层**。条款对某企业不适用，是计划层基于企业事实的判定，不是标准级的属性值 |

---

## 三、什么属于哪一层（这张表是本文的重点）

| 内容 | 层 | 归属 |
|---|---|---|
| 属性有哪些、类型、字典、是否启用 | 属性定义 | 表一 |
| `7.1.5.2` 要求校准证书、缺失算严重 | 条款配置 | 表二 |
| **`ClauseNumber`（7.1.5.2）** | 人读的编号 | `cert_iso_clause` 已有，**不在表二冗余**；要显示就 join，或计划里带 |
| 条款怎么合并成一次检查（`MergeGroupCode` 生效） | **审核计划** | `cert_audit_plan_clause` |
| 这条款这次查不查、谁查、哪天查 | **审核计划** | `cert_audit_plan_clause` / `_assign` |
| 阶段（S1 只查文件、不能开 NC） | **审核计划** | 计划按阶段生成任务时套用 |
| 企业实际有没有、是扫描件还是能读、要不要催 | **企业实际值** | `cert_plan_clause_value` |
| 这个值谁给的（AI / 专家）、多确定、谁复核过 | **企业实际值 / 条款配置的变更日志** | 那一层，不在表二 |
| 条款对这家企业适不适用 | **企业实际值**（基于企业事实求值） | 那一层 |

> 一句话：**表二只回答"标准要求什么"，一句话问不出"谁填的、多可信、这家企业有没有"。** 后三个问题各有各的表。

---

## 四、值类型（10 种）

| `ValueType` | 解析 | 例 |
|---|---|---|
| `bool` | 真假 | — |
| `int` / `date` | 整数 / 日期 | — |
| `text` | 单段文本 | `QuestionHint` |
| `text_multi` | 文本列表（JSON 数组字符串） | `RelatedClauses` |
| `dict` | 单选字典码 | `Obligation = shall` |
| `dict_multi` | 多选字典码 | `EvidenceKinds = ["record","site"]` |
| `json` | 结构由代码里的强类型定 | `Artifacts` |
| `prompt_filter` | 召回/过滤提示词，**不产结论** | `MatchPrompt` |
| `prompt_judge` | 判定提示词，**只允许机构级** | — |

**JSON 的结构不在表里，在代码里**：

```csharp
public class ArtifactItem {
    public string ArtifactType { get; set; } = "";   // DICT_ARTIFACT_TYPE
    public string Name { get; set; } = "";
    public bool Mandatory { get; set; } = true;
    public string? Severity { get; set; }            // 空 = 继承条款级 Severity
    public string? Depth { get; set; }
    public string? FormSchemaCode { get; set; }
}
```

**材料级严重度不占一行属性**：它在 `Artifacts` 元素里。它脱离"哪份材料"无法独立存在，所以是字段不是属性。条款级 `Severity`/`SamplingDepth` 保留作默认值。

---

## 五、DDL 草案（未执行）

```sql
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE `cert_clause_attr_def` (
  `Id`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `Code`      VARCHAR(36)  NOT NULL,
  `AttrName`  VARCHAR(100) NOT NULL,
  `ValueType` VARCHAR(20)  NOT NULL COMMENT 'text/text_multi/dict/dict_multi/json/bool/int/date/prompt_filter/prompt_judge',
  `DictCode`  VARCHAR(50)  NULL     COMMENT 'dict/dict_multi 必填',
  `Remark`    VARCHAR(500) NULL     COMMENT '作用',
  `IsValid`   TINYINT      NOT NULL DEFAULT 1,
  `Sort`      INT          NOT NULL DEFAULT 0,
  `IsDeleted` TINYINT      NOT NULL DEFAULT 0,
  `DeleteBy`  VARCHAR(64) NULL, `DeleteTime` DATETIME NULL,
  `CreateBy`  VARCHAR(64) NULL, `CreateTime` DATETIME NULL,
  `UpdateBy`  VARCHAR(64) NULL, `UpdateTime` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款属性定义';

CREATE TABLE `cert_clause_attr` (
  `Id`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `StandardCode` VARCHAR(36) NOT NULL,
  `ClauseCode`   VARCHAR(36) NOT NULL,
  `AttrCode`     VARCHAR(50) NOT NULL,
  `AttrValue`    TEXT        NULL COMMENT '值；json 类型存 JSON 字符串',
  `IsValid`      TINYINT     NOT NULL DEFAULT 1,
  `IsDeleted`    TINYINT     NOT NULL DEFAULT 0,
  `DeleteBy`     VARCHAR(64) NULL, `DeleteTime` DATETIME NULL,
  `CreateBy`     VARCHAR(64) NULL, `CreateTime` DATETIME NULL,
  `UpdateBy`     VARCHAR(64) NULL, `UpdateTime` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_attr` (`StandardCode`,`ClauseCode`,`AttrCode`),
  KEY `idx_clause` (`StandardCode`,`ClauseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款配置：标准 × 条款 × 属性 = 一个值';
```

---

## 六、待裁决

| # | 决定 | 建议 |
|---|---|---|
| **D-J1** | 表二只留 `AttrValue` 一个值字段，接受否？ | 接受；要筛就加生成列，不铺六列 |
| **D-J2** | `Source` / `Confidence` / `ReviewStatus` / `ValueState` 全部移出表二？ | 移出（属企业实际值 / 变更日志） |
| **D-J3** | `ClauseNumber` 不在表二冗余？ | 不冗余；显示时 join 条款表 |
| **D-J4** | `MergeGroupCode` 留作"建议"，实际合并由计划决定？ | 留建议，不生效 |

---

*21 号 V4。DDL 为草案，未执行（库中确认无此二表）。*
