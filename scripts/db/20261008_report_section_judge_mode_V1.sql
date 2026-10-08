-- ============================================================================
-- 20261008_report_section_judge_mode_V1.sql
-- 报告章节定义 —— 新增 JudgeMode 判定方式字段
--
-- 【业务背景】
--   自动报告生成需要区分"AI 自动判定 / 人工判定 / 半自动"三类章节：
--   - auto：体系文件覆盖度等可从企业上传资料自动分析
--   - manual：现场作业一致性、员工访谈等必须人工判断
--   - semi：AI 初判 + 人工确认
--
-- 【对照】
--   NC 规则定义（ValidationRule.JudgeMode）已具备完全同构字段，
--   本报告章节补齐对齐，使报告执行引擎可据此跳过不可自动化的章节。
--
-- 幂等：支持重复执行（ON DUPLICATE KEY UPDATE / IF NOT EXISTS）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① cert_report_section 新增 JudgeMode 列
-- ────────────────────────────────────────────────────────────────────────────
ALTER TABLE `cert_report_section`
  ADD COLUMN `JudgeMode` varchar(20) NOT NULL DEFAULT 'auto'
    COMMENT '判定方式（auto=AI 自动 / manual=人工 / semi=半自动）'
    AFTER `ClauseCode`;

-- ────────────────────────────────────────────────────────────────────────────
-- ② 数据字典：报告章节判定方式
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `ParentCode`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`, `Remark`)
VALUES
  ('REPORT_JUDGE_MODE', '报告章节判定方式', 'report_judge_mode', '26b0f1d2ae6c11f1953796fd503fd974', 480, 1, 0, NOW(), 'seed_report_judge_mode', 'cert_report_section.JudgeMode 下拉选项，与 NC 规则定义 JudgeMode 语义对齐')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`), `ParentCode` = VALUES(`ParentCode`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('JM_auto',   'REPORT_JUDGE_MODE', 'auto',   'AI 自动判定',                 10, 1, 0, NOW(), 'seed_report_judge_mode'),
  ('JM_manual', 'REPORT_JUDGE_MODE', 'manual', '人工判定（需现场/访谈）',    20, 1, 0, NOW(), 'seed_report_judge_mode'),
  ('JM_semi',   'REPORT_JUDGE_MODE', 'semi',   '半自动（AI 初判+人工确认）', 30, 1, 0, NOW(), 'seed_report_judge_mode')
ON DUPLICATE KEY UPDATE
  `DicValue` = VALUES(`DicValue`), `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 验证
-- ────────────────────────────────────────────────────────────────────────────
SELECT 'JudgeMode 列' AS check_item,
  CASE WHEN COUNT(*) > 0 THEN '✅ 已添加' ELSE '❌ 缺失' END AS status
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_report_section' AND COLUMN_NAME = 'JudgeMode';

SELECT '字典定义' AS check_item,
  CASE WHEN COUNT(*) > 0 THEN '✅ 已添加' ELSE '❌ 缺失' END AS status
FROM `Sys_Dictionary` WHERE `Code` = 'REPORT_JUDGE_MODE';

SELECT '字典明细' AS check_item, COUNT(*) AS item_count
FROM `Sys_DictionaryList` WHERE `DicCode` = 'REPORT_JUDGE_MODE';
