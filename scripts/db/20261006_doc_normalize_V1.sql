-- ══════════════════════════════════════════════════════════════════════════════
-- 20261006 · 企业资料规范化 P0 地基
--   ① 新表 cert_doc_fill_value       （单元格级取值账本）
--   ② 新表 cert_doc_normalize_action （动作留痕，只追加）
--   ③ cert_standard_directory_file  +11 列
--   ④ cert_doc_fill_log             + 4 列
--   ⑤ cert_doc_template_anchor      + 2 列 + idx_parent
--   ⑥ 2 个字典（fill_value_status / normalize_action）
-- ══════════════════════════════════════════════════════════════════════════════
-- 权威依据（⛔ 不要凭记忆改）：
--   表结构   → docs/.../05-企业资料规范化/参考/41-01-数据模型与两个口径-V1.md §一
--   列数裁定 → docs/.../55-专家端企业资料规范化完整设计方案-V1.md §4.5（★ 20 → 17 列）
--   P0 范围  → docs/.../54-专家端资料规范化开发计划-V1.md §4 · §7（WBS P0）
--
-- ★★★ 三处「同义列」裁定（55 §4.5，⛔ 违反即静默分叉）：
--   1. ⛔ 不新增 NormalizedPath —— 沿用既有 cert_standard_directory_file.StoragePath（已回填 186/669）
--   2. ⛔ 不新增 cert_doc_fill_log.StandardFileCode —— 沿用既有 TemplateFileCode（其值本就 = 标准文件 Code）
--   3. ⛔ 不新增 cert_doc_fill_log.TemplateCode   —— 模板与标准文件 1:1，可反查
--   ⇒ 本脚本合计加列 = 11 + 4 + 2 = 17
--
-- 幂等：可重复执行（表 IF NOT EXISTS；列/字典先探测再建）
-- ══════════════════════════════════════════════════════════════════════════════

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ──────────────────────────────────────────────────────────────────────────────
-- ① cert_doc_fill_value —— 单元格级取值账本
--    一次填充中，**每一个锚点**（含表格区域每个单元格）的「值 + 来源 + 证据 + 可信度 + 人工覆盖」
--    ★ 唯一真相源：cert_doc_fill_log 的每一个计数都能由本表聚合出来（log 是物化汇总）
-- ──────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_doc_fill_value` (
  `Id`                bigint        NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义，永不入 WHERE）',
  `Code`              varchar(36)   NOT NULL              COMMENT '★业务键 GUID（唯一键）',
  `CreateTime`        datetime      NOT NULL              COMMENT '创建时间',
  `CreateBy`          varchar(64)   DEFAULT NULL          COMMENT '创建人Code',
  `UpdateTime`        datetime      DEFAULT NULL          COMMENT '更新时间',
  `UpdateBy`          varchar(64)   DEFAULT NULL          COMMENT '更新人Code',

  -- ── 身份段（P13：NOT NULL DEFAULT ''，⛔ 禁 NULL）──
  `OrgCode`           varchar(36)   NOT NULL DEFAULT ''   COMMENT '认证机构Code（冗余，便于按机构查）',
  `EnterpriseCode`    varchar(36)   NOT NULL DEFAULT ''   COMMENT '★企业Code → cert_enterprise.Code',
  `StageCode`         varchar(36)   NOT NULL DEFAULT ''   COMMENT '★阶段Code → cert_cert_stage.Code',
  `StandardCode`      varchar(36)   NOT NULL DEFAULT ''   COMMENT '★标准Code → cert_iso_standard.Code',

  -- ── 宿主（三个维度定位一行）──
  `StandardFileCode`  varchar(36)   NOT NULL DEFAULT ''   COMMENT '★目标标准文档 → cert_standard_directory_file.Code',
  `TemplateCode`      varchar(36)   NOT NULL DEFAULT ''   COMMENT '模板 → cert_doc_template.Code（固定文档时留空）',
  `AnchorCode`        varchar(36)   NOT NULL              COMMENT '★锚点 → cert_doc_template_anchor.Code（★固定文档时 = 该标准文档的标准侧 Code，见 41-01 §1.1.1）',
  `FillLogCode`       varchar(36)   NOT NULL DEFAULT ''   COMMENT '本次填充留痕 → cert_doc_fill_log.Code',

  -- ── ① 值 ──
  `ValueType`         varchar(20)   NOT NULL DEFAULT 'text' COMMENT '值类型 text/number/date/bool/enum（与锚点 ValueType 同口径）',
  `ValueText`         text                                COMMENT '文本值（ValueType=text 时用）',
  `ValueNumber`       decimal(20,4) DEFAULT NULL          COMMENT '数值（ValueType=number 时用）',
  `ValueDate`         datetime      DEFAULT NULL          COMMENT '日期（ValueType=date 时用）',
  `ValueDisplay`      varchar(500)  NOT NULL DEFAULT ''   COMMENT '★按类型格式化后的展示值（120 字符截断，界面直接用，⛔ 不在前端二次格式化）',

  -- ── ② 来源（D1 取值来源，见 22 号 §四）──
  `SourceKind`        varchar(20)   NOT NULL              COMMENT '★global/self/profile/compute/ai/manual —— D1 取值来源（sibling 一期不用）',
  `SourceLabel`       varchar(200)  NOT NULL DEFAULT ''   COMMENT '★人话来源标签（如「企业基础信息 · 企业全称」），列表页直接显示',
  `SourceDetailJson`  json          DEFAULT NULL          COMMENT '★完整来源链 {paramCode,profileCode,originalFileCode,fieldPath,pageHint,tableHint} —— 供「查看来源」下钻',

  -- ── ③ 证据 ──
  `EvidenceText`      text          DEFAULT NULL          COMMENT '证据原文片段（≤500 字），一键可看',
  `EvidencePageHint`  varchar(50)   DEFAULT NULL          COMMENT '证据位置提示（如 P3 / 第2段 / 表2行3），⛔ 不做跨文档双向定位',

  -- ── ④ 可信度（★ 单元格级，口径见 54 §4.5）──
  `Confidence`        decimal(3,2)  NOT NULL DEFAULT 1.00 COMMENT '★单元格级可信度 0.00~1.00；确定性来源恒 1.00；AI 取模型给分；人工恒 1.00；未知来源兜底 0.50',
  `ConfidenceReason`  varchar(200)  NOT NULL DEFAULT ''   COMMENT '可信度依据（如「来源为 AI 建议，未人工确认」）；1.00 时留空',

  -- ── ⑤ 人工覆盖（★「更改数据来源」「全部重写」的落点）──
  `IsOverridden`      tinyint(1)    NOT NULL DEFAULT 0    COMMENT '★是否被人工改过值或改过来源（改来源不改值时，本行值不变但本位置 1）',
  `OverrideKind`      varchar(20)   NOT NULL DEFAULT ''   COMMENT '人工干预类型 value=只改值 / source=只改来源 / both=两者都改',
  `OverrideReason`    varchar(500)  DEFAULT NULL          COMMENT '改的理由（审计要求：⛔ 改值必须填理由）',
  `OverriddenBy`      varchar(64)   DEFAULT NULL          COMMENT '干预人Code',
  `OverriddenTime`    datetime      DEFAULT NULL          COMMENT '干预时间',
  `IsPinned`          tinyint(1)    NOT NULL DEFAULT 0    COMMENT '★是否「钉住」—— 全部重写时 ⛔ 跳过本行（人工确认过的值不被自动覆盖）',

  -- ── ⑥ 状态 ──
  `FillStatus`        varchar(20)   NOT NULL DEFAULT 'filled' COMMENT 'filled=已写入 / pending=待办（无值） / kept_as_is=未命中且保留原文 / removed=按 remove 清空',
  `WriteMode`         varchar(20)   NOT NULL DEFAULT 'overwrite' COMMENT '写入方式 replace/overwrite/append/remove（快照自锚点，便于审计回放）',
  `OriginalText`      text          DEFAULT NULL          COMMENT '★替换前的原文快照（WriteMode=replace 时必落）——支撑差异比对与回滚',

  -- ── ⑦ 位置（界面定位用）──
  `LocationKind`      varchar(20)   NOT NULL DEFAULT ''   COMMENT '位置类别 body/table_cell/header/excel_cell/excel_region/word_table_region（对齐 FillLocationKind）',
  `LocationDesc`      varchar(200)  NOT NULL DEFAULT ''   COMMENT '可读位置（如「正文·第 12 段」/「Sheet1!B7」）',
  `Sort`              int           NOT NULL DEFAULT 0    COMMENT '锚点序号（来自锚点表 Sort，保证展示顺序稳定）',

  -- ── 审计段 ──
  `Remark`            varchar(500)  DEFAULT NULL,
  `IsDeleted`         tinyint(1)    NOT NULL DEFAULT 0    COMMENT '软删除',
  `DeleteBy`          varchar(64)   DEFAULT NULL,
  `DeleteTime`        datetime      DEFAULT NULL,
  `IsValid`           int           NOT NULL DEFAULT 1    COMMENT '★1有效0无效（铁律九，⛔ 禁 Enable）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  -- ★ 同一锚点在一份产物里只能有一行（表格行锚点用 Sort 区分）
  UNIQUE KEY `uk_log_anchor` (`FillLogCode`,`AnchorCode`),
  KEY `idx_scope`    (`EnterpriseCode`,`StandardCode`,`StageCode`),
  KEY `idx_std_file` (`EnterpriseCode`,`StandardFileCode`),
  KEY `idx_anchor`   (`TemplateCode`,`AnchorCode`),
  KEY `idx_status`   (`FillStatus`),
  KEY `idx_conf`     (`Confidence`),
  KEY `idx_pinned`   (`IsPinned`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='文档填充取值账本（★ 单元格级：值 + 来源 + 证据 + 可信度 + 人工覆盖）';

-- ⚠️ 索引长度自检：uk_log_anchor = (36+36)×4 = 288 B ✅ / idx_scope = 432 B ✅（上限 3072 B）
-- ⛔ 不建 (EnterpriseCode,StandardCode,StageCode,StandardFileCode,AnchorCode) 联合唯一键 —— 语义错误，
--    产物身份已由 FillLogCode 唯一确定。

-- ──────────────────────────────────────────────────────────────────────────────
-- ② cert_doc_normalize_action —— 动作留痕（★ 只追加，⛔ 永不 UPDATE/DELETE）
--    为什么不用行内 3 列：26 号 A-2「解锁也留痕」——LockedBy/LockedTime 只能记最后一次，
--    而锁定/解锁/重写是反复发生的动作。
-- ──────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_doc_normalize_action` (
  `Id`             bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID（⛔零语义）',
  `Code`           varchar(36)  NOT NULL                COMMENT '★业务键 GUID（唯一键）',
  `CreateTime`     datetime     NOT NULL                COMMENT '★动作发生时间（只追加，⛔ 永不 UPDATE）',
  `CreateBy`       varchar(64)  DEFAULT NULL            COMMENT '操作人Code',

  `OrgCode`        varchar(36)  NOT NULL DEFAULT ''     COMMENT '认证机构Code',
  `EnterpriseCode` varchar(36)  NOT NULL DEFAULT ''     COMMENT '企业Code',
  `StandardCode`   varchar(36)  NOT NULL DEFAULT ''     COMMENT '标准Code',
  `StageCode`      varchar(36)  NOT NULL DEFAULT ''     COMMENT '阶段Code',

  `ActionType`     varchar(20)  NOT NULL                COMMENT '★动作 lock/unlock/rewrite/pin/unpin/batch_run/batch_cancel（对齐 26 号 §9.4）',
  `ScopeType`      varchar(20)  NOT NULL DEFAULT ''     COMMENT '作用范围 file/folder/stage/standard/enterprise（对应 Q-I 的粒度）',
  `ScopeCode`      varchar(36)  NOT NULL DEFAULT ''     COMMENT '范围标识（文件/文件夹 Code）',
  `ScopeName`      varchar(200) NOT NULL DEFAULT ''     COMMENT '范围名称快照（如「4 记录文件」）',
  `TargetCode`     varchar(36)  NOT NULL DEFAULT ''     COMMENT '直接目标（文件 Code）；批量时留空',
  `AnchorCode`     varchar(36)  NOT NULL DEFAULT ''     COMMENT '锚点（改单元格来源时填；否则空）',
  `BeforeJson`     json         DEFAULT NULL            COMMENT '变更前快照 {IsLocked,SourceKind,ValueText,...}',
  `AfterJson`      json         DEFAULT NULL            COMMENT '变更后快照',
  `Reason`         varchar(500) DEFAULT NULL            COMMENT '★理由（unlock / rewrite ⛔ 必填，审计要求）',
  `QueueCode`      varchar(36)  NOT NULL DEFAULT ''     COMMENT '关联批次 → yzh_queue.QueueCode（batch_run / batch_cancel 时填）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_target` (`TargetCode`),
  KEY `idx_scope`  (`EnterpriseCode`,`ScopeType`,`ScopeCode`),
  KEY `idx_action` (`ActionType`),
  KEY `idx_time`   (`CreateTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='企业资料规范化动作留痕（锁定/解锁/重写/钉住/批量运行，★ 只追加永不 UPDATE）';
-- ⚠️ 本表 ⛔ 不建 IsDeleted（32 号 §八：动作历史永不可删 ⇒ 误操作只能写反向动作）

-- ──────────────────────────────────────────────────────────────────────────────
-- ③④⑤ 既有表加列（幂等：先探测再 ALTER）
-- ──────────────────────────────────────────────────────────────────────────────
DROP PROCEDURE IF EXISTS `p0_add_col`;
DELIMITER $$
CREATE PROCEDURE `p0_add_col`(IN p_table VARCHAR(64), IN p_col VARCHAR(64), IN p_ddl TEXT)
BEGIN
  IF (SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_col) = 0
  THEN
    SET @s := CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN ', p_ddl);
    PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
    SELECT CONCAT('✓ 已加列 ', p_table, '.', p_col) AS msg;
  ELSE
    SELECT CONCAT('· 已存在跳过 ', p_table, '.', p_col) AS msg;
  END IF;
END$$
DELIMITER ;

-- ── ③ cert_standard_directory_file +11 列（★ 不含 NormalizedPath，沿用既有 StoragePath）──
CALL p0_add_col('cert_standard_directory_file','IsLocked',
  "`IsLocked` tinyint(1) NOT NULL DEFAULT 0 COMMENT '★锁定后任何生成路径不得覆盖（26 号 S-6 锁定优先）' AFTER `ContractCode`");
CALL p0_add_col('cert_standard_directory_file','LockedBy',
  "`LockedBy` varchar(64) DEFAULT NULL COMMENT '锁定人Code' AFTER `IsLocked`");
CALL p0_add_col('cert_standard_directory_file','LockedTime',
  "`LockedTime` datetime DEFAULT NULL COMMENT '锁定时间' AFTER `LockedBy`");
-- ⚠️ 解锁的 By/Time/Reason ⛔ 不设列 —— 解锁是多次动作，列只能记最后一次 ⇒ 统一落 cert_doc_normalize_action
CALL p0_add_col('cert_standard_directory_file','SourceProfileCode',
  "`SourceProfileCode` varchar(36) NOT NULL DEFAULT '' COMMENT '★生成依据：产出本次文件所用的画像 → cert_enterprise_doc_profile.Code（可多值时逗号分隔，最多 3 个）' AFTER `LockedTime`");
CALL p0_add_col('cert_standard_directory_file','SourceOriginalPath',
  "`SourceOriginalPath` varchar(512) DEFAULT NULL COMMENT '★溯源展示：对应原始文件路径（冗余存储 ⚠️ ⛔ 生成过程绝不读它，违反即退回读原始文件）' AFTER `SourceProfileCode`");
CALL p0_add_col('cert_standard_directory_file','NormalizedPdfPath',
  "`NormalizedPdfPath` varchar(512) DEFAULT NULL COMMENT '产物 PDF（预览用；转换未完成时为空）。⚠️ 与 PreviewPdfPath（源文件预览）不同，两者都保留' AFTER `SourceOriginalPath`");
CALL p0_add_col('cert_standard_directory_file','NormalizedTime',
  "`NormalizedTime` datetime DEFAULT NULL COMMENT '最近一次规范化完成时间' AFTER `NormalizedPdfPath`");
CALL p0_add_col('cert_standard_directory_file','FillCompletion',
  "`FillCompletion` decimal(5,4) NOT NULL DEFAULT 0.0000 COMMENT '★完成率 0.0000~1.0000（口径见 54 §4.5；⛔ Total=0 时记 0 不记 1）' AFTER `NormalizedTime`");
CALL p0_add_col('cert_standard_directory_file','FillConfidence',
  "`FillConfidence` decimal(3,2) NOT NULL DEFAULT 1.00 COMMENT '★加权可信度 0.00~1.00（口径见 54 §4.5）' AFTER `FillCompletion`");
CALL p0_add_col('cert_standard_directory_file','FillAnchorCount',
  "`FillAnchorCount` int NOT NULL DEFAULT 0 COMMENT '锚点总数（分母快照）' AFTER `FillConfidence`");
CALL p0_add_col('cert_standard_directory_file','FillPendingCount',
  "`FillPendingCount` int NOT NULL DEFAULT 0 COMMENT '待办数（分子缺口）' AFTER `FillAnchorCount`");

-- ── ④ cert_doc_fill_log +4 列（★ 不含 StandardFileCode/TemplateCode，沿用既有 TemplateFileCode）──
CALL p0_add_col('cert_doc_fill_log','AnchorCode',
  "`AnchorCode` varchar(36) NOT NULL DEFAULT '' COMMENT '★锚点 → cert_doc_template_anchor.Code（单锚点试跑时填；整份填充时留空）' AFTER `TemplateFileCode`");
CALL p0_add_col('cert_doc_fill_log','QueueCode',
  "`QueueCode` varchar(36) NOT NULL DEFAULT '' COMMENT '★批次 → yzh_queue.QueueCode（A-3「生成批次」）' AFTER `AnchorCode`");
CALL p0_add_col('cert_doc_fill_log','QueueTaskCode',
  "`QueueTaskCode` varchar(36) NOT NULL DEFAULT '' COMMENT '★单文件任务 → yzh_queue_task.Code（审计下钻到队列明细）' AFTER `QueueCode`");
CALL p0_add_col('cert_doc_fill_log','AvgConfidence',
  "`AvgConfidence` decimal(3,2) NOT NULL DEFAULT 1.00 COMMENT '★加权可信度（可由 cert_doc_fill_value 聚合，此列为物化快照）' AFTER `Completion`");

-- ── ⑤ cert_doc_template_anchor +2 列（IsLocked 已有，⛔ 别重加）──
CALL p0_add_col('cert_doc_template_anchor','SampleData',
  "`SampleData` tinyint(1) NOT NULL DEFAULT 0 COMMENT '★模板里的示例数据（25 号 Q-5：填充前必须清空，否则新企业继承上一家企业姓名/日期）' AFTER `OriginalText`");
CALL p0_add_col('cert_doc_template_anchor','ParentAnchorCode',
  "`ParentAnchorCode` varchar(36) DEFAULT NULL COMMENT '★父锚点 → 本表 Code（表格列锚点指向表格锚点）' AFTER `SampleData`");

-- idx_parent（索引幂等）
SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='cert_doc_template_anchor' AND INDEX_NAME='idx_parent');
SET @s := IF(@idx = 0,
  'ALTER TABLE `cert_doc_template_anchor` ADD KEY `idx_parent` (`ParentAnchorCode`)',
  'SELECT ''· idx_parent 已存在跳过'' AS msg');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

DROP PROCEDURE IF EXISTS `p0_add_col`;

-- ⚠️ SampleData 默认 0 是刻意的：哪些锚点是示例数据需人工判定（不能靠正则猜 —— 真实内容也可能是人名）
--    ⇒ 本脚本不做自动标记，标记动作放后台菜单 MENU_00218 的人工操作里。

-- ──────────────────────────────────────────────────────────────────────────────
-- ⑥ 数据字典（新增 2 个）
--    ★ 本库字典主表 = Sys_Dictionary（定义）+ Sys_DictionaryList（明细，DicCode 指向定义的**大写 Code**）
--    ★ DicNo 一律小写（41-01 的大写写法在本库不成立 ⇒ 会建出一组永远查不到的字典）
-- ──────────────────────────────────────────────────────────────────────────────
SET @dictRoot := '26b0f1d2ae6c11f1953796fd503fd974';  -- 业务字典分类根（与 fill_source_kind 等同父）

INSERT INTO `Sys_Dictionary` (`Code`,`DicNo`,`DicName`,`OrderNo`,`ParentCode`,`IsValid`,`IsDeleted`,`CreateTime`)
SELECT 'FILL_VALUE_STATUS','fill_value_status','取值账本状态',450,@dictRoot,1,0,NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary` WHERE `Code`='FILL_VALUE_STATUS');

INSERT INTO `Sys_Dictionary` (`Code`,`DicNo`,`DicName`,`OrderNo`,`ParentCode`,`IsValid`,`IsDeleted`,`CreateTime`)
SELECT 'NORMALIZE_ACTION','normalize_action','规范化动作类型',460,@dictRoot,1,0,NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Sys_Dictionary` WHERE `Code`='NORMALIZE_ACTION');

-- 明细：fill_value_status（4 值）
INSERT INTO `Sys_DictionaryList` (`Code`,`DicCode`,`DicValue`,`DicName`,`OrderNo`,`IsValid`,`IsDeleted`,`CreateTime`)
SELECT * FROM (
  SELECT 'FVS_filled'      AS c,'FILL_VALUE_STATUS' AS d,'filled'      AS v,'已写入'   AS n,10 AS o,1 AS iv,0 AS dl,NOW() AS t UNION ALL
  SELECT 'FVS_pending'          ,'FILL_VALUE_STATUS'    ,'pending'          ,'待办（无值）'    ,20,1,0,NOW() UNION ALL
  SELECT 'FVS_kept_as_is'       ,'FILL_VALUE_STATUS'    ,'kept_as_is'       ,'未命中保留原文'  ,30,1,0,NOW() UNION ALL
  SELECT 'FVS_removed'          ,'FILL_VALUE_STATUS'    ,'removed'          ,'按 remove 清空'  ,40,1,0,NOW()
) x WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `DicCode`='FILL_VALUE_STATUS');

-- 明细：normalize_action（7 值）
INSERT INTO `Sys_DictionaryList` (`Code`,`DicCode`,`DicValue`,`DicName`,`OrderNo`,`IsValid`,`IsDeleted`,`CreateTime`)
SELECT * FROM (
  SELECT 'NA_lock'        AS c,'NORMALIZE_ACTION' AS d,'lock'         AS v,'锁定'      AS n,10 AS o,1 AS iv,0 AS dl,NOW() AS t UNION ALL
  SELECT 'NA_unlock'          ,'NORMALIZE_ACTION'    ,'unlock'          ,'解锁'          ,20,1,0,NOW() UNION ALL
  SELECT 'NA_rewrite'         ,'NORMALIZE_ACTION'    ,'rewrite'         ,'重新生成'      ,30,1,0,NOW() UNION ALL
  SELECT 'NA_pin'             ,'NORMALIZE_ACTION'    ,'pin'             ,'钉住锚点'      ,40,1,0,NOW() UNION ALL
  SELECT 'NA_unpin'           ,'NORMALIZE_ACTION'    ,'unpin'           ,'取消钉住'      ,50,1,0,NOW() UNION ALL
  SELECT 'NA_batch_run'       ,'NORMALIZE_ACTION'    ,'batch_run'       ,'批量运行'      ,60,1,0,NOW() UNION ALL
  SELECT 'NA_batch_cancel'    ,'NORMALIZE_ACTION'    ,'batch_cancel'    ,'取消批次'      ,70,1,0,NOW()
) x WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList` WHERE `DicCode`='NORMALIZE_ACTION');

-- ══════════════════════════════════════════════════════════════════════════════
-- 验证（G3 出口门）—— 逐条核对，⛔ 不许「跑完就算」
-- ══════════════════════════════════════════════════════════════════════════════

-- ① 两张新表（应输出 2 行）
SELECT TABLE_NAME, TABLE_COMMENT FROM information_schema.TABLES
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('cert_doc_fill_value','cert_doc_normalize_action');

-- ② 新增列合计（应输出 17 行）
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE() AND (
   (TABLE_NAME='cert_standard_directory_file' AND COLUMN_NAME IN
     ('IsLocked','LockedBy','LockedTime','SourceProfileCode','SourceOriginalPath',
      'NormalizedPdfPath','NormalizedTime','FillCompletion','FillConfidence','FillAnchorCount','FillPendingCount'))
   OR (TABLE_NAME='cert_doc_fill_log' AND COLUMN_NAME IN ('AnchorCode','QueueCode','QueueTaskCode','AvgConfidence'))
   OR (TABLE_NAME='cert_doc_template_anchor' AND COLUMN_NAME IN ('SampleData','ParentAnchorCode'))
 ) ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- ③ 字典（应输出 2 + 4 + 7 = 13 行）
SELECT 'DEF' AS kind, Code, DicNo, DicName, OrderNo FROM Sys_Dictionary
 WHERE Code IN ('FILL_VALUE_STATUS','NORMALIZE_ACTION')
UNION ALL
SELECT 'ITEM', Code, DicCode, CONCAT(DicValue,' / ',DicName), OrderNo FROM Sys_DictionaryList
 WHERE DicCode IN ('FILL_VALUE_STATUS','NORMALIZE_ACTION')
 ORDER BY kind DESC, Code;

-- ④ ★ 小写列名验证（必须输出 0 行）
--    ⚠️ 必须带 CONVERT(... USING utf8mb4) COLLATE utf8mb4_bin —— information_schema.COLUMN_NAME
--       排序规则大小写不敏感，直接 NOT REGEXP '^[A-Z]' 会**永远返回 0 行**（假阴性）
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME IN ('cert_doc_fill_value','cert_doc_normalize_action')
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]';

-- ⑤ ⛔ 确认没有误加同义列（应输出 0 行）
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE()
   AND ((TABLE_NAME='cert_standard_directory_file' AND COLUMN_NAME='NormalizedPath')
     OR (TABLE_NAME='cert_doc_fill_log' AND COLUMN_NAME IN ('StandardFileCode','TemplateCode')));

-- ⑥ ⛔ 确认没有误加 Enable 列（应输出 0 行，铁律九）
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME IN ('cert_doc_fill_value','cert_doc_normalize_action')
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'Enable';
