# 联网查询公共服务：Skill 化设计

> **版本**：V1 ｜ **日期**：2026-10-28 ｜ **状态**：待裁决（D-P1…D-P6）
>
> **定位**：把 00 号 §六 那句「联网查询公共服务」从**一句话**变成**可被提示词引用的 Skill 集**。
> **独立性**：独立一套表，**不影响 L0 属性池 / L1 条款配置 / 既有功能设计** —— 它只给提示词提供上下文。
>
> **为什么现在要定**：24 号把 `Artifacts` / `EvidenceChain` 的值改成了提示词（§3.3），提示词需要引用外部数据。专家端的一部分证据**本来就来源于公共服务**（工商、抽查、处罚、事故），不该全靠企业自述。

---

## 一、Skill 化的三个理由

| 问题 | 现在 | Skill 化后 |
|---|---|---|
| 数据源没有定义 | 只有一句"联网查询公共服务" | 每个源一个 Skill（`Code` + 入参 + 出参 + 摘要模板） |
| 查询没有留痕 | 不知道查没查、什么时候查的 | 结果表带 `QueriedAt` / `Source` / 原始 JSON |
| 提示词引用不了 | 只能靠人记 | `{{skill:国家监督抽查}}` 占位，运行时注入 |

> **一句话**：让「外部数据」变成和「企业资料」平级的**提示词上下文来源**。

---

## 二、两张表

### 2.1 `cert_public_skill`（Skill 定义 · 平台级）

| 字段 | 类型 | 必填 | 说明 |
|---|---|:--:|---|
| `Code` | varchar(50) | ✅ | 业务键，如 `SAMR_SPOT_CHECK` |
| `SkillName` | varchar(100) | ✅ | 国家监督抽查 |
| `Category` | varchar(50) | ✅ | 工商 / 质量 / 安全 / 环保 / 司法 / 信用 |
| `Provider` | varchar(200) | | 数据来源（国家企业信用信息公示系统…） |
| `AccessMode` | varchar(20) | ✅ | `api` / `web` / `manual` |
| `Endpoint` | varchar(500) | | 接口或页面地址（`manual` 为空） |
| `ParamsSchema` | json | | 入参结构，如 `{"creditCode":"","years":3}` |
| `ResultSchema` | json | | 出参结构 |
| `SummaryTemplate` | varchar(1000) | | **摘要模板**，供提示词注入，如 `"近{years}年抽查{n}次，不合格{m}次"` |
| `CacheTtlHours` | int | ✅ | 缓存有效期（小时） |
| `IsValid` / `Sort` | | | 同项目惯例 |

### 2.2 `cert_public_skill_result`（查询结果 + 留痕 · 企业级）

| 字段 | 类型 | 必填 | 说明 |
|---|---|:--:|---|
| `EnterpriseCode` | varchar(36) | ✅ | 企业 |
| `SkillCode` | varchar(50) | ✅ | 哪个 Skill |
| `QueryKey` | varchar(64) | ✅ | **参数指纹**（`md5(params)`），同参数复用同一行 |
| `ParamsJson` | json | | 本次查询入参（如信用代码、年份范围） |
| `ResultJson` | json | | 原始结果 |
| `Summary` | varchar(2000) | | 按 `SummaryTemplate` 渲染的摘要，**提示词注入的就是它** |
| `Status` | varchar(20) | ✅ | `success` / `empty` / `failed` / `blocked` |
| `QueriedAt` | datetime | ✅ | 查询时间 |
| `ExpireAt` | datetime | ✅ | 过期时间（`QueriedAt + CacheTtlHours`） |
| `HitCount` | int | ✅ | 被提示词引用次数（用于判断这个 Skill 值不值得维护） |

**唯一键** `(EnterpriseCode, SkillCode, QueryKey)`；索引 `(EnterpriseCode, SkillCode, ExpireAt)`。

---

## 三、7 个 Skill（对齐 00 号 §六）

| `Code` | 名称 | 用来干什么 | 特别之处 |
|---|---|---|---|
| `GS_REGISTER` | 工商登记 | 存续状态、成立年限、经营范围 | 判断"运行 ≥3 月"的前置事实 |
| `SAMR_SPOT_CHECK` | **国家监督抽查** | 产品抽查不合格记录 | ★ **命中不合格 ⇒ CNCA 5.9 要求 30 日内特殊审核**（法定倒计时，不是加权信号） |
| `PENALTY` | 行政处罚 | 近 3 年处罚记录 | 只作线索 |
| `SAFETY_ACCIDENT` | 生产安全事故 | 事故等级与时间 | 只作线索 |
| `COMPLAINT` | 投诉举报 | 有效投诉记录 | 只作线索 |
| `LITIGATION` | 诉讼与失信 | 被执行人、失信名单 | 只作线索 |
| `ENV_PENALTY` | 环保处罚 | 环保违法记录 | 只作线索 |

> 首批建议只做 **2 个**：`SAMR_SPOT_CHECK`（有法定触发，价值最高）+ `GS_REGISTER`（最常用，且是"运行 ≥3 月"的事实来源）。其余按需补。

---

## 四、提示词怎么引用（核心机制）

### 4.1 语法

在 L1 的 `prompt` 类型属性值里写占位符：

```
参考【国家监督抽查】结果：{{skill:国家监督抽查}}
若该企业近 3 年有不合格记录，必须核查整改闭环（整改报告、验证记录）。
```

占位符用 **`SkillName`**（人可读），不用 `Code` —— 因为提示词是给专家写的，可读性优先。

### 4.2 运行时解析（四步）

```
① 按 EnterpriseCode + SkillName 找到 SkillCode
② 查 cert_public_skill_result（同 QueryKey）
   ├─ 命中且未过期 → 取 Summary
   ├─ 命中但已过期 → 重查
   └─ 未命中       → 调 Skill（api/web）或等人工录入（manual）
③ 结果落库（新行或更新旧行），刷新 ExpireAt
④ 把 Summary 替换进提示词
```

### 4.3 查不到时注入什么

| 情况 | 注入 | 为什么 |
|---|---|---|
| `Status=empty`（查了，无记录） | `"（无记录）"` | 让 LLM 知道**查过了**，与"没查"区分 |
| `Status=failed` | `"（查询失败，未获取到）"` | 明确失败，不假装无记录 |
| Skill 不存在 | **报配置错误**，不放行 | 拼错 Skill 名必须暴露，不能静默 |

> ⚠️ **`empty ≠ 没问题`**。工商无处罚记录只说明"没查到"，不构成"合规"的证据。

---

## 五、三条红线（与 00 号 §六 一致，不许破）

1. **只查公开数据** —— 不碰企业非公开信息。
2. **结果只作线索，不作 NC 证据** —— 唯一例外是国抽不合格，那是**触发法定任务**（30 日内特殊审核），不是"证据"。
3. **必须留痕** —— 查了什么、什么时候、哪个源、原始结果。

---

## 六、与现有设计的关系（明确"不影响"）

| 现有 | 影响 | 说明 |
|---|---|---|
| L0 属性池 | **无** | Skill 不占属性位，属性池仍是 8 条 |
| L1 条款配置 | **仅多一种占位符语法** | `{{skill:xxx}}`，不影响 `AttrValue` 结构 |
| `cert_enterprise` | 通过 `EnterpriseCode` 关联 | 不加列 |
| `cert_validation_rule` | **无** | Skill 结果不进判定，只进提示词上下文 |
| 24 号 两份要求 | **增强** | `EvidenceChain` 提示词可引用 Skill，让取证更有针对性 |

**为什么独立一套表，不塞进属性池**：

- Skill 是**数据源定义**（平台级，跨企业复用）；属性是**条款要求**（标准级）。
- Skill 结果挂**企业**（每次查询一份）；属性值挂**条款**。
- 塞进属性 ⇒ 属性池被数据源污染，且"这个企业查没查过国抽"无处记。

---

## 七、DDL 草案（未执行）

```sql
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE `cert_public_skill` (
  `Id`              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `Code`            VARCHAR(50)  NOT NULL COMMENT '业务键',
  `SkillName`       VARCHAR(100) NOT NULL COMMENT '中文名，提示词占位符用它',
  `Category`        VARCHAR(50)  NOT NULL COMMENT '工商/质量/安全/环保/司法/信用',
  `Provider`        VARCHAR(200) NULL,
  `AccessMode`      VARCHAR(20)  NOT NULL DEFAULT 'api' COMMENT 'api/web/manual',
  `Endpoint`        VARCHAR(500) NULL,
  `ParamsSchema`    JSON         NULL,
  `ResultSchema`    JSON         NULL,
  `SummaryTemplate` VARCHAR(1000) NULL COMMENT '摘要模板，供提示词注入',
  `CacheTtlHours`   INT          NOT NULL DEFAULT 168,
  `IsValid`         TINYINT      NOT NULL DEFAULT 1,
  `Sort`            INT          NOT NULL DEFAULT 0,
  `IsDeleted`       TINYINT      NOT NULL DEFAULT 0,
  `CreateBy`        VARCHAR(64) NULL, `CreateTime` DATETIME NULL,
  `UpdateBy`        VARCHAR(64) NULL, `UpdateTime` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_name` (`SkillName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='联网查询 Skill 定义';

CREATE TABLE `cert_public_skill_result` (
  `Id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `EnterpriseCode` VARCHAR(36) NOT NULL,
  `SkillCode`      VARCHAR(50) NOT NULL,
  `QueryKey`       VARCHAR(64) NOT NULL COMMENT 'md5(params)，同参数复用',
  `ParamsJson`     JSON        NULL,
  `ResultJson`     JSON        NULL,
  `Summary`        VARCHAR(2000) NULL COMMENT '提示词注入的就是它',
  `Status`         VARCHAR(20) NOT NULL DEFAULT 'success' COMMENT 'success/empty/failed/blocked',
  `QueriedAt`      DATETIME    NOT NULL,
  `ExpireAt`       DATETIME    NOT NULL,
  `HitCount`       INT         NOT NULL DEFAULT 0,
  `IsDeleted`      TINYINT     NOT NULL DEFAULT 0,
  `CreateBy`       VARCHAR(64) NULL, `CreateTime` DATETIME NULL,
  `UpdateBy`       VARCHAR(64) NULL, `UpdateTime` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_query` (`EnterpriseCode`,`SkillCode`,`QueryKey`),
  KEY `idx_expire` (`EnterpriseCode`,`SkillCode`,`ExpireAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='联网查询结果与留痕';
```

---

## 八、待裁决

| # | 决定 | 建议 |
|---|---|---|
| **D-P1** | 独立 2 表，不复用 `cert_expert_task`？ | **是** —— 那是 AI 任务，不是数据源 |
| **D-P2** | 7 个 Skill 一次做全，还是先做 2 个？ | 先 `SAMR_SPOT_CHECK` + `GS_REGISTER` |
| **D-P3** | 缓存 TTL 默认多久？ | 工商 720h（30 天）/ 抽查 168h / 处罚 168h |
| **D-P4** | 查不到时注入「（无记录）」还是跳过？ | **注入** —— 让 LLM 知道查过了 |
| **D-P5** | `AccessMode=manual` 怎么处理？ | 人工录入结果，同表同结构，`Status` 照填 |
| **D-P6** | Skill 结果要不要进 L2 企业值？ | **不进** —— 它是提示词上下文，不是条款值 |

---

## 九、坑

1. **把 Skill 结果当 NC 证据** ⇒ 违法。公开数据只能作线索，唯一例外是国抽不合格触发法定任务。
2. **缓存不设过期** ⇒ 拿三年前的处罚记录下判定。`ExpireAt` 必须每次重查时刷新。
3. **`empty` 当"没问题"** ⇒ 工商无处罚只说明"没查到"，不是"合规"。提示词里要写清"无记录 ≠ 合规"。
4. **Skill 名拼错静默放行** ⇒ 提示词里 `{{skill:国家监查抽查}}` 少一个字，结果注入空字符串，LLM 照常输出。**必须报配置错误**。
5. **把 Skill 塞进属性池** ⇒ 属性池被数据源污染，且"这家企业查没查过"无处记。

---

*本文为联网查询 Skill 化 V1。2 张表 DDL 未执行。与 24 号 / 25 号 完全增量，不改任何既有表结构。*
