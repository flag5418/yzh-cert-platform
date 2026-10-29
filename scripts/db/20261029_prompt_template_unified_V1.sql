-- ============================================================================
-- 20261029_prompt_template_unified_V1.sql —— 提示词模板统一表（cert_prompt_template）
-- ============================================================================
-- 设计：一张表管全部提示词，PromptCode 区分类型，ScopeKey 区分作用域。
--
-- 作用域优先级（具体 → 通用）：
--   ① OrgCode + ScopeKey   （机构 × 细分维度）
--   ② OrgCode              （机构默认）
--   ③ ScopeKey             （平台级 × 细分维度）
--   ④ 平台默认               （OrgCode 空 + ScopeKey 空）
--
-- 已接管：
--   - clause_attr_gen：条款属性预生成（原 cert_sys_config · clause_attr_prompt）
--   - doc_extract：文档提取提示词（原 cert_doc_extract_rules.PromptContent）
--
-- 不接管：
--   - wf_prompt_template（Dify 工作流模板体系，字段语义和生命周期完全不同）
-- ============================================================================

CREATE TABLE IF NOT EXISTS `cert_prompt_template` (
  `Id`       BIGINT       NOT NULL AUTO_INCREMENT  COMMENT '自增主键（不参与业务）',
  `Code`     VARCHAR(64)  NOT NULL                 COMMENT '业务键 GUID（唯一）',
  `PromptCode`   VARCHAR(100) NOT NULL             COMMENT '提示词代码（英文标识，如 clause_attr_gen / doc_extract）',
  `PromptName`   VARCHAR(200) NOT NULL             COMMENT '提示词名称（人类可读）',
  `OrgCode`      VARCHAR(64)  DEFAULT NULL          COMMENT '机构 Code（空=平台级；机构专属时填）',
  `ScopeKey`     VARCHAR(100) DEFAULT NULL          COMMENT '细分维度（标准Code / 阶段Code；空=默认）',
  `Template`     TEXT         NOT NULL             COMMENT '提示词正文（含 {{placeholders}} 占位符）',
  `Placeholders` TEXT         DEFAULT NULL          COMMENT '占位符注册表（JSON 数组：[{token, description}]）',
  `Remark`       VARCHAR(500) DEFAULT NULL          COMMENT '备注（提示词的作用说明）',
  `Sort`     INT          NOT NULL DEFAULT 0       COMMENT '排序号',
  `IsValid`  INT          NOT NULL DEFAULT 1       COMMENT '启用标志（1=启用 / 0=停用）',
  `Status`   VARCHAR(20)  DEFAULT 'active'         COMMENT '业务状态',

  -- BaseEntity 审计字段
  `IsDeleted`    TINYINT(1)   NOT NULL DEFAULT 0     COMMENT '软删除标志',
  `CreateBy`     VARCHAR(64)  DEFAULT NULL            COMMENT '创建人（用户 Code）',
  `CreateTime`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间（本地时间）',
  `UpdateBy`     VARCHAR(64)  DEFAULT NULL            COMMENT '更新人（用户 Code）',
  `UpdateTime`   DATETIME     DEFAULT NULL            COMMENT '更新时间（本地时间）',
  `DeleteBy`     VARCHAR(64)  DEFAULT NULL            COMMENT '删除人（用户 Code）`,
  `DeleteTime`   DATETIME     DEFAULT NULL            COMMENT '删除时间（本地时间）',

  PRIMARY KEY (`Id`),

  -- 唯一索引：三元组 (PromptCode, OrgCode, ScopeKey) 幂等 upsert 判据
  UNIQUE KEY `uk_prompt_scope` (`PromptCode`, `OrgCode`(64), `ScopeKey`(64)),
  -- Code 业务键唯一
  UNIQUE KEY `uk_prompt_code`  (`Code`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci
  COMMENT = '提示词模板（统一管理全部提示词：作用域 + 占位符渲染）';

-- ────────────────────────────────────────────────────────────────────────────
-- 种子数据：clause_attr_gen（平台默认 = 原 ClauseAttrPromptService.FallbackTemplate）
-- ⚠️ 人工调优过的正文由用户通过前端编辑保存时再做 upsert；这里只种一条**占位**行，
--    让 GetAsync 不回落到硬编码兜底（控制台 / API 都能读到）。
-- ────────────────────────────────────────────────────────────────────────────

INSERT INTO `cert_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Remark`, `Sort`, `IsValid`, `Status`, `IsDeleted`, `CreateBy`, `CreateTime`)
VALUES
  (REPLACE(UUID(), '-', ''), 'clause_attr_gen', '条款属性预生成提示词',
   NULL, NULL,
   '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，为这条条款产出「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。
字段名 = 文末「属性清单」里的字段名，**逐个都要输出**；不许增、删、改名，不许输出清单外的字段。

## 通用判据

① 先读条款正文的**主句**（第一句带义务动词的话），再看细节；正文为空或纯说明性 ⇒ 按「不产生检查要求」处理，各字段给空值。
② 出现「适用时 / 如有时 / 当…时」这类限定语 ⇒ 该条款是**条件适用**，必须把触发条件写出来，不许笼统写「视情况而定」。
③ 判不了就留空串（或空数组），不许编造、不许用通用套话把字段填满。空值 = 待人工确认，是合法输出。
④ 所有文本用简体中文；字段名保持英文原样。

## 硬约束

- 「企业该提供什么」与「审核员怎么取证」必须分开产出（各归各的属性），合并会导致企业照着取证剧本准备、审核失效。
- 不许把条款原文抄一遍充当答案。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

{{attr_pool}}',
   '条款属性配置页 AI 预生成的平台级提示词（对全部标准、全部条款生效）。属性池注入段由 ClauseAttrPromptService 实时渲染。',
   100, 1, 'active', 0, 'migration_20261029', NOW())
ON DUPLICATE KEY UPDATE `Id` = `Id`;

-- ────────────────────────────────────────────────────────────────────────────
-- 种子数据：doc_extract（平台默认 = 空模板，提示"请配置"）
-- 具体标准的在各标准行（ScopeKey = cert_iso_standard.Code）首次配置时 upsert 产生。
-- ────────────────────────────────────────────────────────────────────────────

INSERT INTO `cert_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Remark`, `Sort`, `IsValid`, `Status`, `IsDeleted`, `CreateBy`, `CreateTime`)
VALUES
  (REPLACE(UUID(), '-', ''), 'doc_extract', '文档提取提示词',
   NULL, NULL,
   '你是一名文档信息提取专家。请从企业提交的文档中，提取以下字段：

{{extract_fields}}

## 输出要求
- 只输出 JSON，不要 markdown 代码块，不要任何解释文字。
- 读不到就留空串，不许推测。',
   '文档提取提示词的平台默认模板（细分维度为空）。具体标准的提示词在第一次编辑该标准时自动创建。',
   100, 1, 'active', 0, 'migration_20261029', NOW())
ON DUPLICATE KEY UPDATE `Id` = `Id`;

-- 验证
-- SELECT PromptCode, ScopeKey, OrgCode, IsValid, LENGTH(Template), CreateBy FROM cert_prompt_template;
